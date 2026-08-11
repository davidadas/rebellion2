using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Sets one officer's captivity state and emits the standard state-change result.
    /// </summary>
    [PersistableObject(Name = "SetCaptivity")]
    public sealed class SetCaptivityAction : GameAction
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
            IEnumerable<Officer> officers = Selectors
                .SelectMany(selector => selector.Select(game, context.Random, context.Activation))
                .Cast<Officer>();
            if (!string.IsNullOrWhiteSpace(OfficerInstanceID))
            {
                Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
                if (officer == null)
                    throw new InvalidOperationException(
                        $"SetCaptivity could not resolve officer '{OfficerInstanceID}'."
                    );
                officers = new[] { officer }.Concat(officers);
            }
            List<Officer> selected = officers.Distinct().ToList();
            if (selected.Count == 0)
                throw new InvalidOperationException(
                    "SetCaptivity requires an officer or at least one matching selector."
                );

            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in selected)
            {
                officer.IsCaptured = IsCaptured;
                officer.CaptorInstanceID = IsCaptured ? CaptorFactionInstanceID : null;
                officer.CanEscape = CanEscape;
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
    /// Adjusts one stored officer rating using exactly one authored calculation.
    /// </summary>
    [PersistableObject(Name = "AdjustOfficerRating")]
    public sealed class AdjustOfficerRatingAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public OfficerRating Rating { get; set; }

        public int? Amount { get; set; }
        public int? PercentOfBaseRating { get; set; }
        public int? PercentOfCurrentRating { get; set; }
        public int? PercentOfCurrentRank { get; set; }
        public int? PercentOfPositiveRatingGap { get; set; }
        public string ReferenceOfficerInstanceID { get; set; }
        public int MinimumAmount { get; set; }

        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"AdjustOfficerRating could not resolve officer '{OfficerInstanceID}'."
                );
            if (Rating == OfficerRating.None)
                throw new InvalidOperationException(
                    "AdjustOfficerRating requires a concrete officer rating."
                );
            int modeCount = new int?[]
            {
                Amount,
                PercentOfBaseRating,
                PercentOfCurrentRating,
                PercentOfCurrentRank,
                PercentOfPositiveRatingGap,
            }.Count(value => value.HasValue);
            if (modeCount != 1)
                throw new InvalidOperationException(
                    "AdjustOfficerRating requires exactly one adjustment value."
                );
            if (PercentOfCurrentRank.HasValue && Rating != OfficerRating.Force)
                throw new InvalidOperationException(
                    "PercentOfCurrentRank is only valid for the Force rating."
                );
            Officer referenceOfficer = null;
            if (PercentOfPositiveRatingGap.HasValue)
            {
                referenceOfficer = game.GetSceneNodeByInstanceID<Officer>(
                    ReferenceOfficerInstanceID
                );
                if (referenceOfficer == null)
                    throw new InvalidOperationException(
                        $"AdjustOfficerRating could not resolve reference officer '{ReferenceOfficerInstanceID}'."
                    );
                if (PercentOfPositiveRatingGap.Value < 0 || MinimumAmount < 0)
                    throw new InvalidOperationException(
                        "Rating-gap adjustments require non-negative percentage and minimum values."
                    );
            }

            int baseRating = officer.GetBaseRating(Rating);
            int adjustment =
                Amount
                ?? (
                    PercentOfBaseRating.HasValue
                        ? checked(baseRating * PercentOfBaseRating.Value / 100)
                    : PercentOfCurrentRating.HasValue
                        ? checked(
                            officer.GetEffectiveRating(Rating) * PercentOfCurrentRating.Value / 100
                        )
                    : PercentOfCurrentRank.HasValue
                        ? checked(officer.ForceRank * PercentOfCurrentRank.Value / 100)
                    : Math.Max(
                        MinimumAmount,
                        checked(
                            Math.Max(
                                0,
                                referenceOfficer.GetEffectiveRating(Rating)
                                    - officer.GetEffectiveRating(Rating)
                            )
                            * PercentOfPositiveRatingGap.Value
                            / 100
                        )
                    )
                );
            officer.SetBaseRating(Rating, checked(baseRating + adjustment));
            if (Rating != OfficerRating.Force)
                return new List<GameResult>();
            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = adjustment,
                    PreviousForceRank = baseRating + officer.ForceTrainingAdjustment,
                    CurrentForceRank = officer.ForceRank,
                    SuppressRankChangeMessage = true,
                    Tick = game.CurrentTick,
                },
            };
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

            if (!string.IsNullOrWhiteSpace(DisplayImagePath))
                officer.DisplayImagePath = DisplayImagePath;
            if (!string.IsNullOrWhiteSpace(SmallDisplayImagePath))
                officer.SmallDisplayImagePath = SmallDisplayImagePath;
            if (!string.IsNullOrWhiteSpace(MessageImagePath))
                officer.MessageImagePath = MessageImagePath;
            if (!string.IsNullOrWhiteSpace(EncyclopediaImagePath))
                officer.EncyclopediaImagePath = EncyclopediaImagePath;
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

        public OfficerVoiceSet VoiceSet { get; set; } = new OfficerVoiceSet();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameActionContext context)
        {
            GameRoot game = context.Game;
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerVoiceSet could not resolve officer '{OfficerInstanceID}'."
                );

            officer.VoiceSet.MergeFrom(VoiceSet);
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
