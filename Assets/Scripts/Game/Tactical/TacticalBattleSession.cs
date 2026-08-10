using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Owns the isolated tactical state for one pending strategic encounter.
    /// </summary>
    public sealed class TacticalBattleSession
    {
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
            BuildCommandGroups();
            simulator = new TacticalBattleSimulator(this.units, groupView, random);
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
        /// Advances every active tactical unit when the simulation is running.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            if (elapsedTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));
            if (IsPaused || IsComplete)
                return;

            foreach (TacticalUnitState unit in units)
                unit.Advance(elapsedTime, random);

            foreach (TacticalShipGroup group in groups)
                group.RemoveInactiveTargets();

            simulator.Advance(elapsedTime);
        }

        private static CombatSide DetermineWinner(bool attackerActive, bool defenderActive)
        {
            if (attackerActive == defenderActive)
                return CombatSide.Draw;

            return attackerActive ? CombatSide.Attacker : CombatSide.Defender;
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
