using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Advisor;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    #region RandomActions
    /// <summary>
    /// Defines an inclusive integer roll that may supply an action value or event binding.
    /// </summary>
    [PersistableObject(Name = "RollInteger")]
    public sealed class RollInteger
    {
        // Range.
        [PersistableAttribute]
        public int Minimum { get; set; }

        [PersistableAttribute]
        public int Maximum { get; set; }

        /// <summary>
        /// Rolls one integer inside the authored inclusive range.
        /// </summary>
        /// <param name="provider">The random-number provider used for the roll.</param>
        /// <returns>An integer from <see cref="Minimum"/> through <see cref="Maximum"/>.</returns>
        internal int Roll(IRandomNumberProvider provider)
        {
            if (Minimum > Maximum)
                throw new InvalidOperationException("RollInteger Minimum cannot exceed Maximum.");

            long valueCount = (long)Maximum - Minimum + 1;
            long offset = (long)Math.Floor(provider.NextDouble() * valueCount);
            return checked((int)(Minimum + offset));
        }
    }

    /// <summary>
    /// Defines a double roll whose minimum is inclusive and maximum is exclusive.
    /// </summary>
    [PersistableObject(Name = "RollDouble")]
    public sealed class RollDouble
    {
        // Range.
        [PersistableAttribute]
        public double Minimum { get; set; }

        [PersistableAttribute]
        public double Maximum { get; set; }

        /// <summary>
        /// Rolls one double inside the authored range.
        /// </summary>
        /// <param name="provider">The random-number provider used for the roll.</param>
        /// <returns>A double no less than <see cref="Minimum"/> and less than <see cref="Maximum"/>.</returns>
        internal double Roll(IRandomNumberProvider provider)
        {
            if (
                double.IsNaN(Minimum)
                || double.IsInfinity(Minimum)
                || double.IsNaN(Maximum)
                || double.IsInfinity(Maximum)
                || Minimum >= Maximum
            )
                throw new InvalidOperationException(
                    "RollDouble requires finite bounds with Minimum less than Maximum."
                );

            double sample = provider.NextDouble();
            return Minimum * (1 - sample) + Maximum * sample;
        }
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
            return value ?? roll.Roll(context.Random);
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
            return value ?? roll.Roll(context.Random);
        }
    }

    /// <summary>
    /// Defines one conditionally eligible weighted outcome.
    /// </summary>
    [PersistableObject(Name = "Outcome")]
    public sealed class RandomOutcome
    {
        [PersistableAttribute]
        public int Weight { get; set; } = 1;

        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();

        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    /// <summary>
    /// Selects and executes exactly one eligible outcome using relative authored weights.
    /// </summary>
    [PersistableObject(Name = "RollOutcome")]
    public sealed class RollOutcomeAction : GameAction
    {
        public List<RandomOutcome> Outcomes { get; set; } = new List<RandomOutcome>();

        /// <summary>
        /// Executes one eligible outcome selected by its authored weight.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            List<RandomOutcome> eligible = Outcomes
                .Where(outcome =>
                    outcome.Weight > 0
                    && outcome.Conditionals.All(condition =>
                        condition.IsMet(context.Game, context.Evaluation)
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

            GameAction.ExecuteAll(selected.Actions, context);
            return;
        }
    }

    /// <summary>
    /// Executes authored actions when one normalized probability roll succeeds.
    /// </summary>
    [PersistableObject(Name = "RollChance")]
    public sealed class RollChanceAction : GameAction
    {
        // Probability.
        [PersistableAttribute]
        public double? Probability { get; set; }

        [PersistableAttribute]
        public string ProbabilityBinding { get; set; }

        public RollDouble RollDouble { get; set; }

        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        /// <summary>
        /// Executes the authored actions when the resolved probability accepts the random roll.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            double probability = ResolveProbability(context);
            if (double.IsNaN(probability) || probability < 0 || probability > 1)
                throw new InvalidOperationException(
                    "RollChance Probability must be between zero and one."
                );
            if (context.Random.NextDouble() < probability)
                GameAction.ExecuteAll(Actions, context);
        }

        /// <summary>
        /// Resolves exactly one fixed, bound, or rolled probability.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        /// <returns>The normalized probability.</returns>
        private double ResolveProbability(GameActionContext context)
        {
            return GameActionNumericValue.ResolveDouble(
                Probability,
                ProbabilityBinding,
                RollDouble,
                context,
                "RollChance",
                "Probability"
            );
        }
    }

    #endregion

    #region CompositeActions
    [PersistableObject(Name = "If")]
    public sealed class IfAction : GameAction
    {
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
        public List<GameAction> Else { get; set; } = new List<GameAction>();

        /// <summary>
        /// Executes the authored success or fallback actions for the current conditions.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            IEnumerable<GameAction> selected = Conditionals.TrueForAll(condition =>
                condition.IsMet(context.Game, context.Evaluation)
            )
                ? Actions
                : Else;
            GameAction.ExecuteAll(selected, context);
            return;
        }
    }
    #endregion

    #region EventStateActions
    public enum EventVariableOperation
    {
        Set,
        Add,
        Minimum,
        Maximum,
    }

    [PersistableObject(Name = "SetEventVariable")]
    public sealed class SetEventVariableAction : GameAction
    {
        public string Key { get; set; }
        public EventVariableOperation Operation { get; set; }
        public int? Operand { get; set; }

        public string OperandBinding { get; set; }

        public RollInteger RollInteger { get; set; }

        /// <summary>
        /// Applies the authored operation to one event-runtime variable.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            int operand = GameActionNumericValue.ResolveInteger(
                Operand,
                OperandBinding,
                RollInteger,
                context,
                "SetEventVariable",
                "Operand"
            );
            int previousValue = context.Game.EventRuntime.GetVariable(Key);
            int currentValue = Operation switch
            {
                EventVariableOperation.Set => operand,
                EventVariableOperation.Add => checked(previousValue + operand),
                EventVariableOperation.Minimum => Math.Min(previousValue, operand),
                EventVariableOperation.Maximum => Math.Max(previousValue, operand),
                _ => throw new InvalidOperationException(
                    $"Unsupported event variable operation '{Operation}'."
                ),
            };
            context.Game.EventRuntime.SetVariable(Key, currentValue);
            return;
        }
    }
    #endregion

    #region FogOfWarActions
    /// <summary>
    /// Supplies current observations about selected objects to one faction.
    /// </summary>
    [PersistableObject(Name = "RevealToFaction")]
    public sealed class RevealToFactionAction : GameAction
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Targets { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Produces current observations of the selected targets for the recipient faction.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            Faction recipient = context.Game.GetFactionByOwnerInstanceID(FactionInstanceID);
            List<ISceneNode> observations = Targets
                .SelectMany(selector =>
                    selector.Select(context.Game, context.Random, context.Evaluation)
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
    }
    #endregion

    #region MessageActions
    /// <summary>
    /// Emits a normal faction message from presentation data authored with a game event.
    /// </summary>
    [PersistableObject(Name = "SendMessage")]
    public sealed class SendMessageAction : GameAction
    {
        [PersistableAttribute]
        public string RecipientFactionInstanceID { get; set; }

        [PersistableAttribute]
        public string SubjectInstanceID { get; set; }

        [PersistableAttribute]
        public string SubjectBinding { get; set; }

        [PersistableAttribute]
        public string RelatedSubjectInstanceID { get; set; }

        [PersistableAttribute]
        public string LocationInstanceID { get; set; }

        [PersistableAttribute]
        public string LocationBinding { get; set; }

        [PersistableAttribute(Name = "Type")]
        public MessageType MessageType { get; set; } = MessageType.Advice;
        public string Subject { get; set; }
        public string Body { get; set; }
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
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            IRandomNumberProvider provider = context.Random;
            ISceneNode subject = !string.IsNullOrWhiteSpace(SubjectBinding)
                ? context.Evaluation?.GetBindingReference<ISceneNode>(SubjectBinding)
                : game.GetSceneNodeByInstanceID<ISceneNode>(
                    SubjectInstanceID,
                    includeDisabled: true
                );
            ISceneNode relatedSubject = game.GetSceneNodeByInstanceID<ISceneNode>(
                RelatedSubjectInstanceID
            );
            if (string.IsNullOrWhiteSpace(RecipientFactionInstanceID))
                throw new InvalidOperationException(
                    "SendMessage requires RecipientFactionInstanceID."
                );

            Faction recipient = game.GetFactionByOwnerInstanceID(RecipientFactionInstanceID);
            Planet location = !string.IsNullOrWhiteSpace(LocationBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(LocationBinding)
                : game.GetSceneNodeByInstanceID<Planet>(LocationInstanceID);
            if (location == null && subject != null)
                location = subject as Planet ?? subject.GetParentOfType<Planet>();

            string backgroundAudioPath = MessageMediaResolver.Resolve(BackgroundAudio, context);
            string imagePath = MessageMediaResolver.Resolve(BackgroundImage, context);

            context.Request(
                new MessageDeliveryRequest
                {
                    Recipient = recipient,
                    SubjectNode = subject,
                    RelatedSubjectNode = relatedSubject,
                    Location = location,
                    MessageType = MessageType,
                    Subject = Subject,
                    Body = Body ?? string.Empty,
                    BackgroundImageKey = BackgroundImage?.Key,
                    BackgroundImagePath = imagePath,
                    OverlayImagePath = OverlayImage?.Path ?? (subject as Officer)?.MessageImagePath,
                    BackgroundAudioPath = backgroundAudioPath,
                    OfficerVoicePath = OfficerVoice?.ResolvePath(subject as Officer, provider),
                    AdvisorNotification = this.AdvisorNotification,
                    Tick = game.CurrentTick,
                }
            );
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

        /// <summary>
        /// Resolves one background-audio source to its external content path.
        /// </summary>
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
    #endregion

    #region OfficerActions
    /// <summary>
    /// Sets one officer's captivity state and emits the standard state-change result.
    /// </summary>
    [PersistableObject(Name = "SetCaptureStatus")]
    public sealed class SetCaptureStatusAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public bool IsCaptured { get; set; }

        [PersistableAttribute]
        public string CaptorFactionInstanceID { get; set; }

        [PersistableAttribute]
        public bool CanEscape { get; set; } = true;

        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies the authored captivity state to every selected officer.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            if (IsCaptured && string.IsNullOrWhiteSpace(CaptorFactionInstanceID))
                throw new InvalidOperationException(
                    "SetCaptureStatus requires CaptorFactionInstanceID when capturing officers."
                );
            if (!IsCaptured && !string.IsNullOrWhiteSpace(CaptorFactionInstanceID))
                throw new InvalidOperationException(
                    "SetCaptureStatus cannot specify CaptorFactionInstanceID when releasing officers."
                );
            IEnumerable<ISceneNode> selectedNodes = Selectors.SelectMany(selector =>
                selector.Select(game, context.Random, context.Evaluation)
            );
            if (!string.IsNullOrWhiteSpace(OfficerInstanceID))
            {
                Officer officer = game.GetSceneNodeByInstanceID<Officer>(
                    OfficerInstanceID,
                    includeDisabled: true
                );
                if (officer == null)
                    throw new InvalidOperationException(
                        $"SetCaptureStatus could not resolve officer '{OfficerInstanceID}'."
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
                officer.IsCaptured = IsCaptured;
                officer.CaptorInstanceID = IsCaptured ? CaptorFactionInstanceID : null;
                officer.CanEscape = IsCaptured ? CanEscape : true;
                results.Add(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = officer,
                        IsCaptured = IsCaptured,
                        Context = officer.GetParentOfType<Planet>(),
                        Tick = game.CurrentTick,
                    }
                );
            }
            context.Record(results);
        }
    }

    /// <summary>
    /// Adjusts selected officers using one authored calculation.
    /// </summary>
    [PersistableObject(Name = "ChangeOfficerRating")]
    public sealed class ChangeOfficerRatingAction : GameAction
    {
        // Officer Targets.
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        // Rating.
        [PersistableAttribute]
        public OfficerRating Rating { get; set; }

        // Adjustment.
        public int? Amount { get; set; }
        public string AmountBinding { get; set; }
        public RollInteger RollInteger { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }

        [PersistableAttribute]
        public string ReferenceOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public int MinimumAmount { get; set; }

        // Additional Officer Targets.
        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies the authored rating change to every selected officer.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            bool hasAmount =
                Amount.HasValue || !string.IsNullOrWhiteSpace(AmountBinding) || RollInteger != null;
            int modeCount =
                (hasAmount ? 1 : 0)
                + (PercentOfStored.HasValue ? 1 : 0)
                + (PercentOfEffective.HasValue ? 1 : 0)
                + (PercentOfPositiveGap.HasValue ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    "ChangeOfficerRating requires exactly one adjustment value."
                );
            Officer referenceOfficer = null;
            if (PercentOfPositiveGap.HasValue)
            {
                referenceOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    ReferenceOfficerInstanceID,
                    includeDisabled: true
                );
                if (referenceOfficer == null)
                    throw new InvalidOperationException(
                        $"ChangeOfficerRating could not resolve reference officer '{ReferenceOfficerInstanceID}'."
                    );
                if (PercentOfPositiveGap.Value < 0 || MinimumAmount < 0)
                    throw new InvalidOperationException(
                        "Rating-gap adjustments require non-negative percentage and minimum values."
                    );
            }

            List<Officer> officers = ResolveOfficers(context);

            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in officers)
            {
                int baseValue = officer.GetBaseRating(Rating);
                int currentValue = officer.GetEffectiveRating(Rating);
                int adjustment = hasAmount
                    ? GameActionNumericValue.ResolveInteger(
                        Amount,
                        AmountBinding,
                        RollInteger,
                        context,
                        "ChangeOfficerRating",
                        "Amount"
                    )
                    : (
                        PercentOfStored.HasValue ? checked(baseValue * PercentOfStored.Value / 100)
                        : PercentOfEffective.HasValue
                            ? checked(currentValue * PercentOfEffective.Value / 100)
                        : Math.Max(
                            MinimumAmount,
                            checked(
                                Math.Max(
                                    0,
                                    referenceOfficer.GetEffectiveRating(Rating) - currentValue
                                )
                                * PercentOfPositiveGap.Value
                                / 100
                            )
                        )
                    );
                officer.SetBaseRating(Rating, checked(baseValue + adjustment));
            }
            context.Record(results);
        }

        /// <summary>
        /// Resolves and validates every officer targeted by this rating change.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        /// <returns>The distinct resolved officers.</returns>
        private List<Officer> ResolveOfficers(GameActionContext context)
        {
            GameRoot game = context.Game;
            IEnumerable<ISceneNode> selected = Selectors.SelectMany(selector =>
                selector.Select(game, context.Random, context.Evaluation)
            );
            if (!string.IsNullOrWhiteSpace(OfficerInstanceID))
            {
                Officer explicitOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    OfficerInstanceID,
                    includeDisabled: true
                );
                if (explicitOfficer == null)
                    throw new InvalidOperationException(
                        $"ChangeOfficerRating could not resolve officer '{OfficerInstanceID}'."
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
    }

    /// <summary>
    /// Increases selected officers' stored Force progression using one authored calculation.
    /// </summary>
    [PersistableObject(Name = "IncreaseForceRank")]
    public sealed class IncreaseForceRankAction : GameAction
    {
        // Officer Targets.
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        // Adjustment.
        public int? Amount { get; set; }
        public string AmountBinding { get; set; }
        public RollInteger RollInteger { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }

        [PersistableAttribute]
        public string ReferenceOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public int MinimumAmount { get; set; }

        // Additional Officer Targets.
        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies one positive Force increase mode to every explicitly named or selected officer.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            bool hasAmount =
                Amount.HasValue || !string.IsNullOrWhiteSpace(AmountBinding) || RollInteger != null;
            int modeCount =
                (hasAmount ? 1 : 0)
                + (PercentOfStored.HasValue ? 1 : 0)
                + (PercentOfEffective.HasValue ? 1 : 0)
                + (PercentOfPositiveGap.HasValue ? 1 : 0);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    "IncreaseForceRank requires exactly one increase value."
                );
            if (
                Amount is <= 0
                || PercentOfStored is <= 0
                || PercentOfEffective is <= 0
                || PercentOfPositiveGap is <= 0
            )
                throw new InvalidOperationException(
                    "IncreaseForceRank values must be greater than zero."
                );
            if (MinimumAmount < 0)
                throw new InvalidOperationException(
                    "IncreaseForceRank MinimumAmount cannot be negative."
                );

            GameRoot game = context.Game;
            Officer referenceOfficer = null;
            if (PercentOfPositiveGap.HasValue)
            {
                referenceOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    ReferenceOfficerInstanceID,
                    includeDisabled: true
                );
                if (referenceOfficer == null)
                    throw new InvalidOperationException(
                        $"IncreaseForceRank could not resolve reference officer '{ReferenceOfficerInstanceID}'."
                    );
            }

            IEnumerable<ISceneNode> selected = Selectors.SelectMany(selector =>
                selector.Select(game, context.Random, context.Evaluation)
            );
            if (!string.IsNullOrWhiteSpace(OfficerInstanceID))
            {
                Officer explicitOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    OfficerInstanceID,
                    includeDisabled: true
                );
                if (explicitOfficer == null)
                    throw new InvalidOperationException(
                        $"IncreaseForceRank could not resolve officer '{OfficerInstanceID}'."
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
                        Amount,
                        AmountBinding,
                        RollInteger,
                        context,
                        "IncreaseForceRank",
                        "Amount"
                    )
                    : (
                        PercentOfStored.HasValue ? checked(stored * PercentOfStored.Value / 100)
                        : PercentOfEffective.HasValue
                            ? checked(effective * PercentOfEffective.Value / 100)
                        : Math.Max(
                            MinimumAmount,
                            checked(
                                Math.Max(0, referenceOfficer.ForceRank - effective)
                                * PercentOfPositiveGap.Value
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
    }

    /// <summary>
    /// Resolves one officer's effective rating through an authored probability table.
    /// </summary>
    [PersistableObject(Name = "PerformSkillCheck")]
    public sealed class PerformSkillCheckAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public OfficerRating Rating { get; set; }

        [PersistableAttribute]
        public string ProbabilityTable { get; set; }

        [PersistableAttribute]
        public int RatingMultiplier { get; set; } = 1;

        public List<GameAction> OnSuccess { get; set; } = new List<GameAction>();
        public List<GameAction> OnFailure { get; set; } = new List<GameAction>();

        /// <summary>
        /// Performs the authored officer skill check and executes its matching branch.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"PerformSkillCheck could not resolve officer '{OfficerInstanceID}'."
                );
            if (Rating == OfficerRating.None)
                throw new InvalidOperationException("PerformSkillCheck requires a rating.");
            if (RatingMultiplier == 0)
                throw new InvalidOperationException(
                    "PerformSkillCheck RatingMultiplier cannot be zero."
                );

            GameConfig.MissionProbabilityTablesConfig tables = context
                .Game
                .Config
                ?.ProbabilityTables
                ?.Mission;
            if (tables?.GetSuccessTable(ProbabilityTable) == null)
                throw new InvalidOperationException(
                    $"PerformSkillCheck could not resolve probability table '{ProbabilityTable}'."
                );

            int probability = tables.GetSuccessProbability(
                ProbabilityTable,
                checked(officer.GetEffectiveRating(Rating) * RatingMultiplier)
            );
            bool succeeded = context.Random.NextDouble() * 100 < probability;
            IEnumerable<GameAction> actions = succeeded ? OnSuccess : OnFailure;
            GameAction.ExecuteAll(actions, context);
            return;
        }
    }

    /// <summary>
    /// Marks one officer as having latent Force potential.
    /// </summary>
    [PersistableObject(Name = "SetForceSensitive")]
    public sealed class SetForceSensitiveAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        /// <summary>
        /// Marks the configured officer as Force-sensitive without revealing that potential.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetForceSensitive could not resolve officer '{OfficerInstanceID}'."
                );
            officer.IsForceSensitive = true;
            return;
        }
    }

    /// <summary>
    /// Reveals one Force-sensitive officer's potential and initializes usable Force progression.
    /// </summary>
    [PersistableObject(Name = "SetForceEligible")]
    public sealed class SetForceEligibleAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        /// <summary>
        /// Reveals and initializes an officer's existing latent Force potential once.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetForceEligible could not resolve officer '{OfficerInstanceID}'."
                );
            if (!officer.IsForceSensitive)
                throw new InvalidOperationException(
                    $"SetForceEligible requires Force-sensitive officer '{OfficerInstanceID}'."
                );
            if (officer.IsForceEligible)
                return;

            officer.IsForceEligible = true;
            int startingValue =
                officer.JediLevel + context.Random.NextInt(0, officer.JediLevelVariance + 1);
            officer.ForceValue = Math.Max(officer.ForceValue, startingValue);
            return;
        }
    }

    /// <summary>
    /// Applies a data-authored inclusive random injury range to one officer.
    /// </summary>
    [PersistableObject(Name = "ApplyOfficerInjury")]
    public sealed class ApplyOfficerInjuryAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
        public int MinimumInjury { get; set; }
        public int MaximumInjury { get; set; }

        /// <summary>
        /// Rolls and applies an injury within the authored severity range.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(
                OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"ApplyOfficerInjury could not resolve officer '{OfficerInstanceID}'."
                );

            int injury = context.Random.NextInt(MinimumInjury, checked(MaximumInjury + 1));
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
    }

    /// <summary>
    /// Replaces the authored image paths used for an officer.
    /// </summary>
    [PersistableObject(Name = "SetOfficerImages")]
    public sealed class SetOfficerImagesAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
        public string DisplayImagePath { get; set; }
        public string SmallDisplayImagePath { get; set; }
        public string MessageImagePath { get; set; }
        public string EncyclopediaImagePath { get; set; }

        /// <summary>
        /// Merges authored image paths into the officer's active image set.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(
                OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerImages could not resolve officer '{OfficerInstanceID}'."
                );
            officer.ImageSet.MergeFrom(
                new OfficerImageSet
                {
                    DisplayImagePath = DisplayImagePath,
                    SmallDisplayImagePath = SmallDisplayImagePath,
                    MessageImagePath = MessageImagePath,
                    EncyclopediaImagePath = EncyclopediaImagePath,
                }
            );
            officer.ApplyImageSet();
            return;
        }
    }

    /// <summary>
    /// Replaces selected officer voice-line collections with authored asset paths.
    /// </summary>
    [PersistableObject(Name = "SetOfficerVoiceSet")]
    public sealed class SetOfficerVoiceSetAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableCollectionItem(Name = "Path")]
        public List<string> Order { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> PersonnelArrived { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionSuccess { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionFailure { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> MissionAbort { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> Released { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> Recovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> EnemyDetected { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceGrowth { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> ForceUserDiscovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> TraitorDiscovered { get; set; } = new List<string>();

        [PersistableCollectionItem(Name = "Path")]
        public List<string> RescueAttempt { get; set; } = new List<string>();

        /// <summary>
        /// Merges authored voice categories into the officer's active voice set.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(
                OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerVoiceSet could not resolve officer '{OfficerInstanceID}'."
                );

            officer.VoiceSet.MergeFrom(
                new OfficerVoiceSet
                {
                    OrderPaths = Order,
                    PersonnelArrivedPaths = PersonnelArrived,
                    MissionSuccessPaths = MissionSuccess,
                    MissionFailurePaths = MissionFailure,
                    MissionAbortPaths = MissionAbort,
                    ReleasedPaths = Released,
                    RecoveredPaths = Recovered,
                    EnemyDetectedPaths = EnemyDetected,
                    ForceGrowthPaths = ForceGrowth,
                    ForceUserDiscoveredPaths = ForceUserDiscovered,
                    TraitorDiscoveredPaths = TraitorDiscovered,
                    RescueAttemptPaths = RescueAttempt,
                }
            );
            return;
        }
    }

    /// <summary>
    /// Requests resolution of a duel between two officers.
    /// </summary>
    [PersistableObject(Name = "TriggerDuel")]
    public sealed class TriggerDuelAction : GameAction
    {
        [PersistableAttribute]
        public string FirstOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string SecondOfficerInstanceID { get; set; }

        public string ImagePath { get; set; }
        public string AudioPath { get; set; }

        /// <summary>
        /// Requests a duel between the two authored officers.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer first = game.GetSceneNodeByInstanceID<Officer>(FirstOfficerInstanceID);
            Officer second = game.GetSceneNodeByInstanceID<Officer>(SecondOfficerInstanceID);
            if (first == null || second == null)
                throw new InvalidOperationException(
                    $"TriggerDuel could not resolve officers '{FirstOfficerInstanceID}' and '{SecondOfficerInstanceID}'."
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

            context.Request(
                new DuelRequest
                {
                    EncounteredOfficer = first,
                    OpposingOfficer = second,
                    ImagePath = ImagePath,
                    AudioPath = AudioPath,
                    Tick = game.CurrentTick,
                }
            );
        }
    }
    #endregion

    #region PresentationActions
    /// <summary>
    /// Resolves canonical game entities targeted by presentation actions.
    /// </summary>
    internal static class DisplayActionTargets
    {
        /// <summary>
        /// Resolves the union of an explicit instance and selector results into unique registered
        /// game entities, failing when the action would mutate no valid target.
        /// </summary>
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
                selector.Select(context.Game, context.Random, context.Evaluation)
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
    /// Replaces the display name of every explicitly named or selected entity.
    /// </summary>
    [PersistableObject(Name = "SetDisplayName")]
    public sealed class SetDisplayNameAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableAttribute]
        public string Name { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Resolves all authored targets and applies the configured display name.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.ResolveTargets(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "SetDisplayName"
                )
            )
            {
                if (target is CapitalShip capitalShip)
                    capitalShip.AssignName(Name);
                else
                    target.DisplayName = Name;
            }
            return;
        }
    }

    /// <summary>
    /// Replaces the optional status text of every explicitly named or selected entity.
    /// </summary>
    [PersistableObject(Name = "SetDisplayStatus")]
    public sealed class SetDisplayStatusAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableAttribute]
        public string Status { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Resolves all authored targets and applies the configured status text.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.ResolveTargets(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "SetDisplayStatus"
                )
            )
                target.DisplayStatus = Status;
            return;
        }
    }

    /// <summary>
    /// Clears the optional status text of every explicitly named or selected entity.
    /// </summary>
    [PersistableObject(Name = "ClearDisplayStatus")]
    public sealed class ClearDisplayStatusAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Resolves all authored targets and removes their current status text.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.ResolveTargets(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "ClearDisplayStatus"
                )
            )
                target.DisplayStatus = null;
            return;
        }
    }
    #endregion

    #region PlanetActions
    /// <summary>
    /// Provides shared targeting and adjustment behavior for one concrete planet value.
    /// </summary>
    public abstract class ChangePlanetValueAction : GameAction
    {
        // Planet Targets.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Adjustment.
        public int? Amount { get; set; }
        public string AmountBinding { get; set; }
        public RollInteger RollInteger { get; set; }
        public int? PercentOfCurrent { get; set; }

        // Additional Planet Targets.
        [PersistableMember(Name = "Planets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        internal abstract PlanetChangeCategory Category { get; }

        /// <summary>
        /// Reads the concrete value changed by the action.
        /// </summary>
        /// <param name="planet">The planet being changed.</param>
        /// <returns>The current value.</returns>
        internal abstract int GetValue(Planet planet);

        /// <summary>
        /// Writes the concrete value changed by the action.
        /// </summary>
        /// <param name="planet">The planet being changed.</param>
        /// <param name="value">The replacement value.</param>
        internal abstract void SetValue(Planet planet, int value);

        /// <summary>
        /// Applies one signed adjustment to every resolved planet.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            bool hasAmount =
                Amount.HasValue || !string.IsNullOrWhiteSpace(AmountBinding) || RollInteger != null;
            if ((hasAmount ? 1 : 0) + (PercentOfCurrent.HasValue ? 1 : 0) != 1)
                throw new InvalidOperationException(
                    $"{GetType().Name} requires exactly one amount or percentage adjustment."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (
                Planet planet in PlanetActionTargets.Resolve(
                    PlanetInstanceID,
                    PlanetBinding,
                    Selectors,
                    context,
                    GetType().Name
                )
            )
            {
                int oldValue = GetValue(planet);
                int adjustment = hasAmount
                    ? GameActionNumericValue.ResolveInteger(
                        Amount,
                        AmountBinding,
                        RollInteger,
                        context,
                        GetType().Name,
                        "Amount"
                    )
                    : checked(oldValue * PercentOfCurrent.Value / 100);
                int newValue = Math.Max(0, checked(oldValue + adjustment));
                SetValue(planet, newValue);
                results.Add(
                    PlanetActionResults.Create(context.Game, planet, Category, oldValue, newValue)
                );
            }
            context.Record(results);
        }
    }

    /// <summary>
    /// Changes the number of raw-resource nodes available on selected planets.
    /// </summary>
    [PersistableObject(Name = "ChangeRawResourceNodes")]
    public sealed class ChangeRawResourceNodesAction : ChangePlanetValueAction
    {
        internal override PlanetChangeCategory Category => PlanetChangeCategory.RawMaterial;

        /// <summary>
        /// Returns the planet's current raw-resource node count.
        /// </summary>
        /// <param name="planet">The planet being changed.</param>
        /// <returns>The current raw-resource node count.</returns>
        internal override int GetValue(Planet planet) => planet.NumRawResourceNodes;

        /// <summary>
        /// Sets the planet's raw-resource node count.
        /// </summary>
        /// <param name="planet">The planet being changed.</param>
        /// <param name="value">The replacement raw-resource node count.</param>
        internal override void SetValue(Planet planet, int value) =>
            planet.NumRawResourceNodes = value;
    }

    /// <summary>
    /// Changes the energy capacity available on selected planets.
    /// </summary>
    [PersistableObject(Name = "ChangeEnergyCapacity")]
    public sealed class ChangeEnergyCapacityAction : ChangePlanetValueAction
    {
        internal override PlanetChangeCategory Category => PlanetChangeCategory.Energy;

        /// <summary>
        /// Returns the planet's current energy capacity.
        /// </summary>
        /// <param name="planet">The planet being changed.</param>
        /// <returns>The current energy capacity.</returns>
        internal override int GetValue(Planet planet) => planet.EnergyCapacity;

        /// <summary>
        /// Sets the planet's energy capacity.
        /// </summary>
        /// <param name="planet">The planet being changed.</param>
        /// <param name="value">The replacement energy capacity.</param>
        internal override void SetValue(Planet planet, int value) => planet.EnergyCapacity = value;
    }

    /// <summary>
    /// Changes one faction's popular support on selected planets by a signed amount.
    /// </summary>
    [PersistableObject(Name = "ChangePopularSupport")]
    public sealed class ChangePopularSupportAction : GameAction
    {
        // Faction.
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        // Planet Targets.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Adjustment.
        public int? Amount { get; set; }
        public string AmountBinding { get; set; }
        public RollInteger RollInteger { get; set; }
        public int? PercentOfCurrent { get; set; }

        // Additional Planet Targets.
        [PersistableMember(Name = "Planets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies the authored support change to every resolved planet.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            Faction faction = PopularSupportChange.ResolveFaction(
                context.Game,
                FactionInstanceID,
                "ChangePopularSupport"
            );
            bool hasAmount =
                Amount.HasValue || !string.IsNullOrWhiteSpace(AmountBinding) || RollInteger != null;
            if ((hasAmount ? 1 : 0) + (PercentOfCurrent.HasValue ? 1 : 0) != 1)
                throw new InvalidOperationException(
                    "ChangePopularSupport requires exactly one amount or percentage adjustment."
                );

            foreach (
                Planet planet in PlanetActionTargets.Resolve(
                    PlanetInstanceID,
                    PlanetBinding,
                    Selectors,
                    context,
                    "ChangePopularSupport"
                )
            )
            {
                int oldValue = planet.GetPopularSupport(FactionInstanceID);
                int adjustment = hasAmount
                    ? GameActionNumericValue.ResolveInteger(
                        Amount,
                        AmountBinding,
                        RollInteger,
                        context,
                        "ChangePopularSupport",
                        "Amount"
                    )
                    : checked(oldValue * PercentOfCurrent.Value / 100);
                PopularSupportChange.Apply(
                    context,
                    planet,
                    faction,
                    checked(oldValue + adjustment)
                );
            }
        }
    }

    /// <summary>
    /// Sets one faction's popular support on selected planets to an absolute value.
    /// </summary>
    [PersistableObject(Name = "SetPopularSupport")]
    public sealed class SetPopularSupportAction : GameAction
    {
        // Faction.
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        // Planet Targets.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Support Value.
        public int? Support { get; set; }
        public string SupportBinding { get; set; }
        public RollInteger RollInteger { get; set; }

        // Additional Planet Targets.
        [PersistableMember(Name = "Planets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Sets the authored faction support on every resolved planet.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            Faction faction = PopularSupportChange.ResolveFaction(
                context.Game,
                FactionInstanceID,
                "SetPopularSupport"
            );
            int support = GameActionNumericValue.ResolveInteger(
                Support,
                SupportBinding,
                RollInteger,
                context,
                "SetPopularSupport",
                "Support"
            );
            foreach (
                Planet planet in PlanetActionTargets.Resolve(
                    PlanetInstanceID,
                    PlanetBinding,
                    Selectors,
                    context,
                    "SetPopularSupport"
                )
            )
                PopularSupportChange.Apply(context, planet, faction, support);
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
    /// Reduces raw-resource nodes and energy capacity using independent per-point rolls.
    /// </summary>
    [PersistableObject(Name = "DamagePlanetResources")]
    public sealed class DamagePlanetResourcesAction : GameAction
    {
        // Planet Target.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Damage Probability.
        [PersistableAttribute(Name = "LossProbabilityPerResource")]
        public double? LossProbabilityPerResource { get; set; }

        [PersistableAttribute]
        public string ProbabilityBinding { get; set; }

        public RollDouble RollDouble { get; set; }

        [PersistableAttribute(Name = "MinimumTotalLoss")]
        public int MinimumTotalLoss { get; set; } = 1;

        /// <summary>
        /// Randomly reduces planet resources while enforcing the minimum total loss.
        /// </summary>
        /// <param name="context">The current action execution context.</param>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Planet planet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            if (planet == null)
                throw new InvalidOperationException("DamagePlanetResources requires a planet.");

            double probability = GameActionNumericValue.ResolveDouble(
                LossProbabilityPerResource,
                ProbabilityBinding,
                RollDouble,
                context,
                "DamagePlanetResources",
                "LossProbabilityPerResource"
            );
            if (double.IsNaN(probability) || probability < 0 || probability > 1)
                throw new InvalidOperationException(
                    "DamagePlanetResources.LossProbabilityPerResource must be between zero and one."
                );
            if (MinimumTotalLoss < 0)
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
            int requiredLoss = Math.Min(MinimumTotalLoss, availableResources);
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
            ).SelectMany(selector => selector.Select(game, context.Random, context.Evaluation));
            Planet explicitPlanet = !string.IsNullOrWhiteSpace(planetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(planetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(planetInstanceID);
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

    #endregion

    #region UnitActions
    [PersistableObject(Name = "DestroyUnits")]
    public sealed class DestroyUnitsAction : GameAction
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        [PersistableMember(Name = "Units")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Deletes every unit selected by the authored unit selectors.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            if (Selectors.Count == 0)
                throw new InvalidOperationException("DestroyUnits requires at least one selector.");
            HashSet<ISceneNode> selected = Selectors
                .SelectMany(selector => selector.Select(game, context.Random, context.Evaluation))
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

            Planet planet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);

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
        private static bool HasSelectedAncestor(ISceneNode unit, HashSet<ISceneNode> selected)
        {
            for (ISceneNode parent = unit.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (selected.Contains(parent))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A data-defined request to change ownership of selected planets or units.
    /// </summary>
    [PersistableObject(Name = "ChangeOwner")]
    public sealed class ChangeOwnerAction : GameAction
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        public List<GameEventSelector> Planets { get; set; } = new List<GameEventSelector>();

        public List<GameEventSelector> Units { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Resolves exactly one ownership domain and delegates the change to gameplay.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            bool hasPlanets = Planets.Count > 0;
            bool hasUnits = Units.Count > 0;
            if (hasPlanets == hasUnits)
                throw new InvalidOperationException(
                    "ChangeOwner requires exactly one of Planets or Units."
                );

            Faction faction = context.Game.GetFactionByOwnerInstanceID(FactionInstanceID);
            List<ISceneNode> selected = (hasPlanets ? Planets : Units)
                .SelectMany(selector =>
                    selector.Select(context.Game, context.Random, context.Evaluation)
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

            context.Request(
                new OwnershipChangeRequest
                {
                    NewOwner = faction,
                    Planets = selected.OfType<Planet>().ToList(),
                    Units = hasUnits ? selected : new List<ISceneNode>(),
                    Tick = context.Game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Returns whether ownership can be transferred for the selected unit type.
        /// </summary>
        private static bool IsSupportedUnit(ISceneNode node) =>
            node is Officer
            || node is CapitalShip
            || node is Starfighter
            || node is Regiment
            || node is SpecialForces
            || node is Building;
    }

    /// <summary>
    /// Resolves canonical movable units and valid destination containers.
    /// </summary>
    internal static class UnitActionTargets
    {
        /// <summary>
        /// Resolves explicit and selected movable units for an action.
        /// </summary>
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
                        ? source.Spawn(context)
                        : throw new InvalidOperationException(
                            $"{actionName} cannot use SpawnUnits as a unit source."
                        )
                )
                .ToList();
            IEnumerable<ISceneNode> selected = sources
                .Where(source => source is not SpawnUnits)
                .SelectMany(selector => selector.Select(game, context.Random, context.Evaluation));
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
                ? ((SelectFirst)destinationSelectors[0]).SelectCandidates(
                    game,
                    context.Random,
                    context.Evaluation
                )
                : destinationSelectors.SelectMany(selector =>
                    selector.Select(game, context.Random, context.Evaluation)
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
    }

    public abstract class UnitTransferAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableAttribute]
        public string DestinationInstanceID { get; set; }

        public List<GameEventSelector> Units { get; set; } = new List<GameEventSelector>();

        public List<GameEventSelector> Destination { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Resolves the units and destinations shared by transit-based transfer actions.
        /// </summary>
        protected (List<IMovable> Units, List<ContainerNode> Destinations) Resolve(
            GameActionContext context,
            string actionName
        ) =>
            (
                UnitActionTargets.ResolveUnits(UnitInstanceID, Units, context, actionName),
                UnitActionTargets.ResolveDestinations(
                    DestinationInstanceID,
                    Destination,
                    context,
                    actionName
                )
            );
    }

    /// <summary>
    /// Places one or more units at a destination without transit time.
    /// </summary>
    [PersistableObject(Name = "PlaceUnits")]
    public sealed class PlaceUnitsAction : UnitTransferAction
    {
        /// <summary>
        /// Requests immediate placement of the resolved units at the resolved destination.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            List<ContainerNode> destinations = UnitActionTargets.ResolveDestinations(
                DestinationInstanceID,
                Destination,
                context,
                "PlaceUnits"
            );
            List<IMovable> units = UnitActionTargets.ResolveUnits(
                UnitInstanceID,
                Units,
                context,
                "PlaceUnits",
                allowSpawn: true
            );
            context.Request(
                new UnitPlacementRequest
                {
                    Units = units,
                    Destinations = destinations,
                    Tick = context.Game.CurrentTick,
                }
            );
        }
    }

    /// <summary>
    /// Supplies newly instantiated units from one registered content definition.
    /// </summary>
    [PersistableObject]
    public sealed class SpawnUnits : GameEventSelector
    {
        [PersistableAttribute]
        public string TypeID { get; set; }

        [PersistableAttribute]
        public int Count { get; set; } = 1;

        [PersistableAttribute]
        public string OwnerFactionInstanceID { get; set; }

        /// <summary>
        /// Creates detached runtime units for immediate placement.
        /// </summary>
        internal IEnumerable<ISceneNode> Spawn(GameActionContext context)
        {
            if (context.UnitFactory == null)
                throw new InvalidOperationException(
                    "SpawnUnits requires the active content unit factory."
                );
            if (string.IsNullOrWhiteSpace(TypeID) || Count < 1)
                throw new InvalidOperationException(
                    "SpawnUnits requires a TypeID and a positive Count."
                );
            context.Game.GetFactionByOwnerInstanceID(OwnerFactionInstanceID);

            for (int index = 0; index < Count; index++)
            {
                ISceneNode unit = context.UnitFactory.Create(TypeID, OwnerFactionInstanceID);
                unit.InstanceID = Guid.NewGuid().ToString("N");
                yield return unit;
            }
        }

        /// <summary>
        /// Rejects use as a general selector because spawned units require immediate placement.
        /// </summary>
        internal override IEnumerable<ISceneNode> Select(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
        )
        {
            throw new InvalidOperationException(
                "SpawnUnits may only be used as a PlaceUnits unit source."
            );
        }
    }

    /// <summary>
    /// Sends one or more units through normal movement and transit.
    /// </summary>
    [PersistableObject(Name = "SendUnits")]
    public sealed class SendUnitsAction : UnitTransferAction
    {
        /// <summary>
        /// Requests normal transit for the resolved units to the resolved destination.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            (List<IMovable> units, List<ContainerNode> destinations) = Resolve(
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
            context.Request(
                new UnitMovementRequest
                {
                    Units = units,
                    Destinations = destinations,
                    Tick = context.Game.CurrentTick,
                }
            );
        }
    }

    /// <summary>
    /// Defines whether selected scene nodes participate in active gameplay.
    /// </summary>
    public enum SceneNodeState
    {
        /// <summary>
        /// The node participates in normal gameplay queries.
        /// </summary>
        Active,

        /// <summary>
        /// The node remains retained but is excluded from normal gameplay queries.
        /// </summary>
        Inactive,
    }

    /// <summary>
    /// Sets the gameplay state of one or more retained scene nodes.
    /// </summary>
    [PersistableObject(Name = "SetNodeState")]
    public sealed class SetNodeStateAction : GameAction
    {
        [PersistableAttribute]
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public SceneNodeState State { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies the authored local active state to every selected scene node.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            IEnumerable<ISceneNode> selected = Selectors.SelectMany(selector =>
                selector.Select(context.Game, context.Random, context.Evaluation)
            );
            ISceneNode explicitNode = string.IsNullOrWhiteSpace(InstanceID)
                ? null
                : context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                    InstanceID,
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
                node.IsEnabled = State == SceneNodeState.Active;
            return;
        }
    }
    #endregion
}
