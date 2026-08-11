using System;
using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Suppresses one matching automatically generated message in the current result batch.
    /// </summary>
    [PersistableObject(Name = "SuppressNextMessage")]
    public sealed class SuppressNextMessageAction : GameAction
    {
        [PersistableAttribute(Name = "Type")]
        public MessageResultType MessageType { get; set; }

        [PersistableAttribute]
        public string RecipientFactionInstanceID { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            if (MessageType == MessageResultType.None)
                throw new InvalidOperationException(
                    "SuppressNextMessage requires a concrete message result type."
                );

            Faction recipient = string.IsNullOrWhiteSpace(RecipientFactionInstanceID)
                ? null
                : game.GetFactionByOwnerInstanceID(RecipientFactionInstanceID);
            if (!string.IsNullOrWhiteSpace(RecipientFactionInstanceID) && recipient == null)
                throw new InvalidOperationException(
                    $"SuppressNextMessage could not resolve faction '{RecipientFactionInstanceID}'."
                );

            return new List<GameResult>
            {
                new SuppressNextMessageResult
                {
                    MessageType = MessageType,
                    Recipient = recipient,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Selects one authored narrative fragment from current simulation state.
    /// </summary>
    [PersistableObject(Name = "BodySegment")]
    public sealed class NarrativeBodySegment
    {
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public string Body { get; set; }
        public string ElseBody { get; set; }

        /// <summary>
        /// Selects the primary or fallback body from the current conditions.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="triggerResult">The result that activated the containing event.</param>
        /// <returns>The body selected by the condition results.</returns>
        public string Resolve(GameRoot game, GameResult triggerResult = null)
        {
            return Conditionals.TrueForAll(condition => condition.IsMet(game, triggerResult))
                ? Body
                : ElseBody;
        }
    }

    /// <summary>
    /// Emits a normal faction message from presentation data authored with a game event.
    /// </summary>
    [PersistableObject(Name = "SendMessage")]
    public sealed class SendMessageAction : GameAction
    {
        [PersistableAttribute]
        public string RecipientFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string RecipientUnitInstanceID { get; set; }

        [PersistableAttribute]
        public string SubjectInstanceID { get; set; }

        [PersistableAttribute]
        public string RelatedSubjectInstanceID { get; set; }

        [PersistableAttribute]
        public string LocationInstanceID { get; set; }

        [PersistableAttribute(Name = "Type")]
        public MessageType MessageType { get; set; } = MessageType.Advice;
        public string Title { get; set; }
        public string Body { get; set; }
        public List<NarrativeBodySegment> BodySegments { get; set; } =
            new List<NarrativeBodySegment>();
        public MessageBackgroundImage BackgroundImage { get; set; }
        public string OverlayImagePath { get; set; }
        public string AudioPath { get; set; }
        public string OfficerVoicePath { get; set; }
        public AdvisorNotification AdvisorNotification { get; set; }

        /// <summary>
        /// Resolves the authored references and emits presentation-neutral narrative data.
        /// </summary>
        /// <param name="game">The game state used to resolve faction and scene-node IDs.</param>
        /// <returns>A single narrative message result.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return ExecuteCore(game, null);
        }

        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            return ExecuteCore(game, context?.TriggerResult);
        }

        /// <summary>
        /// Builds the configured narrative result from the optional triggering result.
        /// </summary>
        private List<GameResult> ExecuteCore(GameRoot game, GameResult triggerResult)
        {
            ISceneNode subject = game.GetSceneNodeByInstanceID<ISceneNode>(SubjectInstanceID);
            ISceneNode relatedSubject = game.GetSceneNodeByInstanceID<ISceneNode>(
                RelatedSubjectInstanceID
            );
            ISceneNode recipientUnit = game.GetSceneNodeByInstanceID<ISceneNode>(
                RecipientUnitInstanceID
            );
            string recipientId = RecipientFactionInstanceID;
            if (string.IsNullOrWhiteSpace(recipientId))
                recipientId = recipientUnit?.OwnerInstanceID ?? subject?.OwnerInstanceID;

            if (string.IsNullOrWhiteSpace(recipientId))
                throw new InvalidOperationException(
                    "SendMessage could not resolve its recipient faction."
                );

            Faction recipient = game.GetFactionByOwnerInstanceID(recipientId);
            Planet location = game.GetSceneNodeByInstanceID<Planet>(LocationInstanceID);
            if (location == null && subject != null)
                location = subject as Planet ?? subject.GetParentOfType<Planet>();

            string bodyTemplate = Body ?? string.Empty;
            foreach (NarrativeBodySegment segment in BodySegments)
                bodyTemplate += segment.Resolve(game, triggerResult) ?? string.Empty;
            OfficerEncounterResult encounter = triggerResult as OfficerEncounterResult;
            string voicePath = AudioPath ?? encounter?.AudioPath;
            string imagePath = BackgroundImage?.Path ?? encounter?.ImagePath;

            ValidateAdvisorNotification();
            return new List<GameResult>
            {
                new NarrativeMessageResult
                {
                    Recipient = recipient,
                    SubjectNode = subject,
                    RelatedSubjectNode = relatedSubject,
                    Location = location,
                    MessageType = MessageType,
                    Subject = Title,
                    Body = bodyTemplate,
                    BackgroundImageKey = BackgroundImage?.Key,
                    BackgroundImagePath = imagePath,
                    OverlayImagePath = OverlayImagePath ?? (subject as Officer)?.MessageImagePath,
                    AudioPath = voicePath,
                    OfficerVoicePath = OfficerVoicePath,
                    AdvisorNotification = AdvisorNotification,
                    Tick = game.CurrentTick,
                },
            };
        }

        private void ValidateAdvisorNotification()
        {
            AdvisorNotification?.Validate();
        }
    }
}
