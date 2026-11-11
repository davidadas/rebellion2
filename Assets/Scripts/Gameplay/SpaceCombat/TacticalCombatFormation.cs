using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Util.Mathematics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies a group's spatial formation.
    /// </summary>
    public enum TacticalCombatFormationKind
    {
        Surround,
        Line,
        Direct,
    }

    /// <summary>
    /// Owns a group's moving reference point, member offsets, and collision recovery vectors.
    /// </summary>
    public sealed class TacticalCombatFormation
    {
        private static readonly Comparer<TacticalCombatUnit> _memberComparer =
            Comparer<TacticalCombatUnit>.Create(CompareMembers);
        private readonly Dictionary<TacticalCombatUnit, Member> _members = new();
        private readonly List<TacticalCombatUnit> _memberOrder = new();
        private readonly Vector3 _initialHeading;
        private Vector3[] _rotationBuffer = Array.Empty<Vector3>();
        private Member[] _rotationMembers = Array.Empty<Member>();
        private Vector3 _previousPosition;
        private Vector3 _lastMovement;
        private float _spacingFloor = 12;
        private float _surroundScale = 12.5f;
        private int _currentCount;
        private int _maximumCount;
        private float _minimumMovementSpeed;
        public TacticalCombatFormationKind Kind { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Heading { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float MovementSpeed => _currentCount == 0 ? 0 : _minimumMovementSpeed;
        public float TurnRate { get; private set; }
        public int StableUpdates { get; private set; }
        public IReadOnlyList<TacticalCombatUnit> Members { get; }

        /// <summary>
        /// Creates a formation at its first member's position.
        /// </summary>
        /// <param name="kind">The formation's member layout.</param>
        /// <param name="units">The ordered member roster.</param>
        /// <param name="heading">The side's initial heading.</param>
        public TacticalCombatFormation(
            TacticalCombatFormationKind kind,
            IReadOnlyList<TacticalCombatUnit> units,
            Vector3 heading
        )
        {
            if (units == null || units.Count == 0)
                throw new ArgumentException(
                    "A formation requires at least one unit.",
                    nameof(units)
                );
            Kind = kind;
            _initialHeading = heading;
            Heading = heading;
            Position = units[0].Position;
            Members = _memberOrder.AsReadOnly();
            for (int index = 0; index < units.Count; index++)
                AddMember(units[index]);
        }

        /// <summary>
        /// Adds a unit and assigns its next formation offset.
        /// </summary>
        /// <param name="unit">The joining unit.</param>
        public void AddMember(TacticalCombatUnit unit)
        {
            if (_members.ContainsKey(unit))
                return;
            _currentCount++;
            _maximumCount = Math.Max(_maximumCount, _currentCount);
            _spacingFloor = NativeVectorMath.UpdateSpacing(_spacingFloor, unit.CollisionRadius);
            if (_currentCount == 1)
                Position = unit.Position;
            _members.Add(unit, new Member { Offset = ComputeOffset(unit) });
            int index = _memberOrder.BinarySearch(unit, _memberComparer);
            _memberOrder.Insert(index < 0 ? ~index : index, unit);
            EnsureRotationCapacity();
        }

        /// <summary>
        /// Removes a member without reassigning surviving members' offsets.
        /// </summary>
        /// <param name="unit">The departing unit.</param>
        public void RemoveMember(TacticalCombatUnit unit)
        {
            if (!_members.Remove(unit))
                return;
            _memberOrder.Remove(unit);
            _currentCount--;
        }

        /// <summary>
        /// Rebuilds the member layout after a formation order.
        /// </summary>
        /// <param name="kind">The requested layout.</param>
        public void SetKind(TacticalCombatFormationKind kind)
        {
            Kind = kind;
            Heading = _initialHeading;
            Velocity = Vector3.Zero;
            _minimumMovementSpeed = 0;
            TurnRate = 0;
            _lastMovement = Vector3.Zero;
            _previousPosition = Vector3.Zero;
            _spacingFloor = 12;
            _surroundScale = 12.5f;
            _maximumCount = 0;
            _currentCount = 0;
            StableUpdates = 0;
            for (int index = 0; index < _memberOrder.Count; index++)
            {
                TacticalCombatUnit unit = _memberOrder[index];
                _currentCount++;
                _maximumCount = _currentCount;
                _spacingFloor = NativeVectorMath.UpdateSpacing(_spacingFloor, unit.CollisionRadius);
                _members[unit] = new Member { Offset = ComputeOffset(unit) };
            }
            if (_memberOrder.Count > 0)
                Position = _memberOrder[0].Position;
            _memberOrder.Sort(_memberComparer);
            EnsureRotationCapacity();
        }

        /// <summary>
        /// Rebuilds a marker with the designated leader first and the remaining registry-ordered roster.
        /// </summary>
        /// <param name="kind">The requested layout.</param>
        /// <param name="leader">The active group's designated leader.</param>
        /// <param name="units">The complete active roster in registry order.</param>
        public void SetKind(
            TacticalCombatFormationKind kind,
            TacticalCombatUnit leader,
            IReadOnlyList<TacticalCombatUnit> units
        )
        {
            List<TacticalCombatUnit> ordered = new(units.Count);
            if (leader != null)
                ordered.Add(leader);
            foreach (TacticalCombatUnit unit in units)
                if (!ReferenceEquals(unit, leader))
                    ordered.Add(unit);
            _memberOrder.Clear();
            _memberOrder.AddRange(ordered);
            _members.Clear();
            SetKind(kind);
        }

        /// <summary>
        /// Orders marker traversal independently of the order used to assign formation offsets.
        /// </summary>
        /// <param name="first">The first marker member.</param>
        /// <param name="second">The second marker member.</param>
        /// <returns>The unsigned battle registration ordering.</returns>
        private static int CompareMembers(TacticalCombatUnit first, TacticalCombatUnit second) =>
            ((uint)first.Index).CompareTo((uint)second.Index);

        /// <summary>
        /// Refreshes movement limits and long-lived collision recovery once per system update.
        /// </summary>
        /// <param name="systemTime">The battle's elapsed whole seconds.</param>
        public void Refresh(float systemTime)
        {
            if (_memberOrder.Count == 0)
                return;
            float movement = float.MaxValue;
            float turn = (float)Math.Sqrt(99d * 99 * 3);
            int eligibleCount = 0;
            for (int index = 0; index < _memberOrder.Count; index++)
            {
                TacticalCombatUnit unit = _memberOrder[index];
                float speed = unit.GetSpeed();
                if (!(speed > 0))
                    continue;
                movement = Math.Min(movement, speed);
                turn = Math.Min(turn, unit.TurnRate);
                eligibleCount++;
                if (unit.IsFighter)
                    continue;
                Member member = _members[unit];
                if (
                    unit.HasCollision
                    && Velocity == Vector3.Zero
                    && StableUpdates >= 5
                    && systemTime - unit.CollisionRecoveryTime > 12
                )
                {
                    unit.CollisionRecoveryTime = systemTime;
                    unit.CollisionRecoveryPhase =
                        (unit.CollisionRecoveryPhase + unit.Index + 1) & 3;
                    member.Recovery = unit.CollisionRecoveryPhase switch
                    {
                        0 => new Vector3(20, 0, 0),
                        1 => new Vector3(-20, 10, 5),
                        2 => new Vector3(5, 0, 20),
                        _ => new Vector3(0, -10, -20),
                    };
                    member.HasRecovery = true;
                }
                else if (!unit.HasCollision && systemTime - unit.CollisionRecoveryTime > 12)
                    member.HasRecovery = false;
            }
            _minimumMovementSpeed = movement == float.MaxValue ? 0 : movement;
            TurnRate = turn;
            _currentCount = eligibleCount;
            StableUpdates = Position == _previousPosition ? StableUpdates + 1 : 0;
            _previousPosition = Position;
        }

        /// <summary>
        /// Calculates the movement requested from one formation member.
        /// </summary>
        /// <param name="unit">The moving member.</param>
        /// <returns>The displacement toward its formation position, or a direct flight velocity.</returns>
        public Vector3 GetMemberMovement(TacticalCombatUnit unit)
        {
            if (!_members.TryGetValue(unit, out Member member))
                return Vector3.Zero;
            if (Kind != TacticalCombatFormationKind.Direct && member.HasRecovery)
                return member.Recovery;
            return NativeVectorMath.MemberTarget(
                Position,
                member.Offset,
                unit.Position,
                Velocity,
                Kind == TacticalCombatFormationKind.Direct
            );
        }

        /// <summary>
        /// Moves the formation reference point toward a navigation or engagement point.
        /// </summary>
        /// <param name="point">The requested point.</param>
        /// <param name="elapsedMilliseconds">The continuous movement interval.</param>
        /// <param name="minimumDistance">The arrival distance.</param>
        /// <param name="targetHeading">The formation's stored normalized requested heading.</param>
        /// <returns>Whether the reference point moved.</returns>
        public bool MoveTowards(
            Vector3 point,
            float elapsedMilliseconds,
            float minimumDistance,
            Vector3 targetHeading
        )
        {
            Vector3 delta = point - Position;
            double liveDistance = TacticalCombatGeometry.MeasureLength(delta);
            float distance = (float)liveDistance;
            double arrivalDistance =
                Kind == TacticalCombatFormationKind.Direct ? liveDistance : distance;
            bool moved = delta != Vector3.Zero && arrivalDistance > minimumDistance;
            if (moved)
            {
                Vector3 direction =
                    Kind == TacticalCombatFormationKind.Direct
                        ? TacticalCombatGeometry.Normalize(delta, distance)
                        : TacticalCombatGeometry.NormalizeWithStoredScale(delta, distance);
                float speed =
                    Kind == TacticalCombatFormationKind.Direct
                        ? MovementSpeed
                        : (float)((double)MovementSpeed * 0.95f);
                Velocity = new Vector3(
                    (float)((double)direction.X * speed),
                    (float)(
                        (double)direction.Y
                        * speed
                        * (Kind == TacticalCombatFormationKind.Direct ? 1 : 0.4f)
                    ),
                    (float)((double)direction.Z * speed)
                );
                Position = TacticalCombatGeometry.Integrate(
                    Position,
                    Velocity,
                    elapsedMilliseconds
                );
                _lastMovement = delta;
            }
            else
                Velocity = Vector3.Zero;
            if (Kind == TacticalCombatFormationKind.Direct)
                return moved;
            if (Kind == TacticalCombatFormationKind.Surround)
            {
                targetHeading = IsZeroOrUnordered(_lastMovement) ? Heading : _lastMovement;
                targetHeading.Y = 0;
                if (IsZeroOrUnordered(targetHeading))
                    targetHeading = _memberOrder.Count > 0 ? _initialHeading : Vector3.UnitZ;
                targetHeading = NativeVectorMath.NormalizeDirection(targetHeading);
            }
            else if (
                (targetHeading.X == 0 || float.IsNaN(targetHeading.X))
                && (targetHeading.Z == 0 || float.IsNaN(targetHeading.Z))
            )
                return moved;
            double dot =
                (double)targetHeading.X * Heading.X
                + ((double)Heading.Y * targetHeading.Y + (double)Heading.Z * targetHeading.Z);
            if (dot < 0 || double.IsNaN(dot))
                Heading =
                    Kind == TacticalCombatFormationKind.Surround
                        ? new Vector3(-Heading.X, Heading.Y, -Heading.Z)
                        : -Heading;
            float angle = TacticalCombatGeometry.GetHeadingAngle(Heading, targetHeading);
            if (
                Kind == TacticalCombatFormationKind.Surround
                || angle != 0 && angle >= (float)((double)3.1415927f * 0.083333336f)
            )
            {
                Heading = TacticalCombatGeometry.TurnTowards(
                    Heading,
                    targetHeading,
                    TurnRate,
                    elapsedMilliseconds,
                    out float turn
                );
                RotateOffsets(turn);
            }
            else if (Velocity == Vector3.Zero)
                Velocity = Vector3.UnitY;
            return moved;
        }

        /// <summary>
        /// Updates the reference point when a fighter group begins circling its target.
        /// </summary>
        /// <param name="position">The orbit reference position.</param>
        public void SetPosition(Vector3 position)
        {
            Position = position;
        }

        /// <summary>
        /// Stops movement of the reference point while preserving its heading.
        /// </summary>
        public void Stop()
        {
            Velocity = Vector3.Zero;
        }

        /// <summary>
        /// Assigns the next member's offset for the active layout.
        /// </summary>
        /// <param name="unit">The joining unit.</param>
        /// <returns>The member's offset from the reference point.</returns>
        private Vector3 ComputeOffset(TacticalCombatUnit unit)
        {
            if (Kind == TacticalCombatFormationKind.Surround)
                return NativeVectorMath.SurroundOffset(
                    (uint)_currentCount,
                    ref _surroundScale,
                    12.5f
                );
            return Kind == TacticalCombatFormationKind.Line
                ? NativeVectorMath.LineOffset(
                    (uint)_currentCount,
                    (uint)_maximumCount,
                    unit.CollisionRadius,
                    _spacingFloor,
                    Heading
                )
                : NativeVectorMath.DirectOffset((uint)_currentCount, Heading);
        }

        /// <summary>
        /// Reserves reusable rotation storage when the member roster grows.
        /// </summary>
        private void EnsureRotationCapacity()
        {
            int required = _memberOrder.Count;
            if (_rotationMembers.Length >= required)
                return;
            int capacity = Math.Max(required, Math.Max(4, _rotationMembers.Length * 2));
            Array.Resize(ref _rotationMembers, capacity);
            Array.Resize(ref _rotationBuffer, checked(capacity * 2));
        }

        /// <summary>
        /// Rotates member and recovery offsets in a single native call without per-frame allocation.
        /// </summary>
        /// <param name="angle">The signed applied yaw in radians.</param>
        private void RotateOffsets(float angle)
        {
            int count = 0;
            for (int index = 0; index < _memberOrder.Count; index++)
            {
                Member member = _members[_memberOrder[index]];
                _rotationMembers[index] = member;
                _rotationBuffer[count++] = member.Offset;
                if (member.HasRecovery)
                    _rotationBuffer[count++] = member.Recovery;
            }
            NativeVectorMath.RotateOffsets(_rotationBuffer, count, angle);
            count = 0;
            for (int index = 0; index < _memberOrder.Count; index++)
            {
                Member member = _rotationMembers[index];
                member.Offset = _rotationBuffer[count++];
                if (member.HasRecovery)
                    member.Recovery = _rotationBuffer[count++];
                _rotationMembers[index] = null;
            }
        }

        /// <summary>
        /// Tests whether all components are zero or unordered.
        /// </summary>
        /// <param name="value">The vector to classify.</param>
        /// <returns>Whether no component is ordered and nonzero.</returns>
        private static bool IsZeroOrUnordered(Vector3 value) =>
            (value.X == 0 || float.IsNaN(value.X))
            && (value.Y == 0 || float.IsNaN(value.Y))
            && (value.Z == 0 || float.IsNaN(value.Z));

        /// <summary>
        /// Stores a member's persistent formation offset and temporary collision recovery.
        /// </summary>
        private sealed class Member
        {
            public Vector3 Offset;
            public Vector3 Recovery;
            public bool HasRecovery;
        }
    }
}
