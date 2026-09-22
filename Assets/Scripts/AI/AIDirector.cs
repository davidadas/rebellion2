using System;
using System.Collections.Generic;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Systems;
using Rebellion.Util.Random;

namespace Rebellion.AI
{
    /// <summary>
    /// Coordinates the AI turn phases for each faction.
    /// </summary>
    public sealed class AIDirector
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _random;
        private readonly MissionSystem _missions;
        private readonly MovementSystem _movement;
        private readonly ManufacturingSystem _manufacturing;
        private readonly MaintenanceSystem _maintenance;
        private readonly BombardmentSystem _bombardment;
        private readonly PlanetaryAssaultSystem _planetaryAssault;
        private readonly IReadOnlyList<IAITurnPhase> _turnPhases;

        /// <summary>
        /// Raised immediately before one named unit of faction-turn work begins.
        /// </summary>
        public event Action<Faction, string> FactionTurnStepStarted;

        /// <summary>
        /// Raised after one named unit of faction-turn work finishes or is interrupted.
        /// </summary>
        public event Action<Faction, string> FactionTurnStepCompleted;

        /// <summary>
        /// Creates an AI director using the current game systems.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="missions">Mission system used by mission proposals.</param>
        /// <param name="movement">Movement system used by movement proposals.</param>
        /// <param name="manufacturing">Manufacturing system used by production proposals.</param>
        /// <param name="bombardment">Bombardment system used by fleet attack proposals.</param>
        /// <param name="planetaryAssault">Planetary-assault system used by fleet attack proposals.</param>
        /// <param name="random">RNG provider used by probabilistic AI decisions.</param>
        /// <param name="maintenance">Maintenance system used to project production capacity.</param>
        public AIDirector(
            GameRoot game,
            MissionSystem missions,
            MovementSystem movement,
            ManufacturingSystem manufacturing,
            BombardmentSystem bombardment,
            PlanetaryAssaultSystem planetaryAssault,
            IRandomNumberProvider random,
            MaintenanceSystem maintenance = null
        )
        {
            _game = game;
            _random = random;
            _missions = missions;
            _movement = movement;
            _manufacturing = manufacturing;
            _maintenance = maintenance;
            _bombardment = bombardment;
            _planetaryAssault = planetaryAssault;
            AIProductionDemandGenerator productionDemandGenerator =
                new AIProductionDemandGenerator();
            AIProductionPlanner productionPlanner = new AIProductionPlanner();
            _turnPhases = new List<IAITurnPhase>
            {
                new AIDemandGenerationPhase(
                    new IAIDemandGenerator[] { new AIAttackDemandGenerator() }
                ),
                new AIPlanningPhase(
                    new IAIProposalPlanner[]
                    {
                        new AIAbortMissionPlanner(),
                        new AIFacilityRemovalPlanner(),
                        new AIMissionPlanner(),
                        new AIOrbitalEngagementPlanner(),
                        new AIFleetPlanner(),
                    }
                ),
                new AIDemandGenerationPhase(new IAIDemandGenerator[] { productionDemandGenerator }),
                new AIPlanningPhase(new IAIProposalPlanner[] { productionPlanner }),
                new AIScoringPhase(),
                new AISelectionPhase(),
                new AIExecutionPhase(),
            };
        }

        /// <summary>
        /// Processes one faction AI turn.
        /// </summary>
        /// <param name="faction">The faction to process.</param>
        /// <param name="factionView">The faction-visible galaxy state for this turn.</param>
        /// <returns>Game results emitted by this AI turn.</returns>
        internal List<GameResult> ProcessFaction(Faction faction, GalaxyMap factionView)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (object _ in ProcessFactionIncrementally(faction, factionView, results)) { }

            return results;
        }

        /// <summary>
        /// Processes one faction AI turn one phase at a time.
        /// </summary>
        /// <param name="faction">The faction to process.</param>
        /// <param name="factionView">The faction-visible galaxy state for this turn.</param>
        /// <param name="results">The result list populated when processing completes.</param>
        /// <returns>A sequence containing one step per completed phase.</returns>
        internal IEnumerable<object> ProcessFactionIncrementally(
            Faction faction,
            GalaxyMap factionView,
            ICollection<GameResult> results
        )
        {
            const string assessmentStep = "Assessment";
            FactionTurnStepStarted?.Invoke(faction, assessmentStep);
            AITurnContext context;
            try
            {
                AIAssessment assessment = new AIAssessment(_game, faction, factionView);
                AIStrategicPlan strategicPlan = new AIStrategicPlan(_game, assessment);
                context = new AITurnContext(
                    _game,
                    faction,
                    _missions,
                    _movement,
                    _manufacturing,
                    _bombardment,
                    _planetaryAssault,
                    _random,
                    assessment,
                    strategicPlan,
                    factionView,
                    _maintenance
                );
            }
            finally
            {
                FactionTurnStepCompleted?.Invoke(faction, assessmentStep);
            }
            yield return null;

            foreach (IAITurnPhase phase in _turnPhases)
            {
                string stepName = phase.GetType().Name;
                FactionTurnStepStarted?.Invoke(faction, stepName);
                try
                {
                    if (phase is IAIIncrementalTurnPhase incrementalPhase)
                    {
                        foreach (object step in incrementalPhase.ExecuteIncrementally(context))
                            yield return step;
                    }
                    else
                        phase.Execute(context);
                }
                finally
                {
                    FactionTurnStepCompleted?.Invoke(faction, stepName);
                }

                yield return null;
            }

            foreach (GameResult result in context.Results)
                results.Add(result);
        }
    }
}
