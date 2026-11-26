using System;
using Rebellion.Game.Combat;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
{
    internal static class TacticalCombatDamage
    {
        /// <summary>
        /// Recharges shields, recovers temporary delays, and requests due component repairs.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <param name="elapsedMilliseconds">The system update interval in milliseconds.</param>
        /// <param name="random">The battle's random-number stream.</param>
        internal static void AdvanceSystems(
            this CombatUnit unit,
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
            double recharge = unit.GetShieldRecharge() * seconds;
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
        /// <returns>The affected surface, destruction, or no impact for an inactive target.</returns>
        internal static TacticalCombatImpactKind ApplyHit(
            this CombatUnit unit,
            TacticalWeaponType weapon,
            float damage,
            float ownTurnBudget,
            float attackerTurnBudget,
            IRandomNumberProvider random,
            string attackerInstanceID
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
                    unit.NotifySound(GetImpactSound(weapon, true));
                    unit.NotifyImpact(weapon, TacticalCombatImpactKind.Hull);
                    if (!unit.IsFighter)
                        RequestIonDisruption(unit, (float)-remainingShields, random);
                    return TacticalCombatImpactKind.Hull;
                }

                float remainingHull = (float)(remainingShields + previousHull);
                unit.Hull = remainingHull;
                unit.NotifySound(GetImpactSound(weapon, true));
                if (!unit.IsAlive)
                {
                    unit.NotifyLethalHit(attackerInstanceID);
                    unit.Hull = 0;
                    unit.NotifyImpact(weapon, TacticalCombatImpactKind.Destroyed);
                    unit.NotifySound(unit.GetExplosionSound());
                    return TacticalCombatImpactKind.Destroyed;
                }

                unit.NotifyImpact(weapon, TacticalCombatImpactKind.Hull);
                if (!unit.IsFighter)
                    RequestHullHitEvent(unit, previousHull, random);
                return TacticalCombatImpactKind.Hull;
            }

            unit.NotifySound(GetImpactSound(weapon, false));
            if (!unit.IsFighter)
            {
                double progress =
                    100.0 / previousShields * ((double)previousShields - unit.Shields);
                float chance = progress < 1 ? 1 : (float)progress;
                if (!(random.NextInt(0, 101) > chance))
                    unit.NotifySystemEvent(
                        new TacticalSystemEvent(TacticalShipSystem.Shields, false)
                    );
            }
            unit.NotifyImpact(weapon, TacticalCombatImpactKind.Shield);
            return TacticalCombatImpactKind.Shield;
        }

        /// <summary>
        /// Selects the explosion size from the captured unit category and complete hull.
        /// </summary>
        /// <returns>The unit's destruction sound.</returns>
        internal static TacticalCombatSound GetExplosionSound(this CombatUnit unit) =>
            unit.IsFighter || !(unit.MaximumHull >= 1100) ? TacticalCombatSound.SmallExplosion
            : unit.MaximumHull <= 2000 ? TacticalCombatSound.MediumExplosion
            : TacticalCombatSound.LargeExplosion;

        /// <summary>
        /// Selects the impact sound family before any resulting component-damage roll.
        /// </summary>
        /// <param name="weapon">The impacting weapon.</param>
        /// <param name="hull">Whether the impact passed through the shields.</param>
        /// <returns>The sound associated with this impact.</returns>
        private static TacticalCombatSound GetImpactSound(TacticalWeaponType weapon, bool hull) =>
            weapon switch
            {
                TacticalWeaponType.LaserCannon => hull
                    ? TacticalCombatSound.LaserHullHit
                    : TacticalCombatSound.LaserShieldHit,
                TacticalWeaponType.IonCannon => hull
                    ? TacticalCombatSound.IonHullHit
                    : TacticalCombatSound.IonShieldHit,
                _ => hull ? TacticalCombatSound.HeavyHullHit : TacticalCombatSound.HeavyShieldHit,
            };

        /// <summary>
        /// Requests the component event selected after a surviving hull impact.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <param name="previousHull">The hull strength before impact.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private static void RequestHullHitEvent(
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
            unit.NotifySystemEvent(new TacticalSystemEvent(system, false));
        }

        /// <summary>
        /// Requests accumulated attack and movement delays and the selected batteries after ion damage.
        /// </summary>
        /// <param name="unit">The affected battle unit.</param>
        /// <param name="excessDamage">The ion strength past the shields.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private static void RequestIonDisruption(
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
            unit.NotifySystemEvent(new TacticalSystemEvent(attackDelay, movementDelay, arcs));
        }

        /// <summary>
        /// Selects a damaged component after a successful damage-control roll.
        /// </summary>
        /// <param name="random">The battle's random-number stream.</param>
        private static void RequestComponentRepair(CombatUnit unit, IRandomNumberProvider random)
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
                    unit.NotifySystemEvent(new TacticalSystemEvent(system, true));
                    return;
                }
                roll -= damage;
            }
        }
    }
}
