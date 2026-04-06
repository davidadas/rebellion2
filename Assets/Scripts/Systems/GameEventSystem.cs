using System.Collections.Generic;
using Rebellion.Core.Simulation;
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

        public GameEventSystem(GameRoot game)
        {
            _game = game;
        }

        private List<GameResult> ProcessEvent(GameEvent gameEvent, IRandomNumberProvider provider)
        {
            if (!gameEvent.AreConditionsMet(_game))
                return new List<GameResult>();

            GameLogger.Log($"Executing game event: {gameEvent.GetDisplayName()}");
            List<GameResult> results = gameEvent.Execute(_game, provider);
            _game.AddCompletedEvent(gameEvent);
            return results;
        }

        /// <summary>
        /// Processes all eligible events and returns the aggregate results.
        /// </summary>
        public List<GameResult> ProcessEvents(
            List<GameEvent> gameEvents,
            IRandomNumberProvider provider
        )
        {
            List<GameResult> allResults = new List<GameResult>();
            List<GameEvent> eventsToRemove = new List<GameEvent>();

            foreach (GameEvent gameEvent in gameEvents)
            {
                allResults.AddRange(ProcessEvent(gameEvent, provider));

                if (!gameEvent.IsRepeatable)
                    eventsToRemove.Add(gameEvent);
            }

            foreach (GameEvent eventToRemove in eventsToRemove)
                _game.RemoveEvent(eventToRemove);

            return allResults;
        }
    }
}
