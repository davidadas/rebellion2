using System;
using System.Runtime.CompilerServices;

namespace Rebellion.Util.Mathematics
{
    /// <summary>
    /// Measures plain vector lengths using the target's native arithmetic at ambiguous float boundaries.
    /// </summary>
    internal static class ExtendedVectorLength
    {
        private static readonly bool _hasBoundedNativePrecision =
            NativeVectorMath.SignificandBits == 53 || NativeVectorMath.SignificandBits == 64;

        /// <summary>
        /// Measures a vector with native retained sums and one final float store.
        /// </summary>
        /// <remarks>
        /// Finite float products are exact in double precision. Two positive additions followed by
        /// a square root at 53 or 64 significant bits differ by less than three times 2^-53 relative
        /// to the double result. Four adjacent double values enclose that bound across a binade.
        /// Matching float-rounded endpoints establish the result for either precision. Ambiguous,
        /// nonfinite, and subnormal inputs execute the complete native expression instead.
        /// </remarks>
        /// <param name="x">The first coordinate.</param>
        /// <param name="y">The second coordinate.</param>
        /// <param name="z">The third coordinate.</param>
        /// <returns>The magnitude rounded to a float.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Measure(float x, float y, float z)
        {
            if (
                !_hasBoundedNativePrecision
                || RequiresNativeInput(x)
                || RequiresNativeInput(y)
                || RequiresNativeInput(z)
            )
                return NativeVectorMath.Measure(x, y, z);
            double length = Math.Sqrt((double)x * x + ((double)y * y + (double)z * z));
            if (!(length > 0))
                return NativeVectorMath.Measure(x, y, z);
            long bits = BitConverter.DoubleToInt64Bits(length);
            float lower = (float)BitConverter.Int64BitsToDouble(bits - 4);
            float upper = (float)BitConverter.Int64BitsToDouble(bits + 4);
            return lower == upper ? lower : NativeVectorMath.Measure(x, y, z);
        }

        /// <summary>
        /// Identifies inputs requiring native nonfinite handling or gradual underflow.
        /// </summary>
        /// <param name="value">The input coordinate.</param>
        /// <returns>Whether its exponent encodes a nonfinite value or a nonzero subnormal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RequiresNativeInput(float value)
        {
            uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            uint exponent = bits & 0x7f800000;
            return exponent == 0x7f800000 || exponent == 0 && (bits & 0x7fffff) != 0;
        }
    }
}
