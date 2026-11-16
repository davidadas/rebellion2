using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies the behavior assigned to a tactical group.
    /// </summary>
    public enum TacticalCombatOrder
    {
        None,
        HoldFire,
        Recover,
        Withdraw,
        AttackFighters,
        AttackCapitalShips,
        AttackBattleStation,
        LeftHook,
        RightHook,
        Hammer,
        Anvil,
        Stop,
    }

    /// <summary>
    /// Captures a weapon discharge and its delayed impact.
    /// </summary>
    public readonly struct TacticalCombatShot
    {
        public TacticalCombatUnit Source { get; }
        public TacticalCombatUnit Target { get; }
        public TacticalWeaponType Weapon { get; }
        public float Strength { get; }
        public int ImpactDelay { get; }
        public int FiringDelay => Weapon == TacticalWeaponType.IonCannon ? 1 : 0;

        /// <summary>
        /// Creates a discharge after firing control selects a battery and target.
        /// </summary>
        /// <param name="source">The firing unit.</param>
        /// <param name="target">The unit receiving the shot.</param>
        /// <param name="weapon">The weapon used.</param>
        /// <param name="strength">The attack strength before target damage handling.</param>
        public TacticalCombatShot(
            TacticalCombatUnit source,
            TacticalCombatUnit target,
            TacticalWeaponType weapon,
            float strength
        )
        {
            Source = source;
            Target = target;
            Weapon = weapon;
            Strength = strength;
            ImpactDelay = weapon is TacticalWeaponType.IonCannon or TacticalWeaponType.Torpedo
                ? 2
                : 1;
        }
    }

    /// <summary>
    /// Selects one ready directional battery per firing opportunity and queues its valid lanes.
    /// This component acquires targets without applying impact damage or changing campaign units.
    /// </summary>
    public sealed class TacticalCombatFireControl
    {
        private readonly float[,] _scores = new float[4, 3];
        private readonly TacticalCombatUnit[,] _targets = new TacticalCombatUnit[4, 3];
        private readonly List<TacticalCombatUnit> _neighbors = new();
        public event Action<TacticalCombatShot> ShotRequested;
        public event Action<TacticalCombatUnit, TacticalCombatUnit> TractorTargetRequested;

        /// <summary>
        /// Attempts designated targets first, followed by opportunity shots at nearby fighters.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="group">The unit's orders and designated targets.</param>
        /// <param name="spatialIndex">The active battle spatial index.</param>
        /// <param name="random">The battle's firing-opportunity stream.</param>
        public void AcquireTargets(
            TacticalCombatUnit unit,
            TacticalCombatGroup group,
            TacticalCombatSpatialIndex spatialIndex,
            IRandomNumberProvider random
        )
        {
            if (
                !unit.IsTargetable
                || unit.Status == TacticalCombatUnitStatus.PreparingHyperspace
                || !HasReadyBattery(unit)
            )
                return;
            TacticalCombatOrder order = group.ActiveOrder;
            if (random.NextInt(0, 3) != 0 || order == TacticalCombatOrder.HoldFire)
                return;
            TacticalCombatUnit primary = group.PrimaryTarget;
            int status = 2;
            if (primary != null)
            {
                if (GetStrongestArc(unit, primary.IsFighter) == null)
                    return;
                status = TryDesignatedTarget(unit, primary);
            }
            for (int index = 0; index < group.ComputedTargets.Count && status == 2; index++)
                status = TryDesignatedTarget(unit, group.ComputedTargets[index]);
            TractorTargetRequested?.Invoke(unit, primary);
            if (
                order is not TacticalCombatOrder.HoldFire and not TacticalCombatOrder.AttackFighters
            )
            {
                if (!HasLaserBattery(unit) || status != 2)
                    return;
            }
            spatialIndex.QueryFighters(unit, GetMaximumRange(unit), _neighbors);
            if (_neighbors.Count == 0)
                return;
            ClearScores();
            for (int index = 0; index < _neighbors.Count; index++)
            {
                TacticalCombatUnit candidate = _neighbors[index];
                TacticalWeaponArc arc = GetFiringArc(unit, candidate.Position - unit.Position);
                if (!unit.Weapons.IsReady(arc))
                    continue;
                float distance = (float)
                    TacticalCombatGeometry.MeasureTargetDistance(
                        candidate.Position - unit.Position
                    );
                ScoreOpportunity(unit, candidate, arc, TacticalWeaponType.LaserCannon, distance);
                ScoreOpportunity(unit, candidate, arc, TacticalWeaponType.Turbolaser, distance);
            }
            FireSelectedBattery(unit);
        }

        /// <summary>
        /// Finds the highest-strength battery for a specified target category.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="targetIsFighter">Whether the target is a squadron.</param>
        /// <returns>The strongest clockwise battery, or null when no battery can damage the target.</returns>
        public static TacticalWeaponArc? GetStrongestArc(
            TacticalCombatUnit unit,
            bool targetIsFighter
        )
        {
            TacticalWeaponArc? selected = null;
            float best = 0;
            for (int index = 0; index < 4; index++)
            {
                TacticalWeaponArc arc = (TacticalWeaponArc)index;
                double total =
                    (double)
                        unit.Weapons.GetTargetDamage(
                            arc,
                            TacticalWeaponType.IonCannon,
                            targetIsFighter
                        )
                    + unit.Weapons.GetTargetDamage(
                        arc,
                        TacticalWeaponType.Turbolaser,
                        targetIsFighter
                    )
                    + unit.Weapons.GetTargetDamage(
                        arc,
                        TacticalWeaponType.LaserCannon,
                        targetIsFighter
                    );
                if (total > best)
                {
                    best = (float)total;
                    selected = arc;
                }
            }
            return selected;
        }

        /// <summary>
        /// Gets the largest range of a weapon present on any battery.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <returns>The maximum active weapon range.</returns>
        public static float GetMaximumRange(TacticalCombatUnit unit)
        {
            float range = 0;
            for (int arc = 0; arc < 4; arc++)
            for (int weapon = 0; weapon < 3; weapon++)
                if (
                    unit.Weapons.GetStrength((TacticalWeaponArc)arc, (TacticalWeaponType)weapon)
                    != 0
                )
                    range = Math.Max(range, unit.Weapons.GetRange((TacticalWeaponType)weapon));
            return range;
        }

        /// <summary>
        /// Attempts a designated target without spending an opportunity on a different battery.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="target">The designated target.</param>
        /// <returns>One for unavailable charge, two for out of range, three for no damage, or four for a discharge.</returns>
        private int TryDesignatedTarget(TacticalCombatUnit unit, TacticalCombatUnit target)
        {
            if (target.IsFighter && !unit.Weapons.IsReady(TacticalWeaponArc.Forward))
                return 1;
            Vector3 delta = target.Position - unit.Position;
            float squaredDistance = (float)(
                (double)delta.X * delta.X + (double)delta.Y * delta.Y + (double)delta.Z * delta.Z
            );
            float range = GetMaximumRange(unit);
            double squaredRange = (double)range * range;
            if (double.IsNaN(squaredRange) || squaredRange < squaredDistance)
                return 2;
            ClearScores();
            TacticalWeaponArc arc = GetFiringArc(unit, delta);
            if (!unit.Weapons.IsReady(arc))
                return 1;
            float distance = (float)Math.Sqrt(squaredDistance);
            for (int weapon = 0; weapon < 3; weapon++)
            {
                TacticalWeaponType type = (TacticalWeaponType)weapon;
                float weaponRange = unit.Weapons.GetRange(type);
                if (
                    unit.Weapons.GetStrength(arc, type) == 0
                    || float.IsNaN(weaponRange)
                    || weaponRange < distance
                )
                    continue;
                _scores[(int)arc, weapon] = unit.Weapons.GetTargetDamage(
                    arc,
                    type,
                    target.IsFighter
                );
                _targets[(int)arc, weapon] = target;
            }
            return FireSelectedBattery(unit);
        }

        /// <summary>
        /// Selects a capital ship's facing battery or a squadron's shared forward battery.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="targetDelta">The target position relative to the firing unit.</param>
        /// <returns>The battery used to score the target.</returns>
        private static TacticalWeaponArc GetFiringArc(
            TacticalCombatUnit unit,
            Vector3 targetDelta
        ) =>
            unit.IsFighter
                ? TacticalWeaponArc.Forward
                : TacticalCombatGeometry.GetWeaponArc(unit.Heading, targetDelta);

        /// <summary>
        /// Retains a qualifying opportunity target for one battery lane.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="candidate">The candidate squadron.</param>
        /// <param name="arc">The candidate's facing battery.</param>
        /// <param name="weapon">The candidate weapon lane.</param>
        /// <param name="distance">The candidate's distance.</param>
        private void ScoreOpportunity(
            TacticalCombatUnit unit,
            TacticalCombatUnit candidate,
            TacticalWeaponArc arc,
            TacticalWeaponType weapon,
            float distance
        )
        {
            if (
                unit.Weapons.GetStrength(arc, weapon) == 0
                || !(unit.Weapons.GetRange(weapon) >= distance)
            )
                return;
            double unscaled = unit.Weapons.GetOpportunityDamage(arc, weapon);
            if (!(unscaled > _scores[(int)arc, (int)weapon]))
                return;
            _scores[(int)arc, (int)weapon] = unit.Systems.ScaleByHull((float)unscaled);
            _targets[(int)arc, (int)weapon] = candidate;
        }

        /// <summary>
        /// Discharges all qualifying lanes on the highest-scoring battery.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <returns>Three when no damage is available, otherwise four.</returns>
        private int FireSelectedBattery(TacticalCombatUnit unit)
        {
            int selected = -1;
            float best = 0;
            for (int arc = 0; arc < 4; arc++)
            {
                double total = (double)_scores[arc, 2] + _scores[arc, 1] + _scores[arc, 0];
                if (total > best)
                {
                    best = (float)total;
                    selected = arc;
                }
            }
            if (selected < 0)
                return 3;
            FireLane(unit, selected, TacticalWeaponType.IonCannon);
            FireLane(unit, selected, TacticalWeaponType.LaserCannon);
            FireLane(unit, selected, TacticalWeaponType.Turbolaser);
            return 4;
        }

        /// <summary>
        /// Queues one weapon lane and any fighter torpedoes triggered by its laser discharge.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="arc">The selected clockwise battery.</param>
        /// <param name="weapon">The selected weapon lane.</param>
        private void FireLane(TacticalCombatUnit unit, int arc, TacticalWeaponType weapon)
        {
            TacticalCombatUnit target = _targets[arc, (int)weapon];
            float strength = _scores[arc, (int)weapon];
            if (
                strength == 0
                || float.IsNaN(strength)
                || target?.IsTargetable != true
                || unit.Status == TacticalCombatUnitStatus.PreparingHyperspace
            )
                return;
            ShotRequested?.Invoke(new TacticalCombatShot(unit, target, weapon, strength));
            if (
                weapon == TacticalWeaponType.LaserCannon
                && unit.IsFighter
                && !target.IsFighter
                && CanFireTorpedo(unit, target)
            )
                ShotRequested?.Invoke(
                    new TacticalCombatShot(
                        unit,
                        target,
                        TacticalWeaponType.Torpedo,
                        unit.GetTorpedoDamage()
                    )
                );
            unit.Weapons.Discharge((TacticalWeaponArc)arc, weapon);
        }

        /// <summary>
        /// Checks the target's shield ratio and retained distance before a squadron's torpedo discharge.
        /// </summary>
        /// <param name="unit">The firing squadron.</param>
        /// <param name="target">The capital ship receiving the laser discharge.</param>
        /// <returns>Whether the shield and range comparisons admit an accompanying torpedo.</returns>
        private static bool CanFireTorpedo(TacticalCombatUnit unit, TacticalCombatUnit target)
        {
            double shields = target.Systems.GetShieldFraction();
            if (shields != 0 && !double.IsNaN(shields))
                return false;
            double distance = TacticalCombatGeometry.MeasureTargetDistance(
                target.Position - unit.Position
            );
            return double.IsNaN(distance) || !(distance > unit.TorpedoRange);
        }

        /// <summary>
        /// Clears the reusable battery scores and target slots.
        /// </summary>
        private void ClearScores()
        {
            Array.Clear(_scores, 0, _scores.Length);
            Array.Clear(_targets, 0, _targets.Length);
        }

        /// <summary>
        /// Checks for any charged battery before consuming a firing-opportunity draw.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <returns>Whether any battery is ready.</returns>
        private static bool HasReadyBattery(TacticalCombatUnit unit)
        {
            for (int arc = 0; arc < 4; arc++)
                if (unit.Weapons.IsReady((TacticalWeaponArc)arc))
                    return true;
            return false;
        }

        /// <summary>
        /// Checks the laser lanes used by opportunity targeting.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <returns>Whether the unit has any laser cannons.</returns>
        private static bool HasLaserBattery(TacticalCombatUnit unit)
        {
            for (int arc = 0; arc < 4; arc++)
                if (
                    unit.Weapons.GetStrength((TacticalWeaponArc)arc, TacticalWeaponType.LaserCannon)
                    != 0
                )
                    return true;
            return false;
        }
    }
}
