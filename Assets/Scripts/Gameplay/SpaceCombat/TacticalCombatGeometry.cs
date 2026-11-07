using System;
using System.Numerics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies a ship's directional weapon battery in clockwise order.
    /// </summary>
    public enum TacticalWeaponArc
    {
        Forward,
        Starboard,
        Aft,
        Port,
    }

    /// <summary>
    /// Provides the geometry shared by tactical movement, targeting, and presentation.
    /// Tactical positions and headings use a forward positive Z axis.
    /// </summary>
    public static class TacticalCombatGeometry
    {
        private const float _quarterTurnComponent = 0.70710677f;

        /// <summary>
        /// Selects the weapon battery facing a relative target position.
        /// The positive diagonal belongs to the forward or port battery.
        /// </summary>
        /// <param name="heading">The firing ship's heading.</param>
        /// <param name="targetDelta">The target position relative to the firing ship.</param>
        /// <returns>The facing weapon battery.</returns>
        public static TacticalWeaponArc GetWeaponArc(Vector3 heading, Vector3 targetDelta)
        {
            float firstX = (float)(
                (double)_quarterTurnComponent * heading.X
                - (double)_quarterTurnComponent * heading.Z
            );
            float firstZ = (float)(
                (double)_quarterTurnComponent * heading.X
                + (double)_quarterTurnComponent * heading.Z
            );
            float secondX = (float)(
                (double)_quarterTurnComponent * heading.X
                + (double)_quarterTurnComponent * heading.Z
            );
            float secondZ = (float)(
                -(double)_quarterTurnComponent * heading.X
                + (double)_quarterTurnComponent * heading.Z
            );
            float firstDot = (float)(
                (double)targetDelta.X * firstX + (double)targetDelta.Z * firstZ
            );
            float secondDot = (float)(
                (double)targetDelta.X * secondX + (double)targetDelta.Z * secondZ
            );
            bool firstNegative = firstDot < 0 || float.IsNaN(firstDot);
            bool secondNegative = secondDot < 0 || float.IsNaN(secondDot);
            if (firstNegative)
                return secondNegative ? TacticalWeaponArc.Aft : TacticalWeaponArc.Starboard;
            return secondNegative ? TacticalWeaponArc.Port : TacticalWeaponArc.Forward;
        }

        /// <summary>
        /// Integrates a velocity expressed in tactical distance units per second.
        /// </summary>
        /// <param name="position">The position before movement.</param>
        /// <param name="velocity">The current velocity.</param>
        /// <param name="elapsedMilliseconds">The elapsed movement time in milliseconds.</param>
        /// <returns>The position after movement.</returns>
        public static Vector3 Integrate(
            Vector3 position,
            Vector3 velocity,
            float elapsedMilliseconds
        )
        {
            double elapsed = (double)elapsedMilliseconds * 0.001f;
            float deltaX = (float)(elapsed * velocity.X);
            return new Vector3(
                (float)((double)position.X + deltaX),
                (float)(elapsed * velocity.Y + position.Y),
                (float)(elapsed * velocity.Z + position.Z)
            );
        }

        /// <summary>
        /// Measures the angle between two stored, normalized headings without rescaling their components.
        /// </summary>
        /// <param name="heading">The current heading.</param>
        /// <param name="targetHeading">The requested heading.</param>
        /// <returns>The unsigned angle in radians.</returns>
        public static float GetHeadingAngle(Vector3 heading, Vector3 targetHeading)
        {
            float dot = (float)(
                (double)targetHeading.Y * heading.Y
                + (double)targetHeading.Z * heading.Z
                + (double)targetHeading.X * heading.X
            );
            return (float)Math.Acos(Math.Max(Math.Min(dot, 1), -1));
        }

        /// <summary>
        /// Turns a heading about the vertical axis, snapping to the requested heading
        /// when the available turn reaches it.
        /// </summary>
        /// <param name="heading">The normalized current heading.</param>
        /// <param name="targetHeading">The normalized requested heading.</param>
        /// <param name="turnRate">The available turn rate in radians per second.</param>
        /// <param name="elapsedMilliseconds">The elapsed movement time in milliseconds.</param>
        /// <param name="appliedAngle">Receives the signed applied angle in radians.</param>
        /// <returns>The heading after turning.</returns>
        public static Vector3 TurnTowards(
            Vector3 heading,
            Vector3 targetHeading,
            float turnRate,
            float elapsedMilliseconds,
            out float appliedAngle
        )
        {
            float angle = GetHeadingAngle(heading, targetHeading);
            appliedAngle = angle;
            if (angle == 0 || float.IsNaN(angle))
                return heading;

            double side =
                -(double)heading.Z * targetHeading.X
                + (double)targetHeading.Z * heading.X
                + (double)targetHeading.Y * heading.Y;
            double signedAngle = side < 0 || double.IsNaN(side) ? -angle : angle;
            double ratio = (double)turnRate * elapsedMilliseconds / ((double)angle * 1000);
            if (ratio >= 1)
            {
                appliedAngle = (float)signedAngle;
                return targetHeading;
            }

            double turn = ratio * signedAngle;
            appliedAngle = (float)turn;
            float cosine = (float)Math.Cos(turn);
            float sine = (float)Math.Sin(turn);
            return Normalize(
                new Vector3(
                    (float)((double)heading.X * cosine - (double)sine * heading.Z),
                    heading.Y,
                    (float)((double)heading.X * sine + (double)cosine * heading.Z)
                )
            );
        }

        /// <summary>
        /// Normalizes a vector while retaining a zero vector without selecting a heading.
        /// </summary>
        /// <param name="vector">The vector to normalize.</param>
        /// <returns>The normalized vector, or the supplied zero vector.</returns>
        public static Vector3 Normalize(Vector3 vector)
        {
            return Normalize(vector, GetLength(vector));
        }

        /// <summary>
        /// Normalizes a vector using its stored magnitude without rounding the reciprocal scale.
        /// </summary>
        /// <param name="vector">The vector to normalize.</param>
        /// <param name="length">The vector's stored magnitude.</param>
        /// <returns>The normalized vector, preserving a zero or unordered magnitude.</returns>
        internal static Vector3 Normalize(Vector3 vector, float length)
        {
            if (length <= 0 || float.IsNaN(length))
                return vector;
            double inverse = 1.0 / length;
            return new Vector3(
                (float)(vector.X * inverse),
                (float)(vector.Y * inverse),
                (float)(vector.Z * inverse)
            );
        }

        /// <summary>
        /// Normalizes a direction with a stored single-precision scale.
        /// </summary>
        /// <param name="heading">The requested direction before normalization.</param>
        /// <returns>The normalized direction, preserving a zero or unordered direction.</returns>
        internal static Vector3 NormalizeWithStoredScale(Vector3 heading)
        {
            float length = (float)
                Math.Sqrt(
                    (double)heading.Y * heading.Y
                        + (double)heading.Z * heading.Z
                        + (double)heading.X * heading.X
                );
            return NormalizeWithStoredScale(heading, length);
        }

        /// <summary>
        /// Normalizes a direction using its stored magnitude and a single-precision scale.
        /// </summary>
        /// <param name="heading">The requested direction before normalization.</param>
        /// <param name="length">The direction's stored magnitude.</param>
        /// <returns>The normalized direction, preserving a zero or unordered magnitude.</returns>
        internal static Vector3 NormalizeWithStoredScale(Vector3 heading, float length)
        {
            if (!(length > 0))
                return heading;
            float inverse = (float)(1.0 / length);
            return new Vector3(
                (float)((double)heading.X * inverse),
                (float)((double)heading.Y * inverse),
                (float)((double)heading.Z * inverse)
            );
        }

        /// <summary>
        /// Measures a vector's length without storing a single-precision magnitude.
        /// </summary>
        /// <param name="vector">The vector to measure.</param>
        /// <returns>The magnitude before single-precision rounding.</returns>
        internal static double MeasureLength(Vector3 vector)
        {
            return Math.Sqrt(
                (double)vector.X * vector.X
                    + ((double)vector.Y * vector.Y + (double)vector.Z * vector.Z)
            );
        }

        /// <summary>
        /// Measures a vector's three-dimensional length with a single final float rounding.
        /// </summary>
        /// <param name="vector">The vector to measure.</param>
        /// <returns>The vector's length.</returns>
        public static float GetLength(Vector3 vector)
        {
            return (float)
                Math.Sqrt(
                    (double)vector.X * vector.X
                        + (double)vector.Z * vector.Z
                        + (double)vector.Y * vector.Y
                );
        }
    }
}
