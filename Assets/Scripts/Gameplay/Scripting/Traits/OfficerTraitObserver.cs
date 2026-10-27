using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay.Scripting.Traits
{
    /// <summary>
    /// Connects trait-owned result reactions to the session's existing result pipeline.
    /// </summary>
    public sealed class OfficerTraitObserver : IResultObserver, IDisposable
    {
        private readonly OfficerTraitCommands _commands;
        private IDisposable _subscription;

        /// <summary>
        /// Creates the trait result listener for one game session.
        /// </summary>
        /// <param name="commands">The trait script activation operations.</param>
        public OfficerTraitObserver(OfficerTraitCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Subscribes trait reactions after event reactions and before subsequent gameplay observers.
        /// </summary>
        /// <param name="results">The current session's result bus.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException("Officer-trait observer is already connected.");
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<GameResult>(HandleResults);
        }

        /// <summary>
        /// Stops receiving results when this session's service scope is disposed.
        /// </summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Activates authored behaviors that match the incoming result wave.
        /// </summary>
        /// <param name="results">The completed gameplay results.</param>
        /// <returns>The resulting trait reactions.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameResult> results) =>
            _commands.ProcessResults(results);
    }
}
