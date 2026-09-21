using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Delivers faction messages and advances their retention lifecycle.
    /// </summary>
    public class MessageCommands
    {
        private readonly GameRoot _game;
        private readonly MessageFactory _messageFactory;

        /// <summary>
        /// Creates message delivery operations for the supplied game and shared message factory.
        /// </summary>
        /// <param name="game">The game state used to resolve message context.</param>
        /// <param name="messageFactory">The factory that resolves authored message templates.</param>
        public MessageCommands(GameRoot game, MessageFactory messageFactory)
        {
            _game = game;
            _messageFactory = messageFactory;
        }

        /// <summary>
        /// Resolves and delivers authored text through the same durable path as automatic messages.
        /// </summary>
        /// <param name="definition">The authored text and resolved background and voice assets.</param>
        /// <param name="recipient">The faction receiving the message.</param>
        /// <param name="subject">The subject used for text and navigation.</param>
        /// <param name="relatedSubject">The related subject used for text.</param>
        /// <param name="location">The selected event location.</param>
        /// <param name="overlayImagePath">The resolved subject overlay.</param>
        /// <param name="notification">The authored advisor presentation.</param>
        /// <param name="sourceEventInstanceID">The event that authored the message.</param>
        /// <returns>The factual delivery results.</returns>
        public List<GameResult> DeliverAuthored(
            MessageDefinition definition,
            Faction recipient,
            ISceneNode subject = null,
            ISceneNode relatedSubject = null,
            Planet location = null,
            string overlayImagePath = null,
            AdvisorNotification notification = null,
            string sourceEventInstanceID = null
        )
        {
            return Deliver(
                new[]
                {
                    _messageFactory.CreateAuthoredMessage(
                        definition,
                        recipient,
                        subject,
                        relatedSubject,
                        location,
                        overlayImagePath,
                        notification,
                        sourceEventInstanceID
                    ),
                }
            );
        }

        /// <summary>
        /// Prepares the complete input batch before attaching messages in order and reporting delivery.
        /// </summary>
        /// <param name="deliveries">The prepared messages, which may resolve lazily during enumeration.</param>
        /// <returns>The factual delivery results.</returns>
        public List<GameResult> Deliver(IEnumerable<MessageDelivery> deliveries)
        {
            List<MessageDelivery> batch = deliveries.ToList();
            List<GameResult> deliveredResults = new List<GameResult>();
            foreach (MessageDelivery delivery in batch)
            {
                if (delivery?.Recipient == null)
                    continue;

                Message message = _messageFactory.CreateMessage(delivery);
                message.CreatedTick = _game.CurrentTick;
                delivery.Recipient.AddMessage(message);
                MessageDeliveredResult delivered = new MessageDeliveredResult
                {
                    Recipient = delivery.Recipient,
                    Message = message,
                    NotificationType = delivery.NotificationType,
                    AdvisorSubjectNotification = delivery.AdvisorSubjectNotification,
                    AdvisorSubjectTypeID = delivery.AdvisorSubjectTypeID,
                    AdvisorNotification = delivery.AdvisorNotification,
                    SourceEventInstanceID = delivery.SourceEventInstanceID,
                    Tick = _game.CurrentTick,
                };
                deliveredResults.Add(delivered);
            }
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
            foreach (Faction faction in _game.GetFactions())
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
