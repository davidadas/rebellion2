using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

public static partial class HeadlessSimulationRunner
{
    private sealed class AttackReadinessTracker
    {
        private const int _readinessSampleIntervalInAITurns = 25;
        private readonly Dictionary<string, AttackReadinessFactionCounters> _counters = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Records failed readiness gates for attack fleets waiting to launch.
        /// </summary>
        /// <param name="game">The game state to inspect.</param>
        /// <remarks>
        /// Readiness blockers persist across construction and travel, so sampling every 25th AI
        /// turn captures their duration without scanning every planet and fleet on every turn.
        /// </remarks>
        public void RecordTick(GameRoot game)
        {
            if (
                game?.Config?.AI == null
                || game.Config.AI.TickInterval <= 0
                || game.CurrentTick
                    % (game.Config.AI.TickInterval * _readinessSampleIntervalInAITurns)
                    != 0
            )
                return;

            Dictionary<string, Planet> planets = game.GetSceneNodesByType<Planet>()
                .Where(planet => !string.IsNullOrWhiteSpace(planet.InstanceID))
                .ToDictionary(planet => planet.InstanceID, StringComparer.Ordinal);

            foreach (Faction faction in game.GetFactions())
            {
                List<Fleet> buildingAttackFleets = game.GetSceneNodesByOwnerInstanceID<Fleet>(
                        faction.InstanceID
                    )
                    .Where(fleet =>
                        fleet.Movement == null
                        && fleet.Order?.OrderType == FleetOrderType.Attack
                        && fleet.Order.Status == FleetOrderStatus.Building
                    )
                    .ToList();
                if (buildingAttackFleets.Count == 0)
                    continue;

                AITurnContext context = new AITurnContext(
                    game,
                    faction,
                    null,
                    null,
                    null,
                    null,
                    null,
                    new SystemRandomProvider(0),
                    new FogOfWarSystem(game).BuildFactionView(faction)
                );
                AIAssessment assessment = context.Assessment;
                AttackReadinessFactionCounters counters = GetCounters(faction.InstanceID);

                foreach (Fleet fleet in buildingAttackFleets)
                {
                    List<string> blockers = GetBlockers(
                        assessment,
                        context.StrategicPlan,
                        fleet,
                        planets
                    );
                    counters.Record(blockers);
                }
            }
        }

        /// <summary>
        /// Builds the readiness summary for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction's attack-readiness summary.</returns>
        public AttackReadinessSimulationSummary BuildSummary(string factionId)
        {
            return GetCounters(factionId).BuildSummary();
        }

        /// <summary>
        /// Gets or creates attack-readiness counters for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction readiness counters.</returns>
        private AttackReadinessFactionCounters GetCounters(string factionId)
        {
            string key = factionId ?? string.Empty;
            if (!_counters.TryGetValue(key, out AttackReadinessFactionCounters counters))
            {
                counters = new AttackReadinessFactionCounters();
                _counters[key] = counters;
            }

            return counters;
        }

        /// <summary>
        /// Gets the readiness gates currently blocking an attack fleet.
        /// </summary>
        /// <param name="assessment">The faction's current strategic assessment.</param>
        /// <param name="strategicPlan">The faction's current strategic fleet targets.</param>
        /// <param name="fleet">The attack fleet to inspect.</param>
        /// <param name="planets">Known planets indexed by instance identifier.</param>
        /// <returns>The active readiness blocker names.</returns>
        private static List<string> GetBlockers(
            AIAssessment assessment,
            AIStrategicPlan strategicPlan,
            Fleet fleet,
            IReadOnlyDictionary<string, Planet> planets
        )
        {
            List<string> blockers = new List<string>();
            if (
                string.IsNullOrWhiteSpace(fleet.Order?.TargetPlanetId)
                || !planets.TryGetValue(fleet.Order.TargetPlanetId, out Planet target)
            )
            {
                blockers.Add("MissingTarget");
                return blockers;
            }

            if (!strategicPlan.CanFleetDepart(fleet))
                blockers.Add("HeadquartersReserve");
            if (!fleet.HasOperationalCapitalShips())
                blockers.Add("OperationalCapitalShips");
            if (
                assessment.GetReadyFleetCombatValue(fleet)
                < assessment.GetRequiredAttackCombatStrength(target)
            )
                blockers.Add("CombatStrength");
            if (
                assessment.GetReadyFleetRegimentCount(fleet)
                < assessment.GetRequiredAttackRegimentCount(target)
            )
                blockers.Add("RegimentCount");
            if (
                assessment.GetReadyFleetRegimentCapacity(fleet)
                < assessment.GetRequiredAttackRegimentCount(target)
            )
                blockers.Add("RegimentCapacity");
            if (
                assessment.GetReadyFleetRegimentAttackStrength(fleet)
                < assessment.GetRequiredAttackRegimentStrength(target)
            )
                blockers.Add("RegimentStrength");
            if (
                assessment.GetFleetBombardmentStrength(fleet)
                < assessment.GetRequiredBombardmentStrength(target)
            )
                blockers.Add("BombardmentStrength");

            return blockers;
        }

        private sealed class AttackReadinessFactionCounters
        {
            private readonly Dictionary<string, int> _samples = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _soleBlockerSamples = new(
                StringComparer.Ordinal
            );

            public int BuildingFleetSamples { get; private set; }

            /// <summary>
            /// Records one attack fleet's active readiness blockers.
            /// </summary>
            /// <param name="blockers">The blocker names observed for the fleet.</param>
            public void Record(IReadOnlyList<string> blockers)
            {
                BuildingFleetSamples++;
                foreach (string blocker in blockers)
                    Increment(_samples, blocker);

                if (blockers.Count == 1)
                    Increment(_soleBlockerSamples, blockers[0]);
            }

            /// <summary>
            /// Builds the aggregate attack-readiness blocker summary.
            /// </summary>
            /// <returns>The attack-readiness summary.</returns>
            public AttackReadinessSimulationSummary BuildSummary()
            {
                return new AttackReadinessSimulationSummary
                {
                    BuildingFleetSamples = BuildingFleetSamples,
                    Blockers = _samples
                        .OrderByDescending(pair => pair.Value)
                        .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new AttackReadinessBlockerSummary
                        {
                            Blocker = pair.Key,
                            Samples = pair.Value,
                            SoleBlockerSamples = _soleBlockerSamples.TryGetValue(
                                pair.Key,
                                out int soleSamples
                            )
                                ? soleSamples
                                : 0,
                        })
                        .ToArray(),
                };
            }

            /// <summary>
            /// Increments a named readiness counter.
            /// </summary>
            /// <param name="counters">The counters to update.</param>
            /// <param name="key">The counter name.</param>
            private static void Increment(Dictionary<string, int> counters, string key)
            {
                counters[key] = counters.TryGetValue(key, out int count) ? count + 1 : 1;
            }
        }
    }
}
