using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI;
using Rebellion.AI.Phases;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Random;

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

                GalaxyMap factionView = new FogOfWarSystem(game).BuildFactionView(faction);
                AIAssessment assessment = new AIAssessment(game, faction, factionView);
                AIStrategicPlan strategicPlan = new AIStrategicPlan(game, assessment);
                AITurnContext context = new AITurnContext(
                    game,
                    faction,
                    null,
                    null,
                    null,
                    null,
                    null,
                    new SystemRandomProvider(0),
                    assessment,
                    strategicPlan,
                    factionView
                );
                new AIDemandGenerationPhase().Execute(context);
                AttackReadinessFactionCounters counters = GetCounters(faction.InstanceID);

                foreach (Fleet fleet in buildingAttackFleets)
                {
                    List<string> blockers = GetBlockers(
                        assessment,
                        context,
                        context.StrategicPlan,
                        fleet,
                        planets,
                        game.Config.AI.FleetDeployment.MinimumAttackStrength
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
        /// <param name="context">The faction's current AI turn context.</param>
        /// <param name="strategicPlan">The faction's current strategic fleet targets.</param>
        /// <param name="fleet">The attack fleet to inspect.</param>
        /// <param name="planets">Known planets indexed by instance identifier.</param>
        /// <param name="minimumAttackStrength">The configured baseline attack strength.</param>
        /// <returns>The active readiness blocker names.</returns>
        private static List<string> GetBlockers(
            AIAssessment assessment,
            AITurnContext context,
            AIStrategicPlan strategicPlan,
            Fleet fleet,
            IReadOnlyDictionary<string, Planet> planets,
            int minimumAttackStrength
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
            int requiredCombat = GetRequiredCombatStrength(context, target);
            if (assessment.GetReadyFleetCombatValue(fleet) < requiredCombat)
            {
                blockers.Add(
                    requiredCombat > minimumAttackStrength
                        ? "CombatStrengthOppositionEscalated"
                        : "CombatStrengthBaseline"
                );
            }
            int requiredRegimentCount = GetRequiredRegimentCount(context, target);
            if (assessment.GetReadyFleetRegimentCount(fleet) < requiredRegimentCount)
                blockers.Add("RegimentCount");
            if (assessment.GetReadyFleetRegimentCapacity(fleet) < requiredRegimentCount)
                blockers.Add("RegimentCapacity");
            if (
                assessment.GetReadyFleetRegimentAttackStrength(fleet)
                < GetRequiredRegimentStrength(context, target)
            )
                blockers.Add("RegimentStrength");
            if (
                assessment.GetFleetBombardmentStrength(fleet)
                < GetRequiredBombardmentStrength(context, target)
            )
                blockers.Add("BombardmentStrength");

            return blockers;
        }

        /// <summary>
        /// Calculates the live combat threshold used by the historical readiness diagnostic.
        /// </summary>
        /// <param name="context">The sampled faction turn context.</param>
        /// <param name="target">The live attack target.</param>
        /// <returns>The required fleet combat strength.</returns>
        private static int GetRequiredCombatStrength(AITurnContext context, Planet target)
        {
            string systemId = context.Assessment.GetPlanetSystemId(target);
            int systemStrength = context
                .Assessment.FactionViewPlanets.Where(planet =>
                    context.Assessment.GetPlanetSystemId(planet) == systemId
                )
                .Select(planet =>
                {
                    int hostileStrength =
                        context.Assessment.GetStrongestHostileFleetStrength(planet)
                        + context.Assessment.GetHostilePlanetaryStarfighterStrength(planet);
                    return hostileStrength > 0
                        ? IntegerMath.ScaleByPercent(
                            hostileStrength,
                            context
                                .Game
                                .Config
                                .AI
                                .FleetDeployment
                                .AttackStrengthPercentOfStrongestHostileFleet
                        )
                        : 0;
                })
                .DefaultIfEmpty()
                .Max();
            int required = Math.Max(
                context.Game.Config.AI.FleetDeployment.MinimumAttackStrength,
                systemStrength
            );
            return IntegerMath.ScaleByPercent(required, GetIntelReservePercent(context, target));
        }

        /// <summary>
        /// Calculates the stale-intelligence reserve applied by the historical diagnostic.
        /// </summary>
        /// <param name="context">The sampled faction turn context.</param>
        /// <param name="target">The live attack target.</param>
        /// <returns>The required combat percentage.</returns>
        private static int GetIntelReservePercent(AITurnContext context, Planet target)
        {
            int maximumAge = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks
            );
            int age = context.Assessment.GetPlanetIntelAge(target);
            if (age <= maximumAge)
                return 100;

            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            int maximumPercent = Math.Max(100, config.StaleIntelMaximumAttackStrengthPercent);
            int saturationAge =
                maximumAge * Math.Max(1, config.StaleIntelReserveSaturationIntervals);
            int staleAge = Math.Min(age - maximumAge, saturationAge - maximumAge);
            return 100
                + (maximumPercent - 100) * staleAge / Math.Max(1, saturationAge - maximumAge);
        }

        /// <summary>
        /// Calculates the live regiment count threshold used by the readiness diagnostic.
        /// </summary>
        /// <param name="context">The sampled faction turn context.</param>
        /// <param name="target">The live attack target.</param>
        /// <returns>The required regiment count.</returns>
        private static int GetRequiredRegimentCount(AITurnContext context, Planet target)
        {
            int defenders = context.Assessment.GetDefendingRegimentCount(target);
            int combatCount = defenders == 0 ? 0 : defenders + 1;
            GameConfig.AIFleetDeploymentConfig fleetConfig = context.Game.Config.AI.FleetDeployment;
            int minimum = Math.Max(1, fleetConfig.MinimumPlanetaryAssaultRegimentCount);
            int stability = UprisingSystem.CalculateGarrisonRequirement(
                target,
                context.Faction,
                context.Game.Config.AI.Garrison
            );
            int occupation = Math.Max(
                minimum,
                Math.Min(
                    stability,
                    context.Game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount
                )
            );
            return combatCount + occupation;
        }

        /// <summary>
        /// Calculates the live regiment-strength threshold used by the readiness diagnostic.
        /// </summary>
        /// <param name="context">The sampled faction turn context.</param>
        /// <param name="target">The live attack target.</param>
        /// <returns>The required regiment attack strength.</returns>
        private static int GetRequiredRegimentStrength(AITurnContext context, Planet target)
        {
            return Math.Max(
                1,
                IntegerMath.ScaleByPercent(
                    context.Assessment.GetDefendingRegimentDefenseStrength(target),
                    context.Game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense
                )
            );
        }

        /// <summary>
        /// Calculates the live bombardment threshold used by the readiness diagnostic.
        /// </summary>
        /// <param name="context">The sampled faction turn context.</param>
        /// <param name="target">The live attack target.</param>
        /// <returns>The required bombardment strength.</returns>
        private static int GetRequiredBombardmentStrength(AITurnContext context, Planet target)
        {
            return PlanetaryAssaultResolver.IsBlockedByShields(
                target,
                context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
            )
                ? context.Assessment.GetBombardmentShieldResistance(target) + 1
                : 0;
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
