using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Routes completed simulation results to authored game-event triggers.
    /// </summary>
    public sealed class GameEventObserver : IResultObserver, IDisposable
    {
        private readonly GameEventCommands _commands;
        private IDisposable _subscription;

        /// <summary>
        /// Creates the game-event result listener.
        /// </summary>
        /// <param name="commands">The game-event operations for the active game.</param>
        public GameEventObserver(GameEventCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Registers the game-event callback with the result bus.
        /// </summary>
        /// <param name="results">The bus that delivers completed simulation results.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException("Game-event observer is already connected.");

            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<GameResult>(HandleResults);
        }

        /// <summary>
        /// Stops receiving completed simulation results.
        /// </summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Applies authored events triggered by the supplied result batch.
        /// </summary>
        /// <param name="results">The completed results to inspect.</param>
        /// <returns>The results produced by activated events.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameResult> results)
        {
            return _commands.ProcessTriggeredEvents(results);
        }
    }
}
