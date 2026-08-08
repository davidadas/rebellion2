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
    [PersistableObject(Name = "RandomOutcome")]
    public class RandomOutcomeAction : GameAction
    {
        [PersistableAttribute(Name = "Value")]
        public double Probability { get; set; }

        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        public RandomOutcomeAction()
            : base() { }

        /// <summary>
        /// Rolls against the configured probability; on success, executes a uniformly-chosen
        /// child action and returns its results. Otherwise returns no results.
        /// </summary>
        /// <param name="game">The game state passed to the chosen child action.</param>
        /// <returns>The results produced by the chosen action, or an empty list if the roll failed.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (provider.NextDouble() < Probability)
            {
                return Actions[provider.NextInt(0, Actions.Count)].Execute(game, provider);
            }

            return new List<GameResult>();
        }
    }

    [PersistableObject(Name = "TriggerDuel")]
    public class TriggerDuelAction : GameAction
    {
        public List<string> AttackerInstanceIDs { get; set; } = new List<string>();
        public List<string> DefenderInstanceIDs { get; set; } = new List<string>();

        public TriggerDuelAction()
            : base() { }

        /// <summary>
        /// Resolves the referenced attacker and defender officers and emits a
        /// <see cref="DuelTriggeredResult"/>. Duel resolution itself is not yet implemented.
        /// </summary>
        /// <param name="game">The game state used to resolve officer references.</param>
        /// <returns>A single <see cref="DuelTriggeredResult"/> describing the participants.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            // @TODO: Implement duel resolution
            return new List<GameResult>
            {
                new DuelTriggeredResult
                {
                    Attackers = AttackerInstanceIDs.ConvertAll(id =>
                        game.GetSceneNodeByInstanceID<Officer>(id)
                    ),
                    Defenders = DefenderInstanceIDs.ConvertAll(id =>
                        game.GetSceneNodeByInstanceID<Officer>(id)
                    ),
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    [PersistableObject(Name = "TriggerEvent")]
    public class TriggerEventAction : GameAction
    {
        public string EventInstanceID { get; set; }

        public TriggerEventAction()
            : base() { }

        /// <summary>
        /// Resolves the referenced <see cref="GameEvent"/> and runs its action chain.
        /// Falls back to <see cref="GameRoot.Random"/> if no provider has been injected.
        /// </summary>
        /// <param name="game">The game state used to resolve the event.</param>
        /// <returns>The results produced by the triggered event's actions.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);
            return gameEvent.Execute(game, provider ?? game.Random);
        }
    }

    /// <summary>
    /// Emits a normal faction message from presentation data authored with a game event.
    /// </summary>
    [PersistableObject(Name = "NarrativeMessage")]
    public class NarrativeMessageAction : GameAction
    {
        public string RecipientFactionInstanceID { get; set; }
        public string RecipientUnitInstanceID { get; set; }
        public string SubjectInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Advice;
        public string TitleTemplate { get; set; }
        public string BodyTemplate { get; set; }
        public string ImagePath { get; set; }
        public string OverlayImagePath { get; set; }
        public string VoicePath { get; set; }
        public AdvisorNotificationCode AdvisorNotification { get; set; }
        public AdvisorSubjectNotification AdvisorSubjectNotification { get; set; }

        /// <summary>
        /// Resolves the authored references and emits presentation-neutral narrative data.
        /// </summary>
        /// <param name="game">The game state used to resolve faction and scene-node IDs.</param>
        /// <returns>A single narrative message result.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            ISceneNode subject = game.GetSceneNodeByInstanceID<ISceneNode>(SubjectInstanceID);
            ISceneNode recipientUnit = game.GetSceneNodeByInstanceID<ISceneNode>(
                RecipientUnitInstanceID
            );
            string recipientId = RecipientFactionInstanceID;
            if (string.IsNullOrWhiteSpace(recipientId))
                recipientId = recipientUnit?.OwnerInstanceID ?? subject?.OwnerInstanceID;

            if (string.IsNullOrWhiteSpace(recipientId))
                throw new InvalidOperationException(
                    "NarrativeMessage could not resolve its recipient faction."
                );

            Faction recipient = game.GetFactionByOwnerInstanceID(recipientId);
            Planet location = game.GetSceneNodeByInstanceID<Planet>(LocationInstanceID);
            if (location == null && subject != null)
                location = subject as Planet ?? subject.GetParentOfType<Planet>();

            return new List<GameResult>
            {
                new NarrativeMessageResult
                {
                    Recipient = recipient,
                    Subject = subject,
                    Location = location,
                    MessageType = MessageType,
                    TitleTemplate = TitleTemplate,
                    BodyTemplate = BodyTemplate,
                    ImagePath = ImagePath,
                    OverlayImagePath = OverlayImagePath,
                    VoicePath = VoicePath,
                    AdvisorNotification = AdvisorNotification,
                    AdvisorSubjectNotification = AdvisorSubjectNotification,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
}
