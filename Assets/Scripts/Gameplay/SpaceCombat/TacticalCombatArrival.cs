using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Presents arrival effects without changing a unit's simulation position.
    /// </summary>
    public interface ITacticalCombatArrivalPresentation
    {
        /// <summary>
        /// Changes the run/pause control's arrival strobe.
        /// </summary>
        /// <param name="active">Whether arrival is being presented.</param>
        void SetPauseStrobe(bool active);

        /// <summary>
        /// Resets a unit's presentation effect before its first arrival transform.
        /// </summary>
        /// <param name="unitIndex">The battle registration identifier.</param>
        void Reset(int unitIndex);

        /// <summary>
        /// Applies one presentation transform.
        /// </summary>
        /// <param name="unitIndex">The battle registration identifier.</param>
        /// <param name="position">The presentation position.</param>
        /// <param name="direction">The presentation direction.</param>
        void SetTransform(int unitIndex, Vector4 position, Vector4 direction);

        /// <summary>
        /// Refreshes a unit's presentation visibility after a transform.
        /// </summary>
        /// <param name="unitIndex">The battle registration identifier.</param>
        void Refresh(int unitIndex);
    }

    /// <summary>
    /// Captures the explicit presentation inputs of one ordered arrival member.
    /// </summary>
    public readonly struct TacticalCombatArrivalMember
    {
        public int UnitIndex { get; }
        public int PresentationOrdinal { get; }
        public CombatSide Side { get; }
        public bool IsFighter { get; }
        public bool IsDocked { get; }
        public Vector4 Position { get; }
        public Vector4 Direction { get; }

        /// <summary>
        /// Captures a unit's registration, authored ordinal, and unmodified transform.
        /// </summary>
        /// <param name="unitIndex">The battle registration identifier.</param>
        /// <param name="presentationOrdinal">The authored presentation type ordinal.</param>
        /// <param name="side">The encounter side containing this member.</param>
        /// <param name="isFighter">Whether this member uses fighter arrival audio.</param>
        /// <param name="isDocked">Whether this member remains aboard a carrier.</param>
        /// <param name="position">The unmodified simulation position.</param>
        /// <param name="direction">The unmodified presentation direction.</param>
        public TacticalCombatArrivalMember(
            int unitIndex,
            int presentationOrdinal,
            CombatSide side,
            bool isFighter,
            bool isDocked,
            Vector4 position,
            Vector4 direction
        )
        {
            UnitIndex = unitIndex;
            PresentationOrdinal = presentationOrdinal;
            Side = side;
            IsFighter = isFighter;
            IsDocked = isDocked;
            Position = position;
            Direction = direction;
        }
    }

    /// <summary>
    /// Applies the entering force's ordered fly-in and arrival sounds on presentation time.
    /// </summary>
    public sealed class TacticalCombatArrival
    {
        private static readonly float[] _distances = { 400, 575, 650, 325 };
        private static readonly float[] _coefficients =
        {
            CalculateCoefficient(400, 0),
            CalculateCoefficient(575, 250),
            CalculateCoefficient(650, 75),
            CalculateCoefficient(325, 275),
        };

        private readonly CombatSide _enteringSide;
        private readonly IReadOnlyList<TacticalCombatArrivalMember> _members;
        private readonly Vector3 _lane;
        private readonly ITacticalCombatArrivalPresentation _presentation;
        private readonly Action<int> _requestNotification;
        private bool _initialized;

        public float ElapsedMilliseconds { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Captures an explicitly selected entering side and its ordered presentation members.
        /// </summary>
        /// <param name="enteringSide">The side captured by strategic presence transitions.</param>
        /// <param name="members">Members in display-group order, then group member order.</param>
        /// <param name="lane">The entering side's authored display lane.</param>
        /// <param name="presentation">The owner of the affected rendering objects.</param>
        /// <param name="requestNotification">Requests a one-shot authored notification.</param>
        public TacticalCombatArrival(
            CombatSide enteringSide,
            IReadOnlyList<TacticalCombatArrivalMember> members,
            Vector3 lane,
            ITacticalCombatArrivalPresentation presentation,
            Action<int> requestNotification
        )
        {
            if (enteringSide is not CombatSide.Attacker and not CombatSide.Defender)
                throw new ArgumentOutOfRangeException(nameof(enteringSide));
            _enteringSide = enteringSide;
            _members = members ?? throw new ArgumentNullException(nameof(members));
            _lane = lane;
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _requestNotification =
                requestNotification ?? throw new ArgumentNullException(nameof(requestNotification));
        }

        /// <summary>
        /// Presents one unscaled frame, including the final snap before reporting completion.
        /// The caller starts simulation clocks only after this method returns true.
        /// </summary>
        /// <param name="elapsedMilliseconds">The frame's unscaled presentation time.</param>
        /// <returns>Whether the complete arrival interval has elapsed.</returns>
        public bool Advance(float elapsedMilliseconds)
        {
            float delta = elapsedMilliseconds * 0.4f;
            if (delta > 200)
                delta = 200;
            _presentation.SetPauseStrobe(true);
            if (!_initialized)
                Initialize();

            for (int index = 0; index < _members.Count; index++)
            {
                TacticalCombatArrivalMember member = _members[index];
                if (!Includes(member))
                    continue;
                if (ElapsedMilliseconds == 0 || float.IsNaN(ElapsedMilliseconds))
                    _requestNotification(member.IsFighter ? 28 : 30);
                Present(member, GetAnimatedPosition(member, delta));
            }

            double elapsed = (double)ElapsedMilliseconds + delta;
            ElapsedMilliseconds = (float)elapsed;
            if (elapsed >= 1500)
            {
                for (int index = 0; index < _members.Count; index++)
                {
                    TacticalCombatArrivalMember member = _members[index];
                    if (Includes(member))
                        Present(member, member.Position);
                }
                IsComplete = true;
                _presentation.SetPauseStrobe(false);
            }
            return IsComplete;
        }

        /// <summary>
        /// Resets eligible members and carries the most recently selected initial displacement.
        /// </summary>
        private void Initialize()
        {
            _initialized = true;
            ElapsedMilliseconds = 0;
            Vector3 offset = Vector3.Zero;
            for (int index = 0; index < _members.Count; index++)
            {
                TacticalCombatArrivalMember member = _members[index];
                if (!Includes(member))
                    continue;
                int selector = GetSelector(member);
                if (selector < 3)
                    offset = _lane * -_distances[selector];
                Vector4 position = member.Position;
                position.X += offset.X;
                position.Y += offset.Y;
                position.Z += offset.Z;
                position.W = -1;
                _presentation.Reset(member.UnitIndex);
                Present(member, position);
            }
        }

        /// <summary>
        /// Computes the accelerated z transform without moving the simulation object.
        /// </summary>
        /// <param name="member">The member being presented.</param>
        /// <param name="delta">The already scaled and upper-clamped frame interval.</param>
        /// <returns>The interpolated presentation position.</returns>
        private Vector4 GetAnimatedPosition(TacticalCombatArrivalMember member, float delta)
        {
            double elapsed = (double)ElapsedMilliseconds + delta;
            if (elapsed > 1500)
                elapsed = 1500;
            float seconds = (float)(elapsed * (double)0.001f);
            int selector = GetSelector(member);
            float distance = (float)(
                (double)seconds * seconds * _coefficients[selector] - _distances[selector]
            );
            float displacement = _lane.Z * distance;
            double z = (double)member.Position.Z + displacement;
            if (!(_lane.Z > 0))
            {
                if (!(z > member.Position.Z))
                    z = member.Position.Z;
            }
            else if (member.Position.Z <= z)
                z = member.Position.Z;
            return new Vector4(member.Position.X, member.Position.Y, (float)z, -1);
        }

        /// <summary>
        /// Applies the transform before refreshing presentation state.
        /// </summary>
        /// <param name="member">The ordered member.</param>
        /// <param name="position">The transform to apply.</param>
        private void Present(TacticalCombatArrivalMember member, Vector4 position)
        {
            _presentation.SetTransform(member.UnitIndex, position, member.Direction);
            _presentation.Refresh(member.UnitIndex);
        }

        /// <summary>
        /// Determines whether this member belongs to the entering force and is not docked.
        /// </summary>
        /// <param name="member">The candidate member.</param>
        /// <returns>Whether this member participates in arrival.</returns>
        private bool Includes(TacticalCombatArrivalMember member) =>
            member.Side == _enteringSide && !member.IsDocked;

        /// <summary>
        /// Combines battle registration and authored presentation ordinals.
        /// </summary>
        /// <param name="member">The member whose arrival staggering is required.</param>
        /// <returns>The arrival displacement selector.</returns>
        private static int GetSelector(TacticalCombatArrivalMember member) =>
            unchecked(member.UnitIndex + member.PresentationOrdinal) & 3;

        /// <summary>
        /// Computes one rounded acceleration coefficient from the stored time-scale operand.
        /// </summary>
        /// <param name="distance">The initial displacement magnitude.</param>
        /// <param name="offsetMilliseconds">The staggering offset within the arrival interval.</param>
        /// <returns>The acceleration coefficient.</returns>
        private static float CalculateCoefficient(float distance, float offsetMilliseconds)
        {
            double seconds = (1500.0 - offsetMilliseconds) * (double)0.001f;
            return (float)(distance / (seconds * seconds));
        }
    }
}
