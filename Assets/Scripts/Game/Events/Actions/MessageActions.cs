using System;
using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Presentation.Advisor;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Suppresses one matching automatically generated message in the current result batch.
    /// </summary>
    [PersistableObject(Name = "SuppressNextAutomaticMessage")]
    public sealed class SuppressNextAutomaticMessageAction : GameAction
    {
        [PersistableAttribute(Name = "Type")]
        public MessageResultType MessageType { get; set; }

        [PersistableAttribute]
        public string RecipientFactionInstanceID { get; set; }

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            if (MessageType == MessageResultType.None)
                throw new InvalidOperationException(
                    "SuppressNextAutomaticMessage requires a concrete message result type."
                );
            GameResult targetResult = context.Activation?.TriggerResult;
            if (targetResult == null)
                throw new InvalidOperationException(
                    "SuppressNextAutomaticMessage requires a triggering result."
                );

            Faction recipient = string.IsNullOrWhiteSpace(RecipientFactionInstanceID)
                ? null
                : game.GetFactionByOwnerInstanceID(RecipientFactionInstanceID);
            if (!string.IsNullOrWhiteSpace(RecipientFactionInstanceID) && recipient == null)
                throw new InvalidOperationException(
                    $"SuppressNextAutomaticMessage could not resolve faction '{RecipientFactionInstanceID}'."
                );

            return new List<GameResult>
            {
                new SuppressNextAutomaticMessageResult
                {
                    MessageType = MessageType,
                    Recipient = recipient,
                    TargetResult = targetResult,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Selects one authored narrative fragment from current simulation state.
    /// </summary>
    [PersistableObject(Name = "ConditionalBody")]
    public sealed class ConditionalMessageBody
    {
        public List<GameConditional> Conditions { get; set; } = new List<GameConditional>();
        public string Body { get; set; }
        public string ElseBody { get; set; }

        /// <summary>
        /// Selects the primary or fallback body from the current conditions.
        /// </summary>
        /// <param name="context">The current condition context and event bindings.</param>
        /// <returns>The body selected by the condition results.</returns>
        public string Resolve(GameConditionContext context)
        {
            return Conditions.TrueForAll(condition => condition.IsMet(context)) ? Body : ElseBody;
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
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<ConditionalMessageBody> ConditionalBodies { get; set; } =
            new List<ConditionalMessageBody>();
        public MessageBackgroundImage BackgroundImage { get; set; }
        public MessageImage OverlayImage { get; set; }
        public MessageAudio BackgroundAudio { get; set; }
        public MessageOfficerVoice OfficerVoice { get; set; }
        public AdvisorNotification AdvisorNotification { get; set; }

        /// <summary>
        /// Resolves the authored references and emits presentation-neutral narrative data.
        /// </summary>
        /// <param name="context">The dependencies and activation data for this action.</param>
        /// <returns>A single narrative message result.</returns>
        public override List<GameResult> Execute(GameActionContext context)
        {
            return ExecuteCore(context);
        }

        /// <summary>
        /// Builds the configured narrative result from the optional triggering result.
        /// </summary>
        private List<GameResult> ExecuteCore(GameActionContext context)
        {
            GameRoot game = context.Game;
            GameResult triggerResult = context.Activation?.TriggerResult;
            IRandomNumberProvider provider = context.Random;
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
            GameConditionContext conditionContext = new GameConditionContext(
                game,
                context.Activation
            );
            foreach (ConditionalMessageBody segment in ConditionalBodies)
                bodyTemplate += segment.Resolve(conditionContext) ?? string.Empty;
            string backgroundAudioPath = MessageMediaResolver.Resolve(BackgroundAudio, context);
            string imagePath = MessageMediaResolver.Resolve(BackgroundImage, context);

            return new List<GameResult>
            {
                new MessageRequestedResult
                {
                    Recipient = recipient,
                    SubjectNode = subject,
                    RelatedSubjectNode = relatedSubject,
                    Location = location,
                    MessageType = MessageType,
                    Subject = Subject,
                    Body = bodyTemplate,
                    BackgroundImageKey = BackgroundImage?.Key,
                    BackgroundImagePath = imagePath,
                    OverlayImagePath = OverlayImage?.Path ?? (subject as Officer)?.MessageImagePath,
                    BackgroundAudioPath = backgroundAudioPath,
                    OfficerVoicePath = OfficerVoice?.Resolve(subject as Officer, provider),
                    AdvisorNotification = AdvisorNotification,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    internal static class MessageMediaResolver
    {
        internal static string Resolve(MessageBackgroundImage image, GameActionContext context)
        {
            if (image == null)
                return null;
            int sourceCount =
                (string.IsNullOrWhiteSpace(image.Key) ? 0 : 1)
                + (string.IsNullOrWhiteSpace(image.Path) ? 0 : 1)
                + (string.IsNullOrWhiteSpace(image.Binding) ? 0 : 1);
            if (sourceCount != 1)
                throw new InvalidOperationException(
                    "BackgroundImage requires exactly one of Key, Path, or Binding."
                );
            return ResolvePath(image.Path, image.Binding, context);
        }

        internal static string Resolve(MessageAudio audio, GameActionContext context)
        {
            if (audio == null)
                return null;
            int sourceCount =
                (string.IsNullOrWhiteSpace(audio.Path) ? 0 : 1)
                + (string.IsNullOrWhiteSpace(audio.Binding) ? 0 : 1);
            if (sourceCount != 1)
                throw new InvalidOperationException(
                    "BackgroundAudio requires exactly one of Path or Binding."
                );
            return ResolvePath(audio.Path, audio.Binding, context);
        }

        private static string ResolvePath(string path, string binding, GameActionContext context)
        {
            if (!string.IsNullOrWhiteSpace(path))
                return path;
            if (context.Activation?.TryGetBindingReference(binding, out string boundPath) == true)
                return boundPath;
            throw new InvalidOperationException(
                $"Message media could not resolve binding '{binding}'."
            );
        }
    }
}
