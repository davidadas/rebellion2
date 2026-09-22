using System;
using Rebellion.Game;

namespace Rebellion.AI.Scorers
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
        /// Evaluates a normalized input around a neutral midpoint.
        /// </summary>
        /// <param name="input">The normalized consideration input.</param>
        /// <param name="consideration">The response curve and full contribution range.</param>
        /// <returns>A utility contribution from negative half-weight through positive half-weight.</returns>
        public static double EvaluateCentered(
            double input,
            GameConfig.AIConsiderationConfig consideration
        )
        {
            if (consideration == null || consideration.Weight == 0)
                return 0;

            return (EvaluateCurve(input, consideration.Curve) - 0.5) * consideration.Weight;
        }

        /// <summary>
        /// Converts a normalized utility contribution to percentage-point demand pressure.
        /// </summary>
        /// <param name="input">The normalized consideration input.</param>
        /// <param name="consideration">The response curve and relative pressure weight.</param>
        /// <returns>A demand-pressure contribution from zero through 100.</returns>
        public static double EvaluatePressure(
            double input,
            GameConfig.AIConsiderationConfig consideration
        ) => Evaluate(input, consideration) * 100;

        /// <summary>
        /// Converts centered utility to signed percentage-point demand pressure.
        /// </summary>
        /// <param name="input">The normalized consideration input.</param>
        /// <param name="consideration">The response curve and relative pressure range.</param>
        /// <returns>Demand pressure from negative 50 through positive 50.</returns>
        public static double EvaluateCenteredPressure(
            double input,
            GameConfig.AIConsiderationConfig consideration
        ) => EvaluateCentered(input, consideration) * 100;

        /// <summary>
        /// Converts utility to a whole percentage-point demand-pressure contribution.
        /// </summary>
        /// <param name="input">The normalized consideration input.</param>
        /// <param name="consideration">The response curve and relative pressure weight.</param>
        /// <returns>A whole demand-pressure contribution from zero through 100.</returns>
        public static double EvaluateDiscretePressure(
            double input,
            GameConfig.AIConsiderationConfig consideration
        ) => Math.Truncate(EvaluatePressure(input, consideration));

        /// <summary>
        /// Evaluates a normalized input through a response curve.
        /// </summary>
        /// <param name="input">The normalized curve input.</param>
        /// <param name="curve">The response-curve configuration.</param>
        /// <returns>A normalized utility value.</returns>
        public static double EvaluateCurve(double input, GameConfig.AIResponseCurveConfig curve)
        {
            ValidateNormalizedInput(input);
            if (curve == null)
                return input;

            return curve.Shape switch
            {
                GameConfig.AIResponseCurveShape.Power => EvaluatePower(input, curve.Exponent),
                GameConfig.AIResponseCurveShape.SmoothStep => input * input * (3 - 2 * input),
                GameConfig.AIResponseCurveShape.Logistic => EvaluateLogistic(
                    input,
                    curve.Midpoint,
                    curve.Steepness
                ),
                _ => input,
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
        /// Normalizes a raw consideration value against its configured saturation value.
        /// </summary>
        /// <param name="value">The raw consideration value.</param>
        /// <param name="consideration">The consideration that owns the saturation value.</param>
        /// <returns>A normalized fulfillment value.</returns>
        public static double Fulfillment(
            double value,
            GameConfig.AIConsiderationConfig consideration
        )
        {
            return Fulfillment(value, consideration?.SaturationValue ?? 1);
        }

        /// <summary>
        /// Constrains a value to the normalized utility interval.
        /// </summary>
        /// <param name="value">The value to constrain.</param>
        /// <returns>A value from zero through one.</returns>
        private static double Clamp(double value)
        {
            if (double.IsNaN(value) || value <= 0)
                return 0;

            return value >= 1 ? 1 : value;
        }

        /// <summary>
        /// Rejects values that violate the normalized utility-input contract.
        /// </summary>
        /// <param name="input">The value supplied to a response curve.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="input"/> is not between zero and one.
        /// </exception>
        private static void ValidateNormalizedInput(double input)
        {
            if (double.IsNaN(input) || input < 0 || input > 1)
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    input,
                    "AI utility curve inputs must be normalized from zero through one."
                );
        }

        /// <summary>
        /// Evaluates a normalized input through a power response curve.
        /// </summary>
        /// <param name="input">The normalized input.</param>
        /// <param name="exponent">The configured curve exponent.</param>
        /// <returns>The normalized curve output.</returns>
        private static double EvaluatePower(double input, double exponent)
        {
            double safeExponent =
                exponent > 0 && !double.IsNaN(exponent) && !double.IsInfinity(exponent)
                    ? exponent
                    : 1;
            return Math.Pow(input, safeExponent);
        }

        /// <summary>
        /// Evaluates a normalized input through an endpoint-normalized logistic response curve.
        /// </summary>
        /// <param name="input">The normalized input.</param>
        /// <param name="midpoint">The configured midpoint.</param>
        /// <param name="steepness">The configured steepness.</param>
        /// <returns>The normalized curve output.</returns>
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

        /// <summary>
        /// Evaluates the logistic function for the supplied parameters.
        /// </summary>
        /// <param name="input">The input value.</param>
        /// <param name="midpoint">The curve midpoint.</param>
        /// <param name="steepness">The curve steepness.</param>
        /// <returns>The logistic function value.</returns>
        private static double Logistic(double input, double midpoint, double steepness)
        {
            return 1 / (1 + Math.Exp(-steepness * (input - midpoint)));
        }
    }
}
