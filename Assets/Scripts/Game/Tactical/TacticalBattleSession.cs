using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Identifies the active stage of a tactical battle.
    /// </summary>
    public enum TacticalBattlePhase
    {
        /// <summary>The participating ships are entering their battle positions.</summary>
        Arrival = 0,

        /// <summary>The participating sides can execute tactical commands and attacks.</summary>
        Engagement = 1,
    }

    /// <summary>
    /// Owns the isolated tactical state for one pending strategic encounter.
    /// </summary>
    public sealed class TacticalBattleSession
    {
        private const float _arrivalDuration = 1f;
        private const float _immediateResultStep = 1f / 60f;
        private const int _maximumImmediateResultSteps = 216000;
        private static readonly float[] ArrivalDistances = { 40f, 57.5f, 65f, 32.5f };
        private readonly List<TacticalShipGroup> groups = new List<TacticalShipGroup>();
        private readonly ReadOnlyCollection<TacticalShipGroup> groupView;
        private readonly Dictionary<
            TacticalBattleSide,
            IReadOnlyList<TacticalShipGroup>
        > fighterGroups = new Dictionary<TacticalBattleSide, IReadOnlyList<TacticalShipGroup>>();
        private readonly Dictionary<
            TacticalBattleSide,
            IReadOnlyList<TacticalShipGroup>
        > taskForces = new Dictionary<TacticalBattleSide, IReadOnlyList<TacticalShipGroup>>();
        private readonly IRandomNumberProvider random;
        private readonly ReadOnlyCollection<TacticalUnitState> units;
        private readonly TacticalBattleSimulator simulator;
        private readonly TacticalCommandAutomation commandAutomation;
        private float arrivalElapsedTime;
        private int pauseCount;

        /// <summary>
        /// Gets the pending strategic encounter represented by this session.
        /// </summary>
        public PendingCombatResult Encounter { get; }

        /// <summary>
        /// Gets every tactical unit participating in the encounter.
        /// </summary>
        public IReadOnlyList<TacticalUnitState> Units => units;

        /// <summary>
        /// Gets the tactical command groups created during the battle.
        /// </summary>
        public IReadOnlyList<TacticalShipGroup> Groups => groupView;

        /// <summary>
        /// Gets the fixed tactical waypoint-marker lattice.
        /// </summary>
        public TacticalNavigationGrid NavigationGrid { get; }

        /// <summary>
        /// Gets the active tactical battle stage.
        /// </summary>
        public TacticalBattlePhase Phase { get; private set; } = TacticalBattlePhase.Arrival;

        /// <summary>
        /// Gets whether one or both sides no longer have an active tactical unit.
        /// </summary>
        public bool IsComplete =>
            !HasActiveUnits(TacticalBattleSide.Attacker)
            || !HasActiveUnits(TacticalBattleSide.Defender);

        /// <summary>
        /// Gets whether one or more tactical systems currently hold the simulation paused.
        /// </summary>
        public bool IsPaused => pauseCount > 0;

        private TacticalBattleSession(
            PendingCombatResult encounter,
            IList<TacticalUnitState> units,
            IRandomNumberProvider random
        )
        {
            Encounter = encounter;
            groupView = groups.AsReadOnly();
            this.random = random;
            this.units = new ReadOnlyCollection<TacticalUnitState>(units);
            NavigationGrid = new TacticalNavigationGrid(TacticalBattleSimulator.BattlefieldScale);
            BuildCommandGroups();
            commandAutomation = new TacticalCommandAutomation(this.units, groupView);
            simulator = new TacticalBattleSimulator(
                this.units,
                groupView,
                BuildFighterCommandBudgets(encounter),
                random
            );
        }

        /// <summary>
        /// Creates an isolated tactical session from a pending fleet encounter.
        /// </summary>
        /// <param name="encounter">The pending strategic encounter.</param>
        /// <param name="random">The game's deterministic random source.</param>
        /// <returns>The initialized tactical session.</returns>
        public static TacticalBattleSession Create(
            PendingCombatResult encounter,
            IRandomNumberProvider random
        )
        {
            if (encounter == null)
                throw new ArgumentNullException(nameof(encounter));
            if (encounter.AttackerFleet == null)
                throw new ArgumentException("Attacking fleet is required.", nameof(encounter));
            if (encounter.DefenderFleet == null)
                throw new ArgumentException("Defending fleet is required.", nameof(encounter));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            List<TacticalUnitState> units = new List<TacticalUnitState>();
            AddFleet(units, encounter.AttackerFleet, TacticalBattleSide.Attacker);
            AddFleet(units, encounter.DefenderFleet, TacticalBattleSide.Defender);
            AddPlanetStarfighters(
                units,
                encounter.Planet,
                encounter.AttackerOwnerInstanceID,
                TacticalBattleSide.Attacker
            );
            AddPlanetStarfighters(
                units,
                encounter.Planet,
                encounter.DefenderOwnerInstanceID,
                TacticalBattleSide.Defender
            );
            return new TacticalBattleSession(encounter, units, random);
        }

        /// <summary>
        /// Builds the strategic combat result after one side can no longer fight.
        /// </summary>
        /// <returns>The completed result without applying it to the strategic game.</returns>
        public SpaceCombatResult BuildResult()
        {
            if (!IsComplete)
                throw new InvalidOperationException("Tactical combat is still active.");

            bool attackerActive = HasActiveUnits(TacticalBattleSide.Attacker);
            bool defenderActive = HasActiveUnits(TacticalBattleSide.Defender);
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = Encounter.AttackerFleet,
                DefenderFleet = Encounter.DefenderFleet,
                AttackerOwnerInstanceID = Encounter.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = Encounter.DefenderOwnerInstanceID,
                Planet = Encounter.Planet,
                Winner = DetermineWinner(attackerActive, defenderActive),
                AttackerOutcome = GetOutcome(TacticalBattleSide.Attacker),
                DefenderOutcome = GetOutcome(TacticalBattleSide.Defender),
                Tick = Encounter.Tick,
                AttackingUnits = CaptureUnits(TacticalBattleSide.Attacker),
                DefendingUnits = CaptureUnits(TacticalBattleSide.Defender),
            };

            foreach (TacticalUnitState unit in units)
            {
                if (unit.Unit is CapitalShip ship && unit.Hull < unit.InitialHull)
                {
                    result.ShipDamage.Add(
                        new ShipDamageResult
                        {
                            Ship = ship,
                            HullBefore = unit.InitialHull,
                            HullAfter = Math.Max(0, unit.Hull),
                        }
                    );
                }
                else if (unit.Unit is Starfighter fighters && unit.Hull < unit.InitialHull)
                {
                    result.FighterLosses.Add(
                        new FighterLossResult
                        {
                            Fighter = fighters,
                            SquadsBefore = unit.InitialHull,
                            SquadsAfter = Math.Max(0, unit.Hull),
                        }
                    );
                }
            }

            RecordSnapshotOutcomes(result.AttackingUnits, TacticalBattleSide.Attacker);
            RecordSnapshotOutcomes(result.DefendingUnits, TacticalBattleSide.Defender);
            return result;
        }

        /// <summary>
        /// Gets the capital-ship task forces assigned to one side's eight HUD slots.
        /// </summary>
        /// <param name="side">The side whose task forces to retrieve.</param>
        /// <returns>The task forces in HUD order.</returns>
        public IReadOnlyList<TacticalShipGroup> GetTaskForces(TacticalBattleSide side)
        {
            ValidateSide(side);
            return taskForces[side];
        }

        /// <summary>
        /// Gets the fighter-type groups assigned to one side's four HUD slots.
        /// </summary>
        /// <param name="side">The side whose fighter groups to retrieve.</param>
        /// <returns>The fighter groups in HUD order.</returns>
        public IReadOnlyList<TacticalShipGroup> GetFighterGroups(TacticalBattleSide side)
        {
            ValidateSide(side);
            return fighterGroups[side];
        }

        /// <summary>
        /// Configures the played side for manual commands and the opposing side for automated commands.
        /// A retained session preserves any command-mode changes made after this initial configuration.
        /// </summary>
        /// <param name="playerSide">The side controlled by the local player.</param>
        public void ConfigurePlayerControl(TacticalBattleSide playerSide)
        {
            commandAutomation.ConfigurePlayerControl(playerSide);
        }

        /// <summary>
        /// Gets whether one side periodically receives computer-generated tactical orders.
        /// </summary>
        /// <param name="side">The tactical side to inspect.</param>
        /// <returns>True when the side is under automated command.</returns>
        public bool IsAutomated(TacticalBattleSide side)
        {
            return commandAutomation.IsAutomated(side);
        }

        /// <summary>
        /// Enables or disables periodic computer-generated orders for one tactical side.
        /// Existing orders remain active when manual command is restored.
        /// </summary>
        /// <param name="side">The tactical side whose control mode changes.</param>
        /// <param name="automated">Whether the side should receive automated orders.</param>
        public void SetAutomated(TacticalBattleSide side, bool automated)
        {
            commandAutomation.SetAutomated(side, automated);
        }

        /// <summary>
        /// Gets one participating Death Star's current superlaser charge percentage.
        /// </summary>
        /// <param name="deathStar">The Death Star whose charge is requested.</param>
        /// <returns>The current charge from zero through one hundred.</returns>
        public float GetSuperlaserCharge(TacticalUnitState deathStar)
        {
            return simulator.GetSuperlaserCharge(deathStar);
        }

        /// <summary>
        /// Fires a charged Death Star at one active opposing tactical object.
        /// </summary>
        /// <param name="deathStar">The firing Death Star.</param>
        /// <param name="target">The selected opposing tactical object.</param>
        /// <returns>True when the shot fires.</returns>
        public bool TryFireSuperlaser(TacticalUnitState deathStar, TacticalUnitState target)
        {
            return simulator.TryFireSuperlaser(deathStar, target);
        }

        /// <summary>
        /// Orders every command group on one side to leave the tactical battlefield when no
        /// opposing gravity well holds it in combat. Units with disabled drives remain until they
        /// can move or are destroyed.
        /// </summary>
        /// <param name="side">The side withdrawing from combat.</param>
        /// <returns>True when withdrawal orders are assigned.</returns>
        public bool OrderWithdrawal(TacticalBattleSide side)
        {
            ValidateSide(side);
            if (IsWithdrawalBlocked(side))
                return false;

            foreach (TacticalShipGroup group in groups.Where(group => group.Side == side))
                group.SetBehavior(TacticalBehavior.Withdraw);
            return true;
        }

        /// <summary>
        /// Tests whether an active opposing capital ship projects a tactical gravity well.
        /// </summary>
        /// <param name="side">The side attempting to withdraw.</param>
        /// <returns>True while an opposing gravity-well ship remains active.</returns>
        public bool IsWithdrawalBlocked(TacticalBattleSide side)
        {
            ValidateSide(side);
            return units.Any(unit =>
                unit.IsActive
                && unit.Side != side
                && unit.Unit is CapitalShip { HasGravityWell: true }
            );
        }

        /// <summary>
        /// Acquires one pause hold on the tactical simulation.
        /// </summary>
        public void Pause()
        {
            pauseCount++;
        }

        /// <summary>
        /// Releases one pause hold without allowing the count to become negative.
        /// </summary>
        public void Resume()
        {
            pauseCount = Math.Max(0, pauseCount - 1);
        }

        /// <summary>
        /// Advances the existing battle deterministically to its final result without presentation delays.
        /// </summary>
        public void ResolveImmediately()
        {
            pauseCount = 0;
            int remainingSteps = _maximumImmediateResultSteps;
            while (!IsComplete && remainingSteps-- > 0)
                Advance(_immediateResultStep);

            if (!IsComplete)
            {
                throw new InvalidOperationException(
                    "Tactical combat did not reach an immediate result."
                );
            }
        }

        /// <summary>
        /// Removes and returns tactical presentation events produced since the previous drain.
        /// </summary>
        /// <returns>The events in simulation order.</returns>
        public IReadOnlyList<TacticalCombatEvent> DrainEvents()
        {
            return simulator.DrainEvents();
        }

        /// <summary>
        /// Advances every active tactical unit when the simulation is running.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            if (elapsedTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));
            if (IsPaused || IsComplete)
                return;

            if (Phase == TacticalBattlePhase.Arrival)
            {
                arrivalElapsedTime = Math.Min(_arrivalDuration, arrivalElapsedTime + elapsedTime);
                if (arrivalElapsedTime >= _arrivalDuration)
                    Phase = TacticalBattlePhase.Engagement;

                return;
            }

            foreach (TacticalUnitState unit in units)
                unit.Advance(elapsedTime, random);

            foreach (TacticalShipGroup group in groups)
                group.RemoveInactiveTargets();

            commandAutomation.Advance(elapsedTime);
            simulator.Advance(elapsedTime);
            FireAutomatedSuperlasers();
        }

        /// <summary>
        /// Fires each automated side's charged Death Star at its first ranked active target.
        /// </summary>
        private void FireAutomatedSuperlasers()
        {
            foreach (
                TacticalUnitState deathStar in units.Where(unit =>
                    unit.IsActive
                    && unit.Unit is CapitalShip { IsDeathStar: true }
                    && commandAutomation.IsAutomated(unit.Side)
                )
            )
            {
                TacticalShipGroup group = groups.Last(candidate =>
                    candidate.Units.Contains(deathStar)
                );
                TacticalUnitState target = group.Targets.FirstOrDefault(candidate =>
                    candidate.IsActive && candidate.Side != deathStar.Side
                );
                target ??= units.FirstOrDefault(candidate =>
                    candidate.IsActive && candidate.Side != deathStar.Side
                );
                if (target != null)
                    simulator.TryFireSuperlaser(deathStar, target);
            }
        }

        /// <summary>
        /// Gets a unit's presentation position during its initial flight into battle.
        /// </summary>
        /// <param name="unit">The tactical unit being presented.</param>
        /// <returns>The current presentation position.</returns>
        public System.Numerics.Vector3 GetPresentationPosition(TacticalUnitState unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            int index = units.IndexOf(unit);
            if (index < 0)
                throw new ArgumentException(
                    "The unit does not belong to this battle.",
                    nameof(unit)
                );
            if (Phase != TacticalBattlePhase.Arrival)
                return unit.Position;

            float progress = arrivalElapsedTime / _arrivalDuration;
            float remaining = 1f - progress * progress;
            float distance = ArrivalDistances[index % ArrivalDistances.Length] * remaining;
            return unit.Position - unit.Forward * distance;
        }

        private static CombatSide DetermineWinner(bool attackerActive, bool defenderActive)
        {
            if (attackerActive == defenderActive)
                return CombatSide.Draw;

            return attackerActive ? CombatSide.Attacker : CombatSide.Defender;
        }

        /// <summary>
        /// Builds each side's normalized fighter-command contribution from its assigned commander.
        /// </summary>
        /// <param name="encounter">The strategic encounter supplying both fleets.</param>
        /// <returns>The fighter-command contribution indexed by tactical side.</returns>
        private static IReadOnlyDictionary<TacticalBattleSide, float> BuildFighterCommandBudgets(
            PendingCombatResult encounter
        )
        {
            return new Dictionary<TacticalBattleSide, float>
            {
                [TacticalBattleSide.Attacker] = GetFighterCommandBudget(encounter.AttackerFleet),
                [TacticalBattleSide.Defender] = GetFighterCommandBudget(encounter.DefenderFleet),
            };
        }

        /// <summary>
        /// Normalizes the assigned commander's Combat rating into the tactical fighter budget.
        /// </summary>
        /// <param name="fleet">The fleet whose commander supports its fighters.</param>
        /// <returns>A value from one through nine, with one used when no commander is assigned.</returns>
        private static float GetFighterCommandBudget(Fleet fleet)
        {
            Officer commander = fleet
                ?.GetOfficers()
                .FirstOrDefault(officer => officer.CurrentRank == OfficerRank.Commander);
            return Math.Clamp(
                (commander?.GetEffectiveRating(OfficerRating.Combat) ?? 0) / 20f + 1f,
                1f,
                9f
            );
        }

        /// <summary>
        /// Builds the fixed capital and fighter command slots used by the tactical HUD.
        /// </summary>
        private void BuildCommandGroups()
        {
            foreach (TacticalBattleSide side in Enum.GetValues(typeof(TacticalBattleSide)))
            {
                TacticalUnitState[] sideUnits = units.Where(unit => unit.Side == side).ToArray();
                IReadOnlyList<TacticalShipGroup> sideTaskForces = BuildTaskForces(
                    side,
                    sideUnits.Where(unit => unit.Kind == TacticalUnitKind.CapitalShip).ToArray()
                );
                IReadOnlyList<TacticalShipGroup> sideFighterGroups = BuildFighterGroups(
                    side,
                    sideUnits.Where(unit => unit.Kind == TacticalUnitKind.Fighters).ToArray()
                );

                taskForces.Add(side, sideTaskForces);
                fighterGroups.Add(side, sideFighterGroups);
                groups.AddRange(sideTaskForces);
                groups.AddRange(sideFighterGroups);
            }
        }

        /// <summary>
        /// Partitions capital ships into the source game's square-root task-force layout.
        /// </summary>
        /// <param name="side">The side that owns the capital ships.</param>
        /// <param name="capitalShips">The capital ships in battle order.</param>
        /// <returns>Up to eight task forces in HUD order.</returns>
        private IReadOnlyList<TacticalShipGroup> BuildTaskForces(
            TacticalBattleSide side,
            IReadOnlyList<TacticalUnitState> capitalShips
        )
        {
            if (capitalShips.Count == 0)
                return Array.Empty<TacticalShipGroup>();

            int groupCount = Math.Min(
                8,
                Math.Max(1, (int)Math.Floor(Math.Sqrt(capitalShips.Count)))
            );
            int unitsPerGroup = capitalShips.Count / groupCount;
            List<TacticalShipGroup> result = new List<TacticalShipGroup>(groupCount);
            for (int index = 0; index < capitalShips.Count; index++)
            {
                if (index % unitsPerGroup == 0 && result.Count < groupCount)
                    result.Add(
                        new TacticalShipGroup(side, units, Array.Empty<TacticalUnitState>())
                    );

                result[result.Count - 1].AddUnit(capitalShips[index]);
            }

            return result.AsReadOnly();
        }

        /// <summary>
        /// Groups fighter squadrons by fighter type for the four fixed fighter controls.
        /// </summary>
        /// <param name="side">The side that owns the fighter squadrons.</param>
        /// <param name="fighters">The fighter squadrons in battle order.</param>
        /// <returns>Up to four fighter-type groups in HUD order.</returns>
        private IReadOnlyList<TacticalShipGroup> BuildFighterGroups(
            TacticalBattleSide side,
            IEnumerable<TacticalUnitState> fighters
        )
        {
            List<IGrouping<string, TacticalUnitState>> typeGroups = fighters
                .GroupBy(unit => unit.Unit.TypeID ?? string.Empty)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();
            if (typeGroups.Count > 4)
                throw new InvalidOperationException(
                    "A tactical side cannot field more than four fighter types."
                );

            return typeGroups
                .ConvertAll(group => new TacticalShipGroup(side, units, group))
                .AsReadOnly();
        }

        /// <summary>
        /// Rejects an undefined tactical side before indexing fixed command slots.
        /// </summary>
        /// <param name="side">The side to validate.</param>
        private static void ValidateSide(TacticalBattleSide side)
        {
            if (!Enum.IsDefined(typeof(TacticalBattleSide), side))
                throw new ArgumentOutOfRangeException(nameof(side));
        }

        private SpaceCombatSideOutcome GetOutcome(TacticalBattleSide side)
        {
            TacticalUnitState[] sideUnits = units.Where(unit => unit.Side == side).ToArray();
            if (sideUnits.Any(unit => unit.IsActive))
                return SpaceCombatSideOutcome.Active;
            if (sideUnits.Any(unit => unit.Hull > 0))
                return SpaceCombatSideOutcome.Withdrawn;

            return SpaceCombatSideOutcome.Destroyed;
        }

        private bool HasActiveUnits(TacticalBattleSide side)
        {
            return units.Any(unit => unit.Side == side && unit.IsActive);
        }

        private List<CombatUnitSnapshot> CaptureUnits(TacticalBattleSide side)
        {
            Fleet fleet =
                side == TacticalBattleSide.Attacker
                    ? Encounter.AttackerFleet
                    : Encounter.DefenderFleet;
            List<CombatUnitSnapshot> snapshots = CombatUnitSnapshot.CaptureFleetUnits(
                new[] { fleet }
            );
            IEnumerable<Starfighter> planetaryFighters = units
                .Where(unit => unit.Side == side && unit.Unit is Starfighter)
                .Select(unit => (Starfighter)unit.Unit)
                .Where(fighter => fighter.GetParentOfType<Planet>() == Encounter.Planet);
            snapshots.AddRange(
                planetaryFighters
                    .Where(fighter =>
                        snapshots.All(snapshot =>
                            snapshot.Unit.GetInstanceID() != fighter.GetInstanceID()
                        )
                    )
                    .Select(fighter => new CombatUnitSnapshot(fighter))
            );
            return snapshots;
        }

        private void RecordSnapshotOutcomes(
            IEnumerable<CombatUnitSnapshot> snapshots,
            TacticalBattleSide side
        )
        {
            TacticalUnitState[] sideUnits = units.Where(unit => unit.Side == side).ToArray();
            CombatUnitSnapshot.RecordOutcomes(
                snapshots,
                sideUnits
                    .Where(unit => unit.Hull < unit.InitialHull)
                    .Select(unit => unit.Unit)
                    .OfType<ISceneNode>(),
                sideUnits
                    .Where(unit => unit.Hull <= 0)
                    .Select(unit => unit.Unit)
                    .OfType<ISceneNode>()
            );
        }

        /// <summary>
        /// Adds the operational ships and embarked fighter squadrons from one fleet.
        /// </summary>
        /// <param name="units">The tactical unit collection.</param>
        /// <param name="fleet">The fleet to enumerate.</param>
        /// <param name="side">The fleet's tactical side.</param>
        private static void AddFleet(
            ICollection<TacticalUnitState> units,
            Fleet fleet,
            TacticalBattleSide side
        )
        {
            foreach (CapitalShip ship in fleet.CapitalShips)
            {
                if (
                    ship == null
                    || ship.ManufacturingStatus != ManufacturingStatus.Complete
                    || ship.Movement != null
                    || ship.CurrentHullStrength <= 0
                )
                    continue;

                TacticalUnitState capitalUnit = TacticalUnitState.FromCapitalShip(ship, side);
                units.Add(capitalUnit);
                foreach (Starfighter fighters in ship.Starfighters)
                {
                    if (
                        fighters == null
                        || fighters.ManufacturingStatus != ManufacturingStatus.Complete
                        || fighters.Movement != null
                        || fighters.CurrentSquadronSize <= 0
                    )
                        continue;

                    units.Add(TacticalUnitState.FromFighters(fighters, side, capitalUnit));
                }
            }
        }

        private static void AddPlanetStarfighters(
            ICollection<TacticalUnitState> units,
            Planet planet,
            string ownerInstanceId,
            TacticalBattleSide side
        )
        {
            if (planet == null || string.IsNullOrEmpty(ownerInstanceId))
                return;

            HashSet<IGameEntity> existingUnits = units.Select(unit => unit.Unit).ToHashSet();
            foreach (Starfighter fighters in planet.Starfighters)
            {
                if (
                    fighters == null
                    || existingUnits.Contains(fighters)
                    || fighters.GetOwnerInstanceID() != ownerInstanceId
                    || fighters.ManufacturingStatus != ManufacturingStatus.Complete
                    || fighters.Movement != null
                    || fighters.CurrentSquadronSize <= 0
                )
                    continue;

                units.Add(TacticalUnitState.FromFighters(fighters, side));
            }
        }
    }
}
