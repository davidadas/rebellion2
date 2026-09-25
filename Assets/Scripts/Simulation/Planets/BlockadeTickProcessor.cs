using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Reconciles planetary blockade state during a game tick.
    /// </summary>
    internal sealed class BlockadeTickProcessor : ITickProcessor
    {
        private readonly BlockadeCommands _commands;

        /// <summary>
        /// Creates blockade tick processing.
        /// </summary>
        /// <param name="commands">The blockade operations and tracked blockade state.</param>
        public BlockadeTickProcessor(BlockadeCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Detects blockade transitions across the galaxy.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The blockade results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            HashSet<string> currentBlockades = _commands.DetectBlockadedPlanets();
            _commands.ApplyBlockadeStatus(currentBlockades, results);
            _commands.ClearBlockadeStatus(currentBlockades, results);
            _commands.RememberBlockades(currentBlockades);
            return results;
        }
    }
}
