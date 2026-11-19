using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSelection
    {
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
            if (!_session.IsSideDirectControlEnabled(_side))
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
                    if (unit.Side != _side && _session.BattleStation != null)
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
            if (unit.Side == _side)
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
            if (unit.Side == _side)
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
            if (unit.Side != _side)
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
            if (unit.Side != _side)
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
            if (!_session.IsSideDirectControlEnabled(_side) || !CanIssueCommands)
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
    }
}
