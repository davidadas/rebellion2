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
    #region CompositeActions
    [PersistableObject(Name = "Outcome")]
    public sealed class RandomOutcome
    {
        [PersistableAttribute]
        public int Weight { get; set; } = 1;

        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();

        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    [PersistableObject(Name = "RollRandom")]
    public sealed class RollRandomAction : GameAction
    {
        public List<RandomOutcome> Outcomes { get; set; } = new List<RandomOutcome>();

        /// <summary>
        /// Executes one eligible outcome selected by its authored weight.
        /// </summary>
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
        public int Operand { get; set; }

        /// <summary>
        /// Applies the authored operation to one event-runtime variable.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            int previousValue = context.Game.EventRuntime.GetVariable(Key);
            int currentValue = Operation switch
            {
                EventVariableOperation.Set => Operand,
                EventVariableOperation.Add => checked(previousValue + Operand),
                EventVariableOperation.Minimum => Math.Min(previousValue, Operand),
                EventVariableOperation.Maximum => Math.Max(previousValue, Operand),
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
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public OfficerRating Rating { get; set; }

        public int? Amount { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }

        [PersistableAttribute]
        public string ReferenceOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public int MinimumAmount { get; set; }

        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies the authored rating change to every selected officer.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            int modeCount = new int?[]
            {
                Amount,
                PercentOfStored,
                PercentOfEffective,
                PercentOfPositiveGap,
            }.Count(value => value.HasValue);
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
                int adjustment =
                    Amount
                    ?? (
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
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        public int? Amount { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }

        [PersistableAttribute]
        public string ReferenceOfficerInstanceID { get; set; }

        [PersistableAttribute]
        public int MinimumAmount { get; set; }

        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies one positive Force increase mode to every explicitly named or selected officer.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            int modeCount = new int?[]
            {
                Amount,
                PercentOfStored,
                PercentOfEffective,
                PercentOfPositiveGap,
            }.Count(value => value.HasValue);
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
                int increase =
                    Amount
                    ?? (
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
                target.DisplayName = Name;
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

    #region ResourceActions
    /// <summary>
    /// Applies one explicit signed resource adjustment to the scoped planet.
    /// </summary>
    [PersistableObject(Name = "ChangePlanetStat")]
    public sealed class ChangePlanetStatAction : GameAction
    {
        [PersistableAttribute]
        public PlanetStat Stat { get; set; }

        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        public int? Amount { get; set; }
        public int? PercentOfCurrent { get; set; }

        [PersistableMember(Name = "Planets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies one signed adjustment to the selected planet statistic.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            if ((Amount.HasValue ? 1 : 0) + (PercentOfCurrent.HasValue ? 1 : 0) != 1)
                throw new InvalidOperationException(
                    "ChangePlanetStat requires exactly one adjustment value."
                );
            IEnumerable<ISceneNode> selected = Selectors.SelectMany(selector =>
                selector.Select(game, context.Random, context.Evaluation)
            );
            Planet explicitPlanet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            if (explicitPlanet != null)
                selected = new ISceneNode[] { explicitPlanet }.Concat(selected);
            List<ISceneNode> nodes = selected.Distinct().ToList();
            if (nodes.Count == 0)
                throw new InvalidOperationException(
                    "ChangePlanetStat requires a planet, planet binding, or matching selector."
                );
            if (nodes.Any(node => node is not Planet))
                throw new InvalidOperationException(
                    "ChangePlanetStat selectors may return only planets."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (Planet planet in nodes.Cast<Planet>())
            {
                int oldValue = planet.GetStatValue(Stat);
                int adjustment = Amount ?? checked(oldValue * PercentOfCurrent.Value / 100);
                int newValue = Math.Max(0, checked(oldValue + adjustment));
                PlanetChangeCategory resultCategory;
                if (Stat == PlanetStat.RawResourceNodes)
                {
                    resultCategory = PlanetChangeCategory.RawMaterial;
                    planet.NumRawResourceNodes = newValue;
                }
                else
                {
                    resultCategory = PlanetChangeCategory.Energy;
                    planet.EnergyCapacity = newValue;
                }
                Faction faction = FindOwner(game, planet);
                results.Add(
                    new PlanetStatChangedResult
                    {
                        Planet = planet,
                        Faction = faction,
                        Category = resultCategory,
                        OldValue = oldValue,
                        NewValue = newValue,
                        Tick = game.CurrentTick,
                    }
                );
            }
            context.Record(results);
        }

        /// <summary>
        /// Resolves the faction that currently owns the planet.
        /// </summary>
        private static Faction FindOwner(GameRoot game, Planet planet) =>
            game.GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == planet.OwnerInstanceID);
    }

    /// <summary>
    /// Reduces selected planet stats by independently rolling once for each current point.
    /// </summary>
    [PersistableObject(Name = "DamagePlanetStats")]
    public sealed class DamagePlanetStatsAction : GameAction
    {
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        [PersistableAttribute(Name = "LossProbabilityPerResource")]
        public double LossProbabilityPerResource { get; set; } = 0.05;

        [PersistableAttribute(Name = "MinimumTotalLoss")]
        public int MinimumTotalLoss { get; set; } = 1;

        public List<PlanetStatReference> Stats { get; set; } = new List<PlanetStatReference>();

        /// <summary>
        /// Randomly reduces selected planet statistics while enforcing the minimum total loss.
        /// </summary>
        internal override void Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Planet planet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Evaluation?.GetBindingReference<Planet>(PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            if (planet == null)
                throw new InvalidOperationException("DamagePlanetStats requires a planet.");

            List<PlanetStat> selectedStats = Stats.Select(stat => stat.Stat).Distinct().ToList();
            if (selectedStats.Count == 0)
                throw new InvalidOperationException(
                    "DamagePlanetStats requires at least one planet stat."
                );
            Dictionary<PlanetStat, int> oldValues = selectedStats.ToDictionary(
                stat => stat,
                stat => planet.GetStatValue(stat)
            );
            if (oldValues.Values.Sum() == 0)
                return;

            if (LossProbabilityPerResource < 0 || LossProbabilityPerResource > 1)
                throw new InvalidOperationException(
                    "DamagePlanetStats.LossProbabilityPerResource must be between zero and one."
                );
            if (MinimumTotalLoss < 0)
                throw new InvalidOperationException(
                    "DamagePlanetStats.MinimumTotalLoss cannot be negative."
                );

            Dictionary<PlanetStat, int> losses = selectedStats.ToDictionary(stat => stat, _ => 0);
            foreach (PlanetStat stat in selectedStats)
            {
                for (int iteration = 0; iteration < oldValues[stat]; iteration++)
                {
                    if (RollProbability(context.Random, LossProbabilityPerResource))
                        losses[stat]++;
                }
            }

            int requiredLoss = Math.Min(MinimumTotalLoss, oldValues.Values.Sum());
            while (losses.Values.Sum() < requiredLoss)
            {
                PlanetStat? available = selectedStats
                    .Where(stat => oldValues[stat] - losses[stat] > 0)
                    .Cast<PlanetStat?>()
                    .FirstOrDefault();
                if (!available.HasValue)
                    break;
                losses[available.Value]++;
            }

            List<GameResult> results = new List<GameResult>();
            foreach (PlanetStat stat in selectedStats)
            {
                int newValue = oldValues[stat] - losses[stat];
                if (stat == PlanetStat.RawResourceNodes)
                    planet.NumRawResourceNodes = newValue;
                else
                    planet.EnergyCapacity = newValue;
                AddStatChange(
                    results,
                    game,
                    planet,
                    stat == PlanetStat.RawResourceNodes
                        ? PlanetChangeCategory.RawMaterial
                        : PlanetChangeCategory.Energy,
                    oldValues[stat],
                    newValue
                );
            }
            context.Record(results);
        }

        /// <summary>
        /// Rolls a normalized probability against the supplied random source.
        /// </summary>
        private static bool RollProbability(IRandomNumberProvider provider, double probability) =>
            provider.NextDouble() < Math.Min(1.0, Math.Max(0.0, probability));

        /// <summary>
        /// Adds a planet-stat result when the value changed.
        /// </summary>
        private static void AddStatChange(
            ICollection<GameResult> results,
            GameRoot game,
            Planet planet,
            PlanetChangeCategory category,
            int oldValue,
            int newValue
        )
        {
            if (oldValue == newValue)
                return;
            results.Add(
                new PlanetStatChangedResult
                {
                    Planet = planet,
                    Faction = game.GetFactions()
                        .FirstOrDefault(faction => faction.InstanceID == planet.OwnerInstanceID),
                    Category = category,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Tick = game.CurrentTick,
                }
            );
        }
    }

    [PersistableObject(Name = "Stat")]
    public sealed class PlanetStatReference
    {
        [PersistableAttribute(Name = "Name")]
        public PlanetStat Stat { get; set; }
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
        /// <summary>The node participates in normal gameplay queries.</summary>
        Active,

        /// <summary>The node remains retained but is excluded from normal gameplay queries.</summary>
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
