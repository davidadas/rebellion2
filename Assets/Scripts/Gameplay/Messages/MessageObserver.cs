using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Selects automatic messages from a settled result batch at its existing message-release boundary.
    /// </summary>
    public sealed class MessageObserver
    {
        private readonly GameRoot _game;
        private readonly MessageFactory _messageFactory;
        private readonly MessageCommands _commands;

        /// <summary>
        /// Connects automatic message selection to durable delivery.
        /// </summary>
        /// <param name="game">The game supplying message context.</param>
        /// <param name="messageFactory">The shared factory that selects and resolves messages.</param>
        /// <param name="commands">The operations that persist messages on recipients.</param>
        public MessageObserver(
            GameRoot game,
            MessageFactory messageFactory,
            MessageCommands commands
        )
        {
            _game = game;
            _messageFactory = messageFactory;
            _commands = commands;
        }

        /// <summary>
        /// Creates and delivers faction messages for the supplied game results.
        /// </summary>
        /// <param name="results">The game results to process.</param>
        /// <returns>The result of process results.</returns>
        public List<GameResult> ProcessResults(IEnumerable<GameResult> results)
        {
            return ProcessResults(results, null);
        }

        /// <summary>
        /// Captures undelivered campaign messages while the combat decision suspends the tick.
        /// </summary>
        /// <param name="results">The settled facts that will produce messages after combat.</param>
        /// <returns>The prepared deliveries in the message factory's order.</returns>
        internal List<ActiveBattle.DeferredMessage> CaptureDeferredMessages(
            IEnumerable<GameResult> results
        )
        {
            List<ActiveBattle.DeferredMessage> messages = new();
            foreach (MessageDelivery delivery in CreateDeliveries(results))
            {
                if (delivery?.Recipient == null)
                    continue;
                delivery.ExistingMessage = delivery.ExistingMessage?.CreateCopy();
                messages.Add(
                    new()
                    {
                        RecipientID = delivery.Recipient.InstanceID,
                        Content = _messageFactory.CreateMessage(delivery),
                        NotificationType = delivery.NotificationType,
                        AdvisorSubjectNotification = delivery.AdvisorSubjectNotification,
                        AdvisorSubjectTypeID = delivery.AdvisorSubjectTypeID,
                        AdvisorNotification = delivery.AdvisorNotification,
                        SourceEventInstanceID = delivery.SourceEventInstanceID,
                    }
                );
            }
            return messages;
        }

        /// <summary>
        /// Delivers queued campaign messages along with the newly resolved result batch.
        /// </summary>
        /// <param name="results">The newly resolved facts.</param>
        /// <param name="deferred">The deliveries restored from the combat save.</param>
        /// <returns>The factual deliveries that can trigger game reactions.</returns>
        internal List<GameResult> ProcessResults(
            IEnumerable<GameResult> results,
            IReadOnlyList<ActiveBattle.DeferredMessage> deferred
        )
        {
            if (deferred == null || deferred.Count == 0)
                return _commands.Deliver(CreateDeliveries(results));

            List<MessageDelivery> deliveries = new(deferred.Count);
            foreach (ActiveBattle.DeferredMessage saved in deferred)
                deliveries.Add(
                    new()
                    {
                        Recipient = _game.GetFactionByOwnerInstanceID(saved.RecipientID),
                        Message = saved.Content,
                        NotificationType = saved.NotificationType,
                        AdvisorSubjectNotification = saved.AdvisorSubjectNotification,
                        AdvisorSubjectTypeID = saved.AdvisorSubjectTypeID,
                        AdvisorNotification = saved.AdvisorNotification,
                        SourceEventInstanceID = saved.SourceEventInstanceID,
                    }
                );
            deliveries.AddRange(CreateDeliveries(results));
            return _commands.Deliver(deliveries);
        }

        /// <summary>
        /// Selects messages from the automatic facts of one settled result batch.
        /// </summary>
        /// <param name="results">The settled facts.</param>
        /// <returns>The resolved delivery content.</returns>
        private List<MessageDelivery> CreateDeliveries(IEnumerable<GameResult> results) =>
            _messageFactory.CreateMessages(
                (results ?? Array.Empty<GameResult>()).Where(result =>
                    result != null
                    && result.ReportContext == null
                    && string.IsNullOrWhiteSpace(result.SourceEventInstanceID)
                ),
                _game
            );
    }
}
