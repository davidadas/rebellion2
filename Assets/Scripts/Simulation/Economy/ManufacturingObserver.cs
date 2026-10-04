using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>Routes completed game changes to the corresponding manufacturing operations.</summary>
    public sealed class ManufacturingObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private readonly ManufacturingCommands _commands;
        private IDisposable[] _subscriptions;

        /// <summary>Creates the manufacturing result listener.</summary>
        /// <param name="game">The active game graph containing manufacturing queues.</param>
        /// <param name="commands">The queue operations for this game.</param>
        public ManufacturingObserver(GameRoot game, ManufacturingCommands commands)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>Registers manufacturing-loss callbacks with the result bus.</summary>
        /// <param name="results">The bus that delivers manufacturing-loss results.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException("Manufacturing observer is already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<GameObjectDestroyedResult>(HandleResults),
                results.Subscribe<GameObjectScrappedResult>(HandleResults),
                results.Subscribe<BombardmentResult>(HandleResults),
                results.Subscribe<PlanetaryAssaultResult>(HandleResults),
                results.Subscribe<PlanetOwnershipChangedResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving manufacturing-loss results.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
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
                ClearUnsupportedProduction(
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
                ClearUnsupportedProduction(
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
                    ClearUnsupportedProduction(result?.Planet, result?.DestroyedBuildings);
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
                    ClearUnsupportedProduction(
                        result?.Planet,
                        result?.CollateralDestroyedBuildings
                    );
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Cancels manufacturing work that is no longer valid after planetary ownership changes.
        /// </summary>
        /// <param name="results">The completed ownership changes to inspect.</param>
        /// <returns>No additional results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                CancelOrdersAssignedToPlanet(result?.Planet, result?.NewOwner?.InstanceID);
                ClearPlanetQueues(result?.Planet);
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Cancels orders from incompatible producers that are assigned directly to a planet.
        /// </summary>
        /// <param name="destination">The planet receiving the manufactured units.</param>
        /// <param name="newOwnerInstanceId">The planet's current owner.</param>
        private void CancelOrdersAssignedToPlanet(Planet destination, string newOwnerInstanceId)
        {
            if (destination == null)
                return;

            foreach (Planet producer in _game.GetSceneNodesByType<Planet>())
            {
                string producerOwnerInstanceId = producer.GetOwnerInstanceID();
                if (
                    string.Equals(
                        producerOwnerInstanceId,
                        newOwnerInstanceId,
                        StringComparison.Ordinal
                    )
                )
                {
                    continue;
                }

                List<IManufacturable> invalidOrders = producer
                    .GetManufacturingQueue()
                    .Values.Where(items => items != null)
                    .SelectMany(items => items)
                    .Where(item =>
                        item is ISceneNode sceneNode
                        && item is not CapitalShip
                        && item is not Starfighter
                        && (
                            ReferenceEquals(sceneNode.GetParent(), destination)
                            || ReferenceEquals(sceneNode.GetLastParent(), destination)
                        )
                    )
                    .ToList();
                _commands.CancelManufacturing(invalidOrders, producerOwnerInstanceId);
            }
        }

        /// <summary>Clears every populated manufacturing lane on a planet.</summary>
        /// <param name="planet">The planet whose manufacturing lanes are cleared.</param>
        private void ClearPlanetQueues(Planet planet)
        {
            if (planet == null)
                return;

            foreach (ManufacturingType type in planet.GetManufacturingQueue().Keys.ToList())
                _commands.ClearQueue(planet, type);
        }

        /// <summary>
        /// Clears lanes whose final operational production facility was destroyed or scrapped.
        /// </summary>
        /// <param name="planet">The planet where production facilities were lost.</param>
        /// <param name="removedBuildings">The production facilities removed from the planet.</param>
        private void ClearUnsupportedProduction(
            Planet planet,
            IEnumerable<Building> removedBuildings
        )
        {
            if (planet == null || removedBuildings == null)
                return;

            foreach (
                ManufacturingType type in removedBuildings
                    .Where(building =>
                        building != null
                        && building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.Movement == null
                        && building.ProcessRate > 0
                    )
                    .Select(building => building.ProductionType)
                    .Where(type => type != ManufacturingType.None)
                    .Distinct()
            )
            {
                bool hasProducer = planet
                    .GetChildren<Building>()
                    .Any(facility =>
                        facility.ProductionType == type
                        && facility.ManufacturingStatus == ManufacturingStatus.Complete
                        && facility.Movement == null
                        && facility.ProcessRate > 0
                    );
                if (!hasProducer)
                    _commands.ClearQueue(planet, type);
            }
        }
    }
}
