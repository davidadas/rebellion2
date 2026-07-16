using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    public enum BombardmentType
    {
        Military,
        Civilian,
        General,
        DestroySystem,
    }

    public enum BombardmentTargetType
    {
        Regiment,
        Building,
        Headquarters,
        EnergyCapacity,
        AllocatedEnergy,
    }

    /// <summary>
    /// Captures the context of a pending combat encounter requiring player resolution.
    /// </summary>
    public class CombatDecisionContext
    {
        public string AttackerFleetInstanceID { get; set; }
        public string DefenderFleetInstanceID { get; set; }
    }

    /// <summary>
    /// Facade over the three combat resolvers (space combat, planetary assault, orbital
    /// bombardment). Also owns fleet encounter detection.
    /// </summary>
    public class CombatSystem
    {
        private readonly GameRoot _game;
        private readonly SpaceCombatResolver _spaceCombat;
        private readonly PlanetaryAssaultResolver _assault;
        private readonly OrbitalBombardmentResolver _bombardment;
        private CombatDecisionContext _pendingDecision;

        /// <summary>
        /// Whether a player-involved combat encounter is waiting for resolution.
        /// </summary>
        public bool HasPendingDecision => _pendingDecision != null;

        public CombatSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            if (movement == null)
                throw new ArgumentNullException(nameof(movement));
            if (ownership == null)
                throw new ArgumentNullException(nameof(ownership));
            _spaceCombat = new SpaceCombatResolver(game, provider, movement);
            _assault = new PlanetaryAssaultResolver(game, provider, ownership);
            _bombardment = new OrbitalBombardmentResolver(game, provider, movement, ownership);
        }

        /// <summary>
        /// Resolves all AI-vs-AI combat encounters this tick in a single pass.
        /// When a player-involved encounter is found, emits a PendingCombatResult and stops —
        /// the caller is responsible for freezing the tick until the player resolves it.
        /// </summary>
        /// <returns>Combat results generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            if (_pendingDecision != null)
                return results;

            HashSet<string> resolvedFleetIds = new HashSet<string>();

            while (TryBeginFleetCombat(resolvedFleetIds, out CombatDecisionContext decision))
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
        /// <param name="results">Output list that receives per-ship damage/destruction events.</param>
        /// <returns>True if auto-resolved; false if either side is player-controlled.</returns>
        private bool TryAutoResolveAICombat(
            CombatDecisionContext decision,
            HashSet<string> resolvedFleetIds,
            List<GameResult> results
        )
        {
            if (!BothSidesAIControlled(decision))
                return false;

            results.AddRange(_spaceCombat.ResolveAutomaticFleetEncounter(decision));

            if (IsEncounterStillContested(decision))
            {
                resolvedFleetIds.Add(decision.AttackerFleetInstanceID);
                resolvedFleetIds.Add(decision.DefenderFleetInstanceID);
            }
            return true;
        }

        /// <summary>
        /// Checks whether both fleets in the encounter still occupy a contested planet.
        /// </summary>
        /// <param name="decision">The combat decision context to evaluate.</param>
        /// <returns>True when both fleets still contest the same planet.</returns>
        private bool IsEncounterStillContested(CombatDecisionContext decision)
        {
            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );

            return AreFleetsContestingPlanet(attacker, defender);
        }

        /// <summary>
        /// Returns true when both fleets in the decision belong to AI-controlled factions.
        /// </summary>
        /// <param name="decision">The combat decision context to evaluate.</param>
        /// <returns>True if both sides are AI-controlled; false otherwise.</returns>
        private bool BothSidesAIControlled(CombatDecisionContext decision)
        {
            Faction attacker = GetFleetFaction(decision.AttackerFleetInstanceID);
            Faction defender = GetFleetFaction(decision.DefenderFleetInstanceID);
            return attacker != null
                && defender != null
                && attacker.IsAIControlled()
                && defender.IsAIControlled();
        }

        /// <summary>
        /// Looks up the controlling faction for a fleet by its instance ID.
        /// </summary>
        /// <param name="fleetInstanceId">The fleet's scene-graph instance ID.</param>
        /// <returns>The faction that owns the fleet, or null if the fleet or faction is missing.</returns>
        private Faction GetFleetFaction(string fleetInstanceId)
        {
            Fleet fleet = _game.GetSceneNodeByInstanceID<Fleet>(fleetInstanceId);
            return _game.GetFactionByOwnerInstanceID(fleet?.GetOwnerInstanceID());
        }

        /// <summary>
        /// Builds a PendingCombatResult for a player-involved encounter that must pause the tick.
        /// </summary>
        /// <param name="decision">The combat decision context to forward to the player.</param>
        /// <returns>Pending-combat event with both fleets and the current tick.</returns>
        private PendingCombatResult BuildPendingCombatResult(CombatDecisionContext decision)
        {
            return new PendingCombatResult
            {
                AttackerFleet = _game.GetSceneNodeByInstanceID<Fleet>(
                    decision.AttackerFleetInstanceID
                ),
                DefenderFleet = _game.GetSceneNodeByInstanceID<Fleet>(
                    decision.DefenderFleetInstanceID
                ),
                Tick = _game.CurrentTick,
            };
        }

        /// <summary>
        /// Resolves a pending combat encounter via the space-combat pipeline.
        /// </summary>
        /// <param name="decision">The encounter context to resolve.</param>
        /// <param name="autoResolve">True for AI auto-resolve; false for manual/interactive.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        public List<GameResult> Resolve(CombatDecisionContext decision, bool autoResolve) =>
            _spaceCombat.Resolve(decision, autoResolve);

        /// <summary>
        /// Resolves and clears the player-involved combat encounter waiting for a decision.
        /// </summary>
        /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        public List<GameResult> ResolvePendingCombat(bool autoResolve)
        {
            if (_pendingDecision == null)
                throw new InvalidOperationException("No pending combat to resolve.");

            CombatDecisionContext decision = _pendingDecision;
            List<GameResult> results = Resolve(decision, autoResolve);
            _pendingDecision = null;
            return results;
        }

        /// <summary>
        /// Runs the planetary-assault pipeline against a defending planet.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the assault (all must share a faction).</param>
        /// <param name="defendingPlanet">Planet being assaulted.</param>
        /// <returns>Assault outcome, including destroyed units and any ownership change.</returns>
        public PlanetaryAssaultResult ExecutePlanetaryAssault(
            List<Fleet> attackingFleets,
            Planet defendingPlanet
        ) => _assault.Execute(attackingFleets, defendingPlanet);

        /// <summary>
        /// Runs the 6-stage orbital bombardment pipeline against a target planet.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the bombardment (all must share a faction).</param>
        /// <param name="targetPlanet">Planet being bombarded.</param>
        /// <param name="type">Targets and consequences selected for the bombardment.</param>
        /// <returns>Bombardment outcome, including strikes and any ship/regiment/building destruction.</returns>
        public BombardmentResult ExecuteOrbitalBombardment(
            List<Fleet> attackingFleets,
            Planet targetPlanet,
            BombardmentType type
        ) => _bombardment.Execute(attackingFleets, targetPlanet, type);

        /// <summary>
        /// Detects a hostile fleet encounter while skipping any fleet IDs already handled
        /// this tick.
        /// </summary>
        /// <param name="excludedFleetIds">Fleet instance IDs to skip.</param>
        /// <param name="decision">Output: populated with the encounter context on success.</param>
        /// <returns>True if a hostile encounter was detected.</returns>
        private bool TryBeginFleetCombat(
            HashSet<string> excludedFleetIds,
            out CombatDecisionContext decision
        )
        {
            decision = null;

            if (
                !TryFindContestedFleetPair(excludedFleetIds, out Fleet attacker, out Fleet defender)
            )
                return false;

            attacker.IsInCombat = true;
            defender.IsInCombat = true;

            decision = new CombatDecisionContext
            {
                AttackerFleetInstanceID = attacker.GetInstanceID(),
                DefenderFleetInstanceID = defender.GetInstanceID(),
            };

            return true;
        }

        /// <summary>
        /// Finds the first pair of hostile fleets occupying the same planet. Skips fleets
        /// already engaged in combat or excluded by ID. Faction groups are sorted by owner ID
        /// for deterministic selection.
        /// </summary>
        /// <param name="excludedFleetIds">Fleet IDs to skip.</param>
        /// <param name="attacker">Output attacker fleet.</param>
        /// <param name="defender">Output defender fleet.</param>
        /// <returns>True if a hostile fleet pair was found.</returns>
        private bool TryFindContestedFleetPair(
            HashSet<string> excludedFleetIds,
            out Fleet attacker,
            out Fleet defender
        )
        {
            attacker = null;
            defender = null;

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                List<Fleet> fleets = planet
                    .GetChildren<Fleet>(
                        f =>
                            !f.IsInCombat
                            && !excludedFleetIds.Contains(f.GetInstanceID())
                            && f.Movement == null
                            && SpaceCombatResolver.HasActiveSpaceUnits(f),
                        recurse: false
                    )
                    .ToList();

                if (fleets.Count < 2)
                    continue;

                List<IGrouping<string, Fleet>> factionGroups = fleets
                    .GroupBy(f => f.GetOwnerInstanceID())
                    .Where(g => g.Key != null)
                    .OrderBy(g => g.Key)
                    .ToList();

                if (factionGroups.Count < 2)
                    continue;

                attacker = factionGroups[0].First();
                defender = factionGroups[1].First();
                return true;
            }

            return false;
        }

        private static bool AreFleetsContestingPlanet(Fleet firstFleet, Fleet secondFleet)
        {
            if (firstFleet == null || secondFleet == null)
                return false;

            Planet firstPlanet = firstFleet.GetParentOfType<Planet>();
            Planet secondPlanet = secondFleet.GetParentOfType<Planet>();

            return firstPlanet != null
                && firstPlanet == secondPlanet
                && firstFleet.Movement == null
                && secondFleet.Movement == null
                && SpaceCombatResolver.HasActiveSpaceUnits(firstFleet)
                && SpaceCombatResolver.HasActiveSpaceUnits(secondFleet)
                && firstFleet.GetOwnerInstanceID() != secondFleet.GetOwnerInstanceID();
        }

        /// <summary>
        /// Removes a destroyed capital ship after resolving its carried units.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movement">Movement system used for surviving passenger evacuation.</param>
        /// <param name="ship">The destroyed capital ship.</param>
        internal static void DestroyCapitalShip(
            GameRoot game,
            MovementSystem movement,
            CapitalShip ship
        )
        {
            Fleet fleet = ship.GetParentOfType<Fleet>();

            EvacuateOfficersFromDestroyedShip(game, movement, ship, fleet);
            EvacuateStarfightersFromDestroyedShip(game, movement, ship, fleet);
            DestroyRegimentsAboard(game, ship);

            game.DetachNode(ship);
            GameLogger.Log($"Ship destroyed: {ship.GetDisplayName()}");
        }

        /// <summary>
        /// Moves officers off a destroyed capital ship.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movement">Movement system used for planet evacuation.</param>
        /// <param name="ship">The destroyed capital ship.</param>
        /// <param name="fleet">The fleet that contained the destroyed ship.</param>
        private static void EvacuateOfficersFromDestroyedShip(
            GameRoot game,
            MovementSystem movement,
            CapitalShip ship,
            Fleet fleet
        )
        {
            List<Officer> officers = ship.Officers.ToList();
            if (officers.Count == 0)
                return;

            CapitalShip survivingShip = FindSurvivingShip(fleet, ship);

            foreach (Officer officer in officers)
            {
                if (survivingShip != null)
                {
                    game.MoveNode(officer, survivingShip);
                    GameLogger.Log(
                        $"{officer.GetDisplayName()} evacuated to {survivingShip.GetDisplayName()} after {ship.GetDisplayName()} destroyed."
                    );
                }
                else
                {
                    movement.EvacuateToNearestFriendlyPlanet(officer);
                }
            }
        }

        /// <summary>
        /// Moves surviving starfighters off a destroyed capital ship.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="movement">Movement system used for planet evacuation.</param>
        /// <param name="ship">The destroyed capital ship.</param>
        /// <param name="fleet">The fleet that contained the destroyed ship.</param>
        private static void EvacuateStarfightersFromDestroyedShip(
            GameRoot game,
            MovementSystem movement,
            CapitalShip ship,
            Fleet fleet
        )
        {
            List<Starfighter> starfighters = ship
                .Starfighters.Where(starfighter =>
                    starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                )
                .ToList();

            foreach (Starfighter starfighter in starfighters)
            {
                CapitalShip survivingCarrier = FindSurvivingCarrier(fleet, ship);
                if (survivingCarrier != null)
                    game.MoveNode(starfighter, survivingCarrier);
                else
                    movement.EvacuateToNearestFriendlyPlanet(starfighter);
            }
        }

        /// <summary>
        /// Removes complete regiments aboard a destroyed capital ship.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="ship">The destroyed capital ship.</param>
        private static void DestroyRegimentsAboard(GameRoot game, CapitalShip ship)
        {
            List<Regiment> regiments = ship
                .Regiments.Where(regiment =>
                    regiment.ManufacturingStatus == ManufacturingStatus.Complete
                )
                .ToList();

            foreach (Regiment regiment in regiments)
                game.DetachNode(regiment);
        }

        /// <summary>
        /// Finds another surviving capital ship in the same fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="destroyedShip">The destroyed ship to exclude.</param>
        /// <returns>A surviving capital ship, or null if none exists.</returns>
        private static CapitalShip FindSurvivingShip(Fleet fleet, CapitalShip destroyedShip)
        {
            return fleet?.CapitalShips.FirstOrDefault(ship =>
                !ReferenceEquals(ship, destroyedShip) && ship.CurrentHullStrength > 0
            );
        }

        /// <summary>
        /// Finds another surviving capital ship with starfighter capacity.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="destroyedShip">The destroyed ship to exclude.</param>
        /// <returns>A surviving carrier, or null if none exists.</returns>
        private static CapitalShip FindSurvivingCarrier(Fleet fleet, CapitalShip destroyedShip)
        {
            return fleet?.CapitalShips.FirstOrDefault(ship =>
                !ReferenceEquals(ship, destroyedShip)
                && ship.CurrentHullStrength > 0
                && ship.GetExcessStarfighterCapacity() > 0
            );
        }
    }

    internal class SpaceCombatResolver
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;

        public SpaceCombatResolver(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement
        )
        {
            _game = game;
            _provider = provider;
            _movement = movement;
        }

        /// <summary>
        /// Resolves a pending combat encounter. Applies damage to the game world and clears
        /// IsInCombat on both fleets regardless of outcome.
        /// </summary>
        /// <param name="decision">The combat decision to resolve.</param>
        /// <param name="autoResolve">True to use auto-resolve; false to use manual combat.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        public List<GameResult> Resolve(CombatDecisionContext decision, bool autoResolve)
        {
            if (autoResolve)
                return ResolveFleetEncounter(decision, allowRetreatBeforeCombat: false);

            RunManualCombat();
            ClearCombatFlags(decision);
            return new List<GameResult>();
        }

        public List<GameResult> ResolveAutomaticFleetEncounter(CombatDecisionContext decision)
        {
            return ResolveFleetEncounter(decision, allowRetreatBeforeCombat: true);
        }

        private List<GameResult> ResolveFleetEncounter(
            CombatDecisionContext decision,
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
                while (AreFleetsContestingPlanet(attacker, defender))
                {
                    if (allowRetreatBeforeCombat && TryRetreatOutmatchedFleet(attacker, defender))
                        break;

                    SpaceCombatResult combatResult = ResolveCombatRound(
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

                    if (!AreFleetsContestingPlanet(attacker, defender))
                        break;

                    if (IsSpaceCombatStalemated(attacker, defender, combatResult))
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
                Planet = roundResult.Planet,
                Winner = roundResult.Winner,
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
            AddShipDamage(encounterResult.ShipDamage, roundResult.ShipDamage);
            AddFighterLosses(encounterResult.FighterLosses, roundResult.FighterLosses);
            encounterResult.Events.AddRange(roundResult.Events);
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

        private void RemoveFleetUnableToRetreat(Fleet fleet)
        {
            _game.DetachNode(fleet);
            GameLogger.Log($"Fleet removed after stalemated combat: {fleet.GetDisplayName()}");
        }

        private void ClearCombatFlags(CombatDecisionContext decision)
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

        private bool TryRetreatOutmatchedFleet(Fleet attacker, Fleet defender)
        {
            int attackerPower = attacker.GetCombatValue();
            int defenderPower = defender.GetCombatValue();

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

        private static bool IsRetreatBlockedByGravityWell(Fleet fleet, Fleet opponent)
        {
            if (fleet == null || opponent == null)
                return false;

            Planet fleetPlanet = fleet.GetParentOfType<Planet>();
            if (fleetPlanet == null || fleetPlanet != opponent.GetParentOfType<Planet>())
                return false;

            return GetActiveCapitalShips(opponent).Any(ship => ship.HasGravityWell);
        }

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

        internal static bool HasActiveSpaceUnits(Fleet fleet)
        {
            if (fleet == null)
                return false;

            return GetActiveCapitalShips(fleet).Any() || GetActiveStarfighters(fleet).Any();
        }

        private static bool IsSpaceCombatStalemated(
            Fleet attacker,
            Fleet defender,
            SpaceCombatResult combatResult
        )
        {
            return !HasOperationalSpaceWeapons(attacker) && !HasOperationalSpaceWeapons(defender)
                || !DidCombatChangeState(combatResult);
        }

        private static bool DidCombatChangeState(SpaceCombatResult combatResult)
        {
            if (combatResult == null)
                return false;

            return combatResult.Winner != CombatSide.Draw
                || combatResult.ShipDamage.Any(damage => damage.HullBefore != damage.HullAfter)
                || combatResult.FighterLosses.Any(loss => loss.SquadsBefore != loss.SquadsAfter);
        }

        private static bool HasOperationalSpaceWeapons(Fleet fleet)
        {
            if (fleet == null)
                return false;

            return GetActiveCapitalShips(fleet).Any(IsArmedCapitalShip)
                || GetActiveStarfighters(fleet).Any(IsArmedStarfighter);
        }

        private static IEnumerable<CapitalShip> GetActiveCapitalShips(Fleet fleet)
        {
            return fleet.CapitalShips.Where(IsActiveCapitalShip);
        }

        private static IEnumerable<Starfighter> GetActiveStarfighters(Fleet fleet)
        {
            return GetActiveCapitalShips(fleet)
                .SelectMany(ship => ship.Starfighters)
                .Where(IsActiveStarfighter);
        }

        private static bool IsActiveCapitalShip(CapitalShip ship)
        {
            return ship.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.Movement == null
                && ship.CurrentHullStrength > 0;
        }

        private static bool IsActiveStarfighter(Starfighter starfighter)
        {
            return starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                && starfighter.Movement == null
                && starfighter.CurrentSquadronSize > 0;
        }

        private static bool IsArmedCapitalShip(CapitalShip ship)
        {
            return ship.GetPrimaryWeaponStrength() > 0;
        }

        private static bool IsArmedStarfighter(Starfighter starfighter)
        {
            return starfighter.LaserCannon + starfighter.IonCannon + starfighter.Torpedoes > 0;
        }

        private SpaceCombatResult ResolveCombatRound(
            Fleet attacker,
            Fleet defender,
            IRandomNumberProvider rng
        )
        {
            if (attacker == null || defender == null)
            {
                GameLogger.Warning("ResolveCombatRound: one or both fleets no longer exist.");
                return null;
            }

            Planet planet = attacker.GetParentOfType<Planet>();
            if (planet == null)
            {
                GameLogger.Warning(
                    $"ResolveCombatRound: attacker {attacker.GetDisplayName()} is not at a planet."
                );
                return null;
            }

            SpaceCombatResult result = ResolveSpace(
                attacker,
                defender,
                planet,
                rng,
                _game.CurrentTick,
                _game.Config.Combat
            );
            result.Events = ApplyCombatResult(result);

            GameLogger.Log(
                $"Combat at {planet.GetDisplayName()}: "
                    + $"{attacker.GetDisplayName()} vs {defender.GetDisplayName()} — "
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
        /// <param name="planet">Planet where combat occurs.</param>
        /// <param name="rng">Random-number provider for damage variance.</param>
        /// <param name="tick">Current game tick (recorded on the result).</param>
        /// <param name="config">Combat configuration supplying damage/variance tuning values.</param>
        /// <returns>The combat result with winner, per-ship damage, and fighter losses.</returns>
        private static SpaceCombatResult ResolveSpace(
            Fleet attackerFleet,
            Fleet defenderFleet,
            Planet planet,
            IRandomNumberProvider rng,
            int tick,
            GameConfig.CombatConfig config
        )
        {
            (List<ShipSnap> atkShips, List<FighterSnap> atkFighters) = SnapshotFleet(attackerFleet);
            (List<ShipSnap> defShips, List<FighterSnap> defFighters) = SnapshotFleet(defenderFleet);

            bool anyArmed =
                HasOperationalSpaceWeapons(attackerFleet)
                || HasOperationalSpaceWeapons(defenderFleet);

            if (anyArmed)
            {
                PhaseWeaponFire(atkShips, defShips, rng, config);
                PhaseWeaponFire(defShips, atkShips, rng, config);
                PhaseFighterEngage(atkFighters, defFighters, atkShips, defShips, rng, config);
            }

            return BuildSpaceResult(
                attackerFleet,
                defenderFleet,
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
        /// Builds mutable per-battle snapshots for all capital ships in a fleet and captures
        /// current squadron sizes for all its starfighter groups.
        /// </summary>
        /// <param name="fleet">Fleet to snapshot.</param>
        /// <returns>Ship snapshots and parallel list of squadron sizes.</returns>
        private static (List<ShipSnap> ships, List<FighterSnap> fighters) SnapshotFleet(Fleet fleet)
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
                .Where(IsActiveStarfighter)
                .Select(fighter => new FighterSnap
                {
                    Fighter = fighter,
                    InitialSquadronSize = fighter.CurrentSquadronSize,
                    CurrentSquadronSize = fighter.CurrentSquadronSize,
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
        /// <param name="targets">Target side's ship snapshots (mutated with damage).</param>
        /// <param name="rng">Random-number provider for variance.</param>
        /// <param name="config">Combat configuration supplying damage variance.</param>
        private static void PhaseWeaponFire(
            List<ShipSnap> firing,
            List<ShipSnap> targets,
            IRandomNumberProvider rng,
            GameConfig.CombatConfig config
        )
        {
            int totalFire = CalculateTotalFirepower(firing);
            if (totalFire == 0)
                return;

            List<int> aliveIndices = targets
                .Select((t, idx) => new { t, idx })
                .Where(x => x.t.Alive)
                .Select(x => x.idx)
                .ToList();

            if (aliveIndices.Count == 0)
                return;

            int firePerTarget = totalFire / aliveIndices.Count;
            foreach (int idx in aliveIndices)
            {
                ApplyWeaponDamage(targets[idx], firePerTarget, rng, config);
            }
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
            GameConfig.CombatConfig config
        )
        {
            double roll = rng.NextDouble();
            int variance = (int)(
                (double)baseDamage * config.WeaponDamageVariancePercent * (roll * 2.0 - 1.0) / 100.0
            );
            int damage = Math.Max(baseDamage + variance, 0);

            int absorbed = (int)(damage * target.ShieldNibble / 15.0);
            int hullDamage = Math.Max(damage - absorbed, 0);

            target.HullCurrent = Math.Max(target.HullCurrent - hullDamage, 0);
            if (target.HullCurrent == 0)
                target.Alive = false;
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
            GameConfig.CombatConfig config
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
            GameConfig.CombatConfig config
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
        }

        /// <summary>
        /// Builds a SpaceCombatResult from the final snapshots and initial fighter counts,
        /// recording per-ship damage and per-squadron losses.
        /// </summary>
        /// <param name="attackerFleet">Attacker fleet.</param>
        /// <param name="defenderFleet">Defender fleet.</param>
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
                Planet = planet,
                Winner = DetermineWinner(atkShips, defShips, atkFighters, defFighters),
                Tick = tick,
            };

            CollectShipDamage(result.ShipDamage, atkShips);
            CollectShipDamage(result.ShipDamage, defShips);
            CollectFighterLosses(result.FighterLosses, atkFighters);
            CollectFighterLosses(result.FighterLosses, defFighters);

            return result;
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

            if (result.AttackerFleet.CapitalShips.Count == 0)
                RemoveFleetFromScene(result.AttackerFleet);
            if (result.DefenderFleet.CapitalShips.Count == 0)
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
                    CombatSystem.DestroyCapitalShip(_game, _movement, ship);
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

            public bool Alive => CurrentSquadronSize > 0;
        }
    }
}
