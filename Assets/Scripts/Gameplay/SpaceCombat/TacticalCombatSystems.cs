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

        /// <summary>
        /// Gets the attack timer increment, or null when its value is unresolved.
        /// </summary>
        public float? AttackDelay { get; }

        /// <summary>
        /// Gets the movement timer increment, or null when its value is unresolved.
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

        /// <summary>
        /// Creates a disruption event with known battery changes and unresolved timer increments.
        /// </summary>
        /// <param name="dischargedArcMask">The batteries whose charge must be cleared.</param>
        public TacticalSystemEvent(int dischargedArcMask)
        {
            System = TacticalShipSystem.Weapons;
            Kind = TacticalSystemEventKind.Disruption;
            AttackDelay = null;
            MovementDelay = null;
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
        private float _weaponRecharge;
        private float _shieldRecharge;
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
        public float AttackDelay { get; private set; }
        public float MovementDelay { get; private set; }
        public bool IsAlive => Hull > 0;
        public bool HasHyperdrive =>
            _componentDamage[(int)TacticalShipSystem.Hyperdrive] < _hyperdriveCount;
        public double HullFraction => (double)Hull / MaximumHull;
        public event Action<TacticalSystemEvent> SystemEventRequested;
        public event Action<TacticalCombatSound> SoundRequested;

        /// <summary>
        /// Reports a lethal weapon impact while its resulting hull value is still stored.
        /// The argument identifies the firing unit.
        /// </summary>
        public event Action<string> LethalHit;

        /// <summary>
        /// Requests the weapon's impact effect after the affected surface and hull state are stored.
        /// </summary>
        public event Action<TacticalWeaponType, TacticalCombatImpactKind> ImpactRequested;

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
            _weaponRecharge = isFighter ? 0.25f : (float)((double)weaponRechargeRate * 3.75f);
            _shieldRecharge = isFighter ? 1 : shieldRechargeRate;
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
        /// Scales a value by the remaining hull, rounding after the multiplication and division.
        /// </summary>
        /// <param name="value">The complete unit's value.</param>
        /// <returns>The hull-scaled value.</returns>
        public float ScaleByHull(float value) => (float)((double)Hull * value / MaximumHull);

        /// <summary>
        /// Gets the weapon recharge available for one second.
        /// </summary>
        /// <returns>The weapon recharge budget after hull and component damage.</returns>
        public double GetWeaponRecharge()
        {
            if (IsFighter)
            {
                double recharge = (double)Hull * 3.751f / 8.0f;
                _weaponRecharge = (float)recharge;
                return recharge;
            }
            float scaled = ScaleByHull(_weaponRecharge);
            double remaining =
                scaled
                - (double)_weaponRecharge
                    * 0.25f
                    * _componentDamage[(int)TacticalShipSystem.Weapons];
            return double.IsNaN(remaining) || remaining < 0 ? 0 : remaining;
        }

        /// <summary>
        /// Compares live weapon recharge with its stored rate after the getter updates its sample.
        /// </summary>
        /// <returns>The recharge ratio, or zero when the stored rate is zero or unordered.</returns>
        public double GetWeaponRechargeRatio()
        {
            if (_weaponRecharge == 0 || float.IsNaN(_weaponRecharge))
                return 0;
            double recharge = GetWeaponRecharge();
            return recharge / _weaponRecharge;
        }

        /// <summary>
        /// Compares the current shield pool with the current shield limit.
        /// </summary>
        /// <returns>The shield fraction, or zero when its limit is zero or unordered.</returns>
        public double GetShieldFraction() =>
            _shieldLimit == 0 || float.IsNaN(_shieldLimit) ? 0 : (double)Shields / _shieldLimit;

        /// <summary>
        /// Gets the shield recharge available for one second and updates the fighter's stored sample.
        /// </summary>
        /// <returns>The live recharge budget after hull and component damage.</returns>
        public double GetShieldRecharge()
        {
            if (IsFighter)
            {
                double recharge = (double)Hull * 0.025f / 8.0f;
                _shieldRecharge = (float)recharge;
                return recharge;
            }
            float scaled = ScaleByHull(_shieldRecharge);
            double remaining =
                scaled
                - (double)_shieldRecharge
                    * 0.25f
                    * _componentDamage[(int)TacticalShipSystem.Shields];
            return double.IsNaN(remaining) || remaining < 0 ? 0 : remaining;
        }

        /// <summary>
        /// Compares live recharge with its current stored rate, retaining the getter's sample update.
        /// </summary>
        /// <returns>The recharge ratio, or zero when the stored rate is zero or unordered.</returns>
        public double GetShieldRechargeRatio()
        {
            if (_shieldRecharge == 0 || float.IsNaN(_shieldRecharge))
                return 0;
            double recharge = GetShieldRecharge();
            return recharge / _shieldRecharge;
        }

        /// <summary>
        /// Calculates movement after command support, engine damage, and tractor restraint.
        /// </summary>
        /// <param name="commandBudget">The side's active command support budget.</param>
        /// <param name="tractorRestraint">The combined opposing tractor restraint.</param>
        /// <returns>The nonnegative available movement speed.</returns>
        public float GetMovementBudget(float commandBudget, float tractorRestraint) =>
            (float)GetAvailableMovement(commandBudget, tractorRestraint);

        /// <summary>
        /// Compares live supported movement with the unit's stored engine budget.
        /// </summary>
        /// <param name="commandBudget">The side's live command support.</param>
        /// <param name="tractorRestraint">The ordered sum of retained tractor contributions.</param>
        /// <returns>The movement ratio, or zero when the stored budget is zero or unordered.</returns>
        public double GetMovementRatio(double commandBudget, float tractorRestraint) =>
            _movementBudget == 0 || float.IsNaN(_movementBudget)
                ? 0
                : GetAvailableMovement(commandBudget, tractorRestraint) / _movementBudget;

        /// <summary>
        /// Compares remaining installed hyperdrives with the complete drive count.
        /// </summary>
        /// <returns>The available drive fraction, or zero when no drives are installed.</returns>
        public double GetHyperdriveRatio() =>
            _hyperdriveCount == 0
                ? 0
                : (double)(
                    _hyperdriveCount
                    - Math.Min(_hyperdriveCount, GetDamage(TacticalShipSystem.Hyperdrive))
                ) / _hyperdriveCount;

        /// <summary>
        /// Compares available tractor power with its stored complete-unit budget.
        /// </summary>
        /// <returns>The tractor ratio, or zero when the stored budget is zero or unordered.</returns>
        public double GetTractorRatio() =>
            _tractorBudget == 0 || float.IsNaN(_tractorBudget)
                ? 0
                : GetTractorContribution() / _tractorBudget;

        /// <summary>
        /// Checks both departure budgets before narrowing movement to a stored single-precision speed.
        /// </summary>
        /// <param name="commandBudget">The selected command officer's live movement support.</param>
        /// <param name="tractorRestraint">The ordered, single-precision sum of retained tractor contributions.</param>
        /// <returns>Whether installed hyperdrive and available movement budgets are both ordered and nonzero.</returns>
        internal bool CanEnterHyperspace(double commandBudget, float tractorRestraint) =>
            HasHyperdrive && GetAvailableMovement(commandBudget, tractorRestraint) > 0;

        /// <summary>
        /// Calculates available movement without a return-value store between the budget and its eligibility check.
        /// </summary>
        /// <param name="commandBudget">The selected command officer's live movement support.</param>
        /// <param name="tractorRestraint">The ordered sum of retained tractor contributions.</param>
        /// <returns>The ordered positive budget, or zero when movement is unavailable.</returns>
        internal double GetAvailableMovement(double commandBudget, float tractorRestraint)
        {
            if (MovementDelay != 0 && !float.IsNaN(MovementDelay))
                return 0;
            double total = commandBudget + _movementBudget;
            double consumed = total * 0.25f * _componentDamage[(int)TacticalShipSystem.Engines];
            double remaining = total - (consumed + tractorRestraint);
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>
        /// Calculates the maneuver rating used by turning and fighter damage.
        /// </summary>
        /// <param name="commandBudget">The side's active command support budget.</param>
        /// <param name="tractorRestraint">The combined opposing tractor restraint.</param>
        /// <returns>The unit's maneuver rating, capped at nine.</returns>
        public float GetTurnBudget(float commandBudget, float tractorRestraint)
        {
            if (GetAvailableMovement(commandBudget, tractorRestraint) == 0)
                return 1;
            double value = IsFighter ? (double)_turnBonus + 1 : (double)commandBudget + _turnBonus;
            return (float)Math.Min(value, 9);
        }

        /// <summary>
        /// Calculates the restraint applied by this unit's tractor beam.
        /// </summary>
        /// <returns>The tractor restraint after hull and component damage.</returns>
        public float GetTractorBudget() => (float)GetTractorContribution();

        /// <summary>
        /// Retains the hull-scaled tractor contribution until it is added to the target's ordered accumulator.
        /// </summary>
        /// <returns>The nonnegative contribution after hull and tractor-system damage.</returns>
        internal double GetTractorContribution()
        {
            float scaled = ScaleByHull(_tractorBudget);
            double remaining =
                scaled
                - (double)_tractorBudget
                    * 0.25f
                    * _componentDamage[(int)TacticalShipSystem.TractorBeam];
            return remaining < 0 || double.IsNaN(remaining) ? 0 : remaining;
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
                double limit = (double)Hull * _shieldLimit / MaximumHull;
                _shieldLimit = (float)limit;
                Shields = (float)(limit > Shields ? Shields : limit);
            }
            double recharge = GetShieldRecharge() * seconds;
            if (_shieldLimit - Shields > 0)
            {
                double charged = recharge + Shields;
                Shields = (float)(charged >= _shieldLimit ? _shieldLimit : charged);
            }
            if (IsFighter)
                return;

            MovementDelay = DecayDelay(MovementDelay, elapsedMilliseconds);
            AttackDelay = DecayDelay(AttackDelay, elapsedMilliseconds);
            if (_damageEventCount == 0)
                return;
            _repairMilliseconds -= elapsedMilliseconds;
            if (_repairMilliseconds > 0)
                return;
            _repairMilliseconds = 50000;
            RequestComponentRepair(random);
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
        /// <param name="weapon">The impacting weapon.</param>
        /// <param name="damage">The queued attack strength.</param>
        /// <param name="ownTurnBudget">The target's current maneuver rating.</param>
        /// <param name="attackerTurnBudget">The attacker's current maneuver rating.</param>
        /// <param name="random">The battle's random-number stream.</param>
        /// <param name="attackerInstanceID">The firing unit's identity for a lethal impact report.</param>
        /// <returns>The affected surface, destruction, or no impact for an inactive target.</returns>
        public TacticalCombatImpactKind ApplyHit(
            TacticalWeaponType weapon,
            float damage,
            float ownTurnBudget,
            float attackerTurnBudget,
            IRandomNumberProvider random,
            string attackerInstanceID
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
            if (!(remainingShields >= 0))
            {
                Shields = 0;
                if (weapon == TacticalWeaponType.IonCannon)
                {
                    SoundRequested?.Invoke(GetImpactSound(weapon, true));
                    ImpactRequested?.Invoke(weapon, TacticalCombatImpactKind.Hull);
                    if (!IsFighter)
                        RequestIonDisruption((float)-remainingShields, random);
                    return TacticalCombatImpactKind.Hull;
                }

                float remainingHull = (float)(remainingShields + previousHull);
                Hull = remainingHull;
                SoundRequested?.Invoke(GetImpactSound(weapon, true));
                if (!IsAlive)
                {
                    LethalHit?.Invoke(attackerInstanceID);
                    Hull = 0;
                    ImpactRequested?.Invoke(weapon, TacticalCombatImpactKind.Destroyed);
                    SoundRequested?.Invoke(GetExplosionSound());
                    return TacticalCombatImpactKind.Destroyed;
                }

                ImpactRequested?.Invoke(weapon, TacticalCombatImpactKind.Hull);
                if (!IsFighter)
                    RequestHullHitEvent(previousHull, random);
                return TacticalCombatImpactKind.Hull;
            }

            SoundRequested?.Invoke(GetImpactSound(weapon, false));
            if (!IsFighter)
            {
                double progress = 100.0 / previousShields * ((double)previousShields - Shields);
                float chance = progress < 1 ? 1 : (float)progress;
                if (!(random.NextInt(0, 101) > chance))
                    SystemEventRequested?.Invoke(
                        new TacticalSystemEvent(TacticalShipSystem.Shields, false)
                    );
            }
            ImpactRequested?.Invoke(weapon, TacticalCombatImpactKind.Shield);
            return TacticalCombatImpactKind.Shield;
        }

        /// <summary>
        /// Selects the explosion size from the captured unit category and complete hull.
        /// </summary>
        /// <returns>The unit's destruction sound.</returns>
        public TacticalCombatSound GetExplosionSound() =>
            IsFighter || !(MaximumHull >= 1100) ? TacticalCombatSound.SmallExplosion
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
        /// Clears the unit's hull when its deactivation command is applied.
        /// </summary>
        public void Destroy() => Hull = 0;

        /// <summary>
        /// Removes hull directly and clears a living unit's shields during a staged attack sequence.
        /// </summary>
        /// <param name="loss">The hull loss, independent of shields.</param>
        public void ApplyHullLoss(float loss)
        {
            if (!IsAlive)
            {
                Hull = 0;
                return;
            }
            double remaining = (double)Hull - loss;
            Shields = 0;
            Hull = remaining > 0 ? (float)remaining : 0;
        }

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
        /// Requests the component event selected after a surviving hull impact.
        /// </summary>
        /// <param name="previousHull">The hull strength before impact.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestHullHitEvent(float previousHull, IRandomNumberProvider random)
        {
            double score =
                ((double)Hull - previousHull) * 100 / previousHull + random.NextInt(0, 101);
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
                Hull = 0;
                return;
            }
            SystemEventRequested?.Invoke(new TacticalSystemEvent(system, false));
        }

        /// <summary>
        /// Requests known battery changes after ion damage while retaining unresolved timer increments.
        /// </summary>
        /// <param name="excessDamage">The ion strength past the shields.</param>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestIonDisruption(float excessDamage, IRandomNumberProvider random)
        {
            float firstAccumulator = 0;
            float secondAccumulator = 0;
            int arcs = 0;
            while (excessDamage > 0)
            {
                int component = random.NextInt(1, 11);
                if (component == 1)
                    firstAccumulator = (float)((double)random.NextInt(30, 51) + firstAccumulator);
                else if (component == 2)
                    secondAccumulator = (float)((double)random.NextInt(30, 51) + secondAccumulator);
                else
                    arcs |= 1 << ((component - 3) >> 1);
                excessDamage = (float)((double)excessDamage - 1);
            }
            int dischargedArcMask =
                (unchecked((uint)BitConverter.SingleToInt32Bits(firstAccumulator)) == 1 ? 1 : 0)
                | (unchecked((uint)BitConverter.SingleToInt32Bits(secondAccumulator)) == 1 ? 2 : 0)
                | ((arcs & 3) << 2);
            SystemEventRequested?.Invoke(new TacticalSystemEvent(dischargedArcMask));
        }

        /// <summary>
        /// Selects a damaged component after a successful damage-control roll.
        /// </summary>
        /// <param name="random">The battle's random-number stream.</param>
        private void RequestComponentRepair(IRandomNumberProvider random)
        {
            if (
                _damageControl == 0
                || float.IsNaN(_damageControl)
                || random.NextInt(1, 101) > _damageControl
            )
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
