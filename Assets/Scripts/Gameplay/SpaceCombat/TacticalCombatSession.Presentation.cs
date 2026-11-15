using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rebellion.Gameplay
{
    public sealed partial class TacticalCombatSession
    {
        private TacticalCombatPresentationBuffer _networkPresentation;

        public double NetworkPresentationTimestampMilliseconds =>
            _networkPresentation?.TimestampMilliseconds ?? 0;

        /// <summary>
        /// Advances a passive view's delayed poses without advancing combat, outcomes, or random state.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed local view time.</param>
        public void AdvanceNetworkPresentation(float elapsedMilliseconds)
        {
            if (_networkReplica)
                _networkPresentation?.Advance(elapsedMilliseconds);
        }

        /// <summary>
        /// Samples a replica's presentation pose while retaining the authoritative participant state.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The delayed canonical position.</param>
        /// <param name="heading">The delayed heading.</param>
        /// <returns>Whether this passive view has a captured pose for the participant.</returns>
        public bool TryGetNetworkPresentationPose(
            string unitID,
            out Vector3 position,
            out Vector3 heading
        ) => TryGetNetworkPresentationPose(unitID, out position, out heading, out _);

        /// <summary>
        /// Samples a replica's pose and continuous departure time for its local rendering transform.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The delayed canonical position.</param>
        /// <param name="heading">The delayed heading.</param>
        /// <param name="hyperspaceMilliseconds">The delayed departure animation time.</param>
        /// <returns>Whether this passive view has a captured pose for the participant.</returns>
        public bool TryGetNetworkPresentationPose(
            string unitID,
            out Vector3 position,
            out Vector3 heading,
            out float hyperspaceMilliseconds
        ) =>
            TryGetNetworkPresentationPose(
                unitID,
                out position,
                out heading,
                out hyperspaceMilliseconds,
                out _,
                out _
            );

        /// <summary>
        /// Samples motion and visibility at the same delayed timestamp as reliable presentation effects.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The delayed canonical position.</param>
        /// <param name="heading">The delayed heading.</param>
        /// <param name="hyperspaceMilliseconds">The delayed departure animation time.</param>
        /// <param name="status">The lifecycle at the sampled timestamp.</param>
        /// <param name="isAlive">Whether the hull was alive at the sampled timestamp.</param>
        /// <returns>Whether this passive view has a captured pose for the participant.</returns>
        public bool TryGetNetworkPresentationPose(
            string unitID,
            out Vector3 position,
            out Vector3 heading,
            out float hyperspaceMilliseconds,
            out TacticalCombatUnitStatus status,
            out bool isAlive
        )
        {
            position = default;
            heading = default;
            hyperspaceMilliseconds = 0;
            status = default;
            isAlive = false;
            if (
                !_networkReplica
                || _networkPresentation == null
                || !_networkPresentation.TryGetPose(
                    unitID,
                    out TacticalCombatPresentationBuffer.Pose pose
                )
            )
                return false;
            position = pose.Position;
            heading = pose.Heading;
            hyperspaceMilliseconds = pose.HyperspaceMilliseconds;
            status = pose.Status;
            isAlive = pose.IsAlive;
            return true;
        }

        /// <summary>
        /// Resolves a replica's delayed visibility lifecycle without changing current command eligibility.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="status">The lifecycle at the sampled timestamp.</param>
        /// <param name="isAlive">Whether the hull was alive at the sampled timestamp.</param>
        /// <returns>Whether this passive view has a captured lifecycle for the participant.</returns>
        public bool TryGetNetworkPresentationLifecycle(
            string unitID,
            out TacticalCombatUnitStatus status,
            out bool isAlive
        ) => TryGetNetworkPresentationPose(unitID, out _, out _, out _, out status, out isAlive);

        /// <summary>
        /// Captures validated pose data before the incoming frame changes any authoritative replica state.
        /// </summary>
        /// <param name="frame">The identity-validated complete authority projection.</param>
        private void CaptureNetworkPresentation(TacticalCombatNetworkFrame frame)
        {
            Dictionary<string, TacticalCombatPresentationBuffer.Pose> poses = new(
                frame.Units.Length,
                StringComparer.Ordinal
            );
            foreach (TacticalCombatNetworkUnitState state in frame.Units)
                poses.Add(
                    state.UnitID,
                    new TacticalCombatPresentationBuffer.Pose(
                        state.Position.ToVector(),
                        state.Heading.ToVector(),
                        state.Status,
                        state.CarrierID,
                        state.HasGroup ? state.GroupID : null,
                        state.HyperspaceMilliseconds,
                        state.Hull > 0
                    )
                );
            _networkPresentation ??= new TacticalCombatPresentationBuffer();
            if (!_networkPresentation.Add(frame.Sequence, frame.TimestampMilliseconds, poses))
                throw new ArgumentException(
                    "The combat presentation timestamp or pose is invalid.",
                    nameof(frame)
                );
        }
    }
}
