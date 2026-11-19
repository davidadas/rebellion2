using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Calculates battle support shifts from configured side values and loss weights.
    /// </summary>
    internal static class SpaceCombatSupportCalculator
    {
        private const int _percentDivisor = 100;

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
            SpaceCombatSupportWeights firstSideWeights,
            SpaceCombatSupportWeights secondSideWeights,
            int divisor,
            out SpaceCombatSupportAdjustment adjustment
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
            SpaceCombatSupportWeights leadingWeights = firstSideLeads
                ? firstSideWeights
                : secondSideWeights;
            SpaceCombatSupportWeights trailingWeights = firstSideLeads
                ? secondSideWeights
                : firstSideWeights;

            int leadingTerm = 0;
            int trailingTerm = 0;
            if (leadingTotal != 0)
            {
                int gap = unchecked(leadingTotal - trailingTotal);
                leadingTerm = unchecked(
                    leadingWeights.LeadingFlat
                    + MultiplyDivide(leadingWeights.LeadingGapScale, gap, leadingTotal)
                );
                trailingTerm = unchecked(
                    trailingWeights.TrailingFlat
                    + trailingWeights.TrailingGapScale
                    - MultiplyDivide(trailingWeights.TrailingGapScale, gap, leadingTotal)
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

            SpaceCombatSupportWeights beneficiaryWeights =
                beneficiarySide == 1 ? firstSideWeights : secondSideWeights;
            int comparisonSide = 3 - beneficiarySide;
            SpaceCombatSupportWeights comparisonWeights =
                comparisonSide == 1 ? firstSideWeights : secondSideWeights;
            adjustment = new SpaceCombatSupportAdjustment(
                beneficiarySide,
                unchecked((uint)Math.Min(beneficiaryWeights.MagnitudeCap, Math.Max(0, magnitude))),
                comparisonSide,
                comparisonWeights.SupportThreshold
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
    }
}
