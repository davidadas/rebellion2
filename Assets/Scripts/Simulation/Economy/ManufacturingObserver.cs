using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>Routes production-facility losses to cancellation of unsupported manufacturing lanes.</summary>
    public sealed class ManufacturingObserver : IDisposable
    {
        private readonly ManufacturingCommands _commands;
        private readonly IDisposable[] _subscriptions;

        /// <summary>Creates the manufacturing result listener.</summary>
        /// <param name="commands">The queue operations for this game.</param>
        /// <param name="results">The bus that delivers manufacturing-loss results.</param>
        public ManufacturingObserver(ManufacturingCommands commands, GameResultBus results)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<GameObjectDestroyedResult>(HandleResults),
                results.Subscribe<GameObjectScrappedResult>(HandleResults),
                results.Subscribe<BombardmentResult>(HandleResults),
                results.Subscribe<PlanetaryAssaultResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving manufacturing-loss results.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions)
                subscription.Dispose();
        }

        /// <summary>
        /// Cancels affected production lanes after individually reported buildings are destroyed.
        /// </summary>
        /// <param name="results">The destruction results to inspect.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectDestroyedResult> results)
        {
            if (results == null)
                return new List<GameResult>();

            foreach (
                IGrouping<Planet, GameObjectDestroyedResult> planetResults in results
                    .Where(result =>
                        result?.DestroyedObject is Building && result.Context is Planet
                    )
                    .GroupBy(result => (Planet)result.Context)
            )
            {
                _commands.CancelUnsupportedProduction(
                    planetResults.Key,
                    planetResults.Select(result => (Building)result.DestroyedObject)
                );
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Cancels affected production lanes after production buildings are intentionally scrapped.
        /// </summary>
        /// <param name="results">The scrapping results to inspect.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectScrappedResult> results)
        {
            if (results == null)
                return new List<GameResult>();

            foreach (
                IGrouping<Planet, GameObjectScrappedResult> planetResults in results
                    .Where(result => result?.ScrappedObject is Building && result.Context is Planet)
                    .GroupBy(result => (Planet)result.Context)
            )
            {
                _commands.CancelUnsupportedProduction(
                    planetResults.Key,
                    planetResults.Select(result => (Building)result.ScrappedObject)
                );
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Cancels affected production lanes after orbital bombardment destroys buildings.
        /// </summary>
        /// <param name="results">The bombardment results to inspect.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<BombardmentResult> results)
        {
            if (results != null)
            {
                foreach (BombardmentResult result in results)
                    _commands.CancelUnsupportedProduction(
                        result?.Planet,
                        result?.DestroyedBuildings
                    );
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Cancels affected production lanes after a planetary assault destroys buildings.
        /// </summary>
        /// <param name="results">The assault results to inspect.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetaryAssaultResult> results)
        {
            if (results != null)
            {
                foreach (PlanetaryAssaultResult result in results)
                    _commands.CancelUnsupportedProduction(
                        result?.Planet,
                        result?.CollateralDestroyedBuildings
                    );
            }

            return new List<GameResult>();
        }
    }
}
