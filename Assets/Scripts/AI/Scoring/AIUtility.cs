using System;
using Rebellion.Game;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Evaluates normalized AI considerations on a common utility scale.
    /// </summary>
    public static class AIUtility
    {
        /// <summary>
        /// Evaluates a normalized input through a consideration's response curve and weight.
        /// </summary>
        /// <param name="input">The normalized consideration input.</param>
        /// <param name="consideration">The response curve and contribution weight.</param>
        /// <returns>The weighted utility contribution.</returns>
        public static double Evaluate(double input, GameConfig.AIConsiderationConfig consideration)
        {
            if (consideration == null || consideration.Weight == 0)
                return 0;

            return EvaluateCurve(input, consideration.Curve) * consideration.Weight;
        }

        /// <summary>
        /// Evaluates a consideration and quantizes its contribution for a discrete scoring domain.
        /// </summary>
        /// <param name="input">The normalized consideration input.</param>
        /// <param name="consideration">The response curve and contribution weight.</param>
        /// <returns>The weighted utility contribution truncated to a whole score point.</returns>
        public static double EvaluateDiscrete(
            double input,
            GameConfig.AIConsiderationConfig consideration
        )
        {
            return Math.Truncate(Evaluate(input, consideration));
        }

        /// <summary>
        /// Normalizes a raw consideration value against its configured maximum before evaluation.
        /// </summary>
        /// <param name="value">The raw consideration value.</param>
        /// <param name="consideration">The input range, response curve, and contribution weight.</param>
        /// <returns>The weighted utility contribution.</returns>
        public static double EvaluateRaw(
            double value,
            GameConfig.AIConsiderationConfig consideration
        )
        {
            if (consideration == null)
                return 0;

            double maximum = consideration.InputMaximum;
            if (
                maximum > 0
                && (
                    consideration.Curve == null
                    || consideration.Curve.Shape == GameConfig.AIResponseCurveShape.Linear
                )
            )
            {
                if (double.IsNaN(value) || value <= 0)
                    return 0;

                return value >= maximum
                    ? consideration.Weight
                    : value * (consideration.Weight / maximum);
            }

            return Evaluate(Fulfillment(value, maximum), consideration);
        }

        /// <summary>
        /// Evaluates a normalized input through a response curve.
        /// </summary>
        /// <param name="input">The normalized curve input.</param>
        /// <param name="curve">The response-curve configuration.</param>
        /// <returns>A normalized utility value.</returns>
        public static double EvaluateCurve(double input, GameConfig.AIResponseCurveConfig curve)
        {
            double boundedInput = Clamp(input);
            if (curve == null)
                return boundedInput;

            return curve.Shape switch
            {
                GameConfig.AIResponseCurveShape.Power => EvaluatePower(
                    boundedInput,
                    curve.Exponent
                ),
                GameConfig.AIResponseCurveShape.SmoothStep => boundedInput
                    * boundedInput
                    * (3 - 2 * boundedInput),
                GameConfig.AIResponseCurveShape.Logistic => EvaluateLogistic(
                    boundedInput,
                    curve.Midpoint,
                    curve.Steepness
                ),
                _ => boundedInput,
            };
        }

        /// <summary>
        /// Converts a value and target into a normalized fulfillment input.
        /// </summary>
        /// <param name="value">The measured value.</param>
        /// <param name="target">The value representing full fulfillment.</param>
        /// <returns>A normalized fulfillment value.</returns>
        public static double Fulfillment(double value, double target)
        {
            if (target <= 0)
                return 1;

            return Clamp(value / target);
        }

        /// <summary>
        /// Constrains a value to the normalized utility interval.
        /// </summary>
        /// <param name="value">The value to constrain.</param>
        /// <returns>A value from zero through one.</returns>
        public static double Clamp(double value)
        {
            if (double.IsNaN(value) || value <= 0)
                return 0;

            return value >= 1 ? 1 : value;
        }

        private static double EvaluatePower(double input, double exponent)
        {
            double safeExponent =
                exponent > 0 && !double.IsNaN(exponent) && !double.IsInfinity(exponent)
                    ? exponent
                    : 1;
            return Math.Pow(input, safeExponent);
        }

        private static double EvaluateLogistic(double input, double midpoint, double steepness)
        {
            double safeMidpoint = Clamp(midpoint);
            double safeSteepness =
                steepness > 0 && !double.IsNaN(steepness) && !double.IsInfinity(steepness)
                    ? steepness
                    : 10;
            double minimum = Logistic(0, safeMidpoint, safeSteepness);
            double maximum = Logistic(1, safeMidpoint, safeSteepness);
            double range = maximum - minimum;
            if (range <= double.Epsilon)
                return input;

            return Clamp((Logistic(input, safeMidpoint, safeSteepness) - minimum) / range);
        }

        private static double Logistic(double input, double midpoint, double steepness)
        {
            return 1 / (1 + Math.Exp(-steepness * (input - midpoint)));
        }
    }
}
