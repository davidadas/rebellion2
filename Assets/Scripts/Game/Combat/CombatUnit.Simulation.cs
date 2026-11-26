using System;
using System.Numerics;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
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
    /// Captures one campaign unit's mutable tactical state without changing the campaign graph.
    /// </summary>
    public sealed partial class CombatUnit
    {
        [PersistableIgnore]
        public TacticalCombatUnitDefinition Definition { get; internal set; }

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
        public TacticalCombatWeapons Weapons { get; private set; }
        public bool IsFighter => Unit is Starfighter;
        public bool HasPrimaryHyperdrive =>
            Unit is Starfighter fighter
                ? fighter.Hyperdrive != 0
                : Unit is CapitalShip { Hyperdrive: not 0 };
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
        public string TargetID { get; internal set; }
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

        private readonly int _torpedoStrength;

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
        /// <param name="definition">The authored tactical unit definition.</param>
        public CombatUnit(
            BaseSceneNode sourceUnit,
            CombatSide side,
            int index,
            TacticalCombatUnitDefinition definition = null
        )
        {
            CaptureSource(sourceUnit);
            InitializeTacticalState(side, index, definition);
        }

        /// <summary>
        /// Initializes combat pools and equipment from the battle-owned ship copy.
        /// </summary>
        /// <param name="side">The battle side assigned at deployment.</param>
        /// <param name="index">The unit's registration order.</param>
        /// <param name="definition">The authored equipment and geometry for the unit type.</param>
        internal void InitializeTacticalState(
            CombatSide side,
            int index,
            TacticalCombatUnitDefinition definition
        )
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
            int[,] weaponStrengths = new int[4, 3];
            float[] weaponRanges = new float[3];
            if (isFighter)
            {
                weaponStrengths[0, 0] = fighter.LaserCannon * fighter.MaxSquadronSize;
                weaponStrengths[0, 1] = fighter.IonCannon * fighter.MaxSquadronSize;
                weaponRanges[0] = fighter.LaserRange;
                weaponRanges[1] = fighter.IonRange;
            }
            else
            {
                int[] sourceArcs = { 0, 3, 1, 2 };
                PrimaryWeaponType[] sourceWeapons =
                {
                    PrimaryWeaponType.LaserCannon,
                    PrimaryWeaponType.IonCannon,
                    PrimaryWeaponType.Turbolaser,
                };
                for (int weapon = 0; weapon < sourceWeapons.Length; weapon++)
                {
                    int[] values = ship.PrimaryWeapons[sourceWeapons[weapon]];
                    weaponRanges[weapon] = values[CapitalShip.PrimaryWeaponRangeIndex];
                    for (int arc = 0; arc < sourceArcs.Length; arc++)
                        weaponStrengths[arc, weapon] = values[sourceArcs[arc]];
                }
            }

            Definition = definition;
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
                (uint)((HasPrimaryHyperdrive ? 1 : 0) + (definition?.BackupHyperdrive > 0 ? 1 : 0)),
                damageControl,
                systemDamage
            );
            Weapons = new TacticalCombatWeapons(
                weaponStrengths,
                weaponRanges,
                IsFighter ? 0 : Systems.GetDamage(TacticalShipSystem.Weapons),
                IsFighter
            );
            MaximumTurnRate =
                !IsFighter && definition?.PresentationIndex == 21
                    ? (float)((double)1.5707964f * 0.033333335f)
                    : 0;
            HalfHeight = IsFighter ? 0.3f : 3;
            CollisionRadius =
                IsFighter ? 1.2f
                : definition?.PresentationIndex == 21 ? 19
                : 3.6f;
            Heading = Vector3.UnitZ;
            CommandBudget = 1;
            if (!IsFighter && definition?.HullWidth > 0 && definition.HullDepth > 0)
                HullDimensions = new Vector3(
                    definition.HullWidth,
                    definition.HullHeight,
                    definition.HullDepth
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
    }
}
