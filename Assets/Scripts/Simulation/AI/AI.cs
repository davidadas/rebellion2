using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Runs AI faction turns after world systems finish each tick.
    /// </summary>
    internal class AI
    {
        private readonly GameRoot _game;
        private readonly FogOfWar _fogOfWar;
        private readonly AIDirector _director;

        /// <summary>
        /// Creates an AI system.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="commands">Executes authoritative AI decisions.</param>
        /// <param name="queries">Answers simulation questions for AI planning.</param>
        /// <param name="randomProvider">Random number provider for AI choices.</param>
        /// <param name="fogOfWarManager">Fog-of-war system used to limit AI knowledge.</param>
        public AI(
            GameRoot game,
            GameCommands commands,
            GameQueries queries,
            IRandomNumberProvider randomProvider,
            FogOfWar fogOfWarManager
        )
        {
            _game = game;
            _fogOfWar = fogOfWarManager;
            _director = new AIDirector(game, commands, queries, randomProvider);
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
                foreach (
                    object step in _director.ProcessFactionIncrementally(
                        faction,
                        factionView,
                        results
                    )
                )
                    yield return step;
            }
        }
    }
}
