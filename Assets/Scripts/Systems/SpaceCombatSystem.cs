using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Combat;
using Rebellion.Game;
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
        private readonly AISpaceCombatPolicy _aiCombatPolicy;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;
        private readonly Dictionary<CapitalShip, float> _battleHullStrengths =
            new Dictionary<CapitalShip, float>();
        private readonly Dictionary<CapitalShip, float> _battleShieldStrengths =
            new Dictionary<CapitalShip, float>();
        private SpaceCombatDecision _pendingDecision;
        private bool _battleTacticalStateChanged;

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
        /// <param name="provider">Random-number provider used by combat resolution.</param>
        /// <param name="movement">Movement system used for retreats and evacuation.</param>
        public SpaceCombatSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement
        )
        {
            _game = game;
            _aiCombatPolicy = new AISpaceCombatPolicy(game);
            _provider = provider;
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
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

            return new PendingCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID,
                Planet = ResolveCombatPlanet(decision),
                AttackerCanRetreat = CanRetreatFleets(attackerFleets, defenderFleets),
                DefenderCanRetreat = CanRetreatFleets(defenderFleets, attackerFleets),
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

            if (!TryRetreatFleets(retreatingFleets, opposingFleets, ignoreGravityWell: false))
                return false;

            results.Add(
                BuildRetreatResult(
                    decision,
                    attackerRetreated,
                    attackerFleets,
                    defenderFleets,
                    planet
                )
            );
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

            (List<ShipSnap> attackerShips, List<FighterSnap> attackerFighters) = SnapshotForce(
                attackerFleets,
                planet,
                decision.AttackerOwnerInstanceID,
                _game.Config.Combat.SpaceCombat
            );
            (List<ShipSnap> defenderShips, List<FighterSnap> defenderFighters) = SnapshotForce(
                defenderFleets,
                planet,
                decision.DefenderOwnerInstanceID,
                _game.Config.Combat.SpaceCombat
            );
            result.AttackingUnits.AddRange(CaptureCombatUnits(attackerShips, attackerFighters));
            result.DefendingUnits.AddRange(CaptureCombatUnits(defenderShips, defenderFighters));

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
                return ResolveFleetEncounter(decision, allowRetreatBeforeCombat: false);

            RunManualCombat();
            ClearCombatFlags(decision);
            return new List<GameResult>();
        }

        /// <summary>
        /// Resolves an AI-controlled fleet encounter with pre-combat retreat enabled.
        /// </summary>
        /// <param name="decision">Encounter context to resolve.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        private List<GameResult> ResolveAutomaticFleetEncounter(SpaceCombatDecision decision)
        {
            return ResolveFleetEncounter(decision, allowRetreatBeforeCombat: true);
        }

        /// <summary>
        /// Resolves an entire fleet encounter until combat ends, retreats, or reaches stalemate.
        /// </summary>
        /// <param name="decision">Encounter context to resolve.</param>
        /// <param name="allowRetreatBeforeCombat">Whether an outmatched fleet may retreat first.</param>
        /// <returns>The aggregated result for the encounter.</returns>
        private List<GameResult> ResolveFleetEncounter(
            SpaceCombatDecision decision,
            bool allowRetreatBeforeCombat
        )
        {
            List<GameResult> results = new List<GameResult>();
            SpaceCombatResult combatEncounterResult = null;
            List<Fleet> attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);
            _battleHullStrengths.Clear();
            _battleShieldStrengths.Clear();
            _battleTacticalStateChanged = false;

            try
            {
                while (AreForcesContestingPlanet(decision))
                {
                    Planet planet = ResolveCombatPlanet(decision);
                    if (
                        allowRetreatBeforeCombat
                        && TryRetreatOutmatchedFleets(
                            decision,
                            attackerFleets,
                            defenderFleets,
                            planet
                        )
                    )
                        break;

                    SpaceCombatResult combatResult = ResolveCombatRound(
                        decision,
                        attackerFleets,
                        defenderFleets,
                        _provider
                    );
                    if (combatResult != null)
                    {
                        combatEncounterResult ??= CreateCombatEncounterResult(combatResult);
                        AddCombatRoundResult(combatEncounterResult, combatResult);
                    }

                    attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
                    defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);

                    if (!AreForcesContestingPlanet(decision))
                        break;

                    if (
                        IsSpaceCombatStalemated(
                            decision,
                            attackerFleets,
                            defenderFleets,
                            combatResult
                        )
                    )
                    {
                        ResolveStalematedForces(
                            decision,
                            attackerFleets,
                            defenderFleets,
                            combatEncounterResult
                        );
                        break;
                    }
                }
            }
            finally
            {
                _battleHullStrengths.Clear();
                _battleShieldStrengths.Clear();
                _battleTacticalStateChanged = false;
                ClearCombatFlags(decision);
            }

            UpdateCombatEncounterResultOutcomes(combatEncounterResult, decision);

            if (combatEncounterResult != null)
                results.Add(combatEncounterResult);

            return results;
        }

        /// <summary>
        /// Creates the single encounter result that will aggregate every combat round.
        /// </summary>
        /// <param name="roundResult">The first combat round result in the encounter.</param>
        /// <returns>The encounter result.</returns>
        private static SpaceCombatResult CreateCombatEncounterResult(SpaceCombatResult roundResult)
        {
            return new SpaceCombatResult
            {
                AttackerFleet = roundResult.AttackerFleet,
                DefenderFleet = roundResult.DefenderFleet,
                AttackerOwnerInstanceID = roundResult.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = roundResult.DefenderOwnerInstanceID,
                Planet = roundResult.Planet,
                PlanetOwnerInstanceID = roundResult.PlanetOwnerInstanceID,
                AttackerRetreatPlanetInstanceID = roundResult.AttackerRetreatPlanetInstanceID,
                DefenderRetreatPlanetInstanceID = roundResult.DefenderRetreatPlanetInstanceID,
                Winner = roundResult.Winner,
                AttackerOutcome = roundResult.AttackerOutcome,
                DefenderOutcome = roundResult.DefenderOutcome,
                Tick = roundResult.Tick,
            };
        }

        /// <summary>
        /// Adds one combat round's outcome into an encounter-level combat result.
        /// </summary>
        /// <param name="encounterResult">The encounter result to update.</param>
        /// <param name="roundResult">The combat round result to merge.</param>
        private static void AddCombatRoundResult(
            SpaceCombatResult encounterResult,
            SpaceCombatResult roundResult
        )
        {
            encounterResult.Winner = roundResult.Winner;
            encounterResult.Tick = roundResult.Tick;
            if (string.IsNullOrEmpty(encounterResult.AttackerOwnerInstanceID))
                encounterResult.AttackerOwnerInstanceID = roundResult.AttackerOwnerInstanceID;
            if (string.IsNullOrEmpty(encounterResult.DefenderOwnerInstanceID))
                encounterResult.DefenderOwnerInstanceID = roundResult.DefenderOwnerInstanceID;
            encounterResult.AttackerOutcome = roundResult.AttackerOutcome;
            encounterResult.DefenderOutcome = roundResult.DefenderOutcome;
            AddShipDamage(encounterResult.ShipDamage, roundResult.ShipDamage);
            AddFighterLosses(encounterResult.FighterLosses, roundResult.FighterLosses);
            AddCombatUnitSnapshots(encounterResult.AttackingUnits, roundResult.AttackingUnits);
            AddCombatUnitSnapshots(encounterResult.DefendingUnits, roundResult.DefendingUnits);
            encounterResult.Events.AddRange(roundResult.Events);
        }

        /// <summary>
        /// Merges one round's captured units into an encounter-level snapshot.
        /// </summary>
        /// <param name="encounterUnits">The encounter-level units to update.</param>
        /// <param name="roundUnits">The round-level units to merge.</param>
        private static void AddCombatUnitSnapshots(
            List<CombatUnitSnapshot> encounterUnits,
            IEnumerable<CombatUnitSnapshot> roundUnits
        )
        {
            foreach (CombatUnitSnapshot roundUnit in roundUnits)
            {
                string instanceId = roundUnit?.Unit?.GetInstanceID();
                CombatUnitSnapshot encounterUnit = encounterUnits.FirstOrDefault(unit =>
                    unit?.Unit?.GetInstanceID() == instanceId
                );
                if (encounterUnit == null)
                {
                    encounterUnits.Add(roundUnit);
                    continue;
                }

                encounterUnit.Damaged |= roundUnit.Damaged;
                encounterUnit.Destroyed |= roundUnit.Destroyed;
                encounterUnit.Captured |= roundUnit.Captured;
            }
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
            result.AttackerOutcome = GetCombatSideOutcome(
                attackerFleets,
                result.AttackerOwnerInstanceID,
                result.Planet,
                result.AttackerOutcome
            );
            result.DefenderOutcome = GetCombatSideOutcome(
                defenderFleets,
                result.DefenderOwnerInstanceID,
                result.Planet,
                result.DefenderOutcome
            );
            result.AttackerRetreatPlanetInstanceID = GetRetreatPlanetInstanceID(
                attackerFleets,
                result.Planet,
                result.AttackerOutcome
            );
            result.DefenderRetreatPlanetInstanceID = GetRetreatPlanetInstanceID(
                defenderFleets,
                result.Planet,
                result.DefenderOutcome
            );
        }

        /// <summary>Returns the final destination ID recorded for a withdrawn fleet.</summary>
        private static string GetRetreatPlanetInstanceID(
            IReadOnlyList<Fleet> fleets,
            Planet battlePlanet,
            SpaceCombatSideOutcome outcome
        )
        {
            if (outcome != SpaceCombatSideOutcome.Withdrawn)
                return null;
            Planet destination = fleets
                ?.Select(fleet => fleet?.GetParentOfType<Planet>())
                .FirstOrDefault(planet => planet != null && planet != battlePlanet);
            return destination == battlePlanet ? null : destination?.InstanceID;
        }

        /// <summary>
        /// Resolves a combat side's final encounter outcome.
        /// </summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <param name="ownerInstanceId">The participating owner's identifier.</param>
        /// <param name="battlePlanet">The encounter location.</param>
        /// <param name="roundOutcome">The outcome recorded by the final combat round.</param>
        /// <returns>The final encounter outcome.</returns>
        private static SpaceCombatSideOutcome GetCombatSideOutcome(
            IReadOnlyList<Fleet> fleets,
            string ownerInstanceId,
            Planet battlePlanet,
            SpaceCombatSideOutcome roundOutcome
        )
        {
            if (roundOutcome == SpaceCombatSideOutcome.Destroyed)
                return SpaceCombatSideOutcome.Destroyed;

            if (HasActiveSpaceUnits(fleets, battlePlanet, ownerInstanceId))
                return SpaceCombatSideOutcome.Active;

            if (fleets?.Any(fleet => fleet?.Movement != null) == true)
                return SpaceCombatSideOutcome.Withdrawn;

            List<Planet> currentPlanets =
                fleets
                    ?.Select(fleet => fleet?.GetParentOfType<Planet>())
                    .Where(planet => planet != null)
                    .ToList()
                ?? new List<Planet>();
            if (currentPlanets.Count == 0)
                return SpaceCombatSideOutcome.Destroyed;

            if (battlePlanet != null && currentPlanets.Any(planet => planet != battlePlanet))
                return SpaceCombatSideOutcome.Withdrawn;

            return SpaceCombatSideOutcome.Active;
        }

        /// <summary>
        /// Adds round ship damage into an encounter-level damage list.
        /// </summary>
        /// <param name="encounterDamage">The encounter-level damage list to update.</param>
        /// <param name="roundDamage">The round damage list to merge.</param>
        private static void AddShipDamage(
            List<ShipDamageResult> encounterDamage,
            List<ShipDamageResult> roundDamage
        )
        {
            foreach (ShipDamageResult damage in roundDamage)
            {
                ShipDamageResult existingDamage = encounterDamage.FirstOrDefault(result =>
                    result.Ship == damage.Ship
                );

                if (existingDamage == null)
                {
                    encounterDamage.Add(
                        new ShipDamageResult
                        {
                            Ship = damage.Ship,
                            HullBefore = damage.HullBefore,
                            HullAfter = damage.HullAfter,
                        }
                    );
                    continue;
                }

                existingDamage.HullAfter = damage.HullAfter;
            }
        }

        /// <summary>
        /// Adds round fighter losses into an encounter-level loss list.
        /// </summary>
        /// <param name="encounterLosses">The encounter-level loss list to update.</param>
        /// <param name="roundLosses">The round loss list to merge.</param>
        private static void AddFighterLosses(
            List<FighterLossResult> encounterLosses,
            List<FighterLossResult> roundLosses
        )
        {
            foreach (FighterLossResult loss in roundLosses)
            {
                FighterLossResult existingLoss = encounterLosses.FirstOrDefault(result =>
                    result.Fighter != null && result.Fighter == loss.Fighter
                );

                if (existingLoss == null)
                {
                    encounterLosses.Add(
                        new FighterLossResult
                        {
                            Fighter = loss.Fighter,
                            SquadsBefore = loss.SquadsBefore,
                            SquadsAfter = loss.SquadsAfter,
                        }
                    );
                    continue;
                }

                existingLoss.SquadsAfter = loss.SquadsAfter;
            }
        }

        /// <summary>
        /// Attempts to withdraw stalemated forces and destroys any forces left contesting the
        /// battle planet.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attackerFleets">Attacking fleets.</param>
        /// <param name="defenderFleets">Defending fleets.</param>
        /// <param name="encounterResult">The encounter result that receives forced losses.</param>
        private void ResolveStalematedForces(
            SpaceCombatDecision decision,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            SpaceCombatResult encounterResult
        )
        {
            bool attackerRetreated = TryRetreatFleets(
                attackerFleets,
                defenderFleets,
                ignoreGravityWell: true
            );
            bool defenderRetreated = TryRetreatFleets(
                defenderFleets,
                attackerFleets,
                ignoreGravityWell: true
            );

            if (!AreForcesContestingPlanet(decision))
                return;

            Planet planet = ResolveCombatPlanet(decision);
            List<Fleet> strandedAttackerFleets = GetStationaryFleetsAtPlanet(
                attackerFleets,
                planet
            );
            List<Fleet> strandedDefenderFleets = GetStationaryFleetsAtPlanet(
                defenderFleets,
                planet
            );
            bool hasOperationalWeapons =
                HasOperationalSpaceWeapons(
                    strandedAttackerFleets,
                    planet,
                    decision.AttackerOwnerInstanceID
                )
                || HasOperationalSpaceWeapons(
                    strandedDefenderFleets,
                    planet,
                    decision.DefenderOwnerInstanceID
                );
            if (!hasOperationalWeapons)
            {
                DestroyStalematedForces(
                    decision,
                    strandedAttackerFleets,
                    strandedDefenderFleets,
                    encounterResult
                );
                return;
            }

            if (!attackerRetreated)
                RemoveFleetsUnableToRetreat(strandedAttackerFleets);
            if (!defenderRetreated)
                RemoveFleetsUnableToRetreat(strandedDefenderFleets);
        }

        /// <summary>
        /// Records and applies destruction for every force unable to leave a stalemated battle.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attackerFleets">Attacking fleets.</param>
        /// <param name="defenderFleets">Defending fleets.</param>
        /// <param name="encounterResult">The encounter result that receives forced losses.</param>
        private void DestroyStalematedForces(
            SpaceCombatDecision decision,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            SpaceCombatResult encounterResult
        )
        {
            Planet planet = ResolveCombatPlanet(decision);
            List<CapitalShip> attackerShips = attackerFleets
                .SelectMany(GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<CapitalShip> defenderShips = defenderFleets
                .SelectMany(GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<Starfighter> attackerFighters = GetStalematedStarfighters(
                attackerFleets,
                planet,
                decision.AttackerOwnerInstanceID
            );
            List<Starfighter> defenderFighters = GetStalematedStarfighters(
                defenderFleets,
                planet,
                decision.DefenderOwnerInstanceID
            );

            RecordForcedDestruction(
                encounterResult.AttackingUnits,
                attackerShips,
                attackerFighters
            );
            RecordForcedDestruction(
                encounterResult.DefendingUnits,
                defenderShips,
                defenderFighters
            );

            List<ShipDamageResult> forcedShipDamage = attackerShips
                .Concat(defenderShips)
                .Select(ship => new ShipDamageResult
                {
                    Ship = ship,
                    HullBefore = ship.CurrentHullStrength,
                    HullAfter = 0,
                })
                .ToList();
            List<FighterLossResult> forcedFighterLosses = attackerFighters
                .Concat(defenderFighters)
                .Select(fighter => new FighterLossResult
                {
                    Fighter = fighter,
                    SquadsBefore = fighter.CurrentSquadronSize,
                    SquadsAfter = 0,
                })
                .ToList();

            AddShipDamage(encounterResult.ShipDamage, forcedShipDamage);
            AddFighterLosses(encounterResult.FighterLosses, forcedFighterLosses);
            encounterResult.Events.AddRange(
                ApplyCombatLosses(
                    forcedShipDamage,
                    forcedFighterLosses,
                    attackerFleets,
                    defenderFleets
                )
            );

            GameLogger.Log(
                $"Stalemated forces destroyed at {planet.GetDisplayName()}: "
                    + $"{attackerShips.Count + attackerFighters.Count} attacker units and "
                    + $"{defenderShips.Count + defenderFighters.Count} defender units."
            );
        }

        /// <summary>
        /// Returns participating fleets that remain stationary at the battle planet.
        /// </summary>
        /// <param name="fleets">Fleets to inspect.</param>
        /// <param name="planet">The battle planet.</param>
        /// <returns>The stationary fleets at the battle planet.</returns>
        private static List<Fleet> GetStationaryFleetsAtPlanet(
            IReadOnlyList<Fleet> fleets,
            Planet planet
        )
        {
            return (fleets ?? Array.Empty<Fleet>())
                .Where(fleet =>
                    fleet != null
                    && fleet.Movement == null
                    && fleet.GetParentOfType<Planet>() == planet
                )
                .ToList();
        }

        /// <summary>
        /// Returns active starfighters stranded in fleets or deployed at the battle planet.
        /// </summary>
        /// <param name="fleets">Stationary fleets to inspect.</param>
        /// <param name="planet">The battle planet.</param>
        /// <param name="ownerInstanceId">The owner of planetary starfighters to include.</param>
        /// <returns>The stranded active starfighters.</returns>
        private static List<Starfighter> GetStalematedStarfighters(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId
        )
        {
            return (fleets ?? Array.Empty<Fleet>())
                .SelectMany(GetActiveStarfighters)
                .Concat(GetActivePlanetStarfighters(planet, ownerInstanceId))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Marks forced ship and fighter losses in one side's encounter snapshot.
        /// </summary>
        /// <param name="snapshots">The encounter snapshots to update.</param>
        /// <param name="ships">Ships destroyed by the stalemate resolution.</param>
        /// <param name="fighters">Starfighters destroyed by the stalemate resolution.</param>
        private static void RecordForcedDestruction(
            IEnumerable<CombatUnitSnapshot> snapshots,
            IEnumerable<CapitalShip> ships,
            IEnumerable<Starfighter> fighters
        )
        {
            List<ISceneNode> destroyedUnits = ships.Cast<ISceneNode>().Concat(fighters).ToList();
            CombatUnitSnapshot.RecordOutcomes(snapshots, destroyedUnits, destroyedUnits);
        }

        /// <summary>
        /// Removes an armed stalemated fleet that cannot leave the contested planet.
        /// </summary>
        /// <param name="fleets">Fleets to remove.</param>
        private void RemoveFleetsUnableToRetreat(IEnumerable<Fleet> fleets)
        {
            foreach (Fleet fleet in fleets.Where(fleet => fleet != null).ToList())
            {
                _game.DetachNode(fleet);
                GameLogger.Log($"Fleet removed after stalemated combat: {fleet.GetDisplayName()}");
            }
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
        /// Attempts to retreat the weaker fleet before combat begins.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attackerFleets">Attacking fleets.</param>
        /// <param name="defenderFleets">Defending fleets.</param>
        /// <param name="planet">The combat location.</param>
        /// <returns>True when at least one fleet retreats.</returns>
        private bool TryRetreatOutmatchedFleets(
            SpaceCombatDecision decision,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            Planet planet
        )
        {
            int attackerPower = GetCombatValue(
                attackerFleets,
                GetActivePlanetStarfighters(planet, decision.AttackerOwnerInstanceID)
            );
            int defenderPower = GetCombatValue(
                defenderFleets,
                GetActivePlanetStarfighters(planet, decision.DefenderOwnerInstanceID)
            );
            Fleet attacker = GetRepresentativeFleet(attackerFleets);
            Fleet defender = GetRepresentativeFleet(defenderFleets);

            if (attackerPower == defenderPower)
            {
                bool attackerRetreated =
                    _aiCombatPolicy.ShouldRetreat(attacker, planet, attackerPower, defenderPower)
                    && TryRetreatFleets(attackerFleets, defenderFleets, ignoreGravityWell: false);
                bool defenderRetreated =
                    _aiCombatPolicy.ShouldRetreat(defender, planet, defenderPower, attackerPower)
                    && TryRetreatFleets(defenderFleets, attackerFleets, ignoreGravityWell: false);
                return attackerRetreated || defenderRetreated;
            }

            if (attackerPower < defenderPower)
            {
                return _aiCombatPolicy.ShouldRetreat(attacker, planet, attackerPower, defenderPower)
                    && TryRetreatFleets(attackerFleets, defenderFleets, ignoreGravityWell: false);
            }

            return _aiCombatPolicy.ShouldRetreat(defender, planet, defenderPower, attackerPower)
                && TryRetreatFleets(defenderFleets, attackerFleets, ignoreGravityWell: false);
        }

        /// <summary>
        /// Reports whether a fleet can withdraw from its opponent.
        /// </summary>
        /// <param name="fleets">The fleets requesting withdrawal.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <returns>True when no opposing gravity well prevents withdrawal.</returns>
        private static bool CanRetreatFleets(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents
        )
        {
            return fleets?.Count > 0 && !IsRetreatBlockedByGravityWell(fleets, opponents);
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
            return fleetPlanet != null
                && opponents?.Any(opponent =>
                    opponent?.GetParentOfType<Planet>() == fleetPlanet
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
        /// Determines whether another combat round cannot change the encounter.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attackerFleets">Attacking fleets after the round.</param>
        /// <param name="defenderFleets">Defending fleets after the round.</param>
        /// <param name="combatResult">Result of the latest combat round.</param>
        /// <returns>True when neither side can inflict damage or the round changed no state.</returns>
        private bool IsSpaceCombatStalemated(
            SpaceCombatDecision decision,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            SpaceCombatResult combatResult
        )
        {
            Planet planet = combatResult?.Planet;
            return !HasOperationalSpaceWeapons(
                    attackerFleets,
                    planet,
                    decision.AttackerOwnerInstanceID
                )
                    && !HasOperationalSpaceWeapons(
                        defenderFleets,
                        planet,
                        decision.DefenderOwnerInstanceID
                    )
                || !DidCombatChangeState(combatResult);
        }

        /// <summary>
        /// Determines whether a combat round changed units or produced a winner.
        /// </summary>
        /// <param name="combatResult">Combat round to inspect.</param>
        /// <returns>True when the round changed hull, fighter counts, or winner state.</returns>
        private bool DidCombatChangeState(SpaceCombatResult combatResult)
        {
            if (combatResult == null)
                return false;

            return _battleTacticalStateChanged
                || combatResult.Winner != CombatSide.Draw
                || combatResult.ShipDamage.Any(damage => damage.HullBefore != damage.HullAfter)
                || combatResult.FighterLosses.Any(loss => loss.SquadsBefore != loss.SquadsAfter);
        }

        /// <summary>
        /// Determines whether a fleet has an active armed ship or starfighter group.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when an active space unit can attack.</returns>
        private static bool HasOperationalSpaceWeapons(Fleet fleet)
        {
            if (fleet == null)
                return false;

            return GetActiveCapitalShips(fleet).Any(IsArmedCapitalShip)
                || GetActiveStarfighters(fleet).Any(IsArmedStarfighter);
        }

        /// <summary>
        /// Returns whether an owner has operational weapons across its fleets or planet.
        /// </summary>
        /// <param name="fleets">The owner's participating fleets.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="ownerInstanceId">The owner whose weapons are being inspected.</param>
        /// <returns>True when an active unit can attack.</returns>
        private static bool HasOperationalSpaceWeapons(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId
        )
        {
            return fleets?.Any(HasOperationalSpaceWeapons) == true
                || GetActivePlanetStarfighters(planet, ownerInstanceId).Any(IsArmedStarfighter);
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
        /// Calculates combined combat value for fleets and their deployed planetary starfighters.
        /// </summary>
        /// <param name="fleets">The participating fleets, when present.</param>
        /// <param name="planetaryStarfighters">The deployed starfighters to include.</param>
        /// <returns>The combined current combat value.</returns>
        private static int GetCombatValue(
            IReadOnlyList<Fleet> fleets,
            IEnumerable<Starfighter> planetaryStarfighters
        )
        {
            int combatValue = fleets?.Sum(fleet => fleet?.GetCombatValue() ?? 0) ?? 0;
            foreach (Starfighter fighter in planetaryStarfighters)
            {
                int weaponStrength = fighter.LaserCannon + fighter.IonCannon + fighter.Torpedoes;
                combatValue +=
                    fighter.MaxSquadronSize > 0
                        ? weaponStrength * fighter.CurrentSquadronSize / fighter.MaxSquadronSize
                        : weaponStrength;
            }

            return combatValue;
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
        /// Determines whether a capital ship has operational space-combat weapons.
        /// </summary>
        /// <param name="ship">Capital ship to inspect.</param>
        /// <returns>True when the ship has positive weapon strength.</returns>
        private static bool IsArmedCapitalShip(CapitalShip ship)
        {
            return ship.GetPrimaryWeaponStrength() > 0;
        }

        /// <summary>
        /// Determines whether a starfighter group has operational weapons.
        /// </summary>
        /// <param name="starfighter">Starfighter group to inspect.</param>
        /// <returns>True when the group has positive weapon strength.</returns>
        private static bool IsArmedStarfighter(Starfighter starfighter)
        {
            return starfighter.LaserCannon + starfighter.IonCannon + starfighter.Torpedoes > 0;
        }

        /// <summary>
        /// Resolves one space-combat round and applies it to the game state.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attackerFleets">Attacking fleets.</param>
        /// <param name="defenderFleets">Defending fleets.</param>
        /// <param name="rng">Random-number provider for the round.</param>
        /// <returns>The applied round result, or null when the encounter is no longer valid.</returns>
        private SpaceCombatResult ResolveCombatRound(
            SpaceCombatDecision decision,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            IRandomNumberProvider rng
        )
        {
            Planet planet = ResolveCombatPlanet(decision);
            if (planet == null)
            {
                GameLogger.Warning("ResolveCombatRound: the combat planet no longer exists.");
                return null;
            }

            SpaceCombatResult result = ResolveSpace(
                attackerFleets,
                defenderFleets,
                decision.AttackerOwnerInstanceID,
                decision.DefenderOwnerInstanceID,
                planet,
                rng,
                _game.CurrentTick,
                _game.Config.Combat.SpaceCombat
            );
            result.Events = ApplyCombatResult(result, attackerFleets, defenderFleets);

            GameLogger.Log(
                $"Combat at {planet.GetDisplayName()}: "
                    + $"{decision.AttackerOwnerInstanceID} vs "
                    + $"{decision.DefenderOwnerInstanceID} — "
                    + $"Winner: {result.Winner}"
            );

            return result;
        }

        /// <summary>
        /// Placeholder for interactive/manual combat resolution.
        /// </summary>
        private void RunManualCombat() { }

        /// <summary>
        /// 7-phase space combat pipeline: snapshot -> composition -> weapon fire -> fighter
        /// engagement -> result. Shield absorption and hull damage happen inside weapon fire.
        /// </summary>
        /// <param name="attackerFleets">The attacking fleets.</param>
        /// <param name="defenderFleets">The defending fleets.</param>
        /// <param name="attackerOwnerInstanceId">The attacking owner identifier.</param>
        /// <param name="defenderOwnerInstanceId">The defending owner identifier.</param>
        /// <param name="planet">Planet where combat occurs.</param>
        /// <param name="rng">Random-number provider for damage variance.</param>
        /// <param name="tick">Current game tick (recorded on the result).</param>
        /// <param name="config">Combat configuration supplying damage/variance tuning values.</param>
        /// <returns>The combat result with winner, per-ship damage, and fighter losses.</returns>
        private SpaceCombatResult ResolveSpace(
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            string attackerOwnerInstanceId,
            string defenderOwnerInstanceId,
            Planet planet,
            IRandomNumberProvider rng,
            int tick,
            GameConfig.SpaceCombatConfig config
        )
        {
            _battleTacticalStateChanged = false;
            (List<ShipSnap> atkShips, List<FighterSnap> atkFighters) = SnapshotForce(
                attackerFleets,
                planet,
                attackerOwnerInstanceId,
                config
            );
            (List<ShipSnap> defShips, List<FighterSnap> defFighters) = SnapshotForce(
                defenderFleets,
                planet,
                defenderOwnerInstanceId,
                config
            );

            RechargeShields(atkShips);
            RechargeShields(defShips);

            bool anyArmed =
                HasOperationalSpaceWeapons(attackerFleets, planet, attackerOwnerInstanceId)
                || HasOperationalSpaceWeapons(defenderFleets, planet, defenderOwnerInstanceId);

            if (anyArmed)
            {
                PhaseWeaponFire(atkShips, defShips, defFighters, rng, config);
                PhaseWeaponFire(defShips, atkShips, atkFighters, rng, config);
                PhaseFighterEngage(atkFighters, defFighters, atkShips, defShips, rng, config);
            }

            StoreShieldStrengths(atkShips);
            StoreShieldStrengths(defShips);

            return BuildSpaceResult(
                GetRepresentativeFleet(attackerFleets),
                GetRepresentativeFleet(defenderFleets),
                attackerOwnerInstanceId,
                defenderOwnerInstanceId,
                planet,
                atkShips,
                defShips,
                atkFighters,
                defFighters,
                tick
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
        /// Builds mutable per-battle snapshots for one side's fleet and planetary starfighters.
        /// </summary>
        /// <param name="fleets">Fleets to snapshot.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The side's owner identifier.</param>
        /// <param name="config">Combat configuration supplying fighter durability.</param>
        /// <returns>Ship and fighter snapshots for the represented side.</returns>
        private (List<ShipSnap> ships, List<FighterSnap> fighters) SnapshotForce(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId,
            GameConfig.SpaceCombatConfig config
        )
        {
            List<ShipSnap> ships = new List<ShipSnap>();

            foreach (
                CapitalShip ship in (fleets ?? Array.Empty<Fleet>()).SelectMany(
                    GetActiveCapitalShips
                )
            )
            {
                int shieldMax = Math.Max(ship.MaxShieldStrength, 0);
                float shieldCurrent = shieldMax;
                if (_battleShieldStrengths.TryGetValue(ship, out float storedShieldStrength))
                {
                    shieldCurrent = Math.Min(Math.Max(storedShieldStrength, 0), shieldMax);
                }

                float hullCurrent = ship.CurrentHullStrength;
                if (_battleHullStrengths.TryGetValue(ship, out float storedHullStrength))
                {
                    hullCurrent = Math.Min(Math.Max(storedHullStrength, 0), ship.MaxHullStrength);
                }

                ships.Add(
                    new ShipSnap
                    {
                        Ship = ship,
                        HullInitial = ship.CurrentHullStrength,
                        HullBeforeRound = hullCurrent,
                        HullCurrent = hullCurrent,
                        HullMax = ship.MaxHullStrength,
                        ShieldInitial = shieldCurrent,
                        ShieldCurrent = shieldCurrent,
                        ShieldMax = shieldMax,
                        WeaponNibble = 15,
                        Alive = true,
                    }
                );
            }

            List<FighterSnap> fighters = ships
                .SelectMany(ship => ship.Ship.GetChildren<Starfighter>())
                .Concat(GetActivePlanetStarfighters(planet, ownerInstanceId))
                .Where(IsActiveStarfighter)
                .Select(fighter => new FighterSnap
                {
                    Fighter = fighter,
                    InitialSquadronSize = fighter.CurrentSquadronSize,
                    CurrentSquadronSize = fighter.CurrentSquadronSize,
                    ShieldCurrent = fighter.ShieldStrength,
                    DurabilityPerFighter = config.FighterTacticalDurability,
                    DurabilityCurrent =
                        fighter.CurrentSquadronSize * config.FighterTacticalDurability,
                })
                .ToList();

            return (ships, fighters);
        }

        /// <summary>
        /// Recharges surviving capital-ship shields for the next combat round.
        /// </summary>
        /// <param name="ships">The capital-ship snapshots to recharge.</param>
        private static void RechargeShields(List<ShipSnap> ships)
        {
            foreach (ShipSnap ship in ships.Where(ship => ship.Alive))
            {
                if (ship.HullMax <= 0)
                    continue;

                float effectiveRechargeRate =
                    Math.Max(ship.Ship.ShieldRechargeRate, 0)
                    * Math.Max(ship.HullCurrent, 0)
                    / ship.HullMax;
                ship.ShieldCurrent = Math.Min(
                    ship.ShieldMax,
                    ship.ShieldCurrent + effectiveRechargeRate
                );
            }
        }

        /// <summary>
        /// Preserves capital-ship hull and shield state for the next combat round.
        /// </summary>
        /// <param name="ships">The capital-ship snapshots to preserve.</param>
        private void StoreShieldStrengths(List<ShipSnap> ships)
        {
            foreach (ShipSnap ship in ships)
            {
                _battleTacticalStateChanged |=
                    ship.HullCurrent != ship.HullBeforeRound
                    || ship.ShieldCurrent != ship.ShieldInitial;
                _battleHullStrengths[ship.Ship] = ship.HullCurrent;
                _battleShieldStrengths[ship.Ship] = ship.ShieldCurrent;
            }
        }

        /// <summary>
        /// One side fires all primary weapon arcs at the other. Total firepower is scaled by
        /// each ship's weapon nibble, divided evenly across alive targets, and applied with
        /// shield absorption and configured damage variance.
        /// </summary>
        /// <param name="firing">Firing side's ship snapshots.</param>
        /// <param name="shipTargets">Target side's ship snapshots.</param>
        /// <param name="fighterTargets">Target side's fighter snapshots.</param>
        /// <param name="rng">Random-number provider for variance.</param>
        /// <param name="config">Combat configuration supplying damage variance.</param>
        private static void PhaseWeaponFire(
            List<ShipSnap> firing,
            List<ShipSnap> shipTargets,
            List<FighterSnap> fighterTargets,
            IRandomNumberProvider rng,
            GameConfig.SpaceCombatConfig config
        )
        {
            List<ShipSnap> aliveShips = shipTargets.Where(target => target.Alive).ToList();
            List<FighterSnap> aliveFighters = fighterTargets.Where(target => target.Alive).ToList();

            if (aliveShips.Count == 0 && aliveFighters.Count == 0)
                return;

            int totalFire = CalculateTotalFirepower(firing);
            if (totalFire == 0)
                return;

            int firePerTarget = totalFire / (aliveShips.Count + aliveFighters.Count);
            foreach (ShipSnap target in aliveShips)
            {
                ApplyWeaponDamage(target, firePerTarget, rng, config);
            }

            foreach (FighterSnap target in aliveFighters)
                ApplyWeaponDamage(target, firePerTarget, rng, config);
        }

        /// <summary>
        /// Sums primary weapon arc values across all alive ships, scaled by each ship's
        /// weapon nibble (0-15).
        /// </summary>
        /// <param name="ships">Ship snapshots with alive/weapon-nibble state.</param>
        /// <returns>Total firepower for the side this tick.</returns>
        private static int CalculateTotalFirepower(List<ShipSnap> ships)
        {
            int totalFire = 0;
            for (int i = 0; i < ships.Count; i++)
            {
                if (!ships[i].Alive)
                    continue;

                CapitalShip ship = ships[i].Ship;
                int raw = ship.GetPrimaryWeaponStrength();

                totalFire += raw * ships[i].WeaponNibble / 15;
            }
            return totalFire;
        }

        /// <summary>
        /// Applies weapon damage to a single target with configured variance and shield absorption.
        /// </summary>
        /// <param name="target">Target ship snapshot (mutated).</param>
        /// <param name="baseDamage">Pre-variance damage to apply.</param>
        /// <param name="rng">Random-number provider for variance.</param>
        /// <param name="config">Combat configuration supplying variance percentage.</param>
        private static void ApplyWeaponDamage(
            ShipSnap target,
            int baseDamage,
            IRandomNumberProvider rng,
            GameConfig.SpaceCombatConfig config
        )
        {
            int damage = CalculateWeaponDamage(baseDamage, rng, config);
            ApplyDamage(target, damage);
        }

        /// <summary>
        /// Applies damage to a capital ship's shields before its hull.
        /// </summary>
        /// <param name="target">The capital-ship snapshot to damage.</param>
        /// <param name="damage">The non-negative damage to apply.</param>
        private static void ApplyDamage(ShipSnap target, int damage)
        {
            float shieldDamage = Math.Min(target.ShieldCurrent, damage);
            target.ShieldCurrent -= shieldDamage;
            float hullDamage = damage - shieldDamage;

            target.HullCurrent = Math.Max(target.HullCurrent - hullDamage, 0);
            if (target.HullCurrent <= 0)
                target.Alive = false;
        }

        /// <summary>
        /// Applies one varied weapon strike to a fighter snapshot's shields and durability.
        /// </summary>
        /// <param name="target">The fighter snapshot to mutate.</param>
        /// <param name="baseDamage">The base damage before variance.</param>
        /// <param name="rng">The random number provider for variance.</param>
        /// <param name="config">The combat damage configuration.</param>
        private static void ApplyWeaponDamage(
            FighterSnap target,
            int baseDamage,
            IRandomNumberProvider rng,
            GameConfig.SpaceCombatConfig config
        )
        {
            int damage = CalculateWeaponDamage(baseDamage, rng, config);
            int shieldDamage = Math.Min(target.ShieldCurrent, damage);
            target.ShieldCurrent -= shieldDamage;
            target.DurabilityCurrent = Math.Max(
                target.DurabilityCurrent - (damage - shieldDamage),
                0
            );
            target.CurrentSquadronSize =
                target.DurabilityCurrent == 0
                    ? 0
                    : Math.Max(target.DurabilityCurrent / target.DurabilityPerFighter, 1);
        }

        /// <summary>
        /// Applies configured random variance to a base weapon damage value.
        /// </summary>
        /// <param name="baseDamage">The unmodified weapon damage.</param>
        /// <param name="rng">The random number provider for variance.</param>
        /// <param name="config">The combat damage configuration.</param>
        /// <returns>The non-negative varied damage value.</returns>
        private static int CalculateWeaponDamage(
            int baseDamage,
            IRandomNumberProvider rng,
            GameConfig.SpaceCombatConfig config
        )
        {
            if (baseDamage == 0)
                return 0;

            double roll = rng.NextDouble();
            int variance = (int)(
                (double)baseDamage * config.WeaponDamageVariancePercent * (roll * 2.0 - 1.0) / 100.0
            );
            return Math.Max(baseDamage + variance, 0);
        }

        /// <summary>
        /// Fighter phase: each side's fighters attack enemy capital ships, then opposing
        /// squadrons dogfight each other.
        /// </summary>
        /// <param name="atkFighters">Attacker fighter snapshots (mutated).</param>
        /// <param name="defFighters">Defender fighter snapshots (mutated).</param>
        /// <param name="atkShips">Attacker ship snapshots (targets for defender fighters).</param>
        /// <param name="defShips">Defender ship snapshots (targets for attacker fighters).</param>
        /// <param name="rng">Random-number provider.</param>
        /// <param name="config">Combat configuration supplying damage/loss tuning.</param>
        private static void PhaseFighterEngage(
            List<FighterSnap> atkFighters,
            List<FighterSnap> defFighters,
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            IRandomNumberProvider rng,
            GameConfig.SpaceCombatConfig config
        )
        {
            FightersAttackShips(atkFighters, defShips, rng, config);
            FightersAttackShips(defFighters, atkShips, rng, config);

            int atkTotal = atkFighters.Sum(fighter => fighter.CurrentSquadronSize);
            int defTotal = defFighters.Sum(fighter => fighter.CurrentSquadronSize);

            if (atkTotal == 0 || defTotal == 0)
                return;

            double rollAtk = rng.NextDouble();
            double rollDef = rng.NextDouble();

            double atkHitRate = (double)defTotal / (atkTotal + defTotal);
            double defHitRate = (double)atkTotal / (atkTotal + defTotal);

            double lossRate = config.FighterDogfightLossRatePercent / 100.0;
            int atkLosses = Math.Min((int)(atkTotal * atkHitRate * lossRate * rollAtk), atkTotal);
            int defLosses = Math.Min((int)(defTotal * defHitRate * lossRate * rollDef), defTotal);

            ApplyFighterLosses(atkFighters, atkLosses);
            ApplyFighterLosses(defFighters, defLosses);
        }

        /// <summary>
        /// Each fighter squadron picks a random alive enemy capital ship and attacks it
        /// with total squadron firepower times squadron size, with configured damage spread.
        /// </summary>
        /// <param name="squadrons">Fighter snapshots for the attacking side.</param>
        /// <param name="enemyShips">Enemy ship snapshots to attack (mutated).</param>
        /// <param name="rng">Random-number provider.</param>
        /// <param name="config">Combat configuration supplying damage range.</param>
        private static void FightersAttackShips(
            List<FighterSnap> squadrons,
            List<ShipSnap> enemyShips,
            IRandomNumberProvider rng,
            GameConfig.SpaceCombatConfig config
        )
        {
            List<int> aliveTargets = enemyShips
                .Select((s, idx) => new { s, idx })
                .Where(x => x.s.Alive)
                .Select(x => x.idx)
                .ToList();

            if (aliveTargets.Count == 0)
                return;

            for (int sqIdx = 0; sqIdx < squadrons.Count; sqIdx++)
            {
                FighterSnap squadron = squadrons[sqIdx];
                if (!squadron.Alive)
                    continue;

                Starfighter fighter = squadron.Fighter;
                int totalAttack =
                    (fighter.LaserCannon + fighter.IonCannon + fighter.Torpedoes)
                    * squadron.CurrentSquadronSize;

                if (totalAttack == 0)
                    continue;

                int targetIdx = aliveTargets[(int)(rng.NextDouble() * aliveTargets.Count)];

                double roll = rng.NextDouble();
                double basePct = config.FighterDamageBasePercent / 100.0;
                double spreadPct = config.FighterDamageSpreadPercent / 100.0;
                int damage = (int)(totalAttack * (basePct + spreadPct * roll));

                ApplyDamage(enemyShips[targetIdx], damage);
                if (enemyShips[targetIdx].HullCurrent <= 0)
                {
                    aliveTargets.Remove(targetIdx);
                    if (aliveTargets.Count == 0)
                        break;
                }
            }
        }

        /// <summary>
        /// Applies fighter losses across the affected squadrons.
        /// </summary>
        /// <param name="squadrons">Fighter snapshots to reduce (mutated).</param>
        /// <param name="totalLosses">Total number of fighters to remove.</param>
        private static void ApplyFighterLosses(List<FighterSnap> squadrons, int totalLosses)
        {
            if (totalLosses == 0)
                return;

            int remaining = totalLosses;
            int total = squadrons.Sum(fighter => fighter.CurrentSquadronSize);

            if (total == 0)
                return;

            for (int i = 0; i < squadrons.Count && remaining > 0; i++)
            {
                if (!squadrons[i].Alive)
                    continue;

                int loss = Math.Min(
                    squadrons[i].CurrentSquadronSize * totalLosses / total,
                    remaining
                );
                squadrons[i].CurrentSquadronSize = Math.Max(
                    squadrons[i].CurrentSquadronSize - loss,
                    0
                );
                remaining -= loss;
            }

            for (int i = 0; i < squadrons.Count && remaining > 0; i++)
            {
                if (squadrons[i].Alive)
                {
                    squadrons[i].CurrentSquadronSize--;
                    remaining--;
                }
            }

            foreach (FighterSnap squadron in squadrons)
            {
                squadron.DurabilityCurrent =
                    squadron.CurrentSquadronSize * squadron.DurabilityPerFighter;
            }
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
                AttackerOutcome = GetCombatSideRoundOutcome(atkShips, atkFighters),
                DefenderOutcome = GetCombatSideRoundOutcome(defShips, defFighters),
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
        /// Resolves a combat side's outcome from post-round unit snapshots.
        /// </summary>
        /// <param name="ships">The side's post-round ship snapshots.</param>
        /// <param name="fighters">The side's post-round fighter snapshots.</param>
        /// <returns>The side's round outcome.</returns>
        private static SpaceCombatSideOutcome GetCombatSideRoundOutcome(
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

            float survivingHullStrength = Math.Max(ship.HullCurrent, 1);
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
            List<GameResult> events = ApplyShipDamage(shipDamage);
            ApplyFighterSquadronLosses(fighterLosses);

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

        /// <summary>Mutable per-battle snapshot of one capital ship.</summary>
        private class ShipSnap
        {
            public CapitalShip Ship;
            public int HullInitial;
            public float HullBeforeRound;
            public float HullCurrent;
            public int HullMax;
            public float ShieldInitial;
            public float ShieldCurrent;
            public int ShieldMax;

            /// <summary>Weapon recharge allocation (0-15).</summary>
            public int WeaponNibble;

            public bool Alive;
        }

        private class FighterSnap
        {
            public Starfighter Fighter;
            public int InitialSquadronSize;
            public int CurrentSquadronSize;
            public int ShieldCurrent;
            public int DurabilityPerFighter;
            public int DurabilityCurrent;

            public bool Alive => CurrentSquadronSize > 0;
        }
    }
}
