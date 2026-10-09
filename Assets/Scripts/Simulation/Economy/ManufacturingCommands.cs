using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Manages unit and facility production orders.
    /// </summary>
    public class ManufacturingCommands
    {
        private readonly GameRoot _game;
        private readonly ManufacturingQueries _queries;
        private readonly MovementCommands _movementSystem;
        private readonly FleetCommands _fleetSystem;
        private readonly List<GameResult> _pendingResults = new List<GameResult>();

        /// <summary>
        /// Creates a new ManufacturingCommands.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="fleetSystem">Owns fleet creation and empty-fleet cleanup.</param>
        /// <param name="queries">Read-only manufacturing rules for the active game.</param>
        /// <param name="movementSystem">Used to dispatch completed units to their destinations.</param>
        public ManufacturingCommands(
            GameRoot game,
            FleetCommands fleetSystem,
            ManufacturingQueries queries,
            MovementCommands movementSystem = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fleetSystem = fleetSystem ?? throw new ArgumentNullException(nameof(fleetSystem));
            _movementSystem = movementSystem;
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Returns and clears results queued by immediate manufacturing operations.
        /// </summary>
        /// <returns>The pending manufacturing results.</returns>
        internal List<GameResult> TakePendingResults()
        {
            List<GameResult> results = new List<GameResult>(_pendingResults);
            _pendingResults.Clear();
            return results;
        }

        /// <summary>
        /// Creates and queues copies of a manufacturing template for one destination.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <returns>True when the complete order was queued.</returns>
        public bool StartManufacturing(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceId
        )
        {
            if (
                !_queries.CanStartManufacturing(
                    producer,
                    template,
                    destination,
                    count,
                    ownerInstanceId
                )
            )
                return false;

            CancelConflictingProject(producer, template);
            return QueueManufacturingItems(producer, template, destination, count);
        }

        /// <summary>
        /// Sets the product and total unfinished quantity for one manufacturing lane.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The requested total number of unfinished copies.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <returns>True when the manufacturing order was applied.</returns>
        public bool SetManufacturingOrder(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceId
        )
        {
            if (
                !_queries.CanSetManufacturingOrder(
                    producer,
                    template,
                    destination,
                    count,
                    ownerInstanceId
                )
            )
            {
                return false;
            }

            ManufacturingType type = template.GetManufacturingType();
            ManufacturingOrder activeOrder = ManufacturingQueries.GetManufacturingOrder(
                producer,
                type
            );
            producer.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> queue);
            if (ManufacturingRules.MatchesProduct(activeOrder, template))
            {
                if (count < activeOrder.Quantity)
                {
                    TrimQueueItems(producer, queue, count);
                    return true;
                }

                if (count == activeOrder.Quantity)
                    return true;

                return QueueManufacturingItems(
                    producer,
                    template,
                    destination,
                    count - activeOrder.Quantity
                );
            }

            HashSet<Fleet> productionFleets = new HashSet<Fleet>();
            if (queue?.Count > 0)
            {
                productionFleets = DetachQueueItems(producer, queue);
                producer.GetManufacturingQueue().Remove(type);
            }

            bool started = QueueManufacturingItems(producer, template, destination, count);
            RemoveEmptyFleets(productionFleets);

            return started;
        }

        /// <summary>
        /// Creates and queues copies after the requested order has been validated and prepared.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <returns>True when at least one copy was queued.</returns>
        private bool QueueManufacturingItems(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count
        )
        {
            bool started = false;
            Fleet capitalShipDestination = null;
            Planet destinationPlanet = destination as Planet;
            Fleet destinationFleet = destination as Fleet;
            CapitalShip destinationShip = destination as CapitalShip;

            for (int index = 0; index < count; index++)
            {
                if (template is not ISceneNode templateNode)
                    return started;

                ISceneNode sceneNode = templateNode.CreateCopy();
                sceneNode.InstanceID = null;
                IManufacturable item = (IManufacturable)sceneNode;

                sceneNode.OwnerInstanceID = producer.GetOwnerInstanceID();
                item.ManufacturingStatus = ManufacturingStatus.Building;
                item.ManufacturingProgress = 0;
                if (item is IMovable movable)
                    movable.Movement = null;

                bool enqueued;
                if (destinationFleet != null)
                {
                    enqueued = Enqueue(producer, item, destinationFleet);
                }
                else if (destinationShip != null)
                {
                    enqueued = Enqueue(producer, item, destinationShip);
                }
                else if (destinationPlanet != null && item is CapitalShip)
                {
                    capitalShipDestination ??= GetProductionFleet(
                        destinationPlanet,
                        producer.GetOwnerInstanceID()
                    );
                    if (capitalShipDestination == null)
                        return started;

                    enqueued = Enqueue(producer, item, capitalShipDestination);
                }
                else if (destinationPlanet != null)
                {
                    enqueued = Enqueue(producer, item, destinationPlanet);
                }
                else
                {
                    return started;
                }

                if (!enqueued)
                {
                    FleetLifecycle.RemoveEmptyFleet(_game, capitalShipDestination);
                    return started;
                }

                started = true;
            }

            return started;
        }

        /// <summary>
        /// Removes items from the end of a manufacturing lane while preserving earlier work.
        /// </summary>
        /// <param name="planet">The planet whose lane is being shortened.</param>
        /// <param name="items">The ordered lane items.</param>
        /// <param name="retainedCount">The number of items to retain from the front.</param>
        private void TrimQueueItems(Planet planet, List<IManufacturable> items, int retainedCount)
        {
            while (items.Count > retainedCount)
            {
                IManufacturable item = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                item.ManufacturingQueueSequence = 0;
                Fleet destinationFleet = DetachQueuedItem(item);
                FleetLifecycle.RemoveEmptyFleet(_game, destinationFleet);
                GameLogger.Debug(
                    $"Cancelled manufacturing: {item.GetType().Name} at {planet.GetDisplayName()}"
                );
            }
        }

        /// <summary>
        /// Gets the first stationary friendly fleet at a production destination, creating one
        /// when none exists.
        /// </summary>
        /// <param name="planet">The planet receiving completed capital ships.</param>
        /// <param name="ownerInstanceId">The faction receiving the ships.</param>
        /// <returns>The fleet that should receive the manufactured ships.</returns>
        private Fleet GetProductionFleet(Planet planet, string ownerInstanceId)
        {
            return planet
                    .GetChildren<Fleet>()
                    .FirstOrDefault(fleet =>
                        fleet.Movement == null
                        && string.Equals(
                            fleet.GetOwnerInstanceID(),
                            ownerInstanceId,
                            StringComparison.Ordinal
                        )
                    )
                ?? _fleetSystem.CreateAtPlanet(planet, ownerInstanceId);
        }

        /// <summary>
        /// Changes the delivery destination for every unfinished item in one production lane.
        /// </summary>
        /// <param name="producer">The planet that owns the production queue.</param>
        /// <param name="type">The production lane to retarget.</param>
        /// <param name="destination">The new delivery container.</param>
        /// <param name="ownerInstanceId">The faction requesting the change.</param>
        /// <returns>True when the lane is empty or every queued item accepted the new destination.</returns>
        public bool RetargetManufacturingDestination(
            Planet producer,
            ManufacturingType type,
            ContainerNode destination,
            string ownerInstanceId
        )
        {
            if (
                producer == null
                || type == ManufacturingType.None
                || destination == null
                || string.IsNullOrEmpty(ownerInstanceId)
                || !string.Equals(
                    producer.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                )
            )
            {
                return false;
            }

            if (
                !producer.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> queue)
                || queue.Count == 0
            )
            {
                return true;
            }

            if (_movementSystem == null)
                return false;

            List<ISceneNode> items = queue.OfType<ISceneNode>().ToList();
            return items.Count == queue.Count
                && _movementSystem.TryRequestMove(items, destination, ownerInstanceId);
        }

        /// <summary>
        /// Cancels the active project in a production lane when a different unit type is ordered.
        /// </summary>
        /// <param name="producer">The producer.</param>
        /// <param name="template">The template.</param>
        private void CancelConflictingProject(Planet producer, IManufacturable template)
        {
            ManufacturingType type = template.GetManufacturingType();
            if (
                producer
                    .GetManufacturingQueue()
                    .TryGetValue(type, out List<IManufacturable> activeProject) != true
                || activeProject == null
                || activeProject.Count == 0
                || activeProject.All(item => item.GetTypeID() == template.GetTypeID())
            )
            {
                return;
            }

            ClearQueueItems(producer, activeProject);
            producer.GetManufacturingQueue().Remove(type);
        }

        /// <summary>
        /// Enqueues an item for production at <paramref name="planet"/>, delivering to
        /// <paramref name="destination"/> on completion.
        /// </summary>
        /// <param name="planet">The planet where production occurs.</param>
        /// <param name="item">The item to manufacture.</param>
        /// <param name="destination">The planet receiving the completed item.</param>
        /// <returns>True when the item was queued; otherwise, false.</returns>
        public bool Enqueue(Planet planet, IManufacturable item, Planet destination)
        {
            Faction faction = GetValidatedFaction(planet, item);
            if (faction == null || !IsLiveDestination(destination))
                return false;

            if (item is CapitalShip)
                return false;

            if (
                !string.Equals(
                    destination.GetOwnerInstanceID(),
                    item.GetOwnerInstanceID(),
                    StringComparison.Ordinal
                )
            )
                return false;

            if (!destination.CanAcceptChild(item))
                return false;

            if (!HasMaintenanceHeadroom(faction, item))
                return false;

            _game.AttachNode(item, destination);

            _pendingResults.Add(
                new ManufacturingDeployedResult
                {
                    Faction = faction,
                    DeployedObject = item,
                    Location = destination,
                    Tick = _game.CurrentTick,
                }
            );

            CommitToQueue(planet, item);
            return true;
        }

        /// <summary>
        /// Enqueues an item for production at <paramref name="planet"/>, placing it into
        /// <paramref name="destination"/> fleet on completion. The caller is responsible for
        /// selecting the target fleet.
        /// </summary>
        /// <param name="planet">The planet where production occurs.</param>
        /// <param name="item">The item to manufacture.</param>
        /// <param name="destination">The fleet receiving the completed item.</param>
        /// <returns>True when the item was queued; otherwise, false.</returns>
        public bool Enqueue(Planet planet, IManufacturable item, Fleet destination)
        {
            Faction faction = GetValidatedFaction(planet, item);
            if (faction == null || !IsLiveDestination(destination))
                return false;

            if (
                !string.Equals(
                    destination.GetOwnerInstanceID(),
                    item.GetOwnerInstanceID(),
                    StringComparison.Ordinal
                )
            )
                return false;

            ISceneNode parent = destination;

            if (item is Starfighter)
            {
                CapitalShip target = destination.FindShipForStarfighter();
                if (target == null)
                    return false;
                parent = target;
            }
            else if (item is Regiment)
            {
                CapitalShip target = destination.FindShipForRegiment();
                if (target == null)
                    return false;
                parent = target;
            }
            else if (item is SpecialForces)
            {
                CapitalShip target = ManufacturingQueries.FindSpecialForcesCarrier(
                    destination,
                    item.GetOwnerInstanceID()
                );
                if (target == null)
                    return false;
                parent = target;
            }

            if (!parent.CanAcceptChild(item))
                return false;

            if (!HasMaintenanceHeadroom(faction, item))
                return false;

            _game.AttachNode(item, parent);

            _pendingResults.Add(
                new ManufacturingDeployedResult
                {
                    Faction = faction,
                    DeployedObject = item,
                    Location = destination,
                    Tick = _game.CurrentTick,
                }
            );

            CommitToQueue(planet, item);
            return true;
        }

        /// <summary>
        /// Queues a starfighter or regiment for deployment to a specific capital ship.
        /// </summary>
        /// <param name="planet">The planet where production is queued.</param>
        /// <param name="item">The item to produce.</param>
        /// <param name="destination">The capital ship receiving the completed item.</param>
        /// <returns>True when the item was queued; otherwise false.</returns>
        public bool Enqueue(Planet planet, IManufacturable item, CapitalShip destination)
        {
            Faction faction = GetValidatedFaction(planet, item);
            if (faction == null || !IsLiveDestination(destination))
                return false;

            if (
                !string.Equals(
                    destination.GetOwnerInstanceID(),
                    item.GetOwnerInstanceID(),
                    StringComparison.Ordinal
                )
            )
                return false;

            if (
                item is Starfighter or Regiment
                && !ManufacturingRules.IsCarrierAvailable(destination)
            )
                return false;

            if (!destination.CanAcceptChild(item))
                return false;

            if (!HasMaintenanceHeadroom(faction, item))
                return false;

            _game.AttachNode(item, destination);

            _pendingResults.Add(
                new ManufacturingDeployedResult
                {
                    Faction = faction,
                    DeployedObject = item as IGameEntity,
                    Location = destination,
                    Tick = _game.CurrentTick,
                }
            );

            CommitToQueue(planet, item);
            return true;
        }

        /// <summary>
        /// Returns whether a manufacturing destination is the registered live node at a planet.
        /// </summary>
        /// <param name="destination">The destination to validate.</param>
        /// <returns>True when the destination is attached to the active scene graph.</returns>
        private bool IsLiveDestination(ContainerNode destination)
        {
            if (destination == null)
                return false;

            ContainerNode liveDestination = _game.GetSceneNodeByInstanceID<ContainerNode>(
                destination.InstanceID
            );
            if (!ReferenceEquals(liveDestination, destination))
                return false;

            return destination is Planet || destination.GetParentOfType<Planet>() != null;
        }

        /// <summary>
        /// Validates the planet's owner for production.
        /// </summary>
        /// <param name="planet">The planet where production would occur.</param>
        /// <param name="item">The item to produce.</param>
        /// <returns>The owning faction, or null if validation fails.</returns>
        private Faction GetValidatedFaction(Planet planet, IManufacturable item)
        {
            if (planet == null || item == null)
                return null;

            string ownerInstanceId = planet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerInstanceId))
                return null;

            Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            if (faction == null)
                return null;

            return faction;
        }

        /// <summary>
        /// Returns true if the faction can afford the item's projected maintenance.
        /// </summary>
        /// <param name="faction">The faction producing the item.</param>
        /// <param name="item">The item being produced.</param>
        /// <returns>True when the item can be queued.</returns>
        private bool HasMaintenanceHeadroom(Faction faction, IManufacturable item)
        {
            if (item.GetMaintenanceCost() <= 0 || ManufacturingRules.IsResourceFacility(item))
                return true;

            int projectedHeadroom =
                ResourceProductionQueries.CalculateMaintenanceCapacity(_game, faction)
                - faction.GetTotalProjectedMaintenanceCost(item);

            return projectedHeadroom >= 0;
        }

        /// <summary>
        /// Initializes the item's manufacturing state and adds it to the planet's queue.
        /// </summary>
        /// <param name="planet">The planet producing the item.</param>
        /// <param name="item">The item to enqueue for production.</param>
        private void CommitToQueue(Planet planet, IManufacturable item)
        {
            item.ManufacturingStatus = ManufacturingStatus.Building;
            item.ManufacturingProgress = 0;
            item.ProducerOwnerID = planet.GetOwnerInstanceID();
            item.ProducerPlanetID = planet.GetInstanceID();

            planet.AddToManufacturingQueue(item);

            _pendingResults.Add(
                new GameObjectCreatedResult { GameObject = item, Tick = _game.CurrentTick }
            );

            GameLogger.Log(
                $"Enqueued {item.GetDisplayName()} for production at {planet.GetDisplayName()} (cost: {item.GetConstructionCost()})"
            );
        }

        /// <summary>
        /// Clears one manufacturing queue on a planet.
        /// </summary>
        /// <param name="planet">The planet whose queue should be cleared.</param>
        /// <param name="type">The manufacturing queue type to clear.</param>
        /// <returns>True when a populated queue was cleared; otherwise false.</returns>
        public bool ClearQueue(Planet planet, ManufacturingType type)
        {
            if (planet == null || type == ManufacturingType.None)
                return false;

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (
                !queue.TryGetValue(type, out List<IManufacturable> items)
                || items == null
                || items.Count == 0
            )
                return false;

            ClearQueueItems(planet, items);
            queue.Remove(type);
            return true;
        }

        /// <summary>
        /// Cancels one queued manufacturing item owned by the supplied faction.
        /// </summary>
        /// <param name="item">The queued item to cancel.</param>
        /// <param name="ownerInstanceId">The faction authorized to cancel the item.</param>
        /// <returns>True when the queued item was removed; otherwise false.</returns>
        public bool CancelManufacturing(IManufacturable item, string ownerInstanceId)
        {
            if (
                item is not ISceneNode sceneNode
                || item.ManufacturingStatus != ManufacturingStatus.Building
                || string.IsNullOrEmpty(item.ProducerPlanetID)
                || string.IsNullOrEmpty(ownerInstanceId)
            )
                return false;

            Planet producer = _game.GetSceneNodeByInstanceID<Planet>(item.ProducerPlanetID);
            if (
                producer == null
                || !string.Equals(
                    producer.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                )
            )
                return false;

            ManufacturingType type = item.GetManufacturingType();
            Dictionary<ManufacturingType, List<IManufacturable>> queues =
                producer.GetManufacturingQueue();
            if (!queues.TryGetValue(type, out List<IManufacturable> queue) || queue == null)
                return false;

            IManufacturable queuedItem = queue.FirstOrDefault(candidate =>
                ReferenceEquals(candidate, item)
                || candidate is ISceneNode candidateNode
                    && !string.IsNullOrEmpty(sceneNode.InstanceID)
                    && string.Equals(
                        candidateNode.InstanceID,
                        sceneNode.InstanceID,
                        StringComparison.Ordinal
                    )
            );
            if (queuedItem == null)
                return false;

            queue.Remove(queuedItem);
            queuedItem.ManufacturingQueueSequence = 0;
            Fleet destinationFleet = DetachQueuedItem(queuedItem);
            FleetLifecycle.RemoveEmptyFleet(_game, destinationFleet);
            if (queue.Count == 0)
            {
                queues.Remove(type);
            }

            return true;
        }

        /// <summary>
        /// Cancels every authorized manufacturing item that remains queued or under construction.
        /// </summary>
        /// <param name="items">The manufacturing items to cancel.</param>
        /// <param name="ownerInstanceId">The faction authorized to cancel the items.</param>
        /// <returns>True when at least one selected item was cancelled.</returns>
        public bool CancelManufacturing(
            IReadOnlyList<IManufacturable> items,
            string ownerInstanceId
        )
        {
            if (items == null || items.Count == 0)
                return false;

            bool cancelled = false;
            foreach (IManufacturable item in items)
                cancelled |= CancelManufacturing(item, ownerInstanceId);

            return cancelled;
        }

        /// <summary>
        /// Detaches queued manufacturing items.
        /// </summary>
        /// <param name="planet">The planet whose queued items are being cleared.</param>
        /// <param name="items">The queued items to clear.</param>
        private void ClearQueueItems(Planet planet, List<IManufacturable> items)
        {
            RemoveEmptyFleets(DetachQueueItems(planet, items));
        }

        /// <summary>
        /// Detaches all items from a manufacturing queue and returns affected destination fleets.
        /// </summary>
        /// <param name="planet">The planet whose queued items are being detached.</param>
        /// <param name="items">The queued items to detach.</param>
        /// <returns>The destination fleets that may be empty after detachment.</returns>
        private HashSet<Fleet> DetachQueueItems(Planet planet, List<IManufacturable> items)
        {
            HashSet<Fleet> destinationFleets = new HashSet<Fleet>();
            foreach (IManufacturable item in items.ToList())
            {
                item.ManufacturingQueueSequence = 0;
                Fleet destinationFleet = DetachQueuedItem(item);
                if (destinationFleet != null)
                    destinationFleets.Add(destinationFleet);

                GameLogger.Debug(
                    $"Cancelled manufacturing: {item.GetType().Name} at {planet.GetDisplayName()}"
                );
            }

            items.Clear();
            return destinationFleets;
        }

        /// <summary>
        /// Detaches one queued item from its destination.
        /// </summary>
        /// <param name="item">The queued item to detach.</param>
        /// <returns>The former destination fleet, or null for a non-fleet destination.</returns>
        private Fleet DetachQueuedItem(IManufacturable item)
        {
            if (item is IMovable movable)
                movable.Movement = null;

            ISceneNode sceneNode = item;
            ISceneNode parent = sceneNode.GetParent();
            if (parent != null)
                _game.DetachNode(sceneNode);

            return parent as Fleet;
        }

        /// <summary>
        /// Removes destination fleets that remain empty after a manufacturing mutation.
        /// </summary>
        /// <param name="fleets">The destination fleets affected by the mutation.</param>
        private void RemoveEmptyFleets(IEnumerable<Fleet> fleets)
        {
            if (fleets == null)
                return;

            foreach (Fleet fleet in fleets)
                FleetLifecycle.RemoveEmptyFleet(_game, fleet);
        }
    }
}
