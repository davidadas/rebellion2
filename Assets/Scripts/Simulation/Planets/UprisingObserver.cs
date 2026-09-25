using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>Routes garrison changes to uprising reconciliation in incoming planet order.</summary>
    public sealed class UprisingObserver : IResultObserver, IDisposable
    {
        private readonly UprisingCommands _commands;
        private IDisposable _subscription;

        /// <summary>Creates the uprising garrison listener.</summary>
        /// <param name="commands">The uprising operations for this game.</param>
        public UprisingObserver(UprisingCommands commands)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>Registers the garrison-change callback with the result bus.</summary>
        /// <param name="results">The bus that delivers garrison changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException("Uprising observer is already connected.");
            _subscription = (
                results ?? throw new ArgumentNullException(nameof(results))
            ).Subscribe<PlanetGarrisonChangedResult>(HandleResults);
        }

        /// <summary>Stops receiving garrison changes.</summary>
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Reconciles uprising state for planets whose active garrisons changed.
        /// </summary>
        /// <param name="results">The result batch to inspect.</param>
        /// <returns>Any uprising results caused by the garrison changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetGarrisonChangedResult> results)
        {
            List<GameResult> uprisingResults = new List<GameResult>();
            if (results == null)
                return uprisingResults;

            IEnumerable<Planet> affectedPlanets = results
                .Select(result => result.Planet)
                .Where(planet => planet != null)
                .Distinct();
            foreach (Planet planet in affectedPlanets)
                uprisingResults.AddRange(_commands.ReconcileGarrison(planet));

            return uprisingResults;
        }
    }
}
