using System;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies a ship system that can suffer damage or receive repairs in combat.
    /// </summary>
    public enum TacticalShipSystem
    {
        Shields,
        Weapons,
        TractorBeam,
        Engines,
        Hyperdrive,
    }

    /// <summary>
    /// Identifies the damage delivered by a tactical weapon.
    /// </summary>
    public enum TacticalWeaponType
    {
        LaserCannon,
        IonCannon,
        Turbolaser,
        Torpedo,
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
        public float AttackDelay { get; }
        public float MovementDelay { get; }
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
            AttackDelay = 0;
            MovementDelay = 0;
            DischargedArcMask = 0;
        }

        /// <summary>
        /// Creates an ion disruption event with temporary delays and discharged batteries.
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

    /// <summary>
    /// Owns tactical hull, shield, recharge, and component damage state.
    /// Campaign objects are updated only when the battle result is committed.
    /// </summary>
    public sealed class TacticalCombatSystems
    {
        private readonly uint[] _componentDamage = new uint[5];
        private readonly float _weaponRecharge;
        private readonly float _shieldRecharge;
        private readonly float _movementBudget;
        private readonly float _tractorBudget;
        private readonly float _turnBonus;
        private readonly uint _hyperdriveCount;
        private readonly float _damageControl;
        private readonly int _campaignRepairDueTick;
        private readonly int _campaignRepairInterval;
        private uint _damageEventCount;
        private float _repairMilliseconds;
        private float _shieldLimit;

        public bool IsFighter { get; }
        public float MaximumHull { get; }
        public float Hull { get; private set; }
        public float Shields { get; private set; }
        public float MaximumShields => _shieldLimit;
        internal float BaseShieldRecharge => _shieldRecharge;
        public float AttackDelay { get; private set; }
        public float MovementDelay { get; private set; }
        public bool IsAlive => Hull > 0;
        public bool HasHyperdrive =>
            _componentDamage[(int)TacticalShipSystem.Hyperdrive] < _hyperdriveCount;
        public float HullFraction => Hull / MaximumHull;
        public event Action<TacticalSystemEvent> SystemEventRequested;
        public event Action<TacticalCombatSound> SoundRequested;

        /// <summary>
        /// Captures one unit's combat system values before the battle begins.
        /// </summary>
        /// <param name="isFighter">Whether this is a fighter squadron.</param>
        /// <param name="maximumHull">The complete unit's hull pool.</param>
        /// <param name="currentHull">The surviving hull pool at entry.</param>
        /// <param name="maximumShields">The complete shield pool.</param>
        /// <param name="weaponRechargeRate">The capital ship's weapon recharge rate.</param>
        /// <param name="shieldRechargeRate">The capital ship's shield recharge rate.</param>
        /// <param name="sublightSpeed">The unit's sublight engine rating.</param>
        /// <param name="maneuverability">The unit's maneuverability rating.</param>
        /// <param name="tractorPower">The unit's tractor beam strength.</param>
        /// <param name="hyperdrives">The number of installed hyperdrives.</param>
        /// <param name="damageControlRating">The damage-control repair chance out of 100.</param>
        /// <param name="initialDamage">The component damage carried into the battle.</param>
        public TacticalCombatSystems(
            bool isFighter,
            float maximumHull,
            float currentHull,
            float maximumShields,
            float weaponRechargeRate,
            float shieldRechargeRate,
            float sublightSpeed,
            float maneuverability,
            float tractorPower,
            uint hyperdrives,
            float damageControlRating,
            CapitalShipSystemDamage initialDamage = null
        )
        {
            _campaignRepairDueTick = initialDamage?.RepairDueTick ?? 0;
            _campaignRepairInterval = initialDamage?.RepairInterval ?? 0;
            IsFighter = isFighter;
            MaximumHull = maximumHull;
            Hull = currentHull;
            _shieldLimit = maximumShields;
            Shields = maximumShields;
            _weaponRecharge = (float)((double)weaponRechargeRate * 3.75f);
            _shieldRecharge = shieldRechargeRate;
            _movementBudget = isFighter
                ? (float)((double)sublightSpeed * 0.4f)
                : (float)((double)sublightSpeed * 0.4f * 0.6f);
            _turnBonus = maneuverability;
            _tractorBudget = tractorPower;
            _hyperdriveCount = hyperdrives;
            _damageControl = damageControlRating;
            if (initialDamage != null)
            {
                _componentDamage[(int)TacticalShipSystem.Shields] = (uint)initialDamage.Shields;
                _componentDamage[(int)TacticalShipSystem.Weapons] = (uint)initialDamage.Weapons;
                _componentDamage[(int)TacticalShipSystem.TractorBeam] = (uint)
                    initialDamage.TractorBeam;
                _componentDamage[(int)TacticalShipSystem.Engines] = (uint)initialDamage.Engines;
                _componentDamage[(int)TacticalShipSystem.Hyperdrive] = (uint)(
                    initialDamage.PrimaryHyperdrive + initialDamage.BackupHyperdrive
                );
                _damageEventCount =
                    _componentDamage[(int)TacticalShipSystem.Engines]
                    + _componentDamage[(int)TacticalShipSystem.Shields]
                    + _componentDamage[(int)TacticalShipSystem.Hyperdrive]
                    + _componentDamage[(int)TacticalShipSystem.TractorBeam];
            }
        }

        /// <summary>
        /// Captures the component damage for the campaign, assigning disabled drives to
        /// the primary drive before the backup drive.
        /// </summary>
        /// <param name="primaryHyperdriveInstalled">Whether the ship has a primary drive.</param>
        /// <param name="backupHyperdriveInstalled">Whether the ship has a backup drive.</param>
        /// <returns>The component damage to commit with the battle result.</returns>
        public CapitalShipSystemDamage CaptureDamage(
            bool primaryHyperdriveInstalled,
            bool backupHyperdriveInstalled
        )
        {
            int remaining = (int)_componentDamage[(int)TacticalShipSystem.Hyperdrive];
            int primary = primaryHyperdriveInstalled ? Math.Min(remaining, 1) : 0;
            remaining -= primary;
            CapitalShipSystemDamage result = new()
            {
                Shields = (int)_componentDamage[(int)TacticalShipSystem.Shields],
                Weapons = (int)_componentDamage[(int)TacticalShipSystem.Weapons],
                TractorBeam = (int)_componentDamage[(int)TacticalShipSystem.TractorBeam],
                Engines = (int)_componentDamage[(int)TacticalShipSystem.Engines],
                PrimaryHyperdrive = primary,
                BackupHyperdrive = backupHyperdriveInstalled ? Math.Min(remaining, 1) : 0,
            };
            if (result.Total > 0)
            {
                result.RepairDueTick = _campaignRepairDueTick;
                result.RepairInterval = _campaignRepairInterval;
            }
            return result;
        }

        /// <summary>
        /// Gets the accumulated damage level for a component.
        /// </summary>
        /// <param name="system">The component to inspect.</param>
        /// <returns>The component's damage level.</returns>
        public uint GetDamage(TacticalShipSystem system)
        {
            return _componentDamage[(int)system];
        }

        /// <summary>
        /// Gets the weapon recharge available for one second.
        /// </summary>
        /// <returns>The weapon recharge budget after hull and component damage.</returns>
        public float GetWeaponRecharge()
        {
            return IsFighter
                ? (float)((double)Hull * 3.751f / 8.0f)
                : GetDamagedBudget(
                    _weaponRecharge,
                    _componentDamage[(int)TacticalShipSystem.Weapons]
                );
        }

        /// <summary>
        /// Gets the shield recharge available for one second.
        /// </summary>
        /// <returns>The shield recharge budget after hull and component damage.</returns>
        public float GetShieldRecharge()
        {
            return IsFighter
                ? (float)((double)Hull * 0.025f / 8.0f)
                : GetDamagedBudget(
                    _shieldRecharge,
                    _componentDamage[(int)TacticalShipSystem.Shields]
                );
        }

        /// <summary>
        /// Calculates movement after command support, engine damage, and tractor restraint.
        /// </summary>
        /// <param name="commandBudget">The side's active command support budget.</param>
        /// <param name="tractorRestraint">The combined opposing tractor restraint.</param>
        /// <returns>The nonnegative available movement speed.</returns>
        public float GetMovementBudget(float commandBudget, float tractorRestraint)
        {
            if (MovementDelay != 0)
                return 0;
            double total = (double)commandBudget + _movementBudget;
            double consumed = total * 0.25f * _componentDamage[(int)TacticalShipSystem.Engines];
            double remaining = total - (consumed + tractorRestraint);
            return remaining > 0 ? (float)remaining : 0;
        }

        /// <summary>
        /// Calculates the maneuver rating used by turning and fighter damage.
        /// </summary>
        /// <param name="commandBudget">The side's active command support budget.</param>
        /// <param name="tractorRestraint">The combined opposing tractor restraint.</param>
        /// <returns>The unit's maneuver rating, capped at nine.</returns>
        public float GetTurnBudget(float commandBudget, float tractorRestraint)
        {
            if (GetMovementBudget(commandBudget, tractorRestraint) == 0)
                return 1;
            double value = IsFighter ? (double)_turnBonus + 1 : (double)commandBudget + _turnBonus;
            return (float)Math.Min(value, 9);
        }

        /// <summary>
        /// Calculates the restraint applied by this unit's tractor beam.
        /// </summary>
        /// <returns>The tractor restraint after hull and component damage.</returns>
        public float GetTractorBudget()
        {
            return GetDamagedBudget(
                _tractorBudget,
                _componentDamage[(int)TacticalShipSystem.TractorBeam]
            );
        }

        /// <summary>
        /// Applies a queued component hit or repair.
        /// </summary>
        /// <param name="system">The affected component.</param>
        /// <param name="isRepair">Whether to repair one damage level.</param>
        public void ApplyComponentEvent(TacticalShipSystem system, bool isRepair)
        {
            int index = (int)system;
            if (isRepair)
            {
                if (_damageEventCount == 0)
                    return;
                _damageEventCount--;
                _componentDamage[index]--;
                return;
            }

            _damageEventCount++;
            uint limit = system == TacticalShipSystem.Hyperdrive ? _hyperdriveCount : 4;
            _componentDamage[index] = Math.Min(_componentDamage[index] + 1, limit);
        }

        /// <summary>
        /// Applies the temporary delays from a queued ion disruption.
        /// </summary>
        /// <param name="attackDelay">The additional attack delay in seconds.</param>
        /// <param name="movementDelay">The additional movement delay in seconds.</param>
        public void ApplyDisruption(float attackDelay, float movementDelay)
        {
            AttackDelay += attackDelay;
            MovementDelay += movementDelay;
        }

        /// <summary>
        /// Recharges shields, recovers temporary delays, and requests due component repairs.
        /// </summary>
        /// <param name="elapsedMilliseconds">The system update interval in milliseconds.</param>
        /// <param name="random">The battle's random-number stream.</param>
        public void Advance(float elapsedMilliseconds, IRandomNumberProvider random)
        {
            float seconds = elapsedMilliseconds * 0.001f;
            if (IsFighter)
            {
                _shieldLimit = (float)((double)Hull * _shieldLimit / MaximumHull);
                Shields = Math.Min(_shieldLimit, Shields);
            }
            if (_shieldLimit - Shields > 0)
                Shields = (float)
                    Math.Min((double)GetShieldRecharge() * seconds + Shields, _shieldLimit);
            if (IsFighter)
                return;

            if (MovementDelay > 0)
                MovementDelay = Math.Max(MovementDelay - seconds, 0);
            if (AttackDelay > 0)
                AttackDelay = Math.Max(AttackDelay - seconds, 0);
            if (_damageEventCount == 0)
                return;
            _repairMilliseconds -= elapsedMilliseconds;
            if (_repairMilliseconds > 0)
                return;
            _repairMilliseconds = 50000;
            RequestComponentRepair(random);
        }

        /// <summary>
        /// Applies one weapon impact and requests its resulting component events.
        /// </summary>
        /// <param name="weapon">The impacting weapon.</param>
        /// <param name="damage">The queued attack strength.</param>
        /// <param name="ownTurnBudget">The target's current maneuver rating.</param>
        /// <param name="attackerTurnBudget">The attacker's current maneuver rating.</param>
        /// <param name="random">The battle's random-number stream.</param>
        /// <returns>The affected surface, destruction, or no impact for an inactive target.</returns>
        public TacticalCombatImpactKind ApplyHit(
            TacticalWeaponType weapon,
            float damage,
            float ownTurnBudget,
            float attackerTurnBudget,
            IRandomNumberProvider random
        )
        {
            if (!IsAlive)
            {
                Hull = 0;
                return TacticalCombatImpactKind.None;
            }

            float previousHull = Hull;
            float previousShields = Shields;
            double applied = damage;
            if (IsFighter && weapon != TacticalWeaponType.Torpedo)
            {
                float ratio = (float)((double)ownTurnBudget / attackerTurnBudget);
                applied /= Math.Max(ratio, 0.1f);
            }
            applied = Math.Max(applied, 1);
            double remainingShields = (double)previousShields - applied;
            Shields = (float)remainingShields;
            if (remainingShields < 0)
            {
                Shields = 0;
                if (weapon == TacticalWeaponType.IonCannon)
                {
                    SoundRequested?.Invoke(GetImpactSound(weapon, true));
                    if (!IsFighter)
                        RequestIonDisruption((float)-remainingShields, random);
                    return TacticalCombatImpactKind.Hull;
                }

                Hull = Math.Max((float)(remainingShields + previousHull), 0);
                SoundRequested?.Invoke(GetImpactSound(weapon, true));
                if (!IsAlive)
                    SoundRequested?.Invoke(GetExplosionSound());
                else if (!IsFighter)
                    RequestHullHitEvent(previousHull, random);
                return IsAlive ? TacticalCombatImpactKind.Hull : TacticalCombatImpactKind.Destroyed;
            }

            SoundRequested?.Invoke(GetImpactSound(weapon, false));
            if (IsFighter)
                return TacticalCombatImpactKind.Shield;
            double progress = 100.0 / previousShields * ((double)previousShields - Shields);
            float chance = progress < 1 ? 1 : (float)progress;
            if (random.NextInt(0, 101) <= chance)
                SystemEventRequested?.Invoke(
                    new TacticalSystemEvent(TacticalShipSystem.Shields, false)
                );
            return TacticalCombatImpactKind.Shield;
        }

        /// <summary>
        /// Selects the explosion size from the captured unit category and complete hull.
        /// </summary>
        /// <returns>The unit's destruction sound.</returns>
        public TacticalCombatSound GetExplosionSound() =>
            IsFighter || MaximumHull < 1100 ? TacticalCombatSound.SmallExplosion
            : MaximumHull <= 2000 ? TacticalCombatSound.MediumExplosion
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
        /// Removes the unit's hull and shields when its destruction command is applied.
        /// </summary>
        public void Destroy()
        {
            Hull = 0;
            Shields = 0;
        }

        /// <summary>
        /// Removes hull directly during a staged fighter attack sequence.
        /// </summary>
        /// <param name="loss">The hull loss, independent of shields.</param>
        public void ApplyHullLoss(float loss) => Hull = Math.Max(Hull - loss, 0);

        /// <summary>
        /// Restores the pools captured when a staged fighter attack began.
        /// </summary>
        /// <param name="hull">The captured hull strength.</param>
        /// <param name="shields">The captured shield strength.</param>
        public void RestorePools(float hull, float shields)
        {
            Hull = hull;
            Shields = shields;
        }

        /// <summary>
        /// Calculates a hull-scaled budget with a quarter of its base removed per damage level.
        /// </summary>
        /// <param name="budget">The complete unit's budget.</param>
        /// <param name="damage">The component's damage level.</param>
        /// <returns>The nonnegative remaining budget.</returns>
        private float GetDamagedBudget(float budget, uint damage)
        {
            float scaled = (float)((double)Hull * budget / MaximumHull);
            double remaining = scaled - (double)budget * 0.25f * damage;
            return remaining > 0 ? (float)remaining : 0;
        }

        /// <summary>
        /// Requests the component event selected after a surviving hull impact.
        /// </summary>
        /// <param name="previousHull">The hull strength before impact.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestHullHitEvent(float previousHull, IRandomNumberProvider random)
        {
            float score = (float)(
                ((double)Hull - previousHull) * 100 / previousHull + random.NextInt(0, 101)
            );
            if (score <= 50)
                return;
            TacticalShipSystem system =
                score <= 65 ? TacticalShipSystem.Shields
                : score <= 80 ? TacticalShipSystem.Weapons
                : score <= 90 ? TacticalShipSystem.TractorBeam
                : score <= 95 ? TacticalShipSystem.Engines
                : TacticalShipSystem.Hyperdrive;
            if (score > 100)
            {
                Hull = 0;
                return;
            }
            SystemEventRequested?.Invoke(new TacticalSystemEvent(system, false));
        }

        /// <summary>
        /// Requests a temporary disruption for each point of ion damage past the shields.
        /// </summary>
        /// <param name="excessDamage">The ion strength past the shields.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestIonDisruption(float excessDamage, IRandomNumberProvider random)
        {
            float attackDelay = 0;
            float movementDelay = 0;
            int arcs = 0;
            while (excessDamage > 0)
            {
                int component = random.NextInt(1, 11);
                if (component == 1)
                    attackDelay += random.NextInt(30, 51);
                else if (component == 2)
                    movementDelay += random.NextInt(30, 51);
                else
                    arcs |= 1 << ((component - 3) >> 1);
                excessDamage -= 1;
            }
            SystemEventRequested?.Invoke(new TacticalSystemEvent(attackDelay, movementDelay, arcs));
        }

        /// <summary>
        /// Selects a damaged component after a successful damage-control roll.
        /// </summary>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestComponentRepair(IRandomNumberProvider random)
        {
            if (_damageControl == 0 || random.NextInt(1, 101) > _damageControl)
                return;
            uint roll =
                _damageEventCount > 1 ? (uint)random.NextInt(1, (int)_damageEventCount + 1) : 1;
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
                uint damage = _componentDamage[(int)system];
                if (roll <= damage)
                {
                    SystemEventRequested?.Invoke(new TacticalSystemEvent(system, true));
                    return;
                }
                roll -= damage;
            }
        }
    }
}
