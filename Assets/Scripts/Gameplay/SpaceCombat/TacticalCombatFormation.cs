using System;
using System.Collections.Generic;
using System.Numerics;

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
        private Vector3 _previousPosition;
        private Vector3 _lastMovement;
        private float _spacingFloor = 12;
        private float _surroundScale = 12.5f;
        private int _currentCount;
        private int _maximumCount;
        public TacticalCombatFormationKind Kind { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Heading { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float MovementSpeed { get; private set; }
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
            _spacingFloor = Math.Max(_spacingFloor, (float)((double)unit.CollisionRadius * 1.2f));
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
            MovementSpeed = 0;
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
                _spacingFloor = Math.Max(
                    _spacingFloor,
                    (float)((double)unit.CollisionRadius * 1.2f)
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
            float movement = float.MaxValue;
            float turn = (float)Math.Sqrt(99d * 99 * 3);
            int eligibleCount = 0;
            for (int index = 0; index < _memberOrder.Count; index++)
            {
                TacticalCombatUnit unit = _memberOrder[index];
                float speed = unit.GetSpeed();
                if (speed <= 0)
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
            MovementSpeed = movement == float.MaxValue ? 0 : movement;
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
            Vector3 delta = new(
                (float)((double)Position.X - member.Offset.X - unit.Position.X),
                (float)((double)Position.Y - member.Offset.Y - unit.Position.Y),
                (float)((double)Position.Z - member.Offset.Z - unit.Position.Z)
            );
            double squared =
                (double)delta.X * delta.X + (double)delta.Z * delta.Z + (double)delta.Y * delta.Y;
            if (Kind == TacticalCombatFormationKind.Direct)
                return squared > 4 ? delta : Velocity;
            return squared > 1 ? delta : Vector3.Zero;
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
                targetHeading = _lastMovement == Vector3.Zero ? Heading : _lastMovement;
                targetHeading.Y = 0;
                if (targetHeading == Vector3.Zero && _memberOrder.Count > 0)
                    targetHeading = _initialHeading;
                targetHeading = TacticalCombatGeometry.Normalize(targetHeading);
            }
            else if (targetHeading.X == 0 && targetHeading.Z == 0)
                return moved;
            if (Vector3.Dot(targetHeading, Heading) < 0)
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
                return ComputeSurroundOffset();
            if (_currentCount == 1)
                return Vector3.Zero;
            int multiplier = (_currentCount & 1) != 0 ? 1 : -1;
            if (Kind == TacticalCombatFormationKind.Line)
            {
                double spacing = Math.Max((double)unit.CollisionRadius * 1.2f, _spacingFloor);
                int scaled =
                    (int)(multiplier * spacing) * (Math.Max(_maximumCount, _currentCount) >> 1);
                return new Vector3(
                    (float)(-(double)Heading.Z * scaled),
                    0,
                    (float)((double)Heading.X * scaled)
                );
            }
            int spacingIndex = (int)(multiplier * 3d * -1.5d);
            int angleIndex = 1 - spacingIndex * (_currentCount >> 1);
            double baseAngle = (double)angleIndex * 3.1415927f;
            double angleA = baseAngle * 0.6666667f;
            double cosineA = Math.Cos(angleA);
            float sineA = (float)Math.Sin((float)angleA);
            double angleB = baseAngle * 0.5f;
            double cosineB = Math.Cos(angleB);
            double sineB = Math.Sin((float)angleB);
            double angleC = baseAngle * 0.4f;
            float cosineC = (float)Math.Cos(angleC);
            float sineC = (float)Math.Sin((float)angleC);
            float rotatedZX = (float)(cosineA * Heading.Z + sineA * (double)Heading.X);
            float rotatedXZ = (float)(
                (float)cosineA * (double)Heading.X - sineA * (double)Heading.Z
            );
            float rotatedYZ = (float)(sineB * rotatedZX + cosineB * Heading.Y);
            return new Vector3(
                (float)((double)cosineC * rotatedYZ + (double)sineC * rotatedXZ),
                (float)((double)cosineC * rotatedXZ - (double)sineC * rotatedYZ),
                (float)((float)cosineB * (double)rotatedZX - (float)sineB * (double)Heading.Y)
            );
        }

        /// <summary>
        /// Assigns a member to the next direction and shell of a surrounding formation.
        /// </summary>
        /// <returns>The surrounding offset.</returns>
        private Vector3 ComputeSurroundOffset()
        {
            float two = (float)(_surroundScale / Math.Sqrt(2));
            float three = (float)(_surroundScale / Math.Sqrt(3));
            Vector3 direction = (_currentCount % 22) switch
            {
                0 => new Vector3(two, -two, 0),
                1 => new Vector3(0, 0, _surroundScale),
                2 => new Vector3(0, 0, -_surroundScale),
                3 => new Vector3(_surroundScale, 0, 0),
                4 => new Vector3(-_surroundScale, 0, 0),
                5 => new Vector3(0, _surroundScale, 0),
                6 => new Vector3(0, -_surroundScale, 0),
                7 => new Vector3(three, three, three),
                8 => new Vector3(-three, -three, -three),
                9 => new Vector3(-three, three, three),
                10 => new Vector3(three, -three, -three),
                11 => new Vector3(three, -three, three),
                12 => new Vector3(-three, three, -three),
                13 => new Vector3(-three, -three, three),
                14 => new Vector3(three, three, -three),
                15 or 19 => new Vector3(0, -two, -two),
                16 => new Vector3(0, -two, two),
                17 => new Vector3(-two, two, 0),
                18 => new Vector3(two, two, 0),
                20 => new Vector3(0, two, two),
                _ => new Vector3(-two, -two, 0),
            };
            direction = TacticalCombatGeometry.Normalize(direction);
            double distance = ((_currentCount / 22) * 0.25d + 1) * 12.5f;
            _surroundScale = (float)distance;
            return new Vector3(
                (float)(distance * direction.X),
                (float)(distance * direction.Y),
                (float)(distance * direction.Z)
            );
        }

        /// <summary>
        /// Rotates stored member and recovery offsets after the reference heading turns.
        /// </summary>
        /// <param name="angle">The applied signed yaw in radians.</param>
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
        /// Rotates an offset while preserving its vertical component.
        /// </summary>
        /// <param name="offset">The existing offset.</param>
        /// <param name="cosine">The rounded rotation cosine.</param>
        /// <param name="sine">The rounded rotation sine.</param>
        /// <returns>The rotated offset.</returns>
        private static Vector3 RotateOffset(Vector3 offset, float cosine, float sine)
        {
            return new Vector3(
                (float)((double)cosine * offset.X - (double)sine * offset.Z),
                offset.Y,
                (float)((double)sine * offset.X + (double)cosine * offset.Z)
            );
        }

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
