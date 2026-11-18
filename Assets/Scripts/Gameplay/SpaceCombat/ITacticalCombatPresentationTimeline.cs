using System.Numerics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Exposes sampled encounter poses without granting ownership of playback or transport.
    /// </summary>
    public interface ITacticalCombatPresentationTimeline
    {
        TacticalCombatSession Session { get; }
        double TimestampMilliseconds { get; }

        /// <summary>
        /// Samples a participant's position and heading at the presentation timestamp.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The sampled canonical position.</param>
        /// <param name="heading">The sampled heading.</param>
        /// <returns>Whether a pose exists for this participant.</returns>
        bool TryGetPose(string unitID, out Vector3 position, out Vector3 heading);

        /// <summary>
        /// Samples motion and lifecycle at the presentation timestamp.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The sampled canonical position.</param>
        /// <param name="heading">The sampled heading.</param>
        /// <param name="hyperspaceMilliseconds">The sampled departure animation time.</param>
        /// <param name="status">The sampled lifecycle.</param>
        /// <param name="isAlive">Whether the sampled hull was alive.</param>
        /// <returns>Whether a pose exists for this participant.</returns>
        bool TryGetPose(
            string unitID,
            out Vector3 position,
            out Vector3 heading,
            out float hyperspaceMilliseconds,
            out TacticalCombatUnitStatus status,
            out bool isAlive
        );

        /// <summary>
        /// Samples visibility without changing current command eligibility.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="status">The sampled lifecycle.</param>
        /// <param name="isAlive">Whether the sampled hull was alive.</param>
        /// <returns>Whether a lifecycle exists for this participant.</returns>
        bool TryGetLifecycle(string unitID, out TacticalCombatUnitStatus status, out bool isAlive);
    }
}
