using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using CommandKind = Rebellion.Game.Combat.ActiveBattle.CommandKind;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Gets the requested command group currently occupying an object category.
        /// </summary>
        /// <param name="side">The category's owning side.</param>
        /// <param name="category">The zero-based category from zero through seven.</param>
        /// <returns>The current owner, or null for an empty category.</returns>
        public TacticalCombatGroup GetObjectGroup(CombatSide side, int category) =>
            _objectGroups[(int)side, category];

        /// <summary>
        /// Gets the deployed fighter group currently occupying a fighter category.
        /// </summary>
        /// <param name="side">The category's owning side.</param>
        /// <param name="category">The zero-based category from zero through three.</param>
        /// <returns>The current owner, or null for an empty category.</returns>
        public TacticalCombatGroup GetFighterGroup(CombatSide side, int category) =>
            _fighterGroups[(int)side, category];

        /// <summary>
        /// Assigns a requested command group to a reusable object category.
        /// </summary>
        /// <param name="side">The category's owning side.</param>
        /// <param name="category">The zero-based category from zero through seven.</param>
        /// <param name="group">The new owner, or null to clear the category.</param>
        public void SetObjectGroup(CombatSide side, int category, TacticalCombatGroup group)
        {
            if (group != null)
            {
                ValidateGroup(group);
                if (group.Side != side || group.IsFighterGroup)
                    throw new ArgumentException(
                        "An object category requires a command group of the same side.",
                        nameof(group)
                    );
            }
            _objectGroups[(int)side, category] = group;
            GroupCategoryChanged?.Invoke();
        }

        /// <summary>
        /// Changes a side's command authority while retaining its current applied group orders.
        /// </summary>
        /// <param name="side">The controlled side.</param>
        /// <param name="automated">Whether tactical automation should issue its orders.</param>
        public void SetAutomated(CombatSide side, bool automated)
        {
            GetSide(side).IsAutomated = automated;
            foreach (TacticalCombatGroup group in _groupsByID.Values)
                if (group.Side == side)
                    group.IsAutomated = automated;
        }

        /// <summary>
        /// Clears category ownership only when it still names the group being removed.
        /// </summary>
        /// <param name="group">The previous category owner.</param>
        private void ClearGroupCategories(TacticalCombatGroup group)
        {
            bool changed = false;
            for (int category = 0; category < 8; category++)
                if (ReferenceEquals(_objectGroups[(int)group.Side, category], group))
                {
                    _objectGroups[(int)group.Side, category] = null;
                    changed = true;
                    break;
                }
            for (int category = 0; category < 4; category++)
                if (ReferenceEquals(_fighterGroups[(int)group.Side, category], group))
                {
                    _fighterGroups[(int)group.Side, category] = null;
                    changed = true;
                    break;
                }
            if (changed)
                GroupCategoryChanged?.Invoke();
        }

        /// <summary>
        /// Gets a tactical unit by its campaign instance identifier.
        /// </summary>
        /// <param name="instanceID">The campaign unit identifier.</param>
        /// <returns>The unit, or null when it is outside this encounter.</returns>
        public CombatUnit GetUnit(string instanceID) =>
            instanceID != null && _unitsByID.TryGetValue(instanceID, out CombatUnit unit)
                ? unit
                : null;

        /// <summary>
        /// Gets the active group containing a tactical unit.
        /// </summary>
        /// <param name="unit">The group's member.</param>
        /// <returns>The member's group, or null after removal.</returns>
        public TacticalCombatGroup GetGroup(CombatUnit unit) =>
            unit != null && _appliedGroups.TryGetValue(unit, out TacticalCombatGroup group)
                ? group
                : null;

        /// <summary>
        /// Gets the latest requested owner before queued membership changes apply.
        /// </summary>
        /// <param name="unit">The selected unit.</param>
        /// <returns>The requested group, or null after requested removal.</returns>
        public TacticalCombatGroup GetRequestedGroup(CombatUnit unit) =>
            unit != null && _requestedGroups.TryGetValue(unit, out TacticalCombatGroup group)
                ? group
                : null;

        /// <summary>
        /// Requests an ordered group while retaining prior applied ownership until command dispatch.
        /// </summary>
        /// <param name="members">The ordered members of one side.</param>
        /// <returns>The newly registered tactical group.</returns>
        public TacticalCombatGroup CreateGroup(IReadOnlyList<CombatUnit> members)
        {
            if (members == null || members.Count == 0)
                throw new ArgumentException(
                    "A tactical group requires at least one member.",
                    nameof(members)
                );
            CombatSide side = members[0].Side;
            foreach (CombatUnit member in members)
                if (
                    member.Side != side
                    || !_unitsByID.TryGetValue(member.InstanceID, out CombatUnit registered)
                    || !ReferenceEquals(member, registered)
                )
                    throw new ArgumentException(
                        "A tactical group requires registered members of one side.",
                        nameof(members)
                    );
            if (new HashSet<CombatUnit>(members).Count != members.Count)
                throw new ArgumentException(
                    "A tactical group cannot repeat a member.",
                    nameof(members)
                );
            CombatUnit first = members[0];
            RequestMemberRemoval(first);
            TacticalCombatGroup group = new(
                ++_nextGroupID[(int)side],
                first,
                GetSide(side).Forward
            );
            RegisterGroup(group);
            QueueCommand(new Command(CommandKind.CreateGroup, first, group: group));
            for (int index = 1; index < members.Count; index++)
                AddGroupMember(group, members[index]);
            return group;
        }

        /// <summary>
        /// Requests a member transfer while preserving its current formation until dispatch.
        /// </summary>
        /// <param name="group">The requested destination group.</param>
        /// <param name="unit">The transferring unit.</param>
        public void AddGroupMember(TacticalCombatGroup group, CombatUnit unit)
        {
            ValidateGroup(group);
            if (
                unit == null
                || unit.Side != group.Side
                || !_unitsByID.TryGetValue(unit.InstanceID, out CombatUnit registered)
                || !ReferenceEquals(registered, unit)
            )
                throw new ArgumentException(
                    "A member must belong to this encounter and side.",
                    nameof(unit)
                );
            RequestMemberRemoval(unit);
            group.AddRequestedMember(unit);
            _requestedGroups[unit] = group;
            QueueCommand(new Command(CommandKind.AddGroupMember, unit, group: group));
        }

        /// <summary>
        /// Requests removal from one group without changing a later requested owner.
        /// </summary>
        /// <param name="group">The group losing the member.</param>
        /// <param name="unit">The requested member to remove.</param>
        public void RemoveGroupMember(TacticalCombatGroup group, CombatUnit unit)
        {
            ValidateGroup(group);
            if (
                unit == null
                || !_unitsByID.TryGetValue(unit.InstanceID, out CombatUnit registered)
                || !ReferenceEquals(registered, unit)
            )
                throw new ArgumentException(
                    "The member does not belong to this encounter.",
                    nameof(unit)
                );
            RemoveRequestedMember(group, unit);
            if (group.RequestedMembers.Count == 0)
                ClearGroupCategories(group);
            QueueCommand(new Command(CommandKind.RemoveGroupMember, unit, group: group));
        }

        /// <summary>
        /// Removes requested membership and queues the corresponding active-roster removal.
        /// </summary>
        /// <param name="unit">The member leaving its requested group.</param>
        private void RequestMemberRemoval(CombatUnit unit)
        {
            TacticalCombatGroup previous = GetRequestedGroup(unit);
            if (previous == null)
                return;
            RemoveGroupMember(previous, unit);
        }

        /// <summary>
        /// Removes category ownership now and queues deletion of the group's applied roster.
        /// </summary>
        /// <param name="group">The registered group being replaced.</param>
        public void DeleteGroup(TacticalCombatGroup group)
        {
            ValidateGroup(group);
            ClearGroupCategories(group);
            QueueCommand(new Command(CommandKind.DeleteGroup, null, group: group));
        }

        /// <summary>
        /// Queues a designated target command for a registered group.
        /// </summary>
        /// <param name="group">The attacking group.</param>
        /// <param name="target">The designated encounter unit.</param>
        /// <param name="append">Whether to append to the existing target list.</param>
        public void SetTarget(TacticalCombatGroup group, CombatUnit target, bool append = false)
        {
            ValidateGroup(group);
            if (
                target == null
                || !_unitsByID.TryGetValue(target.InstanceID, out CombatUnit registered)
                || !ReferenceEquals(target, registered)
            )
                throw new ArgumentException(
                    "The target does not belong to this encounter.",
                    nameof(target)
                );
            group.RequestTarget(target, append);
            QueueCommand(
                new Command(CommandKind.Target, null, target, group: group, append: append)
            );
        }

        /// <summary>
        /// Registers group lifecycle callbacks used by queued withdrawal and recovery transitions.
        /// </summary>
        /// <param name="group">The new encounter group.</param>
        private void RegisterGroup(TacticalCombatGroup group)
        {
            _groupsByID.Add(group.ID, group);
            IndexGroupMembership(group);
            group.IsAutomated = GetSide(group.Side).IsAutomated;
            group.HyperspaceRequested += RequestHyperspace;
            group.OrderRequested += RequestOrder;
            group.AttackModeRequested += QueueAttackMode;
        }

        /// <summary>
        /// Indexes the requested and applied rosters of a registered group.
        /// </summary>
        /// <param name="group">The group whose ordered rosters supply the membership.</param>
        private void IndexGroupMembership(TacticalCombatGroup group)
        {
            foreach (CombatUnit unit in group.Members)
                _appliedGroups[unit] = group;
            foreach (CombatUnit unit in group.RequestedMembers)
                _requestedGroups[unit] = group;
        }

        /// <summary>
        /// Removes a requested member while retaining any newer requested assignment.
        /// </summary>
        /// <param name="group">The group from which the unit is being removed.</param>
        /// <param name="unit">The departing member.</param>
        private void RemoveRequestedMember(TacticalCombatGroup group, CombatUnit unit)
        {
            group.RemoveRequestedMember(unit);
            if (ReferenceEquals(GetRequestedGroup(unit), group))
                _requestedGroups.Remove(unit);
        }

        /// <summary>
        /// Removes both ownership lanes when a unit stops participating in the battle.
        /// </summary>
        /// <param name="unit">The destroyed, departing, or recovering member.</param>
        private void RemoveFromGroup(CombatUnit unit)
        {
            TacticalCombatGroup requested = GetRequestedGroup(unit);
            if (requested != null)
                RemoveRequestedMember(requested, unit);
            TacticalCombatGroup applied = GetGroup(unit);
            if (applied != null)
                RemoveAppliedMember(applied, unit);
            if (requested?.Members.Count == 0 && requested.RequestedMembers.Count == 0)
                DeregisterGroup(requested);
        }

        /// <summary>
        /// Applies membership removal and deletes the group after its final active member leaves.
        /// </summary>
        /// <param name="group">The previous applied owner.</param>
        /// <param name="unit">The departing member.</param>
        private void RemoveAppliedMember(TacticalCombatGroup group, CombatUnit unit)
        {
            group.RemoveMember(unit);
            if (ReferenceEquals(GetGroup(unit), group))
                _appliedGroups.Remove(unit);
            if (group.Members.Count == 0)
                DeregisterGroup(group);
        }

        /// <summary>
        /// Releases a group's registry, requested ownership, and lifecycle subscriptions.
        /// </summary>
        /// <param name="group">The group whose active roster is empty.</param>
        private void DeregisterGroup(TacticalCombatGroup group)
        {
            ClearGroupCategories(group);
            while (group.RequestedMembers.Count != 0)
                RemoveRequestedMember(group, group.RequestedMembers[0]);
            group.HyperspaceRequested -= RequestHyperspace;
            group.OrderRequested -= RequestOrder;
            group.AttackModeRequested -= QueueAttackMode;
            DetachAttackRun(group);
            _pendingManeuvers.Remove(group);
            _pendingAttackOrders.Remove(group);
            _completedAttackOrders.Remove(group);
            _groupsByID.Remove(group.ID);
            _groups.Remove(group);
        }

        /// <summary>
        /// Queues a behavior requested by a registered group's label change.
        /// </summary>
        /// <param name="group">The requesting group.</param>
        /// <param name="order">The requested behavior.</param>
        private void RequestOrder(TacticalCombatGroup group, TacticalCombatOrder order)
        {
            if (
                group.IsFighterGroup
                && order is TacticalCombatOrder.Recover or TacticalCombatOrder.AttackBattleStation
            )
                ClearGroupCategories(group);
            QueueCommand(new Command(CommandKind.Order, null, group: group, value: (int)order));
        }

        /// <summary>
        /// Queues a requested attack-mode change through the battle command stream.
        /// </summary>
        /// <param name="group">The registered group.</param>
        /// <param name="closeAttack">Whether to close with the target.</param>
        private void QueueAttackMode(TacticalCombatGroup group, bool closeAttack) =>
            QueueCommand(
                new Command(CommandKind.AttackMode, null, group: group, value: closeAttack ? 1 : 0)
            );

        /// <summary>
        /// Queues a formation change for a registered command or fighter group.
        /// </summary>
        /// <param name="group">The group receiving the formation.</param>
        /// <param name="formation">The requested formation.</param>
        public void SetFormation(TacticalCombatGroup group, TacticalCombatFormationKind formation)
        {
            if (
                group == null
                || !_groupsByID.TryGetValue(group.ID, out TacticalCombatGroup registered)
                || !ReferenceEquals(group, registered)
            )
                throw new ArgumentException(
                    "The group does not belong to this encounter.",
                    nameof(group)
                );
            group.RequestFormation(formation);
            QueueCommand(
                new Command(CommandKind.Formation, null, group: group, value: (int)formation)
            );
        }

        /// <summary>
        /// Queues a navigation-list change for the battle's delayed command phase.
        /// </summary>
        /// <param name="group">The moving group.</param>
        /// <param name="position">The selected navigation point.</param>
        /// <param name="append">Whether to extend the route.</param>
        public void SetWaypoint(TacticalCombatGroup group, Vector3 position, bool append = false)
        {
            ValidateGroup(group);
            group.RequestWaypoint(position, append);
            QueueCommand(
                new Command(
                    CommandKind.Waypoint,
                    null,
                    group: group,
                    append: append,
                    position: position
                )
            );
        }

        /// <summary>
        /// Removes a requested target and queues removal from the active target list.
        /// </summary>
        /// <param name="group">The attacking group.</param>
        /// <param name="target">The target to remove.</param>
        public void RemoveTarget(TacticalCombatGroup group, CombatUnit target)
        {
            ValidateGroup(group);
            group.RemoveTarget(target, true);
            QueueCommand(new Command(CommandKind.RemoveTarget, null, target, group: group));
        }

        /// <summary>
        /// Removes a requested waypoint and queues removal from the active route.
        /// </summary>
        /// <param name="group">The moving group.</param>
        /// <param name="position">The waypoint to remove.</param>
        public void RemoveWaypoint(TacticalCombatGroup group, Vector3 position)
        {
            ValidateGroup(group);
            group.RemoveWaypoint(position, true);
            QueueCommand(
                new Command(CommandKind.RemoveWaypoint, null, group: group, position: position)
            );
        }

        /// <summary>
        /// Rejects commands for a group owned by another encounter or already removed.
        /// </summary>
        /// <param name="group">The commanded group.</param>
        private void ValidateGroup(TacticalCombatGroup group)
        {
            if (
                group == null
                || !_groupsByID.TryGetValue(group.ID, out TacticalCombatGroup registered)
                || !ReferenceEquals(registered, group)
            )
                throw new ArgumentException(
                    "The group does not belong to this encounter.",
                    nameof(group)
                );
        }
    }
}
