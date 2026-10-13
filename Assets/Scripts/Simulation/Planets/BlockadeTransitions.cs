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
    /// Records blockade starts at the operation that changes a planet's orbital state.
    /// </summary>
    internal static class BlockadeTransitions
    {
        /// <summary>
        /// Adds a blockade-start result when an operation changed the planet from unblocked to blockaded.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="planet">The planet whose orbital state changed.</param>
        /// <param name="wasBlockaded">Whether the planet was blockaded before the operation.</param>
        /// <param name="results">The result collection receiving the transition.</param>
        /// <returns>True when a blockade-start result was added.</returns>
        internal static bool AddStartedResult(
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
            if (planet == null || wasBlockaded || !planet.IsBlockaded())
                return false;

            Fleet blockadingFleet = planet
                .GetChildren<Fleet>()
                .FirstOrDefault(fleet =>
                    fleet.Movement == null
                    && fleet.OwnerInstanceID != planet.OwnerInstanceID
                    && fleet.HasOperationalCapitalShips()
                );
            if (blockadingFleet == null)
                return false;

            results.Add(
                new BlockadeChangedResult
                {
                    Planet = planet,
                    BlockadingFleet = blockadingFleet,
                    Blockaded = true,
                    Tick = game.CurrentTick,
                }
            );
            return true;
        }
    }
}
