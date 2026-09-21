using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    public partial class GameEventExecutor
    {
        /// <summary>Interprets one authored action in the supplied activation without catching failures.</summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The activation's state, random source, and pending work.</param>
        public static void ExecuteAction(GameAction action, GameActionContext context)
        {
            switch (action)
            {
                case RollOutcomeAction definition:
                    Execute(definition, context);
                    break;
                case RollChanceAction definition:
                    Execute(definition, context);
                    break;
                case IfAction definition:
                    Execute(definition, context);
                    break;
                case SetEventVariableAction definition:
                    Execute(definition, context);
                    break;
                case RevealToFactionAction definition:
                    Execute(definition, context);
                    break;
                case SendMessageAction definition:
                    Execute(definition, context);
                    break;
                case SetCaptureStatusAction definition:
                    Execute(definition, context);
                    break;
                case ChangeOfficerRatingAction definition:
                    Execute(definition, context);
                    break;
                case IncreaseForceRankAction definition:
                    Execute(definition, context);
                    break;
                case PerformSkillCheckAction definition:
                    Execute(definition, context);
                    break;
                case SetForceSensitiveAction definition:
                    Execute(definition, context);
                    break;
                case SetForceEligibleAction definition:
                    Execute(definition, context);
                    break;
                case ApplyOfficerInjuryAction definition:
                    Execute(definition, context);
                    break;
                case SetOfficerImagesAction definition:
                    Execute(definition, context);
                    break;
                case SetOfficerVoiceSetAction definition:
                    Execute(definition, context);
                    break;
                case TriggerDuelAction definition:
                    Execute(definition, context);
                    break;
                case SetDisplayNameAction definition:
                    Execute(definition, context);
                    break;
                case SetDisplayStatusAction definition:
                    Execute(definition, context);
                    break;
                case ClearDisplayStatusAction definition:
                    Execute(definition, context);
                    break;
                case ChangeRawResourceNodesAction definition:
                    Execute(definition, context);
                    break;
                case ChangeEnergyCapacityAction definition:
                    Execute(definition, context);
                    break;
                case ChangePopularSupportAction definition:
                    Execute(definition, context);
                    break;
                case SetPopularSupportAction definition:
                    Execute(definition, context);
                    break;
                case DamagePlanetResourcesAction definition:
                    Execute(definition, context);
                    break;
                case DestroyUnitsAction definition:
                    Execute(definition, context);
                    break;
                case ChangeOwnerAction definition:
                    Execute(definition, context);
                    break;
                case PlaceUnitsAction definition:
                    Execute(definition, context);
                    break;
                case SendUnitsAction definition:
                    Execute(definition, context);
                    break;
                case SetNodeStateAction definition:
                    Execute(definition, context);
                    break;
                case null:
                    throw new NullReferenceException();
                default:
                    throw new InvalidOperationException(
                        $"Unsupported game action '{action.GetType().Name}'."
                    );
            }
        }

        /// <summary>Executes authored actions in order, logging each failure and continuing the activation.</summary>
        /// <param name="actions">The ordered action definitions.</param>
        /// <param name="context">The shared activation state.</param>
        public static void ExecuteActions(
            IEnumerable<GameAction> actions,
            GameActionContext context
        )
        {
            foreach (GameAction action in actions ?? Enumerable.Empty<GameAction>())
            {
                try
                {
                    ExecuteAction(action, context);
                }
                catch (Exception exception)
                {
                    string eventInstanceId = context?.Evaluation?.Event?.InstanceID ?? "unknown";
                    string actionName = action?.GetType().Name ?? "null";
                    GameLogger.Log(
                        $"Event '{eventInstanceId}' action '{actionName}' failed: {exception}",
                        GameLogger.LogLevel.Error
                    );
                }
            }
        }

        /// <summary>
        /// Executes one eligible outcome selected by its authored weight.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        private static void Execute(RollOutcomeAction action, GameActionContext context)
        {
            List<RandomOutcome> eligible = action
                .Outcomes.Where(outcome =>
                    outcome.Weight > 0
                    && outcome.Conditionals.All(condition =>
                        GameEventExecutor.IsMet(condition, context.Game, context.Evaluation)
                    )
                )
                .ToList();
            if (eligible.Count == 0)
                return;

            int roll = context.Random.NextInt(0, eligible.Sum(outcome => outcome.Weight));
            RandomOutcome selected = null;
            foreach (RandomOutcome outcome in eligible)
            {
                roll -= outcome.Weight;
                if (roll < 0)
                {
                    selected = outcome;
                    break;
                }
            }

            ExecuteActions(selected.Actions, context);
            return;
        }

        /// <summary>
        /// Executes the authored actions when the resolved probability accepts the random roll.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        private static void Execute(RollChanceAction action, GameActionContext context)
        {
            double probability = ResolveProbability(action, context);
            if (double.IsNaN(probability) || probability < 0 || probability > 1)
                throw new InvalidOperationException(
                    "RollChance Probability must be between zero and one."
                );
            if (context.Random.NextDouble() < probability)
                ExecuteActions(action.Actions, context);
        }

        /// <summary>
        /// Resolves exactly one fixed, bound, or rolled probability.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        /// <returns>The normalized probability.</returns>
        private static double ResolveProbability(RollChanceAction action, GameActionContext context)
        {
            return GameActionNumericValue.ResolveDouble(
                action.Probability,
                action.ProbabilityBinding,
                action.RollDouble,
                context,
                "RollChance",
                "Probability"
            );
        }

        /// <summary>
        /// Executes the authored success or fallback actions for the current conditions.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(IfAction action, GameActionContext context)
        {
            IEnumerable<GameAction> selected = action.Conditionals.TrueForAll(condition =>
                GameEventExecutor.IsMet(condition, context.Game, context.Evaluation)
            )
                ? action.Actions
                : action.Else;
            ExecuteActions(selected, context);
            return;
        }

        /// <summary>
        /// Applies the authored operation to one event-runtime variable.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        private static void Execute(SetEventVariableAction action, GameActionContext context)
        {
            int operand = GameActionNumericValue.ResolveInteger(
                action.Operand,
                action.OperandBinding,
                action.RollInteger,
                context,
                "SetEventVariable",
                "Operand"
            );
            int previousValue = context.Game.EventRuntime.GetVariable(action.Key);
            int currentValue = action.Operation switch
            {
                EventVariableOperation.Set => operand,
                EventVariableOperation.Add => checked(previousValue + operand),
                EventVariableOperation.Minimum => Math.Min(previousValue, operand),
                EventVariableOperation.Maximum => Math.Max(previousValue, operand),
                _ => throw new InvalidOperationException(
                    $"Unsupported event variable operation '{action.Operation}'."
                ),
            };
            context.Game.EventRuntime.SetVariable(action.Key, currentValue);
            return;
        }

        /// <summary>
        /// Produces current observations of the selected targets for the recipient faction.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(RevealToFactionAction action, GameActionContext context)
        {
            Faction recipient = context.Game.GetFactionByOwnerInstanceID(action.FactionInstanceID);
            List<ISceneNode> observations = action
                .Targets.SelectMany(selector =>
                    GameEventExecutor.Select(
                        selector,
                        context.Game,
                        context.Random,
                        context.Evaluation
                    )
                )
                .Distinct()
                .ToList();
            if (observations.Count == 0)
                return;

            context.Record(
                new IntelligenceRevealedResult
                {
                    Recipient = recipient,
                    Observations = observations,
                    Tick = context.Game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Resolves the authored references and emits presentation-neutral narrative data.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The dependencies and activation data for this action.</param>
        private static void Execute(SendMessageAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            IRandomNumberProvider provider = context.Random;
            ISceneNode subject = !string.IsNullOrWhiteSpace(action.SubjectBinding)
                ? context.Evaluation?.GetBindingReference<ISceneNode>(action.SubjectBinding)
                : game.GetSceneNodeByInstanceID<ISceneNode>(
                    action.SubjectInstanceID,
                    includeDisabled: true
                );
            ISceneNode relatedSubject = game.GetSceneNodeByInstanceID<ISceneNode>(
                action.RelatedSubjectInstanceID,
                includeDisabled: true
            );
            if (string.IsNullOrWhiteSpace(action.RecipientFactionInstanceID))
                throw new InvalidOperationException(
                    "SendMessage requires RecipientFactionInstanceID."
                );

            Faction recipient = game.GetFactionByOwnerInstanceID(action.RecipientFactionInstanceID);
            Planet location = !string.IsNullOrWhiteSpace(action.LocationBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(action.LocationBinding)
                : game.GetSceneNodeByInstanceID<Planet>(
                    action.LocationInstanceID,
                    includeDisabled: true
                );
            if (location == null && subject != null)
                location = subject as Planet ?? subject.GetParentOfType<Planet>();

            string backgroundAudioPath = MessageMediaResolver.Resolve(
                action.BackgroundAudio,
                context
            );
            string imagePath = MessageMediaResolver.Resolve(action.BackgroundImage, context);

            MessageDefinition definition = new MessageDefinition
            {
                MessageType = action.MessageType,
                Subject = action.Subject,
                Body = action.Body ?? string.Empty,
                BackgroundImage =
                    string.IsNullOrWhiteSpace(action.BackgroundImage?.Key)
                    && string.IsNullOrWhiteSpace(imagePath)
                        ? null
                        : new MessageBackgroundImage
                        {
                            Key = action.BackgroundImage?.Key,
                            Path = imagePath,
                        },
                BackgroundAudioPath = backgroundAudioPath,
            };
            string overlayImagePath =
                action.OverlayImage?.Path
                ?? (action.ShowSubjectImage ? (subject as Officer)?.MessageImagePath : null);
            definition.OfficerVoicePath = action.OfficerVoice?.ResolvePath(
                subject as Officer,
                provider
            );
            AdvisorNotification notification = action.AdvisorNotification;
            context.Defer(
                nameof(SendMessageAction),
                (executor, sourceEventInstanceID) =>
                {
                    return (
                        executor._messageCommands
                        ?? throw new InvalidOperationException(
                            "Message commands are not configured."
                        )
                    ).DeliverAuthored(
                        definition,
                        recipient,
                        subject,
                        relatedSubject,
                        location,
                        overlayImagePath,
                        notification,
                        sourceEventInstanceID
                    );
                }
            );
        }

        /// <summary>
        /// Applies the authored captivity state to every selected officer.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SetCaptureStatusAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            if (action.IsCaptured && string.IsNullOrWhiteSpace(action.CaptorFactionInstanceID))
                throw new InvalidOperationException(
                    "SetCaptureStatus requires CaptorFactionInstanceID when capturing officers."
                );
            if (!action.IsCaptured && !string.IsNullOrWhiteSpace(action.CaptorFactionInstanceID))
                throw new InvalidOperationException(
                    "SetCaptureStatus cannot specify CaptorFactionInstanceID when releasing officers."
                );
            IEnumerable<ISceneNode> selectedNodes = action.Selectors.SelectMany(selector =>
                GameEventExecutor.Select(selector, game, context.Random, context.Evaluation)
            );
            if (!string.IsNullOrWhiteSpace(action.OfficerInstanceID))
            {
                Officer officer = game.GetSceneNodeByInstanceID<Officer>(
                    action.OfficerInstanceID,
                    includeDisabled: true
                );
                if (officer == null)
                    throw new InvalidOperationException(
                        $"SetCaptureStatus could not resolve officer '{action.OfficerInstanceID}'."
                    );
                selectedNodes = new ISceneNode[] { officer }.Concat(selectedNodes);
            }
            List<ISceneNode> selected = selectedNodes.Distinct().ToList();
            if (selected.Count == 0)
                throw new InvalidOperationException(
                    "SetCaptureStatus requires an officer or at least one matching selector."
                );
            if (selected.Any(node => node is not Officer))
                throw new InvalidOperationException(
                    "SetCaptureStatus selectors may return only officers."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in selected.Cast<Officer>())
            {
                officer.IsCaptured = action.IsCaptured;
                officer.CaptorInstanceID = action.IsCaptured
                    ? action.CaptorFactionInstanceID
                    : null;
                officer.CanEscape = action.IsCaptured ? action.CanEscape : true;
                results.Add(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = officer,
                        IsCaptured = action.IsCaptured,
                        Context = officer.GetParentOfType<Planet>(),
                        Tick = game.CurrentTick,
                    }
                );
            }
            context.Record(results);
        }

        /// <summary>
        /// Applies the authored rating change to every selected officer.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        private static void Execute(ChangeOfficerRatingAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            bool hasAmount =
                action.Amount.HasValue
                || !string.IsNullOrWhiteSpace(action.AmountBinding)
                || action.RollInteger != null;
            int modeCount =
                (hasAmount ? 1 : 0)
                + (action.PercentOfStored.HasValue ? 1 : 0)
                + (action.PercentOfEffective.HasValue ? 1 : 0)
                + (action.PercentOfPositiveGap.HasValue ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    "ChangeOfficerRating requires exactly one adjustment value."
                );
            Officer referenceOfficer = null;
            if (action.PercentOfPositiveGap.HasValue)
            {
                referenceOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    action.ReferenceOfficerInstanceID,
                    includeDisabled: true
                );
                if (referenceOfficer == null)
                    throw new InvalidOperationException(
                        $"ChangeOfficerRating could not resolve reference officer '{action.ReferenceOfficerInstanceID}'."
                    );
                if (action.PercentOfPositiveGap.Value < 0 || action.MinimumAmount < 0)
                    throw new InvalidOperationException(
                        "Rating-gap adjustments require non-negative percentage and minimum values."
                    );
            }

            List<Officer> officers = ResolveOfficers(action, context);

            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in officers)
            {
                int baseValue = officer.GetBaseRating(action.Rating);
                int currentValue = officer.GetEffectiveRating(action.Rating);
                int adjustment = hasAmount
                    ? GameActionNumericValue.ResolveInteger(
                        action.Amount,
                        action.AmountBinding,
                        action.RollInteger,
                        context,
                        "ChangeOfficerRating",
                        "Amount"
                    )
                    : (
                        action.PercentOfStored.HasValue
                            ? checked(baseValue * action.PercentOfStored.Value / 100)
                        : action.PercentOfEffective.HasValue
                            ? checked(currentValue * action.PercentOfEffective.Value / 100)
                        : Math.Max(
                            action.MinimumAmount,
                            checked(
                                Math.Max(
                                    0,
                                    referenceOfficer.GetEffectiveRating(action.Rating)
                                        - currentValue
                                )
                                * action.PercentOfPositiveGap.Value
                                / 100
                            )
                        )
                    );
                officer.SetBaseRating(action.Rating, checked(baseValue + adjustment));
            }
            context.Record(results);
        }

        /// <summary>
        /// Resolves and validates every officer targeted by this rating change.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        /// <returns>The distinct resolved officers.</returns>
        private static List<Officer> ResolveOfficers(
            ChangeOfficerRatingAction action,
            GameActionContext context
        )
        {
            GameRoot game = context.Game;
            IEnumerable<ISceneNode> selected = action.Selectors.SelectMany(selector =>
                GameEventExecutor.Select(selector, game, context.Random, context.Evaluation)
            );
            if (!string.IsNullOrWhiteSpace(action.OfficerInstanceID))
            {
                Officer explicitOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    action.OfficerInstanceID,
                    includeDisabled: true
                );
                if (explicitOfficer == null)
                    throw new InvalidOperationException(
                        $"ChangeOfficerRating could not resolve officer '{action.OfficerInstanceID}'."
                    );
                selected = new ISceneNode[] { explicitOfficer }.Concat(selected);
            }

            List<ISceneNode> nodes = selected.Distinct().ToList();
            if (nodes.Count == 0)
                throw new InvalidOperationException(
                    "ChangeOfficerRating requires an officer or a matching selector."
                );
            if (nodes.Any(node => node is not Officer))
                throw new InvalidOperationException(
                    "ChangeOfficerRating selectors may return only officers."
                );
            return nodes.Cast<Officer>().ToList();
        }

        /// <summary>
        /// Applies one positive Force increase mode to every explicitly named or selected officer.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        private static void Execute(IncreaseForceRankAction action, GameActionContext context)
        {
            bool hasAmount =
                action.Amount.HasValue
                || !string.IsNullOrWhiteSpace(action.AmountBinding)
                || action.RollInteger != null;
            int modeCount =
                (hasAmount ? 1 : 0)
                + (action.PercentOfStored.HasValue ? 1 : 0)
                + (action.PercentOfEffective.HasValue ? 1 : 0)
                + (action.PercentOfPositiveGap.HasValue ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    "IncreaseForceRank requires exactly one increase value."
                );
            if (
                action.Amount is <= 0
                || action.PercentOfStored is <= 0
                || action.PercentOfEffective is <= 0
                || action.PercentOfPositiveGap is <= 0
            )
                throw new InvalidOperationException(
                    "IncreaseForceRank values must be greater than zero."
                );
            if (action.MinimumAmount < 0)
                throw new InvalidOperationException(
                    "IncreaseForceRank MinimumAmount cannot be negative."
                );

            GameRoot game = context.Game;
            Officer referenceOfficer = null;
            if (action.PercentOfPositiveGap.HasValue)
            {
                referenceOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    action.ReferenceOfficerInstanceID,
                    includeDisabled: true
                );
                if (referenceOfficer == null)
                    throw new InvalidOperationException(
                        $"IncreaseForceRank could not resolve reference officer '{action.ReferenceOfficerInstanceID}'."
                    );
            }

            IEnumerable<ISceneNode> selected = action.Selectors.SelectMany(selector =>
                GameEventExecutor.Select(selector, game, context.Random, context.Evaluation)
            );
            if (!string.IsNullOrWhiteSpace(action.OfficerInstanceID))
            {
                Officer explicitOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    action.OfficerInstanceID,
                    includeDisabled: true
                );
                if (explicitOfficer == null)
                    throw new InvalidOperationException(
                        $"IncreaseForceRank could not resolve officer '{action.OfficerInstanceID}'."
                    );
                selected = new ISceneNode[] { explicitOfficer }.Concat(selected);
            }

            List<Officer> officers = selected.Distinct().OfType<Officer>().ToList();
            if (officers.Count == 0)
                throw new InvalidOperationException(
                    "IncreaseForceRank requires an officer or a matching selector."
                );
            if (selected.Any(node => node is not Officer))
                throw new InvalidOperationException(
                    "IncreaseForceRank selectors may return only officers."
                );

            foreach (Officer officer in officers)
            {
                int stored = officer.ForceValue;
                int effective = officer.ForceRank;
                int increase = hasAmount
                    ? GameActionNumericValue.ResolveInteger(
                        action.Amount,
                        action.AmountBinding,
                        action.RollInteger,
                        context,
                        "IncreaseForceRank",
                        "Amount"
                    )
                    : (
                        action.PercentOfStored.HasValue
                            ? checked(stored * action.PercentOfStored.Value / 100)
                        : action.PercentOfEffective.HasValue
                            ? checked(effective * action.PercentOfEffective.Value / 100)
                        : Math.Max(
                            action.MinimumAmount,
                            checked(
                                Math.Max(0, referenceOfficer.ForceRank - effective)
                                * action.PercentOfPositiveGap.Value
                                / 100
                            )
                        )
                    );
                if (increase <= 0)
                    throw new InvalidOperationException(
                        $"IncreaseForceRank calculated no increase for '{officer.InstanceID}'."
                    );
                officer.ForceValue = checked(stored + increase);
            }
            return;
        }

        /// <summary>
        /// Performs the authored officer skill check and executes its matching branch.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(PerformSkillCheckAction action, GameActionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                action.OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"PerformSkillCheck could not resolve officer '{action.OfficerInstanceID}'."
                );
            if (action.Rating == SkillRating.None)
                throw new InvalidOperationException("PerformSkillCheck requires a rating.");
            if (action.RatingMultiplier == 0)
                throw new InvalidOperationException(
                    "PerformSkillCheck RatingMultiplier cannot be zero."
                );

            GameConfig.MissionProbabilityTablesConfig tables = context
                .Game
                .Config
                ?.ProbabilityTables
                ?.Mission;
            if (tables?.GetSuccessTable(action.ProbabilityTable) == null)
                throw new InvalidOperationException(
                    $"PerformSkillCheck could not resolve probability table '{action.ProbabilityTable}'."
                );

            int probability = tables.GetSuccessProbability(
                action.ProbabilityTable,
                checked(officer.GetEffectiveRating(action.Rating) * action.RatingMultiplier)
            );
            bool succeeded = context.Random.NextDouble() * 100 < probability;
            IEnumerable<GameAction> actions = succeeded ? action.OnSuccess : action.OnFailure;
            ExecuteActions(actions, context);
            return;
        }

        /// <summary>
        /// Marks the configured officer as Force-sensitive without revealing that potential.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SetForceSensitiveAction action, GameActionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                action.OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetForceSensitive could not resolve officer '{action.OfficerInstanceID}'."
                );
            officer.IsForceSensitive = true;
            return;
        }

        /// <summary>
        /// Reveals and initializes an officer's existing latent Force potential once.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SetForceEligibleAction action, GameActionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                action.OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetForceEligible could not resolve officer '{action.OfficerInstanceID}'."
                );
            if (!officer.IsForceSensitive)
                throw new InvalidOperationException(
                    $"SetForceEligible requires Force-sensitive officer '{action.OfficerInstanceID}'."
                );
            if (officer.IsForceEligible)
                return;

            officer.IsForceEligible = true;
            int startingValue =
                officer.JediLevel + context.Random.NextInt(0, officer.JediLevelVariance + 1);
            officer.ForceValue = Math.Max(officer.ForceValue, startingValue);
            return;
        }

        /// <summary>
        /// Rolls and applies an injury within the authored severity range.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(ApplyOfficerInjuryAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(
                action.OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"ApplyOfficerInjury could not resolve officer '{action.OfficerInstanceID}'."
                );

            int injury = context.Random.NextInt(
                action.MinimumInjury,
                checked(action.MaximumInjury + 1)
            );
            officer.ApplyInjury(injury, game.Config.Recovery.MaxInjuryPoints);
            context.Record(
                new OfficerInjuredResult
                {
                    Officer = officer,
                    Severity = injury,
                    Tick = game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Merges authored image paths into the officer's active image set.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SetOfficerImagesAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(
                action.OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerImages could not resolve officer '{action.OfficerInstanceID}'."
                );
            officer.ImageSet.MergeFrom(
                new OfficerImageSet
                {
                    DisplayImagePath = action.DisplayImagePath,
                    SmallDisplayImagePath = action.SmallDisplayImagePath,
                    MessageImagePath = action.MessageImagePath,
                    EncyclopediaImagePath = action.EncyclopediaImagePath,
                }
            );
            officer.ApplyImageSet();
            return;
        }

        /// <summary>
        /// Merges authored voice categories into the officer's active voice set.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SetOfficerVoiceSetAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(
                action.OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerVoiceSet could not resolve officer '{action.OfficerInstanceID}'."
                );

            officer.VoiceSet.MergeFrom(
                new OfficerVoiceSet
                {
                    OrderPaths = action.Order,
                    PersonnelArrivedPaths = action.PersonnelArrived,
                    MissionSuccessPaths = action.MissionSuccess,
                    MissionFailurePaths = action.MissionFailure,
                    MissionAbortPaths = action.MissionAbort,
                    ReleasedPaths = action.Released,
                    RecoveredPaths = action.Recovered,
                    EnemyDetectedPaths = action.EnemyDetected,
                    ForceGrowthPaths = action.ForceGrowth,
                    ForceUserDiscoveredPaths = action.ForceUserDiscovered,
                    TraitorDiscoveredPaths = action.TraitorDiscovered,
                    RescueAttemptPaths = action.RescueAttempt,
                }
            );
            return;
        }

        /// <summary>
        /// Requests a duel between the two authored officers.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(TriggerDuelAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer first = game.GetSceneNodeByInstanceID<Officer>(action.FirstOfficerInstanceID);
            Officer second = game.GetSceneNodeByInstanceID<Officer>(action.SecondOfficerInstanceID);
            if (first == null || second == null)
                throw new InvalidOperationException(
                    $"TriggerDuel could not resolve officers '{action.FirstOfficerInstanceID}' and '{action.SecondOfficerInstanceID}'."
                );

            if (context.Evaluation?.TriggerResult is MissionCompletedResult completion)
            {
                bool firstParticipated = completion.Participants.Contains(first);
                bool secondParticipated = completion.Participants.Contains(second);
                if (firstParticipated == secondParticipated)
                    throw new InvalidOperationException(
                        "TriggerDuel requires exactly one configured officer to participate in the triggering mission."
                    );
                if (secondParticipated)
                    (first, second) = (second, first);
            }

            string imagePath = action.ImagePath;
            string audioPath = action.AudioPath;
            context.Defer(
                nameof(TriggerDuelAction),
                (executor, sourceEventInstanceID) =>
                    (
                        executor._duelCommands
                        ?? throw new InvalidOperationException("Duel commands are not configured.")
                    ).Resolve(first, second, imagePath, audioPath, sourceEventInstanceID)
            );
        }

        /// <summary>
        /// Resolves all authored targets and applies the configured display name.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SetDisplayNameAction action, GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.ResolveTargets(
                    action.TargetInstanceID,
                    action.Selectors,
                    context,
                    "SetDisplayName"
                )
            )
            {
                if (target is CapitalShip capitalShip)
                    capitalShip.AssignName(action.Name);
                else
                    target.DisplayName = action.Name;
            }
            return;
        }

        /// <summary>
        /// Resolves all authored targets and applies the configured status text.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SetDisplayStatusAction action, GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.ResolveTargets(
                    action.TargetInstanceID,
                    action.Selectors,
                    context,
                    "SetDisplayStatus"
                )
            )
                target.DisplayStatus = action.Status;
            return;
        }

        /// <summary>
        /// Resolves all authored targets and removes their current status text.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(ClearDisplayStatusAction action, GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.ResolveTargets(
                    action.TargetInstanceID,
                    action.Selectors,
                    context,
                    "ClearDisplayStatus"
                )
            )
                target.DisplayStatus = null;
            return;
        }

        /// <summary>
        /// Applies one signed adjustment to every resolved planet.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        /// <param name="category">The changed planet value's result category.</param>
        /// <param name="getValue">Reads the selected planet value.</param>
        /// <param name="setValue">Writes the selected planet value.</param>
        private static void Execute(
            ChangePlanetValueAction action,
            GameActionContext context,
            PlanetChangeCategory category,
            Func<Planet, int> getValue,
            Action<Planet, int> setValue
        )
        {
            bool hasAmount =
                action.Amount.HasValue
                || !string.IsNullOrWhiteSpace(action.AmountBinding)
                || action.RollInteger != null;
            if ((hasAmount ? 1 : 0) + (action.PercentOfCurrent.HasValue ? 1 : 0) != 1)
                throw new InvalidOperationException(
                    $"{action.GetType().Name} requires exactly one amount or percentage adjustment."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (
                Planet planet in PlanetActionTargets.Resolve(
                    action.PlanetInstanceID,
                    action.PlanetBinding,
                    action.Selectors,
                    context,
                    action.GetType().Name
                )
            )
            {
                int oldValue = getValue(planet);
                int adjustment = hasAmount
                    ? GameActionNumericValue.ResolveInteger(
                        action.Amount,
                        action.AmountBinding,
                        action.RollInteger,
                        context,
                        action.GetType().Name,
                        "Amount"
                    )
                    : checked(oldValue * action.PercentOfCurrent.Value / 100);
                int newValue = Math.Max(0, checked(oldValue + adjustment));
                setValue(planet, newValue);
                results.Add(
                    PlanetActionResults.Create(context.Game, planet, category, oldValue, newValue)
                );
            }
            context.Record(results);
        }

        /// <summary>
        /// Applies the authored support change to every resolved planet.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        private static void Execute(ChangePopularSupportAction action, GameActionContext context)
        {
            Faction faction = PopularSupportChange.ResolveFaction(
                context.Game,
                action.FactionInstanceID,
                "ChangePopularSupport"
            );
            bool hasAmount =
                action.Amount.HasValue
                || !string.IsNullOrWhiteSpace(action.AmountBinding)
                || action.RollInteger != null;
            if ((hasAmount ? 1 : 0) + (action.PercentOfCurrent.HasValue ? 1 : 0) != 1)
                throw new InvalidOperationException(
                    "ChangePopularSupport requires exactly one amount or percentage adjustment."
                );

            foreach (
                Planet planet in PlanetActionTargets.Resolve(
                    action.PlanetInstanceID,
                    action.PlanetBinding,
                    action.Selectors,
                    context,
                    "ChangePopularSupport"
                )
            )
            {
                int oldValue = planet.GetPopularSupport(action.FactionInstanceID);
                int adjustment = hasAmount
                    ? GameActionNumericValue.ResolveInteger(
                        action.Amount,
                        action.AmountBinding,
                        action.RollInteger,
                        context,
                        "ChangePopularSupport",
                        "Amount"
                    )
                    : checked(oldValue * action.PercentOfCurrent.Value / 100);
                PopularSupportChange.Apply(
                    context,
                    planet,
                    faction,
                    checked(oldValue + adjustment)
                );
            }
        }

        /// <summary>
        /// Sets the authored faction support on every resolved planet.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        private static void Execute(SetPopularSupportAction action, GameActionContext context)
        {
            Faction faction = PopularSupportChange.ResolveFaction(
                context.Game,
                action.FactionInstanceID,
                "SetPopularSupport"
            );
            int support = GameActionNumericValue.ResolveInteger(
                action.Support,
                action.SupportBinding,
                action.RollInteger,
                context,
                "SetPopularSupport",
                "Support"
            );
            foreach (
                Planet planet in PlanetActionTargets.Resolve(
                    action.PlanetInstanceID,
                    action.PlanetBinding,
                    action.Selectors,
                    context,
                    "SetPopularSupport"
                )
            )
                PopularSupportChange.Apply(context, planet, faction, support);
        }

        /// <summary>
        /// Randomly reduces planet resources while enforcing the minimum total loss.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        private static void Execute(DamagePlanetResourcesAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            Planet planet = !string.IsNullOrWhiteSpace(action.PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(action.PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(
                    action.PlanetInstanceID,
                    includeDisabled: true
                );
            if (planet == null)
                throw new InvalidOperationException("DamagePlanetResources requires a planet.");

            double probability = GameActionNumericValue.ResolveDouble(
                action.LossProbabilityPerResource,
                action.ProbabilityBinding,
                action.RollDouble,
                context,
                "DamagePlanetResources",
                "LossProbabilityPerResource"
            );
            if (double.IsNaN(probability) || probability < 0 || probability > 1)
                throw new InvalidOperationException(
                    "DamagePlanetResources.LossProbabilityPerResource must be between zero and one."
                );
            if (action.MinimumTotalLoss < 0)
                throw new InvalidOperationException(
                    "DamagePlanetResources.MinimumTotalLoss cannot be negative."
                );

            int previousRawResourceNodes = planet.NumRawResourceNodes;
            int previousEnergyCapacity = planet.EnergyCapacity;
            int availableResources = previousRawResourceNodes + previousEnergyCapacity;
            if (availableResources == 0)
                return;

            int rawResourceLoss = RollLoss(previousRawResourceNodes, probability, context.Random);
            int energyLoss = RollLoss(previousEnergyCapacity, probability, context.Random);
            int requiredLoss = Math.Min(action.MinimumTotalLoss, availableResources);
            int remainingRequiredLoss = requiredLoss - rawResourceLoss - energyLoss;
            if (remainingRequiredLoss > 0)
            {
                int additionalRawResourceLoss = Math.Min(
                    remainingRequiredLoss,
                    previousRawResourceNodes - rawResourceLoss
                );
                rawResourceLoss += additionalRawResourceLoss;
                remainingRequiredLoss -= additionalRawResourceLoss;
                energyLoss += Math.Min(remainingRequiredLoss, previousEnergyCapacity - energyLoss);
            }

            planet.NumRawResourceNodes = previousRawResourceNodes - rawResourceLoss;
            planet.EnergyCapacity = previousEnergyCapacity - energyLoss;
            context.Record(
                PlanetActionResults.Create(
                    game,
                    planet,
                    PlanetChangeCategory.RawMaterial,
                    previousRawResourceNodes,
                    planet.NumRawResourceNodes
                )
            );
            context.Record(
                PlanetActionResults.Create(
                    game,
                    planet,
                    PlanetChangeCategory.Energy,
                    previousEnergyCapacity,
                    planet.EnergyCapacity
                )
            );
        }

        /// <summary>
        /// Counts successful independent loss rolls for one planet resource.
        /// </summary>
        /// <param name="available">The number of resource points available to lose.</param>
        /// <param name="probability">The normalized loss probability per resource point.</param>
        /// <param name="provider">The random-number provider used for the roll.</param>
        /// <returns>The number of resource points lost.</returns>
        private static int RollLoss(
            int available,
            double probability,
            IRandomNumberProvider provider
        )
        {
            int loss = 0;
            for (int index = 0; index < available; index++)
            {
                if (provider.NextDouble() < probability)
                    loss++;
            }
            return loss;
        }

        /// <summary>
        /// Deletes every unit selected by the authored unit selectors.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(DestroyUnitsAction action, GameActionContext context)
        {
            GameRoot game = context.Game;
            if (action.Selectors.Count == 0)
                throw new InvalidOperationException("DestroyUnits requires at least one selector.");
            HashSet<ISceneNode> selected = action
                .Selectors.SelectMany(selector =>
                    GameEventExecutor.Select(selector, game, context.Random, context.Evaluation)
                )
                .ToHashSet();
            List<ISceneNode> destroyedRoots = selected
                .Where(unit => !HasSelectedAncestor(unit, selected))
                .ToList();
            List<ISceneNode> destroyed = new List<ISceneNode>();

            foreach (ISceneNode root in destroyedRoots)
            {
                root.Traverse(unit => destroyed.Add(unit));
                game.DeleteNode(root);
            }

            Planet planet = !string.IsNullOrWhiteSpace(action.PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(action.PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(
                    action.PlanetInstanceID,
                    includeDisabled: true
                );

            context.Record(
                destroyed.ConvertAll<GameResult>(unit => new GameObjectDestroyedResult
                {
                    DestroyedObject = unit,
                    Context = planet,
                    Tick = game.CurrentTick,
                })
            );
        }

        /// <summary>
        /// Returns whether another selected node already contains the candidate node.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="selected">The selected.</param>
        /// <returns>True when the selected ancestor condition is met; otherwise false.</returns>
        private static bool HasSelectedAncestor(ISceneNode unit, HashSet<ISceneNode> selected)
        {
            for (ISceneNode parent = unit.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (selected.Contains(parent))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Resolves exactly one ownership domain and delegates the change to gameplay.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(ChangeOwnerAction action, GameActionContext context)
        {
            bool hasPlanets = action.Planets.Count > 0;
            bool hasUnits = action.Units.Count > 0;
            if (hasPlanets == hasUnits)
                throw new InvalidOperationException(
                    "ChangeOwner requires exactly one of Planets or Units."
                );

            Faction faction = context.Game.GetFactionByOwnerInstanceID(action.FactionInstanceID);
            List<ISceneNode> selected = (hasPlanets ? action.Planets : action.Units)
                .SelectMany(selector =>
                    GameEventExecutor.Select(
                        selector,
                        context.Game,
                        context.Random,
                        context.Evaluation
                    )
                )
                .Distinct()
                .ToList();
            if (selected.Count == 0)
                throw new InvalidOperationException("ChangeOwner selected no objects.");
            if (hasPlanets && selected.Any(node => node is not Planet))
                throw new InvalidOperationException(
                    "ChangeOwner Planets selectors may only return planets."
                );
            if (hasUnits && selected.Any(node => !IsSupportedUnit(node)))
                throw new InvalidOperationException(
                    "ChangeOwner Units selectors may only return officers, ships, regiments, special forces, or buildings."
                );

            List<Planet> planets = selected.OfType<Planet>().ToList();
            List<ISceneNode> units = hasUnits ? selected : new List<ISceneNode>();
            context.Defer(
                nameof(ChangeOwnerAction),
                (executor, _) =>
                    (
                        executor._planetaryControlCommands
                        ?? throw new InvalidOperationException(
                            "Ownership commands are not configured."
                        )
                    ).ChangeOwnership(faction, planets, units)
            );
        }

        /// <summary>
        /// Returns whether ownership can be transferred for the selected unit type.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>True when the supported unit condition is met; otherwise false.</returns>
        private static bool IsSupportedUnit(ISceneNode node) =>
            node is Officer
            || node is CapitalShip
            || node is Starfighter
            || node is Regiment
            || node is SpecialForces
            || node is Building;

        /// <summary>
        /// Resolves the units and destinations shared by transit-based transfer actions.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        /// <param name="actionName">The action name.</param>
        /// <returns>The resolved value.</returns>
        private static (List<IMovable> Units, List<ContainerNode> Destinations) Resolve(
            UnitTransferAction action,
            GameActionContext context,
            string actionName
        ) =>
            (
                UnitActionTargets.ResolveUnits(
                    action.UnitInstanceID,
                    action.Units,
                    context,
                    actionName,
                    includeDisabled: true
                ),
                UnitActionTargets.ResolveDestinations(
                    action.DestinationInstanceID,
                    action.Destination,
                    context,
                    actionName
                )
            );

        /// <summary>
        /// Requests immediate placement of the resolved units at the resolved destination.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(PlaceUnitsAction action, GameActionContext context)
        {
            List<ContainerNode> destinations = UnitActionTargets.ResolveDestinations(
                action.DestinationInstanceID,
                action.Destination,
                context,
                "PlaceUnits"
            );
            List<IMovable> units = UnitActionTargets.ResolveUnits(
                action.UnitInstanceID,
                action.Units,
                context,
                "PlaceUnits",
                allowSpawn: true,
                includeDisabled: true
            );
            if (
                units.Any(unit =>
                    unit is ISceneNode node && node.GetParent() != null && !node.IsActive()
                )
            )
                throw new InvalidOperationException(
                    "PlaceUnits requires existing units to be active."
                );
            context.Defer(
                nameof(PlaceUnitsAction),
                (executor, _) =>
                {
                    (
                        executor._movementCommands
                        ?? throw new InvalidOperationException(
                            "Movement commands are not configured."
                        )
                    ).TryPlaceUnits(units, destinations);
                    return new List<GameResult>();
                }
            );
        }

        /// <summary>
        /// Requests normal transit for the resolved units to the resolved destination.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SendUnitsAction action, GameActionContext context)
        {
            (List<IMovable> units, List<ContainerNode> destinations) = Resolve(
                action,
                context,
                "SendUnits"
            );
            if (
                units.Any(unit =>
                    unit is not ISceneNode node || node.GetParent() == null || !node.IsActive()
                )
            )
                throw new InvalidOperationException(
                    "SendUnits requires active units at a valid scene location."
                );
            context.Defer(
                nameof(SendUnitsAction),
                (executor, sourceEventInstanceID) =>
                {
                    List<GameResult> results = new List<GameResult>();
                    (
                        executor._movementCommands
                        ?? throw new InvalidOperationException(
                            "Movement commands are not configured."
                        )
                    ).TryRequestMove(units, destinations, sourceEventInstanceID, results);
                    return results;
                }
            );
        }

        /// <summary>
        /// Applies the authored local active state to every selected scene node.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        private static void Execute(SetNodeStateAction action, GameActionContext context)
        {
            IEnumerable<ISceneNode> selected = action.Selectors.SelectMany(selector =>
                GameEventExecutor.Select(selector, context.Game, context.Random, context.Evaluation)
            );
            ISceneNode explicitNode = string.IsNullOrWhiteSpace(action.InstanceID)
                ? null
                : context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                    action.InstanceID,
                    includeDisabled: true
                );
            if (explicitNode != null)
                selected = new[] { explicitNode }.Concat(selected);
            List<ISceneNode> nodes = selected.Distinct().ToList();
            if (nodes.Count == 0)
                throw new InvalidOperationException(
                    "SetNodeState requires a resolvable node or selector."
                );

            foreach (ISceneNode node in nodes)
            {
                if (
                    action.State == SceneNodeState.Inactive
                    && node is IMissionParticipant
                    && node.GetParent() is Mission mission
                    && mission.GetParent() != null
                )
                {
                    throw new InvalidOperationException(
                        $"SetNodeState cannot deactivate mission participant "
                            + $"'{node.InstanceID}' on active mission '{mission.InstanceID}'."
                    );
                }

                node.IsEnabled = action.State == SceneNodeState.Active;
            }
            return;
        }

        /// <summary>Applies the authored adjustment to the planet's raw-resource count.</summary>
        /// <param name="action">The authored adjustment.</param>
        /// <param name="context">The shared activation state.</param>
        private static void Execute(
            ChangeRawResourceNodesAction action,
            GameActionContext context
        ) =>
            Execute(
                action,
                context,
                PlanetChangeCategory.RawMaterial,
                planet => planet.NumRawResourceNodes,
                (planet, value) => planet.NumRawResourceNodes = value
            );

        /// <summary>Applies the authored adjustment to the planet's energy capacity.</summary>
        /// <param name="action">The authored adjustment.</param>
        /// <param name="context">The shared activation state.</param>
        private static void Execute(ChangeEnergyCapacityAction action, GameActionContext context) =>
            Execute(
                action,
                context,
                PlanetChangeCategory.Energy,
                planet => planet.EnergyCapacity,
                (planet, value) => planet.EnergyCapacity = value
            );
    }

    /// <summary>
    /// Resolves the mutually exclusive numeric value forms supported by event actions.
    /// </summary>
    internal static class GameActionNumericValue
    {
        /// <summary>
        /// Resolves exactly one fixed, bound, or rolled integer.
        /// </summary>
        /// <param name="value">The fixed value, when authored.</param>
        /// <param name="binding">The event binding reference, when authored.</param>
        /// <param name="roll">The integer roll, when authored.</param>
        /// <param name="context">The current action execution context.</param>
        /// <param name="actionName">The XML action name used in validation errors.</param>
        /// <param name="valueName">The XML value name used in validation errors.</param>
        /// <returns>The resolved integer.</returns>
        internal static int ResolveInteger(
            int? value,
            string binding,
            RollInteger roll,
            GameActionContext context,
            string actionName,
            string valueName
        )
        {
            int modeCount =
                (value.HasValue ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(binding) ? 1 : 0)
                + (roll != null ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    $"{actionName} requires exactly one {valueName}, {valueName}Binding, or RollInteger."
                );
            if (!string.IsNullOrWhiteSpace(binding))
            {
                if (context.Evaluation?.TryGetBindingReference(binding, out int boundValue) != true)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve integer binding '{binding}'."
                    );
                return boundValue;
            }
            return value ?? GameEventExecutor.Roll(roll, context.Random);
        }

        /// <summary>
        /// Resolves exactly one fixed, bound, or rolled double.
        /// </summary>
        /// <param name="value">The fixed value, when authored.</param>
        /// <param name="binding">The event binding reference, when authored.</param>
        /// <param name="roll">The double roll, when authored.</param>
        /// <param name="context">The current action execution context.</param>
        /// <param name="actionName">The XML action name used in validation errors.</param>
        /// <param name="valueName">The XML value name used in validation errors.</param>
        /// <returns>The resolved double.</returns>
        internal static double ResolveDouble(
            double? value,
            string binding,
            RollDouble roll,
            GameActionContext context,
            string actionName,
            string valueName
        )
        {
            int modeCount =
                (value.HasValue ? 1 : 0)
                + (!string.IsNullOrWhiteSpace(binding) ? 1 : 0)
                + (roll != null ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    $"{actionName} requires exactly one {valueName}, {valueName}Binding, or RollDouble."
                );
            if (!string.IsNullOrWhiteSpace(binding))
            {
                if (
                    context.Evaluation?.TryGetBindingReference(binding, out double boundValue)
                    != true
                )
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve double binding '{binding}'."
                    );
                return boundValue;
            }
            return value ?? GameEventExecutor.Roll(roll, context.Random);
        }
    }

    /// <summary>
    /// Validates authored message-media sources and resolves paths supplied by event bindings.
    /// </summary>
    internal static class MessageMediaResolver
    {
        /// <summary>
        /// Resolves one background-image source to its external content path.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="context">The context.</param>
        /// <returns>The resolved value.</returns>
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
            if (!string.IsNullOrWhiteSpace(image.Key))
                return null;
            return ResolvePath(image.Path, image.Binding, context);
        }

        /// <summary>
        /// Resolves one background-audio source to its external content path.
        /// </summary>
        /// <param name="audio">The audio.</param>
        /// <param name="context">The context.</param>
        /// <returns>The resolved value.</returns>
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

        /// <summary>
        /// Resolves either an authored path or a path supplied by an event binding.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="binding">The binding.</param>
        /// <param name="context">The context.</param>
        /// <returns>The resolved path.</returns>
        private static string ResolvePath(string path, string binding, GameActionContext context)
        {
            if (!string.IsNullOrWhiteSpace(path))
                return path;
            if (context.Evaluation?.TryGetBindingReference(binding, out string boundPath) == true)
                return boundPath;
            throw new InvalidOperationException(
                $"Message media could not resolve binding '{binding}'."
            );
        }
    }

    /// <summary>
    /// Resolves canonical game entities targeted by presentation actions.
    /// </summary>
    internal static class DisplayActionTargets
    {
        /// <summary>
        /// Resolves the union of an explicit instance and selector results into unique registered
        /// game entities, failing when the action would mutate no valid target.
        /// </summary>
        /// <param name="targetInstanceID">The target instance id.</param>
        /// <param name="selectors">The selectors.</param>
        /// <param name="context">The context.</param>
        /// <param name="actionName">The action name.</param>
        /// <returns>The resolved targets.</returns>
        internal static List<BaseGameEntity> ResolveTargets(
            string targetInstanceID,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName
        )
        {
            IEnumerable<ISceneNode> selected = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).SelectMany(selector =>
                GameEventExecutor.Select(selector, context.Game, context.Random, context.Evaluation)
            );
            if (!string.IsNullOrWhiteSpace(targetInstanceID))
            {
                ISceneNode target = context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                    targetInstanceID,
                    includeDisabled: true
                );
                if (target == null)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve target '{targetInstanceID}'."
                    );
                selected = new[] { target }.Concat(selected);
            }

            List<ISceneNode> resolved = selected
                .Where(node => node != null)
                .Select(node =>
                    context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                        node.InstanceID,
                        includeDisabled: true
                    )
                )
                .Where(node => node != null)
                .Distinct()
                .ToList();
            if (resolved.Count == 0)
                throw new InvalidOperationException(
                    $"{actionName} requires a resolvable target or selector."
                );
            if (resolved.Any(node => node is not BaseGameEntity))
                throw new InvalidOperationException(
                    $"{actionName} selectors may return only game entities."
                );
            return resolved.Cast<BaseGameEntity>().ToList();
        }
    }

    /// <summary>
    /// Applies faction support changes and records every value affected by rebalancing.
    /// </summary>
    internal static class PopularSupportChange
    {
        /// <summary>
        /// Resolves an explicitly authored faction.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="factionInstanceID">The authored faction instance ID.</param>
        /// <param name="actionName">The XML action name used in validation errors.</param>
        /// <returns>The resolved faction.</returns>
        internal static Faction ResolveFaction(
            GameRoot game,
            string factionInstanceID,
            string actionName
        ) =>
            game.GetFactions().FirstOrDefault(faction => faction.InstanceID == factionInstanceID)
            ?? throw new InvalidOperationException(
                $"{actionName} could not resolve faction '{factionInstanceID}'."
            );

        /// <summary>
        /// Applies support while recording every faction value changed by rebalancing.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        /// <param name="planet">The planet whose support is changing.</param>
        /// <param name="targetFaction">The faction receiving the authored support value.</param>
        /// <param name="support">The requested support value.</param>
        internal static void Apply(
            GameActionContext context,
            Planet planet,
            Faction targetFaction,
            int support
        )
        {
            Dictionary<string, int> previous = context
                .Game.GetFactions()
                .ToDictionary(
                    faction => faction.InstanceID,
                    faction => planet.GetPopularSupport(faction.InstanceID)
                );
            planet.SetPopularSupport(targetFaction.InstanceID, support);
            foreach (Faction faction in context.Game.GetFactions())
            {
                int oldValue = previous[faction.InstanceID];
                int newValue = planet.GetPopularSupport(faction.InstanceID);
                context.Record(
                    PlanetActionResults.Create(
                        context.Game,
                        planet,
                        PlanetChangeCategory.Loyalty,
                        oldValue,
                        newValue,
                        faction
                    )
                );
            }
        }
    }

    /// <summary>
    /// Creates one standard result describing a changed planet value.
    /// </summary>
    internal static class PlanetActionResults
    {
        /// <summary>
        /// Creates one result when the supplied planet value changed.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="planet">The changed planet.</param>
        /// <param name="category">The category of planet value that changed.</param>
        /// <param name="oldValue">The value before the change.</param>
        /// <param name="newValue">The value after the change.</param>
        /// <param name="faction">The affected faction when the value is faction-specific.</param>
        /// <returns>A change result, or null when the value did not change.</returns>
        internal static PlanetStatChangedResult Create(
            GameRoot game,
            Planet planet,
            PlanetChangeCategory category,
            int oldValue,
            int newValue,
            Faction faction = null
        )
        {
            if (oldValue == newValue)
                return null;
            return new PlanetStatChangedResult
            {
                Planet = planet,
                Faction =
                    faction
                    ?? game.GetFactions()
                        .FirstOrDefault(candidate =>
                            candidate.InstanceID == planet.OwnerInstanceID
                        ),
                Category = category,
                OldValue = oldValue,
                NewValue = newValue,
                Tick = game.CurrentTick,
            };
        }
    }

    /// <summary>
    /// Resolves planet targets shared by explicit planet actions.
    /// </summary>
    internal static class PlanetActionTargets
    {
        /// <summary>
        /// Resolves and validates every directly named, bound, or selected planet.
        /// </summary>
        /// <param name="planetInstanceID">The directly authored planet instance ID.</param>
        /// <param name="planetBinding">The authored planet binding reference.</param>
        /// <param name="selectors">The authored planet selectors.</param>
        /// <param name="context">The current action execution context.</param>
        /// <param name="actionName">The XML action name used in validation errors.</param>
        /// <returns>The distinct resolved planets.</returns>
        internal static List<Planet> Resolve(
            string planetInstanceID,
            string planetBinding,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName
        )
        {
            GameRoot game = context.Game;
            IEnumerable<ISceneNode> selected = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).SelectMany(selector =>
                GameEventExecutor.Select(selector, game, context.Random, context.Evaluation)
            );
            Planet explicitPlanet = !string.IsNullOrWhiteSpace(planetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(planetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(planetInstanceID, includeDisabled: true);
            if (explicitPlanet != null)
                selected = new ISceneNode[] { explicitPlanet }.Concat(selected);
            List<ISceneNode> nodes = selected.Distinct().ToList();
            if (nodes.Count == 0)
                throw new InvalidOperationException(
                    $"{actionName} requires a planet, planet binding, or matching selector."
                );
            if (nodes.Any(node => node is not Planet))
                throw new InvalidOperationException(
                    $"{actionName} selectors may return only planets."
                );
            return nodes.Cast<Planet>().ToList();
        }
    }

    /// <summary>
    /// Resolves canonical movable units and valid destination containers.
    /// </summary>
    internal static class UnitActionTargets
    {
        /// <summary>
        /// Resolves explicit and selected movable units for an action.
        /// </summary>
        /// <param name="unitInstanceID">The unit instance id.</param>
        /// <param name="selectors">The selectors.</param>
        /// <param name="context">The context.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="allowSpawn">Whether allow spawn.</param>
        /// <param name="includeDisabled">Whether include disabled.</param>
        /// <returns>The resolved units.</returns>
        internal static List<IMovable> ResolveUnits(
            string unitInstanceID,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName,
            bool allowSpawn = false,
            bool includeDisabled = false
        )
        {
            GameRoot game = context.Game;
            List<GameEventSelector> sources = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).ToList();
            List<ISceneNode> spawned = sources
                .OfType<SpawnUnits>()
                .SelectMany(source =>
                    allowSpawn
                        ? Spawn(source, context)
                        : throw new InvalidOperationException(
                            $"{actionName} cannot use SpawnUnits as a unit source."
                        )
                )
                .ToList();
            IEnumerable<ISceneNode> selected = sources
                .Where(source => source is not SpawnUnits)
                .SelectMany(selector =>
                    GameEventExecutor.Select(selector, game, context.Random, context.Evaluation)
                );
            if (!string.IsNullOrWhiteSpace(unitInstanceID))
            {
                ISceneNode direct = includeDisabled
                    ? game.GetSceneNodeByInstanceID<ISceneNode>(
                        unitInstanceID,
                        includeDisabled: true
                    )
                    : game.GetSceneNodeByInstanceID<ISceneNode>(unitInstanceID);
                if (direct == null)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve unit '{unitInstanceID}'."
                    );
                selected = new[] { direct }.Concat(selected);
            }

            List<ISceneNode> resolved = selected
                .Where(node => node != null)
                .Select(node =>
                    includeDisabled
                        ? game.GetSceneNodeByInstanceID<ISceneNode>(
                            node.InstanceID,
                            includeDisabled: true
                        )
                        : game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID)
                )
                .Where(node => node != null)
                .GroupBy(node => node.InstanceID, StringComparer.Ordinal)
                .Select(group => group.First())
                .Concat(spawned)
                .ToList();
            if (resolved.Count == 0)
                throw new InvalidOperationException(
                    $"{actionName} requires at least one resolvable unit."
                );
            if (resolved.Any(unit => unit is not IMovable))
                throw new InvalidOperationException(
                    $"{actionName} unit selectors may return only movable units."
                );
            return resolved.Cast<IMovable>().ToList();
        }

        /// <summary>
        /// Resolves explicit and selected destination containers for an action.
        /// </summary>
        /// <param name="destinationInstanceID">The destination instance id.</param>
        /// <param name="selectors">The selectors.</param>
        /// <param name="context">The context.</param>
        /// <param name="actionName">The action name.</param>
        /// <returns>The resolved destinations.</returns>
        internal static List<ContainerNode> ResolveDestinations(
            string destinationInstanceID,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName
        )
        {
            GameRoot game = context.Game;
            List<GameEventSelector> destinationSelectors = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).ToList();
            bool selectFirstAccepted =
                destinationSelectors.Count == 1 && destinationSelectors[0] is SelectFirst;
            IEnumerable<ISceneNode> selected = selectFirstAccepted
                ? GameEventExecutor.SelectCandidates(
                    ((SelectFirst)destinationSelectors[0]),
                    game,
                    context.Random,
                    context.Evaluation
                )
                : destinationSelectors.SelectMany(selector =>
                    GameEventExecutor.Select(selector, game, context.Random, context.Evaluation)
                );
            if (!string.IsNullOrWhiteSpace(destinationInstanceID))
            {
                ISceneNode direct = game.GetSceneNodeByInstanceID<ISceneNode>(
                    destinationInstanceID
                );
                if (direct == null)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve destination '{destinationInstanceID}'."
                    );
                selected = new[] { direct }.Concat(selected);
            }

            List<ContainerNode> destinations = selected
                .Where(node => node != null)
                .Select(node => game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID))
                .OfType<ContainerNode>()
                .Where(node => node.GetParent() != null)
                .GroupBy(node => node.InstanceID, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            if (destinations.Count == 0 || (!selectFirstAccepted && destinations.Count != 1))
                throw new InvalidOperationException(
                    $"{actionName} requires exactly one destination or an explicit SelectFirst; resolved {destinations.Count}."
                );
            return destinations;
        }

        /// <summary>
        /// Creates detached runtime units for immediate placement.
        /// </summary>
        /// <param name="action">The authored unit source.</param>
        /// <param name="context">The context.</param>
        /// <returns>The result of spawn.</returns>
        private static IEnumerable<ISceneNode> Spawn(SpawnUnits action, GameActionContext context)
        {
            if (context.UnitFactory == null)
                throw new InvalidOperationException(
                    "SpawnUnits requires the active content unit factory."
                );
            if (string.IsNullOrWhiteSpace(action.TypeID) || action.Count < 1)
                throw new InvalidOperationException(
                    "SpawnUnits requires a TypeID and a positive Count."
                );
            context.Game.GetFactionByOwnerInstanceID(action.OwnerFactionInstanceID);

            for (int index = 0; index < action.Count; index++)
            {
                ISceneNode unit = context.UnitFactory.Create(
                    action.TypeID,
                    action.OwnerFactionInstanceID
                );
                unit.InstanceID = Guid.NewGuid().ToString("N");
                yield return unit;
            }
        }
    }
}
