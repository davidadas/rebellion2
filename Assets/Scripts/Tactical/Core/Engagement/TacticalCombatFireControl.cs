using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
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
        public HardpointWeaponType Weapon { get; }
        public float Strength { get; }
        public int ImpactDelay { get; }
        internal TacticalWeaponArc? Arc { get; }
        public int FiringDelay => Weapon == HardpointWeaponType.IonCannon ? 1 : 0;

        /// <summary>
        /// Selects the original discharge cue for a weapon without an authored sound.
        /// </summary>
        /// <param name="factionIndex">The firing side's authored faction index.</param>
        /// <returns>The cue requested when the discharge is applied.</returns>
        internal TacticalCombatSound GetDefaultFiringSound(int factionIndex) =>
            Weapon switch
            {
                HardpointWeaponType.LaserCannon when Source.IsFighter => factionIndex == 0
                    ? TacticalCombatSound.AllianceFighterLaser
                    : TacticalCombatSound.EmpireFighterLaser,
                HardpointWeaponType.LaserCannon => TacticalCombatSound.CapitalLaser,
                HardpointWeaponType.IonCannon => Source.IsFighter
                    ? TacticalCombatSound.FighterIon
                    : TacticalCombatSound.CapitalIon,
                _ => TacticalCombatSound.HeavyWeapon,
            };

        /// <summary>
        /// Creates a discharge after firing control selects a battery and target.
        /// </summary>
        /// <param name="source">The firing unit.</param>
        /// <param name="target">The unit receiving the shot.</param>
        /// <param name="weapon">The weapon used.</param>
        /// <param name="strength">The attack strength before target damage handling.</param>
        /// <param name="arc">The firing battery when the shot uses an installed weapon.</param>
        public TacticalCombatShot(
            CombatUnit source,
            CombatUnit target,
            HardpointWeaponType weapon,
            float strength,
            TacticalWeaponArc? arc = null
        )
        {
            Source = source;
            Target = target;
            Weapon = weapon;
            Strength = strength;
            Arc = arc;
            ImpactDelay = weapon
                is HardpointWeaponType.IonCannon
                    or HardpointWeaponType.TorpedoLauncher
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
        private const int _laserLane = 0;
        private const int _ionLane = 1;
        private const int _turbolaserLane = 2;
        private static readonly HardpointWeaponType[] _batteryWeapons =
        {
            HardpointWeaponType.LaserCannon,
            HardpointWeaponType.IonCannon,
            HardpointWeaponType.Turbolaser,
        };

        private readonly float[,] _scores = new float[4, _batteryWeapons.Length];
        private readonly CombatUnit[,] _targets = new CombatUnit[4, _batteryWeapons.Length];
        private readonly List<CombatUnit> _neighbors = new();
        public event Action<TacticalCombatShot> ShotRequested;
        public event Action<CombatUnit, CombatUnit> TractorTargetRequested;

        private const float _shotScale = 0.26666668f;
        private const float _laserCapitalScale = 0.167f;

        /// <summary>
        /// Establishes the initial charge and readiness for the deployed participant's batteries.
        /// </summary>
        /// <param name="unit">The deployed participant.</param>
        internal static void InitializeBatteries(CombatUnit unit)
        {
            bool isFighter = unit.IsFighter;
            uint weaponDamage = isFighter ? 0 : unit.GetDamage(TacticalShipSystem.Weapons);
            for (int index = 0; index < 4; index++)
            {
                TacticalWeaponArc arc = (TacticalWeaponArc)index;
                float capacity = unit.GetBatteryCapacity(arc);
                unit.SetBatteryCharge(arc, !isFighter && capacity > 0 ? capacity : 0, capacity > 0);
                unit.ResetBatteryRechargePosition(arc);
                if (!isFighter && capacity > 0 && weaponDamage >= 4)
                    unit.ClearBattery(arc);
            }
        }

        /// <summary>
        /// Calculates the squadron's torpedo attack after entry and battle losses.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <returns>The torpedo attack strength.</returns>
        internal static float GetTorpedoDamage(CombatUnit unit) => (float)GetTorpedoAttack(unit);

        /// <summary>
        /// Retains the hull-scaled torpedo contribution until its consumer chooses the stored boundary.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <returns>The live torpedo attack strength after the per-mount scalar store.</returns>
        internal static double GetTorpedoAttack(CombatUnit unit)
        {
            float strength = (float)(
                (double)(
                    unit.Unit is Starfighter fighter
                        ? fighter.Torpedoes * fighter.MaxSquadronSize
                        : 0
                ) * 0.53333336f
            );
            return (double)unit.Hull * strength / unit.MaximumHull;
        }

        /// <summary>
        /// Gets the weapon recharge available for one second.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <returns>The weapon recharge budget after hull and component damage.</returns>
        internal static double GetWeaponRecharge(CombatUnit unit)
        {
            if (unit.IsFighter)
            {
                double recharge = (double)unit.Hull * 3.751f / 8.0f;
                unit.WeaponRechargeSample = (float)recharge;
                return recharge;
            }
            float scaled = unit.ScaleByHull(unit.WeaponRechargeSample);
            double remaining =
                scaled
                - (double)unit.WeaponRechargeSample
                    * 0.25f
                    * unit.GetDamage(TacticalShipSystem.Weapons);
            return double.IsNaN(remaining) || remaining < 0 ? 0 : remaining;
        }

        /// <summary>
        /// Compares live weapon recharge with its stored rate after the getter updates its sample.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <returns>The recharge ratio, or zero when the stored rate is zero or unordered.</returns>
        internal static double GetWeaponRechargeRatio(CombatUnit unit)
        {
            if (unit.WeaponRechargeSample == 0 || float.IsNaN(unit.WeaponRechargeSample))
                return 0;
            double recharge = GetWeaponRecharge(unit);
            return recharge / unit.WeaponRechargeSample;
        }

        /// <summary>
        /// Advances queued batteries using the unit's recharge budget.
        /// </summary>
        /// <param name="unit">The ship or squadron whose batteries are recharging.</param>
        /// <param name="seconds">The elapsed recharge interval in seconds.</param>
        /// <param name="rechargeRate">The unit's available recharge per second.</param>
        internal static void RechargeBatteries(CombatUnit unit, float seconds, double rechargeRate)
        {
            double available = rechargeRate * seconds;
            for (int order = 0; order < unit.QueuedBatteryCount && available > 0; order++)
            {
                TacticalWeaponArc arc = unit.GetQueuedBattery(order);
                float capacity = unit.GetBatteryCapacity(arc);
                float charge = unit.GetBatteryCharge(arc);
                double remaining = (double)capacity - charge;
                if (double.IsNaN(remaining) || available < remaining)
                {
                    unit.SetBatteryCharge(arc, (float)(available + charge));
                    // Partial fills retain the remaining budget for subsequent batteries.
                }
                else
                {
                    unit.SetBatteryCharge(arc, capacity, true);
                    available -= remaining;
                }
            }

            for (int order = 0; order < unit.QueuedBatteryCount; order++)
                if (unit.IsBatteryReady(unit.GetQueuedBattery(order)))
                    unit.RemoveQueuedBattery(order);
        }

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
                ScoreOpportunity(unit, candidate, arc, _laserLane, distance);
                ScoreOpportunity(unit, candidate, arc, _turbolaserLane, distance);
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
                            HardpointWeaponType.IonCannon,
                            targetIsFighter
                        )
                    + TacticalCombatFireControl.GetTargetDamage(
                        unit,
                        arc,
                        HardpointWeaponType.Turbolaser,
                        targetIsFighter
                    )
                    + TacticalCombatFireControl.GetTargetDamage(
                        unit,
                        arc,
                        HardpointWeaponType.LaserCannon,
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
                foreach (HardpointWeaponType weapon in _batteryWeapons)
                    if (unit.GetWeaponStrength((TacticalWeaponArc)arc, weapon) != 0)
                        range = Math.Max(
                            range,
                            unit.GetWeaponRange((TacticalWeaponArc)arc, weapon)
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
            for (int weapon = 0; weapon < _batteryWeapons.Length; weapon++)
            {
                HardpointWeaponType type = _batteryWeapons[weapon];
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
        /// <param name="lane">The candidate weapon lane.</param>
        /// <param name="distance">The candidate's distance.</param>
        private void ScoreOpportunity(
            CombatUnit unit,
            CombatUnit candidate,
            TacticalWeaponArc arc,
            int lane,
            float distance
        )
        {
            HardpointWeaponType weapon = _batteryWeapons[lane];
            if (
                unit.GetWeaponStrength(arc, weapon) == 0
                || !(unit.GetWeaponRange(arc, weapon) >= distance)
            )
                return;
            double unscaled = TacticalCombatFireControl.GetOpportunityDamage(unit, arc, weapon);
            if (!(unscaled > _scores[(int)arc, lane]))
                return;
            _scores[(int)arc, lane] = unit.ScaleByHull((float)unscaled);
            _targets[(int)arc, lane] = candidate;
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
                double total =
                    (double)_scores[arc, _turbolaserLane]
                    + _scores[arc, _ionLane]
                    + _scores[arc, _laserLane];
                if (total > best)
                {
                    best = (float)total;
                    selected = arc;
                }
            }
            if (selected < 0)
                return 3;
            FireLane(unit, selected, _ionLane);
            FireLane(unit, selected, _laserLane);
            FireLane(unit, selected, _turbolaserLane);
            return 4;
        }

        /// <summary>
        /// Queues one weapon lane and any fighter torpedoes triggered by its laser discharge.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="arc">The selected clockwise battery.</param>
        /// <param name="lane">The selected weapon lane.</param>
        private void FireLane(CombatUnit unit, int arc, int lane)
        {
            CombatUnit target = _targets[arc, lane];
            float strength = _scores[arc, lane];
            HardpointWeaponType weapon = _batteryWeapons[lane];
            if (
                strength == 0
                || float.IsNaN(strength)
                || target?.IsTargetable != true
                || unit.Status == CombatUnitStatus.PreparingHyperspace
            )
                return;
            ShotRequested?.Invoke(
                new TacticalCombatShot(unit, target, weapon, strength, (TacticalWeaponArc)arc)
            );
            if (
                weapon == HardpointWeaponType.LaserCannon
                && unit.IsFighter
                && !target.IsFighter
                && CanFireTorpedo(unit, target)
            )
                ShotRequested?.Invoke(
                    new TacticalCombatShot(
                        unit,
                        target,
                        HardpointWeaponType.TorpedoLauncher,
                        GetTorpedoDamage(unit)
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
                    unit.GetWeaponStrength((TacticalWeaponArc)arc, HardpointWeaponType.LaserCannon)
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
            HardpointWeaponType weapon,
            bool targetIsFighter
        )
        {
            if (weapon == HardpointWeaponType.IonCannon && targetIsFighter)
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
        internal static float GetRangeScalar(HardpointWeaponType weapon, bool targetIsFighter)
        {
            if (weapon == HardpointWeaponType.IonCannon && targetIsFighter)
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
        private static float GetTargetScale(HardpointWeaponType weapon, bool targetIsFighter) =>
            weapon == HardpointWeaponType.LaserCannon
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
            HardpointWeaponType weapon
        )
        {
            if (weapon == HardpointWeaponType.IonCannon)
                return 0;
            float targetScale = weapon == HardpointWeaponType.Turbolaser ? 0.5f : 1;
            return (double)_shotScale * targetScale * unit.GetWeaponStrength(arc, weapon);
        }
    }
}
