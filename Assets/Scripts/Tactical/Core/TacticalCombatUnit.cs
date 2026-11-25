using System;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Identifies a unit's participation state in a tactical battle.
    /// </summary>
    public enum TacticalCombatUnitStatus
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
    public sealed class TacticalCombatUnit
    {
        public CombatUnit Combatant { get; }
        public TacticalCombatUnitDefinition Definition { get; }
        public string InstanceID { get; }
        public string TypeID { get; }
        public string DisplayName { get; }
        public CombatSide Side { get; }
        public TacticalCombatSystems Systems { get; }
        public TacticalCombatWeapons Weapons { get; }
        public bool IsFighter { get; }
        public bool HasPrimaryHyperdrive { get; }
        public bool HasPlanetaryProtection { get; }
        public bool IsBattleStation { get; }
        public int InitialSquadronSize { get; }
        public float InitialHull { get; }
        public string InitialCarrierID { get; }
        public float TorpedoRange { get; }
        public float TractorRange { get; }
        public bool HasGravityWell { get; }
        public int FighterCapacity { get; }
        public int PayloadCount { get; }
        public float CollisionRadius { get; internal set; }
        public float HalfHeight { get; internal set; }
        public Vector3 HullDimensions { get; internal set; }
        public Vector3 Position { get; internal set; }
        public Vector3 Heading { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public TacticalCombatUnitStatus Status { get; internal set; }
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
        public int Index { get; }
        public string TractorTargetID { get; internal set; }
        public Vector3 DesiredMovement { get; internal set; }
        public Vector3 DesiredHeading { get; internal set; }
        public float TurnRate { get; internal set; }
        public bool HasCollision { get; internal set; }
        internal float CollisionRecoveryTime { get; set; }
        internal int CollisionRecoveryPhase { get; set; }
        public float Proximity =>
            (float)(
                TacticalCombatGeometry.GetMovementLength(Velocity) * 0.5
                + (double)CollisionRadius * 0.5555555820465088f
            );

        /// <summary>
        /// Gets whether the ordinary unit remains in an active display state, independently of hull strength.
        /// </summary>
        public bool IsActiveDisplayObject =>
            !IsBattleStation
            && Status
                is TacticalCombatUnitStatus.Active
                    or TacticalCombatUnitStatus.PreparingHyperspace;
        public bool IsTargetable => IsActiveDisplayObject && Systems.IsAlive;
        public bool IsEngaged =>
            Status
                is TacticalCombatUnitStatus.Active
                    or TacticalCombatUnitStatus.PreparingHyperspace
                    or TacticalCombatUnitStatus.EnteringHyperspace
                    or TacticalCombatUnitStatus.AwaitingWithdrawal
                    or TacticalCombatUnitStatus.AttackingBattleStation;
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
        /// <param name="combatant">The battle-owned unit copy.</param>
        /// <param name="side">The participant's encounter side.</param>
        /// <param name="index">The unique battle registration identifier.</param>
        /// <param name="definition">The authored tactical unit definition.</param>
        public TacticalCombatUnit(
            CombatUnit combatant,
            CombatSide side,
            int index,
            TacticalCombatUnitDefinition definition = null
        )
        {
            Combatant = combatant ?? throw new ArgumentNullException(nameof(combatant));
            BaseSceneNode battleUnit = combatant.Unit;
            if (battleUnit is not CapitalShip && battleUnit is not Starfighter)
                throw new ArgumentException(
                    "The combatant must contain a capital ship or starfighter copy.",
                    nameof(combatant)
                );

            CapitalShip ship = battleUnit as CapitalShip;
            Starfighter fighter = battleUnit as Starfighter;
            bool isFighter = fighter != null;
            bool isBattleStation = ship?.CanDestroyPlanets == true;
            bool isUnderConstruction =
                isBattleStation && ship.ManufacturingStatus != ManufacturingStatus.Complete;
            bool hasPrimaryHyperdrive = isFighter ? fighter.Hyperdrive != 0 : ship.Hyperdrive != 0;
            float maximumHull = isFighter ? fighter.MaxSquadronSize * 8 : ship.MaxHullStrength;
            float initialHull = isFighter
                ? fighter.CurrentSquadronSize * 8
                : ship.CurrentHullStrength;
            int initialSquadronSize = isFighter ? fighter.CurrentSquadronSize : 0;
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
            int torpedoStrength = 0;
            float torpedoRange = 0;
            if (isFighter)
            {
                weaponStrengths[0, 0] = fighter.LaserCannon * fighter.MaxSquadronSize;
                weaponStrengths[0, 1] = fighter.IonCannon * fighter.MaxSquadronSize;
                weaponRanges[0] = fighter.LaserRange;
                weaponRanges[1] = fighter.IonRange;
                torpedoStrength = fighter.Torpedoes * fighter.MaxSquadronSize;
                torpedoRange = fighter.TorpedoRange;
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
            InstanceID = battleUnit.InstanceID;
            TypeID = battleUnit.TypeID;
            DisplayName = battleUnit.GetDisplayName();
            Side = side;
            Index = index;
            IsFighter = isFighter;
            IsBattleStation = isBattleStation;
            HasPrimaryHyperdrive = hasPrimaryHyperdrive;
            HasPlanetaryProtection = combatant.HasPlanetaryProtection;
            InitialHull = initialHull;
            InitialSquadronSize = initialSquadronSize;
            InitialCarrierID = combatant.CarrierSourceUnitInstanceID;
            CarrierID = InitialCarrierID;
            PayloadCount = combatant.PayloadCount;
            Systems = new TacticalCombatSystems(
                IsFighter,
                maximumHull,
                InitialHull,
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
            _torpedoStrength = torpedoStrength;
            TorpedoRange = torpedoRange;
            TractorRange = ship?.TractorBeamnRange ?? 0;
            HasGravityWell = ship?.HasGravityWell == true;
            FighterCapacity = combatant.FighterCapacity;
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
                    ? TacticalCombatUnitStatus.Docked
                : isUnderConstruction ? TacticalCombatUnitStatus.UnderConstruction
                : TacticalCombatUnitStatus.Active;
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
            float strength = (float)((double)_torpedoStrength * 0.53333336f);
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
