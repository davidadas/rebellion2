using System;
using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Runs delegated faction work after planetary ownership transitions have fully settled.
    /// </summary>
    public sealed class FactionAutomationObserver : IResultObserver, IDisposable
    {
        private readonly FactionAutomationCommands _commands;
        private IDisposable _subscription;

        /// <summary>
        /// Creates an observer for ownership-driven faction automation.
        /// </summary>
        /// <param name="commands">The delegated faction operations.</param>
        public FactionAutomationObserver(FactionAutomationCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Registers for settled planetary ownership results.
        /// </summary>
        /// <param name="results">The bus that delivers completed ownership transitions.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException(
                    "Faction automation observer is already connected."
                );

            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Observe<PlanetOwnershipChangedResult>(HandleResults);
        }

        /// <summary>
        /// Stops receiving ownership results.
        /// </summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Runs enabled automation once for each final owner that gained a planet in the batch.
        /// </summary>
        /// <param name="results">The settled planetary ownership transitions.</param>
        public void HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            HashSet<string> processedFactionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                Faction newOwner = result?.NewOwner;
                if (
                    newOwner == null
                    || result.Planet?.GetOwnerInstanceID() != newOwner.InstanceID
                    || !processedFactionIds.Add(newOwner.InstanceID)
                )
                {
                    continue;
                }

                _commands.ProcessFaction(newOwner);
            }
        }
    }
}
