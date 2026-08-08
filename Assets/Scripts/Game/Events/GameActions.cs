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

    [PersistableObject(Name = "ResolveOfficerEncounter")]
    public class ResolveOfficerEncounterAction : GameAction
    {
        public string EncounteredOfficerInstanceID { get; set; }
        public string OpposingOfficerInstanceID { get; set; }

        public ResolveOfficerEncounterAction()
            : base() { }

        /// <summary>
        /// Requests authoritative resolution of a linked-officer encounter.
        /// </summary>
        /// <param name="game">The game state used to resolve the officers.</param>
        /// <returns>The encounter request, or no result when either officer is unavailable.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer encountered = game.GetSceneNodeByInstanceID<Officer>(
                EncounteredOfficerInstanceID
            );
            Officer opposing = game.GetSceneNodeByInstanceID<Officer>(OpposingOfficerInstanceID);
            if (encountered == null || opposing == null)
                return new List<GameResult>();

            return new List<GameResult>
            {
                new OfficerEncounterRequestedResult
                {
                    EncounteredOfficer = encountered,
                    OpposingOfficer = opposing,
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
    /// Starts a persistent, timed story capture through the authoritative mission system.
    /// </summary>
    [PersistableObject(Name = "StartStoryCapture")]
    public sealed class StartStoryCaptureAction : GameAction
    {
        public string TargetOfficerInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public bool CanEscape { get; set; }
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer target = game.GetSceneNodeByInstanceID<Officer>(TargetOfficerInstanceID);
            if (target == null)
                throw new InvalidOperationException(
                    $"StartStoryCapture could not resolve target officer '{TargetOfficerInstanceID}'."
                );

            return new List<GameResult>
            {
                new StoryCaptureRequestedResult
                {
                    Target = target,
                    DurationTicks = DurationTicks,
                    CaptorFactionInstanceID = CaptorFactionInstanceID,
                    CanEscape = CanEscape,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Announces the original bounty-hunter attack through the normal result pipeline.
    /// </summary>
    [PersistableObject(Name = "BountyAttack")]
    public sealed class BountyAttackAction : GameAction
    {
        public string OfficerInstanceID { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"BountyAttack could not resolve officer '{OfficerInstanceID}'."
                );

            return new List<GameResult>
            {
                new BountyAttackResult { Officer = officer, Tick = game.CurrentTick },
            };
        }
    }

    /// <summary>
    /// Starts independent rescue missions for all available content-authored rescuers.
    /// </summary>
    [PersistableObject(Name = "StartStoryRescue")]
    public sealed class StartStoryRescueAction : GameAction
    {
        public string CaptiveOfficerInstanceID { get; set; }
        public List<string> RescuerOfficerInstanceIDs { get; set; } = new List<string>();
        public int DurationTicks { get; set; }
        public int DurationRandomTicks { get; set; }
        public int RatingDivisor { get; set; } = 1;
        public int SuccessCombatBonus { get; set; }
        public int SuccessEspionageBonus { get; set; }
        public bool CaptureRescuerOnFailure { get; set; }
        public bool FailedRescuerCanEscape { get; set; }
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer captive = game.GetSceneNodeByInstanceID<Officer>(CaptiveOfficerInstanceID);
            if (captive == null)
                throw new InvalidOperationException(
                    $"StartStoryRescue could not resolve captive officer '{CaptiveOfficerInstanceID}'."
                );

            List<Officer> rescuers = new List<Officer>();
            foreach (string rescuerId in RescuerOfficerInstanceIDs)
            {
                Officer rescuer = game.GetSceneNodeByInstanceID<Officer>(rescuerId);
                if (rescuer == null)
                    throw new InvalidOperationException(
                        $"StartStoryRescue could not resolve rescuer officer '{rescuerId}'."
                    );
                rescuers.Add(rescuer);
            }

            return new List<GameResult>
            {
                new StoryRescueRequestedResult
                {
                    Captive = captive,
                    Rescuers = rescuers,
                    DurationTicks = DurationTicks,
                    DurationRandomTicks = DurationRandomTicks,
                    RatingDivisor = RatingDivisor,
                    SuccessCombatBonus = SuccessCombatBonus,
                    SuccessEspionageBonus = SuccessEspionageBonus,
                    CaptureRescuerOnFailure = CaptureRescuerOnFailure,
                    FailedRescuerCanEscape = FailedRescuerCanEscape,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Sends a content-selected collector to retrieve all matching prisoners at a story location.
    /// </summary>
    [PersistableObject(Name = "StartStoryPickup")]
    public sealed class StartStoryPickupAction : GameAction
    {
        public string CollectorOfficerInstanceID { get; set; }
        public string LocationOfficerInstanceID { get; set; }
        public string CaptiveFactionInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public bool CaptivesCanEscapeAfterPickup { get; set; }
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer collector = game.GetSceneNodeByInstanceID<Officer>(CollectorOfficerInstanceID);
            if (collector == null)
                throw new InvalidOperationException(
                    $"StartStoryPickup could not resolve collector officer '{CollectorOfficerInstanceID}'."
                );

            Officer locationOfficer = game.GetSceneNodeByInstanceID<Officer>(
                LocationOfficerInstanceID
            );
            Planet location = locationOfficer?.GetParentOfType<Planet>();
            if (location == null)
                throw new InvalidOperationException(
                    $"StartStoryPickup could not resolve a planet for officer '{LocationOfficerInstanceID}'."
                );

            return new List<GameResult>
            {
                new OfficerPickupResult
                {
                    Officer = collector,
                    InProgress = true,
                    Tick = game.CurrentTick,
                },
                new StoryPickupRequestedResult
                {
                    Collector = collector,
                    Location = location,
                    CaptiveFactionInstanceID = CaptiveFactionInstanceID,
                    DurationTicks = DurationTicks,
                    CaptivesCanEscapeAfterPickup = CaptivesCanEscapeAfterPickup,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Starts the persisted, two-leg journey that culminates in the original final battle.
    /// </summary>
    [PersistableObject(Name = "StartStoryFinalBattle")]
    public sealed class StartStoryFinalBattleAction : GameAction
    {
        public string LukeOfficerInstanceID { get; set; }
        public string VaderOfficerInstanceID { get; set; }
        public string PalpatineOfficerInstanceID { get; set; }
        public string CaptorFactionInstanceID { get; set; }
        public int DurationTicks { get; set; }
        public int VictoryForceRank { get; set; }
        public int MinimumFailureInjury { get; set; }
        public int MaximumFailureInjury { get; set; }
        public bool CaptivesCanEscapeOnVictory { get; set; }
        public string DisplayName { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer luke = ResolveOfficer(
                game,
                LukeOfficerInstanceID,
                nameof(LukeOfficerInstanceID)
            );
            Officer vader = ResolveOfficer(
                game,
                VaderOfficerInstanceID,
                nameof(VaderOfficerInstanceID)
            );
            Officer palpatine = ResolveOfficer(
                game,
                PalpatineOfficerInstanceID,
                nameof(PalpatineOfficerInstanceID)
            );

            return new List<GameResult>
            {
                new StoryFinalBattleRequestedResult
                {
                    Luke = luke,
                    Vader = vader,
                    Palpatine = palpatine,
                    CaptorFactionInstanceID = CaptorFactionInstanceID,
                    DurationTicks = DurationTicks,
                    VictoryForceRank = VictoryForceRank,
                    MinimumFailureInjury = MinimumFailureInjury,
                    MaximumFailureInjury = MaximumFailureInjury,
                    CaptivesCanEscapeOnVictory = CaptivesCanEscapeOnVictory,
                    DisplayName = DisplayName,
                    Tick = game.CurrentTick,
                },
            };
        }

        private static Officer ResolveOfficer(GameRoot game, string instanceId, string memberName)
        {
            return game.GetSceneNodeByInstanceID<Officer>(instanceId)
                ?? throw new InvalidOperationException(
                    $"StartStoryFinalBattle could not resolve {memberName} '{instanceId}'."
                );
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
