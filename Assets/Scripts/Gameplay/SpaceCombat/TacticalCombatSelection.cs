using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies the command context represented by the tactical selection.
    /// </summary>
    public enum TacticalCombatSelectionKind
    {
        Empty,
        SingleObject,
        EnemyInspection,
        WaypointInspection,
        MultipleObjects,
        ObjectGroup,
        FighterGroup,
        StationTarget,
    }

    /// <summary>
    /// Owns ordered tactical selection, contextual requests, category commands, and selection playback.
    /// </summary>
    public sealed class TacticalCombatSelection : IDisposable
    {
        private readonly TacticalCombatSession _session;
        private readonly Queue<DisplayModeCommand> _displayCommands = new();
        private readonly Queue<Action> _commands = new();
        private readonly Queue<PointerCommand> _pointers = new();
        private readonly List<TacticalCombatUnit> _members = new();
        private readonly List<TacticalCombatUnit> _highlights = new();
        private readonly List<TacticalCombatUnit> _targets = new();
        private readonly List<Vector3> _waypoints = new();
        private readonly List<TacticalCombatGroup> _groups = new();
        private TacticalCombatUnit _primary;
        private TacticalCombatUnit _inspected;
        private uint _notification;
        private bool _disposed;
        public TacticalCombatSelectionKind Kind { get; private set; }
        public event Action<TacticalCombatSelectionKind> ContextReplaced;
        public event Action StationControlsClosed;
        public event Action WithdrawalStarting;
        public event Action WithdrawalDispatched;
        public TacticalCombatGroup Group { get; private set; }
        public int Category { get; private set; } = -1;
        public Vector3? PrimaryWaypoint { get; private set; }
        public TacticalCombatUnit PrimaryUnit =>
            Kind switch
            {
                TacticalCombatSelectionKind.ObjectGroup => _inspected,
                TacticalCombatSelectionKind.FighterGroup => Group?.Members.FirstOrDefault(),
                _ => _primary,
            };
        public IReadOnlyList<TacticalCombatUnit> Members { get; }
        public IReadOnlyList<TacticalCombatUnit> Highlights { get; }
        public IReadOnlyList<TacticalCombatUnit> Targets { get; }
        public IReadOnlyList<Vector3> Waypoints { get; }
        public IReadOnlyList<TacticalCombatGroup> Groups { get; }
        public bool CanIssueCommands =>
            _session.IsDirectControlEnabled
            && Kind
                is TacticalCombatSelectionKind.SingleObject
                    or TacticalCombatSelectionKind.MultipleObjects
                    or TacticalCombatSelectionKind.ObjectGroup
                    or TacticalCombatSelectionKind.FighterGroup;

        /// <summary>
        /// Attaches selection ownership to one captured tactical encounter.
        /// </summary>
        /// <param name="session">The encounter whose command and audio boundaries are used.</param>
        public TacticalCombatSelection(TacticalCombatSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Members = _members.AsReadOnly();
            Highlights = _highlights.AsReadOnly();
            Targets = _targets.AsReadOnly();
            Waypoints = _waypoints.AsReadOnly();
            Groups = _groups.AsReadOnly();
        }

        /// <summary>
        /// Queues an object hit without replacing the current context first.
        /// </summary>
        /// <param name="unit">The addressed encounter participant.</param>
        /// <param name="extend">Whether to use the context's append or toggle operation.</param>
        public void QueueObject(TacticalCombatUnit unit, bool extend = false)
        {
            ValidateUnit(unit);
            _pointers.Enqueue(new PointerCommand(unit, null, extend));
        }

        /// <summary>
        /// Queues a hit on an addressed navigation marker.
        /// </summary>
        /// <param name="point">The marker's encounter-local position.</param>
        /// <param name="extend">Whether to use the context's append or toggle operation.</param>
        public void QueueWaypoint(Vector3 point, bool extend = false)
        {
            if (!_session.Navigation.Waypoints.Contains(point))
                throw new ArgumentException(
                    "The navigation marker is outside this encounter.",
                    nameof(point)
                );
            _pointers.Enqueue(new PointerCommand(null, point, extend));
        }

        /// <summary>
        /// Queues the empty pointer command used before an ordinary primary-button hit.
        /// </summary>
        public void QueueClear() => _pointers.Enqueue(new PointerCommand(null, null, false));

        /// <summary>
        /// Queues traversal from the inspection panel's addressed capital before semantic and pointer input.
        /// </summary>
        /// <param name="next">Whether to traverse forward instead of backward.</param>
        public void QueueDisplayMode(bool next)
        {
            ThrowIfDisposed();
            if (PrimaryUnit != null)
                _displayCommands.Enqueue(new DisplayModeCommand(PrimaryUnit, next));
        }

        /// <summary>
        /// Queues selection, assignment, or toggling of an object category.
        /// </summary>
        /// <param name="category">The zero-based category from zero through seven.</param>
        public void QueueObjectCategory(int category)
        {
            if ((uint)category >= 8)
                throw new ArgumentOutOfRangeException(nameof(category));
            _commands.Enqueue(() => SelectObjectCategory(category));
        }

        /// <summary>
        /// Queues selection or toggling of an occupied fighter category.
        /// </summary>
        /// <param name="category">The zero-based category from zero through three.</param>
        public void QueueFighterCategory(int category)
        {
            if ((uint)category >= 4)
                throw new ArgumentOutOfRangeException(nameof(category));
            _commands.Enqueue(() => SelectFighterCategory(category));
        }

        /// <summary>
        /// Queues the current context's nearest opposing-object operation.
        /// </summary>
        public void QueueNearestTarget() => _commands.Enqueue(SelectNearestTarget);

        /// <summary>
        /// Queues a change between direct and automatic control without replacing selection.
        /// </summary>
        public void QueueToggleAutomation() =>
            _commands.Enqueue(() =>
                _session.SetAutomated(
                    _session.PlayerSide,
                    !_session.GetSide(_session.PlayerSide).IsAutomated
                )
            );

        /// <summary>
        /// Queues a confirmed withdrawal for a fresh eligibility check before ordered presentation and side mutation.
        /// </summary>
        public void QueueWithdrawal() =>
            _commands.Enqueue(() =>
            {
                if (
                    !_session.IsDirectControlEnabled
                        && !_session.GetSide(_session.PlayerSide).IsAutomated
                    || !_session.CanWithdraw(_session.PlayerSide)
                )
                    return;
                WithdrawalStarting?.Invoke();
                StationControlsClosed?.Invoke();
                _session.SuspendDirectCommands();
                _session.ApplyWithdrawal(_session.PlayerSide);
                WithdrawalDispatched?.Invoke();
            });

        /// <summary>
        /// Queues the station's target-selection context or toggles that context off.
        /// </summary>
        public void QueueStationTarget() =>
            _commands.Enqueue(() =>
            {
                if (!_session.IsDirectControlEnabled)
                    return;
                if (Kind == TacticalCombatSelectionKind.StationTarget)
                    Clear();
                else if (_session.BattleStation?.CanBeginTargetSelection == true)
                    Replace(TacticalCombatSelectionKind.StationTarget);
            });

        /// <summary>
        /// Applies a right-panel behavior request to the current command context.
        /// </summary>
        /// <param name="order">The requested behavior.</param>
        public void RequestOrder(TacticalCombatOrder order)
        {
            ThrowIfDisposed();
            if (!CanIssueCommands)
                return;
            if (Kind == TacticalCombatSelectionKind.SingleObject)
            {
                TacticalCombatGroup group = _session.CreateGroup(new[] { _primary });
                Notify(0x77, 0xf5);
                _session.SetOrder(group, order);
            }
            else if (Kind == TacticalCombatSelectionKind.MultipleObjects)
                ApplyMultiple(group => _session.SetOrder(group, order), 0x78, 0xf6);
            else
            {
                if (
                    Kind == TacticalCombatSelectionKind.FighterGroup
                    && order == TacticalCombatOrder.AttackBattleStation
                    && !_session.CanAttackBattleStation(Group.Side)
                )
                {
                    Clear();
                    return;
                }
                NotifyCategory(
                    Group,
                    Kind == TacticalCombatSelectionKind.FighterGroup ? 0x80 : 0x78,
                    Kind == TacticalCombatSelectionKind.FighterGroup ? 0xfe : 0xf6
                );
                _session.SetOrder(Group, order);
            }
        }

        /// <summary>
        /// Applies a right-panel engagement-mode request through its context-specific ownership path.
        /// </summary>
        /// <param name="close">Whether to use close engagement.</param>
        public void RequestAttackMode(bool close)
        {
            ThrowIfDisposed();
            if (!CanIssueCommands)
                return;
            if (Kind == TacticalCombatSelectionKind.SingleObject)
                _session.CreateGroup(new[] { _primary }).SetAttackMode(close);
            else if (Kind == TacticalCombatSelectionKind.MultipleObjects)
                ApplyMultiple(group => group.SetAttackMode(close), 0x6f, 0xed);
            else
            {
                if (Kind == TacticalCombatSelectionKind.ObjectGroup)
                    NotifyCategory(Group, 0x6f, 0xed);
                Group.SetAttackMode(close);
            }
        }

        /// <summary>
        /// Applies a right-panel formation request without converting single inspection into a group action.
        /// </summary>
        /// <param name="formation">The requested marker layout.</param>
        public void RequestFormation(TacticalCombatFormationKind formation)
        {
            ThrowIfDisposed();
            if (!CanIssueCommands || Kind == TacticalCombatSelectionKind.SingleObject)
                return;
            if (Kind == TacticalCombatSelectionKind.MultipleObjects)
            {
                if (FindFreeCategory() >= 0)
                    _session.CreateGroup(_members.ToArray());
                else
                {
                    TacticalCombatUnit first = _members[0];
                    Notify(0x84, 0x102);
                    foreach (TacticalCombatUnit unit in _members)
                        _session.SetFormation(_session.CreateGroup(new[] { unit }), formation);
                    Inspect(first);
                }
                return;
            }
            if (Kind == TacticalCombatSelectionKind.ObjectGroup)
                NotifyCategory(Group, 0x6f, 0xed);
            _session.SetFormation(Group, formation);
        }

        /// <summary>
        /// Drains display traversal before category commands and pointer pairs, then refreshes context and owned selection audio.
        /// </summary>
        public void ProcessPendingCommands()
        {
            ThrowIfDisposed();
            while (_displayCommands.Count != 0)
                ApplyDisplayMode(_displayCommands.Dequeue());
            while (_commands.Count != 0)
                _commands.Dequeue()();
            while (_pointers.Count != 0)
            {
                PointerCommand command = _pointers.Dequeue();
                if (command.Unit != null)
                    HandleObject(command.Unit, command.Extend);
                else if (command.Waypoint.HasValue)
                    HandleWaypoint(command.Waypoint.Value, command.Extend);
                else
                    Clear();
            }
            _session.Audio.Update();
            Refresh();
            _session.Audio.Update();
        }

        /// <summary>
        /// Drains pending display input before closing station targeting and its owned charge controls.
        /// </summary>
        public void ProcessStationDeparture()
        {
            ProcessPendingCommands();
            if (Kind == TacticalCombatSelectionKind.StationTarget)
                Clear();
            StationControlsClosed?.Invoke();
        }

        /// <summary>
        /// Tests the status-owned presence domain shared by inspection, highlights, and nearest-object queries.
        /// </summary>
        /// <param name="unit">The participant being queried.</param>
        /// <returns>Whether the registered non-station object still belongs to the displayed battle.</returns>
        public bool IsDisplayObject(TacticalCombatUnit unit) =>
            unit != null
            && ReferenceEquals(_session.GetUnit(unit.InstanceID), unit)
            && unit.IsActiveDisplayObject;

        /// <summary>
        /// Releases the selected playback and discards unapplied input requests.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            StopNotification();
            _displayCommands.Clear();
            _commands.Clear();
            _pointers.Clear();
            _disposed = true;
        }

        /// <summary>
        /// Traverses the addressed capital side roster or requested object-category roster without health filtering.
        /// </summary>
        /// <param name="command">The captured panel object and traversal direction.</param>
        private void ApplyDisplayMode(DisplayModeCommand command)
        {
            List<TacticalCombatUnit> candidates = new();
            bool category = Kind == TacticalCombatSelectionKind.ObjectGroup;
            if (category)
            {
                TacticalCombatGroup owner = _session.GetRequestedGroup(command.Unit);
                if (owner == null)
                    return;
                candidates.AddRange(owner.RequestedMembers);
            }
            else
            {
                foreach (TacticalCombatUnit unit in _session.Units)
                    if (
                        unit.Side == command.Unit.Side
                        && !unit.IsFighter
                        && !unit.IsBattleStation
                        && unit.Status
                            is TacticalCombatUnitStatus.Active
                                or TacticalCombatUnitStatus.PreparingHyperspace
                                or TacticalCombatUnitStatus.EnteringHyperspace
                                or TacticalCombatUnitStatus.AwaitingWithdrawal
                    )
                        candidates.Add(unit);
                candidates.Sort(CompareDisplayObjects);
            }
            int index = candidates.IndexOf(command.Unit);
            if (candidates.Count == 0 || index < 0 && !category)
                return;
            index = command.Next ? index + 1 : index - 1;
            if (index >= candidates.Count || index < 0)
                index = command.Next ? 0 : candidates.Count - 1;
            TacticalCombatUnit selected = candidates[index];
            if (category)
                _inspected = selected;
            else
                Inspect(selected);
        }

        /// <summary>
        /// Orders panel traversal by the unsigned registered object key.
        /// </summary>
        /// <param name="left">The first capital object.</param>
        /// <param name="right">The second capital object.</param>
        /// <returns>The unsigned key ordering.</returns>
        private static int CompareDisplayObjects(
            TacticalCombatUnit left,
            TacticalCombatUnit right
        ) => ((uint)left.Index).CompareTo((uint)right.Index);

        /// <summary>
        /// Dispatches one object hit through the current context rather than a universal target rule.
        /// </summary>
        /// <param name="unit">The addressed participant.</param>
        /// <param name="extend">The contextual append or toggle modifier.</param>
        private void HandleObject(TacticalCombatUnit unit, bool extend)
        {
            if (!_session.IsDirectControlEnabled)
            {
                Inspect(unit);
                return;
            }
            switch (Kind)
            {
                case TacticalCombatSelectionKind.SingleObject:
                    HandleSingleObject(unit, extend);
                    break;
                case TacticalCombatSelectionKind.MultipleObjects:
                    HandleMultipleObject(unit, extend);
                    break;
                case TacticalCombatSelectionKind.ObjectGroup:
                    HandleObjectGroup(unit, extend);
                    break;
                case TacticalCombatSelectionKind.FighterGroup:
                    HandleFighterGroup(unit, extend);
                    break;
                case TacticalCombatSelectionKind.StationTarget:
                    if (unit.Side != _session.PlayerSide && _session.BattleStation != null)
                        _session.FireSuperlaser(unit);
                    Clear();
                    break;
                default:
                    Inspect(unit);
                    break;
            }
        }

        /// <summary>
        /// Handles friendly extension, escort designation, and enemy target changes from one primary object.
        /// </summary>
        /// <param name="unit">The clicked participant.</param>
        /// <param name="extend">Whether to append or toggle.</param>
        private void HandleSingleObject(TacticalCombatUnit unit, bool extend)
        {
            if (unit.Side == _session.PlayerSide)
            {
                if (ReferenceEquals(unit, _primary))
                    return;
                if (extend)
                {
                    TacticalCombatUnit first = _primary;
                    Replace(TacticalCombatSelectionKind.MultipleObjects);
                    AddObject(_members, first);
                    AddObject(_members, unit);
                    _primary = _members[0];
                    _inspected = unit;
                    return;
                }
                if (!CanDesignateFriendly(unit))
                    return;
                TacticalCombatGroup group = GetUntrackedGroup(_primary);
                Notify(0x77, 0xf5);
                _session.SetTarget(group, unit);
                _session.SetOrder(group, TacticalCombatOrder.HoldFire);
                return;
            }
            if (!CanDesignateEnemy(unit))
            {
                Inspect(unit);
                return;
            }
            TacticalCombatGroup targetGroup = GetUntrackedGroup(_primary);
            SetEnemyTarget(targetGroup, unit, extend, false, 0x3b, 0xb5);
        }

        /// <summary>
        /// Handles ordered temporary membership and category creation from a multi-object context.
        /// </summary>
        /// <param name="unit">The clicked participant.</param>
        /// <param name="extend">Whether to toggle friendly membership.</param>
        private void HandleMultipleObject(TacticalCombatUnit unit, bool extend)
        {
            if (unit.Side == _session.PlayerSide)
            {
                if (extend)
                {
                    bool removed = _members.Remove(unit);
                    if (!removed)
                        AddObject(_members, unit);
                    if (_members.Count == 1)
                        Inspect(_members[0]);
                    else
                        _inspected = removed ? _members[0] : unit;
                    return;
                }
                if (!CanDesignateFriendly(unit))
                    return;
                int category = FindFreeCategory();
                if (category >= 0)
                {
                    if (_members.Any(member => member.Index == (unit.Index != 0 ? 1 : 0)))
                        _members.Remove(unit);
                    AddObject(_targets, unit);
                    if (_members.Count != 0)
                    {
                        CreateCategory(category);
                        return;
                    }
                }
                Inspect(unit);
                return;
            }
            if (!CanDesignateEnemy(unit))
            {
                Inspect(unit);
                return;
            }
            int free = FindFreeCategory();
            if (free >= 0)
            {
                Notify(0x3c + free, 0xb6 + free);
                AddObject(_targets, unit);
                CreateCategory(free);
                return;
            }
            TacticalCombatUnit first = _members[0];
            Notify(0x84, 0x102);
            foreach (TacticalCombatUnit member in _members)
                _session.SetTarget(_session.CreateGroup(new[] { member }), unit, true);
            Inspect(first);
        }

        /// <summary>
        /// Handles command-category member transfers separately from its target list.
        /// </summary>
        /// <param name="unit">The clicked participant.</param>
        /// <param name="extend">Whether to use category membership or target toggling.</param>
        private void HandleObjectGroup(TacticalCombatUnit unit, bool extend)
        {
            TacticalCombatGroup group = Group;
            if (unit.Side != _session.PlayerSide)
            {
                if (extend && !group.RequestedTargets.Contains(unit) && !CanDesignateEnemy(unit))
                {
                    Inspect(unit);
                    return;
                }
                SetEnemyTarget(group, unit, extend, false, 0x3c, 0xb6);
                return;
            }
            if (extend)
            {
                if (group.RequestedTargets.Contains(unit))
                {
                    _session.RemoveTarget(group, unit);
                    _session.SetOrder(group, TacticalCombatOrder.None);
                }
                else if (!group.RequestedMembers.Contains(unit))
                    _session.AddGroupMember(group, unit);
                else
                    _session.RemoveGroupMember(group, unit);
                return;
            }
            if (group.RequestedMembers.Contains(unit))
            {
                int free = FindFreeCategory();
                if (free >= 0)
                {
                    _members.Clear();
                    _members.AddRange(
                        group.RequestedMembers.Where(member => !ReferenceEquals(member, unit))
                    );
                    _targets.Clear();
                    _targets.Add(unit);
                    _waypoints.Clear();
                    if (_members.Count != 0)
                    {
                        CreateCategory(free);
                        return;
                    }
                }
                Inspect(unit);
            }
            else if (CanDesignateFriendly(unit))
            {
                _session.SetTarget(group, unit);
                NotifyCategory(group, 0x78, 0xf6);
                _session.SetOrder(group, TacticalCombatOrder.HoldFire);
            }
        }

        /// <summary>
        /// Handles proximity-category designation without transferring its fighter membership.
        /// </summary>
        /// <param name="unit">The clicked participant.</param>
        /// <param name="extend">Whether to toggle a designated target.</param>
        private void HandleFighterGroup(TacticalCombatUnit unit, bool extend)
        {
            if (unit.Side != _session.PlayerSide)
            {
                SetEnemyTarget(Group, unit, extend, true, 0x44, 0xbe);
                return;
            }
            if (extend && Group.RequestedTargets.Contains(unit))
            {
                _session.RemoveTarget(Group, unit);
                _session.SetOrder(Group, TacticalCombatOrder.None);
            }
            else if (CanDesignateFriendly(unit))
            {
                _session.SetTarget(Group, unit);
                NotifyCategory(Group, 0x80, 0xfe);
                _session.SetOrder(Group, TacticalCombatOrder.HoldFire);
            }
        }

        /// <summary>
        /// Replaces or toggles enemy designation while preserving category-specific state-one transitions.
        /// </summary>
        /// <param name="group">The requested command owner.</param>
        /// <param name="unit">The clicked opposing object.</param>
        /// <param name="extend">Whether to append or toggle.</param>
        /// <param name="fighter">Whether this is a proximity-category operation.</param>
        /// <param name="allianceMessage">The Alliance acknowledgment base.</param>
        /// <param name="empireMessage">The Empire acknowledgment base.</param>
        private void SetEnemyTarget(
            TacticalCombatGroup group,
            TacticalCombatUnit unit,
            bool extend,
            bool fighter,
            int allianceMessage,
            int empireMessage
        )
        {
            if (extend && group.RequestedTargets.Contains(unit))
            {
                _session.RemoveTarget(group, unit);
                return;
            }
            if (
                (!extend || group.RequestedTargets.Count == 0)
                && group.RequestedWaypoints.Count == 0
            )
            {
                if (
                    Kind
                    is TacticalCombatSelectionKind.ObjectGroup
                        or TacticalCombatSelectionKind.FighterGroup
                )
                    NotifyCategory(group, allianceMessage, empireMessage);
                else
                    Notify(allianceMessage, empireMessage);
            }
            if (!extend || !fighter && group.Order == TacticalCombatOrder.HoldFire)
            {
                _session.SetOrder(group, TacticalCombatOrder.None);
                _session.SetTarget(group, unit);
            }
            else
                _session.SetTarget(group, unit, true);
        }

        /// <summary>
        /// Dispatches a navigation hit through the current selection's route policy.
        /// </summary>
        /// <param name="point">The addressed lattice point.</param>
        /// <param name="extend">Whether to append or toggle.</param>
        private void HandleWaypoint(Vector3 point, bool extend)
        {
            if (!_session.IsDirectControlEnabled || !CanIssueCommands)
            {
                if (Kind == TacticalCombatSelectionKind.StationTarget)
                    Clear();
                else
                {
                    Replace(TacticalCombatSelectionKind.WaypointInspection);
                    PrimaryWaypoint = point;
                }
                return;
            }
            if (Kind == TacticalCombatSelectionKind.MultipleObjects)
            {
                int free = FindFreeCategory();
                if (free >= 0)
                {
                    Notify(0x22 + free, 0x9c + free);
                    AddUnique(_waypoints, point);
                    CreateCategory(free);
                }
                else
                {
                    TacticalCombatUnit first = _members[0];
                    Notify(0x84, 0x102);
                    foreach (TacticalCombatUnit unit in _members)
                        _session.SetWaypoint(_session.CreateGroup(new[] { unit }), point, true);
                    Inspect(first);
                }
                return;
            }
            TacticalCombatGroup group =
                Kind == TacticalCombatSelectionKind.SingleObject
                    ? GetUntrackedGroup(_primary)
                    : Group;
            int alliance =
                Kind == TacticalCombatSelectionKind.FighterGroup ? 0x2a
                : Kind == TacticalCombatSelectionKind.ObjectGroup ? 0x22
                : 0x21;
            int empire =
                Kind == TacticalCombatSelectionKind.FighterGroup ? 0xa4
                : Kind == TacticalCombatSelectionKind.ObjectGroup ? 0x9c
                : 0x9b;
            if (extend && group.RequestedWaypoints.Contains(point))
                _session.RemoveWaypoint(group, point);
            else
            {
                if (Kind == TacticalCombatSelectionKind.SingleObject)
                {
                    if (!extend)
                    {
                        _session.SetOrder(group, TacticalCombatOrder.None);
                        _session.SetWaypoint(group, point);
                        Notify(alliance, empire);
                        return;
                    }
                    if (group.RequestedWaypoints.Count == 0)
                        Notify(alliance, empire);
                }
                else if (!extend || group.RequestedWaypoints.Count == 0)
                    NotifyCategory(group, alliance, empire);
                if (!extend)
                    _session.SetOrder(group, TacticalCombatOrder.None);
                _session.SetWaypoint(group, point, extend);
            }
        }

        /// <summary>
        /// Selects or assigns a reusable command category while retaining its captured owner identity.
        /// </summary>
        /// <param name="category">The requested object slot.</param>
        private void SelectObjectCategory(int category)
        {
            TacticalCombatGroup group = _session.GetObjectGroup(_session.PlayerSide, category);
            if (group != null)
            {
                if (Kind == TacticalCombatSelectionKind.ObjectGroup && Category == category)
                    Clear();
                else
                    SelectGroup(group, category, false);
            }
            else if (_session.IsDirectControlEnabled)
            {
                if (
                    Kind
                    is TacticalCombatSelectionKind.SingleObject
                        or TacticalCombatSelectionKind.MultipleObjects
                )
                    CreateCategory(category);
                else if (
                    Kind
                    is TacticalCombatSelectionKind.ObjectGroup
                        or TacticalCombatSelectionKind.FighterGroup
                        or TacticalCombatSelectionKind.StationTarget
                )
                    Clear();
            }
        }

        /// <summary>
        /// Selects an occupied deployed fighter category without creating a replacement for an empty slot.
        /// </summary>
        /// <param name="category">The requested fighter slot.</param>
        private void SelectFighterCategory(int category)
        {
            TacticalCombatGroup group = _session.GetFighterGroup(_session.PlayerSide, category);
            if (group == null)
                return;
            if (Kind == TacticalCombatSelectionKind.FighterGroup && Category == category)
                Clear();
            else
                SelectGroup(group, category, true);
        }

        /// <summary>
        /// Creates a requested command category from the context's ordered primary, target, and route chains.
        /// </summary>
        /// <param name="category">The destination object slot.</param>
        private void CreateCategory(int category)
        {
            TacticalCombatGroup group = _session.CreateGroup(_members.ToArray());
            _session.SetObjectGroup(_session.PlayerSide, category, group);
            foreach (TacticalCombatUnit target in _targets)
                _session.SetTarget(group, target, true);
            foreach (Vector3 point in _waypoints)
                _session.SetWaypoint(group, point, true);
            bool friendly = _targets.Count != 0 && _targets[0].Side == group.Side;
            if (friendly)
            {
                NotifyCategory(group, 0x78, 0xf6);
                _session.SetOrder(group, TacticalCombatOrder.HoldFire);
            }
            SelectGroup(group, category, false);
        }

        /// <summary>
        /// Applies a multi-object behavior or engagement request through free-category and full-category paths.
        /// </summary>
        /// <param name="apply">The exact requested group operation.</param>
        /// <param name="allianceMessage">The Alliance category acknowledgment base.</param>
        /// <param name="empireMessage">The Empire category acknowledgment base.</param>
        private void ApplyMultiple(
            Action<TacticalCombatGroup> apply,
            int allianceMessage,
            int empireMessage
        )
        {
            int free = FindFreeCategory();
            if (free >= 0)
            {
                TacticalCombatGroup group = _session.CreateGroup(_members.ToArray());
                _session.SetObjectGroup(_session.PlayerSide, free, group);
                NotifyCategory(group, allianceMessage, empireMessage);
                apply(group);
                SelectGroup(group, free, false);
                return;
            }
            TacticalCombatUnit first = _members[0];
            Notify(0x84, 0x102);
            foreach (TacticalCombatUnit unit in _members)
                apply(_session.CreateGroup(new[] { unit }));
            Inspect(first);
        }

        /// <summary>
        /// Finds the first reusable object category in fixed slot order.
        /// </summary>
        /// <returns>The free slot, or minus one when all eight are occupied.</returns>
        private int FindFreeCategory()
        {
            for (int index = 0; index < 8; index++)
                if (_session.GetObjectGroup(_session.PlayerSide, index) == null)
                    return index;
            return -1;
        }

        /// <summary>
        /// Resolves an untracked requested owner or creates an individual command group.
        /// </summary>
        /// <param name="unit">The selected primary object.</param>
        /// <returns>The owner receiving its target or route request.</returns>
        private TacticalCombatGroup GetUntrackedGroup(TacticalCombatUnit unit)
        {
            TacticalCombatGroup group = _session.GetRequestedGroup(unit);
            if (group != null && FindObjectCategory(group) < 0)
                return group;
            return _session.CreateGroup(new[] { unit });
        }

        /// <summary>
        /// Finds a requested owner in the player's fixed object slots.
        /// </summary>
        /// <param name="group">The requested owner.</param>
        /// <returns>Its category, or minus one when untracked.</returns>
        private int FindObjectCategory(TacticalCombatGroup group)
        {
            for (int category = 0; category < 8; category++)
                if (ReferenceEquals(_session.GetObjectGroup(_session.PlayerSide, category), group))
                    return category;
            return -1;
        }

        /// <summary>
        /// Gets the current category occupied by a captured object or fighter group.
        /// </summary>
        /// <param name="group">The captured owner.</param>
        /// <param name="alliance">The Alliance acknowledgment base.</param>
        /// <param name="empire">The Empire acknowledgment base.</param>
        private void NotifyCategory(TacticalCombatGroup group, int alliance, int empire)
        {
            int category = -1;
            if (group.IsFighterGroup)
            {
                for (int index = 0; index < 4; index++)
                    if (
                        ReferenceEquals(_session.GetFighterGroup(_session.PlayerSide, index), group)
                    )
                    {
                        category = index;
                        break;
                    }
            }
            else
                category = FindObjectCategory(group);
            if (category >= 0)
                Notify(alliance + category, empire + category);
        }

        /// <summary>
        /// Queues a one-shot acknowledgment from the player's faction bank.
        /// </summary>
        /// <param name="alliance">The Alliance message ID.</param>
        /// <param name="empire">The Empire message ID.</param>
        private void Notify(int alliance, int empire) =>
            _session.Audio.EnqueueNotification(
                _session.GetSide(_session.PlayerSide).FactionIndex == 0 ? alliance : empire
            );

        /// <summary>
        /// Rejects friendly designations that already follow another object.
        /// </summary>
        /// <param name="unit">The proposed friendly target.</param>
        /// <returns>Whether its current applied owner admits the designation.</returns>
        private bool CanDesignateFriendly(TacticalCombatUnit unit)
        {
            TacticalCombatGroup group = _session.GetGroup(unit);
            return group == null
                || group.ActiveOrder != TacticalCombatOrder.HoldFire
                    && group.Order != TacticalCombatOrder.HoldFire;
        }

        /// <summary>
        /// Separates the displayed object domain from objects admitted as opposing order targets.
        /// </summary>
        /// <param name="unit">The opposing object.</param>
        /// <returns>Whether the current status admits an enemy designation.</returns>
        private bool CanDesignateEnemy(TacticalCombatUnit unit) =>
            IsDisplayObject(unit) && unit.Status != TacticalCombatUnitStatus.PreparingHyperspace;

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
            IReadOnlyList<TacticalCombatUnit> members = Kind
                is TacticalCombatSelectionKind.ObjectGroup
                    or TacticalCombatSelectionKind.FighterGroup
                ? Group.RequestedMembers
                : _members;
            if (Kind != TacticalCombatSelectionKind.StationTarget)
            {
                if (members.Count == 0)
                    return;
                foreach (TacticalCombatUnit unit in members)
                    origin += unit.Position;
                double inverse = 1d / members.Count;
                origin = new Vector3(
                    (float)(origin.X * inverse),
                    (float)(origin.Y * inverse),
                    (float)(origin.Z * inverse)
                );
            }
            TacticalCombatUnit selected = FindNearest(origin, false);
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
        private TacticalCombatUnit FindNearest(Vector3 origin, bool fighter)
        {
            TacticalCombatUnit selected = null;
            float best = float.MaxValue;
            foreach (TacticalCombatUnit unit in _session.Units)
            {
                if (
                    unit.Side == _session.PlayerSide
                    || unit.IsFighter != fighter
                    || !IsDisplayObject(unit)
                )
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
        private void Inspect(TacticalCombatUnit unit)
        {
            bool opposing = unit.Side != _session.PlayerSide;
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
            _highlights.Clear();
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
            _highlights.Clear();
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
                _highlights.AddRange(_members.Where(IsDisplayObject));
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
                _highlights.Add(_primary);
                TacticalCombatGroup group = _session.GetRequestedGroup(_primary);
                if (group != null)
                {
                    if (FindObjectCategory(group) >= 0)
                        foreach (TacticalCombatUnit unit in group.RequestedMembers)
                            if (IsDisplayObject(unit))
                                AddUnique(_highlights, unit);
                    CopyRequests(group);
                    _groups.Add(group);
                }
            }
            else if (Kind == TacticalCombatSelectionKind.MultipleObjects)
            {
                _members.RemoveAll(unit => !IsDisplayObject(unit));
                if (_members.Count < 2)
                {
                    TacticalCombatUnit unit = _members.FirstOrDefault();
                    if (unit == null)
                        Clear();
                    else
                        Inspect(unit);
                    Refresh();
                    return;
                }
                _primary = _members[0];
                _highlights.AddRange(_members);
            }
            else if (Kind == TacticalCombatSelectionKind.EnemyInspection)
            {
                if (!IsDisplayObject(_primary))
                    Clear();
                else
                    _highlights.Add(_primary);
            }
            else if (
                Kind == TacticalCombatSelectionKind.StationTarget
                && (
                    _session.BattleStation?.CanKeepTargetSelection(
                        _session.GetSide(_session.BattleStation.Unit.Side).IsWithdrawing,
                        _session.IsDirectControlEnabled
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
            foreach (TacticalCombatUnit unit in group.RequestedTargets)
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
                _notification != 0
                && (!_session.Audio.Notifications.IsActive(_notification) || _session.IsPaused)
            )
                StopNotification();
            if (_notification != 0 || _session.IsPaused)
                return;
            TacticalCombatUnit unit =
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
        private uint EnqueueSelectedNotification(TacticalCombatUnit unit)
        {
            int message =
                unit.IsFighter ? (_session.GetSide(unit.Side).FactionIndex == 0 ? 22 : 21)
                : !(unit.Systems.MaximumHull >= 1100) ? 25
                : !(unit.Systems.MaximumHull >= 2000) ? 24
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
        private static void AddObject(List<TacticalCombatUnit> items, TacticalCombatUnit unit)
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
        private void ValidateUnit(TacticalCombatUnit unit)
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

        private readonly struct DisplayModeCommand
        {
            public TacticalCombatUnit Unit { get; }
            public bool Next { get; }

            /// <summary>
            /// Captures the inspected object before later queued selection changes.
            /// </summary>
            /// <param name="unit">The status panel's captured object.</param>
            /// <param name="next">Whether to traverse forward.</param>
            public DisplayModeCommand(TacticalCombatUnit unit, bool next)
            {
                Unit = unit;
                Next = next;
            }
        }

        private readonly struct PointerCommand
        {
            public TacticalCombatUnit Unit { get; }
            public Vector3? Waypoint { get; }
            public bool Extend { get; }

            /// <summary>
            /// Captures one pointer-pair request without applying selection or gameplay commands.
            /// </summary>
            /// <param name="unit">The addressed object, or null.</param>
            /// <param name="waypoint">The addressed navigation marker, or null.</param>
            /// <param name="extend">The contextual append or toggle modifier.</param>
            public PointerCommand(TacticalCombatUnit unit, Vector3? waypoint, bool extend)
            {
                Unit = unit;
                Waypoint = waypoint;
                Extend = extend;
            }
        }
    }
}
