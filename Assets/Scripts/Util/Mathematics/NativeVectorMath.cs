using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Rebellion.Util.Mathematics
{
    /// <summary>
    /// Executes complete vector operations using the target's native retained arithmetic.
    /// </summary>
    internal static class NativeVectorMath
    {
        internal static readonly uint SignificandBits;

        /// <summary>
        /// Checks the library ABI and records the target arithmetic capability.
        /// </summary>
        static NativeVectorMath()
        {
            if (GetAbi() != 1 || GetVectorSize() != Marshal.SizeOf<Vector3>())
                throw new PlatformNotSupportedException(
                    "The vector math library ABI is incompatible."
                );
            SignificandBits = GetSignificandBits();
        }

        /// <summary>
        /// Measures a vector with native retained sums and a native square root.
        /// </summary>
        /// <param name="x">The first coordinate.</param>
        /// <param name="y">The second coordinate.</param>
        /// <param name="z">The third coordinate.</param>
        /// <returns>The magnitude rounded to a float.</returns>
        internal static float Measure(float x, float y, float z)
        {
            Check(MeasureNative(x, y, z, out float result));
            return result;
        }

        /// <summary>
        /// Turns a heading with retained angle, trigonometric, and normalization intermediates.
        /// </summary>
        /// <param name="heading">The stored normalized heading.</param>
        /// <param name="target">The requested normalized heading.</param>
        /// <param name="rate">The resolved angular speed in radians per second.</param>
        /// <param name="angle">The stored unsigned heading angle.</param>
        /// <param name="elapsed">The elapsed milliseconds.</param>
        /// <param name="applied">Receives the signed applied angle rounded to a float.</param>
        /// <returns>The updated heading.</returns>
        internal static Vector3 Turn(
            Vector3 heading,
            Vector3 target,
            float rate,
            float angle,
            float elapsed,
            out float applied
        )
        {
            Check(TurnNative(angle, elapsed, rate, ref target, ref heading, out applied));
            return heading;
        }

        /// <summary>
        /// Normalizes a formation direction with a stored float magnitude and retained reciprocal.
        /// </summary>
        /// <param name="direction">The requested direction.</param>
        /// <returns>The normalized direction, preserving a zero or unordered magnitude.</returns>
        internal static Vector3 NormalizeDirection(Vector3 direction)
        {
            Check(NormalizeDirectionNative(ref direction));
            return direction;
        }

        /// <summary>
        /// Aligns a stationary capital's target direction with its selected weapon battery.
        /// </summary>
        /// <param name="delta">The stored target position relative to the capital.</param>
        /// <param name="arc">The clockwise battery index, with four representing an absent selection.</param>
        /// <returns>The rotated direction before planar normalization.</returns>
        internal static Vector3 RotateBatteryDirection(Vector3 delta, uint arc)
        {
            Check(RotateBatteryDirectionNative(ref delta, arc, out Vector3 result));
            return result;
        }

        /// <summary>
        /// Calculates a direct formation member's offset.
        /// </summary>
        /// <param name="count">The current unsigned member count.</param>
        /// <param name="heading">The formation heading.</param>
        /// <returns>The member offset.</returns>
        internal static Vector3 DirectOffset(uint count, Vector3 heading)
        {
            Check(DirectOffsetNative(count, ref heading, out Vector3 result));
            return result;
        }

        /// <summary>
        /// Calculates a surrounding member offset and updates the stored shell scale.
        /// </summary>
        /// <param name="count">The current unsigned member count.</param>
        /// <param name="scale">The previous shell scale, replaced by the resulting stored scale.</param>
        /// <param name="configuredScale">The configured shell spacing.</param>
        /// <returns>The member offset.</returns>
        internal static Vector3 SurroundOffset(uint count, ref float scale, float configuredScale)
        {
            Check(
                SurroundOffsetNative(
                    count,
                    scale,
                    configuredScale,
                    out float storedScale,
                    out Vector3 result
                )
            );
            scale = storedScale;
            return result;
        }

        /// <summary>
        /// Calculates a line member offset with truncation before the wrapped spacing product.
        /// </summary>
        /// <param name="count">The current unsigned member count.</param>
        /// <param name="maximumCount">The retained maximum member count.</param>
        /// <param name="extent">The joining unit's collision extent.</param>
        /// <param name="floor">The retained spacing floor.</param>
        /// <param name="heading">The formation heading.</param>
        /// <returns>The member offset.</returns>
        internal static Vector3 LineOffset(
            uint count,
            uint maximumCount,
            float extent,
            float floor,
            Vector3 heading
        )
        {
            Check(
                LineOffsetNative(
                    count,
                    maximumCount,
                    extent,
                    floor,
                    ref heading,
                    out Vector3 result
                )
            );
            return result;
        }

        /// <summary>
        /// Computes a member displacement and applies the formation's complete distance gate.
        /// </summary>
        /// <param name="marker">The formation reference position.</param>
        /// <param name="offset">The member's stored offset.</param>
        /// <param name="unit">The member's live position.</param>
        /// <param name="velocity">The direct formation's fallback velocity.</param>
        /// <param name="direct">Whether the direct distance gate applies.</param>
        /// <returns>The admitted displacement or the applicable fallback.</returns>
        internal static Vector3 MemberTarget(
            Vector3 marker,
            Vector3 offset,
            Vector3 unit,
            Vector3 velocity,
            bool direct
        )
        {
            Check(
                MemberTargetNative(
                    ref marker,
                    ref offset,
                    ref unit,
                    ref velocity,
                    direct ? 1u : 0u,
                    out Vector3 result
                )
            );
            return result;
        }

        /// <summary>
        /// Rotates a populated vector buffer in one native call.
        /// </summary>
        /// <param name="values">The reusable offset buffer.</param>
        /// <param name="count">The populated prefix length.</param>
        /// <param name="angle">The signed yaw in radians.</param>
        internal static void RotateOffsets(Vector3[] values, int count, float angle)
        {
            if (count > 0)
                Check(RotateOffsetsNative(angle, values, (uint)count));
        }

        /// <summary>
        /// Updates a member spacing floor with an ordered comparison before its float store.
        /// </summary>
        /// <param name="floor">The previous floor.</param>
        /// <param name="extent">The joining member's collision extent.</param>
        /// <returns>The updated stored spacing floor.</returns>
        internal static float UpdateSpacing(float floor, float extent)
        {
            Check(SpacingNative(floor, extent, out float result));
            return result;
        }

        /// <summary>
        /// Rejects a native operation whose arithmetic environment could not be established or restored.
        /// </summary>
        /// <param name="status">The native success flag.</param>
        private static void Check(int status)
        {
            if (status == 0)
                throw new InvalidOperationException(
                    "The vector arithmetic environment is unavailable."
                );
        }

        /// <summary>
        /// Reads the native ABI version.
        /// </summary>
        /// <returns>The native ABI version.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_abi",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern uint GetAbi();

        /// <summary>
        /// Reads the native vector size.
        /// </summary>
        /// <returns>The native vector size in bytes.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_vector_size",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern uint GetVectorSize();

        /// <summary>
        /// Reads the native arithmetic significand width.
        /// </summary>
        /// <returns>The retained significant binary digit count.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_significand_bits",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern uint GetSignificandBits();

        /// <summary>
        /// Measures native retained length.
        /// </summary>
        /// <param name="x">The first coordinate.</param>
        /// <param name="y">The second coordinate.</param>
        /// <param name="z">The third coordinate.</param>
        /// <param name="result">Receives the stored magnitude.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_length",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int MeasureNative(float x, float y, float z, out float result);

        /// <summary>
        /// Executes a complete native heading turn.
        /// </summary>
        /// <param name="angle">The stored unsigned angle.</param>
        /// <param name="elapsed">The elapsed milliseconds.</param>
        /// <param name="rate">The resolved angular speed.</param>
        /// <param name="target">The requested normalized heading.</param>
        /// <param name="current">The heading updated in place.</param>
        /// <param name="applied">Receives the stored signed turn.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_turn",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int TurnNative(
            float angle,
            float elapsed,
            float rate,
            ref Vector3 target,
            ref Vector3 current,
            out float applied
        );

        /// <summary>
        /// Normalizes a formation direction in place.
        /// </summary>
        /// <param name="direction">The direction updated in place.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_normalize_direction",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int NormalizeDirectionNative(ref Vector3 direction);

        /// <summary>
        /// Rotates a stored target direction with native retained trigonometric intermediates.
        /// </summary>
        /// <param name="delta">The target displacement.</param>
        /// <param name="arc">The selected clockwise battery index.</param>
        /// <param name="result">Receives the stored rotated direction.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_battery_direction",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int RotateBatteryDirectionNative(
            ref Vector3 delta,
            uint arc,
            out Vector3 result
        );

        /// <summary>
        /// Calculates a native direct offset.
        /// </summary>
        /// <param name="count">The current member count.</param>
        /// <param name="heading">The formation heading.</param>
        /// <param name="result">Receives the offset.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_direct_offset",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int DirectOffsetNative(
            uint count,
            ref Vector3 heading,
            out Vector3 result
        );

        /// <summary>
        /// Calculates a native surround offset.
        /// </summary>
        /// <param name="count">The current member count.</param>
        /// <param name="previousScale">The previous stored scale.</param>
        /// <param name="configuredScale">The configured shell spacing.</param>
        /// <param name="storedScale">Receives the updated stored scale.</param>
        /// <param name="result">Receives the offset.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_surround_offset",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int SurroundOffsetNative(
            uint count,
            float previousScale,
            float configuredScale,
            out float storedScale,
            out Vector3 result
        );

        /// <summary>
        /// Calculates a native line offset.
        /// </summary>
        /// <param name="count">The current member count.</param>
        /// <param name="maximumCount">The retained maximum member count.</param>
        /// <param name="extent">The joining member's collision extent.</param>
        /// <param name="floor">The retained spacing floor.</param>
        /// <param name="heading">The formation heading.</param>
        /// <param name="result">Receives the offset.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_line_offset",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int LineOffsetNative(
            uint count,
            uint maximumCount,
            float extent,
            float floor,
            ref Vector3 heading,
            out Vector3 result
        );

        /// <summary>
        /// Calculates a native member displacement and distance gate.
        /// </summary>
        /// <param name="marker">The reference position.</param>
        /// <param name="offset">The stored member offset.</param>
        /// <param name="unit">The member position.</param>
        /// <param name="velocity">The direct fallback velocity.</param>
        /// <param name="direct">The direct-mode flag.</param>
        /// <param name="result">Receives the admitted movement.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_member_target",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int MemberTargetNative(
            ref Vector3 marker,
            ref Vector3 offset,
            ref Vector3 unit,
            ref Vector3 velocity,
            uint direct,
            out Vector3 result
        );

        /// <summary>
        /// Rotates the populated native vector buffer.
        /// </summary>
        /// <param name="angle">The signed yaw.</param>
        /// <param name="values">The contiguous vector buffer.</param>
        /// <param name="count">The populated prefix length.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_rotate_offsets",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int RotateOffsetsNative(
            float angle,
            [In, Out] Vector3[] values,
            uint count
        );

        /// <summary>
        /// Updates a native spacing floor.
        /// </summary>
        /// <param name="floor">The previous floor.</param>
        /// <param name="extent">The joining member's collision extent.</param>
        /// <param name="result">Receives the stored floor.</param>
        /// <returns>The arithmetic environment success flag.</returns>
        [DllImport(
            "RebellionTacticalMath",
            EntryPoint = "tactical_math_spacing",
            CallingConvention = CallingConvention.Cdecl
        )]
        private static extern int SpacingNative(float floor, float extent, out float result);
    }
}
