using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Tactical;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Multiplayer.Tactical
{
    /// <summary>
    /// Owns a replica's delayed display timeline independently of authoritative battle state.
    /// </summary>
    public sealed class TacticalCombatReplicaPlayback
        : ITacticalCombatPresentationTimeline,
            IDisposable
    {
        private TacticalCombatReplicaBuffer _replicaBuffer;
        private bool _disposed;

        public TacticalCombatSession Session { get; }
        public double TimestampMilliseconds => _replicaBuffer?.TimestampMilliseconds ?? 0;

        /// <summary>
        /// Observes validated replica projections before they update participant state.
        /// </summary>
        /// <param name="session">The passive encounter whose display timeline is owned.</param>
        public TacticalCombatReplicaPlayback(TacticalCombatSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            if (!session.IsReplica)
                throw new InvalidOperationException("Only a combat replica has delayed playback.");
            session.SnapshotApplying += CaptureSnapshotPresentation;
        }

        /// <summary>
        /// Advances a passive view's delayed poses without advancing combat, outcomes, or random state.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed local view time.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (!_disposed)
                _replicaBuffer?.Advance(elapsedMilliseconds);
        }

        /// <summary>
        /// Samples a replica's presentation pose while retaining the authoritative participant state.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The delayed canonical position.</param>
        /// <param name="heading">The delayed heading.</param>
        /// <returns>Whether this passive view has a captured pose for the participant.</returns>
        public bool TryGetPose(string unitID, out Vector3 position, out Vector3 heading) =>
            TryGetPose(unitID, out position, out heading, out _);

        /// <summary>
        /// Samples a replica's pose and continuous departure time for its local rendering transform.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The delayed canonical position.</param>
        /// <param name="heading">The delayed heading.</param>
        /// <param name="hyperspaceMilliseconds">The delayed departure animation time.</param>
        /// <returns>Whether this passive view has a captured pose for the participant.</returns>
        public bool TryGetPose(
            string unitID,
            out Vector3 position,
            out Vector3 heading,
            out float hyperspaceMilliseconds
        ) =>
            TryGetPose(unitID, out position, out heading, out hyperspaceMilliseconds, out _, out _);

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
        public bool TryGetPose(
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
                _replicaBuffer == null
                || !_replicaBuffer.TryGetPose(
                    unitID,
                    out TacticalCombatReplicaBuffer.Pose pose
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
        public bool TryGetLifecycle(
            string unitID,
            out TacticalCombatUnitStatus status,
            out bool isAlive
        ) => TryGetPose(unitID, out _, out _, out _, out status, out isAlive);

        /// <summary>
        /// Stops observing projections while retaining the last displayed pose for departing readers.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Session.SnapshotApplying -= CaptureSnapshotPresentation;
        }

        /// <summary>
        /// Captures validated pose data before the incoming snapshot changes any authoritative replica state.
        /// </summary>
        /// <param name="snapshot">The identity-validated complete authority projection.</param>
        private void CaptureSnapshotPresentation(TacticalCombatSnapshot snapshot)
        {
            Dictionary<string, TacticalCombatReplicaBuffer.Pose> poses = new(
                snapshot.Units.Length,
                StringComparer.Ordinal
            );
            foreach (TacticalCombatUnitSnapshot state in snapshot.Units)
                poses.Add(
                    state.UnitID,
                    new TacticalCombatReplicaBuffer.Pose(
                        state.Position.ToVector(),
                        state.Heading.ToVector(),
                        state.Status,
                        state.CarrierID,
                        state.HasGroup ? state.GroupID : null,
                        state.HyperspaceMilliseconds,
                        state.Hull > 0
                    )
                );
            _replicaBuffer ??= new TacticalCombatReplicaBuffer();
            if (!_replicaBuffer.Add(snapshot.Sequence, snapshot.TimestampMilliseconds, poses))
                throw new ArgumentException(
                    "The combat presentation timestamp or pose is invalid.",
                    nameof(snapshot)
                );
        }
    }
}
