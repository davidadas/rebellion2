using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Owns the units, targets, and active behavior for one tactical command group.
    /// </summary>
    public sealed class TacticalShipGroup
    {
        private readonly IReadOnlyCollection<TacticalUnitState> battleUnits;
        private readonly List<TacticalNavPoint> navigationPoints = new List<TacticalNavPoint>();
        private readonly ReadOnlyCollection<TacticalNavPoint> navigationPointView;
        private readonly List<TacticalUnitState> targets = new List<TacticalUnitState>();
        private readonly ReadOnlyCollection<TacticalUnitState> targetView;
        private readonly List<TacticalUnitState> units;
        private readonly ReadOnlyCollection<TacticalUnitState> unitView;

        /// <summary>
        /// Gets the side that controls this group.
        /// </summary>
        public TacticalBattleSide Side { get; }

        /// <summary>
        /// Gets the units assigned to this group.
        /// </summary>
        public IReadOnlyList<TacticalUnitState> Units => unitView;

        /// <summary>
        /// Gets the group's ordered navigation-point list.
        /// </summary>
        public IReadOnlyList<TacticalNavPoint> NavigationPoints => navigationPointView;

        /// <summary>
        /// Gets the group's ordered target list.
        /// </summary>
        public IReadOnlyList<TacticalUnitState> Targets => targetView;

        /// <summary>
        /// Gets the behavior currently assigned to the group.
        /// </summary>
        public TacticalBehavior Behavior { get; private set; }

        /// <summary>
        /// Gets the capital ships' current engagement formation.
        /// </summary>
        public TacticalFormation Formation { get; private set; } = TacticalFormation.StandOff;

        /// <summary>
        /// Gets the command marker around which the group's formation is arranged.
        /// </summary>
        internal Vector3 MarkerPosition { get; private set; }

        internal TacticalShipGroup(
            TacticalBattleSide side,
            IReadOnlyCollection<TacticalUnitState> battleUnits,
            IEnumerable<TacticalUnitState> units
        )
        {
            Side = side;
            this.battleUnits = battleUnits ?? throw new ArgumentNullException(nameof(battleUnits));
            this.units = new List<TacticalUnitState>(units);
            unitView = this.units.AsReadOnly();
            navigationPointView = navigationPoints.AsReadOnly();
            targetView = targets.AsReadOnly();
        }

        /// <summary>
        /// Adds a battle unit to this command group.
        /// </summary>
        /// <param name="unit">The unit to add.</param>
        public void AddUnit(TacticalUnitState unit)
        {
            ValidateBattleUnit(unit);
            if (!units.Contains(unit))
                units.Add(unit);
        }

        /// <summary>
        /// Removes a battle unit from this command group.
        /// </summary>
        /// <param name="unit">The unit to remove.</param>
        public void RemoveUnit(TacticalUnitState unit)
        {
            units.Remove(unit);
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
        /// Replaces the group's target list with one opposing unit and directs the group to engage it.
        /// </summary>
        /// <param name="target">The opposing tactical unit to engage.</param>
        public void AssignPrimaryTarget(TacticalUnitState target)
        {
            ValidateTarget(target);
            if (!target.IsActive)
                return;

            targets.Clear();
            targets.Add(target);
            Behavior = TacticalBehavior.PrimaryTarget;
        }

        /// <summary>
        /// Replaces the capital ships' engagement formation.
        /// </summary>
        /// <param name="formation">The formation to assign.</param>
        public void SetFormation(TacticalFormation formation)
        {
            if (!Enum.IsDefined(typeof(TacticalFormation), formation))
                throw new ArgumentOutOfRangeException(nameof(formation));

            Formation = formation;
        }

        /// <summary>
        /// Moves the command marker that anchors the group's formation.
        /// </summary>
        /// <param name="position">The marker's tactical position.</param>
        internal void SetMarkerPosition(Vector3 position)
        {
            MarkerPosition = position;
        }

        /// <summary>
        /// Adds an opposing active unit to the group's target list.
        /// </summary>
        /// <param name="target">The target to add.</param>
        public void AddTarget(TacticalUnitState target)
        {
            ValidateTarget(target);
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

        /// <summary>
        /// Replaces the group's ordered target list.
        /// </summary>
        /// <param name="newTargets">The replacement targets in priority order.</param>
        public void ReplaceTargets(IEnumerable<TacticalUnitState> newTargets)
        {
            if (newTargets == null)
                throw new ArgumentNullException(nameof(newTargets));

            List<TacticalUnitState> replacements = new List<TacticalUnitState>();
            foreach (TacticalUnitState target in newTargets)
            {
                ValidateTarget(target);
                if (target.IsActive && !replacements.Contains(target))
                    replacements.Add(target);
            }

            targets.Clear();
            targets.AddRange(replacements);
        }

        /// <summary>
        /// Adds a navigation point to the group's route.
        /// </summary>
        /// <param name="point">The navigation point to append.</param>
        public void AddNavigationPoint(TacticalNavPoint point)
        {
            if (point == null)
                throw new ArgumentNullException(nameof(point));
            if (!navigationPoints.Contains(point))
                navigationPoints.Add(point);
        }

        /// <summary>
        /// Removes a navigation point from the group's route.
        /// </summary>
        /// <param name="point">The navigation point to remove.</param>
        public void RemoveNavigationPoint(TacticalNavPoint point)
        {
            navigationPoints.Remove(point);
        }

        /// <summary>
        /// Replaces the group's ordered navigation route.
        /// </summary>
        /// <param name="newPoints">The replacement route.</param>
        public void ReplaceNavigationPoints(IEnumerable<TacticalNavPoint> newPoints)
        {
            if (newPoints == null)
                throw new ArgumentNullException(nameof(newPoints));

            List<TacticalNavPoint> replacements = new List<TacticalNavPoint>();
            foreach (TacticalNavPoint point in newPoints)
            {
                if (point == null)
                    throw new ArgumentException(
                        "A navigation route cannot contain a null point.",
                        nameof(newPoints)
                    );
                if (!replacements.Contains(point))
                    replacements.Add(point);
            }

            navigationPoints.Clear();
            navigationPoints.AddRange(replacements);
        }

        private void ValidateBattleUnit(TacticalUnitState unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (!battleUnits.Contains(unit))
                throw new ArgumentException(
                    "The unit does not belong to this battle.",
                    nameof(unit)
                );
            if (unit.Side != Side)
                throw new ArgumentException("The unit belongs to the opposing side.", nameof(unit));
        }

        private void ValidateTarget(TacticalUnitState target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!battleUnits.Contains(target))
                throw new ArgumentException(
                    "The target does not belong to this battle.",
                    nameof(target)
                );
            if (target.Side == Side)
                throw new ArgumentException(
                    "A ship group cannot target its own side.",
                    nameof(target)
                );
        }
    }
}
