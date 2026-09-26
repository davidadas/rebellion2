using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.AI
{
    /// <summary>
    /// Coordinates the AI turn phases for each faction.
    /// </summary>
    public sealed class AIDirector
    {
        private readonly GameRoot _game;
        private readonly FogOfWarQueries _fogOfWar;
        private readonly IRandomNumberProvider _random;
        private readonly MissionCommands _missions;
        private readonly MissionQueries _missionQueries;
        private readonly MovementCommands _movement;
        private readonly ManufacturingCommands _manufacturing;
        private readonly MaintenanceCommands _maintenance;
        private readonly BombardmentCommands _bombardment;
        private readonly BombardmentQueries _bombardmentQueries;
        private readonly PlanetaryAssaultCommands _planetaryAssault;
        private readonly PlanetaryAssaultQueries _planetaryAssaultQueries;
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
        /// Raised immediately before one faction's AI turn begins.
        /// </summary>
        public event Action<Faction> FactionTurnStarted;

        /// <summary>
        /// Raised after one faction's AI turn completes.
        /// </summary>
        public event Action<Faction> FactionTurnCompleted;

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
            MissionCommands missions,
            MovementCommands movement,
            ManufacturingCommands manufacturing,
            BombardmentCommands bombardment,
            PlanetaryAssaultCommands planetaryAssault,
            IRandomNumberProvider random,
            MaintenanceCommands maintenance = null
        )
        {
            _game = game;
            _fogOfWar = game == null ? null : new FogOfWarQueries(game);
            _random = random;
            _missions = missions;
            _missionQueries = game == null ? null : new MissionQueries(game);
            _movement = movement;
            _manufacturing = manufacturing;
            _maintenance = maintenance;
            _bombardment = bombardment;
            _bombardmentQueries = game == null ? null : new BombardmentQueries(game);
            _planetaryAssault = planetaryAssault;
            _planetaryAssaultQueries = game == null ? null : new PlanetaryAssaultQueries(game);
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
        /// Processes AI turns for every eligible AI-controlled faction.
        /// </summary>
        /// <returns>The results produced by AI actions.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            foreach (object _ in ProcessTickIncrementally(results)) { }

            return results;
        }

        /// <summary>
        /// Processes eligible AI factions one phase at a time.
        /// </summary>
        /// <param name="results">The result list populated as faction turns complete.</param>
        /// <returns>A sequence containing one step per completed AI phase.</returns>
        internal IEnumerable<object> ProcessTickIncrementally(ICollection<GameResult> results)
        {
            int tickInterval = _game.Config.AI.TickInterval;
            if (tickInterval <= 0 || _game.CurrentTick % tickInterval != 0)
                yield break;

            foreach (Faction faction in _game.GetFactions().Where(_game.IsFactionAIControlled))
            {
                GalaxyMap factionView = _fogOfWar.BuildFactionView(faction);
                FactionTurnStarted?.Invoke(faction);
                foreach (object step in ProcessFactionIncrementally(faction, factionView, results))
                    yield return step;
                FactionTurnCompleted?.Invoke(faction);
            }
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
