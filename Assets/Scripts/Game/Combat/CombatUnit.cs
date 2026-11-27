using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Identifies a unit's participation state in a tactical battle.
    /// </summary>
    public enum CombatUnitStatus
    {
        Docked,
        UnderConstruction,
        Active,
        Recovered,
        PreparingHyperspace,
        EnteringHyperspace,
        AwaitingWithdrawal,
        Withdrawn,
        Destroyed,
        AttackingBattleStation,
    }

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
    /// Owns the independently mutable copy of one strategic battle participant.
    /// </summary>
    [PersistableObject]
    public sealed class CombatUnit : BaseGameEntity
    {
        private BaseSceneNode _unit;
        private CapitalShipSystemDamage _fighterDamage;

        [PersistableMember(Name = nameof(Unit))]
        [PersistableInclude(typeof(CapitalShip))]
        [PersistableInclude(typeof(Starfighter))]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private BaseSceneNode SavedUnit
        {
            get => _unit;
            set
            {
                _unit = value;
                if (value is CapitalShip ship && _fighterDamage != null)
                {
                    // A legacy damage element can precede the enclosed ship.
                    ship.SystemDamage = _fighterDamage;
                    _fighterDamage = null;
                }
            }
        }

        [PersistableMember(Name = "BatteryCharge")]
        private float[] _batteryCharge = new float[4];

        [PersistableMember(Name = "BatteryReady")]
        private uint[] _batteryReady = new uint[4];

        [PersistableMember(Name = "BatteryRechargeOrder")]
        private int[] _batteryRechargeOrder = new int[4];

        [PersistableMember(Name = "BatteryRechargeCount")]
        private int _batteryRechargeCount;

        private IReadOnlyList<HardpointGroup> _batteries;

        [PersistableIgnore]
        public IReadOnlyList<HardpointGroup> Batteries => _batteries ??= GetBatteries();

        // Older active-battle XML stored the rechargeable state in its own element.
        [PersistableMember(Name = "Weapons")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private LegacyBattleWeapons LegacyWeapons
        {
            get => null;
            set
            {
                if (value == null)
                    return;
                _batteryCharge = value.Charge ?? _batteryCharge;
                _batteryReady = value.Ready ?? _batteryReady;
                _batteryRechargeOrder = value.RechargeOrder ?? _batteryRechargeOrder;
                _batteryRechargeCount = value.RechargeCount;
            }
        }

        [PersistableObject(Name = "TacticalCombatWeapons")]
        private sealed class LegacyBattleWeapons
        {
            public int[] Strengths { get; set; }
            public float[] Ranges { get; set; }
            public float[] Charge { get; set; }
            public float[] MaximumCharge { get; set; }
            public uint[] Ready { get; set; }
            public int[] RechargeOrder { get; set; }
            public int RechargeCount { get; set; }
        }

        [PersistableMember(Name = "Systems")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private LegacyBattleSystems LegacySystems
        {
            get => null;
            set
            {
                if (value == null)
                    return;
                if (value.ComponentDamage != null)
                    RestoreComponentDamage(value.ComponentDamage);
                _weaponRecharge = value.WeaponRecharge;
                _shieldRecharge = value.ShieldRecharge;
                _movementBudget = value.MovementBudget;
                _tractorBudget = value.TractorBudget;
                _turnBonus = value.TurnBonus;
                _hyperdriveCount = value.HyperdriveCount;
                _damageControl = value.DamageControl;
                _damageEventCount = value.DamageEventCount;
                _repairMilliseconds = value.RepairMilliseconds;
                _shieldLimit = value.ShieldLimit;
                MaximumHull = value.MaximumHull;
                Hull = value.Hull;
                Shields = value.Shields;
                AttackDelay = value.AttackDelay;
                MovementDelay = value.MovementDelay;
            }
        }

        [PersistableObject]
        private sealed class LegacyBattleSystems
        {
            public uint[] ComponentDamage { get; set; }
            public float WeaponRecharge { get; set; }
            public float ShieldRecharge { get; set; }
            public float MovementBudget { get; set; }
            public float TractorBudget { get; set; }
            public float TurnBonus { get; set; }
            public uint HyperdriveCount { get; set; }
            public float DamageControl { get; set; }
            public int CampaignRepairDueTick { get; set; }
            public int CampaignRepairInterval { get; set; }
            public uint DamageEventCount { get; set; }
            public float RepairMilliseconds { get; set; }
            public float ShieldLimit { get; set; }
            public bool IsFighter { get; set; }
            public float MaximumHull { get; set; }
            public float Hull { get; set; }
            public float Shields { get; set; }
            public float AttackDelay { get; set; }
            public float MovementDelay { get; set; }
        }

        public string SourceUnitInstanceID { get; set; }
        public string CarrierSourceUnitInstanceID { get; set; }
        public bool HasPlanetaryProtection { get; set; }
        public int PayloadCount { get; set; }
        public int FighterCapacity { get; set; }

        [PersistableIgnore]
        public BaseSceneNode Unit => _unit;

        [PersistableIgnore]
        public new string InstanceID
        {
            get => Unit?.InstanceID ?? base.InstanceID;
            private set => base.InstanceID = value;
        }

        [PersistableIgnore]
        public new string TypeID
        {
            get => Unit?.TypeID ?? base.TypeID;
            private set => base.TypeID = value;
        }

        [PersistableIgnore]
        public new string DisplayName
        {
            get => Unit?.GetDisplayName() ?? base.DisplayName;
            private set => base.DisplayName = value;
        }
        public CombatSide Side { get; internal set; }
        public bool IsFighter => Unit is Starfighter;
        internal int DriveSlotCount => Unit is CapitalShip ship ? ship.Hyperdrives.Length : 2;
        public bool IsBattleStation => Unit is CapitalShip { CanDestroyPlanets: true };
        public int InitialSquadronSize => (Unit as Starfighter)?.CurrentSquadronSize ?? 0;
        public float InitialHull =>
            Unit is Starfighter fighter
                ? fighter.CurrentSquadronSize * 8
                : (Unit as CapitalShip)?.CurrentHullStrength ?? 0;
        public float TorpedoRange => (Unit as Starfighter)?.TorpedoRange ?? 0;
        public float TractorRange => (Unit as CapitalShip)?.TractorBeamnRange ?? 0;
        public bool HasGravityWell => Unit is CapitalShip { HasGravityWell: true };
        public float CollisionRadius { get; internal set; }
        public float HalfHeight { get; internal set; }
        public Vector3 HullDimensions { get; internal set; }
        public Vector3 Position { get; internal set; }
        public Vector3 Heading { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public CombatUnitStatus Status { get; internal set; }
        public string CarrierID { get; internal set; }
        public float HyperspaceMilliseconds { get; internal set; }
        public float TractorRestraint { get; internal set; }

        /// <summary>
        /// Gets whether the retained tractor-source chain contains any contributor, even with no current restraint.
        /// </summary>
        public bool HasTractorSources { get; internal set; }

        public float CommandBudget { get; internal set; }
        public float MaximumTurnRate { get; internal set; }
        public int Index { get; internal set; }
        public string TractorTargetID { get; internal set; }
        public Vector3 DesiredMovement { get; internal set; }
        public Vector3 DesiredHeading { get; internal set; }
        public float TurnRate { get; internal set; }
        public bool HasCollision { get; internal set; }

        [PersistableMember]
        internal float CollisionRecoveryTime { get; set; }

        [PersistableMember]
        internal int CollisionRecoveryPhase { get; set; }

        /// <summary>
        /// Gets whether the ordinary unit remains in an active display state, independently of hull strength.
        /// </summary>
        public bool IsActiveDisplayObject =>
            !IsBattleStation
            && Status is CombatUnitStatus.Active or CombatUnitStatus.PreparingHyperspace;
        public bool IsTargetable => IsActiveDisplayObject && IsAlive;
        public bool IsEngaged =>
            Status
                is CombatUnitStatus.Active
                    or CombatUnitStatus.PreparingHyperspace
                    or CombatUnitStatus.EnteringHyperspace
                    or CombatUnitStatus.AwaitingWithdrawal
                    or CombatUnitStatus.AttackingBattleStation;
        public int SquadronSize => IsAlive ? Math.Max((int)(Hull / 8), 1) : 0;

        // Older saves also used this element for the capital ship's current battle damage.
        [PersistableMember(Name = "ComponentDamage")]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private CapitalShipSystemDamage SavedFighterDamage
        {
            get => _unit is CapitalShip ? null : _fighterDamage;
            set
            {
                if (value == null)
                    return;
                if (_unit is CapitalShip ship)
                    ship.SystemDamage = value;
                else
                    _fighterDamage = value;
            }
        }

        private CapitalShipSystemDamage ComponentDamage =>
            _unit is CapitalShip ship
                ? ship.SystemDamage ??= new CapitalShipSystemDamage()
                : _fighterDamage ??= new CapitalShipSystemDamage();

        [PersistableMember(Name = "WeaponRecharge")]
        private float _weaponRecharge;

        [PersistableMember(Name = "ShieldRecharge")]
        private float _shieldRecharge;

        [PersistableMember(Name = "MovementBudget")]
        private float _movementBudget;

        [PersistableMember(Name = "TractorBudget")]
        private float _tractorBudget;

        [PersistableMember(Name = "TurnBonus")]
        private float _turnBonus;

        [PersistableMember(Name = "HyperdriveCount")]
        private uint _hyperdriveCount;

        [PersistableMember(Name = "DamageControl")]
        private float _damageControl;

        [PersistableMember(Name = "DamageEventCount")]
        private uint _damageEventCount;

        [PersistableMember(Name = "RepairMilliseconds")]
        private float _repairMilliseconds;

        [PersistableMember(Name = "ShieldLimit")]
        private float _shieldLimit;

        public float MaximumHull { get; private set; }
        public float Hull { get; internal set; }
        public float Shields { get; internal set; }
        public float MaximumShields => _shieldLimit;
        public float AttackDelay { get; internal set; }
        public float MovementDelay { get; internal set; }
        public bool IsAlive => Hull > 0;

        public bool HasHyperdrive => GetDamage(TacticalShipSystem.Hyperdrive) < _hyperdriveCount;
        public double HullFraction => (double)Hull / MaximumHull;

        internal float WeaponRechargeSample
        {
            get => _weaponRecharge;
            set => _weaponRecharge = value;
        }

        internal float ShieldRechargeSample
        {
            get => _shieldRecharge;
            set => _shieldRecharge = value;
        }

        internal float MovementBudget => _movementBudget;
        internal float TractorPower => _tractorBudget;
        internal float Maneuverability => _turnBonus;

        internal float ShieldLimit
        {
            get => _shieldLimit;
            set => _shieldLimit = value;
        }

        internal float RepairMilliseconds
        {
            get => _repairMilliseconds;
            set => _repairMilliseconds = value;
        }

        internal int QueuedBatteryCount => _batteryRechargeCount;

        internal float DamageControl => _damageControl;

        [PersistableIgnore]
        internal uint DamageEventCount
        {
            get => _damageEventCount;
            set => _damageEventCount = value;
        }
        internal bool HasComponentDamage => Unit is CapitalShip || _fighterDamage != null;

        /// <summary>
        /// Captures the campaign unit state used for the duration of one battle.
        /// </summary>
        /// <param name="sourceUnit">The strategic capital ship or starfighter.</param>
        /// <returns>An independently mutable combat copy.</returns>
        public static CombatUnit Create(BaseSceneNode sourceUnit)
        {
            CombatUnit unit = new();
            unit.CaptureSource(sourceUnit);
            return unit;
        }

        /// <summary>
        /// Creates a battle unit for deserialization.
        /// </summary>
        public CombatUnit() { }

        /// <summary>
        /// Copies the campaign unit and captures its starting carrier and payload.
        /// </summary>
        /// <param name="sourceUnit">The campaign unit entering the battle.</param>
        private void CaptureSource(BaseSceneNode sourceUnit)
        {
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (sourceUnit is not CapitalShip && sourceUnit is not Starfighter)
                throw new ArgumentException(
                    "Only capital ships and starfighters can enter a space battle.",
                    nameof(sourceUnit)
                );

            _unit = (BaseSceneNode)sourceUnit.CreateCopy();
            SourceUnitInstanceID = sourceUnit.InstanceID;
            CarrierSourceUnitInstanceID =
                sourceUnit is Starfighter
                    ? (sourceUnit.ParentNode as CapitalShip)?.InstanceID
                    : null;

            if (sourceUnit is CapitalShip ship)
            {
                PayloadCount =
                    ship.GetChildren<Officer>().Count
                    + ship.GetChildren<Regiment>().Count
                    + ship.GetChildren<SpecialForces>().Count;
                int fighterCount = ship.GetChildren<Starfighter>(includeDisabled: true).Count;
                int activeFighterCount = ship.GetChildren<Starfighter>()
                    .Count(fighter =>
                        fighter.ManufacturingStatus == ManufacturingStatus.Complete
                        && fighter.Movement == null
                        && fighter.CurrentSquadronSize > 0
                    );
                FighterCapacity = ship.StarfighterCapacity - fighterCount + activeFighterCount;
            }
        }

        /// <summary>
        /// Indexes the installed batteries in tactical arc order.
        /// </summary>
        /// <returns>The batteries that supply the live weapon state.</returns>
        private IReadOnlyList<HardpointGroup> GetBatteries()
        {
            HardpointGroup[] groups = new HardpointGroup[4];
            if (Unit is CapitalShip { HardpointGroups: { } hardpoints })
            {
                foreach (HardpointGroup group in hardpoints)
                {
                    int index = (int)group.Arc;
                    if (index < 0 || index >= groups.Length || groups[index] != null)
                        throw new ArgumentException(
                            "A ship can have one battery in each firing arc."
                        );
                    groups[index] = group;
                }
            }
            else if (Unit is Starfighter fighter)
            {
                HardpointGroup forward = new() { Arc = TacticalWeaponArc.Forward };
                forward
                    .GetHardpoints()
                    .Add(
                        new Hardpoint
                        {
                            Weapon = new WeaponData
                            {
                                WeaponType = HardpointWeaponType.LaserCannon,
                                Damage = fighter.LaserCannon * fighter.MaxSquadronSize,
                                Range = fighter.LaserRange,
                            },
                        }
                    );
                forward
                    .GetHardpoints()
                    .Add(
                        new Hardpoint
                        {
                            Weapon = new WeaponData
                            {
                                WeaponType = HardpointWeaponType.IonCannon,
                                Damage = fighter.IonCannon * fighter.MaxSquadronSize,
                                Range = fighter.IonRange,
                            },
                        }
                    );
                groups[(int)TacticalWeaponArc.Forward] = forward;
            }

            for (int index = 0; index < groups.Length; index++)
                groups[index] ??= new HardpointGroup { Arc = (TacticalWeaponArc)index };
            return Array.AsReadOnly(groups);
        }

        /// <summary>
        /// Checks whether a battery can acquire a new target and fire.
        /// </summary>
        /// <param name="arc">The battery to inspect.</param>
        /// <returns>Whether its recharge is complete.</returns>
        public bool IsBatteryReady(TacticalWeaponArc arc)
        {
            return _batteryReady[(int)arc] == 1;
        }

        /// <summary>
        /// Reads a battery by its recharge queue position.
        /// </summary>
        /// <param name="index">The zero-based queue position.</param>
        /// <returns>The queued battery arc.</returns>
        internal TacticalWeaponArc GetQueuedBattery(int index) =>
            (TacticalWeaponArc)_batteryRechargeOrder[index];

        /// <summary>
        /// Stores the new charge for a battery.
        /// </summary>
        /// <param name="arc">The affected battery.</param>
        /// <param name="charge">Its new charge.</param>
        internal void SetBatteryCharge(TacticalWeaponArc arc, float charge) =>
            _batteryCharge[(int)arc] = charge;

        /// <summary>
        /// Stores a battery's charge and readiness together.
        /// </summary>
        /// <param name="arc">The affected battery.</param>
        /// <param name="charge">Its new charge.</param>
        /// <param name="ready">Whether the battery can fire.</param>
        internal void SetBatteryCharge(TacticalWeaponArc arc, float charge, bool ready)
        {
            SetBatteryCharge(arc, charge);
            _batteryReady[(int)arc] = ready ? 1u : 0u;
        }

        /// <summary>
        /// Removes a battery from the recharge queue and shifts later entries forward.
        /// </summary>
        /// <param name="index">The completed queue position.</param>
        internal void RemoveQueuedBattery(int index)
        {
            for (int shift = index; shift < 3; shift++)
                _batteryRechargeOrder[shift] = _batteryRechargeOrder[shift + 1];
            if (_batteryRechargeCount == 4)
                _batteryRechargeOrder[3] = 0;
            _batteryRechargeCount--;
        }

        /// <summary>
        /// Gets the charge currently held by one battery.
        /// </summary>
        /// <param name="arc">The battery to inspect.</param>
        /// <returns>The current weapon charge.</returns>
        public float GetBatteryCharge(TacticalWeaponArc arc)
        {
            return _batteryCharge[(int)arc];
        }

        /// <summary>
        /// Gets the stored target used when recharging one battery.
        /// </summary>
        /// <param name="arc">The battery to inspect.</param>
        /// <returns>The battery's recharge target.</returns>
        public float GetBatteryCapacity(TacticalWeaponArc arc) =>
            (float)(
                (double)GetWeaponStrength(arc, TacticalWeaponType.LaserCannon)
                + GetWeaponStrength(arc, TacticalWeaponType.IonCannon)
                + GetWeaponStrength(arc, TacticalWeaponType.Turbolaser)
            );

        /// <summary>
        /// Reads the combined rating for a weapon in one battery arc.
        /// </summary>
        /// <param name="arc">The requested ship-relative weapon arc.</param>
        /// <param name="weapon">The weapon type being read or translated.</param>
        /// <returns>The weapon strength across matching mounts.</returns>
        public int GetWeaponStrength(TacticalWeaponArc arc, TacticalWeaponType weapon) =>
            Batteries[(int)arc].GetStrength(GetMountWeaponType(weapon));

        /// <summary>
        /// Reads the range of a weapon in one battery arc.
        /// </summary>
        /// <param name="arc">The requested ship-relative weapon arc.</param>
        /// <param name="weapon">The weapon type being read or translated.</param>
        /// <returns>The matching weapon range, or zero when absent.</returns>
        public float GetWeaponRange(TacticalWeaponArc arc, TacticalWeaponType weapon) =>
            (float)Batteries[(int)arc].GetRange(GetMountWeaponType(weapon));

        /// <summary>
        /// Maps a tactical discharge to its authored mount category.
        /// </summary>
        /// <param name="weapon">The weapon type being read or translated.</param>
        /// <returns>The corresponding hardpoint weapon category.</returns>
        private static HardpointWeaponType GetMountWeaponType(TacticalWeaponType weapon) =>
            weapon switch
            {
                TacticalWeaponType.LaserCannon => HardpointWeaponType.LaserCannon,
                TacticalWeaponType.IonCannon => HardpointWeaponType.IonCannon,
                TacticalWeaponType.Turbolaser => HardpointWeaponType.Turbolaser,
                _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
            };

        /// <summary>
        /// Consumes the authored strength of a fired lane and schedules its battery for recharge.
        /// All valid lanes on a selected battery may discharge during the same firing command.
        /// </summary>
        /// <param name="arc">The firing battery.</param>
        /// <param name="weapon">The fired battery weapon.</param>
        public void DischargeBattery(TacticalWeaponArc arc, TacticalWeaponType weapon)
        {
            int index = (int)arc;
            _batteryCharge[index] = (float)(
                (double)_batteryCharge[index] - GetWeaponStrength(arc, weapon)
            );
            _batteryReady[index] = 0;
            QueueRecharge(index);
        }

        /// <summary>
        /// Clears a battery's charge after an ion or weapon-system disruption.
        /// </summary>
        /// <param name="arc">The affected battery.</param>
        public void ClearBattery(TacticalWeaponArc arc)
        {
            int index = (int)arc;
            _batteryReady[index] = 0;
            _batteryCharge[index] = 0;
            QueueRecharge(index);
        }

        /// <summary>
        /// Adds one battery to the recharge queue if it is not already present.
        /// </summary>
        /// <param name="arc">The zero-based clockwise battery index.</param>
        private void QueueRecharge(int arc)
        {
            for (int index = 0; index < _batteryRechargeCount; index++)
                if (_batteryRechargeOrder[index] == arc)
                    return;
            _batteryRechargeOrder[_batteryRechargeCount++] = arc;
        }

        /// <summary>
        /// Captures one unit's combat system values before the battle begins.
        /// </summary>
        /// <param name="maximumHull">The complete unit's hull pool.</param>
        /// <param name="currentHull">The surviving hull pool at entry.</param>
        /// <param name="maximumShields">The complete shield pool.</param>
        /// <param name="weaponRechargeRate">The capital ship's weapon recharge rate.</param>
        /// <param name="shieldRechargeRate">The capital ship's shield recharge rate.</param>
        /// <param name="movementBudget">The unit's initial movement budget.</param>
        /// <param name="maneuverability">The unit's maneuverability rating.</param>
        /// <param name="tractorPower">The unit's tractor beam strength.</param>
        /// <param name="hyperdrives">The number of installed hyperdrives.</param>
        /// <param name="damageControlRating">The damage-control repair chance out of 100.</param>
        internal void InitializeCombatPools(
            float maximumHull,
            float currentHull,
            float maximumShields,
            float weaponRechargeRate,
            float shieldRechargeRate,
            float movementBudget,
            float maneuverability,
            float tractorPower,
            uint hyperdrives,
            float damageControlRating
        )
        {
            AttackDelay = MovementDelay = _repairMilliseconds = 0;
            _damageEventCount = 0;
            MaximumHull = maximumHull;
            Hull = currentHull;
            _shieldLimit = maximumShields;
            Shields = maximumShields;
            _weaponRecharge = weaponRechargeRate;
            _shieldRecharge = shieldRechargeRate;
            _movementBudget = movementBudget;
            _turnBonus = maneuverability;
            _tractorBudget = tractorPower;
            _hyperdriveCount = hyperdrives;
            _damageControl = damageControlRating;
        }

        /// <summary>
        /// Captures the component damage for the campaign in the ship's equipment order.
        /// </summary>
        /// <returns>The component damage to commit with the battle result.</returns>
        public CapitalShipSystemDamage CaptureDamage() => ComponentDamage.CreateCopy();

        /// <summary>
        /// Copies the damage recorded at each drive position.
        /// </summary>
        /// <returns>The independent drive damage array.</returns>
        internal int[] CaptureDriveDamage() => (int[])ComponentDamage.HyperdriveDamage.Clone();

        /// <summary>
        /// Checks whether the captured unit has a drive at the given position.
        /// </summary>
        /// <param name="index">The drive index.</param>
        /// <returns>Whether that drive participates in tactical combat.</returns>
        internal bool HasHyperdriveAt(int index)
        {
            int rating = Unit switch
            {
                CapitalShip ship => ship.GetHyperdriveRating(index),
                Starfighter fighter when index == 0 => fighter.Hyperdrive,
                Starfighter fighter when index == 1 => fighter.BackupHyperdrive,
                _ => 0,
            };
            return index == 0 ? rating != 0 : rating > 0;
        }

        /// <summary>
        /// Counts the drives installed per the captured equipment.
        /// </summary>
        /// <returns>The number of valid drives in equipment order.</returns>
        internal uint CountInstalledHyperdrives()
        {
            uint count = 0;
            for (int index = 0; index < DriveSlotCount; index++)
                if (HasHyperdriveAt(index))
                    count++;
            return count;
        }

        /// <summary>
        /// Assigns disabled drives in order, skipping empty positions.
        /// </summary>
        /// <param name="remaining">The total number of disabled drives.</param>
        /// <param name="retainTotal">Whether to retain excess damage from older saved battle pools.</param>
        /// <returns>The drive damage by equipment position.</returns>
        private int[] DistributeDriveDamage(int remaining, bool retainTotal = false)
        {
            int[] damage = new int[DriveSlotCount];
            // An older save can restore component damage before the unit's equipment is read.
            bool equipmentPending = retainTotal && Unit == null;
            for (int index = 0; index < damage.Length && remaining != 0; index++)
            {
                if (!equipmentPending && !HasHyperdriveAt(index))
                    continue;
                damage[index] = Math.Min(remaining, 1);
                remaining -= damage[index];
            }

            if (retainTotal && remaining != 0)
            {
                if (damage.Length == 0)
                    return new[] { remaining };
                damage[damage.Length - 1] += remaining;
            }
            return damage;
        }

        /// <summary>
        /// Gets the accumulated damage level for a component.
        /// </summary>
        /// <param name="system">The component to inspect.</param>
        /// <returns>The component's damage level.</returns>
        public uint GetDamage(TacticalShipSystem system)
        {
            return unchecked((uint)ComponentDamage.GetCombatDamage(system));
        }

        /// <summary>
        /// Scales a value by the remaining hull, rounding after the multiplication and division.
        /// </summary>
        /// <param name="value">The complete unit's value.</param>
        /// <returns>The hull-scaled value.</returns>
        public float ScaleByHull(float value) => (float)((double)Hull * value / MaximumHull);

        /// <summary>
        /// Compares the current shield pool with the current shield limit.
        /// </summary>
        /// <returns>The shield fraction, or zero when its limit is zero or unordered.</returns>
        public double GetShieldFraction() =>
            _shieldLimit == 0 || float.IsNaN(_shieldLimit) ? 0 : (double)Shields / _shieldLimit;

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
        /// Clears a battery's stored recharge position during deployment.
        /// </summary>
        /// <param name="arc">The affected battery.</param>
        internal void ResetBatteryRechargePosition(TacticalWeaponArc arc) =>
            _batteryRechargeOrder[(int)arc] = -1;

        /// <summary>
        /// Gets stored damage at one drive position.
        /// </summary>
        /// <param name="index">The affected drive slot.</param>
        /// <returns>The damage stored at that slot.</returns>
        internal int GetDriveDamage(int index) => ComponentDamage.GetHyperdriveDamage(index);

        /// <summary>
        /// Stores damage at one drive position.
        /// </summary>
        /// <param name="index">The affected drive slot.</param>
        /// <param name="damage">The new damage there.</param>
        internal void SetDriveDamage(int index, int damage) =>
            ComponentDamage.SetHyperdriveDamage(index, damage);

        /// <summary>
        /// Sets the damage recorded for a component system.
        /// </summary>
        /// <param name="system">The affected component.</param>
        /// <param name="damage">The new damage level.</param>
        internal void SetSystemDamage(TacticalShipSystem system, uint damage) =>
            ComponentDamage.SetCombatDamage(system, unchecked((int)damage));

        /// <summary>
        /// Writes a damage level into the shared ship-component state.
        /// </summary>
        /// <param name="system">The affected ship system.</param>
        /// <param name="damage">The new damage level.</param>
        private void SetDamage(TacticalShipSystem system, uint damage)
        {
            if (system == TacticalShipSystem.Hyperdrive)
                ComponentDamage.HyperdriveDamage = DistributeDriveDamage(
                    unchecked((int)damage),
                    retainTotal: true
                );
            else
                SetSystemDamage(system, damage);
        }

        /// <summary>
        /// Restores the ordered component damage values from persisted battle state.
        /// </summary>
        /// <param name="damage">Damage in tactical system registration order.</param>
        /// <param name="driveDamage">Exact damage by drive position when recorded.</param>
        internal void RestoreComponentDamage(uint[] damage, int[] driveDamage = null)
        {
            for (int system = 0; system < damage.Length; system++)
            {
                if (system == (int)TacticalShipSystem.Hyperdrive && driveDamage != null)
                    ComponentDamage.HyperdriveDamage = (int[])driveDamage.Clone();
                else
                    SetDamage((TacticalShipSystem)system, damage[system]);
            }
        }
    }
}
