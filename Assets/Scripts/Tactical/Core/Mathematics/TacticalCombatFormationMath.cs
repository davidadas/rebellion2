using System;
using System.Numerics;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Calculates formation offsets with double-precision intermediates and float storage.
    /// </summary>
    internal static class TacticalCombatFormationMath
    {
        /// <summary>
        /// Calculates the staggered offset of a direct formation member.
        /// </summary>
        /// <param name="count">The current member count.</param>
        /// <param name="heading">The formation heading.</param>
        /// <returns>The member's offset.</returns>
        internal static Vector3 DirectOffset(uint count, Vector3 heading)
        {
            if (count == 1)
                return Vector3.Zero;
            int side = (count & 1) != 0 ? 1 : -1;
            int spacing = (int)(side * 3.0 * -1.5);
            int halfCount = unchecked((int)count) >> 1;
            int angleIndex = unchecked(1 - spacing * halfCount);
            double baseAngle = angleIndex * (double)3.1415927f;
            double angleA = baseAngle * (double)0.6666667f;
            double cosineA = Math.Cos(angleA);
            float sineA = (float)Math.Sin((float)angleA);
            double angleB = baseAngle * 0.5;
            double cosineB = Math.Cos(angleB);
            double sineB = Math.Sin((float)angleB);
            double angleC = baseAngle * (double)0.4f;
            float cosineC = (float)Math.Cos(angleC);
            float sineC = (float)Math.Sin((float)angleC);
            float zx = (float)(cosineA * heading.Z + (double)sineA * heading.X);
            float xz = (float)((double)(float)cosineA * heading.X - (double)sineA * heading.Z);
            float yz = (float)(sineB * zx + cosineB * heading.Y);
            return new Vector3(
                (float)((double)cosineC * yz + (double)sineC * xz),
                (float)((double)cosineC * xz - (double)sineC * yz),
                (float)((double)(float)cosineB * zx - (double)(float)sineB * heading.Y)
            );
        }

        /// <summary>
        /// Calculates the offset of a surrounding formation member and saves the radius for the next member.
        /// </summary>
        /// <param name="count">The current member count.</param>
        /// <param name="scale">The previous radius, replaced with the new radius.</param>
        /// <param name="configuredScale">The configured shell spacing.</param>
        /// <returns>The member's offset.</returns>
        internal static Vector3 SurroundOffset(uint count, ref float scale, float configuredScale)
        {
            float a = (float)(scale / Math.Sqrt(3.0));
            float b = (float)(scale / Math.Sqrt(2.0));
            Vector3 direction = (count % 22) switch
            {
                0 => new(b, -b, 0),
                1 => new(0, 0, scale),
                2 => new(0, 0, -scale),
                3 => new(scale, 0, 0),
                4 => new(-scale, 0, 0),
                5 => new(0, scale, 0),
                6 => new(0, -scale, 0),
                7 => new(a, a, a),
                8 => new(-a, -a, -a),
                9 => new(-a, a, a),
                10 => new(a, -a, -a),
                11 => new(a, -a, a),
                12 => new(-a, a, -a),
                13 => new(-a, -a, a),
                14 => new(a, a, -a),
                15 or 19 => new(0, -b, -b),
                16 => new(0, -b, b),
                17 => new(-b, b, 0),
                18 => new(b, b, 0),
                20 => new(0, b, b),
                _ => new(-b, -b, 0),
            };
            direction = TacticalCombatGeometry.Normalize(direction);
            double distance = (count / 22 * 0.25 + 1) * configuredScale;
            scale = (float)distance;
            return new Vector3(
                (float)(distance * direction.X),
                (float)(distance * direction.Y),
                (float)(distance * direction.Z)
            );
        }

        /// <summary>
        /// Calculates a line offset using integer spacing and a wrapping product.
        /// </summary>
        /// <param name="count">The current member count.</param>
        /// <param name="maximumCount">The largest observed member count.</param>
        /// <param name="extent">The joining member's collision extent.</param>
        /// <param name="floor">The stored minimum spacing.</param>
        /// <param name="heading">The formation heading.</param>
        /// <returns>The member's offset.</returns>
        internal static Vector3 LineOffset(
            uint count,
            uint maximumCount,
            float extent,
            float floor,
            Vector3 heading
        )
        {
            if (count == 1)
                return Vector3.Zero;
            double spacing = Spacing(floor, extent) * ((count & 1) != 0 ? 1 : -1);
            int integerSpacing =
                spacing >= -9223372036854775808.0 && spacing < 9223372036854775808.0
                    ? unchecked((int)(long)spacing)
                    : 0;
            int scaled = unchecked(
                integerSpacing * (unchecked((int)Math.Max(maximumCount, count)) >> 1)
            );
            return new Vector3(
                (float)(-(double)heading.Z * scaled),
                0,
                (float)((double)heading.X * scaled)
            );
        }

        /// <summary>
        /// Computes displacement and returns the applicable fallback within the formation's movement threshold.
        /// </summary>
        /// <param name="marker">The formation reference position.</param>
        /// <param name="offset">The stored member offset.</param>
        /// <param name="unit">The member position.</param>
        /// <param name="velocity">The fallback velocity for a direct formation.</param>
        /// <param name="direct">Whether the formation uses the direct threshold.</param>
        /// <returns>The admitted displacement or the fallback movement.</returns>
        internal static Vector3 MemberTarget(
            Vector3 marker,
            Vector3 offset,
            Vector3 unit,
            Vector3 velocity,
            bool direct
        )
        {
            float x = (float)((double)marker.X - offset.X - unit.X);
            double y = (double)marker.Y - offset.Y - unit.Y;
            float z = (float)((double)marker.Z - offset.Z - unit.Z);
            if (direct)
            {
                float storedY = (float)y;
                double squared = (double)x * x + (double)storedY * storedY + (double)z * z;
                return squared > 4 ? new Vector3(x, storedY, z) : velocity;
            }
            double otherSquared = (double)x * x + (double)z * z + y * y;
            return otherSquared > 1 ? new Vector3(x, (float)y, z) : Vector3.Zero;
        }

        /// <summary>
        /// Stores the spacing floor after comparing the current floor and the member's extent.
        /// </summary>
        /// <param name="floor">The stored minimum spacing.</param>
        /// <param name="extent">The joining member's collision extent.</param>
        /// <returns>The updated minimum spacing.</returns>
        internal static float UpdateSpacing(float floor, float extent) =>
            (float)Spacing(floor, extent);

        /// <summary>
        /// Resolves the spacing before converting it to the caller's storage precision.
        /// </summary>
        /// <param name="floor">The stored minimum spacing.</param>
        /// <param name="extent">The joining member's collision extent.</param>
        /// <returns>The larger of the floor and the scaled extent, retaining an unordered scaled extent.</returns>
        private static double Spacing(float floor, float extent)
        {
            double spacing = extent * (double)1.2f;
            return floor > spacing ? floor : spacing;
        }
    }
}
