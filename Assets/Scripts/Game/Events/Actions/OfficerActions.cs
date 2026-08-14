using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
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

        [PersistableInlineCollection]
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
    [PersistableObject(Name = "AdjustOfficerStat")]
    public sealed class AdjustOfficerStatAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public OfficerStat Stat { get; set; }

        public int? Amount { get; set; }
        public int? PercentOfStored { get; set; }
        public int? PercentOfEffective { get; set; }
        public int? PercentOfPositiveGap { get; set; }
        public string ReferenceOfficerInstanceID { get; set; }
        public int MinimumAmount { get; set; }

        [PersistableInlineCollection]
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
                    "AdjustOfficerStat requires exactly one adjustment value."
                );
            Officer referenceOfficer = null;
            if (PercentOfPositiveGap.HasValue)
            {
                referenceOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    ReferenceOfficerInstanceID
                );
                if (referenceOfficer == null)
                    throw new InvalidOperationException(
                        $"AdjustOfficerStat could not resolve reference officer '{ReferenceOfficerInstanceID}'."
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
                        $"AdjustOfficerStat could not resolve officer '{OfficerInstanceID}'."
                    );
                selected = new ISceneNode[] { explicitOfficer }.Concat(selected);
            }
            List<ISceneNode> nodes = selected.Distinct().ToList();
            if (nodes.Count == 0)
                throw new InvalidOperationException(
                    "AdjustOfficerStat requires an officer or a matching selector."
                );
            if (nodes.Any(node => node is not Officer))
                throw new InvalidOperationException(
                    "AdjustOfficerStat selectors may return only officers."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in nodes.Cast<Officer>())
            {
                int baseValue = officer.GetBaseStat(Stat);
                int currentValue = officer.GetCurrentStat(Stat);
                int adjustment =
                    Amount
                    ?? (
                        PercentOfStored.HasValue ? checked(baseValue * PercentOfStored.Value / 100)
                        : PercentOfEffective.HasValue
                            ? checked(currentValue * PercentOfEffective.Value / 100)
                        : Math.Max(
                            MinimumAmount,
                            checked(
                                Math.Max(0, referenceOfficer.GetCurrentStat(Stat) - currentValue)
                                * PercentOfPositiveGap.Value
                                / 100
                            )
                        )
                    );
                int previousForceRank = officer.ForceRank;
                officer.SetBaseStat(Stat, checked(baseValue + adjustment));
                if (Stat != OfficerStat.Force)
                    continue;
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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
                    Order = Order,
                    PersonnelArrived = PersonnelArrived,
                    MissionSuccess = MissionSuccess,
                    MissionFailure = MissionFailure,
                    MissionAbort = MissionAbort,
                    Released = Released,
                    Recovered = Recovered,
                    EnemyDetected = EnemyDetected,
                    ForceGrowth = ForceGrowth,
                    ForceUserDiscovered = ForceUserDiscovered,
                    TraitorDiscovered = TraitorDiscovered,
                    RescueAttempt = RescueAttempt,
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
}
