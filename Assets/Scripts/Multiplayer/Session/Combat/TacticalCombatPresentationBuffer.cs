using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Tactical;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Samples bounded, delayed participant poses without changing authoritative combat state.
    /// </summary>
    internal sealed class TacticalCombatPresentationBuffer
    {
        private readonly double _delayMilliseconds;
        private readonly int _capacity;
        private readonly double _maximumGapMilliseconds;
        private readonly List<Snapshot> _snapshots = new();
        private long _lastSequence = -1;
        private double _clockMilliseconds;
        private Snapshot _before;
        private Snapshot _after;
        private float _blend;

        public int BufferedFrameCount => _snapshots.Count;
        public double TimestampMilliseconds { get; private set; }

        /// <summary>
        /// Creates the presentation history and its latency and discontinuity bounds.
        /// </summary>
        /// <param name="delayMilliseconds">The desired delay behind the received authority clock.</param>
        /// <param name="capacity">The maximum number of complete snapshots retained.</param>
        /// <param name="maximumGapMilliseconds">The largest gap across which poses may interpolate.</param>
        public TacticalCombatPresentationBuffer(
            double delayMilliseconds = 100,
            int capacity = 32,
            double maximumGapMilliseconds = 250
        )
        {
            if (!IsFinite(delayMilliseconds) || delayMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));
            if (capacity < 2)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!IsFinite(maximumGapMilliseconds) || maximumGapMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumGapMilliseconds));
            _delayMilliseconds = delayMilliseconds;
            _capacity = capacity;
            _maximumGapMilliseconds = maximumGapMilliseconds;
        }

        /// <summary>
        /// Copies a complete pose snapshot, rebasing at clock discontinuities and ignoring stale data.
        /// </summary>
        /// <param name="sequence">The complete state sequence that owns these poses.</param>
        /// <param name="timestampMilliseconds">The monotonic authority presentation timestamp.</param>
        /// <param name="poses">The complete participant poses at that timestamp.</param>
        /// <returns>Whether the snapshot was accepted without modifying an existing snapshot.</returns>
        public bool Add(
            long sequence,
            double timestampMilliseconds,
            IReadOnlyDictionary<string, Pose> poses
        )
        {
            if (
                sequence <= _lastSequence
                || !IsFinite(timestampMilliseconds)
                || timestampMilliseconds < 0
                || poses == null
            )
                return false;
            foreach (KeyValuePair<string, Pose> pair in poses)
                if (
                    string.IsNullOrEmpty(pair.Key)
                    || !IsFinite(pair.Value.Position)
                    || !IsFinite(pair.Value.Heading)
                    || !IsFinite(pair.Value.HyperspaceMilliseconds)
                )
                    return false;

            Snapshot previous = _snapshots.Count > 0 ? _snapshots[_snapshots.Count - 1] : null;
            bool rebase =
                previous == null
                || timestampMilliseconds <= previous.TimestampMilliseconds
                || timestampMilliseconds - previous.TimestampMilliseconds > _maximumGapMilliseconds;
            Dictionary<string, Pose> entries = new(poses.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, Pose> pair in poses)
                entries.Add(pair.Key, pair.Value);
            if (rebase)
            {
                _snapshots.Clear();
                _clockMilliseconds = timestampMilliseconds;
            }
            else
                _clockMilliseconds = Math.Max(_clockMilliseconds, timestampMilliseconds);
            _snapshots.Add(new Snapshot(timestampMilliseconds, entries));
            if (_snapshots.Count > _capacity)
                _snapshots.RemoveAt(0);
            _lastSequence = sequence;
            SelectBracket();
            return true;
        }

        /// <summary>
        /// Advances only the local presentation clock and drains the delay when packets stop arriving.
        /// </summary>
        /// <param name="elapsedMilliseconds">The finite, nonnegative elapsed local view time.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (_snapshots.Count == 0 || !IsFinite(elapsedMilliseconds) || elapsedMilliseconds <= 0)
                return;
            double latest = _snapshots[_snapshots.Count - 1].TimestampMilliseconds;
            _clockMilliseconds = Math.Min(
                _clockMilliseconds + elapsedMilliseconds,
                latest + _delayMilliseconds
            );
            SelectBracket();
        }

        /// <summary>
        /// Samples one participant in constant time from the current shared interpolation bracket.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="pose">The sampled pose, or an empty value when the identity is unavailable.</param>
        /// <returns>Whether the history contains the participant.</returns>
        public bool TryGetPose(string unitID, out Pose pose)
        {
            pose = default;
            if (
                _snapshots.Count == 0
                || unitID == null
                || !_before.Entries.TryGetValue(unitID, out Pose before)
            )
                return false;
            pose = before;
            if (
                !before.IsAlive
                || before.Status
                    is not (
                        TacticalCombatUnitStatus.Active
                        or TacticalCombatUnitStatus.PreparingHyperspace
                        or TacticalCombatUnitStatus.EnteringHyperspace
                    )
            )
                return true;

            if (_after.Entries.TryGetValue(unitID, out Pose after) && after.Continues(before))
                pose = Pose.Interpolate(before, after, _blend);
            return true;
        }

        /// <summary>
        /// Resolves one bounded timestamp bracket for all participant queries in the next view frame.
        /// </summary>
        private void SelectBracket()
        {
            Snapshot first = _snapshots[0];
            Snapshot latest = _snapshots[_snapshots.Count - 1];
            TimestampMilliseconds = Math.Max(
                first.TimestampMilliseconds,
                Math.Min(latest.TimestampMilliseconds, _clockMilliseconds - _delayMilliseconds)
            );
            _before = first;
            _after = first;
            _blend = 0;
            foreach (Snapshot snapshot in _snapshots)
            {
                if (snapshot.TimestampMilliseconds <= TimestampMilliseconds)
                {
                    _before = snapshot;
                    _after = snapshot;
                    continue;
                }
                _after = snapshot;
                _blend = (float)(
                    (TimestampMilliseconds - _before.TimestampMilliseconds)
                    / (_after.TimestampMilliseconds - _before.TimestampMilliseconds)
                );
                break;
            }
        }

        /// <summary>
        /// Checks whether a scalar can participate in a finite presentation calculation.
        /// </summary>
        /// <param name="value">The scalar to inspect.</param>
        /// <returns>Whether it is neither infinite nor undefined.</returns>
        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// Checks all components of a pose vector before admitting it to the history.
        /// </summary>
        /// <param name="value">The vector to inspect.</param>
        /// <returns>Whether all three components are finite.</returns>
        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);

        /// <summary>
        /// Stores only pose and continuity data; no gameplay objects or mutable systems are retained.
        /// </summary>
        public readonly struct Pose
        {
            public readonly Vector3 Position;
            public readonly Vector3 Heading;
            public readonly TacticalCombatUnitStatus Status;
            public readonly string CarrierID;
            public readonly int? GroupID;
            public readonly float HyperspaceMilliseconds;
            public readonly bool IsAlive;

            /// <summary>
            /// Captures one participant's pose and the boundaries that invalidate interpolation.
            /// </summary>
            /// <param name="position">The canonical encounter position.</param>
            /// <param name="heading">The canonical encounter heading.</param>
            /// <param name="status">The current visibility and movement lifecycle.</param>
            /// <param name="carrierID">The owning carrier identity, if any.</param>
            /// <param name="groupID">The applied group identity, if any.</param>
            /// <param name="hyperspaceMilliseconds">The departure animation's elapsed time.</param>
            /// <param name="isAlive">Whether the participant's hull is alive at this timestamp.</param>
            public Pose(
                Vector3 position,
                Vector3 heading,
                TacticalCombatUnitStatus status = TacticalCombatUnitStatus.Active,
                string carrierID = null,
                int? groupID = null,
                float hyperspaceMilliseconds = 0,
                bool isAlive = true
            )
            {
                Position = position;
                Heading = heading;
                Status = status;
                CarrierID = string.IsNullOrEmpty(carrierID) ? null : carrierID;
                GroupID = groupID;
                HyperspaceMilliseconds = hyperspaceMilliseconds;
                IsAlive = isAlive;
            }

            /// <summary>
            /// Tests whether the previous pose belongs to this same continuous lifecycle segment.
            /// </summary>
            /// <param name="previous">The preceding received pose.</param>
            /// <returns>Whether interpolation may cross the snapshot boundary.</returns>
            public bool Continues(Pose previous) =>
                Status == previous.Status
                && IsAlive == previous.IsAlive
                && string.Equals(CarrierID, previous.CarrierID, StringComparison.Ordinal)
                && GroupID == previous.GroupID
                && (
                    Status != TacticalCombatUnitStatus.EnteringHyperspace
                    || HyperspaceMilliseconds >= previous.HyperspaceMilliseconds
                );

            /// <summary>
            /// Blends continuous motion, retaining an authored heading when opposite directions cancel.
            /// </summary>
            /// <param name="before">The pose at the beginning of the bracket.</param>
            /// <param name="after">The pose at the end of the bracket.</param>
            /// <param name="blend">The bounded fractional progress through the bracket.</param>
            /// <returns>The finite interpolated pose.</returns>
            public static Pose Interpolate(Pose before, Pose after, float blend)
            {
                if (blend <= 0)
                    return before;
                if (blend >= 1)
                    return after;
                Vector3 position = before.Position * (1 - blend) + after.Position * blend;
                Vector3 heading = Normalize(
                    Normalize(before.Heading) * (1 - blend) + Normalize(after.Heading) * blend
                );
                if (heading == Vector3.Zero)
                    heading = after.Heading;
                return new Pose(
                    position,
                    heading,
                    after.Status,
                    after.CarrierID,
                    after.GroupID,
                    before.HyperspaceMilliseconds * (1 - blend)
                        + after.HyperspaceMilliseconds * blend,
                    after.IsAlive
                );
            }

            /// <summary>
            /// Normalizes finite headings without overflowing on large received components.
            /// </summary>
            /// <param name="value">The received or blended heading.</param>
            /// <returns>A unit direction, or zero when no direction exists.</returns>
            private static Vector3 Normalize(Vector3 value)
            {
                float scale = Math.Max(
                    Math.Abs(value.X),
                    Math.Max(Math.Abs(value.Y), Math.Abs(value.Z))
                );
                if (scale == 0)
                    return Vector3.Zero;
                return Vector3.Normalize(value / scale);
            }
        }

        private sealed class Snapshot
        {
            public readonly double TimestampMilliseconds;
            public readonly Dictionary<string, Pose> Entries;

            /// <summary>
            /// Owns the detached complete set of poses at one authority timestamp.
            /// </summary>
            /// <param name="timestampMilliseconds">The captured authority timestamp.</param>
            /// <param name="entries">The detached pose entries.</param>
            public Snapshot(double timestampMilliseconds, Dictionary<string, Pose> entries)
            {
                TimestampMilliseconds = timestampMilliseconds;
                Entries = entries;
            }
        }
    }
}
