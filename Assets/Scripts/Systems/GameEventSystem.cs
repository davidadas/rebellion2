using System.Collections.Generic;
using Rebellion.Game;
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
        public List<GameResult> ProcessEvents(List<GameEvent> gameEvents)
        {
            List<GameResult> allResults = new List<GameResult>();
            List<GameEvent> eventsToRemove = new List<GameEvent>();

            foreach (GameEvent gameEvent in gameEvents)
            {
                allResults.AddRange(ProcessEvent(gameEvent));

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
        private List<GameResult> ProcessEvent(GameEvent gameEvent)
        {
            if (!gameEvent.AreConditionsMet(_game))
                return new List<GameResult>();

            GameLogger.Log($"Executing game event: {gameEvent.GetDisplayName()}");
            List<GameResult> results = gameEvent.Execute(_game, _provider);
            _game.AddCompletedEvent(gameEvent);
            return results;
        }
    }
}
