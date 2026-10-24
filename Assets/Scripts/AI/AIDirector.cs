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
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
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
        private readonly HashSet<string> _optionalProductionOrders = new(StringComparer.Ordinal);
        private readonly FogOfWarQueries _fogOfWar;
        private readonly IRandomNumberProvider _random;
        private readonly MissionCommands _missions;
        private readonly MissionQueries _missionQueries;
        private readonly MissionEncounterOdds.Cache _missionEncounterCache;
        private readonly MovementCommands _movement;
        private readonly HeadquartersCommands _headquarters;
        private readonly OfficerCommandCommands _officerCommands;
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
        /// <param name="headquarters">Headquarters system used by relocation proposals.</param>
        /// <param name="officerCommands">Officer command appointment operations.</param>
        /// <param name="results">Completed game changes that can invalidate incremental execution state.</param>
        public AIDirector(
            GameRoot game,
            MissionCommands missions,
            MovementCommands movement,
            ManufacturingCommands manufacturing,
            BombardmentCommands bombardment,
            PlanetaryAssaultCommands planetaryAssault,
            IRandomNumberProvider random,
            MaintenanceCommands maintenance = null,
            HeadquartersCommands headquarters = null,
            OfficerCommandCommands officerCommands = null,
            GameResultBus results = null
        )
        {
            _game = game;
            _fogOfWar = game == null ? null : new FogOfWarQueries(game);
            _random = random;
            _missions = missions;
            _missionQueries = game == null ? null : new MissionQueries(game);
            _missionEncounterCache = new MissionEncounterOdds.Cache(
                game?.Config?.AI?.MissionPlanning?.EncounterCacheCapacity ?? 0
            );
            _movement = movement;
            _headquarters = headquarters;
            _officerCommands = officerCommands;
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
                        new AIHeadquartersRelocationPlanner(),
                        new AIMissionPlanner(),
                        new AIOrbitalEngagementPlanner(),
                        new AIFleetPlanner(),
                    }
                ),
                new AIDemandGenerationPhase(new IAIDemandGenerator[] { productionDemandGenerator }),
                new AIPlanningPhase(
                    new IAIProposalPlanner[]
                    {
                        productionPlanner,
                        new AIFacilityRemovalPlanner(),
                        new AIMaintenanceRecoveryPlanner(),
                    }
                ),
                new AIScoringPhase(),
                new AISelectionPhase(),
                new AIExecutionPhase(results),
            };
        }

        /// <summary>
        /// Returns whether this session registered an order as optional work.
        /// </summary>
        /// <param name="order">The manufacturing order to inspect.</param>
        /// <returns>True when the order may yield to demanded production.</returns>
        public bool IsOptionalProduction(IManufacturable order)
        {
            return order is ISceneNode node && _optionalProductionOrders.Contains(node.InstanceID);
        }

        /// <summary>
        /// Records production intent in memory for the current game session.
        /// </summary>
        /// <param name="order">The queued order whose priority changed.</param>
        /// <param name="optional">Whether the order may yield to demanded work.</param>
        private void RecordProductionPriority(IManufacturable order, bool optional)
        {
            if (order is not ISceneNode node)
                return;
            if (optional)
                _optionalProductionOrders.Add(node.InstanceID);
            else
                _optionalProductionOrders.Remove(node.InstanceID);
        }

        /// <summary>
        /// Releases bookkeeping for orders that have left active construction.
        /// </summary>
        private void PruneProductionPriorities()
        {
            _optionalProductionOrders.RemoveWhere(id =>
                _game?.GetSceneNodeByInstanceID<ISceneNode>(id) is not IManufacturable order
                || order.ManufacturingStatus != ManufacturingStatus.Building
                || order.IsManufacturingComplete()
            );
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
                PruneProductionPriorities();
                AIAssessment assessment = new AIAssessment(
                    _game,
                    faction,
                    factionView,
                    IsOptionalProduction
                );
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
                    _maintenance,
                    _headquarters,
                    _officerCommands,
                    new MissionQueries(
                        _game,
                        assessment.GetMissionDetectorCommander,
                        _missionEncounterCache
                    ),
                    RecordProductionPriority
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
