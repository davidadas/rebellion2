using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Runs AI faction turns after world systems finish each tick.
    /// </summary>
    public class AISystem
    {
        private readonly GameRoot _game;
        private readonly FogOfWarSystem _fogOfWar;
        private readonly AIDirector _director;

        /// <summary>
        /// Creates an AI system.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="missionManager">The mission system.</param>
        /// <param name="movementManager">The movement system.</param>
        /// <param name="manufacturingManager">The manufacturing system.</param>
        /// <param name="bombardmentSystem">The bombardment system.</param>
        /// <param name="planetaryAssaultSystem">The planetary-assault system.</param>
        /// <param name="randomProvider">Random number provider for AI choices.</param>
        /// <param name="fogOfWarManager">Fog-of-war system used to limit AI knowledge.</param>
        public AISystem(
            GameRoot game,
            MissionSystem missionManager,
            MovementSystem movementManager,
            ManufacturingSystem manufacturingManager,
            BombardmentSystem bombardmentSystem,
            PlanetaryAssaultSystem planetaryAssaultSystem,
            IRandomNumberProvider randomProvider,
            FogOfWarSystem fogOfWarManager
        )
        {
            _game = game;
            _fogOfWar = fogOfWarManager;
            _director = new AIDirector(
                game,
                missionManager,
                movementManager,
                manufacturingManager,
                bombardmentSystem,
                planetaryAssaultSystem,
                randomProvider
            );
        }

        /// <summary>
        /// Processes AI turns for all AI-controlled factions.
        /// </summary>
        /// <returns>An empty result list.</returns>
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

            foreach (Faction faction in _game.GetFactions().Where(f => f.IsAIControlled()))
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
