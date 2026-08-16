using System.Collections.Generic;
using System.Linq;
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
            IEnumerable<GameResult> automaticResults = resultBatch.Where(
                IsEligibleForAutomaticMessage
            );
            IEnumerable<MessageRequestedResult> authoredRequests =
                resultBatch.OfType<MessageRequestedResult>();
            IEnumerable<MessageRequestedResult> requests = _messageFactory
                .CreateMessages(automaticResults, _game)
                .Concat(_messageFactory.CreateAuthoredMessages(authoredRequests));
            List<GameResult> deliveredResults = new List<GameResult>();
            foreach (MessageRequestedResult request in requests)
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
        private Message CreateMessage(MessageRequestedResult request) =>
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
                CreatedTick = _game.CurrentTick,
            };

        /// <summary>
        /// Returns whether an ordinary simulation result may produce an automatic message.
        /// Authored event effects must request their messages explicitly.
        /// </summary>
        private static bool IsEligibleForAutomaticMessage(GameResult result) =>
            result is not MessageRequestedResult
            && string.IsNullOrWhiteSpace(result.SourceEventInstanceID);

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
