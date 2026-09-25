using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances captive escape attempts during a game tick.
    /// </summary>
    internal sealed class CaptiveTickProcessor : ITickProcessor
    {
        private readonly CaptiveCommands _commands;

        /// <summary>
        /// Creates captive tick processing.
        /// </summary>
        /// <param name="commands">The custody and escape operations.</param>
        public CaptiveTickProcessor(CaptiveCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Resolves every captive escape attempt due this tick.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The captive-state results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (Officer officer in game.GetSceneNodesByType<Officer>())
                _commands.ProcessEscapeAttempt(officer, results);

            return results;
        }
    }
}
