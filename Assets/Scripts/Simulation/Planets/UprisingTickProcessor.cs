using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances planetary uprisings during a game tick.
    /// </summary>
    internal sealed class UprisingTickProcessor : ITickProcessor
    {
        private readonly UprisingCommands _commands;

        /// <summary>
        /// Creates uprising tick processing.
        /// </summary>
        /// <param name="commands">The uprising operations and garrison state.</param>
        public UprisingTickProcessor(UprisingCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Reconciles garrisons and resolves active uprisings.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The uprising results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
                _commands.ProcessPlanet(planet, results);

            return results;
        }
    }
}
