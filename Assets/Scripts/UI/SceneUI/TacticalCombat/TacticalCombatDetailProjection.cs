using System;
using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>
/// Retains the angular detail scale shared by every sprite under one camera field.
/// </summary>
public sealed class TacticalCombatDetailProjection
{
    private readonly Binary _tangent;
    private readonly double? _exceptionalTangent;
    public float Field { get; }

    private static readonly (ulong High, ulong Low)[] _angles =
    {
        (0x3243f6a8885a308d, 0x313198a2e0370734),
        (0x1dac670561bb4f68, 0xadfc88bd978751a0),
        (0x0fadbafc96406eb1, 0x56dc79ef5f7a217e),
        (0x07f56ea6ab0bdb71, 0x9644bcc4f9f44478),
        (0x03feab76e59fbd38, 0xdb2c9e4b7038b835),
        (0x01ffd55bba97624a, 0x84ef3aeedbb518c4),
        (0x00fffaaadddb94d5, 0xbbe78c564015f760),
        (0x007fff5556eeea5c, 0xb40311a8fddf3058),
        (0x003fffeaaab7776e, 0x52ec4abedadb53e0),
        (0x001ffffd5555bbbb, 0xa9729ab7aac08947),
        (0x000fffffaaaaaddd, 0xddb94b968067ef3b),
        (0x0007fffff555556e, 0xeeeea5ca5d895893),
        (0x0003fffffeaaaaab, 0x777776e52e5356f5),
        (0x0001ffffffd55555, 0x5bbbbbba972972d0),
        (0x0000fffffffaaaaa, 0xaadddddddb94b94c),
        (0x00007fffffff5555, 0x5556eeeeeeea5ca6),
        (0x00003fffffffeaaa, 0xaaaab77777776e53),
        (0x00001ffffffffd55, 0x555555bbbbbbbba9),
        (0x00000fffffffffaa, 0xaaaaaaaddddddddd),
        (0x000007fffffffff5, 0x555555556eeeeeee),
        (0x000003fffffffffe, 0xaaaaaaaaab777777),
        (0x000001ffffffffff, 0xd5555555555bbbbc),
        (0x000000ffffffffff, 0xfaaaaaaaaaaaddde),
        (0x0000007fffffffff, 0xff555555555556ef),
        (0x0000003fffffffff, 0xffeaaaaaaaaaaab7),
        (0x0000001fffffffff, 0xfffd555555555556),
        (0x0000000fffffffff, 0xffffaaaaaaaaaaab),
        (0x00000007ffffffff, 0xfffff55555555555),
        (0x00000003ffffffff, 0xfffffeaaaaaaaaab),
        (0x00000001ffffffff, 0xffffffd555555555),
        (0x00000000ffffffff, 0xfffffffaaaaaaaab),
        (0x000000007fffffff, 0xffffffff55555555),
        (0x000000003fffffff, 0xffffffffeaaaaaab),
        (0x000000001fffffff, 0xfffffffffd555555),
        (0x000000000fffffff, 0xffffffffffaaaaab),
        (0x0000000007ffffff, 0xfffffffffff55555),
        (0x0000000003ffffff, 0xfffffffffffeaaab),
        (0x0000000001ffffff, 0xffffffffffffd555),
        (0x0000000000ffffff, 0xffffffffffffffab),
        (0x00000000007fffff, 0xffffffffffffff55),
        (0x00000000003fffff, 0xffffffffffffffeb),
        (0x00000000001fffff, 0xfffffffffffffffd),
    };

    /// <summary>
    /// Caches the field-dependent tangent before any per-unit detail assessments.
    /// </summary>
    /// <param name="field">The camera's stored projection field.</param>
    public TacticalCombatDetailProjection(float field)
    {
        Field = field;
        if (float.IsNaN(field))
        {
            _exceptionalTangent = field;
            return;
        }
        if (float.IsInfinity(field))
        {
            _exceptionalTangent = double.NaN;
            return;
        }
        Binary angle = Binary.FromSingle(field);
        angle = new Binary(angle.Magnitude, angle.Scale - 1, angle.Negative);
        if (Math.Abs((double)field * 0.5) > 0.75)
        {
            double tangent = NativeTangent((double)field * 0.5);
            if (double.IsNaN(tangent) || double.IsInfinity(tangent))
                _exceptionalTangent = tangent;
            else
                _tangent = Binary.FromDouble(tangent);
            return;
        }
        if (angle.Magnitude.IsZero || GetBitLength(angle.Magnitude) - 1 + angle.Scale < -63)
        {
            _tangent = angle;
            return;
        }
        BigInteger input = angle.Magnitude << (angle.Scale + 126);
        BigInteger x = ((BigInteger)0x26dd3b6a10d79699UL << 64) | 0xfd7e424af5ff503aUL;
        BigInteger y = BigInteger.Zero;
        BigInteger remaining = input;
        for (int index = 0; index < 96; index++)
        {
            BigInteger shiftedX = x >> index;
            BigInteger shiftedY = y >> index;
            BigInteger step =
                index < _angles.Length
                    ? ((BigInteger)_angles[index].High << 64) | _angles[index].Low
                    : BigInteger.One << (126 - index);
            if (remaining.Sign >= 0)
            {
                x -= shiftedY;
                y += shiftedX;
                remaining -= step;
            }
            else
            {
                x += shiftedY;
                y -= shiftedX;
                remaining += step;
            }
        }
        BigInteger sine = BigInteger.Abs(y);
        BigInteger unit = BigInteger.One << 64;
        BigInteger sineOverAngle = sine >= input ? unit : DivideRounded(sine << 64, input);
        BigInteger cosine = RoundShift(x, 62);
        sineOverAngle = BigInteger.Min(sineOverAngle, unit);
        cosine = BigInteger.Min(cosine, unit);
        Binary ratio = DivideToPrecision(sineOverAngle, cosine, 64);
        _tangent = RoundToPrecision(
            angle.Magnitude * ratio.Magnitude,
            angle.Scale + ratio.Scale,
            64,
            angle.Negative
        );
    }

    /// <summary>
    /// Computes a retained angular metric with one final 53-significand-bit multiplication.
    /// </summary>
    /// <param name="distance">The rounded absolute camera depth.</param>
    /// <returns>The detail metric, including its unordered or infinite classification.</returns>
    public double GetFighterMetric(float distance)
    {
        if (_exceptionalTangent.HasValue)
        {
            if (double.IsNaN(_exceptionalTangent.Value))
                return SelectNaN(_exceptionalTangent.Value, distance);
            return float.IsNaN(distance) ? distance : _exceptionalTangent.Value * distance;
        }
        if (float.IsNaN(distance))
            return distance;
        if (float.IsInfinity(distance))
            return _tangent.Magnitude.IsZero ? double.NaN : _tangent.ToDouble() * distance;
        Binary depth = Binary.FromSingle(distance);
        return RoundToPrecision(
                _tangent.Magnitude * depth.Magnitude,
                _tangent.Scale + depth.Scale,
                53,
                _tangent.Negative != depth.Negative
            )
            .ToDouble();
    }

    /// <summary>
    /// Selects a sprite using this camera's cached angular metric.
    /// </summary>
    /// <param name="distance">The rounded absolute camera depth.</param>
    /// <param name="highDetail">Whether the nearest sprite resource is enabled.</param>
    /// <param name="current">The currently selected resource index.</param>
    /// <returns>The requested sprite resource index.</returns>
    public int SelectFighter(float distance, bool highDetail, int current) =>
        TacticalCombatDetailLevels.SelectFighter(GetFighterMetric(distance), highDetail, current);

    /// <summary>
    /// Retains quiet-operand priority, then payload magnitude and positive-sign precedence.
    /// </summary>
    /// <param name="left">The cached quiet NaN.</param>
    /// <param name="right">The live binary32 multiplier.</param>
    /// <returns>The selected quiet NaN.</returns>
    private static double SelectNaN(double left, float right)
    {
        uint rightBits = unchecked((uint)BitConverter.SingleToInt32Bits(right));
        if (!float.IsNaN(right) || (rightBits & 0x400000) == 0)
            return left;
        double converted = right;
        ulong leftBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(left));
        ulong convertedBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(converted));
        ulong leftPayload = leftBits & 0xfffffffffffff;
        ulong rightPayload = convertedBits & 0xfffffffffffff;
        return leftPayload < rightPayload || leftPayload == rightPayload && (leftBits >> 63) != 0
            ? converted
            : left;
    }

    /// <summary>
    /// Stores a signed integer significand and its exact binary scale.
    /// </summary>
    private readonly struct Binary
    {
        internal BigInteger Magnitude { get; }
        internal int Scale { get; }
        internal bool Negative { get; }

        /// <summary>
        /// Retains the exact finite magnitude and sign.
        /// </summary>
        /// <param name="magnitude">The nonnegative integer significand.</param>
        /// <param name="scale">The binary exponent applied to the significand.</param>
        /// <param name="negative">Whether the represented value is negative.</param>
        internal Binary(BigInteger magnitude, int scale, bool negative)
        {
            Magnitude = magnitude;
            Scale = scale;
            Negative = negative;
        }

        /// <summary>
        /// Decodes a finite binary32 value without a floating-point rounding step.
        /// </summary>
        /// <param name="value">The finite input value.</param>
        /// <returns>Its exact binary representation.</returns>
        internal static Binary FromSingle(float value)
        {
            uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            int exponent = (int)((bits >> 23) & 255);
            uint fraction = bits & 0x7fffff;
            return new Binary(
                exponent == 0 ? fraction : fraction | 0x800000,
                exponent == 0 ? -149 : exponent - 150,
                (bits & 0x80000000) != 0
            );
        }

        /// <summary>
        /// Decodes a finite binary64 value without a floating-point rounding step.
        /// </summary>
        /// <param name="value">The finite input value.</param>
        /// <returns>Its exact binary representation.</returns>
        internal static Binary FromDouble(double value)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            int exponent = (int)((bits >> 52) & 2047);
            ulong fraction = bits & 0xfffffffffffff;
            return new Binary(
                exponent == 0 ? fraction : fraction | 0x10000000000000,
                exponent == 0 ? -1074 : exponent - 1075,
                (bits & 0x8000000000000000) != 0
            );
        }

        /// <summary>
        /// Stores a finite value after its retained arithmetic has completed.
        /// </summary>
        /// <returns>The signed binary64 value.</returns>
        internal double ToDouble()
        {
            double value = (double)Magnitude * Math.Pow(2, Scale);
            return Negative ? -value : value;
        }
    }

    /// <summary>
    /// Rounds an exact positive ratio to the requested binary precision.
    /// </summary>
    /// <param name="numerator">The exact numerator.</param>
    /// <param name="denominator">The positive denominator.</param>
    /// <param name="precision">The number of retained significand bits.</param>
    /// <returns>The rounded finite ratio.</returns>
    private static Binary DivideToPrecision(
        BigInteger numerator,
        BigInteger denominator,
        int precision
    )
    {
        int exponent = GetBitLength(numerator) - GetBitLength(denominator);
        if (
            exponent >= 0
                ? numerator < denominator << exponent
                : numerator << -exponent < denominator
        )
            exponent--;
        int shift = precision - 1 - exponent;
        BigInteger quotient =
            shift >= 0
                ? DivideRounded(numerator << shift, denominator)
                : DivideRounded(numerator, denominator << -shift);
        return RoundToPrecision(quotient, -shift, precision, false);
    }

    /// <summary>
    /// Rounds an exact integer product to the selected significand width.
    /// </summary>
    /// <param name="magnitude">The nonnegative exact significand.</param>
    /// <param name="scale">Its exact binary exponent.</param>
    /// <param name="precision">The number of retained significand bits.</param>
    /// <param name="negative">The result's sign.</param>
    /// <returns>The nearest even finite result.</returns>
    private static Binary RoundToPrecision(
        BigInteger magnitude,
        int scale,
        int precision,
        bool negative
    )
    {
        int discarded = GetBitLength(magnitude) - precision;
        if (discarded > 0)
        {
            magnitude = RoundShift(magnitude, discarded);
            scale += discarded;
            if (GetBitLength(magnitude) > precision)
            {
                magnitude >>= 1;
                scale++;
            }
        }
        return new Binary(magnitude, scale, negative);
    }

    /// <summary>
    /// Rounds discarded binary digits to the nearest even retained integer.
    /// </summary>
    /// <param name="value">The nonnegative integer to round.</param>
    /// <param name="shift">The positive number of discarded digits.</param>
    /// <returns>The rounded retained integer.</returns>
    private static BigInteger RoundShift(BigInteger value, int shift) =>
        DivideRounded(value, BigInteger.One << shift);

    /// <summary>
    /// Rounds an exact positive quotient to the nearest even integer.
    /// </summary>
    /// <param name="numerator">The nonnegative numerator.</param>
    /// <param name="denominator">The positive denominator.</param>
    /// <returns>The rounded integer quotient.</returns>
    private static BigInteger DivideRounded(BigInteger numerator, BigInteger denominator)
    {
        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        int comparison = (remainder << 1).CompareTo(denominator);
        return comparison > 0 || comparison == 0 && !quotient.IsEven ? quotient + 1 : quotient;
    }

    /// <summary>
    /// Counts significant integer digits without a floating-point logarithm.
    /// </summary>
    /// <param name="value">The nonnegative integer.</param>
    /// <returns>Its bit count, or zero for zero.</returns>
    private static int GetBitLength(BigInteger value)
    {
        int count = 0;
        while (value > ulong.MaxValue)
        {
            value >>= 64;
            count += 64;
        }
        ulong tail = (ulong)value;
        while (tail != 0)
        {
            tail >>= 1;
            count++;
        }
        return count;
    }

    /// <summary>
    /// Uses the host's C math ABI for the binary64 large-angle path.
    /// </summary>
    /// <param name="radians">The exactly converted angle.</param>
    /// <returns>The host tangent result.</returns>
    private static double NativeTangent(double radians)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsTangent(radians);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return MacTangent(radians);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return LinuxTangent(radians);
        throw new PlatformNotSupportedException("The platform has no configured C math library.");
    }

    /// <summary>
    /// Evaluates binary64 tangent through the Windows C math API.
    /// </summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The platform tangent.</returns>
    [DllImport(
        "api-ms-win-crt-math-l1-1-0.dll",
        EntryPoint = "tan",
        CallingConvention = CallingConvention.Cdecl
    )]
    private static extern double WindowsTangent(double radians);

    /// <summary>
    /// Evaluates binary64 tangent through the macOS C math API.
    /// </summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The platform tangent.</returns>
    [DllImport(
        "/usr/lib/libSystem.B.dylib",
        EntryPoint = "tan",
        CallingConvention = CallingConvention.Cdecl
    )]
    private static extern double MacTangent(double radians);

    /// <summary>
    /// Evaluates binary64 tangent through the GNU C math ABI.
    /// </summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The platform tangent.</returns>
    [DllImport("libm.so.6", EntryPoint = "tan", CallingConvention = CallingConvention.Cdecl)]
    private static extern double LinuxTangent(double radians);
}
