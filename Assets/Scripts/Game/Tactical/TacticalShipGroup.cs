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
        /// Gets the friendly tactical object currently being escorted.
        /// </summary>
        public TacticalUnitState EscortTarget { get; private set; }

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

        /// <summary>
        /// Gets the revision of the group's targeting and behavior command state.
        /// </summary>
        internal int CommandRevision { get; private set; }

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
            CommandRevision++;
            if (behavior != TacticalBehavior.Escort)
                EscortTarget = null;
        }

        /// <summary>
        /// Replaces the group's target list with one opposing unit without changing its active order.
        /// </summary>
        /// <param name="target">The opposing tactical unit to engage.</param>
        public void AssignPrimaryTarget(TacticalUnitState target)
        {
            ValidateTarget(target);
            if (!target.IsActive)
                return;

            targets.Clear();
            targets.Add(target);
            CommandRevision++;
            EscortTarget = null;
            if (Behavior == TacticalBehavior.Escort)
                Behavior = TacticalBehavior.None;
        }

        /// <summary>
        /// Directs the group to escort one active friendly tactical object.
        /// </summary>
        /// <param name="target">The friendly tactical object to escort.</param>
        public void AssignEscortTarget(TacticalUnitState target)
        {
            ValidateBattleUnit(target);
            if (!target.IsActive || units.Contains(target))
                return;

            targets.Clear();
            EscortTarget = target;
            Behavior = TacticalBehavior.Escort;
            CommandRevision++;
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
        /// Captures the group's mutable command state in stable battle order.
        /// </summary>
        /// <param name="groupIndex">The group's index in the battle group list.</param>
        /// <returns>The resumable group state.</returns>
        internal TacticalShipGroupSnapshot CaptureState(int groupIndex)
        {
            return new TacticalShipGroupSnapshot
            {
                GroupIndex = groupIndex,
                UnitInstanceIDs = units.Select(unit => unit.Unit.GetInstanceID()).ToList(),
                Behavior = Behavior,
                Formation = Formation,
                MarkerPosition = CaptureVector(MarkerPosition),
                CommandRevision = CommandRevision,
                TargetInstanceIDs = targets.Select(target => target.Unit.GetInstanceID()).ToList(),
                EscortTargetInstanceID = EscortTarget?.Unit.GetInstanceID(),
                NavigationPoints = navigationPoints
                    .Select(point => new TacticalVectorSnapshot
                    {
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z,
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// Restores this group's membership and active command state.
        /// </summary>
        /// <param name="snapshot">The saved group state.</param>
        /// <param name="unitsById">All participating tactical units indexed by identifier.</param>
        internal void RestoreState(
            TacticalShipGroupSnapshot snapshot,
            IReadOnlyDictionary<string, TacticalUnitState> unitsById
        )
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (unitsById == null)
                throw new ArgumentNullException(nameof(unitsById));

            units.Clear();
            units.AddRange(ResolveUnits(snapshot.UnitInstanceIDs, unitsById));
            targets.Clear();
            targets.AddRange(ResolveUnits(snapshot.TargetInstanceIDs, unitsById));
            navigationPoints.Clear();
            navigationPoints.AddRange(
                snapshot.NavigationPoints.Select(point => new TacticalNavPoint(
                    point.X,
                    point.Y,
                    point.Z
                ))
            );
            Behavior = snapshot.Behavior;
            Formation = snapshot.Formation;
            MarkerPosition = RestoreVector(snapshot.MarkerPosition);
            CommandRevision = snapshot.CommandRevision;
            EscortTarget = string.IsNullOrEmpty(snapshot.EscortTargetInstanceID)
                ? null
                : ResolveUnit(snapshot.EscortTargetInstanceID, unitsById);
        }

        /// <summary>
        /// Resolves an ordered identifier list to participating tactical units.
        /// </summary>
        /// <param name="unitInstanceIDs">The ordered strategic unit identifiers.</param>
        /// <param name="unitsById">All participating tactical units indexed by identifier.</param>
        /// <returns>The resolved tactical units in the saved order.</returns>
        private static IEnumerable<TacticalUnitState> ResolveUnits(
            IEnumerable<string> unitInstanceIDs,
            IReadOnlyDictionary<string, TacticalUnitState> unitsById
        )
        {
            if (unitInstanceIDs == null)
                throw new ArgumentException(
                    "The tactical unit identifiers are missing.",
                    nameof(unitInstanceIDs)
                );

            return unitInstanceIDs.Select(unitInstanceID => ResolveUnit(unitInstanceID, unitsById));
        }

        /// <summary>
        /// Resolves one saved strategic unit identifier to its tactical state.
        /// </summary>
        /// <param name="unitInstanceID">The saved strategic unit identifier.</param>
        /// <param name="unitsById">All participating tactical units indexed by identifier.</param>
        /// <returns>The matching tactical unit.</returns>
        private static TacticalUnitState ResolveUnit(
            string unitInstanceID,
            IReadOnlyDictionary<string, TacticalUnitState> unitsById
        )
        {
            if (
                string.IsNullOrEmpty(unitInstanceID)
                || !unitsById.TryGetValue(unitInstanceID, out TacticalUnitState unit)
            )
            {
                throw new ArgumentException(
                    $"Tactical unit '{unitInstanceID}' is not part of this battle.",
                    nameof(unitInstanceID)
                );
            }

            return unit;
        }

        /// <summary>
        /// Converts a runtime vector to persisted components.
        /// </summary>
        /// <param name="value">The vector to capture.</param>
        /// <returns>The persisted vector.</returns>
        private static TacticalVectorSnapshot CaptureVector(Vector3 value)
        {
            return new TacticalVectorSnapshot
            {
                X = value.X,
                Y = value.Y,
                Z = value.Z,
            };
        }

        /// <summary>
        /// Converts persisted components to a runtime vector.
        /// </summary>
        /// <param name="snapshot">The persisted vector.</param>
        /// <returns>The restored runtime vector.</returns>
        private static Vector3 RestoreVector(TacticalVectorSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentException(
                    "The tactical marker position is missing.",
                    nameof(snapshot)
                );

            return new Vector3(snapshot.X, snapshot.Y, snapshot.Z);
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
            CommandRevision++;
        }

        /// <summary>
        /// Removes a unit from the group's target list.
        /// </summary>
        /// <param name="target">The target to remove.</param>
        public void RemoveTarget(TacticalUnitState target)
        {
            if (targets.Remove(target))
                CommandRevision++;
        }

        /// <summary>
        /// Removes targets that can no longer participate in combat.
        /// </summary>
        public void RemoveInactiveTargets()
        {
            if (targets.RemoveAll(target => !target.IsActive) > 0)
                CommandRevision++;
            if (EscortTarget?.IsActive != true)
            {
                EscortTarget = null;
                if (Behavior == TacticalBehavior.Escort)
                    Behavior = TacticalBehavior.None;
            }
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
            CommandRevision++;
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
