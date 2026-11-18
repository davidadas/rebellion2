using System;
using System.Numerics;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;

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
        /// Creates independent battle state from the participant values captured at encounter entry.
        /// </summary>
        /// <param name="setup">The detached hardware and initial participation values.</param>
        /// <param name="side">The participant's encounter side.</param>
        /// <param name="index">The unique battle registration identifier.</param>
        /// <param name="definition">The authored tactical unit definition.</param>
        public TacticalCombatUnit(
            TacticalCombatUnitSetup setup,
            CombatSide side,
            int index,
            TacticalCombatUnitDefinition definition = null
        )
        {
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));
            Definition = definition;
            InstanceID = setup.InstanceID;
            TypeID = setup.TypeID;
            DisplayName = setup.DisplayName;
            Side = side;
            Index = index;
            IsFighter = setup.IsFighter;
            IsBattleStation = setup.IsBattleStation;
            HasPrimaryHyperdrive = setup.HasPrimaryHyperdrive;
            HasPlanetaryProtection = setup.HasPlanetaryProtection;
            InitialHull = setup.InitialHull;
            InitialSquadronSize = setup.InitialSquadronSize;
            InitialCarrierID = setup.InitialCarrierID;
            CarrierID = InitialCarrierID;
            PayloadCount = setup.PayloadCount;
            Systems = new TacticalCombatSystems(
                IsFighter,
                setup.MaximumHull,
                InitialHull,
                setup.MaximumShields,
                setup.WeaponRecharge,
                setup.ShieldRecharge,
                setup.SublightSpeed,
                setup.Maneuverability,
                setup.TractorBeamPower,
                (uint)((HasPrimaryHyperdrive ? 1 : 0) + (definition?.BackupHyperdrive > 0 ? 1 : 0)),
                setup.DamageControl,
                setup.SystemDamage
            );
            Weapons = new TacticalCombatWeapons(
                setup.WeaponStrengths,
                setup.WeaponRanges,
                IsFighter ? 0 : Systems.GetDamage(TacticalShipSystem.Weapons),
                IsFighter
            );
            _torpedoStrength = setup.TorpedoStrength;
            TorpedoRange = setup.TorpedoRange;
            TractorRange = setup.TractorRange;
            HasGravityWell = setup.HasGravityWell;
            FighterCapacity = setup.FighterCapacity;
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
                : setup.IsUnderConstruction ? TacticalCombatUnitStatus.UnderConstruction
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
        internal void ApplyNetworkTractorMembership(bool populated) =>
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
