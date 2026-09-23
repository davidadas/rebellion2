using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances manufacturing queues during a game tick.
    /// </summary>
    internal sealed class ManufacturingTickProcessor : ITickProcessor
    {
        private readonly ManufacturingCommands _commands;

        /// <summary>
        /// Creates manufacturing tick processing.
        /// </summary>
        /// <param name="commands">The manufacturing operations and pending results.</param>
        public ManufacturingTickProcessor(ManufacturingCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Advances every active manufacturing queue.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The manufacturing results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = _commands.TakePendingResults();
            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
                results.AddRange(_commands.ProcessPlanetManufacturing(planet));

            return results;
        }
    }
}
