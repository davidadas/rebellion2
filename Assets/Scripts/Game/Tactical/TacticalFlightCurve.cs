using System;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Provides the four staggered quadratic flight curves used when ships enter or leave battle.
    /// </summary>
    internal static class TacticalFlightCurve
    {
        internal const float ArrivalDuration = 1.5f;
        internal const float MaximumArrivalStep = 0.2f;
        internal const float WithdrawalDuration = 4f;

        private static readonly float[] Distances = { 40f, 57.5f, 65f, 32.5f };
        private static readonly float[] DurationOffsets = { 0f, 0.25f, 0.075f, 0.275f };

        /// <summary>
        /// Gets the distance remaining before a unit reaches its battle position.
        /// </summary>
        /// <param name="lane">The unit's flight-curve lane.</param>
        /// <param name="elapsedTime">The elapsed arrival time in seconds.</param>
        /// <returns>The nonnegative distance remaining along the approach vector.</returns>
        internal static float GetArrivalDistance(int lane, float elapsedTime)
        {
            int normalizedLane = NormalizeLane(lane);
            float duration = ArrivalDuration - DurationOffsets[normalizedLane];
            float progress = Math.Max(0f, elapsedTime) / duration;
            return Distances[normalizedLane] * Math.Max(0f, 1f - progress * progress);
        }

        /// <summary>
        /// Gets the distance traveled by a unit leaving the battlefield.
        /// </summary>
        /// <param name="lane">The unit's flight-curve lane.</param>
        /// <param name="elapsedTime">The elapsed withdrawal time in seconds.</param>
        /// <returns>The distance traveled along the exit vector.</returns>
        internal static float GetWithdrawalDistance(int lane, float elapsedTime)
        {
            int normalizedLane = NormalizeLane(lane);
            float duration = WithdrawalDuration - DurationOffsets[normalizedLane];
            float progress = Math.Clamp(elapsedTime, 0f, WithdrawalDuration) / duration;
            return Distances[normalizedLane] * progress * progress;
        }

        /// <summary>
        /// Normalizes a stable tactical object key to one of the four flight curves.
        /// </summary>
        /// <param name="lane">The tactical object's curve key.</param>
        /// <returns>A curve index from zero through three.</returns>
        private static int NormalizeLane(int lane)
        {
            int normalized = lane % Distances.Length;
            return normalized < 0 ? normalized + Distances.Length : normalized;
        }
    }
}
