using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Captures a weapon discharge and its delayed impact.
    /// </summary>
    public readonly struct TacticalCombatShot
    {
        public CombatUnit Source { get; }
        public CombatUnit Target { get; }
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
            CombatUnit source,
            CombatUnit target,
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
        private readonly CombatUnit[,] _targets = new CombatUnit[4, 3];
        private readonly List<CombatUnit> _neighbors = new();
        public event Action<TacticalCombatShot> ShotRequested;
        public event Action<CombatUnit, CombatUnit> TractorTargetRequested;

        private const float _shotScale = 0.26666668f;
        private const float _laserCapitalScale = 0.167f;

        /// <summary>
        /// Attempts designated targets first, followed by opportunity shots at nearby fighters.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="group">The unit's orders and designated targets.</param>
        /// <param name="spatialIndex">The active battle spatial index.</param>
        /// <param name="random">The battle's firing-opportunity stream.</param>
        public void AcquireTargets(
            CombatUnit unit,
            TacticalCombatGroup group,
            TacticalCombatSpatialIndex spatialIndex,
            IRandomNumberProvider random
        )
        {
            if (
                !unit.IsTargetable
                || unit.Status == CombatUnitStatus.PreparingHyperspace
                || !HasReadyBattery(unit)
            )
                return;
            TacticalCombatOrder order = group.ActiveOrder;
            if (random.NextInt(0, 3) != 0 || order == TacticalCombatOrder.HoldFire)
                return;
            CombatUnit primary = group.PrimaryTarget;
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
                CombatUnit candidate = _neighbors[index];
                TacticalWeaponArc arc = GetFiringArc(unit, candidate.Position - unit.Position);
                if (!unit.IsBatteryReady(arc))
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
        public static TacticalWeaponArc? GetStrongestArc(CombatUnit unit, bool targetIsFighter)
        {
            TacticalWeaponArc? selected = null;
            float best = 0;
            for (int index = 0; index < 4; index++)
            {
                TacticalWeaponArc arc = (TacticalWeaponArc)index;
                double total =
                    (double)
                        TacticalCombatFireControl.GetTargetDamage(
                            unit,
                            arc,
                            TacticalWeaponType.IonCannon,
                            targetIsFighter
                        )
                    + TacticalCombatFireControl.GetTargetDamage(
                        unit,
                        arc,
                        TacticalWeaponType.Turbolaser,
                        targetIsFighter
                    )
                    + TacticalCombatFireControl.GetTargetDamage(
                        unit,
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
        public static float GetMaximumRange(CombatUnit unit)
        {
            float range = 0;
            for (int arc = 0; arc < 4; arc++)
            for (int weapon = 0; weapon < 3; weapon++)
                if (unit.GetWeaponStrength((TacticalWeaponArc)arc, (TacticalWeaponType)weapon) != 0)
                    range = Math.Max(
                        range,
                        unit.GetWeaponRange((TacticalWeaponArc)arc, (TacticalWeaponType)weapon)
                    );
            return range;
        }

        /// <summary>
        /// Attempts a designated target without spending an opportunity on a different battery.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="target">The designated target.</param>
        /// <returns>One for unavailable charge, two for out of range, three for no damage, or four for a discharge.</returns>
        private int TryDesignatedTarget(CombatUnit unit, CombatUnit target)
        {
            if (target.IsFighter && !unit.IsBatteryReady(TacticalWeaponArc.Forward))
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
            if (!unit.IsBatteryReady(arc))
                return 1;
            float distance = (float)Math.Sqrt(squaredDistance);
            for (int weapon = 0; weapon < 3; weapon++)
            {
                TacticalWeaponType type = (TacticalWeaponType)weapon;
                float weaponRange = unit.GetWeaponRange(arc, type);
                if (
                    unit.GetWeaponStrength(arc, type) == 0
                    || float.IsNaN(weaponRange)
                    || weaponRange < distance
                )
                    continue;
                _scores[(int)arc, weapon] = TacticalCombatFireControl.GetTargetDamage(
                    unit,
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
        private static TacticalWeaponArc GetFiringArc(CombatUnit unit, Vector3 targetDelta) =>
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
            CombatUnit unit,
            CombatUnit candidate,
            TacticalWeaponArc arc,
            TacticalWeaponType weapon,
            float distance
        )
        {
            if (
                unit.GetWeaponStrength(arc, weapon) == 0
                || !(unit.GetWeaponRange(arc, weapon) >= distance)
            )
                return;
            double unscaled = TacticalCombatFireControl.GetOpportunityDamage(unit, arc, weapon);
            if (!(unscaled > _scores[(int)arc, (int)weapon]))
                return;
            _scores[(int)arc, (int)weapon] = unit.ScaleByHull((float)unscaled);
            _targets[(int)arc, (int)weapon] = candidate;
        }

        /// <summary>
        /// Discharges all qualifying lanes on the highest-scoring battery.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <returns>Three when no damage is available, otherwise four.</returns>
        private int FireSelectedBattery(CombatUnit unit)
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
        private void FireLane(CombatUnit unit, int arc, TacticalWeaponType weapon)
        {
            CombatUnit target = _targets[arc, (int)weapon];
            float strength = _scores[arc, (int)weapon];
            if (
                strength == 0
                || float.IsNaN(strength)
                || target?.IsTargetable != true
                || unit.Status == CombatUnitStatus.PreparingHyperspace
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
            unit.DischargeBattery((TacticalWeaponArc)arc, weapon);
        }

        /// <summary>
        /// Checks the target's shield ratio and retained distance before a squadron's torpedo discharge.
        /// </summary>
        /// <param name="unit">The firing squadron.</param>
        /// <param name="target">The capital ship receiving the laser discharge.</param>
        /// <returns>Whether the shield and range comparisons admit an accompanying torpedo.</returns>
        private static bool CanFireTorpedo(CombatUnit unit, CombatUnit target)
        {
            double shields = target.GetShieldFraction();
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
        private static bool HasReadyBattery(CombatUnit unit)
        {
            for (int arc = 0; arc < 4; arc++)
                if (unit.IsBatteryReady((TacticalWeaponArc)arc))
                    return true;
            return false;
        }

        /// <summary>
        /// Checks the laser lanes used by opportunity targeting.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <returns>Whether the unit has any laser cannons.</returns>
        private static bool HasLaserBattery(CombatUnit unit)
        {
            for (int arc = 0; arc < 4; arc++)
                if (
                    unit.GetWeaponStrength((TacticalWeaponArc)arc, TacticalWeaponType.LaserCannon)
                    != 0
                )
                    return true;
            return false;
        }

        /// <summary>
        /// Calculates a weapon's attack strength against a designated target.
        /// </summary>
        /// <param name="unit">The unit firing the weapon.</param>
        /// <param name="arc">The firing battery.</param>
        /// <param name="weapon">The battery weapon.</param>
        /// <param name="targetIsFighter">Whether the designated target is a fighter squadron.</param>
        /// <returns>The queued weapon attack strength.</returns>
        internal static float GetTargetDamage(
            CombatUnit unit,
            TacticalWeaponArc arc,
            TacticalWeaponType weapon,
            bool targetIsFighter
        )
        {
            if (weapon == TacticalWeaponType.IonCannon && targetIsFighter)
                return 0;
            float targetScale = GetTargetScale(weapon, targetIsFighter);
            return (float)((double)_shotScale * targetScale * unit.GetWeaponStrength(arc, weapon));
        }

        /// <summary>
        /// Gets the rounded per-mount scalar used before recharge and mount count are applied.
        /// </summary>
        /// <param name="weapon">The battery weapon.</param>
        /// <param name="targetIsFighter">Whether the designated target is a fighter squadron.</param>
        /// <returns>The single-precision lane scalar.</returns>
        internal static float GetRangeScalar(TacticalWeaponType weapon, bool targetIsFighter)
        {
            if (weapon == TacticalWeaponType.IonCannon && targetIsFighter)
                return 0;
            float targetScale = GetTargetScale(weapon, targetIsFighter);
            return (float)((double)_shotScale * targetScale);
        }

        /// <summary>
        /// Selects the configured lane multiplier for the target category.
        /// </summary>
        /// <param name="weapon">The battery weapon.</param>
        /// <param name="targetIsFighter">Whether the target is a fighter squadron.</param>
        /// <returns>The target-category multiplier.</returns>
        private static float GetTargetScale(TacticalWeaponType weapon, bool targetIsFighter) =>
            weapon == TacticalWeaponType.LaserCannon
                ? targetIsFighter
                    ? 1
                    : _laserCapitalScale
                : targetIsFighter
                    ? 0.5f
                    : 1;

        /// <summary>
        /// Calculates an opportunity shot's complete attack strength while scanning nearby targets.
        /// </summary>
        /// <param name="unit">The unit firing the weapon.</param>
        /// <param name="arc">The firing battery.</param>
        /// <param name="weapon">The battery weapon.</param>
        /// <returns>The opportunity-shot attack strength before hull scaling.</returns>
        internal static double GetOpportunityDamage(
            CombatUnit unit,
            TacticalWeaponArc arc,
            TacticalWeaponType weapon
        )
        {
            if (weapon == TacticalWeaponType.IonCannon)
                return 0;
            float targetScale = weapon == TacticalWeaponType.Turbolaser ? 0.5f : 1;
            return (double)_shotScale * targetScale * unit.GetWeaponStrength(arc, weapon);
        }
    }
}
