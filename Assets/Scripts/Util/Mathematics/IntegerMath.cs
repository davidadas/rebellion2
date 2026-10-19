namespace Rebellion.Util.Mathematics
{
    /// <summary>
    /// Provides overflow-safe integer scaling and division helpers.
    /// </summary>
    public static class IntegerMath
    {
        private const int _percentageScale = 100;

        /// <summary>
        /// Scales an integer by a percent value, truncating the result.
        /// </summary>
        /// <param name="value">The value to scale.</param>
        /// <param name="percent">The percent to apply.</param>
        /// <returns>The scaled value.</returns>
        public static int ScaleByPercent(int value, int percent)
        {
            return SaturateToInt((long)value * percent / _percentageScale);
        }

        /// <summary>
        /// Scales an integer by a percent value, rounding the result up.
        /// </summary>
        /// <param name="value">The value to scale.</param>
        /// <param name="percent">The percent to apply.</param>
        /// <returns>The scaled value.</returns>
        public static int ScaleByPercentRoundedUp(int value, int percent)
        {
            return SaturateToInt(((long)value * percent + _percentageScale - 1) / _percentageScale);
        }

        /// <summary>
        /// Divides two integers, rounding the quotient up.
        /// </summary>
        /// <param name="value">The value to divide.</param>
        /// <param name="divisor">The divisor to use.</param>
        /// <returns>The rounded-up quotient, or zero for a non-positive input.</returns>
        public static int DivideRoundedUp(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            return (int)(((long)value + divisor - 1) / divisor);
        }

        /// <summary>
        /// Divides two long integers, rounding the quotient up.
        /// </summary>
        /// <param name="value">The value to divide.</param>
        /// <param name="divisor">The divisor to use.</param>
        /// <returns>The rounded-up quotient, or zero for a non-positive input.</returns>
        public static long DivideRoundedUp(long value, long divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            return (value + divisor - 1) / divisor;
        }

        /// <summary>
        /// Adds two integers and saturates the result to the representable integer range.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>The sum, saturated to an integer.</returns>
        public static int SaturatingAdd(int left, int right)
        {
            return SaturateToInt((long)left + right);
        }

        /// <summary>
        /// Multiplies two integers and saturates the result to the representable integer range.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>The product, saturated to an integer.</returns>
        public static int SaturatingMultiply(int left, int right)
        {
            return SaturateToInt((long)left * right);
        }

        /// <summary>
        /// Narrows a long value while saturating at the representable integer bounds.
        /// </summary>
        /// <param name="value">The value to narrow.</param>
        /// <returns>The value as an integer, or the nearest integer bound.</returns>
        public static int SaturateToInt(long value)
        {
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }
    }
}
