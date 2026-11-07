using System;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
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
        public ISceneNode Source { get; }
        public TacticalCombatUnitDefinition Definition { get; }
        public string InstanceID { get; }
        public string TypeID { get; }
        public string DisplayName { get; }
        public CombatSide Side { get; }
        public TacticalCombatSystems Systems { get; }
        public TacticalCombatWeapons Weapons { get; }
        public bool IsFighter => Source is Starfighter;
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
        public float CommandBudget { get; internal set; }
        public float MaximumTurnRate { get; internal set; }
        public int GroupID { get; internal set; }
        public int Index { get; }
        public string TractorTargetID { get; internal set; }
        public Vector3 DesiredMovement { get; internal set; }
        public Vector3 DesiredHeading { get; internal set; }
        public float TurnRate { get; internal set; }
        public bool HasCollision { get; internal set; }
        public float Proximity =>
            (float)(
                TacticalCombatGeometry.GetLength(Velocity) * 0.5
                + CollisionRadius * 0.5555555820465088f
            );
        public bool IsTargetable =>
            !IsBattleStation
            && Systems.IsAlive
            && Status
                is TacticalCombatUnitStatus.Active
                    or TacticalCombatUnitStatus.PreparingHyperspace;
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
        /// Captures one capital ship at the start of an encounter.
        /// </summary>
        /// <param name="ship">The campaign ship.</param>
        /// <param name="side">The ship's encounter side.</param>
        /// <param name="index">The unit's unique battle registration identifier.</param>
        /// <param name="definition">The shared tactical unit definition.</param>
        public TacticalCombatUnit(
            CapitalShip ship,
            CombatSide side,
            int index,
            TacticalCombatUnitDefinition definition = null
        )
        {
            Source = ship ?? throw new ArgumentNullException(nameof(ship));
            Definition = definition;
            InstanceID = ship.InstanceID;
            TypeID = ship.TypeID;
            DisplayName = ship.GetDisplayName();
            Side = side;
            Index = index;
            InitialHull = ship.CurrentHullStrength;
            IsBattleStation = ship.CanDestroyPlanets;
            PayloadCount =
                ship.GetChildren<Officer>().Count
                + ship.GetChildren<Regiment>().Count
                + ship.GetChildren<SpecialForces>().Count;
            Systems = new TacticalCombatSystems(
                false,
                ship.MaxHullStrength,
                ship.CurrentHullStrength,
                ship.MaxShieldStrength,
                ship.WeaponRecharge,
                ship.ShieldRechargeRate,
                ship.SublightSpeed,
                ship.Maneuverability,
                ship.TractorBeamPower,
                (uint)((ship.Hyperdrive != 0 ? 1 : 0) + (definition?.BackupHyperdrive > 0 ? 1 : 0)),
                ship.DamageControl,
                ship.SystemDamage
            );
            int[,] strengths = new int[4, 3];
            int[] sourceArcs = { 0, 3, 1, 2 };
            PrimaryWeaponType[] sourceWeapons =
            {
                PrimaryWeaponType.LaserCannon,
                PrimaryWeaponType.IonCannon,
                PrimaryWeaponType.Turbolaser,
            };
            float[] ranges = new float[3];
            for (int weapon = 0; weapon < 3; weapon++)
            {
                int[] values = ship.PrimaryWeapons[sourceWeapons[weapon]];
                ranges[weapon] = values[CapitalShip.PrimaryWeaponRangeIndex];
                for (int arc = 0; arc < 4; arc++)
                    strengths[arc, weapon] = values[sourceArcs[arc]];
            }
            Weapons = new TacticalCombatWeapons(
                strengths,
                ranges,
                Systems.GetDamage(TacticalShipSystem.Weapons)
            );
            TractorRange = ship.TractorBeamnRange;
            HasGravityWell = ship.HasGravityWell;
            FighterCapacity =
                ship.StarfighterCapacity
                - ship.GetChildren<Starfighter>(includeDisabled: true).Count
                + ship.GetChildren<Starfighter>().Count(SpaceCombatQueries.IsActiveStarfighter);
            MaximumTurnRate = definition?.MaximumTurnRate ?? 0;
            HalfHeight = 3;
            CollisionRadius = 3.6f;
            Heading = Vector3.UnitZ;
            Status =
                IsBattleStation && ship.ManufacturingStatus != ManufacturingStatus.Complete
                    ? TacticalCombatUnitStatus.UnderConstruction
                    : TacticalCombatUnitStatus.Active;
            CommandBudget = 1;
            if (definition?.HullWidth > 0 && definition.HullDepth > 0)
            {
                HullDimensions = new Vector3(
                    definition.HullWidth,
                    definition.HullHeight,
                    definition.HullDepth
                );
                HalfHeight = Math.Max(definition.HullHeight * 0.5f, 0.5f);
                CollisionRadius = Math.Max(
                    (float)
                        Math.Sqrt(
                            (double)definition.HullWidth * definition.HullWidth
                                + (double)definition.HullDepth * definition.HullDepth
                        ),
                    0.5f
                );
            }
            DesiredHeading = Heading;
            TurnRate = GetInitialTurnRate();
        }

        /// <summary>
        /// Captures a fighter squadron, preserving its carrier and entry losses.
        /// </summary>
        /// <param name="fighter">The campaign squadron.</param>
        /// <param name="side">The squadron's encounter side.</param>
        /// <param name="index">The unit's unique battle registration identifier.</param>
        /// <param name="definition">The shared tactical unit definition.</param>
        public TacticalCombatUnit(
            Starfighter fighter,
            CombatSide side,
            int index,
            TacticalCombatUnitDefinition definition = null
        )
        {
            Source = fighter ?? throw new ArgumentNullException(nameof(fighter));
            Definition = definition;
            InstanceID = fighter.InstanceID;
            TypeID = fighter.TypeID;
            DisplayName = fighter.GetDisplayName();
            Side = side;
            Index = index;
            InitialSquadronSize = fighter.CurrentSquadronSize;
            InitialHull = fighter.CurrentSquadronSize * 8;
            InitialCarrierID = (fighter.GetParent() as CapitalShip)?.InstanceID;
            CarrierID = InitialCarrierID;
            Systems = new TacticalCombatSystems(
                true,
                fighter.MaxSquadronSize * 8,
                InitialHull,
                fighter.ShieldStrength * fighter.MaxSquadronSize,
                0,
                0,
                fighter.SublightSpeed,
                fighter.Agility,
                0,
                fighter.Hyperdrive != 0 ? 1u : 0u,
                0
            );
            int[,] strengths = new int[4, 3];
            strengths[0, 0] = fighter.LaserCannon * fighter.MaxSquadronSize;
            strengths[0, 1] = fighter.IonCannon * fighter.MaxSquadronSize;
            Weapons = new TacticalCombatWeapons(
                strengths,
                new float[] { fighter.LaserRange, fighter.IonRange, 0 },
                0
            );
            _torpedoStrength = fighter.Torpedoes * fighter.MaxSquadronSize;
            TorpedoRange = fighter.TorpedoRange;
            CollisionRadius = 1.2f;
            HalfHeight = 0.3f;
            Heading = Vector3.UnitZ;
            CommandBudget = 1;
            DesiredHeading = Heading;
            TurnRate = GetInitialTurnRate();
            Status =
                InitialCarrierID != null && !Systems.HasHyperdrive
                    ? TacticalCombatUnitStatus.Docked
                    : TacticalCombatUnitStatus.Active;
        }

        /// <summary>
        /// Calculates the squadron's torpedo attack after entry and battle losses.
        /// </summary>
        /// <returns>The torpedo attack strength.</returns>
        public float GetTorpedoDamage()
        {
            float strength = (float)((double)_torpedoStrength * 0.53333336f);
            return (float)((double)Systems.Hull * strength / Systems.MaximumHull);
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
