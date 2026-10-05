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
        private readonly UprisingResolver _resolver;

        /// <summary>
        /// Creates uprising tick processing.
        /// </summary>
        /// <param name="resolver">The uprising lifecycle resolver and garrison state.</param>
        public UprisingTickProcessor(UprisingResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
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
                _resolver.ProcessPlanet(planet, results);

            return results;
        }
    }
}
