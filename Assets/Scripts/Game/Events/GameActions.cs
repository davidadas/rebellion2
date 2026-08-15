using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Advisor;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
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

        public List<GameConditional> When { get; set; } = new List<GameConditional>();

        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    [PersistableObject(Name = "Random")]
    public sealed class RandomAction : GameAction
    {
        public List<RandomOutcome> Outcomes { get; set; } = new List<RandomOutcome>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            List<RandomOutcome> eligible = Outcomes
                .Where(outcome =>
                    outcome.Weight > 0
                    && outcome.When.All(condition =>
                        condition.IsMet(context.Game, context.Activation)
                    )
                )
                .ToList();
            if (eligible.Count == 0)
                return new List<GameResult>();

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

            return GameAction.ExecuteAll(selected.Actions, context);
        }
    }

    [PersistableObject(Name = "If")]
    public sealed class IfAction : GameAction
    {
        public List<GameConditional> Conditions { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
        public List<GameAction> Else { get; set; } = new List<GameAction>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            IEnumerable<GameAction> selected = Conditions.TrueForAll(condition =>
                condition.IsMet(context.Game, context.Activation)
            )
                ? Actions
                : Else;
            return GameAction.ExecuteAll(selected, context);
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

        public override List<GameResult> Execute(GameActionContext context)
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
            return new List<GameResult>();
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

        [PersistableMember(Name = "Subjects")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            Faction recipient = context.Game.GetFactionByOwnerInstanceID(FactionInstanceID);
            List<ISceneNode> observations = Selectors
                .SelectMany(selector =>
                    selector.Select(context.Game, context.Random, context.Activation)
                )
                .Distinct()
                .ToList();
            if (observations.Count == 0)
                return new List<GameResult>();

            return new List<GameResult>
            {
                new IntelligenceRevealedResult
                {
                    Recipient = recipient,
                    Observations = observations,
                    Tick = context.Game.CurrentTick,
                },
            };
        }
    }
    #endregion

    #region MessageActions
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
            ISceneNode subject = !string.IsNullOrWhiteSpace(SubjectBinding)
                ? context.Activation?.GetBindingReference<ISceneNode>(SubjectBinding)
                : game.GetSceneNodeByInstanceID<ISceneNode>(SubjectInstanceID);
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
            Planet location = !string.IsNullOrWhiteSpace(LocationBinding)
                ? context.Activation?.GetBindingReference<Planet>(LocationBinding)
                : game.GetSceneNodeByInstanceID<Planet>(LocationInstanceID);
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

        public override List<GameResult> Execute(GameActionContext context)
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
                selector.Select(game, context.Random, context.Activation)
            );
            if (!string.IsNullOrWhiteSpace(OfficerInstanceID))
            {
                Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
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
            return results;
        }
    }

    /// <summary>
    /// Adjusts selected officers using one authored calculation.
    /// </summary>
    [PersistableObject(Name = "AdjustOfficerRating")]
    public sealed class AdjustOfficerRatingAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public OfficerRating Rating { get; set; }

        public int? Amount { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }
        public string ReferenceOfficerInstanceID { get; set; }
        public int MinimumAmount { get; set; }

        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
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
                    "AdjustOfficerRating requires exactly one adjustment value."
                );
            Officer referenceOfficer = null;
            if (PercentOfPositiveGap.HasValue)
            {
                referenceOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    ReferenceOfficerInstanceID
                );
                if (referenceOfficer == null)
                    throw new InvalidOperationException(
                        $"AdjustOfficerRating could not resolve reference officer '{ReferenceOfficerInstanceID}'."
                    );
                if (PercentOfPositiveGap.Value < 0 || MinimumAmount < 0)
                    throw new InvalidOperationException(
                        "Rating-gap adjustments require non-negative percentage and minimum values."
                    );
            }

            IEnumerable<ISceneNode> selected = Selectors.SelectMany(selector =>
                selector.Select(game, context.Random, context.Activation)
            );
            if (!string.IsNullOrWhiteSpace(OfficerInstanceID))
            {
                Officer explicitOfficer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
                if (explicitOfficer == null)
                    throw new InvalidOperationException(
                        $"AdjustOfficerRating could not resolve officer '{OfficerInstanceID}'."
                    );
                selected = new ISceneNode[] { explicitOfficer }.Concat(selected);
            }
            List<ISceneNode> nodes = selected.Distinct().ToList();
            if (nodes.Count == 0)
                throw new InvalidOperationException(
                    "AdjustOfficerRating requires an officer or a matching selector."
                );
            if (nodes.Any(node => node is not Officer))
                throw new InvalidOperationException(
                    "AdjustOfficerRating selectors may return only officers."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in nodes.Cast<Officer>())
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
            return results;
        }
    }

    /// <summary>
    /// Adjusts selected officers' stored Force values using one authored calculation.
    /// </summary>
    [PersistableObject(Name = "AdjustOfficerForce")]
    public sealed class AdjustOfficerForceAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        public int? Amount { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }
        public string ReferenceOfficerInstanceID { get; set; }
        public int MinimumAmount { get; set; }

        [PersistableMember(Name = "Officers")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>
        /// Applies one Force adjustment mode to every explicitly named or selected officer.
        /// </summary>
        public override List<GameResult> Execute(GameActionContext context)
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
                    "AdjustOfficerForce requires exactly one adjustment value."
                );

            GameRoot game = context.Game;
            Officer referenceOfficer = null;
            if (PercentOfPositiveGap.HasValue)
            {
                referenceOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    ReferenceOfficerInstanceID
                );
                if (referenceOfficer == null)
                    throw new InvalidOperationException(
                        $"AdjustOfficerForce could not resolve reference officer '{ReferenceOfficerInstanceID}'."
                    );
                if (PercentOfPositiveGap.Value < 0 || MinimumAmount < 0)
                    throw new InvalidOperationException(
                        "Force-gap adjustments require non-negative percentage and minimum values."
                    );
            }

            IEnumerable<ISceneNode> selected = Selectors.SelectMany(selector =>
                selector.Select(game, context.Random, context.Activation)
            );
            if (!string.IsNullOrWhiteSpace(OfficerInstanceID))
            {
                Officer explicitOfficer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
                if (explicitOfficer == null)
                    throw new InvalidOperationException(
                        $"AdjustOfficerForce could not resolve officer '{OfficerInstanceID}'."
                    );
                selected = new ISceneNode[] { explicitOfficer }.Concat(selected);
            }

            List<Officer> officers = selected.Distinct().OfType<Officer>().ToList();
            if (officers.Count == 0)
                throw new InvalidOperationException(
                    "AdjustOfficerForce requires an officer or a matching selector."
                );
            if (selected.Any(node => node is not Officer))
                throw new InvalidOperationException(
                    "AdjustOfficerForce selectors may return only officers."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in officers)
            {
                int stored = officer.ForceValue;
                int effective = officer.ForceRank;
                int adjustment =
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
                int previousForceRank = officer.ForceRank;
                officer.ForceValue = Math.Max(0, checked(stored + adjustment));
                results.Add(
                    new ForceExperienceResult
                    {
                        Officer = officer,
                        ExperienceGained = adjustment,
                        PreviousForceRank = previousForceRank,
                        CurrentForceRank = officer.ForceRank,
                        SuppressRankChangeMessage = true,
                        Tick = game.CurrentTick,
                    }
                );
            }
            return results;
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

        public override List<GameResult> Execute(GameActionContext context)
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
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
            return GameAction.ExecuteAll(actions, context);
        }
    }

    /// <summary>
    /// Sets authored Force-state flags and initializes Force value on eligibility transition.
    /// </summary>
    [PersistableObject(Name = "SetOfficerJediState")]
    public sealed class SetOfficerJediStateAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public bool? IsJedi { get; set; }

        [PersistableAttribute]
        public bool? IsEligible { get; set; }

        /// <summary>Applies authored Jedi eligibility and identity changes to one officer.</summary>
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerJediState could not resolve {nameof(OfficerInstanceID)} '{OfficerInstanceID}'."
                );

            int previousRank = officer.ForceRank;
            bool becameEligible = !officer.IsForceEligible && IsEligible == true;
            if (IsJedi.HasValue)
                officer.IsJedi = IsJedi.Value;
            if (IsEligible.HasValue)
                officer.IsForceEligible = IsEligible.Value;
            if (becameEligible)
            {
                int startingValue =
                    officer.JediLevel + context.Random.NextInt(0, officer.JediLevelVariance + 1);
                officer.ForceValue = Math.Max(officer.ForceValue, startingValue);
            }
            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = Math.Max(0, officer.ForceRank - previousRank),
                    PreviousForceRank = previousRank,
                    CurrentForceRank = officer.ForceRank,
                    SuppressRankChangeMessage = true,
                    Tick = game.CurrentTick,
                },
            };
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

        /// <summary>Rolls and applies an injury within the authored severity range.</summary>
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"ApplyOfficerInjury could not resolve officer '{OfficerInstanceID}'."
                );

            int injury = context.Random.NextInt(MinimumInjury, checked(MaximumInjury + 1));
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

        /// <summary>Merges authored image paths into the officer's active image set.</summary>
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
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
            return new List<GameResult>();
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

        /// <summary>Merges authored voice categories into the officer's active voice set.</summary>
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
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
            return new List<GameResult>();
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

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer first = game.GetSceneNodeByInstanceID<Officer>(FirstOfficerInstanceID);
            Officer second = game.GetSceneNodeByInstanceID<Officer>(SecondOfficerInstanceID);
            if (first == null || second == null)
                throw new InvalidOperationException(
                    $"TriggerDuel could not resolve officers '{FirstOfficerInstanceID}' and '{SecondOfficerInstanceID}'."
                );

            if (context.Activation?.TriggerResult is MissionCompletedResult completion)
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

            return new List<GameResult>
            {
                new DuelRequestedResult
                {
                    EncounteredOfficer = first,
                    OpposingOfficer = second,
                    ImagePath = ImagePath,
                    AudioPath = AudioPath,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
    #endregion

    #region PresentationActions
    internal static class DisplayActionTargets
    {
        internal static List<BaseGameEntity> Resolve(
            string targetInstanceID,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName
        )
        {
            IEnumerable<ISceneNode> selected = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).SelectMany(selector =>
                selector.Select(context.Game, context.Random, context.Activation)
            );
            if (!string.IsNullOrWhiteSpace(targetInstanceID))
            {
                ISceneNode target = context.Game.GetSceneNodeByInstanceID<ISceneNode>(
                    targetInstanceID
                );
                if (target == null)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve target '{targetInstanceID}'."
                    );
                selected = new[] { target }.Concat(selected);
            }

            List<ISceneNode> resolved = selected
                .Where(node => node != null)
                .Select(node => context.Game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID))
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

    [PersistableObject(Name = "SetDisplayName")]
    public sealed class SetDisplayNameAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableAttribute]
        public string Name { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.Resolve(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "SetDisplayName"
                )
            )
                target.DisplayName = Name;
            return new List<GameResult>();
        }
    }

    [PersistableObject(Name = "SetDisplayStatus")]
    public sealed class SetDisplayStatusAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableAttribute]
        public string Status { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.Resolve(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "SetDisplayStatus"
                )
            )
                target.DisplayStatus = Status;
            return new List<GameResult>();
        }
    }

    [PersistableObject(Name = "ClearDisplayStatus")]
    public sealed class ClearDisplayStatusAction : GameAction
    {
        [PersistableAttribute]
        public string TargetInstanceID { get; set; }

        [PersistableMember(Name = "Targets")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            foreach (
                BaseGameEntity target in DisplayActionTargets.Resolve(
                    TargetInstanceID,
                    Selectors,
                    context,
                    "ClearDisplayStatus"
                )
            )
                target.DisplayStatus = null;
            return new List<GameResult>();
        }
    }
    #endregion

    #region ResourceActions
    public enum PlanetStat
    {
        RawResourceNodes,
        EnergyCapacity,
    }

    /// <summary>
    /// Applies one explicit signed resource adjustment to the scoped planet.
    /// </summary>
    [PersistableObject(Name = "AdjustPlanetStat")]
    public sealed class AdjustPlanetStatAction : GameAction
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

        /// <summary>Applies one signed adjustment to the selected planet statistic.</summary>
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            if ((Amount.HasValue ? 1 : 0) + (PercentOfCurrent.HasValue ? 1 : 0) != 1)
                throw new InvalidOperationException(
                    "AdjustPlanetStat requires exactly one adjustment value."
                );
            IEnumerable<ISceneNode> selected = Selectors.SelectMany(selector =>
                selector.Select(game, context.Random, context.Activation)
            );
            Planet explicitPlanet = !string.IsNullOrWhiteSpace(PlanetBinding)
                ? context.Activation?.GetBindingReference<Planet>(PlanetBinding)
                : game.GetSceneNodeByInstanceID<Planet>(PlanetInstanceID);
            explicitPlanet ??= context.Activation?.GetTarget<Planet>();
            if (explicitPlanet != null)
                selected = new ISceneNode[] { explicitPlanet }.Concat(selected);
            List<ISceneNode> nodes = selected.Distinct().ToList();
            if (nodes.Count == 0)
                throw new InvalidOperationException(
                    "AdjustPlanetStat requires a planet, planet binding, target, or matching selector."
                );
            if (nodes.Any(node => node is not Planet))
                throw new InvalidOperationException(
                    "AdjustPlanetStat selectors may return only planets."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (Planet planet in nodes.Cast<Planet>())
            {
                int oldValue = GetValue(planet, Stat);
                int adjustment = Amount ?? checked(oldValue * PercentOfCurrent.Value / 100);
                int newValue = Math.Max(0, checked(oldValue + adjustment));
                PlanetStatType resultStat;
                if (Stat == PlanetStat.RawResourceNodes)
                {
                    resultStat = PlanetStatType.RawMaterial;
                    planet.NumRawResourceNodes = newValue;
                }
                else
                {
                    resultStat = PlanetStatType.Energy;
                    planet.EnergyCapacity = newValue;
                }
                Faction faction = FindOwner(game, planet);
                results.Add(
                    new PlanetStatChangedResult
                    {
                        Planet = planet,
                        Faction = faction,
                        Stat = resultStat,
                        OldValue = oldValue,
                        NewValue = newValue,
                        Tick = game.CurrentTick,
                    }
                );
            }
            return results;
        }

        internal static int GetValue(Planet planet, PlanetStat stat) =>
            stat switch
            {
                PlanetStat.RawResourceNodes => planet.NumRawResourceNodes,
                PlanetStat.EnergyCapacity => planet.EnergyCapacity,
                _ => throw new InvalidOperationException($"Unsupported planet stat '{stat}'."),
            };

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
    [PersistableObject(Name = "ReducePlanetStats")]
    public sealed class ReducePlanetStatsAction : GameAction
    {
        [PersistableAttribute(Name = "LossProbabilityPerResource")]
        public double LossProbabilityPerResource { get; set; } = 0.05;

        [PersistableAttribute(Name = "MinimumTotalLoss")]
        public int MinimumTotalLoss { get; set; } = 1;

        public List<PlanetStatReference> Stats { get; set; } = new List<PlanetStatReference>();

        /// <summary>Randomly reduces selected planet statistics while enforcing the minimum total loss.</summary>
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Planet planet = context.Activation?.GetTarget<Planet>();
            if (planet == null)
                throw new InvalidOperationException("ReducePlanetStats requires a planet target.");

            List<PlanetStat> selectedStats = Stats.Select(stat => stat.Stat).Distinct().ToList();
            if (selectedStats.Count == 0)
                throw new InvalidOperationException(
                    "ReducePlanetStats requires at least one planet stat."
                );
            Dictionary<PlanetStat, int> oldValues = selectedStats.ToDictionary(
                stat => stat,
                stat => AdjustPlanetStatAction.GetValue(planet, stat)
            );
            if (oldValues.Values.Sum() == 0)
                return new List<GameResult>();

            if (LossProbabilityPerResource < 0 || LossProbabilityPerResource > 1)
                throw new InvalidOperationException(
                    "ReducePlanetStats.LossProbabilityPerResource must be between zero and one."
                );
            if (MinimumTotalLoss < 0)
                throw new InvalidOperationException(
                    "ReducePlanetStats.MinimumTotalLoss cannot be negative."
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
                        ? PlanetStatType.RawMaterial
                        : PlanetStatType.Energy,
                    oldValues[stat],
                    newValue
                );
            }
            return results;
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
            PlanetStatType stat,
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
                    Stat = stat,
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

    [PersistableObject(Name = "RecordPlanetIncident")]
    public sealed class RecordPlanetIncidentAction : GameAction
    {
        [PersistableAttribute(Name = "Type")]
        public IncidentType IncidentType { get; set; }

        public override List<GameResult> Execute(GameActionContext context)
        {
            Planet planet = context.Activation?.GetTarget<Planet>();
            if (planet == null)
                throw new InvalidOperationException(
                    "RecordPlanetIncident requires a planet target."
                );

            List<PlanetStatChangedResult> statChanges = context
                .Activation.Results.OfType<PlanetStatChangedResult>()
                .Where(result => result.Planet == planet)
                .ToList();
            List<IGameEntity> destroyed = context
                .Activation.Results.OfType<GameObjectDestroyedResult>()
                .Where(result => result.Context == planet)
                .Select(result => result.DestroyedObject)
                .Where(result => result != null)
                .ToList();
            int severity =
                statChanges.Sum(change => Math.Abs(change.NewValue - change.OldValue))
                + destroyed.Count;
            if (severity == 0)
                return new List<GameResult>();

            return new List<GameResult>
            {
                new PlanetIncidentResult
                {
                    Planet = planet,
                    IncidentType = IncidentType,
                    Severity = severity,
                    DestroyedObjects = destroyed,
                    Tick = context.Game.CurrentTick,
                },
            };
        }
    }
    #endregion

    #region UnitActions
    [PersistableObject(Name = "DestroyUnits")]
    public sealed class DestroyUnitsAction : GameAction
    {
        [PersistableMember(Name = "Units")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>Deletes every unit selected by the authored unit selectors.</summary>
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            if (Selectors.Count == 0)
                throw new InvalidOperationException("DestroyUnits requires at least one selector.");
            HashSet<ISceneNode> selected = Selectors
                .SelectMany(selector => selector.Select(game, context.Random, context.Activation))
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

            Planet planet = context.Activation?.GetTarget<Planet>();

            return destroyed.ConvertAll<GameResult>(unit => new GameObjectDestroyedResult
            {
                DestroyedObject = unit,
                Context = planet,
                Tick = game.CurrentTick,
            });
        }

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

    internal static class UnitActionTargets
    {
        internal static List<IMovable> ResolveUnits(
            string unitInstanceID,
            IEnumerable<GameEventSelector> selectors,
            GameActionContext context,
            string actionName
        )
        {
            GameRoot game = context.Game;
            IEnumerable<ISceneNode> selected = (
                selectors ?? Enumerable.Empty<GameEventSelector>()
            ).SelectMany(selector => selector.Select(game, context.Random, context.Activation));
            if (!string.IsNullOrWhiteSpace(unitInstanceID))
            {
                ISceneNode direct = game.GetSceneNodeByInstanceID<ISceneNode>(unitInstanceID);
                if (direct == null)
                    throw new InvalidOperationException(
                        $"{actionName} could not resolve unit '{unitInstanceID}'."
                    );
                selected = new[] { direct }.Concat(selected);
            }

            List<ISceneNode> resolved = selected
                .Where(node => node != null)
                .Select(node => game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID))
                .Where(node => node != null)
                .GroupBy(node => node.InstanceID, StringComparer.Ordinal)
                .Select(group => group.First())
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
                    context.Activation
                )
                : destinationSelectors.SelectMany(selector =>
                    selector.Select(game, context.Random, context.Activation)
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
        public override List<GameResult> Execute(GameActionContext context)
        {
            (List<IMovable> units, List<ContainerNode> destinations) = Resolve(
                context,
                "PlaceUnits"
            );
            return new List<GameResult>
            {
                new UnitPlacementRequestedResult
                {
                    Units = units,
                    Destination = destinations[0],
                    DestinationCandidates = destinations,
                    Tick = context.Game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Sends one or more units through normal movement and transit.
    /// </summary>
    [PersistableObject(Name = "SendUnits")]
    public sealed class SendUnitsAction : UnitTransferAction
    {
        public override List<GameResult> Execute(GameActionContext context)
        {
            (List<IMovable> units, List<ContainerNode> destinations) = Resolve(
                context,
                "SendUnits"
            );
            if (
                units.Any(unit =>
                    unit is not ISceneNode node
                    || node.GetParent() == null
                    || context.Game.IsInVoid(node)
                )
            )
                throw new InvalidOperationException(
                    "SendUnits requires active units at a valid scene location."
                );
            return new List<GameResult>
            {
                new UnitMovementRequestedResult
                {
                    Units = units,
                    Destinations = destinations,
                    Tick = context.Game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Removes one active unit from the scene graph while retaining it in faction storage.
    /// </summary>
    [PersistableObject(Name = "AddToVoid")]
    public sealed class AddToVoidAction : GameAction
    {
        [PersistableAttribute(Name = "UnitInstanceID")]
        public string UnitInstanceID { get; set; }

        [PersistableMember(Name = "Units")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            List<IMovable> units = UnitActionTargets.ResolveUnits(
                UnitInstanceID,
                Selectors,
                context,
                "AddToVoid"
            );
            foreach (IMovable movable in units)
            {
                ISceneNode unit = (ISceneNode)movable;
                if (unit.GetParent() == null || game.IsInVoid(unit))
                    throw new InvalidOperationException(
                        $"AddToVoid requires an active unit; '{unit.GetDisplayName()}' is not active."
                    );
            }
            foreach (IMovable unit in units)
                game.AddToVoid((ISceneNode)unit);
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Detaches one retained unit from faction void storage.
    /// </summary>
    [PersistableObject(Name = "RemoveFromVoid")]
    public sealed class RemoveFromVoidAction : GameAction
    {
        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        [PersistableMember(Name = "Units")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            List<IMovable> units = UnitActionTargets.ResolveUnits(
                UnitInstanceID,
                Selectors,
                context,
                "RemoveFromVoid"
            );
            foreach (IMovable movable in units)
            {
                ISceneNode unit = (ISceneNode)movable;
                if (!game.IsInVoid(unit))
                    throw new InvalidOperationException(
                        $"RemoveFromVoid requires a retained unit; '{unit.GetDisplayName()}' is not retained."
                    );
            }
            foreach (IMovable unit in units)
                game.RemoveFromVoid((ISceneNode)unit);
            return new List<GameResult>();
        }
    }
    #endregion
}
