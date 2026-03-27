using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Util.Common;

/// <summary>
/// Manages game events and their scheduling.
/// </summary>
namespace Rebellion.Systems
{
    public class GameEventSystem
    {
        private GameRoot game;

        public GameEventSystem(GameRoot game)
        {
            this.game = game;
        }

        private void ProcessEvent(GameEvent gameEvent, IRandomNumberProvider provider)
        {
            if (gameEvent.AreConditionsMet(game))
            {
                GameLogger.Log($"Executing game event: {gameEvent.GetDisplayName()}");
                gameEvent.Execute(game, provider);
                game.AddCompletedEvent(gameEvent);
            }
        }

        /// <summary>
        /// Processes game events that are scheduled for this tick.
        /// </summary>
        /// <param name="gameEvents">List of events to process.</param>
        /// <param name="provider">Random number provider for random event actions.</param>
        public void ProcessEvents(List<GameEvent> gameEvents, IRandomNumberProvider provider)
        {
            List<GameEvent> eventsToRemove = new List<GameEvent>();

            foreach (GameEvent gameEvent in gameEvents)
            {
                ProcessEvent(gameEvent, provider);

                if (!gameEvent.IsRepeatable)
                {
                    if (!gameEvent.IsRepeatable)
                    {
                        eventsToRemove.Add(gameEvent);
                    }
                }
            }

            // Remove events that are no longer needed.
            foreach (GameEvent eventToRemove in eventsToRemove)
            {
                game.RemoveEvent(eventToRemove);
            }
        }
    }
}
