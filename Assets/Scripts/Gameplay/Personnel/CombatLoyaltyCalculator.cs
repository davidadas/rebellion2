using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Calculates battle-loss loyalty changes using signed 32-bit values.
    /// </summary>
    internal static class CombatLoyaltyCalculator
    {
        /// <summary>
        /// Weighs the opposing losses twice and the favored losses once, retaining the
        /// separate truncated divisions and wrapping the difference and final sum.
        /// </summary>
        /// <param name="favoredLoss">The favored faction's accepted lost unit value.</param>
        /// <param name="opposingLoss">The opposing faction's accepted lost unit value.</param>
        /// <param name="divisor">The favored faction's authored loyalty divisor.</param>
        /// <returns>The signed loyalty change relative to the favored faction.</returns>
        /// <exception cref="OverflowException">A quotient cannot be represented as an integer.</exception>
        public static int Calculate(int favoredLoss, int opposingLoss, int divisor)
        {
            int difference = unchecked(opposingLoss - favoredLoss);
            if (divisor == -1 && (difference == int.MinValue || opposingLoss == int.MinValue))
                throw new OverflowException(
                    "The loyalty quotient exceeds the signed integer range."
                );

            return unchecked(difference / divisor + opposingLoss / divisor);
        }
    }
}
