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
        public TacticalCombatSystems Systems { get; private set; }
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
        public bool IsTargetable => IsActiveDisplayObject && Systems.IsAlive;
        public bool IsEngaged =>
            Status
                is CombatUnitStatus.Active
                    or CombatUnitStatus.PreparingHyperspace
                    or CombatUnitStatus.EnteringHyperspace
                    or CombatUnitStatus.AwaitingWithdrawal
                    or CombatUnitStatus.AttackingBattleStation;
        public int SquadronSize => Systems.IsAlive ? Math.Max((int)(Systems.Hull / 8), 1) : 0;

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
            Systems = new TacticalCombatSystems(
                IsFighter,
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
            InitializeBatteries(
                IsFighter ? 0 : Systems.GetDamage(TacticalShipSystem.Weapons),
                IsFighter
            );
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
                IsFighter && InitialCarrierID != null && !Systems.HasHyperdrive
                    ? CombatUnitStatus.Docked
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
            return (double)Systems.Hull * strength / Systems.MaximumHull;
        }

        /// <summary>
        /// Calculates the current sublight speed after command support and tractor restraint.
        /// </summary>
        /// <returns>The available movement speed.</returns>
        public float GetSpeed()
        {
            return Systems.GetMovementBudget(CommandBudget, TractorRestraint);
        }

        /// <summary>
        /// Calculates the current maneuver rating for turning and incoming fighter attacks.
        /// </summary>
        /// <returns>The current maneuver rating.</returns>
        public float GetManeuverability()
        {
            return Systems.GetTurnBudget(CommandBudget, TractorRestraint);
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
                ship.InstallLegacyEquipment();
                foreach (HardpointGroup group in ship.HardpointGroups)
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
    }
}
