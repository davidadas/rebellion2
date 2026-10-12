using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Reconciles planetary blockade state during a game tick.
    /// </summary>
    internal sealed class BlockadeTickProcessor : ITickProcessor
    {
        private readonly BlockadeTracker _tracker;

        /// <summary>
        /// Creates blockade tick processing.
        /// </summary>
        /// <param name="tracker">The blockade transition tracker.</param>
        public BlockadeTickProcessor(BlockadeTracker tracker)
        {
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        }

        /// <summary>
        /// Detects blockade transitions across the galaxy.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The blockade results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            HashSet<string> currentBlockades = _tracker.DetectBlockadedPlanets();
            _tracker.ApplyBlockadeStatus(currentBlockades, results);
            _tracker.ClearBlockadeStatus(currentBlockades, results);
            _tracker.RememberBlockades(currentBlockades);
            return results;
        }

        /// <summary>
        /// Observes planets whose orbital movement or combat just settled.
        /// </summary>
        /// <param name="planets">The affected planets to observe.</param>
        /// <returns>The newly observed blockade starts without changing tracked state.</returns>
        internal IReadOnlyList<BlockadeChangedResult> ObservePlanets(IEnumerable<Planet> planets)
        {
            return _tracker.ObservePlanets(planets);
        }
    }
}
