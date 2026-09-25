using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Phases;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.AI.Director
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
        /// Creates an AI director using the current game systems.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="missions">Mission commands used by mission proposals.</param>
        /// <param name="missionQueries">Mission eligibility and odds used by mission proposals.</param>
        /// <param name="movement">Movement system used by movement proposals.</param>
        /// <param name="manufacturing">Manufacturing system used by production proposals.</param>
        /// <param name="bombardment">Bombardment commands used by fleet attack proposals.</param>
        /// <param name="bombardmentQueries">Bombardment eligibility rules used by fleet attack proposals.</param>
        /// <param name="planetaryAssault">Planetary-assault commands used by fleet attack proposals.</param>
        /// <param name="planetaryAssaultQueries">Assault eligibility rules used by fleet attack proposals.</param>
        /// <param name="random">RNG provider used by probabilistic AI decisions.</param>
        /// <param name="fogOfWar">Builds each faction's permitted view before its turn.</param>
        /// <param name="maintenance">Maintenance commands used to scrap surplus facilities.</param>
        public AIDirector(
            GameRoot game,
            MissionCommands missions,
            MissionQueries missionQueries,
            MovementCommands movement,
            ManufacturingCommands manufacturing,
            BombardmentCommands bombardment,
            BombardmentQueries bombardmentQueries,
            PlanetaryAssaultCommands planetaryAssault,
            PlanetaryAssaultQueries planetaryAssaultQueries,
            IRandomNumberProvider random,
            FogOfWarQueries fogOfWar,
            MaintenanceCommands maintenance = null
        )
        {
            _game = game;
            _fogOfWar = fogOfWar;
            _random = random;
            _missions = missions;
            _missionQueries = missionQueries;
            _movement = movement;
            _manufacturing = manufacturing;
            _maintenance = maintenance;
            _bombardment = bombardment;
            _bombardmentQueries = bombardmentQueries;
            _planetaryAssault = planetaryAssault;
            _planetaryAssaultQueries = planetaryAssaultQueries;
            _turnPhases = new List<IAITurnPhase>
            {
                new AISpecialForcesIntentPhase(),
                new AIPlanningPhase(),
                new AIScoringPhase(),
                new AISelectionPhase(),
                new AIMissionDecoyAssignmentPhase(),
                new AIExecutionPhase(),
            };
        }

        /// <summary>
        /// Processes AI turns for all AI-controlled factions.
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
                foreach (object step in ProcessFactionIncrementally(faction, factionView, results))
                    yield return step;
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
            AITurnContext context = new AITurnContext(
                _game,
                faction,
                _missions,
                _missionQueries,
                _movement,
                _manufacturing,
                _bombardment,
                _bombardmentQueries,
                _planetaryAssault,
                _planetaryAssaultQueries,
                _random,
                factionView,
                _maintenance
            );
            yield return null;

            foreach (IAITurnPhase phase in _turnPhases)
            {
                if (phase is IAIIncrementalTurnPhase incrementalPhase)
                {
                    foreach (object step in incrementalPhase.ExecuteIncrementally(context))
                        yield return step;
                }
                else
                    phase.Execute(context);

                yield return null;
            }

            foreach (GameResult result in context.Results)
                results.Add(result);
        }
    }
}
