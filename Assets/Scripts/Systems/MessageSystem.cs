using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Converts game results into faction messages and delivers them to each faction.
    /// </summary>
    public class MessageSystem : IGameRequestHandler<MessageDeliveryRequest>
    {
        private readonly GameRoot _game;
        private readonly MessageFactory _messageFactory;

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
            GameResult[] resultBatch =
                results?.Where(result => result != null).ToArray()
                ?? System.Array.Empty<GameResult>();
            IEnumerable<GameResult> automaticResults = resultBatch.Where(result =>
                string.IsNullOrWhiteSpace(result.SourceEventInstanceID)
            );
            return Deliver(_messageFactory.CreateMessages(automaticResults, _game));
        }

        /// <summary>
        /// Delivers event-authored messages through the same durable message path as automatic messages.
        /// </summary>
        /// <param name="requests">The authored delivery requests.</param>
        /// <returns>The factual delivery results.</returns>
        public List<GameResult> HandleRequests(IReadOnlyList<MessageDeliveryRequest> requests)
        {
            return Deliver(_messageFactory.CreateAuthoredMessages(requests));
        }

        /// <summary>
        /// Persists resolved messages on their recipients and reports each successful delivery.
        /// </summary>
        /// <param name="requests">The resolved messages to deliver.</param>
        /// <returns>The factual delivery results.</returns>
        private List<GameResult> Deliver(IEnumerable<MessageDeliveryRequest> requests)
        {
            List<GameResult> deliveredResults = new List<GameResult>();
            foreach (MessageDeliveryRequest request in requests)
            {
                if (request?.Recipient == null)
                    continue;

                Message message = CreateMessage(request);
                request.Recipient.AddMessage(message);
                MessageDeliveredResult delivered = new MessageDeliveredResult
                {
                    Recipient = request.Recipient,
                    Message = message,
                    NotificationType = request.NotificationType,
                    AdvisorSubjectNotification = request.AdvisorSubjectNotification,
                    AdvisorSubjectTypeID = request.AdvisorSubjectTypeID,
                    AdvisorNotification = request.AdvisorNotification,
                    SourceEventInstanceID = request.SourceEventInstanceID,
                    Tick = _game.CurrentTick,
                };
                deliveredResults.Add(delivered);
            }
            return deliveredResults;
        }

        /// <summary>
        /// Constructs the durable message represented by one resolved delivery request.
        /// </summary>
        /// <param name="request">The semantic and presentation data to persist.</param>
        /// <returns>The message ready to attach to the recipient faction.</returns>
        private Message CreateMessage(MessageDeliveryRequest request) =>
            new Message(request.MessageType, request.Subject, request.Body)
            {
                ResultType = request.ResultType,
                DisplayName = request.Subject,
                BackgroundImageKey = request.BackgroundImageKey,
                DisplayImagePath = request.BackgroundImagePath,
                OverlayImagePath = request.OverlayImagePath,
                BackgroundAudioPath = request.BackgroundAudioPath,
                OfficerVoicePath = request.OfficerVoicePath,
                EventLocationInstanceID = request.EventLocationInstanceID,
                NavigationTargetInstanceID = request.NavigationTargetInstanceID,
                NavigationSecondaryTargetInstanceID = request.NavigationSecondaryTargetInstanceID,
                MissionInstanceID = request.MissionInstanceID,
                CombatReport = request.CombatReport,
                CreatedTick = _game.CurrentTick,
            };

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
