using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances unit movement during a game tick.
    /// </summary>
    internal sealed class MovementTickProcessor : ITickProcessor
    {
        private readonly MovementCommands _commands;

        /// <summary>
        /// Creates movement tick processing.
        /// </summary>
        /// <param name="commands">The movement operations and pending results.</param>
        public MovementTickProcessor(MovementCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Advances every movable unit currently in transit.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The movement results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = _commands.TakePendingResults();
            game.GetGalaxyMap()
                .Traverse(node =>
                {
                    if (node is IMovable movable)
                        _commands.UpdateMovement(movable, results);
                });
            return results;
        }
    }
}
