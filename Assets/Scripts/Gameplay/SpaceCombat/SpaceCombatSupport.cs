using System;
using Rebellion.Content;
using Rebellion.Game;
using Rebellion.Game.Factions;

namespace Rebellion.Gameplay
{
    internal static class SpaceCombatSupport
    {
        private const int _percentDivisor = 100;

        /// <summary>
        /// Resolves a complete authored profile from saved launch settings or multiplayer identity.
        /// Missing or ambiguous faction mappings, campaign factions, contexts, or values fail without
        /// selecting another profile.
        /// </summary>
        /// <param name="config">The authored battle support configuration.</param>
        /// <param name="game">The campaign supplying persisted setup and active faction identity.</param>
        /// <param name="tacticalCombat">The shared authored faction-index map.</param>
        /// <param name="profile">The selected complete profile, or null when resolution fails.</param>
        /// <param name="factions">The campaign factions in authored tactical order.</param>
        /// <returns>True when the exact saved context has a complete profile.</returns>
        internal static bool TryResolveProfile(
            this SpaceCombatSupportConfig config,
            GameRoot game,
            TacticalCombatDefinition tacticalCombat,
            out SpaceCombatSupportProfile profile,
            out (Faction First, Faction Second) factions
        )
        {
            profile = null;
            factions = default;
            if (
                game?.Summary == null
                || game.Metadata == null
                || !TryResolveFactionIDs(
                    tacticalCombat,
                    out string firstFactionID,
                    out string secondFactionID
                )
            )
                return false;

            Faction first = game.GetFactions()
                .Find(faction => faction.InstanceID == firstFactionID);
            Faction second = game.GetFactions()
                .Find(faction => faction.InstanceID == secondFactionID);
            if (first == null || second == null)
                return false;

            SpaceCombatSupportContext? context = game.Metadata.IsMultiplayer
                ? SpaceCombatSupportContext.Multiplayer
                : ResolveSinglePlayerContext(game.Summary, firstFactionID, secondFactionID);
            if (
                !context.HasValue
                || config?.Profiles == null
                || !config.Profiles.TryGetValue(
                    context.Value,
                    out SpaceCombatSupportProfile selected
                )
                || selected?.HasAllValues() != true
            )
                return false;

            profile = selected;
            factions = (first, second);
            return true;
        }

        /// <summary>
        /// Resolves each configured strategic side from its unique authored tactical index.
        /// </summary>
        /// <param name="definition">The shared faction-index definition.</param>
        /// <param name="firstFactionID">The faction bound to index zero.</param>
        /// <param name="secondFactionID">The faction bound to index one.</param>
        /// <returns>True when both indices have distinct, unambiguous faction identities.</returns>
        private static bool TryResolveFactionIDs(
            TacticalCombatDefinition definition,
            out string firstFactionID,
            out string secondFactionID
        )
        {
            firstFactionID = null;
            secondFactionID = null;
            if (definition?.Factions == null)
                return false;

            foreach (TacticalCombatFactionDefinition faction in definition.Factions)
            {
                if (faction == null || string.IsNullOrEmpty(faction.FactionInstanceID))
                    return false;
                if (faction.FactionIndex == 0 && firstFactionID == null)
                    firstFactionID = faction.FactionInstanceID;
                else if (faction.FactionIndex == 1 && secondFactionID == null)
                    secondFactionID = faction.FactionInstanceID;
                else
                    return false;
            }

            return firstFactionID != null
                && secondFactionID != null
                && firstFactionID != secondFactionID;
        }

        /// <summary>
        /// Selects one of the six single-player contexts using the campaign's saved launch faction.
        /// </summary>
        /// <param name="summary">The saved campaign setup.</param>
        /// <param name="firstFactionID">The faction bound to tactical index zero.</param>
        /// <param name="secondFactionID">The faction bound to tactical index one.</param>
        /// <returns>The exact setup context, or null for an unsupported faction or difficulty.</returns>
        private static SpaceCombatSupportContext? ResolveSinglePlayerContext(
            GameSummary summary,
            string firstFactionID,
            string secondFactionID
        )
        {
            if (summary.PlayerFactionID == firstFactionID)
                return summary.Difficulty switch
                {
                    GameDifficulty.Easy => SpaceCombatSupportContext.FirstFactionEasy,
                    GameDifficulty.Medium => SpaceCombatSupportContext.FirstFactionMedium,
                    GameDifficulty.Hard => SpaceCombatSupportContext.FirstFactionHard,
                    _ => null,
                };
            if (summary.PlayerFactionID == secondFactionID)
                return summary.Difficulty switch
                {
                    GameDifficulty.Easy => SpaceCombatSupportContext.SecondFactionEasy,
                    GameDifficulty.Medium => SpaceCombatSupportContext.SecondFactionMedium,
                    GameDifficulty.Hard => SpaceCombatSupportContext.SecondFactionHard,
                    _ => null,
                };
            return null;
        }

        /// <summary>
        /// Calculates a support adjustment for a surviving configured side, selecting side two on ties.
        /// </summary>
        /// <param name="firstSideTotal">The current total unit value of configured side one.</param>
        /// <param name="secondSideTotal">The current total unit value of configured side two.</param>
        /// <param name="firstSideLoss">The accumulated lost unit value of configured side one.</param>
        /// <param name="secondSideLoss">The accumulated lost unit value of configured side two.</param>
        /// <param name="survivingSide">The surviving configured side, numbered one or two.</param>
        /// <param name="firstSideWeights">The first side's calculation inputs.</param>
        /// <param name="secondSideWeights">The second side's calculation inputs.</param>
        /// <param name="divisor">The signed divisor applied to the weighted loss difference.</param>
        /// <param name="adjustment">The resulting support rule, or its default value for an invalid side.</param>
        /// <returns>True when the survivor identifies one of the two configured sides.</returns>
        public static bool TryCalculate(
            int firstSideTotal,
            int secondSideTotal,
            int firstSideLoss,
            int secondSideLoss,
            int survivingSide,
            SpaceCombatSupportSideConfig firstSideWeights,
            SpaceCombatSupportSideConfig secondSideWeights,
            int divisor,
            out Adjustment adjustment
        )
        {
            adjustment = default;
            if (survivingSide < 1 || survivingSide > 2)
                return false;

            bool firstSideLeads = firstSideTotal > secondSideTotal;
            int leadingSide = firstSideLeads ? 1 : 2;
            int leadingTotal = firstSideLeads ? firstSideTotal : secondSideTotal;
            int trailingTotal = firstSideLeads ? secondSideTotal : firstSideTotal;
            int leadingLoss = firstSideLeads ? firstSideLoss : secondSideLoss;
            int trailingLoss = firstSideLeads ? secondSideLoss : firstSideLoss;
            SpaceCombatSupportSideConfig leadingWeights = firstSideLeads
                ? firstSideWeights
                : secondSideWeights;
            SpaceCombatSupportSideConfig trailingWeights = firstSideLeads
                ? secondSideWeights
                : firstSideWeights;

            int leadingTerm = 0;
            int trailingTerm = 0;
            if (leadingTotal != 0)
            {
                int gap = unchecked(leadingTotal - trailingTotal);
                leadingTerm = unchecked(
                    leadingWeights.LeadingFlat.Value
                    + MultiplyDivide(leadingWeights.LeadingGapScale.Value, gap, leadingTotal)
                );
                trailingTerm = unchecked(
                    trailingWeights.TrailingFlat.Value
                    + trailingWeights.TrailingGapScale.Value
                    - MultiplyDivide(trailingWeights.TrailingGapScale.Value, gap, leadingTotal)
                );
            }

            int leadingWeighted = MultiplyDivide(leadingLoss, leadingTerm, _percentDivisor);
            int trailingWeighted = MultiplyDivide(trailingLoss, trailingTerm, _percentDivisor);
            int numerator =
                leadingSide == survivingSide
                    ? unchecked(trailingWeighted * 2 - leadingWeighted)
                    : unchecked(leadingWeighted * 2 - trailingWeighted);
            int magnitude = numerator / divisor;
            int beneficiarySide = survivingSide;
            if (magnitude < 0)
            {
                magnitude = unchecked(-magnitude);
                beneficiarySide = 3 - beneficiarySide;
            }

            SpaceCombatSupportSideConfig beneficiaryWeights =
                beneficiarySide == 1 ? firstSideWeights : secondSideWeights;
            int comparisonSide = 3 - beneficiarySide;
            SpaceCombatSupportSideConfig comparisonWeights =
                comparisonSide == 1 ? firstSideWeights : secondSideWeights;
            adjustment = new Adjustment(
                beneficiarySide,
                unchecked(
                    (uint)Math.Min(beneficiaryWeights.MagnitudeCap.Value, Math.Max(0, magnitude))
                ),
                comparisonSide,
                comparisonWeights.SupportThreshold.Value
            );
            return true;
        }

        /// <summary>
        /// Divides the signed low 32 bits of an integer product with truncation toward zero.
        /// </summary>
        /// <param name="value">The first product operand.</param>
        /// <param name="multiplier">The second product operand.</param>
        /// <param name="divisor">The signed divisor.</param>
        /// <returns>The signed integer quotient.</returns>
        private static int MultiplyDivide(int value, int multiplier, int divisor) =>
            unchecked(value * multiplier) / divisor;

        /// <summary>
        /// Captures a calculated support shift using selectors for the configured first and second sides.
        /// </summary>
        internal readonly struct Adjustment
        {
            public int BeneficiarySide { get; }
            public uint Magnitude { get; }
            public int ComparisonSide { get; }
            public uint SupportThreshold { get; }

            /// <summary>
            /// Captures the calculated support rule without selecting destination planets.
            /// </summary>
            /// <param name="beneficiarySide">The benefiting configured side, numbered one or two.</param>
            /// <param name="magnitude">The raw magnitude bits after signed lower and upper bounds are applied.</param>
            /// <param name="comparisonSide">The opposing configured side used for support comparison.</param>
            /// <param name="supportThreshold">The comparison side's configured support threshold.</param>
            public Adjustment(
                int beneficiarySide,
                uint magnitude,
                int comparisonSide,
                uint supportThreshold
            )
            {
                BeneficiarySide = beneficiarySide;
                Magnitude = magnitude;
                ComparisonSide = comparisonSide;
                SupportThreshold = supportThreshold;
            }
        }
    }
}
