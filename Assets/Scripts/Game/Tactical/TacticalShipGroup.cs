using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Owns the units, targets, and active behavior for one tactical command group.
    /// </summary>
    public sealed class TacticalShipGroup
    {
        private readonly List<TacticalUnitState> targets = new List<TacticalUnitState>();
        private readonly ReadOnlyCollection<TacticalUnitState> targetView;
        private readonly ReadOnlyCollection<TacticalUnitState> units;

        /// <summary>
        /// Gets the side that controls this group.
        /// </summary>
        public TacticalBattleSide Side { get; }

        /// <summary>
        /// Gets the units assigned to this group.
        /// </summary>
        public IReadOnlyList<TacticalUnitState> Units => units;

        /// <summary>
        /// Gets the group's ordered target list.
        /// </summary>
        public IReadOnlyList<TacticalUnitState> Targets => targetView;

        /// <summary>
        /// Gets the behavior currently assigned to the group.
        /// </summary>
        public TacticalBehavior Behavior { get; private set; }

        internal TacticalShipGroup(TacticalBattleSide side, IList<TacticalUnitState> units)
        {
            Side = side;
            this.units = new ReadOnlyCollection<TacticalUnitState>(units);
            targetView = targets.AsReadOnly();
        }

        /// <summary>
        /// Replaces the group's active tactical behavior.
        /// </summary>
        /// <param name="behavior">The behavior to assign.</param>
        public void SetBehavior(TacticalBehavior behavior)
        {
            if (!Enum.IsDefined(typeof(TacticalBehavior), behavior))
                throw new ArgumentOutOfRangeException(nameof(behavior));

            Behavior = behavior;
        }

        /// <summary>
        /// Adds an opposing active unit to the group's target list.
        /// </summary>
        /// <param name="target">The target to add.</param>
        public void AddTarget(TacticalUnitState target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (target.Side == Side)
                throw new ArgumentException(
                    "A ship group cannot target its own side.",
                    nameof(target)
                );
            if (!target.IsActive || targets.Contains(target))
                return;

            targets.Add(target);
        }

        /// <summary>
        /// Removes a unit from the group's target list.
        /// </summary>
        /// <param name="target">The target to remove.</param>
        public void RemoveTarget(TacticalUnitState target)
        {
            targets.Remove(target);
        }

        /// <summary>
        /// Removes targets that can no longer participate in combat.
        /// </summary>
        public void RemoveInactiveTargets()
        {
            targets.RemoveAll(target => !target.IsActive);
        }
    }
}
