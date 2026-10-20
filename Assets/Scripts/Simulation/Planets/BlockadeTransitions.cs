using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Records blockade transitions at the operation that changes a planet's orbital state.
    /// </summary>
    internal static class BlockadeTransitions
    {
        /// <summary>
        /// Adds a blockade-change result when an operation changed the planet's blockade state.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="planet">The planet whose orbital state changed.</param>
        /// <param name="wasBlockaded">Whether the planet was blockaded before the operation.</param>
        /// <param name="results">The result collection receiving the transition.</param>
        /// <returns>True when a blockade-change result was added.</returns>
        internal static bool AddChangedResult(
            GameRoot game,
            Planet planet,
            bool wasBlockaded,
            ICollection<GameResult> results
        )
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (planet == null)
                return false;

            bool isBlockaded = planet.IsBlockaded();
            if (wasBlockaded == isBlockaded)
                return false;

            Fleet blockadingFleet = isBlockaded
                ? planet
                    .GetChildren<Fleet>()
                    .FirstOrDefault(fleet =>
                        fleet.Movement == null
                        && fleet.OwnerInstanceID != planet.OwnerInstanceID
                        && fleet.HasOperationalCapitalShips()
                    )
                : null;
            if (isBlockaded && blockadingFleet == null)
                return false;

            game.RecordEconomyMutation();
            results.Add(
                new BlockadeChangedResult
                {
                    Planet = planet,
                    BlockadingFleet = blockadingFleet,
                    Blockaded = isBlockaded,
                    Tick = game.CurrentTick,
                }
            );
            return true;
        }
    }
}
