using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tactical
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
        private static readonly Comparer<CombatUnit> _memberComparer = Comparer<CombatUnit>.Create(
            CompareMembers
        );
        private readonly Dictionary<CombatUnit, Member> _members = new();
        private readonly List<CombatUnit> _memberOrder = new();
        private readonly Vector3 _initialHeading;
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
        public IReadOnlyList<CombatUnit> Members { get; }

        /// <summary>
        /// Restores the visible marker without integrating local movement or collision recovery.
        /// </summary>
        /// <param name="state">The validated group projection.</param>
        /// <param name="members">The authoritative applied members in their captured order.</param>
        internal void ApplySnapshot(
            TacticalCombatGroupSnapshot state,
            IReadOnlyList<CombatUnit> members
        )
        {
            Kind = state.Formation;
            Position = state.Position.ToVector();
            Heading = state.Heading.ToVector();
            Velocity = state.Velocity.ToVector();
            _minimumMovementSpeed = state.MovementSpeed;
            _currentCount = state.Members.Length;
            TurnRate = state.TurnRate;
            StableUpdates = state.StableUpdates;
            _memberOrder.Clear();
            _memberOrder.AddRange(members);
        }

        /// <summary>
        /// Creates a formation at its first member's position.
        /// </summary>
        /// <param name="kind">The formation's member layout.</param>
        /// <param name="units">The ordered member roster.</param>
        /// <param name="heading">The side's initial heading.</param>
        public TacticalCombatFormation(
            TacticalCombatFormationKind kind,
            IReadOnlyList<CombatUnit> units,
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
        public void AddMember(CombatUnit unit)
        {
            if (_members.ContainsKey(unit))
                return;
            _currentCount++;
            _maximumCount = Math.Max(_maximumCount, _currentCount);
            _spacingFloor = TacticalCombatFormationMath.UpdateSpacing(
                _spacingFloor,
                unit.CollisionRadius
            );
            if (_currentCount == 1)
                Position = unit.Position;
            _members.Add(unit, new Member { Offset = ComputeOffset(unit) });
            int index = _memberOrder.BinarySearch(unit, _memberComparer);
            _memberOrder.Insert(index < 0 ? ~index : index, unit);
        }

        /// <summary>
        /// Removes a member without reassigning surviving members' offsets.
        /// </summary>
        /// <param name="unit">The departing unit.</param>
        public void RemoveMember(CombatUnit unit)
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
                CombatUnit unit = _memberOrder[index];
                _currentCount++;
                _maximumCount = _currentCount;
                _spacingFloor = TacticalCombatFormationMath.UpdateSpacing(
                    _spacingFloor,
                    unit.CollisionRadius
                );
                _members[unit] = new Member { Offset = ComputeOffset(unit) };
            }
            if (_memberOrder.Count > 0)
                Position = _memberOrder[0].Position;
            _memberOrder.Sort(_memberComparer);
        }

        /// <summary>
        /// Rebuilds a marker with the designated leader first and the remaining registry-ordered roster.
        /// </summary>
        /// <param name="kind">The requested layout.</param>
        /// <param name="leader">The active group's designated leader.</param>
        /// <param name="units">The complete active roster in registry order.</param>
        public void SetKind(
            TacticalCombatFormationKind kind,
            CombatUnit leader,
            IReadOnlyList<CombatUnit> units
        )
        {
            List<CombatUnit> ordered = new(units.Count);
            if (leader != null)
                ordered.Add(leader);
            foreach (CombatUnit unit in units)
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
        private static int CompareMembers(CombatUnit first, CombatUnit second) =>
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
                CombatUnit unit = _memberOrder[index];
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
        public Vector3 GetMemberMovement(CombatUnit unit)
        {
            if (!_members.TryGetValue(unit, out Member member))
                return Vector3.Zero;
            if (Kind != TacticalCombatFormationKind.Direct && member.HasRecovery)
                return member.Recovery;
            return TacticalCombatFormationMath.MemberTarget(
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
                targetHeading = TacticalCombatGeometry.NormalizeTurnDirection(targetHeading);
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
        private Vector3 ComputeOffset(CombatUnit unit)
        {
            if (Kind == TacticalCombatFormationKind.Surround)
                return TacticalCombatFormationMath.SurroundOffset(
                    (uint)_currentCount,
                    ref _surroundScale,
                    12.5f
                );
            return Kind == TacticalCombatFormationKind.Line
                ? TacticalCombatFormationMath.LineOffset(
                    (uint)_currentCount,
                    (uint)_maximumCount,
                    unit.CollisionRadius,
                    _spacingFloor,
                    Heading
                )
                : TacticalCombatFormationMath.DirectOffset((uint)_currentCount, Heading);
        }

        /// <summary>
        /// Rotates every member offset and stored recovery position around the vertical axis.
        /// </summary>
        /// <param name="angle">The signed applied yaw in radians.</param>
        private void RotateOffsets(float angle)
        {
            float cosine = (float)Math.Cos(angle);
            float sine = (float)Math.Sin(angle);
            for (int index = 0; index < _memberOrder.Count; index++)
            {
                Member member = _members[_memberOrder[index]];
                member.Offset = RotateOffset(member.Offset, cosine, sine);
                if (member.HasRecovery)
                    member.Recovery = RotateOffset(member.Recovery, cosine, sine);
            }
        }

        /// <summary>
        /// Applies a stored yaw rotation to one offset while preserving its vertical component.
        /// </summary>
        /// <param name="offset">The offset before rotation.</param>
        /// <param name="cosine">The stored cosine of the yaw.</param>
        /// <param name="sine">The stored sine of the yaw.</param>
        /// <returns>The rotated offset.</returns>
        private static Vector3 RotateOffset(Vector3 offset, float cosine, float sine) =>
            new(
                (float)((double)cosine * offset.X - (double)sine * offset.Z),
                offset.Y,
                (float)((double)sine * offset.X + (double)cosine * offset.Z)
            );

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
