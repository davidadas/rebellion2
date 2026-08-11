using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Converts game results into faction messages and delivers them to each faction.
    /// </summary>
    public class MessageSystem
    {
        private readonly GameRoot _game;
        private readonly MessageFactory _messageFactory;

        public event Action<MessageDeliveredResult> MessageDelivered;
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Initializes a message system for the supplied game state and message definitions.
        /// </summary>
        /// <param name="game">The game state used to resolve message context.</param>
        /// <param name="definitions">The message definitions used to build messages.</param>
        public MessageSystem(GameRoot game, IEnumerable<MessageDefinition> definitions)
        {
            _game = game;
            _messageFactory = new MessageFactory(definitions);
        }

        /// <summary>
        /// Creates and delivers faction messages for the supplied game results.
        /// </summary>
        /// <param name="results">The game results to process.</param>
        public List<GameResult> ProcessResults(IEnumerable<GameResult> results)
        {
            List<GameResult> deliveredResults = new List<GameResult>();
            foreach (MessageDelivery delivery in _messageFactory.CreateMessages(results, _game))
            {
                if (delivery.Recipient == null || delivery.Message == null)
                    continue;

                delivery.Message.CreatedTick = _game.CurrentTick;
                delivery.Recipient.AddMessage(delivery.Message);
                MessageDeliveredResult delivered = new MessageDeliveredResult
                {
                    Recipient = delivery.Recipient,
                    Message = delivery.Message,
                    NotificationType = delivery.NotificationType,
                    AdvisorSubjectNotification = delivery.AdvisorSubjectNotification,
                    AdvisorSubjectTypeID = delivery.AdvisorSubjectTypeID,
                    AdvisorNotification = delivery.AdvisorNotification,
                    Tick = _game.CurrentTick,
                };
                deliveredResults.Add(delivered);
                MessageDelivered?.Invoke(delivered);
            }

            if (deliveredResults.Count > 0)
                ResultsProduced?.Invoke(deliveredResults);
            return deliveredResults;
        }

        /// <summary>
        /// Advances time-based message lifecycle state for the current game tick.
        /// </summary>
        public void ProcessTick()
        {
            RemoveExpiredMessages();
        }

        /// <summary>
        /// Removes faction messages older than the configured retention period.
        /// </summary>
        private void RemoveExpiredMessages()
        {
            int retentionTicks = _game.Config.Messages.RetentionTicks;
            foreach (Faction faction in _game.Factions)
            {
                if (faction?.Messages == null)
                    continue;

                foreach (List<Message> messages in faction.Messages.Values)
                {
                    messages?.RemoveAll(message =>
                        message != null
                        && (long)message.CreatedTick + retentionTicks < _game.CurrentTick
                    );
                }
            }
        }
    }
}
