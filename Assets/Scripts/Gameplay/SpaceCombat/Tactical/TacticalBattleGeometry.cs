using System;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Provides planar angle, distance, and firing-arc helpers for the tactical space battle.
    /// </summary>
    internal static class TacticalBattleGeometry
    {
        private const double _fullTurn = Math.PI * 2;
        private const double _quarterTurn = Math.PI / 2;
        private const double _eighthTurn = Math.PI / 4;

        /// <summary>
        /// Normalizes an angle into the half-open range from negative pi to pi.
        /// </summary>
        /// <param name="angle">The angle in radians.</param>
        /// <returns>The equivalent angle within the normalized range.</returns>
        internal static double NormalizeAngle(double angle)
        {
            angle %= _fullTurn;
            if (angle > Math.PI)
                angle -= _fullTurn;
            else if (angle <= -Math.PI)
                angle += _fullTurn;
            return angle;
        }

        /// <summary>
        /// Returns the direction from one point to another.
        /// </summary>
        /// <param name="fromX">The origin horizontal coordinate.</param>
        /// <param name="fromY">The origin vertical coordinate.</param>
        /// <param name="toX">The destination horizontal coordinate.</param>
        /// <param name="toY">The destination vertical coordinate.</param>
        /// <returns>The direction in radians measured counter-clockwise from the positive x axis.</returns>
        internal static double GetAngle(double fromX, double fromY, double toX, double toY)
        {
            return Math.Atan2(toY - fromY, toX - fromX);
        }

        /// <summary>
        /// Returns the distance between two points.
        /// </summary>
        /// <param name="fromX">The first horizontal coordinate.</param>
        /// <param name="fromY">The first vertical coordinate.</param>
        /// <param name="toX">The second horizontal coordinate.</param>
        /// <param name="toY">The second vertical coordinate.</param>
        /// <returns>The planar distance between the points.</returns>
        internal static double GetDistance(double fromX, double fromY, double toX, double toY)
        {
            double deltaX = toX - fromX;
            double deltaY = toY - fromY;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        /// <summary>
        /// Returns the firing arc that covers a bearing relative to a ship's bow.
        /// </summary>
        /// <param name="relativeBearing">The bearing in radians relative to the bow.</param>
        /// <returns>The firing arc covering the bearing.</returns>
        internal static PrimaryWeaponArc GetArc(double relativeBearing)
        {
            double bearing = NormalizeAngle(relativeBearing);
            double magnitude = Math.Abs(bearing);
            if (magnitude <= _eighthTurn)
                return PrimaryWeaponArc.Fore;
            if (magnitude >= 3 * _eighthTurn)
                return PrimaryWeaponArc.Aft;
            return bearing > 0 ? PrimaryWeaponArc.Port : PrimaryWeaponArc.Starboard;
        }

        /// <summary>
        /// Returns the bearing of a firing arc's center relative to the bow.
        /// </summary>
        /// <param name="arc">The firing arc.</param>
        /// <returns>The arc center bearing in radians.</returns>
        internal static double GetArcOffset(PrimaryWeaponArc arc)
        {
            return arc switch
            {
                PrimaryWeaponArc.Port => _quarterTurn,
                PrimaryWeaponArc.Aft => Math.PI,
                PrimaryWeaponArc.Starboard => -_quarterTurn,
                _ => 0,
            };
        }

        /// <summary>
        /// Rotates a heading toward a desired heading by at most one step.
        /// </summary>
        /// <param name="heading">The current heading in radians.</param>
        /// <param name="desiredHeading">The desired heading in radians.</param>
        /// <param name="maximumStep">The largest permitted rotation in radians.</param>
        /// <returns>The rotated heading.</returns>
        internal static double TurnToward(double heading, double desiredHeading, double maximumStep)
        {
            double difference = NormalizeAngle(desiredHeading - heading);
            if (Math.Abs(difference) <= maximumStep)
                return NormalizeAngle(desiredHeading);

            return NormalizeAngle(heading + Math.Sign(difference) * maximumStep);
        }
    }
}
