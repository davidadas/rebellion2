using System.Collections.Generic;
using System.Drawing;
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
        /// Phase 1: Cancel competing missions targeting this planet.
        /// Phase 2: Transfer buildings to the new owner.
        /// Phase 3: Clear manufacturing queues.
        /// Phase 4: Evict all enemy units (including in-transit).
        /// Phase 5: Change planet owner.
        /// </summary>
        public void TransferPlanet(Planet planet, Faction newOwner)
        {
            CancelCompetingMissions(planet, newOwner.InstanceID);
            TransferBuildings(planet, newOwner);
            _manufacturingSystem.ClearQueuesOnOwnershipChange(planet);
            EvictEnemyUnits(planet, newOwner.InstanceID);
            _game.ChangeUnitOwnership(planet, newOwner.InstanceID);
        }

        private void CancelCompetingMissions(Planet planet, string newOwnerID)
        {
            List<Mission> competing = _game.GetSceneNodesByType<Mission>()
                .Where(m =>
                    m.CanceledOnOwnershipChange
                    && m.OwnerInstanceID != newOwnerID
                    && m.GetParentOfType<Planet>() == planet
                )
                .ToList();

            foreach (Mission mission in competing)
            {
                foreach (IMissionParticipant participant in mission.GetAllParticipants())
                {
                    Planet fallback = FindNearestFactionPlanet(participant);
                    if (fallback != null)
                        _movementSystem.RequestMove(participant, fallback);
                }

                _game.DetachNode(mission);
            }
        }

        private void TransferBuildings(Planet planet, Faction newOwner)
        {
            foreach (Building building in planet.GetChildren<Building>(b => true, recurse: false))
            {
                building.AllowedOwnerInstanceIDs = new List<string> { newOwner.InstanceID };
                _game.ChangeUnitOwnership(building, newOwner.InstanceID);
            }
        }

        private void EvictEnemyUnits(Planet planet, string newOwnerID)
        {
            List<IMovable> enemies = planet
                .GetChildren<IMovable>(m => m.GetOwnerInstanceID() != newOwnerID, recurse: false)
                .ToList();

            foreach (IMovable unit in enemies)
            {
                Planet fallback = FindNearestFactionPlanet(unit);
                if (fallback != null)
                    _movementSystem.RequestMove(unit, fallback);
            }
        }

        private Planet FindNearestFactionPlanet(IMovable unit)
        {
            string ownerID = unit.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerID))
                return null;

            Planet current = unit.GetParentOfType<Planet>();

            return _game.GetSceneNodesByType<Planet>()
                .Where(p => p.GetOwnerInstanceID() == ownerID && p != current)
                .OrderBy(p =>
                {
                    Point pos = unit.GetPosition();
                    Point ppos = p.GetPosition();
                    double dx = ppos.X - pos.X;
                    double dy = ppos.Y - pos.Y;
                    return dx * dx + dy * dy;
                })
                .FirstOrDefault();
        }
    }
}
