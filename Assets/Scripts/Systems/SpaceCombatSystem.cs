using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
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
    public class SpaceCombatSystem
    {
        private readonly GameRoot _game;
        private readonly MovementSystem _movement;
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
            result = _pendingDecision == null ? null : BuildPendingCombatResult(_pendingDecision);
            return result != null;
        }

        /// <summary>
        /// Creates the space-combat system.
        /// </summary>
        /// <param name="game">Active game state.</param>
        /// <param name="movement">Movement system used for retreats and evacuation.</param>
        public SpaceCombatSystem(GameRoot game, MovementSystem movement)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _autoResolver = new SpaceCombatAutoResolver(
                game.Config.Combat.SpaceCombat,
                game.Random
            );
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
                results.Add(BuildPendingCombatResult(decision));
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
            if (!BothSidesAIControlled(decision))
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
            return AreForcesContestingPlanet(decision);
        }

        /// <summary>
        /// Returns whether both sides belong to AI-controlled factions.
        /// </summary>
        /// <param name="decision">The combat decision to evaluate.</param>
        /// <returns>True when both sides are AI-controlled.</returns>
        private bool BothSidesAIControlled(SpaceCombatDecision decision)
        {
            Faction attacker = _game.GetFactionByOwnerInstanceID(decision.AttackerOwnerInstanceID);
            Faction defender = _game.GetFactionByOwnerInstanceID(decision.DefenderOwnerInstanceID);
            return attacker != null
                && defender != null
                && attacker.IsAIControlled()
                && defender.IsAIControlled();
        }

        /// <summary>
        /// Builds the result that pauses a player-involved encounter.
        /// </summary>
        /// <param name="decision">The pending combat decision.</param>
        /// <returns>The pending-combat result.</returns>
        private PendingCombatResult BuildPendingCombatResult(SpaceCombatDecision decision)
        {
            List<Fleet> attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);
            Fleet attacker = GetRepresentativeFleet(attackerFleets);
            Fleet defender = GetRepresentativeFleet(defenderFleets);
            Planet planet = ResolveCombatPlanet(decision);

            return new PendingCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID,
                Planet = planet,
                AttackerCanRetreat = CanRetreatForces(
                    attackerFleets,
                    defenderFleets,
                    planet,
                    decision.AttackerOwnerInstanceID
                ),
                DefenderCanRetreat = CanRetreatForces(
                    defenderFleets,
                    attackerFleets,
                    planet,
                    decision.DefenderOwnerInstanceID
                ),
                Tick = _game.CurrentTick,
            };
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
            Planet planet = ResolveCombatPlanet(decision);
            results = new List<GameResult>();

            List<Fleet> attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);
            bool attackerRetreated =
                retreatingFactionInstanceId == decision.AttackerOwnerInstanceID;
            List<Fleet> retreatingFleets = attackerRetreated ? attackerFleets : defenderFleets;
            List<Fleet> opposingFleets = attackerRetreated ? defenderFleets : attackerFleets;
            List<Starfighter> retreatingFighters = GetActivePlanetStarfighters(
                    planet,
                    retreatingFactionInstanceId
                )
                .ToList();

            if (
                !CanRetreatForces(
                    retreatingFleets,
                    opposingFleets,
                    planet,
                    retreatingFactionInstanceId
                ) || !TryRetreatFleets(retreatingFleets, opposingFleets, ignoreGravityWell: false)
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

            results.Add(result);
            ClearCombatFlags(decision);
            return true;
        }

        /// <summary>
        /// Builds the combat result emitted after a successful fleet withdrawal.
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
            Fleet attacker = GetRepresentativeFleet(attackerFleets);
            Fleet defender = GetRepresentativeFleet(defenderFleets);
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID,
                Planet = planet,
                PlanetOwnerInstanceID = planet.OwnerInstanceID,
                AttackerRetreatPlanetInstanceID = attackerRetreated
                    ? attacker.GetParentOfType<Planet>()?.InstanceID
                    : null,
                DefenderRetreatPlanetInstanceID = attackerRetreated
                    ? null
                    : defender.GetParentOfType<Planet>()?.InstanceID,
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
                !TryFindContestedForces(
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
        /// Finds the first pair of hostile space forces occupying the same planet.
        /// </summary>
        /// <param name="excludedFleetIds">Fleet instance IDs to skip.</param>
        /// <param name="contestedPlanet">The planet occupied by both sides.</param>
        /// <param name="attackerOwnerInstanceId">The attacking owner identifier.</param>
        /// <param name="defenderOwnerInstanceId">The defending owner identifier.</param>
        /// <param name="attackerFleets">The attacking fleets.</param>
        /// <param name="defenderFleets">The defending fleets.</param>
        /// <returns>True if hostile space forces were found.</returns>
        private bool TryFindContestedForces(
            HashSet<string> excludedFleetIds,
            out Planet contestedPlanet,
            out string attackerOwnerInstanceId,
            out string defenderOwnerInstanceId,
            out List<Fleet> attackerFleets,
            out List<Fleet> defenderFleets
        )
        {
            contestedPlanet = null;
            attackerOwnerInstanceId = null;
            defenderOwnerInstanceId = null;
            attackerFleets = null;
            defenderFleets = null;

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                List<Fleet> fleets = planet
                    .GetChildren<Fleet>()
                    .Where(fleet =>
                        !fleet.IsInCombat
                        && !excludedFleetIds.Contains(fleet.GetInstanceID())
                        && fleet.Movement == null
                        && HasActiveSpaceUnits(fleet)
                    )
                    .ToList();

                List<string> ownerInstanceIds = fleets
                    .Select(fleet => fleet.GetOwnerInstanceID())
                    .Concat(
                        GetActivePlanetStarfighters(planet, null)
                            .Select(fighter => fighter.GetOwnerInstanceID())
                    )
                    .Where(ownerInstanceId => !string.IsNullOrEmpty(ownerInstanceId))
                    .Distinct()
                    .OrderBy(ownerInstanceId => ownerInstanceId)
                    .ToList();

                if (ownerInstanceIds.Count < 2)
                    continue;

                string firstOwnerInstanceId = ownerInstanceIds[0];
                string secondOwnerInstanceId = ownerInstanceIds[1];
                List<Fleet> firstFleets = fleets
                    .Where(fleet => fleet.GetOwnerInstanceID() == firstOwnerInstanceId)
                    .ToList();
                List<Fleet> secondFleets = fleets
                    .Where(fleet => fleet.GetOwnerInstanceID() == secondOwnerInstanceId)
                    .ToList();

                attackerOwnerInstanceId = firstOwnerInstanceId;
                defenderOwnerInstanceId = secondOwnerInstanceId;
                attackerFleets = firstFleets;
                defenderFleets = secondFleets;

                if (attackerFleets.Count == 0 && defenderFleets.Count > 0)
                {
                    (attackerOwnerInstanceId, defenderOwnerInstanceId) = (
                        defenderOwnerInstanceId,
                        attackerOwnerInstanceId
                    );
                    (attackerFleets, defenderFleets) = (defenderFleets, attackerFleets);
                }

                contestedPlanet = planet;
                return true;
            }

            return false;
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
            List<Fleet> attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);
            SpaceCombatResult combatResult = null;

            try
            {
                if (AreForcesContestingPlanet(decision))
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

            List<Fleet> attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);
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

            if (HasActiveSpaceUnits(fleets, battlePlanet, ownerInstanceId))
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
                Fleet fleet in GetFleets(decision.AttackerFleetInstanceIDs)
                    .Concat(GetFleets(decision.DefenderFleetInstanceIDs))
            )
            {
                fleet.SetCombatState(false);
            }
        }

        /// <summary>
        /// Reports whether every force on one side can withdraw from its opponent.
        /// </summary>
        /// <param name="fleets">The fleets requesting withdrawal.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The withdrawing faction identifier.</param>
        /// <returns>True when every fleet and directly deployed fighter can evacuate.</returns>
        private bool CanRetreatForces(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents,
            Planet planet,
            string ownerInstanceId
        )
        {
            return fleets?.Count > 0
                && !IsRetreatBlockedByGravityWell(fleets, opponents)
                && fleets.All(fleet =>
                    HasHyperdriveCapableShip(fleet)
                    && _movement.CanEvacuateToNearestFriendlyPlanet(fleet)
                )
                && GetActivePlanetStarfighters(planet, ownerInstanceId)
                    .All(fighter =>
                        fighter.Hyperdrive > 0
                        && _movement.CanEvacuateToNearestFriendlyPlanet(fighter)
                    );
        }

        /// <summary>
        /// Returns the tactical unit groups capable of leaving an automatically resolved battle.
        /// </summary>
        /// <param name="fleets">The fleets on the withdrawing side.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The withdrawing owner identifier.</param>
        /// <returns>The fleets and independent fighter squadrons that can withdraw.</returns>
        private List<IReadOnlyCollection<ISceneNode>> GetAutomaticWithdrawalGroups(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents,
            Planet planet,
            string ownerInstanceId
        )
        {
            List<IReadOnlyCollection<ISceneNode>> groups =
                new List<IReadOnlyCollection<ISceneNode>>();
            if (IsRetreatBlockedByGravityWell(planet, opponents))
                return groups;

            foreach (
                Fleet fleet in (fleets ?? Array.Empty<Fleet>()).Where(fleet =>
                    HasHyperdriveCapableShip(fleet)
                    && _movement.CanEvacuateToNearestFriendlyPlanet(fleet)
                )
            )
            {
                List<ISceneNode> fleetUnits = GetActiveCapitalShips(fleet)
                    .Cast<ISceneNode>()
                    .Concat(GetActiveStarfighters(fleet))
                    .Distinct()
                    .ToList();
                if (fleetUnits.Count > 0)
                    groups.Add(fleetUnits);
            }

            foreach (Starfighter fighter in GetActivePlanetStarfighters(planet, ownerInstanceId))
            {
                if (fighter.Hyperdrive > 0 && _movement.CanEvacuateToNearestFriendlyPlanet(fighter))
                {
                    groups.Add(new ISceneNode[] { fighter });
                }
            }

            return groups;
        }

        /// <summary>
        /// Returns whether a fleet has a surviving capital ship capable of entering hyperspace.
        /// </summary>
        /// <param name="fleet">The fleet whose withdrawal capability is being checked.</param>
        /// <returns>True when at least one active capital ship has a hyperdrive.</returns>
        private static bool HasHyperdriveCapableShip(Fleet fleet)
        {
            return fleet != null && GetActiveCapitalShips(fleet).Any(ship => ship.Hyperdrive > 0);
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
            return fleet.Movement != null || fleet.GetParentOfType<Planet>() != originalPlanet;
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

            if (!ignoreGravityWell && IsRetreatBlockedByGravityWell(fleets, opponents))
                return false;
            if (fleets.Any(fleet => !HasHyperdriveCapableShip(fleet)))
                return false;

            bool allRetreated = true;
            foreach (Fleet fleet in fleets)
                allRetreated &= TryRetreatFleet(fleet);

            return allRetreated;
        }

        /// <summary>
        /// Determines whether any opposing fleet projects a gravity well at the combat planet.
        /// </summary>
        /// <param name="fleets">The fleets attempting to retreat.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <returns>True when an active opposing ship blocks withdrawal.</returns>
        private static bool IsRetreatBlockedByGravityWell(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents
        )
        {
            Planet fleetPlanet = fleets
                ?.Select(fleet => fleet?.GetParentOfType<Planet>())
                .FirstOrDefault(planet => planet != null);
            return IsRetreatBlockedByGravityWell(fleetPlanet, opponents);
        }

        /// <summary>
        /// Determines whether an opposing fleet projects a gravity well at a specified planet.
        /// </summary>
        /// <param name="planet">The planet where withdrawal would begin.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <returns>True when an active opposing ship blocks withdrawal.</returns>
        private static bool IsRetreatBlockedByGravityWell(
            Planet planet,
            IReadOnlyList<Fleet> opponents
        )
        {
            return planet != null
                && opponents?.Any(opponent =>
                    opponent?.GetParentOfType<Planet>() == planet
                    && GetActiveCapitalShips(opponent).Any(ship => ship.HasGravityWell)
                ) == true;
        }

        /// <summary>
        /// Resolves the planet associated with a pending combat decision.
        /// </summary>
        /// <param name="decision">The pending combat decision.</param>
        /// <returns>The recorded or fleet-hosting planet, or null.</returns>
        private Planet ResolveCombatPlanet(SpaceCombatDecision decision)
        {
            Planet planet = _game.GetSceneNodeByInstanceID<Planet>(decision.PlanetInstanceID);
            if (planet != null)
                return planet;

            Fleet attacker = GetRepresentativeFleet(GetFleets(decision.AttackerFleetInstanceIDs));
            planet = attacker?.GetParentOfType<Planet>();
            if (planet != null)
                return planet;

            Fleet defender = GetRepresentativeFleet(GetFleets(decision.DefenderFleetInstanceIDs));
            return defender?.GetParentOfType<Planet>();
        }

        /// <summary>
        /// Resolves the participating fleets that still exist in the scene graph.
        /// </summary>
        /// <param name="fleetInstanceIds">The participating fleet identifiers.</param>
        /// <returns>The live fleets in encounter order.</returns>
        private List<Fleet> GetFleets(IEnumerable<string> fleetInstanceIds)
        {
            return (fleetInstanceIds ?? Enumerable.Empty<string>())
                .Select(fleetInstanceId => _game.GetSceneNodeByInstanceID<Fleet>(fleetInstanceId))
                .Where(fleet => fleet != null)
                .ToList();
        }

        /// <summary>
        /// Returns the fleet used to identify a multi-fleet combat side.
        /// </summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <returns>The first fleet in encounter order, or null.</returns>
        private static Fleet GetRepresentativeFleet(IReadOnlyList<Fleet> fleets)
        {
            return fleets == null || fleets.Count == 0 ? null : fleets[0];
        }

        /// <summary>
        /// Determines whether two hostile active fleets still contest the same planet.
        /// </summary>
        /// <param name="attacker">Attacking fleet.</param>
        /// <param name="defender">Defending fleet.</param>
        /// <returns>True when both fleets remain stationary, active, hostile, and colocated.</returns>
        internal static bool AreFleetsContestingPlanet(Fleet attacker, Fleet defender)
        {
            if (attacker == null || defender == null)
                return false;

            Planet attackerPlanet = attacker.GetParentOfType<Planet>();
            Planet defenderPlanet = defender.GetParentOfType<Planet>();

            return attackerPlanet != null
                && attackerPlanet == defenderPlanet
                && attacker.Movement == null
                && defender.Movement == null
                && HasActiveSpaceUnits(attacker)
                && HasActiveSpaceUnits(defender)
                && attacker.GetOwnerInstanceID() != defender.GetOwnerInstanceID();
        }

        /// <summary>
        /// Returns whether both recorded sides retain active space forces at the encounter planet.
        /// </summary>
        /// <param name="decision">The encounter to evaluate.</param>
        /// <returns>True when hostile active forces still contest the planet.</returns>
        private bool AreForcesContestingPlanet(SpaceCombatDecision decision)
        {
            Planet planet = ResolveCombatPlanet(decision);
            if (planet == null)
                return false;

            List<Fleet> attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);

            return decision.AttackerOwnerInstanceID != decision.DefenderOwnerInstanceID
                && HasActiveSpaceUnits(attackerFleets, planet, decision.AttackerOwnerInstanceID)
                && HasActiveSpaceUnits(defenderFleets, planet, decision.DefenderOwnerInstanceID);
        }

        /// <summary>
        /// Determines whether a fleet has any active capital ships or starfighters.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when at least one active space unit remains.</returns>
        internal static bool HasActiveSpaceUnits(Fleet fleet)
        {
            if (fleet == null)
                return false;

            return GetActiveCapitalShips(fleet).Any() || GetActiveStarfighters(fleet).Any();
        }

        /// <summary>
        /// Returns whether an owner has active units across any participating fleet or the planet.
        /// </summary>
        /// <param name="fleets">The owner's participating fleets.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="ownerInstanceId">The owner whose forces are being inspected.</param>
        /// <returns>True when at least one active space unit remains.</returns>
        private static bool HasActiveSpaceUnits(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId
        )
        {
            return fleets?.Any(fleet =>
                    fleet != null
                    && fleet.Movement == null
                    && fleet.GetParentOfType<Planet>() == planet
                    && HasActiveSpaceUnits(fleet)
                ) == true
                || GetActivePlanetStarfighters(planet, ownerInstanceId).Any();
        }

        /// <summary>
        /// Returns active capital ships in a fleet.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The active capital ships.</returns>
        private static IEnumerable<CapitalShip> GetActiveCapitalShips(Fleet fleet)
        {
            if (fleet == null)
                return Enumerable.Empty<CapitalShip>();

            return fleet.GetChildren<CapitalShip>().Where(IsActiveCapitalShip);
        }

        /// <summary>
        /// Returns active starfighters carried by active capital ships.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The active starfighter groups.</returns>
        private static IEnumerable<Starfighter> GetActiveStarfighters(Fleet fleet)
        {
            if (fleet == null)
                return Enumerable.Empty<Starfighter>();

            return GetActiveCapitalShips(fleet)
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Where(IsActiveStarfighter);
        }

        /// <summary>
        /// Returns active starfighters deployed directly to a planet for one owner.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="ownerInstanceId">The owner to filter by, or null for every owner.</param>
        /// <returns>The matching active planetary starfighters.</returns>
        private static IEnumerable<Starfighter> GetActivePlanetStarfighters(
            Planet planet,
            string ownerInstanceId
        )
        {
            if (planet == null)
                return Enumerable.Empty<Starfighter>();

            return planet
                .GetChildren<Starfighter>()
                .Where(fighter =>
                    (
                        string.IsNullOrEmpty(ownerInstanceId)
                        || fighter.GetOwnerInstanceID() == ownerInstanceId
                    ) && IsActiveStarfighter(fighter)
                );
        }

        /// <summary>
        /// Determines whether a capital ship can participate in space combat.
        /// </summary>
        /// <param name="ship">Capital ship to inspect.</param>
        /// <returns>True when the ship is complete, stationary, and has remaining hull.</returns>
        private static bool IsActiveCapitalShip(CapitalShip ship)
        {
            return ship.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.Movement == null
                && ship.CurrentHullStrength > 0;
        }

        /// <summary>
        /// Determines whether a starfighter group can participate in space combat.
        /// </summary>
        /// <param name="starfighter">Starfighter group to inspect.</param>
        /// <returns>True when the group is complete, stationary, and has remaining fighters.</returns>
        private static bool IsActiveStarfighter(Starfighter starfighter)
        {
            return starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                && starfighter.Movement == null
                && starfighter.CurrentSquadronSize > 0;
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
            Planet planet = ResolveCombatPlanet(decision);
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
                    fleet != null && GetActiveCapitalShips(fleet).Any(withdrawnUnits.Contains)
                )
            )
            {
                TryRetreatFleet(fleet);
            }

            foreach (
                Starfighter fighter in planet
                    .GetChildren<Starfighter>()
                    .Where(IsActiveStarfighter)
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
                .SelectMany(GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<CapitalShip> defenderShips = defenderFleets
                .SelectMany(GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<Starfighter> attackerPlanetaryFighters = GetActivePlanetStarfighters(
                    planet,
                    attackerOwnerInstanceId
                )
                .ToList();
            List<Starfighter> defenderPlanetaryFighters = GetActivePlanetStarfighters(
                    planet,
                    defenderOwnerInstanceId
                )
                .ToList();
            List<Starfighter> attackerFighters = attackerShips
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(attackerPlanetaryFighters)
                .Where(IsActiveStarfighter)
                .Distinct()
                .ToList();
            List<Starfighter> defenderFighters = defenderShips
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(defenderPlanetaryFighters)
                .Where(IsActiveStarfighter)
                .Distinct()
                .ToList();
            List<IReadOnlyCollection<ISceneNode>> attackerWithdrawalGroups =
                GetAutomaticWithdrawalGroups(
                    attackerFleets,
                    defenderFleets,
                    planet,
                    attackerOwnerInstanceId
                );
            List<IReadOnlyCollection<ISceneNode>> defenderWithdrawalGroups =
                GetAutomaticWithdrawalGroups(
                    defenderFleets,
                    attackerFleets,
                    planet,
                    defenderOwnerInstanceId
                );
            SpaceCombatAutoResult autoResult = _autoResolver.Resolve(
                attackerShips,
                attackerFighters,
                defenderShips,
                defenderFighters,
                attackerWithdrawalGroups,
                defenderWithdrawalGroups
            );
            withdrawnUnits = autoResult
                .Ships.Where(outcome => outcome.Withdrew)
                .Select(outcome => (ISceneNode)outcome.Ship)
                .Concat(
                    autoResult
                        .Fighters.Where(outcome => outcome.Withdrew)
                        .Select(outcome => (ISceneNode)outcome.Fighter)
                )
                .ToHashSet();
            List<ShipSnap> attackerShipSnapshots = CreateShipSnapshots(
                attackerShips,
                autoResult.Ships
            );
            List<ShipSnap> defenderShipSnapshots = CreateShipSnapshots(
                defenderShips,
                autoResult.Ships
            );
            List<FighterSnap> attackerFighterSnapshots = CreateFighterSnapshots(
                attackerFighters,
                autoResult.Fighters
            );
            List<FighterSnap> defenderFighterSnapshots = CreateFighterSnapshots(
                defenderFighters,
                autoResult.Fighters
            );

            SpaceCombatResult result = BuildSpaceResult(
                GetRepresentativeFleet(attackerFleets),
                GetRepresentativeFleet(defenderFleets),
                attackerOwnerInstanceId,
                defenderOwnerInstanceId,
                planet,
                attackerShipSnapshots,
                defenderShipSnapshots,
                attackerFighterSnapshots,
                defenderFighterSnapshots,
                tick
            );
            result.AttackerOutcome = autoResult.AttackerOutcome;
            result.DefenderOutcome = autoResult.DefenderOutcome;
            result.Winner = DetermineWinner(
                result.AttackerOutcome,
                result.DefenderOutcome,
                attackerShipSnapshots,
                defenderShipSnapshots,
                attackerFighterSnapshots,
                defenderFighterSnapshots
            );
            return result;
        }

        /// <summary>
        /// Creates result snapshots for the supplied capital ships.
        /// </summary>
        /// <param name="ships">The capital ships on one combat side.</param>
        /// <param name="outcomes">All resolved capital-ship outcomes.</param>
        /// <returns>The result snapshots for the supplied ships.</returns>
        private static List<ShipSnap> CreateShipSnapshots(
            IReadOnlyList<CapitalShip> ships,
            IReadOnlyList<SpaceCombatAutoShipOutcome> outcomes
        )
        {
            Dictionary<CapitalShip, SpaceCombatAutoShipOutcome> outcomeByShip =
                outcomes.ToDictionary(outcome => outcome.Ship);
            return ships
                .Select(ship => outcomeByShip[ship])
                .Select(outcome => new ShipSnap
                {
                    Ship = outcome.Ship,
                    HullInitial = outcome.HullBefore,
                    HullCurrent = outcome.HullAfter,
                    HullMax = outcome.Ship.MaxHullStrength,
                    Alive = outcome.HullAfter > 0,
                })
                .ToList();
        }

        /// <summary>
        /// Creates result snapshots for the supplied fighter squadrons.
        /// </summary>
        /// <param name="fighters">The fighter squadrons on one combat side.</param>
        /// <param name="outcomes">All resolved fighter outcomes.</param>
        /// <returns>The result snapshots for the supplied squadrons.</returns>
        private static List<FighterSnap> CreateFighterSnapshots(
            IReadOnlyList<Starfighter> fighters,
            IReadOnlyList<SpaceCombatAutoFighterOutcome> outcomes
        )
        {
            Dictionary<Starfighter, SpaceCombatAutoFighterOutcome> outcomeByFighter =
                outcomes.ToDictionary(outcome => outcome.Fighter);
            return fighters
                .Select(fighter => outcomeByFighter[fighter])
                .Select(outcome => new FighterSnap
                {
                    Fighter = outcome.Fighter,
                    InitialSquadronSize = outcome.SquadronSizeBefore,
                    CurrentSquadronSize = outcome.SquadronSizeAfter,
                })
                .ToList();
        }

        /// <summary>
        /// Determines the winning side after destruction and withdrawal are resolved.
        /// </summary>
        /// <param name="attackerOutcome">The attacker's final outcome.</param>
        /// <param name="defenderOutcome">The defender's final outcome.</param>
        /// <param name="attackerShips">The attacking ship snapshots.</param>
        /// <param name="defenderShips">The defending ship snapshots.</param>
        /// <param name="attackerFighters">The attacking fighter snapshots.</param>
        /// <param name="defenderFighters">The defending fighter snapshots.</param>
        /// <returns>The winning side, or a draw when both outcomes match.</returns>
        private static CombatSide DetermineWinner(
            SpaceCombatSideOutcome attackerOutcome,
            SpaceCombatSideOutcome defenderOutcome,
            List<ShipSnap> attackerShips,
            List<ShipSnap> defenderShips,
            List<FighterSnap> attackerFighters,
            List<FighterSnap> defenderFighters
        )
        {
            bool attackerActive = attackerOutcome == SpaceCombatSideOutcome.Active;
            bool defenderActive = defenderOutcome == SpaceCombatSideOutcome.Active;
            if (attackerActive != defenderActive)
                return attackerActive ? CombatSide.Attacker : CombatSide.Defender;

            return DetermineWinner(
                attackerShips,
                defenderShips,
                attackerFighters,
                defenderFighters
            );
        }

        /// <summary>
        /// Determines the combat winner by counting surviving capital ships and fighter squadrons
        /// on each side. Returns Draw if both sides have survivors or both are wiped out.
        /// </summary>
        /// <param name="atkShips">Attacker ship snapshots.</param>
        /// <param name="defShips">Defender ship snapshots.</param>
        /// <param name="atkFighters">Attacker fighter snapshots.</param>
        /// <param name="defFighters">Defender fighter snapshots.</param>
        /// <returns>The winning side, or Draw.</returns>
        private static CombatSide DetermineWinner(
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            List<FighterSnap> atkFighters,
            List<FighterSnap> defFighters
        )
        {
            bool atkAlive = atkShips.Any(s => s.Alive) || atkFighters.Any(fighter => fighter.Alive);
            bool defAlive = defShips.Any(s => s.Alive) || defFighters.Any(fighter => fighter.Alive);

            if (atkAlive && !defAlive)
                return CombatSide.Attacker;
            if (!atkAlive && defAlive)
                return CombatSide.Defender;
            return CombatSide.Draw;
        }

        /// <summary>
        /// Builds a SpaceCombatResult from the final snapshots and initial fighter counts,
        /// recording per-ship damage and per-squadron losses.
        /// </summary>
        /// <param name="attackerFleet">Attacker fleet.</param>
        /// <param name="defenderFleet">Defender fleet.</param>
        /// <param name="attackerOwnerInstanceId">Attacking owner identifier.</param>
        /// <param name="defenderOwnerInstanceId">Defending owner identifier.</param>
        /// <param name="planet">Planet where combat occurred.</param>
        /// <param name="atkShips">Post-combat attacker ship snapshots.</param>
        /// <param name="defShips">Post-combat defender ship snapshots.</param>
        /// <param name="atkFighters">Post-combat attacker fighter snapshots.</param>
        /// <param name="defFighters">Post-combat defender fighter snapshots.</param>
        /// <param name="tick">Game tick when combat occurred.</param>
        /// <returns>The populated combat result.</returns>
        private static SpaceCombatResult BuildSpaceResult(
            Fleet attackerFleet,
            Fleet defenderFleet,
            string attackerOwnerInstanceId,
            string defenderOwnerInstanceId,
            Planet planet,
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            List<FighterSnap> atkFighters,
            List<FighterSnap> defFighters,
            int tick
        )
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attackerFleet,
                DefenderFleet = defenderFleet,
                AttackerOwnerInstanceID = attackerOwnerInstanceId,
                DefenderOwnerInstanceID = defenderOwnerInstanceId,
                Planet = planet,
                PlanetOwnerInstanceID = planet.OwnerInstanceID,
                Winner = DetermineWinner(atkShips, defShips, atkFighters, defFighters),
                AttackerOutcome = GetResolvedCombatSideOutcome(atkShips, atkFighters),
                DefenderOutcome = GetResolvedCombatSideOutcome(defShips, defFighters),
                Tick = tick,
            };

            CollectShipDamage(result.ShipDamage, atkShips);
            CollectShipDamage(result.ShipDamage, defShips);
            CollectFighterLosses(result.FighterLosses, atkFighters);
            CollectFighterLosses(result.FighterLosses, defFighters);
            result.AttackingUnits.AddRange(CaptureCombatUnits(atkShips, atkFighters));
            result.DefendingUnits.AddRange(CaptureCombatUnits(defShips, defFighters));

            return result;
        }

        /// <summary>
        /// Captures the ships, fighters, and carried units present in one combat force.
        /// </summary>
        /// <param name="ships">The participating capital ships.</param>
        /// <param name="fighters">The participating fighter squadrons.</param>
        /// <returns>The detached unit snapshots for the force.</returns>
        private static List<CombatUnitSnapshot> CaptureCombatUnits(
            List<ShipSnap> ships,
            List<FighterSnap> fighters
        )
        {
            List<CombatUnitSnapshot> units = ships
                .SelectMany(ship =>
                    new[] { ship.Ship }
                        .Cast<ISceneNode>()
                        .Concat(ship.Ship.GetChildren<ISceneNode>(recursive: true))
                )
                .Concat(fighters.Select(fighter => fighter.Fighter))
                .Where(unit => unit != null)
                .Distinct()
                .Select(unit => new CombatUnitSnapshot(unit))
                .ToList();
            IEnumerable<ISceneNode> damagedUnits = ships
                .Where(ship => ship.HullCurrent < ship.HullMax)
                .Select(ship => (ISceneNode)ship.Ship)
                .Concat(
                    fighters
                        .Where(fighter => fighter.CurrentSquadronSize < fighter.InitialSquadronSize)
                        .Select(fighter => fighter.Fighter)
                );
            IEnumerable<ISceneNode> destroyedUnits = ships
                .Where(ship => ship.HullCurrent <= 0)
                .Select(ship => (ISceneNode)ship.Ship)
                .Concat(
                    fighters
                        .Where(fighter => fighter.CurrentSquadronSize <= 0)
                        .Select(fighter => fighter.Fighter)
                );
            CombatUnitSnapshot.RecordOutcomes(units, damagedUnits, destroyedUnits);
            return units;
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
                .SelectMany(GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<Starfighter> fighters = ships
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(GetActivePlanetStarfighters(planet, ownerInstanceId))
                .Where(IsActiveStarfighter)
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
        /// Resolves a combat side's outcome from completed tactical unit snapshots.
        /// </summary>
        /// <param name="ships">The side's resolved ship snapshots.</param>
        /// <param name="fighters">The side's resolved fighter snapshots.</param>
        /// <returns>The side's resolved outcome.</returns>
        private static SpaceCombatSideOutcome GetResolvedCombatSideOutcome(
            List<ShipSnap> ships,
            List<FighterSnap> fighters
        )
        {
            return
                ships.Any(ship => ship.HullCurrent > 0)
                || fighters.Any(fighter => fighter.CurrentSquadronSize > 0)
                ? SpaceCombatSideOutcome.Active
                : SpaceCombatSideOutcome.Destroyed;
        }

        /// <summary>
        /// Appends a ShipDamageResult for each ship that took hull damage during the battle.
        /// </summary>
        /// <param name="results">List to append damage entries to.</param>
        /// <param name="ships">Post-combat ship snapshots.</param>
        private static void CollectShipDamage(List<ShipDamageResult> results, List<ShipSnap> ships)
        {
            for (int i = 0; i < ships.Count; i++)
            {
                int hullAfter = GetCommittedHullStrength(ships[i]);
                if (hullAfter < ships[i].HullInitial)
                {
                    results.Add(
                        new ShipDamageResult
                        {
                            Ship = ships[i].Ship,
                            HullBefore = ships[i].HullInitial,
                            HullAfter = hullAfter,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Converts a capital ship's simulated hull strength into its committed integer value.
        /// </summary>
        /// <param name="ship">The capital-ship snapshot to inspect.</param>
        /// <returns>Zero for a destroyed ship; otherwise at least one hull point.</returns>
        private static int GetCommittedHullStrength(ShipSnap ship)
        {
            if (!ship.Alive)
                return 0;

            double survivingHullStrength = Math.Max(ship.HullCurrent, 1);
            return (int)Math.Round(survivingHullStrength, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Appends a FighterLossResult for each squadron that took casualties.
        /// </summary>
        /// <param name="results">List to append loss entries to.</param>
        /// <param name="fighters">Post-combat fighter snapshots.</param>
        private static void CollectFighterLosses(
            List<FighterLossResult> results,
            List<FighterSnap> fighters
        )
        {
            for (int i = 0; i < fighters.Count; i++)
            {
                if (fighters[i].CurrentSquadronSize < fighters[i].InitialSquadronSize)
                {
                    results.Add(
                        new FighterLossResult
                        {
                            Fighter = fighters[i].Fighter,
                            SquadsBefore = fighters[i].InitialSquadronSize,
                            SquadsAfter = fighters[i].CurrentSquadronSize,
                        }
                    );
                }
            }
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

        /// <summary>
        /// Contains one capital ship's detached automatic-combat outcome.
        /// </summary>
        private class ShipSnap
        {
            public CapitalShip Ship;
            public int HullInitial;
            public double HullCurrent;
            public int HullMax;
            public bool Alive;
        }

        /// <summary>
        /// Contains one fighter squadron's detached automatic-combat outcome.
        /// </summary>
        private class FighterSnap
        {
            public Starfighter Fighter;
            public int InitialSquadronSize;
            public int CurrentSquadronSize;

            public bool Alive => CurrentSquadronSize > 0;
        }
    }
}
