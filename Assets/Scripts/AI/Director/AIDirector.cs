using System.Collections.Generic;
using Rebellion.AI.Phases;
using Rebellion.Game;
using Rebellion.Game.Commands;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Util.Random;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Coordinates the AI turn phases for each faction.
    /// </summary>
    public sealed class AIDirector
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _random;
        private readonly IGameCommandExecutor _commands;
        private readonly IGameQueries _queries;
        private readonly IReadOnlyList<IAITurnPhase> _turnPhases;

        /// <summary>
        /// Creates an AI director using the current game systems.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="commands">Executes authoritative AI decisions.</param>
        /// <param name="queries">Answers simulation questions for AI planning.</param>
        /// <param name="random">RNG provider used by probabilistic AI decisions.</param>
        public AIDirector(
            GameRoot game,
            IGameCommandExecutor commands,
            IGameQueries queries,
            IRandomNumberProvider random
        )
        {
            _game = game;
            _random = random;
            _commands = commands;
            _queries = queries;
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
                _commands,
                _queries,
                _random,
                factionView
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
