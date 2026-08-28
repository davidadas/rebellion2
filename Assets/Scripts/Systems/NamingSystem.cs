using System;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;

namespace Rebellion.Systems
{
    /// <summary>
    /// Assigns faction-specific names to eligible game entities.
    /// </summary>
    public sealed class NamingSystem
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates a naming system.
        /// </summary>
        /// <param name="game">The active game.</param>
        public NamingSystem(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Assigns names for factions whose ship naming is automated.
        /// </summary>
        public void ProcessTick()
        {
            foreach (Faction faction in _game.GetFactions())
                ProcessFaction(faction);
        }

        /// <summary>
        /// Assigns sequential faction names to eligible capital ships.
        /// </summary>
        /// <param name="faction">The faction whose ships should be named.</param>
        /// <returns>The number of ships named.</returns>
        public int ProcessFaction(Faction faction)
        {
            if (faction == null)
                throw new ArgumentNullException(nameof(faction));
            if (!faction.IsAIControlled() && !faction.ManageNaming)
                return 0;

            int namedShipCount = 0;
            foreach (CapitalShip ship in faction.GetOwnedUnitsByType<CapitalShip>())
            {
                if (
                    ship.HasAssignedName
                    || ship.ManufacturingStatus != ManufacturingStatus.Complete
                    || string.IsNullOrWhiteSpace(ship.ShipNamePoolID)
                )
                    continue;

                if (faction.TryTakeNextShipName(ship.ShipNamePoolID, out string shipName))
                    ship.AssignName(shipName);
                else
                    ship.AssignName(faction.TakeNextGenericShipName(ship));

                namedShipCount++;
            }

            return namedShipCount;
        }
    }
}
