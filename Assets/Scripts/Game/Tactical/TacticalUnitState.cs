using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Holds mutable tactical state for one strategic combat unit.
    /// </summary>
    public sealed class TacticalUnitState
    {
        private const float _damageControlInterval = 50f;
        private const int _maximumSystemDamage = 4;
        private const float _systemDamagePenalty = 0.25f;
        private readonly ReadOnlyCollection<TacticalWeaponBattery> weaponBatteries;
        private readonly float[] arcCharge = new float[4];
        private readonly float[] arcChargeRequired = new float[4];
        private readonly int[] systemDamage = new int[5];
        private readonly Queue<TacticalWeaponArc> rechargingArcs = new Queue<TacticalWeaponArc>();
        private readonly int damageControl;
        private float shieldRechargeRemainder;
        private float componentDisruptionTime;
        private float damageControlTime;
        private float movementDisruptionTime;

        /// <summary>
        /// Gets the strategic unit represented by this tactical unit.
        /// </summary>
        public IGameEntity Unit { get; }

        /// <summary>
        /// Gets the tactical side controlling this unit.
        /// </summary>
        public TacticalBattleSide Side { get; }

        /// <summary>
        /// Gets the simulation behavior used by this unit.
        /// </summary>
        public TacticalUnitKind Kind { get; }

        /// <summary>
        /// Gets the capital ship that deployed this fighter unit, when one participated.
        /// </summary>
        internal TacticalUnitState RecoveryTarget { get; }

        /// <summary>
        /// Gets the unit's hull strength when the battle began.
        /// </summary>
        public int InitialHull { get; }

        /// <summary>
        /// Gets or sets the unit's current tactical hull strength.
        /// </summary>
        public int Hull { get; set; }

        /// <summary>
        /// Gets the unit's shield strength when the battle began.
        /// </summary>
        public int InitialShields { get; }

        /// <summary>
        /// Gets the amount of shield strength restored during one tactical time unit.
        /// </summary>
        public int ShieldRechargeRate { get; }

        /// <summary>
        /// Gets the weapon energy restored during one tactical time unit.
        /// </summary>
        public int WeaponRechargeRate { get; }

        /// <summary>
        /// Gets the unit's tactical sublight movement rating.
        /// </summary>
        public int SublightSpeed { get; }

        /// <summary>
        /// Gets the unit's tactical turning rating.
        /// </summary>
        public int Maneuverability { get; }

        /// <summary>
        /// Gets or sets the unit's current tactical shield strength.
        /// </summary>
        public int Shields { get; set; }

        /// <summary>
        /// Gets whether the unit can continue participating in combat.
        /// </summary>
        public bool IsActive => Hull > 0 && !HasWithdrawn;

        /// <summary>
        /// Gets the capital ship's tactical weapon batteries.
        /// </summary>
        public IReadOnlyList<TacticalWeaponBattery> WeaponBatteries => weaponBatteries;

        /// <summary>
        /// Gets or sets the unit's center position in tactical space.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Gets or sets the normalized direction faced by the unit.
        /// </summary>
        public Vector3 Forward { get; set; }

        /// <summary>
        /// Gets whether the unit is leaving the tactical battlefield.
        /// </summary>
        public bool IsWithdrawing { get; private set; }

        /// <summary>
        /// Gets whether the unit has completed its withdrawal.
        /// </summary>
        public bool HasWithdrawn { get; private set; }

        /// <summary>
        /// Gets whether temporary tactical disruption prevents the unit from moving.
        /// </summary>
        public bool IsMovementDisabled => movementDisruptionTime > 0f;

        /// <summary>
        /// Gets the sublight movement available after persistent drive damage.
        /// </summary>
        public float EffectiveSublightSpeed =>
            IsMovementDisabled
                ? 0f
                : Math.Max(
                    0f,
                    SublightSpeed
                        * (
                            1f
                            - GetSystemDamage(TacticalDamageSystem.SublightDrive)
                                * _systemDamagePenalty
                        )
                );

        /// <summary>
        /// Gets whether the hyperdrive remains capable of completing a tactical withdrawal.
        /// </summary>
        public bool CanWithdraw =>
            GetSystemDamage(TacticalDamageSystem.Hyperdrive) < _maximumSystemDamage;

        /// <summary>
        /// Gets the remaining temporary component disruption time.
        /// </summary>
        public float ComponentDisruptionTime => componentDisruptionTime;

        /// <summary>
        /// Gets the remaining temporary movement disruption time.
        /// </summary>
        public float MovementDisruptionTime => movementDisruptionTime;

        /// <summary>
        /// Initializes the mutable tactical state for one strategic unit.
        /// </summary>
        /// <param name="unit">The represented strategic unit.</param>
        /// <param name="side">The side controlling the unit.</param>
        /// <param name="kind">The tactical unit kind.</param>
        /// <param name="hull">The initial hull strength.</param>
        /// <param name="shields">The initial shield strength.</param>
        /// <param name="shieldRechargeRate">The shield recharge rate.</param>
        /// <param name="weaponRechargeRate">The weapon recharge rate.</param>
        /// <param name="sublightSpeed">The tactical movement rating.</param>
        /// <param name="maneuverability">The tactical turning rating.</param>
        /// <param name="damageControl">The chance to repair subsystem damage.</param>
        /// <param name="weaponBatteries">The unit's tactical weapon batteries.</param>
        /// <param name="recoveryTarget">The capital ship that deployed this unit.</param>
        private TacticalUnitState(
            IGameEntity unit,
            TacticalBattleSide side,
            TacticalUnitKind kind,
            int hull,
            int shields,
            int shieldRechargeRate,
            int weaponRechargeRate,
            int sublightSpeed,
            int maneuverability,
            int damageControl,
            IList<TacticalWeaponBattery> weaponBatteries,
            TacticalUnitState recoveryTarget = null
        )
        {
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            Side = side;
            Kind = kind;
            InitialHull = Math.Max(0, hull);
            Hull = InitialHull;
            InitialShields = Math.Max(0, shields);
            Shields = InitialShields;
            ShieldRechargeRate = Math.Max(0, shieldRechargeRate);
            WeaponRechargeRate = Math.Max(0, weaponRechargeRate);
            SublightSpeed = Math.Max(0, sublightSpeed);
            Maneuverability = Math.Max(0, maneuverability);
            this.damageControl = Math.Max(0, damageControl);
            Forward = Vector3.UnitZ;
            RecoveryTarget = recoveryTarget;
            this.weaponBatteries = new ReadOnlyCollection<TacticalWeaponBattery>(
                weaponBatteries ?? Array.Empty<TacticalWeaponBattery>()
            );
        }

        /// <summary>
        /// Creates tactical state for a capital ship.
        /// </summary>
        /// <param name="ship">The strategic capital ship.</param>
        /// <param name="side">The side controlling the ship.</param>
        /// <returns>The initialized tactical state.</returns>
        public static TacticalUnitState FromCapitalShip(CapitalShip ship, TacticalBattleSide side)
        {
            if (ship == null)
                throw new ArgumentNullException(nameof(ship));

            return new TacticalUnitState(
                ship,
                side,
                TacticalUnitKind.CapitalShip,
                ship.CurrentHullStrength,
                ship.MaxShieldStrength,
                ship.ShieldRechargeRate,
                ship.WeaponRecharge,
                ship.SublightSpeed,
                ship.Maneuverability,
                ship.DamageControl,
                ship.PrimaryWeapons.OrderBy(entry => entry.Key)
                    .Select(entry => TacticalWeaponBattery.Create(entry.Key, entry.Value))
                    .ToList()
            );
        }

        /// <summary>
        /// Creates tactical state for a fighter squadron.
        /// </summary>
        /// <param name="fighters">The strategic fighter squadron.</param>
        /// <param name="side">The side controlling the squadron.</param>
        /// <param name="recoveryTarget">The capital ship that deployed the squadron.</param>
        /// <returns>The initialized tactical state.</returns>
        public static TacticalUnitState FromFighters(
            Starfighter fighters,
            TacticalBattleSide side,
            TacticalUnitState recoveryTarget = null
        )
        {
            if (fighters == null)
                throw new ArgumentNullException(nameof(fighters));

            return new TacticalUnitState(
                fighters,
                side,
                TacticalUnitKind.Fighters,
                fighters.CurrentSquadronSize,
                fighters.CurrentSquadronSize * fighters.ShieldStrength,
                fighters.CurrentSquadronSize,
                fighters.CurrentSquadronSize,
                fighters.SublightSpeed,
                fighters.Agility,
                0,
                CreateFighterBatteries(fighters),
                recoveryTarget
            );
        }

        /// <summary>
        /// Returns the charged weapon strength available from one arc at the given range.
        /// </summary>
        /// <param name="arc">The firing arc to inspect.</param>
        /// <param name="distance">The distance to the prospective target.</param>
        /// <returns>The available attack strength.</returns>
        public int GetAvailableAttackStrength(TacticalWeaponArc arc, float distance)
        {
            if (distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (!IsArcReady(arc))
                return 0;

            return weaponBatteries
                .Where(battery => distance <= battery.Range)
                .Sum(battery => battery.GetCount(arc));
        }

        /// <summary>
        /// Fires every charged weapon family in one arc that can reach the target.
        /// </summary>
        /// <param name="arc">The firing arc to discharge.</param>
        /// <param name="distance">The distance to the target.</param>
        /// <returns>The independently resolved attacks fired from the arc.</returns>
        public IReadOnlyList<TacticalAttack> FireArc(TacticalWeaponArc arc, float distance)
        {
            if (!IsArcReady(arc))
                return Array.Empty<TacticalAttack>();

            TacticalAttack[] attacks = weaponBatteries
                .Where(battery => distance <= battery.Range && battery.GetCount(arc) > 0)
                .Select(battery => new TacticalAttack(battery.WeaponType, battery.GetCount(arc)))
                .ToArray();
            if (attacks.Length == 0)
                return attacks;

            int index = (int)arc;
            arcCharge[index] = 0f;
            arcChargeRequired[index] = attacks.Sum(attack => attack.Strength);
            if (!rechargingArcs.Contains(arc))
                rechargingArcs.Enqueue(arc);
            return attacks;
        }

        /// <summary>
        /// Marks the unit as withdrawing from tactical combat.
        /// </summary>
        public void BeginWithdrawal()
        {
            if (IsActive)
                IsWithdrawing = true;
        }

        /// <summary>
        /// Marks the withdrawing unit as having left tactical combat.
        /// </summary>
        public void CompleteWithdrawal()
        {
            if (IsWithdrawing)
                HasWithdrawn = true;
        }

        /// <summary>
        /// Applies tactical damage to shields before allowing any remainder to damage the hull.
        /// </summary>
        /// <param name="amount">The nonnegative damage amount.</param>
        public void ApplyDamage(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !IsActive)
                return;

            int absorbedDamage = Math.Min(Shields, amount);
            Shields -= absorbedDamage;
            Hull = Math.Max(0, Hull - (amount - absorbedDamage));
        }

        /// <summary>
        /// Applies one independently resolved weapon-family attack.
        /// </summary>
        /// <param name="attack">The attack to apply.</param>
        /// <param name="random">The deterministic random source used for system disruption.</param>
        public void ApplyDamage(TacticalAttack attack, IRandomNumberProvider random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (attack.WeaponType != TacticalWeaponType.IonCannon)
            {
                ApplyConventionalDamage(attack.Strength, random);
                return;
            }

            ApplyIonDamage(attack.Strength, random);
        }

        /// <summary>
        /// Returns the persistent damage level for one capital-ship subsystem.
        /// </summary>
        /// <param name="system">The subsystem to inspect.</param>
        /// <returns>The damage level from zero through four.</returns>
        public int GetSystemDamage(TacticalDamageSystem system)
        {
            return systemDamage[(int)system];
        }

        /// <summary>
        /// Advances the unit's continuous tactical recharge state.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        /// <param name="random">The deterministic random source used for damage control.</param>
        internal void Advance(float elapsedTime, IRandomNumberProvider random)
        {
            if (elapsedTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            componentDisruptionTime = Math.Max(0f, componentDisruptionTime - elapsedTime);
            movementDisruptionTime = Math.Max(0f, movementDisruptionTime - elapsedTime);
            AdvanceDamageControl(elapsedTime, random);
            AdvanceWeaponRecharge(elapsedTime, GetEffectiveWeaponRechargeRate());

            int maximumShields = GetCurrentMaximumShields();
            float rechargeRate = GetEffectiveShieldRechargeRate();
            if (!IsActive || Shields >= maximumShields || rechargeRate <= 0f)
            {
                shieldRechargeRemainder = 0f;
                return;
            }

            float recharge = shieldRechargeRemainder + rechargeRate * elapsedTime;
            int wholeRecharge = (int)Math.Floor(recharge);
            shieldRechargeRemainder = recharge - wholeRecharge;
            Shields = Math.Min(maximumShields, Shields + wholeRecharge);
            if (Shields == maximumShields)
                shieldRechargeRemainder = 0f;
        }

        /// <summary>
        /// Creates the forward-firing batteries carried by a fighter squadron.
        /// </summary>
        /// <param name="fighters">The strategic fighter squadron.</param>
        /// <returns>The squadron's tactical batteries.</returns>
        private static IList<TacticalWeaponBattery> CreateFighterBatteries(Starfighter fighters)
        {
            return new[]
            {
                TacticalWeaponBattery.CreateFighter(
                    TacticalWeaponType.LaserCannon,
                    fighters.LaserCannon,
                    fighters.LaserRange
                ),
                TacticalWeaponBattery.CreateFighter(
                    TacticalWeaponType.IonCannon,
                    fighters.IonCannon,
                    fighters.IonRange
                ),
                TacticalWeaponBattery.CreateFighter(
                    TacticalWeaponType.Torpedo,
                    fighters.Torpedoes,
                    fighters.TorpedoRange
                ),
            };
        }

        /// <summary>
        /// Applies ion damage to shields and converts capital-ship overflow into temporary disruption.
        /// </summary>
        /// <param name="amount">The nonnegative ion damage amount.</param>
        /// <param name="random">The deterministic random source used for each disruption roll.</param>
        private void ApplyIonDamage(int amount, IRandomNumberProvider random)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !IsActive)
                return;

            int overflow = Math.Max(0, amount - Shields);
            Shields = Math.Max(0, Shields - amount);
            if (Kind != TacticalUnitKind.CapitalShip)
                return;

            for (int point = 0; point < overflow; point++)
            {
                int effect = random.NextInt(1, 11);
                if (effect == 1)
                    componentDisruptionTime += random.NextInt(30, 51);
                else if (effect == 2)
                    movementDisruptionTime += random.NextInt(30, 51);
                else
                    DisruptWeaponArc((TacticalWeaponArc)((effect - 3) / 2));
            }
        }

        /// <summary>
        /// Applies conventional damage and rolls persistent capital-ship subsystem damage.
        /// </summary>
        /// <param name="amount">The nonnegative damage amount.</param>
        /// <param name="random">The deterministic random source used for subsystem damage.</param>
        private void ApplyConventionalDamage(int amount, IRandomNumberProvider random)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || !IsActive)
                return;

            int shieldBefore = Shields;
            int hullBefore = Hull;
            ApplyDamage(Math.Max(1, amount));
            if (Kind != TacticalUnitKind.CapitalShip)
                return;

            if (Shields > 0)
            {
                float shieldLossPercent = Math.Max(
                    1f,
                    100f * (shieldBefore - Shields) / shieldBefore
                );
                if (random.NextInt(0, 101) <= shieldLossPercent)
                    AddSystemDamage(TacticalDamageSystem.ShieldGenerator);
                return;
            }

            if (Hull <= 0 || Hull == hullBefore)
                return;

            float criticalRoll = random.NextInt(0, 101) - 100f * (hullBefore - Hull) / hullBefore;
            if (criticalRoll <= 50f)
                return;
            if (criticalRoll <= 65f)
                AddSystemDamage(TacticalDamageSystem.ShieldGenerator);
            else if (criticalRoll <= 80f)
                AddSystemDamage(TacticalDamageSystem.WeaponSystems);
            else if (criticalRoll <= 90f)
                AddSystemDamage(TacticalDamageSystem.TractorBeam);
            else if (criticalRoll <= 95f)
                AddSystemDamage(TacticalDamageSystem.SublightDrive);
            else
                AddSystemDamage(TacticalDamageSystem.Hyperdrive);
        }

        /// <summary>
        /// Adds one persistent damage level to a subsystem, capped at four.
        /// </summary>
        /// <param name="system">The subsystem receiving damage.</param>
        private void AddSystemDamage(TacticalDamageSystem system)
        {
            int index = (int)system;
            systemDamage[index] = Math.Min(_maximumSystemDamage, systemDamage[index] + 1);
        }

        /// <summary>
        /// Periodically attempts to repair one randomly selected subsystem damage level.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        /// <param name="random">The deterministic random source used for repair rolls.</param>
        private void AdvanceDamageControl(float elapsedTime, IRandomNumberProvider random)
        {
            if (Kind != TacticalUnitKind.CapitalShip || systemDamage.Sum() == 0)
                return;

            damageControlTime -= elapsedTime;
            while (damageControlTime <= 0f)
            {
                damageControlTime += _damageControlInterval;
                if (damageControl <= 0)
                    continue;
                if (random.NextInt(1, 101) > damageControl)
                    continue;

                int selectedDamage = random.NextInt(1, systemDamage.Sum() + 1);
                for (int index = 0; index < systemDamage.Length; index++)
                {
                    if (selectedDamage <= systemDamage[index])
                    {
                        systemDamage[index]--;
                        return;
                    }

                    selectedDamage -= systemDamage[index];
                }
            }
        }

        /// <summary>
        /// Returns the maximum fighter shield pool supported by the surviving squadron.
        /// </summary>
        /// <returns>The current maximum shield strength.</returns>
        private int GetCurrentMaximumShields()
        {
            return Kind == TacticalUnitKind.Fighters && InitialHull > 0
                ? InitialShields * Hull / InitialHull
                : InitialShields;
        }

        /// <summary>
        /// Computes shield recharge after hull condition and subsystem damage.
        /// </summary>
        /// <returns>The effective shield recharge rate.</returns>
        private float GetEffectiveShieldRechargeRate()
        {
            if (InitialHull == 0)
                return 0f;

            float rate = ShieldRechargeRate * (float)Hull / InitialHull;
            return Math.Max(
                0f,
                rate
                    - ShieldRechargeRate
                        * _systemDamagePenalty
                        * GetSystemDamage(TacticalDamageSystem.ShieldGenerator)
            );
        }

        /// <summary>
        /// Computes weapon recharge after hull condition and subsystem damage.
        /// </summary>
        /// <returns>The effective weapon recharge rate.</returns>
        private float GetEffectiveWeaponRechargeRate()
        {
            if (InitialHull == 0)
                return 0f;

            float rate = WeaponRechargeRate * (float)Hull / InitialHull;
            return Math.Max(
                0f,
                rate
                    - WeaponRechargeRate
                        * _systemDamagePenalty
                        * GetSystemDamage(TacticalDamageSystem.WeaponSystems)
            );
        }

        /// <summary>
        /// Clears one arc's active charge so its weapons must recharge before firing again.
        /// </summary>
        /// <param name="arc">The disrupted weapon arc.</param>
        private void DisruptWeaponArc(TacticalWeaponArc arc)
        {
            int index = (int)arc;
            arcCharge[index] = 0f;
            arcChargeRequired[index] = weaponBatteries.Sum(battery => battery.GetCount(arc));
            if (arcChargeRequired[index] > 0f && !rechargingArcs.Contains(arc))
                rechargingArcs.Enqueue(arc);
        }

        /// <summary>
        /// Determines whether a firing arc has recovered enough energy to fire.
        /// </summary>
        /// <param name="arc">The firing arc to inspect.</param>
        /// <returns>True when the arc is ready.</returns>
        private bool IsArcReady(TacticalWeaponArc arc)
        {
            int index = (int)arc;
            return arcChargeRequired[index] <= 0f || arcCharge[index] >= arcChargeRequired[index];
        }

        /// <summary>
        /// Applies available weapon energy to discharged arcs in firing order.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        /// <param name="rechargeRate">The effective recharge rate after tactical damage.</param>
        private void AdvanceWeaponRecharge(float elapsedTime, float rechargeRate)
        {
            if (!IsActive || rechargeRate <= 0f || rechargingArcs.Count == 0)
                return;

            float availableRecharge = rechargeRate * elapsedTime;
            while (availableRecharge > 0f && rechargingArcs.Count > 0)
            {
                TacticalWeaponArc arc = rechargingArcs.Peek();
                int index = (int)arc;
                float remaining = arcChargeRequired[index] - arcCharge[index];
                float applied = Math.Min(remaining, availableRecharge);
                arcCharge[index] += applied;
                availableRecharge -= applied;
                if (arcCharge[index] >= arcChargeRequired[index])
                    rechargingArcs.Dequeue();
            }
        }
    }
}
