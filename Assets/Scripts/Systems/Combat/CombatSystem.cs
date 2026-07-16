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
    /// <summary>
    /// Captures the context of a pending combat encounter requiring player resolution.
    /// </summary>
    public class CombatDecisionContext
    {
        public string AttackerFleetInstanceID { get; set; }
        public string DefenderFleetInstanceID { get; set; }
    }

    /// <summary>
    /// Resolves space combat, planetary assault, and orbital bombardment.
    /// Also owns fleet encounter detection and pending player combat decisions.
    /// </summary>
    public partial class CombatSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;
        private readonly PlanetaryControlSystem _ownership;
        private CombatDecisionContext _pendingDecision;

        /// <summary>
        /// Whether a player-involved combat encounter is waiting for resolution.
        /// </summary>
        public bool HasPendingDecision => _pendingDecision != null;

        /// <summary>
        /// Creates the combat system and its shared gameplay dependencies.
        /// </summary>
        /// <param name="game">Active game state.</param>
        /// <param name="provider">Random-number provider used by combat resolution.</param>
        /// <param name="movement">Movement system used for retreats and evacuation.</param>
        /// <param name="ownership">Planetary control system used by ground combat.</param>
        public CombatSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            _provider = provider;
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
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

            results.AddRange(ResolveAutomaticFleetEncounter(decision));

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
                            && HasActiveSpaceUnits(f),
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
}
