using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Processes game events each tick and returns results for notification/logging.
    /// </summary>
    public class GameEventSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates a new GameEventSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for stochastic event actions.</param>
        public GameEventSystem(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game;
            _provider = provider;
        }

        /// <summary>
        /// Processes all eligible events and returns the aggregate results.
        /// </summary>
        /// <param name="gameEvents">The events to evaluate.</param>
        /// <returns>Results produced by events that executed.</returns>
        public List<GameResult> ProcessEvents(List<GameEvent> gameEvents)
        {
            List<GameResult> allResults = new List<GameResult>();
            List<GameEvent> eventsToRemove = new List<GameEvent>();

            foreach (GameEvent gameEvent in gameEvents)
            {
                if (!TryProcessEvent(gameEvent, out List<GameResult> results))
                    continue;

                allResults.AddRange(results);
                if (!gameEvent.IsRepeatable)
                    eventsToRemove.Add(gameEvent);
            }

            foreach (GameEvent eventToRemove in eventsToRemove)
                _game.RemoveEvent(eventToRemove);

            return allResults;
        }

        /// <summary>
        /// Executes a single game event if its conditions are met.
        /// </summary>
        /// <param name="gameEvent">The event to process.</param>
        /// <param name="results">Receives results produced by the event.</param>
        /// <returns>True when the event executed; otherwise false.</returns>
        private bool TryProcessEvent(GameEvent gameEvent, out List<GameResult> results)
        {
            if (!gameEvent.AreConditionsMet(_game))
            {
                results = new List<GameResult>();
                return false;
            }

            GameLogger.Log($"Executing game event: {gameEvent.GetDisplayName()}");
            results = gameEvent.Execute(_game, _provider);
            _game.AddCompletedEvent(gameEvent);
            return true;
        }
    }
}
