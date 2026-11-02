using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Routes successful linked-officer encounter evasions to Force growth in batch order.
    /// </summary>
    public sealed class JediObserver : IResultObserver, IDisposable
    {
        private readonly JediCommands _commands;
        private IDisposable _subscription;

        /// <summary>
        /// Creates the jedi result listener.
        /// </summary>
        /// <param name="commands">The jedi operations for this game.</param>
        public JediObserver(JediCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Registers the linked-officer encounter callback with the result bus.
        /// </summary>
        /// <param name="results">The bus that delivers linked-officer encounter results.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException("Jedi observer is already connected.");
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<DuelResult>(HandleResults);
        }

        /// <summary>
        /// Stops receiving linked-officer encounter results.
        /// </summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Applies Force growth when an eligible encountered officer evades capture.
        /// </summary>
        /// <param name="results">The linked-officer encounter results to inspect.</param>
        /// <returns>Any Force experience results produced by successful evasions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<DuelResult> results)
        {
            List<GameResult> forceResults = new List<GameResult>();
            if (results == null)
                return forceResults;

            foreach (DuelResult result in results)
            {
                if (result?.EncounteredOfficerEvadedCapture == true)
                    forceResults.AddRange(
                        _commands.ApplyEncounterForceGrowth(result.EncounteredOfficer)
                    );
            }

            return forceResults;
        }
    }
}
