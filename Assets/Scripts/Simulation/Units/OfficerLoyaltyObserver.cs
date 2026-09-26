using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Routes ownership changes to officer loyalty operations in result order.</summary>
    public sealed class OfficerLoyaltyObserver : IResultObserver, IDisposable
    {
        private readonly OfficerLoyaltyCommands _commands;
        private IDisposable _subscription;

        /// <summary>Creates the ownership-change listener for officer loyalty.</summary>
        /// <param name="commands">The loyalty operations for this game.</param>
        public OfficerLoyaltyObserver(OfficerLoyaltyCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>Registers the ownership-change callback with the result bus.</summary>
        /// <param name="results">The bus that delivers ownership changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException(
                    "Officer loyalty observer is already connected."
                );
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<PlanetOwnershipChangedResult>(HandleResults);
        }

        /// <summary>Stops receiving ownership changes.</summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>Applies loyalty shifts for incoming owners in the existing batch order.</summary>
        /// <param name="results">The ownership changes to process.</param>
        /// <returns>No follow-up results; loyalty changes update authoritative state.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                if (result?.PreviousOwner?.InstanceID == result?.NewOwner?.InstanceID)
                    continue;

                _commands.ApplyControlShift(result?.NewOwner);
            }

            return new List<GameResult>();
        }
    }
}
