using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay.GameEvents
{
    /// <summary>
    /// Routes completed simulation results to authored game-event triggers.
    /// </summary>
    public sealed class GameEventObserver : IResultObserver, IDisposable
    {
        private readonly GameEventCommands _commands;
        private readonly PlanetaryControlCommands _planetaryControl;
        private readonly ConditionalWeakTable<
            PlanetOwnershipChangedResult,
            object
        > _ownershipChanges = new();
        private IDisposable _subscription;

        /// <summary>
        /// Creates the game-event result listener.
        /// </summary>
        /// <param name="commands">The game-event operations for the active game.</param>
        /// <param name="planetaryControl">The producer of immediate planetary ownership transitions.</param>
        public GameEventObserver(
            GameEventCommands commands,
            PlanetaryControlCommands planetaryControl
        )
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _planetaryControl =
                planetaryControl ?? throw new ArgumentNullException(nameof(planetaryControl));
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
            _planetaryControl.OwnershipChanged += ObserveOwnershipChange;
        }

        /// <summary>
        /// Stops receiving completed simulation results.
        /// </summary>
        public void Dispose()
        {
            _subscription?.Dispose();
            _planetaryControl.OwnershipChanged -= ObserveOwnershipChange;
        }

        /// <summary>
        /// Applies authored events triggered by the supplied result batch.
        /// </summary>
        /// <param name="results">The completed results to inspect.</param>
        /// <returns>The results produced by activated events.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameResult> results)
        {
            foreach (
                PlanetOwnershipChangedResult change in results?.OfType<PlanetOwnershipChangedResult>()
                    ?? Enumerable.Empty<PlanetOwnershipChangedResult>()
            )
                ObserveOwnershipChange(change);
            return _commands.ProcessTriggeredEvents(results);
        }

        /// <summary>
        /// Synchronizes scope membership once at the transition, or on publication for other producers.
        /// </summary>
        /// <param name="change">The completed ownership transition.</param>
        private void ObserveOwnershipChange(PlanetOwnershipChangedResult change)
        {
            if (_ownershipChanges.TryGetValue(change, out _))
                return;

            _ownershipChanges.Add(change, new object());
            _commands.SynchronizeScopedTargets(new[] { change });
        }
    }
}
