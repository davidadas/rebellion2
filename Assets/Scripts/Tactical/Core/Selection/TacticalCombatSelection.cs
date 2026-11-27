using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tactical
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
    public sealed partial class TacticalCombatSelection : IDisposable
    {
        private readonly TacticalCombatSession _session;
        private readonly CombatSide _side;
        private readonly Queue<DisplayModeCommand> _displayCommands = new();
        private readonly Queue<Action> _commands = new();
        private readonly Queue<PointerCommand> _pointers = new();
        private readonly List<CombatUnit> _members = new();
        private readonly List<CombatUnit> _targets = new();
        private readonly List<Vector3> _waypoints = new();
        private readonly List<TacticalCombatGroup> _groups = new();
        private CombatUnit _primary;
        private CombatUnit _inspected;
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
        public CombatUnit PrimaryUnit =>
            Kind switch
            {
                TacticalCombatSelectionKind.ObjectGroup => _inspected,
                TacticalCombatSelectionKind.FighterGroup => Group?.Members.FirstOrDefault(),
                _ => _primary,
            };
        public IReadOnlyList<CombatUnit> Members { get; }
        public IReadOnlyList<CombatUnit> Targets { get; }
        public IReadOnlyList<Vector3> Waypoints { get; }
        public IReadOnlyList<TacticalCombatGroup> Groups { get; }
        public bool CanIssueCommands =>
            _session.IsSideDirectControlEnabled(_side)
            && Kind
                is TacticalCombatSelectionKind.SingleObject
                    or TacticalCombatSelectionKind.MultipleObjects
                    or TacticalCombatSelectionKind.ObjectGroup
                    or TacticalCombatSelectionKind.FighterGroup;

        /// <summary>
        /// Attaches selection ownership to one captured tactical encounter.
        /// </summary>
        /// <param name="session">The encounter whose command and audio boundaries are used.</param>
        /// <param name="side">The faction owning this independent selection, or the local view's side.</param>
        public TacticalCombatSelection(TacticalCombatSession session, CombatSide? side = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _side = side ?? session.PlayerSide;
            if (_side is not CombatSide.Attacker and not CombatSide.Defender)
                throw new ArgumentOutOfRangeException(nameof(side));
            Members = _members.AsReadOnly();
            Targets = _targets.AsReadOnly();
            Waypoints = _waypoints.AsReadOnly();
            Groups = _groups.AsReadOnly();
        }

        /// <summary>
        /// Queues an object hit without replacing the current context first.
        /// </summary>
        /// <param name="unit">The addressed encounter participant.</param>
        /// <param name="extend">Whether to use the context's append or toggle operation.</param>
        public void QueueObject(CombatUnit unit, bool extend = false)
        {
            ValidateUnit(unit);
            if (
                RequestSynchronization(
                    new()
                    {
                        Kind = TacticalCombatCommandKind.Object,
                        UnitID = unit.InstanceID,
                        Extend = extend,
                    }
                )
            )
                return;
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
            if (
                RequestSynchronization(
                    new()
                    {
                        Kind = TacticalCombatCommandKind.Waypoint,
                        Waypoint = new(point),
                        Extend = extend,
                    }
                )
            )
                return;
            _pointers.Enqueue(new PointerCommand(null, point, extend));
        }

        /// <summary>
        /// Queues the empty pointer command used before an ordinary primary-button hit.
        /// </summary>
        public void QueueClear()
        {
            if (!RequestSynchronization(new() { Kind = TacticalCombatCommandKind.Clear }))
                _pointers.Enqueue(new PointerCommand(null, null, false));
        }

        /// <summary>
        /// Queues traversal from the inspection panel's addressed capital before semantic and pointer input.
        /// </summary>
        /// <param name="next">Whether to traverse forward instead of backward.</param>
        public void QueueDisplayMode(bool next)
        {
            ThrowIfDisposed();
            if (
                RequestSynchronization(
                    new() { Kind = TacticalCombatCommandKind.DisplayMode, Next = next }
                )
            )
                return;
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
            if (
                RequestSynchronization(
                    new() { Kind = TacticalCombatCommandKind.ObjectCategory, Category = category }
                )
            )
                return;
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
            if (
                RequestSynchronization(
                    new() { Kind = TacticalCombatCommandKind.FighterCategory, Category = category }
                )
            )
                return;
            _commands.Enqueue(() => SelectFighterCategory(category));
        }

        /// <summary>
        /// Queues the current context's nearest opposing-object operation.
        /// </summary>
        public void QueueNearestTarget()
        {
            if (!RequestSynchronization(new() { Kind = TacticalCombatCommandKind.NearestTarget }))
                _commands.Enqueue(SelectNearestTarget);
        }

        /// <summary>
        /// Queues continuation after prior display commands and before the simulation pause guard.
        /// </summary>
        public void QueueImmediateResult()
        {
            if (!RequestSynchronization(new() { Kind = TacticalCombatCommandKind.ImmediateResult }))
                _commands.Enqueue(_session.ApplyImmediateResult);
        }

        /// <summary>
        /// Queues a change between direct and automatic control without replacing selection.
        /// </summary>
        public void QueueToggleAutomation()
        {
            if (RequestSynchronization(new() { Kind = TacticalCombatCommandKind.ToggleAutomation }))
                return;
            _commands.Enqueue(() =>
                _session.SetAutomated(_side, !_session.GetSide(_side).IsAutomated)
            );
        }

        /// <summary>
        /// Queues a confirmed withdrawal for a fresh eligibility check before ordered presentation and side mutation.
        /// </summary>
        public void QueueWithdrawal()
        {
            if (RequestSynchronization(new() { Kind = TacticalCombatCommandKind.Withdrawal }))
                return;
            _commands.Enqueue(() =>
            {
                if (
                    !_session.IsSideDirectControlEnabled(_side)
                        && !_session.GetSide(_side).IsAutomated
                    || !_session.CanWithdraw(_side)
                )
                    return;
                WithdrawalStarting?.Invoke();
                StationControlsClosed?.Invoke();
                _session.SuspendDirectCommands(_side);
                _session.ApplyWithdrawal(_side);
                WithdrawalDispatched?.Invoke();
            });
        }

        /// <summary>
        /// Queues the station's target-selection context or toggles that context off.
        /// </summary>
        public void QueueStationTarget()
        {
            if (RequestSynchronization(new() { Kind = TacticalCombatCommandKind.StationTarget }))
                return;
            _commands.Enqueue(() =>
            {
                if (!_session.IsSideDirectControlEnabled(_side))
                    return;
                if (Kind == TacticalCombatSelectionKind.StationTarget)
                    Clear();
                else if (_session.BattleStation?.CanBeginTargetSelection == true)
                    Replace(TacticalCombatSelectionKind.StationTarget);
            });
        }

        /// <summary>
        /// Applies a right-panel behavior request to the current command context.
        /// </summary>
        /// <param name="order">The requested behavior.</param>
        public void RequestOrder(TacticalCombatOrder order)
        {
            ThrowIfDisposed();
            if (!CanIssueCommands)
                return;
            if (
                RequestSynchronization(
                    new() { Kind = TacticalCombatCommandKind.Order, Order = order }
                )
            )
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
            if (
                RequestSynchronization(
                    new() { Kind = TacticalCombatCommandKind.AttackMode, CloseAttack = close }
                )
            )
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
            if (
                RequestSynchronization(
                    new() { Kind = TacticalCombatCommandKind.Formation, Formation = formation }
                )
            )
                return;
            if (Kind == TacticalCombatSelectionKind.MultipleObjects)
            {
                if (FindFreeCategory() >= 0)
                    _session.CreateGroup(_members.ToArray());
                else
                {
                    CombatUnit first = _members[0];
                    Notify(0x84, 0x102);
                    foreach (CombatUnit unit in _members)
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
        public bool IsDisplayObject(CombatUnit unit) =>
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

        private readonly struct DisplayModeCommand
        {
            public CombatUnit Unit { get; }
            public bool Next { get; }

            /// <summary>
            /// Captures the inspected object before later queued selection changes.
            /// </summary>
            /// <param name="unit">The status panel's captured object.</param>
            /// <param name="next">Whether to traverse forward.</param>
            public DisplayModeCommand(CombatUnit unit, bool next)
            {
                Unit = unit;
                Next = next;
            }
        }

        private readonly struct PointerCommand
        {
            public CombatUnit Unit { get; }
            public Vector3? Waypoint { get; }
            public bool Extend { get; }

            /// <summary>
            /// Captures one pointer-pair request without applying selection or gameplay commands.
            /// </summary>
            /// <param name="unit">The addressed object, or null.</param>
            /// <param name="waypoint">The addressed navigation marker, or null.</param>
            /// <param name="extend">The contextual append or toggle modifier.</param>
            public PointerCommand(CombatUnit unit, Vector3? waypoint, bool extend)
            {
                Unit = unit;
                Waypoint = waypoint;
                Extend = extend;
            }
        }
    }
}
