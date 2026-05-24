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
        /// Creates an AI system without combat access.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="missionManager">The mission system.</param>
        /// <param name="movementManager">The movement system.</param>
        /// <param name="manufacturingManager">The manufacturing system.</param>
        /// <param name="randomProvider">Random number provider for AI choices.</param>
        public AISystem(
            GameRoot game,
            MissionSystem missionManager,
            MovementSystem movementManager,
            ManufacturingSystem manufacturingManager,
            IRandomNumberProvider randomProvider
        )
            : this(
                game,
                missionManager,
                movementManager,
                manufacturingManager,
                combatManager: null,
                randomProvider
            ) { }

        /// <summary>
        /// Creates an AI system.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="missionManager">The mission system.</param>
        /// <param name="movementManager">The movement system.</param>
        /// <param name="manufacturingManager">The manufacturing system.</param>
        /// <param name="combatManager">The combat system.</param>
        /// <param name="randomProvider">Random number provider for AI choices.</param>
        public AISystem(
            GameRoot game,
            MissionSystem missionManager,
            MovementSystem movementManager,
            ManufacturingSystem manufacturingManager,
            CombatSystem combatManager,
            IRandomNumberProvider randomProvider
        )
        {
            _game = game;
            _director = new AIDirector(
                game,
                missionManager,
                movementManager,
                manufacturingManager,
                combatManager,
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

            foreach (Faction faction in _game.Factions.Where(f => f.IsAIControlled()))
            {
                _director.ProcessFaction(faction);
            }

            return results;
        }
    }
}
