using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances Force discovery and detection during a game tick.
    /// </summary>
    internal sealed class JediTickProcessor : ITickProcessor
    {
        private readonly JediCommands _commands;

        /// <summary>
        /// Creates Jedi tick processing.
        /// </summary>
        /// <param name="commands">The Force growth and discovery operations.</param>
        public JediTickProcessor(JediCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Advances discovery state and scans for hidden Force users.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The Force-related results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in game.GetSceneNodesByType<Officer>())
                _commands.UpdateForceDiscoveryState(officer, results);

            _commands.ScanForHiddenForceUsers(results);
            return results;
        }
    }
}
