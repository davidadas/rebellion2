using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Extensions;

namespace Rebellion.Systems
{
    /// <summary>
    /// Single authority for transferring ownership of planets and units.
    /// Called by MissionSystem on diplomacy success and CombatSystem on conquest.
    /// </summary>
    public class OwnershipSystem
    {
        private readonly GameRoot _game;
        private readonly MovementSystem _movementSystem;
        private readonly ManufacturingSystem _manufacturingSystem;

        public OwnershipSystem(
            GameRoot game,
            MovementSystem movementSystem,
            ManufacturingSystem manufacturingSystem
        )
        {
            _game = game;
            _movementSystem = movementSystem;
            _manufacturingSystem = manufacturingSystem;
        }

        /// <summary>
        /// Transfers a planet to a new owner.
        /// </summary>
        public void TransferPlanet(Planet planet, Faction newOwner)
        {
            // Cancel competing missions targeting this planet.
            CancelCompetingMissions(planet, newOwner.InstanceID);

            // Transfer buildings to the new owner.
            TransferBuildings(planet, newOwner);

            // Clear Manufacturing queues.
            _manufacturingSystem.ClearQueuesOnOwnershipChange(planet);

            // Evict enemy units.
            EvictEnemyUnits(planet, newOwner.InstanceID);

            // Finaly, change the planet owner.
            _game.ChangeUnitOwnership(planet, newOwner.InstanceID);
        }

        private void CancelCompetingMissions(Planet planet, string newOwnerID)
        {
            List<Mission> competing = _game
                .GetSceneNodesByType<Mission>()
                .Where(m =>
                    m.CanceledOnOwnershipChange
                    && m.OwnerInstanceID != newOwnerID
                    && m.GetParentOfType<Planet>() == planet
                )
                .ToList();

            foreach (Mission mission in competing)
            {
                foreach (IMissionParticipant participant in mission.GetAllParticipants())
                    _movementSystem.EvacuateToNearestFriendlyPlanet(participant);

                _game.DetachNode(mission);
            }
        }

        private void TransferBuildings(Planet planet, Faction newOwner)
        {
            foreach (Building building in planet.GetChildren<Building>(_ => true, recurse: false))
            {
                building.AllowedOwnerInstanceIDs = new List<string> { newOwner.InstanceID };
                _game.ChangeUnitOwnership(building, newOwner.InstanceID);
            }
        }

        private void EvictEnemyUnits(Planet planet, string newOwnerID)
        {
            List<IMovable> enemies = planet
                .GetChildren<IMovable>(
                    m =>
                        m.GetOwnerInstanceID() != newOwnerID && m is not Fleet && m is not Building,
                    recurse: false
                )
                .ToList();

            foreach (IMovable unit in enemies)
                _movementSystem.EvacuateToNearestFriendlyPlanet(unit);
        }
    }
}
