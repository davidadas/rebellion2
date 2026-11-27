using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Combat;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSelection
    {
        /// <summary>
        /// Captures the settled input context while the actual orders remain owned by the battle.
        /// </summary>
        /// <returns>The saved selection.</returns>
        internal ActiveBattle.SelectionState CaptureState() =>
            new()
            {
                Kind = Kind,
                GroupID = Group?.ID,
                Category = Category,
                PrimaryWaypoint = PrimaryWaypoint,
                PrimaryID = _primary?.InstanceID,
                InspectedID = _inspected?.InstanceID,
                NotificationID = _notification,
                Members = _members.ConvertAll(unit => unit.InstanceID),
                Targets = _targets.ConvertAll(unit => unit.InstanceID),
                Waypoints = new(_waypoints),
                Groups = _groups.ConvertAll(group => group.ID),
            };

        /// <summary>
        /// Rebinds the selected context without issuing new input or restarting its notification.
        /// </summary>
        /// <param name="state">The saved selection.</param>
        /// <param name="getGroup">Resolves registered groups in the loaded encounter.</param>
        internal void RestoreState(
            ActiveBattle.SelectionState state,
            Func<int, TacticalCombatGroup> getGroup
        )
        {
            Kind = state.Kind;
            Group = state.GroupID is int groupID ? getGroup(groupID) : null;
            Category = state.Category;
            PrimaryWaypoint = state.PrimaryWaypoint;
            _primary = _session.GetUnit(state.PrimaryID);
            _inspected = _session.GetUnit(state.InspectedID);
            _notification = state.NotificationID;
            _members.AddRange(state.Members.Select(_session.GetUnit));
            _targets.AddRange(state.Targets.Select(_session.GetUnit));
            _waypoints.AddRange(state.Waypoints);
            _groups.AddRange(state.Groups.Select(getGroup));
        }

        /// <summary>
        /// Resolves contextual nearest-object selection, preferring capital objects before fighters.
        /// </summary>
        private void SelectNearestTarget()
        {
            if (
                Kind
                is not (
                    TacticalCombatSelectionKind.SingleObject
                    or TacticalCombatSelectionKind.MultipleObjects
                    or TacticalCombatSelectionKind.ObjectGroup
                    or TacticalCombatSelectionKind.FighterGroup
                    or TacticalCombatSelectionKind.StationTarget
                )
            )
                return;
            Vector3 origin = Vector3.Zero;
            IReadOnlyList<CombatUnit> members = Kind
                is TacticalCombatSelectionKind.ObjectGroup
                    or TacticalCombatSelectionKind.FighterGroup
                ? Group.RequestedMembers
                : _members;
            if (Kind != TacticalCombatSelectionKind.StationTarget)
            {
                if (members.Count == 0)
                    return;
                foreach (CombatUnit unit in members)
                    origin += unit.Position;
                double inverse = 1d / members.Count;
                origin = new Vector3(
                    (float)(origin.X * inverse),
                    (float)(origin.Y * inverse),
                    (float)(origin.Z * inverse)
                );
            }
            CombatUnit selected = FindNearest(origin, false);
            if (selected == null && Kind != TacticalCombatSelectionKind.StationTarget)
                selected = FindNearest(origin, true);
            if (selected != null)
                HandleObject(selected, false);
        }

        /// <summary>
        /// Queries one display category in registration order without consuming random numbers.
        /// </summary>
        /// <param name="origin">The contextual center.</param>
        /// <param name="fighter">The display category to search.</param>
        /// <returns>The first closest admitted object.</returns>
        private CombatUnit FindNearest(Vector3 origin, bool fighter)
        {
            CombatUnit selected = null;
            float best = float.MaxValue;
            foreach (CombatUnit unit in _session.Units)
            {
                if (unit.Side == _side || unit.IsFighter != fighter || !IsDisplayObject(unit))
                    continue;
                double dz = (double)origin.Z - unit.Position.Z;
                double dy = (double)origin.Y - unit.Position.Y;
                float dx = (float)((double)origin.X - unit.Position.X);
                double distance = dy * dy + dz * (float)dz + (double)dx * dx;
                if (!(distance >= best))
                {
                    selected = unit;
                    best = (float)distance;
                }
            }
            return selected;
        }

        /// <summary>
        /// Selects an object for inspection without issuing a gameplay order.
        /// </summary>
        /// <param name="unit">The inspected participant.</param>
        private void Inspect(CombatUnit unit)
        {
            bool opposing = unit.Side != _side;
            uint incoming = opposing && !_session.IsPaused ? EnqueueSelectedNotification(unit) : 0;
            Replace(
                opposing
                    ? TacticalCombatSelectionKind.EnemyInspection
                    : TacticalCombatSelectionKind.SingleObject
            );
            _notification = incoming;
            _primary = unit;
            _inspected = unit;
            if (Kind == TacticalCombatSelectionKind.SingleObject)
                _members.Add(unit);
        }

        /// <summary>
        /// Captures a category's current requested owner independently of future slot reuse.
        /// </summary>
        /// <param name="group">The category's owner.</param>
        /// <param name="category">The fixed slot.</param>
        /// <param name="fighter">Whether to use the proximity-category context.</param>
        private void SelectGroup(TacticalCombatGroup group, int category, bool fighter)
        {
            Replace(
                fighter
                    ? TacticalCombatSelectionKind.FighterGroup
                    : TacticalCombatSelectionKind.ObjectGroup
            );
            Group = group;
            Category = category;
            _inspected = fighter ? null : group.RequestedMembers.FirstOrDefault();
        }

        /// <summary>
        /// Replaces the current context while stopping the playback it owns.
        /// </summary>
        /// <param name="kind">The replacement context kind.</param>
        private void Replace(TacticalCombatSelectionKind kind)
        {
            StopNotification();
            Kind = kind;
            Group = null;
            Category = -1;
            _primary = null;
            _inspected = null;
            PrimaryWaypoint = null;
            _members.Clear();
            _targets.Clear();
            _waypoints.Clear();
            _groups.Clear();
            ContextReplaced?.Invoke(kind);
        }

        /// <summary>
        /// Returns selection to the empty context.
        /// </summary>
        private void Clear() => Replace(TacticalCombatSelectionKind.Empty);

        /// <summary>
        /// Refreshes captured contexts and their copied display lists after all queued input applies.
        /// </summary>
        private void Refresh()
        {
            _groups.Clear();
            if (
                Kind
                is TacticalCombatSelectionKind.ObjectGroup
                    or TacticalCombatSelectionKind.FighterGroup
            )
            {
                if (
                    Group.RequestedMembers.Count == 0
                    || Group.Order == TacticalCombatOrder.Withdraw
                    || Kind == TacticalCombatSelectionKind.FighterGroup
                        && (Group.Order == TacticalCombatOrder.Recover || Group.Members.Count == 0)
                )
                {
                    Clear();
                    return;
                }
                _members.Clear();
                _members.AddRange(Group.RequestedMembers);
                if (
                    Kind == TacticalCombatSelectionKind.ObjectGroup
                    && (
                        _inspected == null
                        || !Group.RequestedMembers.Contains(_inspected)
                        || !IsDisplayObject(_inspected)
                    )
                )
                    _inspected = Group.RequestedMembers[0];
                CopyRequests(Group);
                _groups.Add(Group);
            }
            else if (Kind == TacticalCombatSelectionKind.SingleObject)
            {
                if (!IsDisplayObject(_primary))
                {
                    Clear();
                    return;
                }
                TacticalCombatGroup group = _session.GetRequestedGroup(_primary);
                if (group != null)
                {
                    CopyRequests(group);
                    _groups.Add(group);
                }
            }
            else if (Kind == TacticalCombatSelectionKind.MultipleObjects)
            {
                _members.RemoveAll(unit => !IsDisplayObject(unit));
                if (_members.Count < 2)
                {
                    CombatUnit unit = _members.FirstOrDefault();
                    if (unit == null)
                        Clear();
                    else
                        Inspect(unit);
                    Refresh();
                    return;
                }
                _primary = _members[0];
            }
            else if (
                Kind == TacticalCombatSelectionKind.EnemyInspection
                && !IsDisplayObject(_primary)
            )
                Clear();
            else if (
                Kind == TacticalCombatSelectionKind.StationTarget
                && (
                    _session.BattleStation?.CanKeepTargetSelection(
                        _session.GetSide(_session.BattleStation.Unit.Side).IsWithdrawing,
                        _session.IsSideDirectControlEnabled(_side)
                    ) != true
                )
            )
                Clear();
            RefreshNotification();
        }

        /// <summary>
        /// Copies the current requested targets and route without changing command-primary membership.
        /// </summary>
        /// <param name="group">The requested owner.</param>
        private void CopyRequests(TacticalCombatGroup group)
        {
            _targets.Clear();
            foreach (CombatUnit unit in group.RequestedTargets)
                if (IsDisplayObject(unit))
                    AddObject(_targets, unit);
            _waypoints.Clear();
            _waypoints.AddRange(group.RequestedWaypoints);
        }

        /// <summary>
        /// Maintains the selected unit's looping notification using actual queue ownership and pause state.
        /// </summary>
        private void RefreshNotification()
        {
            if (
                _session.IsSynchronized
                && (!_session.IsRefreshingSynchronizedPresentation || _side != _session.PlayerSide)
            )
                return;
            if (
                _notification != 0
                && (!_session.Audio.Notifications.IsActive(_notification) || _session.IsPaused)
            )
                StopNotification();
            if (_notification != 0 || _session.IsPaused)
                return;
            CombatUnit unit =
                Kind
                    is TacticalCombatSelectionKind.SingleObject
                        or TacticalCombatSelectionKind.EnemyInspection
                    ? _primary
                : Kind
                    is TacticalCombatSelectionKind.ObjectGroup
                        or TacticalCombatSelectionKind.MultipleObjects
                    ? _inspected
                : Kind == TacticalCombatSelectionKind.FighterGroup ? Group.Members.FirstOrDefault()
                : null;
            if (unit == null)
                return;
            _notification = EnqueueSelectedNotification(unit);
        }

        /// <summary>
        /// Selects a concrete unit's looping notification without consuming a selector random draw.
        /// </summary>
        /// <param name="unit">The inspected capital ship or fighter.</param>
        /// <returns>The scheduler-owned loop handle, or zero when the selected bank is silent.</returns>
        private uint EnqueueSelectedNotification(CombatUnit unit)
        {
            if (
                _session.IsSynchronized
                && (!_session.IsRefreshingSynchronizedPresentation || _side != _session.PlayerSide)
            )
                return 0;
            int message =
                unit.IsFighter ? (_session.GetSide(unit.Side).FactionIndex == 0 ? 22 : 21)
                : !(unit.MaximumHull >= 1100) ? 25
                : !(unit.MaximumHull >= 2000) ? 24
                : 23;
            return _session.Audio.EnqueueNotification(message, true);
        }

        /// <summary>
        /// Releases only the active selected notification owned by the current context.
        /// </summary>
        private void StopNotification()
        {
            if (_notification != 0)
                _session.Audio.Notifications.Stop(_notification);
            _notification = 0;
        }

        /// <summary>
        /// Inserts a primary or copied object-chain value in unsigned registration order.
        /// </summary>
        /// <param name="items">The context-owned object chain.</param>
        /// <param name="unit">The addressed object.</param>
        private static void AddObject(List<CombatUnit> items, CombatUnit unit)
        {
            if (items.Contains(unit))
                return;
            int index = 0;
            while (index < items.Count && (uint)items[index].Index < (uint)unit.Index)
                index++;
            items.Insert(index, unit);
        }

        /// <summary>
        /// Inserts an ordered unique value into a context-owned chain.
        /// </summary>
        /// <typeparam name="T">The chain's value type.</typeparam>
        /// <param name="items">The ordered chain.</param>
        /// <param name="item">The requested value.</param>
        private static void AddUnique<T>(List<T> items, T item)
        {
            if (!items.Contains(item))
                items.Add(item);
        }

        /// <summary>
        /// Validates the identity retained by an object input request.
        /// </summary>
        /// <param name="unit">The addressed encounter participant.</param>
        private void ValidateUnit(CombatUnit unit)
        {
            ThrowIfDisposed();
            if (
                unit?.IsBattleStation != false
                || !ReferenceEquals(_session.GetUnit(unit.InstanceID), unit)
            )
                throw new ArgumentException("The object is outside this encounter.", nameof(unit));
        }

        /// <summary>
        /// Rejects input after its selection owner has released playback and pending requests.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TacticalCombatSelection));
        }
    }
}
