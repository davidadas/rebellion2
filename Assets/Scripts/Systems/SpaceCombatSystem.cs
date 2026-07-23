using System;
using System.Collections.Generic;
using System.Linq;
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
        public string AttackerFleetInstanceID { get; set; }
        public string DefenderFleetInstanceID { get; set; }
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
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;
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
        /// <param name="provider">Random-number provider used by combat resolution.</param>
        /// <param name="movement">Movement system used for retreats and evacuation.</param>
        public SpaceCombatSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement
        )
        {
            _game = game;
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
                if (!string.IsNullOrEmpty(decision.AttackerFleetInstanceID))
                    resolvedFleetIds.Add(decision.AttackerFleetInstanceID);
                if (!string.IsNullOrEmpty(decision.DefenderFleetInstanceID))
                    resolvedFleetIds.Add(decision.DefenderFleetInstanceID);
            }

            return true;
        }

        /// <summary>
        /// Checks whether both fleets in the encounter still occupy a contested planet.
        /// </summary>
        /// <param name="decision">The combat decision to evaluate.</param>
        /// <returns>True when both fleets still contest the same planet.</returns>
        private bool IsEncounterStillContested(SpaceCombatDecision decision)
        {
            return AreForcesContestingPlanet(decision);
        }

        /// <summary>
        /// Returns whether both fleets belong to AI-controlled factions.
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
            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );

            return new PendingCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID,
                Planet = ResolveCombatPlanet(decision),
                AttackerCanRetreat = CanRetreatFleet(attacker, defender),
                DefenderCanRetreat = CanRetreatFleet(defender, attacker),
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

            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                _pendingDecision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                _pendingDecision.DefenderFleetInstanceID
            );

            string retreatingFleetInstanceId = null;
            if (_pendingDecision.AttackerOwnerInstanceID == retreatingFactionInstanceId)
                retreatingFleetInstanceId = attacker?.GetInstanceID();
            else if (_pendingDecision.DefenderOwnerInstanceID == retreatingFactionInstanceId)
                retreatingFleetInstanceId = defender?.GetInstanceID();

            if (
                string.IsNullOrEmpty(retreatingFleetInstanceId)
                || !TryResolveRetreat(
                    _pendingDecision,
                    retreatingFleetInstanceId,
                    out List<GameResult> results
                )
            )
                return null;

            _pendingDecision = null;
            return results;
        }

        /// <summary>
        /// Attempts to resolve a pending combat decision by withdrawing one fleet.
        /// </summary>
        /// <param name="decision">The pending combat decision.</param>
        /// <param name="retreatingFleetInstanceId">The fleet requested to withdraw.</param>
        /// <param name="results">Receives the generated combat result.</param>
        /// <returns>True when the fleet withdrew successfully.</returns>
        private bool TryResolveRetreat(
            SpaceCombatDecision decision,
            string retreatingFleetInstanceId,
            out List<GameResult> results
        )
        {
            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );
            Planet planet = ResolveCombatPlanet(decision);
            results = new List<GameResult>();

            if (!TryRetreatFleet(decision, retreatingFleetInstanceId))
                return false;

            results.Add(
                BuildRetreatResult(decision, retreatingFleetInstanceId, attacker, defender, planet)
            );
            return true;
        }

        /// <summary>
        /// Builds the combat result emitted after a successful fleet withdrawal.
        /// </summary>
        /// <param name="decision">The resolved combat decision.</param>
        /// <param name="retreatingFleetInstanceId">The fleet that withdrew.</param>
        /// <param name="attacker">The attacking fleet.</param>
        /// <param name="defender">The defending fleet.</param>
        /// <param name="planet">The combat location.</param>
        /// <returns>The withdrawal combat result.</returns>
        private SpaceCombatResult BuildRetreatResult(
            SpaceCombatDecision decision,
            string retreatingFleetInstanceId,
            Fleet attacker,
            Fleet defender,
            Planet planet
        )
        {
            bool attackerRetreated = retreatingFleetInstanceId == decision.AttackerFleetInstanceID;
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID,
                Planet = planet,
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
                attacker,
                planet,
                decision.AttackerOwnerInstanceID,
                _game.Config.Combat.SpaceCombat
            );
            (List<ShipSnap> defenderShips, List<FighterSnap> defenderFighters) = SnapshotForce(
                defender,
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
                    out Fleet attacker,
                    out Fleet defender
                )
            )
                return false;

            if (attacker != null)
                attacker.IsInCombat = true;
            if (defender != null)
                defender.IsInCombat = true;

            decision = new SpaceCombatDecision
            {
                AttackerFleetInstanceID = attacker?.GetInstanceID(),
                DefenderFleetInstanceID = defender?.GetInstanceID(),
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
        /// <param name="attacker">The attacking fleet, when that side has one.</param>
        /// <param name="defender">The defending fleet, when that side has one.</param>
        /// <returns>True if hostile space forces were found.</returns>
        private bool TryFindContestedForces(
            HashSet<string> excludedFleetIds,
            out Planet contestedPlanet,
            out string attackerOwnerInstanceId,
            out string defenderOwnerInstanceId,
            out Fleet attacker,
            out Fleet defender
        )
        {
            contestedPlanet = null;
            attackerOwnerInstanceId = null;
            defenderOwnerInstanceId = null;
            attacker = null;
            defender = null;

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                List<Fleet> fleets = planet
                    .GetChildren<Fleet>(
                        fleet =>
                            !fleet.IsInCombat
                            && !excludedFleetIds.Contains(fleet.GetInstanceID())
                            && fleet.Movement == null
                            && HasActiveSpaceUnits(fleet),
                        recurse: false
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
                Fleet firstFleet = fleets.FirstOrDefault(fleet =>
                    fleet.GetOwnerInstanceID() == firstOwnerInstanceId
                );
                Fleet secondFleet = fleets.FirstOrDefault(fleet =>
                    fleet.GetOwnerInstanceID() == secondOwnerInstanceId
                );

                attackerOwnerInstanceId = firstOwnerInstanceId;
                defenderOwnerInstanceId = secondOwnerInstanceId;
                attacker = firstFleet;
                defender = secondFleet;

                if (attacker == null && defender != null)
                {
                    (attackerOwnerInstanceId, defenderOwnerInstanceId) = (
                        defenderOwnerInstanceId,
                        attackerOwnerInstanceId
                    );
                    (attacker, defender) = (defender, attacker);
                }

                contestedPlanet = planet;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a pending combat encounter. Applies damage to the game world and clears
        /// IsInCombat on both fleets regardless of outcome.
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
            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );

            try
            {
                while (AreForcesContestingPlanet(decision))
                {
                    Planet planet = ResolveCombatPlanet(decision);
                    if (
                        allowRetreatBeforeCombat
                        && TryRetreatOutmatchedFleet(decision, attacker, defender, planet)
                    )
                        break;

                    SpaceCombatResult combatResult = ResolveCombatRound(
                        decision,
                        attacker,
                        defender,
                        _provider
                    );
                    if (combatResult != null)
                    {
                        combatEncounterResult ??= CreateCombatEncounterResult(combatResult);
                        AddCombatRoundResult(combatEncounterResult, combatResult);
                    }

                    attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                        decision.AttackerFleetInstanceID
                    );
                    defender = _game.GetSceneNodeByInstanceID<Fleet>(
                        decision.DefenderFleetInstanceID
                    );

                    if (!AreForcesContestingPlanet(decision))
                        break;

                    if (IsSpaceCombatStalemated(decision, attacker, defender, combatResult))
                    {
                        SeparateStalematedFleets(attacker, defender);
                        break;
                    }
                }
            }
            finally
            {
                ClearCombatFlags(decision);
            }

            UpdateCombatEncounterResultOutcomes(combatEncounterResult);

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
        private static void UpdateCombatEncounterResultOutcomes(SpaceCombatResult result)
        {
            if (result == null)
                return;

            result.AttackerOutcome = GetCombatSideOutcome(
                result.AttackerFleet,
                result.AttackerOwnerInstanceID,
                result.Planet,
                result.AttackerOutcome
            );
            result.DefenderOutcome = GetCombatSideOutcome(
                result.DefenderFleet,
                result.DefenderOwnerInstanceID,
                result.Planet,
                result.DefenderOutcome
            );
        }

        /// <summary>
        /// Resolves a combat side's final encounter outcome.
        /// </summary>
        /// <param name="fleet">The participating fleet.</param>
        /// <param name="ownerInstanceId">The participating owner's identifier.</param>
        /// <param name="battlePlanet">The encounter location.</param>
        /// <param name="roundOutcome">The outcome recorded by the final combat round.</param>
        /// <returns>The final encounter outcome.</returns>
        private static SpaceCombatSideOutcome GetCombatSideOutcome(
            Fleet fleet,
            string ownerInstanceId,
            Planet battlePlanet,
            SpaceCombatSideOutcome roundOutcome
        )
        {
            if (roundOutcome == SpaceCombatSideOutcome.Destroyed)
                return SpaceCombatSideOutcome.Destroyed;

            if (HasActiveSpaceUnits(fleet, battlePlanet, ownerInstanceId))
                return SpaceCombatSideOutcome.Active;

            if (fleet?.Movement != null)
                return SpaceCombatSideOutcome.Withdrawn;

            Planet currentPlanet = fleet?.GetParentOfType<Planet>();
            if (currentPlanet == null)
                return SpaceCombatSideOutcome.Destroyed;

            if (battlePlanet != null && currentPlanet != battlePlanet)
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
        /// Separates fleets that cannot produce a further combat outcome.
        /// </summary>
        /// <param name="attacker">Attacking fleet.</param>
        /// <param name="defender">Defending fleet.</param>
        private void SeparateStalematedFleets(Fleet attacker, Fleet defender)
        {
            bool attackerRetreated = TryRetreatFleet(attacker, defender, ignoreGravityWell: true);
            bool defenderRetreated = TryRetreatFleet(defender, attacker, ignoreGravityWell: true);

            if (!AreFleetsContestingPlanet(attacker, defender))
                return;

            if (!attackerRetreated)
                RemoveFleetUnableToRetreat(attacker);
            if (!defenderRetreated)
                RemoveFleetUnableToRetreat(defender);
        }

        /// <summary>
        /// Removes a stalemated fleet that cannot leave the contested planet.
        /// </summary>
        /// <param name="fleet">Fleet to remove.</param>
        private void RemoveFleetUnableToRetreat(Fleet fleet)
        {
            if (fleet == null)
                return;

            _game.DetachNode(fleet);
            GameLogger.Log($"Fleet removed after stalemated combat: {fleet.GetDisplayName()}");
        }

        /// <summary>
        /// Clears combat state from fleets that remain after an encounter.
        /// </summary>
        /// <param name="decision">Encounter identifying the affected fleets.</param>
        private void ClearCombatFlags(SpaceCombatDecision decision)
        {
            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );
            if (attacker != null)
                attacker.IsInCombat = false;
            if (defender != null)
                defender.IsInCombat = false;
        }

        /// <summary>
        /// Attempts to retreat the weaker fleet before combat begins.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attacker">Attacking fleet.</param>
        /// <param name="defender">Defending fleet.</param>
        /// <param name="planet">The combat location.</param>
        /// <returns>True when at least one fleet retreats.</returns>
        private bool TryRetreatOutmatchedFleet(
            SpaceCombatDecision decision,
            Fleet attacker,
            Fleet defender,
            Planet planet
        )
        {
            int attackerPower = GetCombatValue(
                attacker,
                GetActivePlanetStarfighters(planet, decision.AttackerOwnerInstanceID)
            );
            int defenderPower = GetCombatValue(
                defender,
                GetActivePlanetStarfighters(planet, decision.DefenderOwnerInstanceID)
            );

            if (attackerPower == defenderPower)
            {
                bool attackerRetreated = TryRetreatFleet(
                    attacker,
                    defender,
                    ignoreGravityWell: false
                );
                bool defenderRetreated = TryRetreatFleet(
                    defender,
                    attacker,
                    ignoreGravityWell: false
                );
                return attackerRetreated || defenderRetreated;
            }

            if (attackerPower < defenderPower)
                return TryRetreatFleet(attacker, defender, ignoreGravityWell: false);

            return TryRetreatFleet(defender, attacker, ignoreGravityWell: false);
        }

        /// <summary>
        /// Reports whether a fleet can withdraw from its opponent.
        /// </summary>
        /// <param name="fleet">The fleet requesting withdrawal.</param>
        /// <param name="opponent">The opposing fleet.</param>
        /// <returns>True when no opposing gravity well prevents withdrawal.</returns>
        private static bool CanRetreatFleet(Fleet fleet, Fleet opponent)
        {
            return fleet != null && !IsRetreatBlockedByGravityWell(fleet, opponent);
        }

        /// <summary>
        /// Withdraws one side from an encounter and clears its combat flags.
        /// </summary>
        /// <param name="decision">The encounter context.</param>
        /// <param name="retreatingFleetInstanceId">The withdrawing fleet identifier.</param>
        /// <returns>True when the fleet withdrew successfully.</returns>
        private bool TryRetreatFleet(SpaceCombatDecision decision, string retreatingFleetInstanceId)
        {
            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );

            bool retreated =
                retreatingFleetInstanceId == decision.AttackerFleetInstanceID
                    ? TryRetreatFleet(attacker, defender, ignoreGravityWell: false)
                    : retreatingFleetInstanceId == decision.DefenderFleetInstanceID
                        && TryRetreatFleet(defender, attacker, ignoreGravityWell: false);

            if (retreated)
                ClearCombatFlags(decision);

            return retreated;
        }

        /// <summary>
        /// Attempts to evacuate a fleet to the nearest friendly planet.
        /// </summary>
        /// <param name="fleet">Fleet attempting to retreat.</param>
        /// <param name="opponent">Opposing fleet that may block retreat.</param>
        /// <param name="ignoreGravityWell">Whether gravity-well interdiction is ignored.</param>
        /// <returns>True when the fleet leaves or begins movement away from the planet.</returns>
        private bool TryRetreatFleet(Fleet fleet, Fleet opponent, bool ignoreGravityWell)
        {
            if (fleet == null)
                return false;

            if (!ignoreGravityWell && IsRetreatBlockedByGravityWell(fleet, opponent))
                return false;

            Planet originalPlanet = fleet.GetParentOfType<Planet>();
            _movement.EvacuateToNearestFriendlyPlanet(fleet);
            return fleet.Movement != null || fleet.GetParentOfType<Planet>() != originalPlanet;
        }

        /// <summary>
        /// Determines whether an opposing gravity-well ship prevents retreat.
        /// </summary>
        /// <param name="fleet">Fleet attempting to retreat.</param>
        /// <param name="opponent">Opposing fleet.</param>
        /// <returns>True when an active opposing ship projects a gravity well at the planet.</returns>
        private static bool IsRetreatBlockedByGravityWell(Fleet fleet, Fleet opponent)
        {
            if (fleet == null || opponent == null)
                return false;

            Planet fleetPlanet = fleet.GetParentOfType<Planet>();
            if (fleetPlanet == null || fleetPlanet != opponent.GetParentOfType<Planet>())
                return false;

            return GetActiveCapitalShips(opponent).Any(ship => ship.HasGravityWell);
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

            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            planet = attacker?.GetParentOfType<Planet>();
            if (planet != null)
                return planet;

            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );
            return defender?.GetParentOfType<Planet>();
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

            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );

            return decision.AttackerOwnerInstanceID != decision.DefenderOwnerInstanceID
                && HasActiveSpaceUnits(attacker, planet, decision.AttackerOwnerInstanceID)
                && HasActiveSpaceUnits(defender, planet, decision.DefenderOwnerInstanceID);
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
        /// Returns whether an owner has active fleet or planetary space units at a planet.
        /// </summary>
        /// <param name="fleet">The owner's participating fleet, when present.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="ownerInstanceId">The owner whose forces are being inspected.</param>
        /// <returns>True when at least one active space unit remains at the planet.</returns>
        private static bool HasActiveSpaceUnits(Fleet fleet, Planet planet, string ownerInstanceId)
        {
            bool hasActiveFleetUnits =
                fleet != null
                && fleet.Movement == null
                && fleet.GetParentOfType<Planet>() == planet
                && HasActiveSpaceUnits(fleet);

            return hasActiveFleetUnits
                || GetActivePlanetStarfighters(planet, ownerInstanceId).Any();
        }

        /// <summary>
        /// Determines whether another combat round cannot change the encounter.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attacker">Attacking fleet after the round.</param>
        /// <param name="defender">Defending fleet after the round.</param>
        /// <param name="combatResult">Result of the latest combat round.</param>
        /// <returns>True when neither side can inflict damage or the round changed no state.</returns>
        private static bool IsSpaceCombatStalemated(
            SpaceCombatDecision decision,
            Fleet attacker,
            Fleet defender,
            SpaceCombatResult combatResult
        )
        {
            Planet planet = combatResult?.Planet;
            return !HasOperationalSpaceWeapons(attacker, planet, decision.AttackerOwnerInstanceID)
                    && !HasOperationalSpaceWeapons(
                        defender,
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
        private static bool DidCombatChangeState(SpaceCombatResult combatResult)
        {
            if (combatResult == null)
                return false;

            return combatResult.Winner != CombatSide.Draw
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
        /// Returns whether an owner has operational fleet or planetary space weapons.
        /// </summary>
        /// <param name="fleet">The owner's participating fleet, when present.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="ownerInstanceId">The owner whose weapons are being inspected.</param>
        /// <returns>True when an active unit can attack.</returns>
        private static bool HasOperationalSpaceWeapons(
            Fleet fleet,
            Planet planet,
            string ownerInstanceId
        )
        {
            return HasOperationalSpaceWeapons(fleet)
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

            return fleet.CapitalShips.Where(IsActiveCapitalShip);
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
                .SelectMany(ship => ship.Starfighters)
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

            return planet.Starfighters.Where(fighter =>
                (
                    string.IsNullOrEmpty(ownerInstanceId)
                    || fighter.GetOwnerInstanceID() == ownerInstanceId
                ) && IsActiveStarfighter(fighter)
            );
        }

        /// <summary>
        /// Calculates combined combat value for a fleet and its deployed planetary starfighters.
        /// </summary>
        /// <param name="fleet">The participating fleet, when present.</param>
        /// <param name="planetaryStarfighters">The deployed starfighters to include.</param>
        /// <returns>The combined current combat value.</returns>
        private static int GetCombatValue(
            Fleet fleet,
            IEnumerable<Starfighter> planetaryStarfighters
        )
        {
            int combatValue = fleet?.GetCombatValue() ?? 0;
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
        /// <param name="attacker">Attacking fleet.</param>
        /// <param name="defender">Defending fleet.</param>
        /// <param name="rng">Random-number provider for the round.</param>
        /// <returns>The applied round result, or null when the encounter is no longer valid.</returns>
        private SpaceCombatResult ResolveCombatRound(
            SpaceCombatDecision decision,
            Fleet attacker,
            Fleet defender,
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
                attacker,
                defender,
                decision.AttackerOwnerInstanceID,
                decision.DefenderOwnerInstanceID,
                planet,
                rng,
                _game.CurrentTick,
                _game.Config.Combat.SpaceCombat
            );
            result.Events = ApplyCombatResult(result);

            GameLogger.Log(
                $"Combat at {planet.GetDisplayName()}: "
                    + $"{attacker?.GetDisplayName() ?? decision.AttackerOwnerInstanceID} vs "
                    + $"{defender?.GetDisplayName() ?? decision.DefenderOwnerInstanceID} — "
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
        /// <param name="attackerFleet">The attacking fleet.</param>
        /// <param name="defenderFleet">The defending fleet.</param>
        /// <param name="attackerOwnerInstanceId">The attacking owner identifier.</param>
        /// <param name="defenderOwnerInstanceId">The defending owner identifier.</param>
        /// <param name="planet">Planet where combat occurs.</param>
        /// <param name="rng">Random-number provider for damage variance.</param>
        /// <param name="tick">Current game tick (recorded on the result).</param>
        /// <param name="config">Combat configuration supplying damage/variance tuning values.</param>
        /// <returns>The combat result with winner, per-ship damage, and fighter losses.</returns>
        private static SpaceCombatResult ResolveSpace(
            Fleet attackerFleet,
            Fleet defenderFleet,
            string attackerOwnerInstanceId,
            string defenderOwnerInstanceId,
            Planet planet,
            IRandomNumberProvider rng,
            int tick,
            GameConfig.SpaceCombatConfig config
        )
        {
            (List<ShipSnap> atkShips, List<FighterSnap> atkFighters) = SnapshotForce(
                attackerFleet,
                planet,
                attackerOwnerInstanceId,
                config
            );
            (List<ShipSnap> defShips, List<FighterSnap> defFighters) = SnapshotForce(
                defenderFleet,
                planet,
                defenderOwnerInstanceId,
                config
            );

            bool anyArmed =
                HasOperationalSpaceWeapons(attackerFleet, planet, attackerOwnerInstanceId)
                || HasOperationalSpaceWeapons(defenderFleet, planet, defenderOwnerInstanceId);

            if (anyArmed)
            {
                PhaseWeaponFire(atkShips, defShips, defFighters, rng, config);
                PhaseWeaponFire(defShips, atkShips, atkFighters, rng, config);
                PhaseFighterEngage(atkFighters, defFighters, atkShips, defShips, rng, config);
            }

            return BuildSpaceResult(
                attackerFleet,
                defenderFleet,
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
        /// <param name="fleet">Fleet to snapshot.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The side's owner identifier.</param>
        /// <param name="config">Combat configuration supplying fighter durability.</param>
        /// <returns>Ship and fighter snapshots for the represented side.</returns>
        private static (List<ShipSnap> ships, List<FighterSnap> fighters) SnapshotForce(
            Fleet fleet,
            Planet planet,
            string ownerInstanceId,
            GameConfig.SpaceCombatConfig config
        )
        {
            List<ShipSnap> ships = new List<ShipSnap>();

            foreach (CapitalShip ship in GetActiveCapitalShips(fleet))
            {
                ships.Add(
                    new ShipSnap
                    {
                        Ship = ship,
                        HullCurrent = ship.CurrentHullStrength,
                        HullMax = ship.MaxHullStrength,
                        ShieldNibble = Math.Min(ship.ShieldRechargeRate, 15),
                        WeaponNibble = 15,
                        Alive = true,
                    }
                );
            }

            List<FighterSnap> fighters = ships
                .SelectMany(ship => ship.Ship.Starfighters)
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
        /// Shield nibble / 15 of damage is absorbed; remainder reduces hull.
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

            int absorbed = (int)(damage * target.ShieldNibble / 15.0);
            int hullDamage = Math.Max(damage - absorbed, 0);

            target.HullCurrent = Math.Max(target.HullCurrent - hullDamage, 0);
            if (target.HullCurrent == 0)
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

                enemyShips[targetIdx].HullCurrent = Math.Max(
                    enemyShips[targetIdx].HullCurrent - damage,
                    0
                );
                if (enemyShips[targetIdx].HullCurrent == 0)
                {
                    enemyShips[targetIdx].Alive = false;
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
                        .Concat(ship.Ship.GetChildren<ISceneNode>(_ => true))
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
                if (ships[i].HullCurrent < ships[i].HullMax)
                {
                    results.Add(
                        new ShipDamageResult
                        {
                            Ship = ships[i].Ship,
                            HullBefore = ships[i].HullMax,
                            HullAfter = ships[i].HullCurrent,
                        }
                    );
                }
            }
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
        /// <returns>Events generated from ship damage and destruction.</returns>
        private List<GameResult> ApplyCombatResult(SpaceCombatResult result)
        {
            List<GameResult> events = ApplyShipDamage(result.ShipDamage);
            ApplyFighterSquadronLosses(result.FighterLosses);

            if (result.AttackerFleet?.CapitalShips.Count == 0)
                RemoveFleetFromScene(result.AttackerFleet);
            if (result.DefenderFleet?.CapitalShips.Count == 0)
                RemoveFleetFromScene(result.DefenderFleet);

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
                    CapitalShipDestruction.Resolve(_game, _movement, ship);
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
                    _game.DetachNode(fighter);
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
            _game.DetachNode(fleet);
            GameLogger.Log($"Fleet destroyed: {fleet.GetDisplayName()}");
        }

        /// <summary>Mutable per-battle snapshot of one capital ship.</summary>
        private class ShipSnap
        {
            public CapitalShip Ship;
            public int HullCurrent;
            public int HullMax;

            /// <summary>Shield recharge allocation (0-15).</summary>
            public int ShieldNibble;

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
