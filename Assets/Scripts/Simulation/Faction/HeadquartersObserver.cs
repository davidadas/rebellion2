using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Routes arrival and ownership result batches to headquarters operations.
    /// </summary>
    public sealed class HeadquartersObserver
    {
        private readonly HeadquartersCommands _commands;

        /// <summary>
        /// Creates the headquarters result observer.
        /// </summary>
        /// <param name="commands">The headquarters operations for the active game.</param>
        public HeadquartersObserver(HeadquartersCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Applies headquarters arrivals in their existing batch order.
        /// </summary>
        /// <param name="results">The completed unit arrivals.</param>
        /// <returns>No additional results, as headquarters arrival only updates its marker.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<UnitArrivedResult> results)
        {
            foreach (UnitArrivedResult result in results ?? Array.Empty<UnitArrivedResult>())
            {
                if (result?.Unit is Building headquarters)
                    _commands.Arrive(headquarters, result.Destination);
            }
            return new List<GameResult>();
        }

        /// <summary>
        /// Applies ownership changes and collects headquarters consequences in their existing batch order.
        /// </summary>
        /// <param name="results">The completed planetary ownership changes.</param>
        /// <returns>The headquarters capture and destruction results for the whole batch.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            List<GameResult> reactions = new();
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                reactions.AddRange(
                    _commands.UpdateOwnership(
                        result?.Planet,
                        result?.PreviousOwner,
                        result?.NewOwner
                    )
                );
            }
            return reactions;
        }
    }
}
