using System;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Assigns faction-specific names to eligible game entities.
    /// </summary>
    public sealed class NamingCommands
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates naming commands for one game.
        /// </summary>
        /// <param name="game">The active game.</param>
        public NamingCommands(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
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
            if (!_game.IsFactionAIControlled(faction) && !faction.ManageNaming)
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
