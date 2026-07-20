using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Runs AI faction turns after world systems finish each tick.
    /// </summary>
    public class AISystem : IGameSystem
    {
        private readonly GameRoot _game;
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
            _director = new AIDirector(
                game,
                missionManager,
                movementManager,
                manufacturingManager,
                bombardmentSystem,
                planetaryAssaultSystem,
                randomProvider,
                fogOfWarManager
            );
        }

        /// <summary>
        /// Processes AI turns for all AI-controlled factions.
        /// </summary>
        /// <returns>An empty result list.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            int tickInterval = _game.Config.AI.TickInterval;
            if (tickInterval <= 0 || _game.CurrentTick % tickInterval != 0)
                return results;

            foreach (Faction faction in _game.Factions.Where(f => f.IsAIControlled()))
            {
                results.AddRange(_director.ProcessFaction(faction));
            }

            return results;
        }
    }
}
