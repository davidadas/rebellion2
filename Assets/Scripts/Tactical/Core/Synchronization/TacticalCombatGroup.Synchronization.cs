using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatGroup
    {
        /// <summary>
        /// Creates a detached movie context after its live fighter group has already been removed.
        /// </summary>
        /// <param name="id">The retained attack-group identity.</param>
        /// <param name="unit">A captured fighter supplying the immutable faction identity.</param>
        /// <param name="session">The local participant view.</param>
        /// <returns>A presentation-only group that does not replace live requested ownership.</returns>
        internal static TacticalCombatGroup CreateSynchronizedMovieGroup(
            int id,
            CombatUnit unit,
            TacticalCombatSession session
        )
        {
            int? requested = unit.RequestedGroupID;
            TacticalCombatGroup group = new(
                id,
                unit,
                session.GetSide(unit.Side).Forward,
                TacticalCombatGroupKind.Fighter
            );
            unit.RequestedGroupID = requested;
            return group;
        }

        /// <summary>
        /// Creates a passive group identity before authoritative requested and applied ownership is restored.
        /// </summary>
        /// <param name="state">The validated group projection.</param>
        /// <param name="session">The participant identity resolver.</param>
        /// <returns>The group belonging to this view's roster.</returns>
        internal static TacticalCombatGroup CreateSnapshotGroup(
            TacticalCombatGroupSnapshot state,
            TacticalCombatSession session
        )
        {
            string first =
                state.Members.FirstOrDefault()
                ?? state.RequestedMembers.FirstOrDefault()
                ?? session.Units.First(unit => unit.Side == state.Side).InstanceID;
            return new(
                state.GroupID,
                session.GetUnit(first),
                session.GetSide(state.Side).Forward,
                state.Kind
            );
        }

        /// <summary>
        /// Captures group lists and marker state without exposing live encounter references.
        /// </summary>
        /// <returns>The group projection.</returns>
        internal TacticalCombatGroupSnapshot CaptureSnapshot() =>
            new()
            {
                GroupID = ID,
                Side = Side,
                Kind = Kind,
                Members = _members.Select(unit => unit.InstanceID).ToArray(),
                RequestedMembers = _requestedMembers.Select(unit => unit.InstanceID).ToArray(),
                PrimaryMemberID = PrimaryMember?.InstanceID,
                RequestedLeaderID = RequestedLeader?.InstanceID,
                Targets = _targets.Select(unit => unit.InstanceID).ToArray(),
                RequestedTargets = _requestedTargets.Select(unit => unit.InstanceID).ToArray(),
                ComputedTargets = _firingTargets.Select(unit => unit.InstanceID).ToArray(),
                Waypoints = _waypoints
                    .Select(point => new TacticalCombatSnapshotVector(point))
                    .ToArray(),
                RequestedWaypoints = _requestedWaypoints
                    .Select(point => new TacticalCombatSnapshotVector(point))
                    .ToArray(),
                Order = Order,
                ActiveOrder = ActiveOrder,
                CloseAttack = CloseAttack,
                ActiveCloseAttack = ActiveCloseAttack,
                Formation = Formation.Kind,
                RequestedFormation = RequestedFormation,
                Position = new(Formation.Position),
                Heading = new(Formation.Heading),
                Velocity = new(Formation.Velocity),
                MovementSpeed = Formation.MovementSpeed,
                TurnRate = Formation.TurnRate,
                StableUpdates = Formation.StableUpdates,
                FighterCategory = FighterCategory,
                HasAttackRun = AttackRun != null,
                AttackRunState = AttackRun?.State ?? default,
                AttackRunMilliseconds = AttackRun?.RemainingMilliseconds ?? 0,
            };

        /// <summary>
        /// Restores a view's group state without queuing orders or changing authoritative ownership.
        /// </summary>
        /// <param name="state">The validated group projection.</param>
        /// <param name="session">The participant identity resolver.</param>
        internal void ApplySnapshot(
            TacticalCombatGroupSnapshot state,
            TacticalCombatSession session
        )
        {
            _members.Clear();
            _members.AddRange(state.Members.Select(session.GetUnit));
            _requestedMembers.Clear();
            _requestedMembers.AddRange(state.RequestedMembers.Select(session.GetUnit));
            PrimaryMember = session.GetUnit(state.PrimaryMemberID);
            RequestedLeader = session.GetUnit(state.RequestedLeaderID);
            _targets.Clear();
            _targets.AddRange(state.Targets.Select(session.GetUnit));
            _requestedTargets.Clear();
            _requestedTargets.AddRange(state.RequestedTargets.Select(session.GetUnit));
            _firingTargets.Clear();
            _firingTargets.AddRange(state.ComputedTargets.Select(session.GetUnit));
            _waypoints.Clear();
            _waypoints.AddRange(state.Waypoints.Select(point => point.ToVector()));
            _requestedWaypoints.Clear();
            _requestedWaypoints.AddRange(
                state.RequestedWaypoints.Select(point => point.ToVector())
            );
            Order = state.Order;
            ActiveOrder = state.ActiveOrder;
            CloseAttack = state.CloseAttack;
            ActiveCloseAttack = state.ActiveCloseAttack;
            RequestedFormation = state.RequestedFormation;
            FighterCategory = state.FighterCategory;
            IsAutomated = session.GetSide(Side).IsAutomated;
            Formation.ApplySnapshot(state, _members);
        }
    }
}
