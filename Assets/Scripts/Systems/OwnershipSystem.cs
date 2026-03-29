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
        private readonly GameRoot game;
        private readonly MovementSystem movementSystem;

        public OwnershipSystem(GameRoot game, MovementSystem movementSystem)
        {
            this.game = game;
            this.movementSystem = movementSystem;
        }

        /// <summary>
        /// Transfers a planet to a new owner.
        /// Phase 1: Cancel competing diplomacy missions targeting this planet.
        /// Phase 2: Transfer buildings to the new owner.
        /// Phase 3: Evict enemy units.
        /// Phase 4: Change planet owner.
        /// </summary>
        public void TransferPlanet(Planet planet, Faction newOwner)
        {
            CancelCompetingMissions(planet, newOwner.InstanceID);
            TransferBuildings(planet, newOwner);
            EvictEnemyUnits(planet, newOwner.InstanceID);
            game.ChangeUnitOwnership(planet, newOwner.InstanceID);
        }

        private void CancelCompetingMissions(Planet planet, string newOwnerID)
        {
            List<Mission> competing = game.GetSceneNodesByType<Mission>()
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
                        movementSystem.RequestMove(participant, fallback);
                }

                game.DetachNode(mission);
            }
        }

        private void TransferBuildings(Planet planet, Faction newOwner)
        {
            foreach (Building building in planet.GetChildren<Building>(b => true, recurse: false))
            {
                building.AllowedOwnerInstanceIDs = new List<string> { newOwner.InstanceID };
                game.ChangeUnitOwnership(building, newOwner.InstanceID);
            }
        }

        private void EvictEnemyUnits(Planet planet, string newOwnerID)
        {
            List<Fleet> enemyFleets = planet
                .GetChildren<Fleet>(f => f.OwnerInstanceID != newOwnerID, recurse: false)
                .ToList();

            foreach (Fleet fleet in enemyFleets)
            {
                Planet fallback = FindNearestFactionPlanet(fleet);
                if (fallback != null)
                    movementSystem.RequestMove(fleet, fallback);
            }
        }

        private Planet FindNearestFactionPlanet(IMovable unit)
        {
            string ownerID = unit.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerID))
                return null;

            Planet current = unit.GetParentOfType<Planet>();

            return game.GetSceneNodesByType<Planet>()
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
