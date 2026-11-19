using System;
using System.Numerics;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSelection
    {
        /// <summary>
        /// Selects or assigns a reusable command category while retaining its captured owner identity.
        /// </summary>
        /// <param name="category">The requested object slot.</param>
        private void SelectObjectCategory(int category)
        {
            TacticalCombatGroup group = _session.GetObjectGroup(_side, category);
            if (group != null)
            {
                if (Kind == TacticalCombatSelectionKind.ObjectGroup && Category == category)
                    Clear();
                else
                    SelectGroup(group, category, false);
            }
            else if (_session.IsSideDirectControlEnabled(_side))
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
            TacticalCombatGroup group = _session.GetFighterGroup(_side, category);
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
            _session.SetObjectGroup(_side, category, group);
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
                _session.SetObjectGroup(_side, free, group);
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
                if (_session.GetObjectGroup(_side, index) == null)
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
                if (ReferenceEquals(_session.GetObjectGroup(_side, category), group))
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
                    if (ReferenceEquals(_session.GetFighterGroup(_side, index), group))
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
            _session.EnqueueFactionNotification(
                _side,
                _session.GetSide(_side).FactionIndex == 0 ? alliance : empire
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
    }
}
