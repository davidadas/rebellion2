using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Owns the isolated tactical state for one pending strategic encounter.
    /// </summary>
    public sealed class TacticalBattleSession
    {
        private readonly List<TacticalShipGroup> groups = new List<TacticalShipGroup>();
        private readonly ReadOnlyCollection<TacticalShipGroup> groupView;
        private readonly ReadOnlyCollection<TacticalUnitState> units;

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

        private TacticalBattleSession(PendingCombatResult encounter, IList<TacticalUnitState> units)
        {
            Encounter = encounter;
            groupView = groups.AsReadOnly();
            this.units = new ReadOnlyCollection<TacticalUnitState>(units);
        }

        /// <summary>
        /// Creates an isolated tactical session from a pending fleet encounter.
        /// </summary>
        /// <param name="encounter">The pending strategic encounter.</param>
        /// <returns>The initialized tactical session.</returns>
        public static TacticalBattleSession Create(PendingCombatResult encounter)
        {
            if (encounter == null)
                throw new ArgumentNullException(nameof(encounter));
            if (encounter.AttackerFleet == null)
                throw new ArgumentException("Attacking fleet is required.", nameof(encounter));
            if (encounter.DefenderFleet == null)
                throw new ArgumentException("Defending fleet is required.", nameof(encounter));

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
            return new TacticalBattleSession(encounter, units);
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
                AttackerOutcome = GetOutcome(attackerActive),
                DefenderOutcome = GetOutcome(defenderActive),
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
        /// Creates a command group from units on one side of this battle.
        /// </summary>
        /// <param name="selectedUnits">The units to assign to the group.</param>
        /// <returns>The initialized tactical ship group.</returns>
        public TacticalShipGroup CreateGroup(IEnumerable<TacticalUnitState> selectedUnits)
        {
            if (selectedUnits == null)
                throw new ArgumentNullException(nameof(selectedUnits));

            List<TacticalUnitState> groupUnits = selectedUnits.Distinct().ToList();
            if (groupUnits.Count == 0)
                throw new ArgumentException(
                    "A ship group requires at least one unit.",
                    nameof(selectedUnits)
                );
            if (groupUnits.Any(unit => unit == null || !units.Contains(unit)))
                throw new ArgumentException(
                    "Every ship group unit must belong to this battle.",
                    nameof(selectedUnits)
                );

            TacticalBattleSide side = groupUnits[0].Side;
            if (groupUnits.Any(unit => unit.Side != side))
                throw new ArgumentException(
                    "Every ship group unit must belong to the same side.",
                    nameof(selectedUnits)
                );

            TacticalShipGroup group = new TacticalShipGroup(side, units, groupUnits);
            groups.Add(group);
            return group;
        }

        /// <summary>
        /// Deletes a tactical command group from this battle.
        /// </summary>
        /// <param name="group">The group to delete.</param>
        /// <returns>True when the group belonged to this battle.</returns>
        public bool DeleteGroup(TacticalShipGroup group)
        {
            return groups.Remove(group);
        }

        private static CombatSide DetermineWinner(bool attackerActive, bool defenderActive)
        {
            if (attackerActive == defenderActive)
                return CombatSide.Draw;

            return attackerActive ? CombatSide.Attacker : CombatSide.Defender;
        }

        private static SpaceCombatSideOutcome GetOutcome(bool isActive)
        {
            return isActive ? SpaceCombatSideOutcome.Active : SpaceCombatSideOutcome.Destroyed;
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
                    .Where(unit => !unit.IsActive)
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

                units.Add(TacticalUnitState.FromCapitalShip(ship, side));
                foreach (Starfighter fighters in ship.Starfighters)
                {
                    if (
                        fighters == null
                        || fighters.ManufacturingStatus != ManufacturingStatus.Complete
                        || fighters.Movement != null
                        || fighters.CurrentSquadronSize <= 0
                    )
                        continue;

                    units.Add(TacticalUnitState.FromFighters(fighters, side));
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
