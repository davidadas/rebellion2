using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Adds Force experience calculated as a percentage of the officer's current rank.
    /// </summary>
    [PersistableObject(Name = "AddForceExperience")]
    public sealed class AddForceExperienceAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
        public int PercentOfCurrentRank { get; set; }

        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"AddForceExperience could not resolve officer '{OfficerInstanceID}'."
                );
            if (PercentOfCurrentRank < 0)
                throw new InvalidOperationException(
                    "AddForceExperience percentage cannot be negative."
                );

            int previousRank = officer.ForceRank;
            int gained = previousRank * PercentOfCurrentRank / 100;
            officer.ForceValue += gained;

            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = gained,
                    PreviousForceRank = previousRank,
                    CurrentForceRank = officer.ForceRank,
                    SuppressRankChangeMessage = true,
                    Tick = game.CurrentTick,
                },
            };
        }
    }

    /// <summary>
    /// Reveals an officer's authored Force potential and initializes its starting value.
    /// </summary>
    [PersistableObject(Name = "RevealOfficerForcePotential")]
    public sealed class RevealOfficerForcePotentialAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }
        public bool SuppressRankChangeMessage { get; set; } = true;

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
                    $"RevealOfficerForcePotential could not resolve {nameof(OfficerInstanceID)} '{OfficerInstanceID}'."
                );
            if (officer.IsForceEligible)
                return new List<GameResult>();

            int previousRank = officer.ForceRank;
            officer.IsJedi = true;
            officer.IsForceEligible = true;
            int startingValue =
                officer.JediLevel + provider.NextInt(0, officer.JediLevelVariance + 1);
            officer.ForceValue = Math.Max(officer.ForceValue, startingValue);
            return new List<GameResult>
            {
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = Math.Max(0, officer.ForceRank - previousRank),
                    PreviousForceRank = previousRank,
                    CurrentForceRank = officer.ForceRank,
                    SuppressRankChangeMessage = SuppressRankChangeMessage,
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
        [PersistableAttribute]
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

        /// <summary>
        /// Resolves a required officer reference for a story action.
        /// </summary>
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
