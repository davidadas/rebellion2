using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Routes ownership changes to officer loyalty operations in result order.</summary>
    public sealed class OfficerLoyaltyObserver
    {
        private readonly OfficerLoyaltyCommands _commands;

        /// <summary>Creates the ownership-change listener for officer loyalty.</summary>
        /// <param name="commands">The loyalty operations for this game.</param>
        public OfficerLoyaltyObserver(OfficerLoyaltyCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>Applies loyalty shifts for incoming owners in the existing batch order.</summary>
        /// <param name="results">The ownership changes to process.</param>
        /// <returns>No follow-up results; loyalty changes update authoritative state.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
                _commands.ApplyControlShift(result?.NewOwner);

            return new List<GameResult>();
        }
    }
}
