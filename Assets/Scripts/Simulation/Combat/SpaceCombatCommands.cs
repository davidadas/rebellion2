using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Identifies a fleet encounter waiting for space-combat resolution.
    /// </summary>
    internal sealed class SpaceCombatDecision
    {
        public List<string> AttackerFleetInstanceIDs { get; set; } = new List<string>();
        public List<string> DefenderFleetInstanceIDs { get; set; } = new List<string>();
        public string AttackerOwnerInstanceID { get; set; }
        public string DefenderOwnerInstanceID { get; set; }
        public string PlanetInstanceID { get; set; }
    }

    /// <summary>
    /// Detects and resolves hostile fleet encounters.
    /// </summary>
    public class SpaceCombatCommands
    {
        private readonly GameRoot _game;
        private readonly SpaceCombatQueries _queries;
        private readonly MovementCommands _movement;
        private readonly SpaceCombatAutoResolver _autoResolver;
        private SpaceCombatDecision _pendingDecision;

        /// <summary>
        /// Whether a player-involved combat encounter is waiting for resolution.
        /// </summary>
        public bool HasPendingDecision => _pendingDecision != null;

        /// <summary>
        /// Gets the presentation snapshot for the combat encounter awaiting player input.
        /// </summary>
        /// <param name="result">Receives the pending encounter snapshot.</param>
        /// <returns>True when an encounter is waiting for player input.</returns>
        public bool TryGetPendingCombat(out PendingCombatResult result)
        {
            result =
                _pendingDecision == null
                    ? null
                    : _queries.BuildPendingCombatResult(_pendingDecision);
            return result != null;
        }

        /// <summary>
        /// Creates space-combat commands.
        /// </summary>
        /// <param name="game">Active game state.</param>
        /// <param name="movement">Movement operations used for retreats and evacuation.</param>
        /// <param name="queries">Encounter and retreat eligibility queries.</param>
        public SpaceCombatCommands(
            GameRoot game,
            MovementCommands movement,
            SpaceCombatQueries queries
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _autoResolver = new SpaceCombatAutoResolver(
                game.Config.Combat.SpaceCombat,
                game.Random
            );
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Resolves all AI-vs-AI combat encounters this tick in a single pass.
        /// When a player-involved encounter is found, emits a PendingCombatResult and stops.
        /// </summary>
        /// <returns>Combat results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            if (_pendingDecision != null)
                return results;

            HashSet<string> resolvedFleetIds = new HashSet<string>();

            while (TryBeginFleetCombat(resolvedFleetIds, out SpaceCombatDecision decision))
            {
                if (TryAutoResolveAICombat(decision, resolvedFleetIds, results))
                    continue;

                _pendingDecision = decision;
                results.Add(_queries.BuildPendingCombatResult(decision));
                return results;
            }

            return results;
        }

        /// <summary>
        /// Attempts to auto-resolve a detected combat encounter when both sides are AI-controlled.
        /// </summary>
        /// <param name="decision">The detected encounter to resolve.</param>
        /// <param name="resolvedFleetIds">Set updated with both fleet IDs on successful resolution.</param>
        /// <param name="results">Output list that receives combat results.</param>
        /// <returns>True if auto-resolved; false if either side is player-controlled.</returns>
        private bool TryAutoResolveAICombat(
            SpaceCombatDecision decision,
            HashSet<string> resolvedFleetIds,
            List<GameResult> results
        )
        {
            if (!_queries.BothSidesAIControlled(decision))
                return false;

            results.AddRange(ResolveAutomaticFleetEncounter(decision));

            if (IsEncounterStillContested(decision))
            {
                resolvedFleetIds.UnionWith(decision.AttackerFleetInstanceIDs);
                resolvedFleetIds.UnionWith(decision.DefenderFleetInstanceIDs);
            }

            return true;
        }

        /// <summary>
        /// Checks whether both sides in the encounter still occupy a contested planet.
        /// </summary>
        /// <param name="decision">The combat decision to evaluate.</param>
        /// <returns>True when both sides still contest the same planet.</returns>
        private bool IsEncounterStillContested(SpaceCombatDecision decision)
        {
            return _queries.AreForcesContestingPlanet(decision);
        }

        /// <summary>
        /// Resolves and clears the player-involved encounter waiting for a decision.
        /// </summary>
        /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        public List<GameResult> ResolvePending(bool autoResolve)
        {
            if (_pendingDecision == null)
                throw new InvalidOperationException("No pending combat to resolve.");

            SpaceCombatDecision decision = _pendingDecision;
            List<GameResult> results = Resolve(decision, autoResolve);
            _pendingDecision = null;
            return results;
        }

        /// <summary>
        /// Resolves a withdrawal by the requested side of the pending encounter.
        /// </summary>
        /// <param name="retreatingFactionInstanceId">The withdrawing faction identifier.</param>
        /// <returns>The combat results, or null when that side cannot withdraw.</returns>
        public List<GameResult> ResolvePendingRetreat(string retreatingFactionInstanceId)
        {
            if (_pendingDecision == null)
                throw new InvalidOperationException("No pending combat to resolve.");

            if (
                retreatingFactionInstanceId != _pendingDecision.AttackerOwnerInstanceID
                    && retreatingFactionInstanceId != _pendingDecision.DefenderOwnerInstanceID
                || !TryResolveRetreat(
                    _pendingDecision,
                    retreatingFactionInstanceId,
                    out List<GameResult> results
                )
            )
                return null;

            _pendingDecision = null;
            return results;
        }

        /// <summary>
        /// Attempts to resolve a pending combat decision by withdrawing one side.
        /// </summary>
        /// <param name="decision">The pending combat decision.</param>
        /// <param name="retreatingFactionInstanceId">The faction requested to withdraw.</param>
        /// <param name="results">Receives the generated combat result.</param>
        /// <returns>True when the side withdrew successfully.</returns>
        private bool TryResolveRetreat(
            SpaceCombatDecision decision,
            string retreatingFactionInstanceId,
            out List<GameResult> results
        )
        {
            Planet planet = _queries.ResolveCombatPlanet(decision);
            results = new List<GameResult>();

            List<Fleet> attackerFleets = _queries.GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = _queries.GetFleets(decision.DefenderFleetInstanceIDs);
            bool attackerRetreated =
                retreatingFactionInstanceId == decision.AttackerOwnerInstanceID;
            List<Fleet> retreatingFleets = attackerRetreated ? attackerFleets : defenderFleets;
            List<Fleet> opposingFleets = attackerRetreated ? defenderFleets : attackerFleets;
            List<Starfighter> retreatingFighters = SpaceCombatQueries
                .GetActivePlanetStarfighters(planet, retreatingFactionInstanceId)
                .ToList();

            if (
                !_queries.CanRetreatForces(
                    retreatingFleets,
                    opposingFleets,
                    planet,
                    retreatingFactionInstanceId
                )
            )
                return false;
            if (
                retreatingFleets.Count > 0
                && !TryRetreatFleets(retreatingFleets, opposingFleets, ignoreGravityWell: false)
            )
                return false;

            SpaceCombatResult result = BuildRetreatResult(
                decision,
                attackerRetreated,
                attackerFleets,
                defenderFleets,
                planet
            );
            foreach (Starfighter fighter in retreatingFighters)
                _movement.EvacuateToNearestFriendlyPlanet(fighter);
            string retreatPlanetInstanceId = GetRetreatPlanetInstanceID(
                retreatingFleets,
                retreatingFighters,
                planet,
                SpaceCombatSideOutcome.Withdrawn
            );
            if (attackerRetreated)
                result.AttackerRetreatPlanetInstanceID = retreatPlanetInstanceId;
            else
                result.DefenderRetreatPlanetInstanceID = retreatPlanetInstanceId;

            results.Add(result);
            ClearCombatFlags(decision);
            return true;
        }

        /// <summary>
        /// Builds the combat result emitted after a successful force withdrawal.
        /// </summary>
        /// <param name="decision">The resolved combat decision.</param>
        /// <param name="attackerRetreated">Whether the attacking side withdrew.</param>
        /// <param name="attackerFleets">The attacking fleets.</param>
        /// <param name="defenderFleets">The defending fleets.</param>
        /// <param name="planet">The combat location.</param>
        /// <returns>The withdrawal combat result.</returns>
        private SpaceCombatResult BuildRetreatResult(
            SpaceCombatDecision decision,
            bool attackerRetreated,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            Planet planet
        )
        {
            Fleet attacker = SpaceCombatQueries.GetRepresentativeFleet(attackerFleets);
            Fleet defender = SpaceCombatQueries.GetRepresentativeFleet(defenderFleets);
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID,
                Planet = planet,
                PlanetOwnerInstanceID = planet.OwnerInstanceID,
                Winner = attackerRetreated ? CombatSide.Defender : CombatSide.Attacker,
                AttackerOutcome = attackerRetreated
                    ? SpaceCombatSideOutcome.Withdrawn
                    : SpaceCombatSideOutcome.Active,
                DefenderOutcome = attackerRetreated
                    ? SpaceCombatSideOutcome.Active
                    : SpaceCombatSideOutcome.Withdrawn,
                Tick = _game.CurrentTick,
            };

            result.AttackingUnits.AddRange(
                CaptureCombatUnits(attackerFleets, planet, decision.AttackerOwnerInstanceID)
            );
            result.DefendingUnits.AddRange(
                CaptureCombatUnits(defenderFleets, planet, decision.DefenderOwnerInstanceID)
            );

            return result;
        }

        /// <summary>
        /// Detects a hostile fleet encounter while skipping fleets already handled this tick.
        /// </summary>
        /// <param name="excludedFleetIds">Fleet instance IDs to skip.</param>
        /// <param name="decision">The detected encounter.</param>
        /// <returns>True if a hostile encounter was detected.</returns>
        private bool TryBeginFleetCombat(
            HashSet<string> excludedFleetIds,
            out SpaceCombatDecision decision
        )
        {
            decision = null;

            if (
                !_queries.TryFindContestedForces(
                    excludedFleetIds,
                    out Planet planet,
                    out string attackerOwnerInstanceId,
                    out string defenderOwnerInstanceId,
                    out List<Fleet> attackerFleets,
                    out List<Fleet> defenderFleets
                )
            )
                return false;

            foreach (Fleet fleet in attackerFleets.Concat(defenderFleets))
                fleet.SetCombatState(true);

            decision = new SpaceCombatDecision
            {
                AttackerFleetInstanceIDs = attackerFleets.ConvertAll(fleet =>
                    fleet.GetInstanceID()
                ),
                DefenderFleetInstanceIDs = defenderFleets.ConvertAll(fleet =>
                    fleet.GetInstanceID()
                ),
                AttackerOwnerInstanceID = attackerOwnerInstanceId,
                DefenderOwnerInstanceID = defenderOwnerInstanceId,
                PlanetInstanceID = planet.GetInstanceID(),
            };

            return true;
        }

        /// <summary>
        /// Resolves a pending combat encounter. Applies damage to the game world and clears
        /// IsInCombat on every participating fleet regardless of outcome.
        /// </summary>
        /// <param name="decision">The combat decision to resolve.</param>
        /// <param name="autoResolve">True to use auto-resolve; false to use manual combat.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        internal List<GameResult> Resolve(SpaceCombatDecision decision, bool autoResolve)
        {
            if (autoResolve)
                return ResolveFleetEncounter(decision);

            RunManualCombat();
            ClearCombatFlags(decision);
            return new List<GameResult>();
        }

        /// <summary>
        /// Resolves an AI-controlled fleet encounter.
        /// </summary>
        /// <param name="decision">Encounter context to resolve.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        private List<GameResult> ResolveAutomaticFleetEncounter(SpaceCombatDecision decision)
        {
            return ResolveFleetEncounter(decision);
        }

        /// <summary>
        /// Resolves an entire fleet encounter through the shared automatic tactical resolver.
        /// </summary>
        /// <param name="decision">Encounter context to resolve.</param>
        /// <returns>The result for the encounter.</returns>
        private List<GameResult> ResolveFleetEncounter(SpaceCombatDecision decision)
        {
            List<GameResult> results = new List<GameResult>();
            List<Fleet> attackerFleets = _queries.GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = _queries.GetFleets(decision.DefenderFleetInstanceIDs);
            SpaceCombatResult combatResult = null;

            try
            {
                if (_queries.AreForcesContestingPlanet(decision))
                    combatResult = ResolveCombat(decision, attackerFleets, defenderFleets);
            }
            finally
            {
                ClearCombatFlags(decision);
            }

            UpdateCombatEncounterResultOutcomes(combatResult, decision);

            if (combatResult != null)
                results.Add(combatResult);

            return results;
        }

        /// <summary>
        /// Updates encounter outcomes from each fleet's final runtime state.
        /// </summary>
        /// <param name="result">The encounter result to update.</param>
        /// <param name="decision">The combat decision identifying both sides.</param>
        private void UpdateCombatEncounterResultOutcomes(
            SpaceCombatResult result,
            SpaceCombatDecision decision
        )
        {
            if (result == null)
                return;

            List<Fleet> attackerFleets = _queries.GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = _queries.GetFleets(decision.DefenderFleetInstanceIDs);
            List<Starfighter> attackerFighters = GetLiveFighters(result.AttackingUnits);
            List<Starfighter> defenderFighters = GetLiveFighters(result.DefendingUnits);
            result.AttackerOutcome = GetCombatSideOutcome(
                attackerFleets,
                attackerFighters,
                result.AttackerOwnerInstanceID,
                result.Planet,
                result.AttackerOutcome
            );
            result.DefenderOutcome = GetCombatSideOutcome(
                defenderFleets,
                defenderFighters,
                result.DefenderOwnerInstanceID,
                result.Planet,
                result.DefenderOutcome
            );
            result.AttackerRetreatPlanetInstanceID = GetRetreatPlanetInstanceID(
                attackerFleets,
                attackerFighters,
                result.Planet,
                result.AttackerOutcome
            );
            result.DefenderRetreatPlanetInstanceID = GetRetreatPlanetInstanceID(
                defenderFleets,
                defenderFighters,
                result.Planet,
                result.DefenderOutcome
            );
            UpdateCombatEncounterWinner(result);
        }

        /// <summary>
        /// Resolves surviving fighter participants from detached combat snapshots.
        /// </summary>
        /// <param name="snapshots">The combat-side unit snapshots.</param>
        /// <returns>The participating fighters that remain in the live game.</returns>
        private List<Starfighter> GetLiveFighters(IEnumerable<CombatUnitSnapshot> snapshots)
        {
            return (snapshots ?? Enumerable.Empty<CombatUnitSnapshot>())
                .Select(snapshot => snapshot?.Unit?.GetInstanceID())
                .Where(instanceId => !string.IsNullOrEmpty(instanceId))
                .Select(instanceId => _game.GetSceneNodeByInstanceID<Starfighter>(instanceId))
                .Where(fighter => fighter != null)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Aligns the encounter winner with the sides that remain active after resolution.
        /// </summary>
        /// <param name="result">The encounter result to update.</param>
        private static void UpdateCombatEncounterWinner(SpaceCombatResult result)
        {
            bool attackerActive = result.AttackerOutcome == SpaceCombatSideOutcome.Active;
            bool defenderActive = result.DefenderOutcome == SpaceCombatSideOutcome.Active;
            if (attackerActive == defenderActive)
                return;

            result.Winner = attackerActive ? CombatSide.Attacker : CombatSide.Defender;
        }

        /// <summary>
        /// Returns the destination recorded for a withdrawn combat side.
        /// </summary>
        /// <param name="fleets">The side's surviving participating fleets.</param>
        /// <param name="fighters">The side's surviving participating fighter squadrons.</param>
        /// <param name="battlePlanet">The planet where combat occurred.</param>
        /// <param name="outcome">The side's final combat outcome.</param>
        /// <returns>The retreat planet identifier, or null when the side did not withdraw.</returns>
        private static string GetRetreatPlanetInstanceID(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Starfighter> fighters,
            Planet battlePlanet,
            SpaceCombatSideOutcome outcome
        )
        {
            if (outcome != SpaceCombatSideOutcome.Withdrawn)
                return null;
            Planet destination = (fleets ?? Array.Empty<Fleet>())
                .Select(fleet => fleet?.GetParentOfType<Planet>())
                .Concat(
                    (fighters ?? Array.Empty<Starfighter>()).Select(fighter =>
                        fighter?.GetParentOfType<Planet>()
                    )
                )
                .FirstOrDefault(planet => planet != null && planet != battlePlanet);
            return destination?.InstanceID;
        }

        /// <summary>
        /// Resolves a combat side's final encounter outcome.
        /// </summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <param name="fighters">The surviving participating fighter squadrons.</param>
        /// <param name="ownerInstanceId">The participating owner's identifier.</param>
        /// <param name="battlePlanet">The encounter location.</param>
        /// <param name="resolvedOutcome">The outcome recorded by the tactical resolver.</param>
        /// <returns>The final encounter outcome.</returns>
        private static SpaceCombatSideOutcome GetCombatSideOutcome(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Starfighter> fighters,
            string ownerInstanceId,
            Planet battlePlanet,
            SpaceCombatSideOutcome resolvedOutcome
        )
        {
            if (resolvedOutcome == SpaceCombatSideOutcome.Destroyed)
                return SpaceCombatSideOutcome.Destroyed;

            if (SpaceCombatQueries.HasActiveSpaceUnits(fleets, battlePlanet, ownerInstanceId))
                return SpaceCombatSideOutcome.Active;

            if (
                fleets?.Any(fleet => fleet?.Movement != null) == true
                || fighters?.Any(fighter => fighter?.Movement != null) == true
            )
                return SpaceCombatSideOutcome.Withdrawn;

            List<Planet> currentPlanets = (fleets ?? Array.Empty<Fleet>())
                .Select(fleet => fleet?.GetParentOfType<Planet>())
                .Concat(
                    (fighters ?? Array.Empty<Starfighter>()).Select(fighter =>
                        fighter?.GetParentOfType<Planet>()
                    )
                )
                .Where(planet => planet != null)
                .ToList();
            if (currentPlanets.Count == 0)
                return SpaceCombatSideOutcome.Destroyed;

            if (battlePlanet != null && currentPlanets.Any(planet => planet != battlePlanet))
                return SpaceCombatSideOutcome.Withdrawn;

            return SpaceCombatSideOutcome.Active;
        }

        /// <summary>
        /// Clears combat state from fleets that remain after an encounter.
        /// </summary>
        /// <param name="decision">Encounter identifying the affected fleets.</param>
        private void ClearCombatFlags(SpaceCombatDecision decision)
        {
            foreach (
                Fleet fleet in _queries
                    .GetFleets(decision.AttackerFleetInstanceIDs)
                    .Concat(_queries.GetFleets(decision.DefenderFleetInstanceIDs))
            )
            {
                fleet.SetCombatState(false);
            }
        }

        /// <summary>
        /// Attempts to evacuate a fleet to the nearest friendly planet.
        /// </summary>
        /// <param name="fleet">Fleet attempting to retreat.</param>
        /// <returns>True when the fleet leaves or begins movement away from the planet.</returns>
        private bool TryRetreatFleet(Fleet fleet)
        {
            if (fleet == null)
                return false;

            Planet originalPlanet = fleet.GetParentOfType<Planet>();
            _movement.EvacuateToNearestFriendlyPlanet(fleet);
            bool retreated =
                fleet.Movement != null || fleet.GetParentOfType<Planet>() != originalPlanet;
            if (
                retreated
                && fleet.Order?.OrderType == FleetOrderType.Attack
                && fleet.Order.TargetPlanetId == originalPlanet?.InstanceID
            )
                fleet.Order = null;

            return retreated;
        }

        /// <summary>
        /// Attempts to evacuate every fleet on one combat side.
        /// </summary>
        /// <param name="fleets">The fleets attempting to retreat.</param>
        /// <param name="opponents">The opposing fleets that may block retreat.</param>
        /// <param name="ignoreGravityWell">Whether gravity-well interdiction is ignored.</param>
        /// <returns>True when every fleet leaves or begins movement away from the planet.</returns>
        private bool TryRetreatFleets(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents,
            bool ignoreGravityWell
        )
        {
            if (fleets == null || fleets.Count == 0)
                return false;

            if (
                !ignoreGravityWell
                && SpaceCombatQueries.IsRetreatBlockedByGravityWell(fleets, opponents)
            )
                return false;
            if (fleets.Any(fleet => !SpaceCombatQueries.HasHyperdriveCapableShip(fleet)))
                return false;

            bool allRetreated = true;
            foreach (Fleet fleet in fleets)
                allRetreated &= TryRetreatFleet(fleet);

            return allRetreated;
        }

        /// <summary>
        /// Resolves one complete space-combat encounter and applies it to the game state.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attackerFleets">Attacking fleets.</param>
        /// <param name="defenderFleets">Defending fleets.</param>
        /// <returns>The applied combat result, or null when the encounter is no longer valid.</returns>
        private SpaceCombatResult ResolveCombat(
            SpaceCombatDecision decision,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets
        )
        {
            Planet planet = _queries.ResolveCombatPlanet(decision);
            if (planet == null)
            {
                GameLogger.Warning("ResolveCombat: the combat planet no longer exists.");
                return null;
            }

            SpaceCombatResult result = ResolveSpace(
                attackerFleets,
                defenderFleets,
                decision.AttackerOwnerInstanceID,
                decision.DefenderOwnerInstanceID,
                planet,
                _game.CurrentTick,
                out HashSet<ISceneNode> withdrawnUnits
            );
            result.Events = ApplyCombatResult(result, attackerFleets, defenderFleets);
            CompleteAutomaticWithdrawals(attackerFleets, defenderFleets, planet, withdrawnUnits);

            GameLogger.Log(
                $"Combat at {planet.GetDisplayName()}: "
                    + $"{decision.AttackerOwnerInstanceID} vs "
                    + $"{decision.DefenderOwnerInstanceID} - "
                    + $"Winner: {result.Winner}"
            );

            return result;
        }

        /// <summary>
        /// Moves forces that the automatic resolver withdrew away from the battle planet.
        /// </summary>
        /// <param name="attackerFleets">The attacking fleets.</param>
        /// <param name="defenderFleets">The defending fleets.</param>
        /// <param name="planet">The planet where combat occurred.</param>
        /// <param name="withdrawnUnits">The units that escaped during tactical resolution.</param>
        private void CompleteAutomaticWithdrawals(
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            Planet planet,
            ISet<ISceneNode> withdrawnUnits
        )
        {
            CompleteAutomaticWithdrawal(attackerFleets, planet, withdrawnUnits);
            CompleteAutomaticWithdrawal(defenderFleets, planet, withdrawnUnits);
        }

        /// <summary>
        /// Evacuates the surviving fleets and independently deployed fighters that can withdraw.
        /// </summary>
        /// <param name="fleets">The withdrawing fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="withdrawnUnits">The units that completed tactical withdrawal.</param>
        private void CompleteAutomaticWithdrawal(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            ISet<ISceneNode> withdrawnUnits
        )
        {
            foreach (
                Fleet fleet in (fleets ?? Array.Empty<Fleet>()).Where(fleet =>
                    fleet != null
                    && SpaceCombatQueries.GetActiveCapitalShips(fleet).Any(withdrawnUnits.Contains)
                )
            )
            {
                TryRetreatFleet(fleet);
            }

            foreach (
                Starfighter fighter in planet
                    .GetChildren<Starfighter>()
                    .Where(SpaceCombatQueries.IsActiveStarfighter)
                    .Where(withdrawnUnits.Contains)
                    .ToList()
            )
            {
                _movement.EvacuateToNearestFriendlyPlanet(fighter);
            }
        }

        /// <summary>
        /// Placeholder for interactive/manual combat resolution.
        /// </summary>
        private void RunManualCombat() { }

        /// <summary>
        /// Resolves one battle with the headless tactical auto-resolver.
        /// </summary>
        /// <param name="attackerFleets">The attacking fleets.</param>
        /// <param name="defenderFleets">The defending fleets.</param>
        /// <param name="attackerOwnerInstanceId">The attacking owner identifier.</param>
        /// <param name="defenderOwnerInstanceId">The defending owner identifier.</param>
        /// <param name="planet">Planet where combat occurs.</param>
        /// <param name="tick">Current game tick (recorded on the result).</param>
        /// <param name="withdrawnUnits">Receives the units that escaped tactical combat.</param>
        /// <returns>The combat result with winner, per-ship damage, and fighter losses.</returns>
        private SpaceCombatResult ResolveSpace(
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            string attackerOwnerInstanceId,
            string defenderOwnerInstanceId,
            Planet planet,
            int tick,
            out HashSet<ISceneNode> withdrawnUnits
        )
        {
            List<CapitalShip> attackerShips = attackerFleets
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<CapitalShip> defenderShips = defenderFleets
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<Starfighter> attackerPlanetaryFighters = SpaceCombatQueries
                .GetActivePlanetStarfighters(planet, attackerOwnerInstanceId)
                .ToList();
            List<Starfighter> defenderPlanetaryFighters = SpaceCombatQueries
                .GetActivePlanetStarfighters(planet, defenderOwnerInstanceId)
                .ToList();
            List<Starfighter> attackerFighters = attackerShips
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(attackerPlanetaryFighters)
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Distinct()
                .ToList();
            List<Starfighter> defenderFighters = defenderShips
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(defenderPlanetaryFighters)
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Distinct()
                .ToList();
            List<IReadOnlyCollection<ISceneNode>> attackerWithdrawalGroups =
                _queries.GetAutomaticWithdrawalGroups(
                    attackerFleets,
                    defenderFleets,
                    planet,
                    attackerOwnerInstanceId
                );
            List<IReadOnlyCollection<ISceneNode>> defenderWithdrawalGroups =
                _queries.GetAutomaticWithdrawalGroups(
                    defenderFleets,
                    attackerFleets,
                    planet,
                    defenderOwnerInstanceId
                );
            SpaceCombatResult result = _autoResolver.Resolve(
                attackerShips,
                attackerFighters,
                defenderShips,
                defenderFighters,
                attackerWithdrawalGroups,
                defenderWithdrawalGroups
            );
            result.AttackerFleet = SpaceCombatQueries.GetRepresentativeFleet(attackerFleets);
            result.DefenderFleet = SpaceCombatQueries.GetRepresentativeFleet(defenderFleets);
            result.AttackerOwnerInstanceID = attackerOwnerInstanceId;
            result.DefenderOwnerInstanceID = defenderOwnerInstanceId;
            result.Planet = planet;
            result.PlanetOwnerInstanceID = planet.OwnerInstanceID;
            result.Tick = tick;
            withdrawnUnits = result.WithdrawnUnits.ToHashSet();
            return result;
        }

        /// <summary>
        /// Captures the current units on one side of a combat encounter without resolving damage.
        /// </summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The owner of planetary starfighters to include.</param>
        /// <returns>The detached unit snapshots for the force.</returns>
        private static List<CombatUnitSnapshot> CaptureCombatUnits(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId
        )
        {
            List<CapitalShip> ships = (fleets ?? Array.Empty<Fleet>())
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<Starfighter> fighters = ships
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(SpaceCombatQueries.GetActivePlanetStarfighters(planet, ownerInstanceId))
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Distinct()
                .ToList();
            List<CombatUnitSnapshot> units = ships
                .SelectMany(ship =>
                    new[] { ship }
                        .Cast<ISceneNode>()
                        .Concat(ship.GetChildren<ISceneNode>(recursive: true))
                )
                .Concat(fighters)
                .Where(unit => unit != null)
                .Distinct()
                .Select(unit => new CombatUnitSnapshot(unit))
                .ToList();
            IEnumerable<ISceneNode> damagedUnits = ships
                .Where(ship => ship.CurrentHullStrength < ship.MaxHullStrength)
                .Cast<ISceneNode>()
                .Concat(
                    fighters
                        .Where(fighter => fighter.CurrentSquadronSize < fighter.MaxSquadronSize)
                        .Cast<ISceneNode>()
                );
            CombatUnitSnapshot.RecordOutcomes(units, damagedUnits, Enumerable.Empty<ISceneNode>());
            return units;
        }

        /// <summary>
        /// Applies a space combat result to the game world: updates hull strength, removes
        /// destroyed ships and depleted fighter squadrons, cleans up empty fleets.
        /// </summary>
        /// <param name="result">The combat result to apply.</param>
        /// <param name="attackerFleets">The attacking fleets to clean up.</param>
        /// <param name="defenderFleets">The defending fleets to clean up.</param>
        /// <returns>Events generated from ship damage and destruction.</returns>
        private List<GameResult> ApplyCombatResult(
            SpaceCombatResult result,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets
        )
        {
            return ApplyCombatLosses(
                result.ShipDamage,
                result.FighterLosses,
                attackerFleets,
                defenderFleets
            );
        }

        /// <summary>
        /// Applies ship and fighter losses and removes fleet containers left without capital ships.
        /// </summary>
        /// <param name="shipDamage">Ship damage to apply.</param>
        /// <param name="fighterLosses">Fighter losses to apply.</param>
        /// <param name="attackerFleets">The attacking fleets to clean up.</param>
        /// <param name="defenderFleets">The defending fleets to clean up.</param>
        /// <returns>Events generated from ship damage and destruction.</returns>
        private List<GameResult> ApplyCombatLosses(
            List<ShipDamageResult> shipDamage,
            List<FighterLossResult> fighterLosses,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets
        )
        {
            ApplyFighterSquadronLosses(fighterLosses);
            List<GameResult> events = ApplyShipDamage(shipDamage);

            foreach (
                Fleet fleet in attackerFleets
                    .Concat(defenderFleets)
                    .Where(fleet => fleet?.GetChildren<CapitalShip>().Count == 0)
                    .ToList()
            )
            {
                RemoveFleetFromScene(fleet);
            }

            return events;
        }

        /// <summary>
        /// Writes hull damage back to each ship, detaches destroyed ships, and evacuates their
        /// officers (to a surviving ship or to the nearest friendly planet).
        /// </summary>
        /// <param name="damageResults">Ship damage entries produced by the battle.</param>
        /// <returns>A GameObjectDamagedResult per damaged ship.</returns>
        private List<GameResult> ApplyShipDamage(List<ShipDamageResult> damageResults)
        {
            List<GameResult> events = new List<GameResult>();

            foreach (ShipDamageResult damage in damageResults)
            {
                CapitalShip ship = damage.Ship;
                if (ship == null)
                    continue;

                ship.CurrentHullStrength = damage.HullAfter;

                events.Add(
                    new GameObjectDamagedResult
                    {
                        GameObject = ship,
                        DamageValue = damage.HullBefore - damage.HullAfter,
                        Tick = _game.CurrentTick,
                    }
                );

                if (damage.HullAfter <= 0)
                {
                    List<IMovable> units = ship.GetChildren<Officer>()
                        .Cast<IMovable>()
                        .Concat(
                            ship.GetChildren<Starfighter>()
                                .Where(starfighter =>
                                    starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                                )
                        )
                        .ToList();
                    _movement.RelocateUnits(units);
                    _game.DeleteNode(ship);
                    GameLogger.Log($"Ship destroyed: {ship.GetDisplayName()}");
                }
            }

            return events;
        }

        /// <summary>
        /// Writes squadron-size losses back to each squadron and detaches any that are wiped out.
        /// </summary>
        /// <param name="lossResults">Fighter loss entries produced by the battle.</param>
        private void ApplyFighterSquadronLosses(List<FighterLossResult> lossResults)
        {
            foreach (FighterLossResult loss in lossResults)
            {
                Starfighter fighter = loss.Fighter;
                if (fighter == null)
                    continue;

                fighter.CurrentSquadronSize = loss.SquadsAfter;

                if (loss.SquadsAfter <= 0)
                {
                    _game.DeleteNode(fighter);
                    GameLogger.Log($"Fighter squadron destroyed: {fighter.GetDisplayName()}");
                }
            }
        }

        /// <summary>
        /// Removes a fleet with no remaining capital ships from the scene graph.
        /// </summary>
        /// <param name="fleet">Empty fleet to remove.</param>
        private void RemoveFleetFromScene(Fleet fleet)
        {
            GameLogger.Warning(
                $"[fleet] removed {fleet.InstanceID} role={fleet.RoleType} owner={fleet.GetOwnerInstanceID()} reason=combat"
            );
            _game.DeleteNode(fleet);
            GameLogger.Log($"Fleet destroyed: {fleet.GetDisplayName()}");
        }
    }
}
