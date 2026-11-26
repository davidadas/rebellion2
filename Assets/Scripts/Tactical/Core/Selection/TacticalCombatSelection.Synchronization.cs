using System.Linq;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSelection
    {
        /// <summary>
        /// Routes a semantic input to the battle authority before it can mutate a projected view.
        /// </summary>
        /// <param name="command">The faction-owned semantic input.</param>
        /// <returns>Whether the synchronization boundary owns this input.</returns>
        private bool RequestSynchronization(TacticalCombatCommand command) =>
            _session.RequestSynchronizedCommand(command);

        /// <summary>
        /// Refreshes roster-dependent context before capture without draining input or updating playback.
        /// </summary>
        internal void RefreshSynchronizedContext() => Refresh();

        /// <summary>
        /// Captures this faction's independent command context for its view.
        /// </summary>
        /// <returns>The selection projection.</returns>
        internal TacticalCombatSelectionSnapshot CaptureSnapshot() =>
            new()
            {
                Side = _side,
                Kind = Kind,
                HasGroup = Group != null,
                GroupID = Group?.ID ?? 0,
                Category = Category,
                HasWaypoint = PrimaryWaypoint.HasValue,
                PrimaryWaypoint = new(PrimaryWaypoint ?? default),
                PrimaryID = _primary?.InstanceID,
                InspectedID = _inspected?.InstanceID,
                Members = _members.Select(unit => unit.InstanceID).ToArray(),
                Targets = _targets.Select(unit => unit.InstanceID).ToArray(),
                Waypoints = _waypoints
                    .Select(point => new TacticalCombatSnapshotVector(point))
                    .ToArray(),
                Groups = _groups.Select(group => group.ID).ToArray(),
            };

        /// <summary>
        /// Restores the authoritative context without issuing gameplay input or changing the camera.
        /// </summary>
        /// <param name="state">This faction's validated command context.</param>
        internal void ApplySnapshot(TacticalCombatSelectionSnapshot state)
        {
            TacticalCombatSelectionKind previous = Kind;
            bool changed =
                previous != state.Kind
                || Group?.ID != (state.HasGroup ? state.GroupID : null)
                || Category != state.Category
                || _primary?.InstanceID != state.PrimaryID
                || _inspected?.InstanceID != state.InspectedID
                || PrimaryWaypoint != (state.HasWaypoint ? state.PrimaryWaypoint.ToVector() : null);
            if (changed)
                StopNotification();
            Kind = state.Kind;
            Group = state.HasGroup ? _session.GetSynchronizedGroup(state.GroupID) : null;
            Category = state.Category;
            PrimaryWaypoint = state.HasWaypoint ? state.PrimaryWaypoint.ToVector() : null;
            _primary = _session.GetUnit(state.PrimaryID);
            _inspected = _session.GetUnit(state.InspectedID);
            _members.Clear();
            _members.AddRange(state.Members.Select(_session.GetUnit));
            _highlights.Clear();
            _targets.Clear();
            _targets.AddRange(state.Targets.Select(_session.GetUnit));
            _waypoints.Clear();
            _waypoints.AddRange(state.Waypoints.Select(point => point.ToVector()));
            _groups.Clear();
            _groups.AddRange(state.Groups.Select(_session.GetSynchronizedGroup));
            BuildHighlights();
            if (changed)
                ContextReplaced?.Invoke(Kind);
            if (previous == TacticalCombatSelectionKind.StationTarget && previous != Kind)
                StationControlsClosed?.Invoke();
        }

        /// <summary>
        /// Announces this view's authoritative withdrawal transition after its state has been restored.
        /// </summary>
        internal void ApplySynchronizedWithdrawal()
        {
            WithdrawalStarting?.Invoke();
            StationControlsClosed?.Invoke();
            WithdrawalDispatched?.Invoke();
        }
    }
}
