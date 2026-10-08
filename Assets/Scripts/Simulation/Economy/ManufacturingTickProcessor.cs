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
            List<GameResult> results = new List<GameResult>();
            foreach (IReadOnlyList<GameResult> step in ProcessTickIncrementally(game))
                results.AddRange(step);

            return results;
        }

        /// <summary>
        /// Advances manufacturing while preserving a presentation boundary after each completed
        /// production point.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The ordered result batches produced during the tick.</returns>
        internal IEnumerable<IReadOnlyList<GameResult>> ProcessTickIncrementally(GameRoot game)
        {
            List<GameResult> pendingResults = _commands.TakePendingResults();
            if (pendingResults.Count > 0)
                yield return pendingResults;

            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
            {
                foreach (
                    IReadOnlyList<GameResult> step in _commands.ProcessPlanetManufacturingIncrementally(
                        planet
                    )
                )
                    yield return step;
            }
        }
    }
}
