using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
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
        public List<UnitSelector> Selectors { get; set; } = new List<UnitSelector>();

        public override List<GameResult> Execute(GameRoot game) => Execute(game, game.Random, null);

        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            IEnumerable<Officer> officers = Selectors
                .SelectMany(selector => selector.Select(game, provider, context))
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
    /// Adjusts one stored officer rating by a fixed amount or a percentage of its current base value.
    /// </summary>
    [PersistableObject(Name = "AdjustOfficerRating")]
    public sealed class AdjustOfficerRatingAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public OfficerRating Rating { get; set; }

        public int? Amount { get; set; }
        public int? Percent { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"AdjustOfficerRating could not resolve officer '{OfficerInstanceID}'."
                );
            if (Rating == OfficerRating.None)
                throw new InvalidOperationException(
                    "AdjustOfficerRating requires a concrete officer rating."
                );
            if (Amount.HasValue == Percent.HasValue)
                throw new InvalidOperationException(
                    "AdjustOfficerRating requires exactly one of Amount or Percent."
                );

            int baseRating = officer.GetBaseRating(Rating);
            int adjustment = Amount ?? checked(baseRating * Percent.Value / 100);
            officer.SetBaseRating(Rating, checked(baseRating + adjustment));
            return new List<GameResult>();
        }
    }

    /// <summary>
    /// Adjusts an officer's Force value by a signed amount or percentage of current rank.
    /// </summary>
    [PersistableObject(Name = "AdjustOfficerForce")]
    public sealed class AdjustOfficerForceAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
        public int? Amount { get; set; }
        public int? Percent { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"AdjustOfficerForce could not resolve officer '{OfficerInstanceID}'."
                );
            if (Amount.HasValue == Percent.HasValue)
                throw new InvalidOperationException(
                    "AdjustOfficerForce requires exactly one of Amount or Percent."
                );

            int previousRank = officer.ForceRank;
            int adjustment = Amount ?? checked(previousRank * Percent.Value / 100);
            officer.ForceValue = Math.Max(0, checked(officer.ForceValue + adjustment));

            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = adjustment,
                    PreviousForceRank = previousRank,
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
    [PersistableObject(Name = "SetOfficerForceState")]
    public sealed class SetOfficerForceStateAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public bool? IsJedi { get; set; }

        [PersistableAttribute]
        public bool? IsEligible { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            return Execute(game, provider);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"SetOfficerForceState could not resolve {nameof(OfficerInstanceID)} '{OfficerInstanceID}'."
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
                    officer.JediLevel + provider.NextInt(0, officer.JediLevelVariance + 1);
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
    public class ApplyOfficerInjuryAction : GameAction
    {
        [PersistableAttribute]
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
