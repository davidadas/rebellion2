using System;
using System.Collections.Generic;
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
    /// Owns the independently mutable copy of one strategic battle participant.
    /// </summary>
    [PersistableObject]
    public sealed class CombatUnit : BaseGameEntity
    {
        [PersistableMember(Name = nameof(Unit))]
        [PersistableInclude(typeof(CapitalShip))]
        [PersistableInclude(typeof(Starfighter))]
        private BaseSceneNode _unit;

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
        /// Replaces the battle copy's mutable capital-ship result state.
        /// </summary>
        /// <param name="hull">The surviving hull strength.</param>
        /// <param name="systemDamage">The surviving component damage.</param>
        public void ApplyCapitalShipState(int hull, CapitalShipSystemDamage systemDamage)
        {
            if (_unit is not CapitalShip ship)
                throw new InvalidOperationException("The battle unit is not a capital ship.");
            ship.CurrentHullStrength = hull;
            ship.SystemDamage = systemDamage?.CreateCopy() ?? new CapitalShipSystemDamage();
        }

        /// <summary>
        /// Replaces the battle copy's mutable starfighter result state.
        /// </summary>
        /// <param name="squadronSize">The surviving fighter count.</param>
        /// <param name="carrierSourceUnitInstanceID">The campaign carrier identity after recovery.</param>
        public void ApplyStarfighterState(int squadronSize, string carrierSourceUnitInstanceID)
        {
            if (_unit is not Starfighter fighter)
                throw new InvalidOperationException("The battle unit is not a starfighter.");
            fighter.CurrentSquadronSize = squadronSize;
            CarrierSourceUnitInstanceID = carrierSourceUnitInstanceID;
        }

        [PersistableIgnore]
        public int PresentationIndex { get; internal set; }

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
        public CombatSide Side { get; private set; }
        public bool IsFighter => Unit is Starfighter;
        public bool HasPrimaryHyperdrive =>
            Unit is Starfighter fighter
                ? fighter.Hyperdrive != 0
                : Unit is CapitalShip { Hyperdrive: not 0 };
        public bool HasBackupHyperdrive =>
            Unit is CapitalShip { BackupHyperdrive: > 0 } or Starfighter { BackupHyperdrive: > 0 };
        public bool IsBattleStation => Unit is CapitalShip { CanDestroyPlanets: true };
        public int InitialSquadronSize => (Unit as Starfighter)?.CurrentSquadronSize ?? 0;
        public float InitialHull =>
            Unit is Starfighter fighter
                ? fighter.CurrentSquadronSize * 8
                : (Unit as CapitalShip)?.CurrentHullStrength ?? 0;
        public string InitialCarrierID => CarrierSourceUnitInstanceID;
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
        public bool HasTractorSources { get; private set; }

        public float CommandBudget { get; internal set; }
        public float MaximumTurnRate { get; internal set; }
        public int? GroupID { get; internal set; }
        public int? RequestedGroupID { get; internal set; }
        public int Index { get; private set; }
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

        /// <summary>
        /// Announces the first retained source and every removal that leaves the source chain empty.
        /// </summary>
        public event Action<bool> TractorSourcesChanged;

        /// <summary>
        /// Requests the generator's presentation during an admitted active movement interval.
        /// </summary>
        public event Action GravityWellPresentationRequested;

        /// <summary>
        /// Creates tactical simulation state over an independently copied campaign unit.
        /// </summary>
        /// <param name="sourceUnit">The campaign unit to copy into battle.</param>
        /// <param name="side">The participant's encounter side.</param>
        /// <param name="index">The unique battle registration identifier.</param>
        public CombatUnit(BaseSceneNode sourceUnit, CombatSide side, int index)
        {
            CaptureSource(sourceUnit);
            InitializeTacticalState(side, index);
        }

        /// <summary>
        /// Initializes combat pools and equipment from the battle-owned ship copy.
        /// </summary>
        /// <param name="side">The battle side assigned at deployment.</param>
        /// <param name="index">The unit's registration order.</param>
        internal void InitializeTacticalState(CombatSide side, int index)
        {
            BaseSceneNode battleUnit = Unit;
            CapitalShip ship = battleUnit as CapitalShip;
            ship?.InstallLegacyEquipment();
            Starfighter fighter = battleUnit as Starfighter;
            bool isFighter = fighter != null;
            bool isBattleStation = ship?.CanDestroyPlanets == true;
            bool isUnderConstruction =
                isBattleStation && ship.ManufacturingStatus != ManufacturingStatus.Complete;
            float maximumHull = isFighter ? fighter.MaxSquadronSize * 8 : ship.MaxHullStrength;
            float initialHull = isFighter
                ? fighter.CurrentSquadronSize * 8
                : ship.CurrentHullStrength;
            float maximumShields = isFighter
                ? fighter.ShieldStrength * fighter.MaxSquadronSize
                : ship.MaxShieldStrength;
            float weaponRecharge = isFighter ? 0 : ship.WeaponRecharge;
            float shieldRecharge = isFighter ? 0 : ship.ShieldRechargeRate;
            float sublightSpeed = isFighter ? fighter.SublightSpeed : ship.SublightSpeed;
            float maneuverability = isFighter ? fighter.Agility : ship.Maneuverability;
            float tractorBeamPower = isFighter ? 0 : ship.TractorBeamPower;
            float damageControl = isFighter ? 0 : ship.DamageControl;
            CapitalShipSystemDamage systemDamage = ship?.SystemDamage?.CreateCopy();

            Side = side;
            Index = index;
            CarrierID = InitialCarrierID;
            InitializeCombatPools(
                maximumHull,
                initialHull,
                maximumShields,
                weaponRecharge,
                shieldRecharge,
                sublightSpeed,
                maneuverability,
                tractorBeamPower,
                (uint)((HasPrimaryHyperdrive ? 1 : 0) + (HasBackupHyperdrive ? 1 : 0)),
                damageControl,
                systemDamage
            );
            InitializeBatteries(IsFighter ? 0 : GetDamage(TacticalShipSystem.Weapons), IsFighter);
            CapitalShipGeometry geometry = ship?.PhysicalGeometry;
            MaximumTurnRate = geometry?.MaximumTurnRate ?? 0;
            HalfHeight = IsFighter
                ? 0.3f
                : geometry?.HalfHeight ?? CapitalShipGeometry.DefaultHalfHeight;
            CollisionRadius = IsFighter
                ? 1.2f
                : geometry?.CollisionRadius ?? CapitalShipGeometry.DefaultCollisionRadius;
            Heading = Vector3.UnitZ;
            CommandBudget = 1;
            if (geometry is { HullWidth: > 0, HullDepth: > 0 })
                HullDimensions = new Vector3(
                    geometry.HullWidth,
                    geometry.HullHeight,
                    geometry.HullDepth
                );
            DesiredHeading = Heading;
            TurnRate = GetInitialTurnRate();
            Status =
                IsFighter && InitialCarrierID != null && !HasHyperdrive ? CombatUnitStatus.Docked
                : isUnderConstruction ? CombatUnitStatus.UnderConstruction
                : CombatUnitStatus.Active;
        }

        /// <summary>
        /// Calculates the squadron's torpedo attack after entry and battle losses.
        /// </summary>
        /// <returns>The torpedo attack strength.</returns>
        public float GetTorpedoDamage() => (float)GetTorpedoAttack();

        /// <summary>
        /// Retains the hull-scaled torpedo contribution until its consumer chooses the stored boundary.
        /// </summary>
        /// <returns>The live torpedo attack strength after the per-mount scalar store.</returns>
        internal double GetTorpedoAttack()
        {
            float strength = (float)(
                (double)(
                    Unit is Starfighter fighter ? fighter.Torpedoes * fighter.MaxSquadronSize : 0
                ) * 0.53333336f
            );
            return (double)Hull * strength / MaximumHull;
        }

        /// <summary>
        /// Calculates the current sublight speed after command support and tractor restraint.
        /// </summary>
        /// <returns>The available movement speed.</returns>
        public float GetSpeed()
        {
            return GetMovementBudget(CommandBudget, TractorRestraint);
        }

        /// <summary>
        /// Calculates the current maneuver rating for turning and incoming fighter attacks.
        /// </summary>
        /// <returns>The current maneuver rating.</returns>
        public float GetManeuverability()
        {
            return GetTurnBudget(CommandBudget, TractorRestraint);
        }

        /// <summary>
        /// Stores retained tractor membership and announces its admitted transition.
        /// Empty removals are announced even when the previous membership was already empty.
        /// </summary>
        /// <param name="populated">Whether the source chain contains a retained contributor.</param>
        internal void SetTractorSources(bool populated)
        {
            HasTractorSources = populated;
            TractorSourcesChanged?.Invoke(populated);
        }

        /// <summary>
        /// Restores retained membership before separately replaying its ordered presentation edges.
        /// </summary>
        /// <param name="populated">Whether the authoritative source chain contains a contributor.</param>
        internal void ApplySnapshotTractorMembership(bool populated) =>
            HasTractorSources = populated;

        /// <summary>
        /// Announces the gravity generator after its active movement has been applied.
        /// </summary>
        internal void RequestGravityWellPresentation() =>
            GravityWellPresentationRequested?.Invoke();

        /// <summary>
        /// Gets the angular speed selected when the unit entered tactical combat.
        /// </summary>
        /// <returns>The angular speed in radians per second.</returns>
        internal float GetInitialTurnRate()
        {
            float budget = GetManeuverability();
            if (budget <= 1)
                return 0.2617994f;
            float rate = (float)((double)1.5707964f * budget * 0.11111111f);
            if (MaximumTurnRate > 0)
                rate = Math.Min(rate, MaximumTurnRate);
            return rate;
        }

        private void InitializeBatteries(uint weaponDamage, bool isFighter)
        {
            for (int index = 0; index < _batteryCharge.Length; index++)
            {
                TacticalWeaponArc arc = (TacticalWeaponArc)index;
                float capacity = GetBatteryCapacity(arc);
                _batteryCharge[index] = !isFighter && capacity > 0 ? capacity : 0;
                _batteryReady[index] = capacity > 0 ? 1u : 0u;
                _batteryRechargeOrder[index] = -1;
                if (!isFighter && capacity > 0 && weaponDamage >= 4)
                    ClearBattery(arc);
            }
        }

        private IReadOnlyList<HardpointGroup> GetBatteries()
        {
            HardpointGroup[] groups = new HardpointGroup[4];
            if (Unit is CapitalShip ship)
            {
                IReadOnlyList<HardpointGroup> hardpoints = ship.HardpointGroups is { Count: > 0 }
                    ? ship.HardpointGroups
                    : ship.CaptureLegacyHardpoints();
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
        /// Restores visible battery charge without admitting a local discharge or recharge.
        /// </summary>
        /// <param name="charge">The charge by battery.</param>
        /// <param name="ready">Whether each battery can fire.</param>
        internal void ApplyBatteryProjection(float[] charge, bool[] ready)
        {
            for (int arc = 0; arc < 4; arc++)
            {
                _batteryCharge[arc] = charge[arc];
                _batteryReady[arc] = ready[arc] ? 1u : 0u;
            }
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

        public int GetWeaponStrength(TacticalWeaponArc arc, TacticalWeaponType weapon) =>
            Batteries[(int)arc].GetStrength(GetMountWeaponType(weapon));

        public float GetWeaponRange(TacticalWeaponArc arc, TacticalWeaponType weapon) =>
            Batteries[(int)arc].GetRange(GetMountWeaponType(weapon));

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
        /// Advances queued batteries using the unit's recharge budget.
        /// </summary>
        /// <param name="seconds">The elapsed recharge interval in seconds.</param>
        /// <param name="rechargeRate">The unit's available recharge per second.</param>
        public void RechargeBatteries(float seconds, double rechargeRate)
        {
            double available = rechargeRate * seconds;
            for (int order = 0; order < _batteryRechargeCount && available > 0; order++)
            {
                int arc = _batteryRechargeOrder[order];
                float capacity = GetBatteryCapacity((TacticalWeaponArc)arc);
                double remaining = (double)capacity - _batteryCharge[arc];
                if (double.IsNaN(remaining) || available < remaining)
                {
                    _batteryCharge[arc] = (float)(available + _batteryCharge[arc]);
                    // Partial fills retain the remaining budget for subsequent batteries.
                }
                else
                {
                    _batteryCharge[arc] = capacity;
                    available -= remaining;
                    _batteryReady[arc] = 1;
                }
            }

            for (int order = 0; order < _batteryRechargeCount; order++)
            {
                if (_batteryReady[_batteryRechargeOrder[order]] != 1)
                    continue;
                for (int shift = order; shift < 3; shift++)
                    _batteryRechargeOrder[shift] = _batteryRechargeOrder[shift + 1];
                if (_batteryRechargeCount == 4)
                    _batteryRechargeOrder[3] = 0;
                _batteryRechargeCount--;
                // The shifted entry is revisited during the next recharge pass.
            }
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

        [PersistableMember(Name = "ComponentDamage")]
        private CapitalShipSystemDamage _battleDamage = new();

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
        public event Action<TacticalSystemEvent> SystemEventRequested;

        /// <summary>
        /// Reports a lethal weapon impact while its resulting hull value is still stored.
        /// The argument identifies the firing unit.
        /// </summary>
        public event Action<string> LethalHit;

        /// <summary>
        /// Requests the weapon's impact effect after the affected surface and hull state are stored.
        /// </summary>
        public event Action<TacticalWeaponType, TacticalCombatImpactKind> ImpactRequested;

        internal float WeaponRechargeSample => _weaponRecharge;
        internal float ShieldRechargeSample => _shieldRecharge;

        /// <summary>
        /// Restores authoritative visible pools and components without applying damage or requesting repairs.
        /// </summary>
        /// <param name="hull">The current hull pool.</param>
        /// <param name="shields">The current shield pool.</param>
        /// <param name="shieldLimit">The current shield capacity.</param>
        /// <param name="attackDelay">The remaining firing delay.</param>
        /// <param name="movementDelay">The remaining movement delay.</param>
        /// <param name="weaponRecharge">The retained weapon recharge sample.</param>
        /// <param name="shieldRecharge">The retained shield recharge sample.</param>
        /// <param name="componentDamage">Damage by ship system.</param>
        internal void ApplySystemProjection(
            float hull,
            float shields,
            float shieldLimit,
            float attackDelay,
            float movementDelay,
            float weaponRecharge,
            float shieldRecharge,
            uint[] componentDamage
        )
        {
            Hull = hull;
            Shields = shields;
            _shieldLimit = shieldLimit;
            AttackDelay = attackDelay;
            MovementDelay = movementDelay;
            _weaponRecharge = weaponRecharge;
            _shieldRecharge = shieldRecharge;
            RestoreComponentDamage(componentDamage);
        }

        /// <summary>
        /// Captures one unit's combat system values before the battle begins.
        /// </summary>
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
        internal void InitializeCombatPools(
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
            bool isFighter = IsFighter;
            AttackDelay = MovementDelay = _repairMilliseconds = 0;
            _damageEventCount = 0;
            _battleDamage = initialDamage?.CreateCopy() ?? new CapitalShipSystemDamage();
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
                // Existing weapon damage does not seed repair work; later weapon hits add events.
                _damageEventCount =
                    GetDamage(TacticalShipSystem.Engines)
                    + GetDamage(TacticalShipSystem.Shields)
                    + GetDamage(TacticalShipSystem.Hyperdrive)
                    + GetDamage(TacticalShipSystem.TractorBeam);
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
            int remaining = (int)GetDamage(TacticalShipSystem.Hyperdrive);
            int primary = primaryHyperdriveInstalled ? Math.Min(remaining, 1) : 0;
            remaining -= primary;
            return new CapitalShipSystemDamage()
            {
                Shields = (int)GetDamage(TacticalShipSystem.Shields),
                Weapons = (int)GetDamage(TacticalShipSystem.Weapons),
                TractorBeam = (int)GetDamage(TacticalShipSystem.TractorBeam),
                Engines = (int)GetDamage(TacticalShipSystem.Engines),
                PrimaryHyperdrive = primary,
                BackupHyperdrive = backupHyperdriveInstalled ? Math.Min(remaining, 1) : 0,
            };
        }

        /// <summary>
        /// Gets the accumulated damage level for a component.
        /// </summary>
        /// <param name="system">The component to inspect.</param>
        /// <returns>The component's damage level.</returns>
        public uint GetDamage(TacticalShipSystem system)
        {
            return unchecked((uint)_battleDamage.GetCombatDamage(system));
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
                scaled - (double)_weaponRecharge * 0.25f * GetDamage(TacticalShipSystem.Weapons);
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
                scaled - (double)_shieldRecharge * 0.25f * GetDamage(TacticalShipSystem.Shields);
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
            double consumed = total * 0.25f * GetDamage(TacticalShipSystem.Engines);
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
                scaled - (double)_tractorBudget * 0.25f * GetDamage(TacticalShipSystem.TractorBeam);
            return remaining < 0 || double.IsNaN(remaining) ? 0 : remaining;
        }

        /// <summary>
        /// Applies a queued component hit or repair.
        /// </summary>
        /// <param name="system">The affected component.</param>
        /// <param name="isRepair">Whether to repair one damage level.</param>
        public void ApplyComponentEvent(TacticalShipSystem system, bool isRepair)
        {
            uint damage = GetDamage(system);
            if (isRepair)
            {
                if (_damageEventCount == 0)
                    return;
                _damageEventCount--;
                SetDamage(system, damage - 1);
                return;
            }

            _damageEventCount++;
            uint limit = system == TacticalShipSystem.Hyperdrive ? _hyperdriveCount : 4;
            SetDamage(system, Math.Min(damage + 1, limit));
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

        internal float DamageControl => _damageControl;
        internal uint DamageEventCount => _damageEventCount;

        internal void NotifyImpact(TacticalWeaponType weapon, TacticalCombatImpactKind impact) =>
            ImpactRequested?.Invoke(weapon, impact);

        internal void NotifyLethalHit(string attackerID) => LethalHit?.Invoke(attackerID);

        internal void NotifySystemEvent(TacticalSystemEvent change) =>
            SystemEventRequested?.Invoke(change);

        private void SetDamage(TacticalShipSystem system, uint damage) =>
            _battleDamage.SetCombatDamage(system, unchecked((int)damage));

        private void RestoreComponentDamage(uint[] damage)
        {
            for (int system = 0; system < damage.Length; system++)
                SetDamage((TacticalShipSystem)system, damage[system]);
        }
    }
}
