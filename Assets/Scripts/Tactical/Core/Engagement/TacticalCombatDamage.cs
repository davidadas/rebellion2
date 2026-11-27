using System;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
{
    internal sealed class TacticalCombatDamage
    {
        internal event Action<CombatUnit, TacticalSystemEvent> SystemChangeRequested;
        internal event Action<CombatUnit, string> LethalHit;
        internal event Action<
            CombatUnit,
            TacticalWeaponType,
            TacticalCombatImpactKind
        > ImpactApplied;

        /// <summary>
        /// Applies a queued component hit or repair.
        /// </summary>
        /// <param name="unit">The affected participant.</param>
        /// <param name="system">The affected component.</param>
        /// <param name="isRepair">Whether to repair one damage level.</param>
        internal static void ApplyComponentEvent(
            CombatUnit unit,
            TacticalShipSystem system,
            bool isRepair
        )
        {
            uint damage = unit.GetDamage(system);
            if (isRepair)
            {
                if (unit.DamageEventCount == 0)
                    return;
                unit.DamageEventCount--;
                if (system == TacticalShipSystem.Hyperdrive)
                    ChangeDriveDamage(unit, isRepair: true);
                else
                    unit.SetSystemDamage(system, damage - 1);
                return;
            }

            unit.DamageEventCount++;
            if (system == TacticalShipSystem.Hyperdrive)
                ChangeDriveDamage(unit, isRepair: false);
            else
                unit.SetSystemDamage(system, Math.Min(damage + 1, 4));
        }

        /// <summary>
        /// Damages the first working drive or repairs the last disabled drive in equipment order.
        /// </summary>
        /// <param name="unit">The affected participant.</param>
        /// <param name="isRepair">Whether the event repairs a drive.</param>
        private static void ChangeDriveDamage(CombatUnit unit, bool isRepair)
        {
            for (int position = 0; position < unit.DriveSlotCount; position++)
            {
                int index = isRepair ? unit.DriveSlotCount - 1 - position : position;
                if (!unit.HasHyperdriveAt(index))
                    continue;
                int damage = unit.GetDriveDamage(index);
                if (isRepair ? damage <= 0 : damage != 0)
                    continue;
                unit.SetDriveDamage(index, isRepair ? damage - 1 : 1);
                return;
            }
        }

        /// <summary>
        /// Gets the shield recharge available for one second and updates the fighter's stored sample.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <returns>The live recharge budget after hull and component damage.</returns>
        internal static double GetShieldRecharge(CombatUnit unit)
        {
            if (unit.IsFighter)
            {
                double recharge = (double)unit.Hull * 0.025f / 8.0f;
                unit.ShieldRechargeSample = (float)recharge;
                return recharge;
            }
            float scaled = unit.ScaleByHull(unit.ShieldRechargeSample);
            double remaining =
                scaled
                - (double)unit.ShieldRechargeSample
                    * 0.25f
                    * unit.GetDamage(TacticalShipSystem.Shields);
            return double.IsNaN(remaining) || remaining < 0 ? 0 : remaining;
        }

        /// <summary>
        /// Compares live recharge with its current stored rate, retaining the getter's sample update.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <returns>The recharge ratio, or zero when the stored rate is zero or unordered.</returns>
        internal static double GetShieldRechargeRatio(CombatUnit unit)
        {
            if (unit.ShieldRechargeSample == 0 || float.IsNaN(unit.ShieldRechargeSample))
                return 0;
            double recharge = GetShieldRecharge(unit);
            return recharge / unit.ShieldRechargeSample;
        }

        /// <summary>
        /// Recharges shields, recovers temporary delays, and requests due component repairs.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <param name="elapsedMilliseconds">The system update interval in milliseconds.</param>
        /// <param name="random">The battle's random-number stream.</param>
        internal void AdvanceSystems(
            CombatUnit unit,
            float elapsedMilliseconds,
            IRandomNumberProvider random
        )
        {
            float seconds = elapsedMilliseconds * 0.001f;
            if (unit.IsFighter)
            {
                double limit = (double)unit.Hull * unit.ShieldLimit / unit.MaximumHull;
                unit.ShieldLimit = (float)limit;
                unit.Shields = (float)(limit > unit.Shields ? unit.Shields : limit);
            }
            double recharge = GetShieldRecharge(unit) * seconds;
            if (unit.ShieldLimit - unit.Shields > 0)
            {
                double charged = recharge + unit.Shields;
                unit.Shields = (float)(charged >= unit.ShieldLimit ? unit.ShieldLimit : charged);
            }
            if (unit.IsFighter)
                return;

            unit.MovementDelay = DecayDelay(unit.MovementDelay, elapsedMilliseconds);
            unit.AttackDelay = DecayDelay(unit.AttackDelay, elapsedMilliseconds);
            if (unit.DamageEventCount == 0)
                return;
            unit.RepairMilliseconds -= elapsedMilliseconds;
            if (unit.RepairMilliseconds > 0)
                return;
            unit.RepairMilliseconds = 50000;
            RequestComponentRepair(unit, random);
        }

        /// <summary>
        /// Stores elapsed seconds before reducing a positive disruption delay.
        /// </summary>
        /// <param name="delay">The stored disruption delay in seconds.</param>
        /// <param name="elapsedMilliseconds">The elapsed repair interval in milliseconds.</param>
        /// <returns>The remaining delay after its ordered lower-bound comparison.</returns>
        private static float DecayDelay(float delay, float elapsedMilliseconds)
        {
            if (!(delay > 0))
                return delay;
            float seconds = (float)((double)elapsedMilliseconds * 0.001f);
            float remaining = delay - seconds;
            return remaining < 0 || float.IsNaN(remaining) ? 0 : remaining;
        }

        /// <summary>
        /// Applies one weapon impact and requests its resulting component events.
        /// </summary>
        /// <param name="unit">The unit receiving the impact.</param>
        /// <param name="weapon">The impacting weapon.</param>
        /// <param name="damage">The queued attack strength.</param>
        /// <param name="ownTurnBudget">The target's current maneuver rating.</param>
        /// <param name="attackerTurnBudget">The attacker's current maneuver rating.</param>
        /// <param name="random">The battle's random-number stream.</param>
        /// <param name="attackerInstanceID">The firing unit's identity for a lethal impact report.</param>
        /// <param name="playSound">The encounter audio callback, when playback is available.</param>
        /// <returns>The affected surface, destruction, or no impact for an inactive target.</returns>
        internal TacticalCombatImpactKind ApplyHit(
            CombatUnit unit,
            TacticalWeaponType weapon,
            float damage,
            float ownTurnBudget,
            float attackerTurnBudget,
            IRandomNumberProvider random,
            string attackerInstanceID,
            Action<TacticalCombatSound> playSound = null
        )
        {
            if (!unit.IsAlive)
            {
                unit.Hull = 0;
                return TacticalCombatImpactKind.None;
            }

            float previousHull = unit.Hull;
            float previousShields = unit.Shields;
            double applied = damage;
            if (unit.IsFighter && weapon != TacticalWeaponType.Torpedo)
            {
                float ratio = (float)((double)ownTurnBudget / attackerTurnBudget);
                applied /= Math.Max(ratio, 0.1f);
            }
            applied = Math.Max(applied, 1);
            double remainingShields = (double)previousShields - applied;
            unit.Shields = (float)remainingShields;
            if (!(remainingShields >= 0))
            {
                unit.Shields = 0;
                if (weapon == TacticalWeaponType.IonCannon)
                {
                    playSound?.Invoke(TacticalCombatAudio.GetImpactSound(weapon, true));
                    ImpactApplied?.Invoke(unit, weapon, TacticalCombatImpactKind.Hull);
                    if (!unit.IsFighter)
                        RequestIonDisruption(unit, (float)-remainingShields, random);
                    return TacticalCombatImpactKind.Hull;
                }

                float remainingHull = (float)(remainingShields + previousHull);
                unit.Hull = remainingHull;
                playSound?.Invoke(TacticalCombatAudio.GetImpactSound(weapon, true));
                if (!unit.IsAlive)
                {
                    LethalHit?.Invoke(unit, attackerInstanceID);
                    unit.Hull = 0;
                    ImpactApplied?.Invoke(unit, weapon, TacticalCombatImpactKind.Destroyed);
                    playSound?.Invoke(TacticalCombatAudio.GetExplosionSound(unit));
                    return TacticalCombatImpactKind.Destroyed;
                }

                ImpactApplied?.Invoke(unit, weapon, TacticalCombatImpactKind.Hull);
                if (!unit.IsFighter)
                    RequestHullHitEvent(unit, previousHull, random);
                return TacticalCombatImpactKind.Hull;
            }

            playSound?.Invoke(TacticalCombatAudio.GetImpactSound(weapon, false));
            if (!unit.IsFighter)
            {
                double progress =
                    100.0 / previousShields * ((double)previousShields - unit.Shields);
                float chance = progress < 1 ? 1 : (float)progress;
                if (!(random.NextInt(0, 101) > chance))
                    SystemChangeRequested?.Invoke(
                        unit,
                        new TacticalSystemEvent(TacticalShipSystem.Shields, false)
                    );
            }
            ImpactApplied?.Invoke(unit, weapon, TacticalCombatImpactKind.Shield);
            return TacticalCombatImpactKind.Shield;
        }

        /// <summary>
        /// Requests the component event selected after a surviving hull impact.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <param name="previousHull">The hull strength before impact.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestHullHitEvent(
            CombatUnit unit,
            float previousHull,
            IRandomNumberProvider random
        )
        {
            double score =
                ((double)unit.Hull - previousHull) * 100 / previousHull + random.NextInt(0, 101);
            if (!(score > 50))
                return;
            float roundedScore = (float)score;
            TacticalShipSystem system =
                roundedScore <= 65 ? TacticalShipSystem.Shields
                : roundedScore <= 80 ? TacticalShipSystem.Weapons
                : roundedScore <= 90 ? TacticalShipSystem.TractorBeam
                : roundedScore <= 95 ? TacticalShipSystem.Engines
                : TacticalShipSystem.Hyperdrive;
            if (roundedScore > 100)
            {
                unit.Hull = 0;
                return;
            }
            SystemChangeRequested?.Invoke(unit, new TacticalSystemEvent(system, false));
        }

        /// <summary>
        /// Requests accumulated attack and movement delays and the selected batteries after ion damage.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <param name="excessDamage">The ion strength past the shields.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestIonDisruption(
            CombatUnit unit,
            float excessDamage,
            IRandomNumberProvider random
        )
        {
            float attackDelay = 0;
            float movementDelay = 0;
            int arcs = 0;
            while (excessDamage > 0)
            {
                int component = random.NextInt(1, 11);
                if (component == 1)
                    attackDelay = (float)((double)random.NextInt(30, 51) + attackDelay);
                else if (component == 2)
                    movementDelay = (float)((double)random.NextInt(30, 51) + movementDelay);
                else
                    arcs |= 1 << ((component - 3) >> 1);
                excessDamage = (float)((double)excessDamage - 1);
            }
            SystemChangeRequested?.Invoke(
                unit,
                new TacticalSystemEvent(attackDelay, movementDelay, arcs)
            );
        }

        /// <summary>
        /// Selects a damaged component after a successful damage-control roll.
        /// </summary>
        /// <param name="unit">The battle unit undergoing repairs.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestComponentRepair(CombatUnit unit, IRandomNumberProvider random)
        {
            if (
                unit.DamageControl == 0
                || float.IsNaN(unit.DamageControl)
                || random.NextInt(1, 101) > unit.DamageControl
            )
                return;
            uint roll =
                unit.DamageEventCount > 1
                    ? (uint)random.NextInt(1, (int)unit.DamageEventCount + 1)
                    : 1;
            TacticalShipSystem[] repairOrder =
            {
                TacticalShipSystem.Engines,
                TacticalShipSystem.Shields,
                TacticalShipSystem.Hyperdrive,
                TacticalShipSystem.TractorBeam,
                TacticalShipSystem.Weapons,
            };
            foreach (TacticalShipSystem system in repairOrder)
            {
                uint damage = unit.GetDamage(system);
                if (roll <= damage)
                {
                    SystemChangeRequested?.Invoke(unit, new TacticalSystemEvent(system, true));
                    return;
                }
                roll -= damage;
            }
        }
    }

    /// <summary>
    /// Identifies the surface reached by an applied tactical weapon impact.
    /// </summary>
    public enum TacticalCombatImpactKind
    {
        None,
        Shield,
        Hull,
        Destroyed,
    }

    /// <summary>
    /// Identifies a persistent component change or temporary ion disruption.
    /// </summary>
    public enum TacticalSystemEventKind
    {
        Damage,
        Repair,
        Disruption,
    }

    /// <summary>
    /// Describes a system event awaiting application by the battle's command queue.
    /// </summary>
    public readonly struct TacticalSystemEvent
    {
        public TacticalShipSystem System { get; }
        public TacticalSystemEventKind Kind { get; }
        public bool IsRepair => Kind == TacticalSystemEventKind.Repair;

        /// <summary>
        /// Gets the attack timer increment for disruption, or null for a component change.
        /// </summary>
        public float? AttackDelay { get; }

        /// <summary>
        /// Gets the movement timer increment for disruption, or null for a component change.
        /// </summary>
        public float? MovementDelay { get; }
        public int DischargedArcMask { get; }

        /// <summary>
        /// Creates a persistent component damage or repair event.
        /// </summary>
        /// <param name="system">The affected ship system.</param>
        /// <param name="isRepair">Whether the event repairs one damage level.</param>
        public TacticalSystemEvent(TacticalShipSystem system, bool isRepair)
        {
            System = system;
            Kind = isRepair ? TacticalSystemEventKind.Repair : TacticalSystemEventKind.Damage;
            AttackDelay = null;
            MovementDelay = null;
            DischargedArcMask = 0;
        }

        /// <summary>
        /// Creates a disruption event with explicit timer increments and discharged batteries.
        /// </summary>
        /// <param name="attackDelay">The additional attack delay in seconds.</param>
        /// <param name="movementDelay">The additional movement delay in seconds.</param>
        /// <param name="dischargedArcMask">The batteries whose charge must be cleared.</param>
        public TacticalSystemEvent(float attackDelay, float movementDelay, int dischargedArcMask)
        {
            System = TacticalShipSystem.Weapons;
            Kind = TacticalSystemEventKind.Disruption;
            AttackDelay = attackDelay;
            MovementDelay = movementDelay;
            DischargedArcMask = dischargedArcMask;
        }
    }
}
