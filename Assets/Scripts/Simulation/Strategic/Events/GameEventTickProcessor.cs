using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances scheduled game events during their ordered tick phase.
    /// </summary>
    internal sealed class GameEventTickProcessor : ITickProcessor
    {
        private readonly GameEventCommands _commands;

        /// <summary>
        /// Creates scheduled game-event tick processing.
        /// </summary>
        /// <param name="commands">The game-event operations for the active game.</param>
        public GameEventTickProcessor(GameEventCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Applies scheduled events that are eligible during the current tick.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The results produced by activated events.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            return _commands.ProcessScheduledEvents(game.GetEventPool());
        }
    }
}
