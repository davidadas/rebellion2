using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Evaluates notifications only for planets affected by completed simulation changes.
    /// Histories are initialized from the loaded world and are never part of a save or checkpoint.
    /// </summary>
    public sealed class PlanetNotificationObserver
        : IResultObserver,
            IObserver<Planet>,
            IObserver<(ISceneNode Node, Planet Origin, Planet Destination)>,
            IDisposable
    {
        private readonly GameRoot _game;
        private readonly HashSet<Planet> _changedPlanets = new();
        private readonly HashSet<PlanetSector> _changedSectors = new();
        private readonly HashSet<Planet> _subscribedPlanets = new();
        private readonly SmugglingCommands _smuggling;
        private readonly UprisingResolver _uprising;
        private IDisposable _subscription;

        /// <summary>
        /// Gets whether changed model facts still await notification selection at the operation boundary.
        /// </summary>
        internal bool HasPendingChanges => _changedPlanets.Count > 0 || _changedSectors.Count > 0;

        /// <summary>
        /// Connects notification selection to the existing smuggling and garrison rules.
        /// </summary>
        /// <param name="game">The world supplying membership and support change observables.</param>
        /// <param name="smuggling">The smuggling rules and observed relationship baseline.</param>
        /// <param name="uprising">The garrison rules and observed warning baseline.</param>
        public PlanetNotificationObserver(
            GameRoot game,
            SmugglingCommands smuggling,
            UprisingResolver uprising
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _smuggling = smuggling ?? throw new ArgumentNullException(nameof(smuggling));
            _uprising = uprising ?? throw new ArgumentNullException(nameof(uprising));
        }

        /// <summary>
        /// Observes complete reaction batches so notifications describe their settled world.
        /// </summary>
        /// <param name="results">The session's result bus.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscription != null)
                throw new InvalidOperationException("Planet notifications are already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            // Resolve the planet subscriptions once when the session is connected.
            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
                SubscribePlanet(planet);
            _game.PresenceObserver = this;
            _subscription = results.ObserveAll(batch =>
            {
                List<GameResult> notifications = HandleResults(batch);
                if (notifications.Count > 0)
                    results.Publish(notifications);
            });
        }

        /// <summary>
        /// Stops receiving completed changes when the session ends.
        /// </summary>
        public void Dispose()
        {
            _subscription?.Dispose();
            if (ReferenceEquals(_game.PresenceObserver, this))
                _game.PresenceObserver = null;
            foreach (Planet planet in _subscribedPlanets)
                if (ReferenceEquals(planet.SupportObserver, this))
                    planet.SupportObserver = null;
            _subscribedPlanets.Clear();
            _changedPlanets.Clear();
            _changedSectors.Clear();
        }

        /// <summary>
        /// Reconciles only locations whose support, ownership, or stationed forces changed.
        /// Sector changes include the other planets affected by suppression ships.
        /// </summary>
        /// <param name="results">The settled result batch.</param>
        /// <returns>New smuggling and near-uprising notification facts.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameResult> results)
        {
            foreach (GameResult result in results ?? Array.Empty<GameResult>())
            {
                switch (result)
                {
                    case GameObjectCreatedResult { GameObject: Planet planet }:
                        SubscribePlanet(planet);
                        _changedPlanets.Add(planet);
                        break;
                    case GameObjectDeployedResult deployed:
                        ObserveEntityChange(deployed.GameObject);
                        break;
                    case ManufacturingDeployedResult deployed:
                        ObserveEntityChange(deployed.DeployedObject);
                        break;
                    case GameObjectEnrouteResult moving:
                        ObserveEntityChange(moving.GameObject);
                        break;
                    case GameObjectEnrouteActiveResult moving:
                        ObserveEntityChange(moving.GameObject);
                        break;
                    case UnitArrivedResult arrived:
                        ObserveEntityChange(arrived.Unit);
                        _changedPlanets.Add(arrived.Destination);
                        break;
                    case PlanetUprisingStartedResult started:
                        _changedPlanets.Add(started.Planet);
                        break;
                    case PlanetUprisingEndedResult ended:
                        _changedPlanets.Add(ended.Planet);
                        break;
                    case BombardmentResult { PlanetDestroyed: true } bombardment:
                        AddSector(bombardment.Planet, _changedPlanets, _changedSectors);
                        break;
                }
            }
            if (!HasPendingChanges)
                return new List<GameResult>();
            foreach (PlanetSector sector in _changedSectors)
                _changedPlanets.UnionWith(sector.GetChildren<Planet>(includeDisabled: true));
            _changedSectors.Clear();
            Planet[] changed = _changedPlanets
                .Where(planet => planet != null)
                .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .ToArray();
            _changedPlanets.Clear();
            List<GameResult> notifications = new();
            foreach (Planet planet in changed)
            {
                SubscribePlanet(planet);
                _smuggling.RefreshPlanet(planet, notifications);
                _uprising.RecordGarrisonWarning(planet, notifications);
            }
            return notifications;
        }

        /// <summary>
        /// Includes a changed location and its sector after combat, displacement, or suppression changes.
        /// </summary>
        /// <param name="planet">The changed location.</param>
        /// <param name="planets">The affected local planets.</param>
        /// <param name="sectors">The affected suppression sectors.</param>
        private static void AddSector(
            Planet planet,
            ISet<Planet> planets,
            ISet<PlanetSector> sectors
        )
        {
            if (planet == null)
                return;
            planets.Add(planet);
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            if (sector != null)
                sectors.Add(sector);
        }

        /// <summary>
        /// Subscribes a live planet once, including planets created after session initialization.
        /// </summary>
        /// <param name="planet">The planet whose support changes must be observed.</param>
        private void SubscribePlanet(Planet planet)
        {
            if (_subscribedPlanets.Add(planet))
                planet.SupportObserver = this;
        }

        /// <summary>
        /// Retains only the affected planet until the enclosing operation has settled.
        /// </summary>
        /// <param name="planet">The planet whose support changed.</param>
        void IObserver<Planet>.OnNext(Planet planet) => _changedPlanets.Add(planet);

        /// <summary>
        /// Records an ownership, availability, or position change before the result batch settles.
        /// </summary>
        /// <param name="change">The affected node and its locations.</param>
        void IObserver<(ISceneNode Node, Planet Origin, Planet Destination)>.OnNext(
            (ISceneNode Node, Planet Origin, Planet Destination) change
        ) => ObservePresenceChange(change.Node, change.Origin, change.Destination);

        /// <summary>
        /// Ends the stream when its publisher completes.
        /// </summary>
        public void OnCompleted() => Dispose();

        /// <summary>
        /// Ends the stream and propagates a publisher failure.
        /// </summary>
        /// <param name="error">The publisher failure.</param>
        public void OnError(Exception error)
        {
            Dispose();
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        /// <summary>
        /// Captures precise old and new locations when membership or ownership changes.
        /// Suppression ships can also change garrison requirements throughout those sectors.
        /// </summary>
        /// <param name="node">The changed unit or planet.</param>
        /// <param name="origin">The previous planet, or null for a new node.</param>
        /// <param name="destination">The new planet, or null after removal.</param>
        private void ObservePresenceChange(ISceneNode node, Planet origin, Planet destination)
        {
            if (node is PlanetSector sector)
            {
                _changedSectors.Add(sector);
                return;
            }
            if (node is GalaxyMap galaxy)
            {
                _changedSectors.UnionWith(galaxy.GetChildren<PlanetSector>(includeDisabled: true));
                return;
            }
            if (node is not (Planet or Building or Fleet or CapitalShip or Starfighter or Regiment))
                return;
            if (origin == null && destination == null)
                return;
            if (node is Planet planet)
                SubscribePlanet(planet);
            _changedPlanets.Add(origin);
            _changedPlanets.Add(destination);
            if (
                node is CapitalShip { SectorSuppression: > 0 }
                || node is Fleet fleet
                    && fleet
                        .GetChildren<CapitalShip>(includeDisabled: true)
                        .Any(ship => ship.SectorSuppression > 0)
            )
            {
                AddSector(origin, _changedPlanets, _changedSectors);
                AddSector(destination, _changedPlanets, _changedSectors);
            }
        }

        /// <summary>
        /// Observes completed changes to transit or manufacturing status on an attached unit.
        /// </summary>
        /// <param name="entity">The unit whose readiness changed.</param>
        private void ObserveEntityChange(IGameEntity entity)
        {
            if (entity is ISceneNode node)
            {
                Planet planet = node as Planet ?? node.GetParentOfType<Planet>();
                ObservePresenceChange(node, planet, planet);
            }
        }
    }
}
