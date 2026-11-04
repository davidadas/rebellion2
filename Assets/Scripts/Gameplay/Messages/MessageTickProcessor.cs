using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Util.Logging;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Advances time-based message lifecycle state.
    /// </summary>
    internal sealed class MessageTickProcessor : ITickProcessor
    {
        /// <summary>
        /// Creates message tick processing for the active game.
        /// </summary>
        public MessageTickProcessor() { }

        /// <summary>
        /// Removes messages older than the configured retention period.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>No gameplay results.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            using var measurement = PerformanceLogger.Measure(
                "Game system tick",
                subjectType: typeof(MessageTickProcessor)
            );
            int retentionTicks = game.Config.Messages.RetentionTicks;
            foreach (Faction faction in game.GetFactions())
            {
                using var factionMeasurement = PerformanceLogger.Measure(
                    "Faction system tick",
                    faction?.PeekInstanceID(),
                    subjectType: typeof(MessageTickProcessor)
                );
                if (faction?.Messages == null)
                    continue;

                foreach (List<Message> messages in faction.Messages.Values)
                {
                    messages?.RemoveAll(message =>
                        message != null
                        && (long)message.CreatedTick + retentionTicks < game.CurrentTick
                    );
                }
            }

            return Array.Empty<GameResult>();
        }
    }
}
