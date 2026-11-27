using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class PlanetNotificationObserver : IResultObserver, IDisposable
    {
        private readonly SmugglingCommands _smuggling;
        private readonly UprisingResolver _uprising;
        private IDisposable _subscription;

        /// <summary>
        /// Connects notification selection to the existing smuggling and garrison rules.
        /// </summary>
        /// <param name="smuggling">The smuggling rules and observed relationship baseline.</param>
        /// <param name="uprising">The garrison rules and observed warning baseline.</param>
        public PlanetNotificationObserver(SmugglingCommands smuggling, UprisingResolver uprising)
        {
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
        public void Dispose() => _subscription?.Dispose();

        /// <summary>
        /// Reconciles only locations whose support, ownership, or stationed forces changed.
        /// Sector changes include the other planets affected by suppression ships.
        /// </summary>
        /// <param name="results">The settled result batch.</param>
        /// <returns>New smuggling and near-uprising notification facts.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameResult> results)
        {
            if (results == null || results.Count == 0)
                return new List<GameResult>();

            HashSet<Planet> planets = new();
            HashSet<PlanetSector> sectors = new();
            foreach (GameResult result in results)
            {
                switch (result)
                {
                    case PlanetSupportChangedResult support:
                        planets.Add(support.Planet);
                        break;
                    case PlanetStatChangedResult { Category: PlanetChangeCategory.Loyalty } support:
                        planets.Add(support.Planet);
                        break;
                    case PlanetOwnershipChangedResult ownership:
                        planets.Add(ownership.Planet);
                        break;
                    case PlanetGarrisonChangedResult garrison:
                        planets.Add(garrison.Planet);
                        break;
                    case UnitOwnershipChangedResult ownership:
                        AddCurrentLocation(ownership.Unit, planets, sectors);
                        break;
                    case SceneNodeActivationChangedResult activation:
                        AddCurrentLocation(activation.Node, planets, sectors);
                        break;
                    case GameObjectCreatedResult created:
                        AddCurrentLocation(created.GameObject, planets, sectors);
                        break;
                    case FleetFormedResult formed:
                        AddCurrentLocation(formed.Fleet, planets, sectors);
                        break;
                    case GameObjectDeployedResult deployed:
                        AddCurrentLocation(deployed.GameObject, planets, sectors);
                        break;
                    case ManufacturingDeployedResult deployed:
                        AddCurrentLocation(deployed.DeployedObject, planets, sectors);
                        break;
                    case UnitPlacedResult placed:
                        AddCurrentLocation(placed.Unit, planets, sectors, placed.PreviousPlanet);
                        break;
                    case GameObjectEnrouteResult moving:
                        AddCurrentLocation(
                            moving.GameObject,
                            planets,
                            sectors,
                            moving.PreviousPlanet
                        );
                        break;
                    case GameObjectEnrouteActiveResult moving:
                        AddCurrentLocation(moving.GameObject, planets, sectors);
                        break;
                    case UnitArrivedResult arrived:
                        AddCurrentLocation(arrived.Unit, planets, sectors);
                        planets.Add(arrived.Destination);
                        break;
                    case GameObjectDestroyedResult { DestroyedObject: ISceneNode node } destroyed:
                        AddLocations(
                            node,
                            destroyed.Location ?? destroyed.Context as Planet,
                            null,
                            planets,
                            sectors
                        );
                        break;
                    case GameObjectScrappedResult { ScrappedObject: ISceneNode node } scrapped:
                        AddLocations(node, scrapped.Context as Planet, null, planets, sectors);
                        break;
                    case EvacuationLossesResult evacuation:
                        planets.Add(evacuation.Location);
                        if (evacuation.LostShips?.Any(ship => ship.SectorSuppression > 0) == true)
                            AddSector(evacuation.Location, planets, sectors);
                        break;
                    case PlanetUprisingStartedResult started:
                        planets.Add(started.Planet);
                        break;
                    case PlanetUprisingEndedResult ended:
                        planets.Add(ended.Planet);
                        break;
                    case BombardmentResult { PlanetDestroyed: true } bombardment:
                        AddSector(bombardment.Planet, planets, sectors);
                        break;
                    case PlanetaryAssaultResult { Success: true } assault:
                        planets.Add(assault.Planet);
                        break;
                    case SpaceCombatResult combat:
                        planets.Add(combat.Planet);
                        break;
                }
            }
            foreach (PlanetSector sector in sectors)
                planets.UnionWith(sector.GetChildren<Planet>(includeDisabled: true));
            planets.Remove(null);
            List<GameResult> notifications = new();
            foreach (
                Planet planet in planets.OrderBy(
                    planet => planet.InstanceID,
                    StringComparer.Ordinal
                )
            )
            {
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
        /// Includes a unit's current location and any previous location carried by its result.
        /// </summary>
        /// <param name="entity">The entity described by the result.</param>
        /// <param name="planets">The affected local planets.</param>
        /// <param name="sectors">The affected suppression sectors.</param>
        /// <param name="previousPlanet">The planet the unit left, when applicable.</param>
        private static void AddCurrentLocation(
            IGameEntity entity,
            ISet<Planet> planets,
            ISet<PlanetSector> sectors,
            Planet previousPlanet = null
        )
        {
            if (entity is ISceneNode node)
                AddLocations(
                    node,
                    previousPlanet,
                    node as Planet ?? node.GetParentOfType<Planet>(),
                    planets,
                    sectors
                );
        }

        /// <summary>
        /// Includes the old and new locations when membership or ownership changes.
        /// Suppression ships can also change garrison requirements throughout those sectors.
        /// </summary>
        /// <param name="node">The changed unit or planet.</param>
        /// <param name="origin">The previous planet, or null for a new node.</param>
        /// <param name="destination">The new planet, or null after removal.</param>
        /// <param name="planets">The affected local planets.</param>
        /// <param name="sectors">The affected suppression sectors.</param>
        private static void AddLocations(
            ISceneNode node,
            Planet origin,
            Planet destination,
            ISet<Planet> planets,
            ISet<PlanetSector> sectors
        )
        {
            if (node is PlanetSector sector)
            {
                sectors.Add(sector);
                return;
            }
            if (node is GalaxyMap galaxy)
            {
                sectors.UnionWith(galaxy.GetChildren<PlanetSector>(includeDisabled: true));
                return;
            }
            if (node is not (Planet or Building or Fleet or CapitalShip or Starfighter or Regiment))
                return;
            if (origin == null && destination == null)
                return;
            planets.Add(origin);
            planets.Add(destination);
            if (
                node is CapitalShip { SectorSuppression: > 0 }
                || node is Fleet fleet
                    && fleet
                        .GetChildren<CapitalShip>(includeDisabled: true)
                        .Any(ship => ship.SectorSuppression > 0)
            )
            {
                AddSector(origin, planets, sectors);
                AddSector(destination, planets, sectors);
            }
        }
    }
}
