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
    public enum EventVariableOperation
    {
        Set,
        Add,
        Minimum,
        Maximum,
    }

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
        public string ImageKey { get; set; }
        public string ImagePath { get; set; }
        public string OverlayImagePath { get; set; }
        public string VoicePath { get; set; }
        public string OfficerVoicePath { get; set; }
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
                    ImageKey = ImageKey,
                    ImagePath = ImagePath,
                    OverlayImagePath = OverlayImagePath,
                    VoicePath = VoicePath,
                    OfficerVoicePath = OfficerVoicePath,
                    AdvisorNotification = AdvisorNotification,
                    AdvisorSubjectNotification = AdvisorSubjectNotification,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Executes one of two authored action lists based on data-defined conditions.
    /// </summary>
    [PersistableObject(Name = "Conditional")]
    public class ConditionalAction : GameAction
    {
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
        public List<GameAction> ElseActions { get; set; } = new List<GameAction>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameAction> selected = Conditionals.TrueForAll(condition => condition.IsMet(game))
                ? Actions
                : ElseActions;
            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in selected)
                results.AddRange(action.Execute(game, provider));
            return results;
        }
    }

    /// <summary>
    /// Mutates a persistent integer used to coordinate data-defined story stages.
    /// </summary>
    [PersistableObject(Name = "SetEventVariable")]
    public class SetEventVariableAction : GameAction
    {
        public string Key { get; set; }
        public EventVariableOperation Operation { get; set; }
        public int Value { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            int previousValue = game.GetEventVariable(Key);
            int currentValue = Operation switch
            {
                EventVariableOperation.Set => Value,
                EventVariableOperation.Add => checked(previousValue + Value),
                EventVariableOperation.Minimum => Math.Min(previousValue, Value),
                EventVariableOperation.Maximum => Math.Max(previousValue, Value),
                _ => throw new InvalidOperationException(
                    $"Unsupported event variable operation '{Operation}'."
                ),
            };
            game.SetEventVariable(Key, currentValue);
            return new List<GameResult>
            {
                new EventVariableChangedResult
                {
                    Key = Key,
                    PreviousValue = previousValue,
                    CurrentValue = currentValue,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Requests authoritative movement for one movable scene node.
    /// </summary>
    [PersistableObject(Name = "RequestMovement")]
    public class RequestMovementAction : GameAction
    {
        public string UnitInstanceID { get; set; }
        public string DestinationInstanceID { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            IMovable unit = game.GetSceneNodeByInstanceID<IMovable>(UnitInstanceID);
            ContainerNode destination = game.GetSceneNodeByInstanceID<ContainerNode>(
                DestinationInstanceID
            );
            if (unit == null)
                throw new InvalidOperationException(
                    $"RequestMovement could not resolve movable unit '{UnitInstanceID}'."
                );
            if (destination == null)
                throw new InvalidOperationException(
                    $"RequestMovement could not resolve destination '{DestinationInstanceID}'."
                );

            return new List<GameResult>
            {
                new UnitMovementRequestedResult
                {
                    Unit = unit,
                    Destination = destination,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Requests a timed, guaranteed Force-training journey managed by MissionSystem.
    /// </summary>
    [PersistableObject(Name = "StartScriptedTraining")]
    public class StartScriptedTrainingAction : GameAction
    {
        public string TraineeInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public int CompletionBonusPercent { get; set; }
        public string CompletionVariableKey { get; set; }
        public int CompletionVariableValue { get; set; } = 1;
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer trainee = game.GetSceneNodeByInstanceID<Officer>(TraineeInstanceID);
            if (trainee == null)
                throw new InvalidOperationException(
                    $"StartScriptedTraining could not resolve trainee '{TraineeInstanceID}'."
                );

            return new List<GameResult>
            {
                new ScriptedTrainingRequestedResult
                {
                    Trainee = trainee,
                    DurationTicks = DurationTicks,
                    CompletionBonusPercent = CompletionBonusPercent,
                    CompletionVariableKey = CompletionVariableKey,
                    CompletionVariableValue = CompletionVariableValue,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Increases one officer's Force value using the greatest configured reward component.
    /// </summary>
    [PersistableObject(Name = "IncreaseOfficerForce")]
    public class IncreaseOfficerForceAction : GameAction
    {
        public string OfficerInstanceID { get; set; }
        public string ReferenceOfficerInstanceID { get; set; }
        public int MinimumIncrease { get; set; }
        public int CurrentRankPercent { get; set; }
        public int PositiveRankGapPercent { get; set; }
        public bool SuppressRankChangeMessage { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = ResolveOfficer(game, OfficerInstanceID, nameof(OfficerInstanceID));
            Officer reference = string.IsNullOrWhiteSpace(ReferenceOfficerInstanceID)
                ? null
                : ResolveOfficer(
                    game,
                    ReferenceOfficerInstanceID,
                    nameof(ReferenceOfficerInstanceID)
                );
            int previousRank = officer.ForceRank;
            int increase = Math.Max(MinimumIncrease, previousRank * CurrentRankPercent / 100);
            if (reference != null)
            {
                int positiveGap = Math.Max(0, reference.ForceRank - previousRank);
                increase = Math.Max(increase, positiveGap * PositiveRankGapPercent / 100);
            }

            officer.ForceValue += increase;
            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = increase,
                    PreviousForceRank = previousRank,
                    CurrentForceRank = officer.ForceRank,
                    SuppressRankChangeMessage = SuppressRankChangeMessage,
                    Tick = game.CurrentTick,
                },
            };
        }

        private static Officer ResolveOfficer(GameRoot game, string instanceId, string memberName)
        {
            return game.GetSceneNodeByInstanceID<Officer>(instanceId)
                ?? throw new InvalidOperationException(
                    $"IncreaseOfficerForce could not resolve {memberName} '{instanceId}'."
                );
        }
    }

    /// <summary>
    /// Applies a data-authored inclusive random injury range to one officer.
    /// </summary>
    [PersistableObject(Name = "ApplyOfficerInjury")]
    public class ApplyOfficerInjuryAction : GameAction
    {
        public string OfficerInstanceID { get; set; }
        public int MinimumInjury { get; set; }
        public int MaximumInjury { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"ApplyOfficerInjury could not resolve officer '{OfficerInstanceID}'."
                );

            int injury = provider.NextInt(MinimumInjury, checked(MaximumInjury + 1));
            officer.ApplyInjury(injury, game.Config.Recovery.MaxInjuryPoints);
            return new List<GameResult>
            {
                new OfficerInjuredResult
                {
                    Officer = officer,
                    Severity = injury,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
}
