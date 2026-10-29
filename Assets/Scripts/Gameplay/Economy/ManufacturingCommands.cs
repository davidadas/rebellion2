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

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Manages unit and facility production orders.
    /// </summary>
    public partial class ManufacturingCommands
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
            producer = _queries.ResolveRegisteredPlanet(producer);
            destination = _queries.ResolveRegisteredNode(destination);
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

            return StartValidatedManufacturing(producer, template, destination, count, out _) > 0;
        }

        /// <summary>
        /// Creates and queues copies within a caller-supplied maintenance budget.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <param name="availableMaintenanceHeadroom">Maintenance headroom available before this order.</param>
        /// <param name="minimumHeadroom">The headroom that must remain after the order.</param>
        /// <param name="addedMaintenance">Receives maintenance committed by queued items.</param>
        /// <param name="releasedMaintenance">Receives maintenance released by removed items.</param>
        /// <returns>True when the complete order was queued.</returns>
        internal bool StartManufacturing(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceId,
            int availableMaintenanceHeadroom,
            int minimumHeadroom,
            out int addedMaintenance,
            out int releasedMaintenance
        )
        {
            addedMaintenance = 0;
            releasedMaintenance = 0;
            producer = _queries.ResolveRegisteredPlanet(producer);
            destination = _queries.ResolveRegisteredNode(destination);
            if (
                !ManufacturingQueries.CanAcceptManufacturingOrder(
                    producer,
                    template,
                    destination,
                    count,
                    ownerInstanceId
                )
            )
                return false;

            int expectedReleasedMaintenance = GetConflictingProjectMaintenance(producer, template);
            if (
                !FitsMaintenanceBudget(
                    template,
                    count,
                    availableMaintenanceHeadroom,
                    minimumHeadroom,
                    expectedReleasedMaintenance
                )
            )
                return false;

            int queued = StartValidatedManufacturing(
                producer,
                template,
                destination,
                count,
                out releasedMaintenance
            );
            addedMaintenance = MultiplyMaintenance(template.GetMaintenanceCost(), queued);
            return queued > 0;
        }

        /// <summary>
        /// Replaces a completed building with its queued upgrade after validating both operations.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The upgraded building template.</param>
        /// <param name="destination">The planet receiving the upgrade.</param>
        /// <param name="replacement">The completed building removed by the upgrade.</param>
        /// <param name="ownerInstanceId">The faction requesting the upgrade.</param>
        /// <param name="availableMaintenanceHeadroom">Maintenance headroom available before this order.</param>
        /// <param name="minimumHeadroom">The headroom that must remain when maintenance increases.</param>
        /// <param name="addedMaintenance">Receives maintenance committed by the upgrade.</param>
        /// <param name="releasedMaintenance">Receives maintenance released by replaced work.</param>
        /// <returns>True when the replacement was removed and its upgrade was queued.</returns>
        internal bool StartBuildingUpgrade(
            Planet producer,
            Building template,
            Planet destination,
            Building replacement,
            string ownerInstanceId,
            int availableMaintenanceHeadroom,
            int minimumHeadroom,
            out int addedMaintenance,
            out int releasedMaintenance
        )
        {
            addedMaintenance = 0;
            releasedMaintenance = 0;
            producer = _queries.ResolveRegisteredPlanet(producer);
            destination = _queries.ResolveRegisteredPlanet(destination);
            Building registeredReplacement = string.IsNullOrEmpty(replacement?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<Building>(replacement.InstanceID);
            if (
                producer?.IsDestroyed != false
                || destination?.IsDestroyed != false
                || template == null
                || registeredReplacement == null
                || !ReferenceEquals(registeredReplacement, replacement)
                || replacement.GetParent() != destination
                || replacement.GetOwnerInstanceID() != ownerInstanceId
                || replacement.GetManufacturingStatus() != ManufacturingStatus.Complete
                || replacement.Movement != null
                || !replacement.CanUpgradeTo(template)
            )
                return false;

            int queuedProjectMaintenance = GetConflictingProjectMaintenance(producer, template);
            int replacementMaintenance = Math.Max(0, replacement.MaintenanceCost);
            int expectedReleasedMaintenance = SaturatingAdd(
                queuedProjectMaintenance,
                replacementMaintenance
            );
            if (
                !FitsMaintenanceBudget(
                    template,
                    1,
                    availableMaintenanceHeadroom,
                    minimumHeadroom,
                    expectedReleasedMaintenance
                )
            )
                return false;

            _game.DetachNode(replacement);
            int queued = 0;
            int actualQueuedProjectMaintenance = 0;
            try
            {
                if (
                    ManufacturingQueries.CanAcceptManufacturingOrder(
                        producer,
                        template,
                        destination,
                        1,
                        ownerInstanceId
                    )
                )
                {
                    queued = StartValidatedManufacturing(
                        producer,
                        template,
                        destination,
                        1,
                        out actualQueuedProjectMaintenance
                    );
                }
            }
            finally
            {
                if (queued == 0 && replacement.GetParent() == null)
                    _game.AttachNode(replacement, destination);
            }

            if (queued == 0)
                return false;

            addedMaintenance = Math.Max(0, template.MaintenanceCost);
            releasedMaintenance = SaturatingAdd(
                actualQueuedProjectMaintenance,
                replacementMaintenance
            );
            return true;
        }

        /// <summary>
        /// Queues a structurally and financially validated manufacturing order.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="releasedMaintenance">Receives maintenance released by a conflicting project.</param>
        /// <returns>The number of copies queued.</returns>
        private int StartValidatedManufacturing(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            out int releasedMaintenance
        )
        {
            releasedMaintenance = CancelConflictingProject(producer, template);
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
            producer = _queries.ResolveRegisteredPlanet(producer);
            destination = _queries.ResolveRegisteredNode(destination);
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
                    ) > 0;
            }

            HashSet<Fleet> productionFleets = new HashSet<Fleet>();
            if (queue?.Count > 0)
            {
                productionFleets = DetachQueueItems(producer, queue);
                producer.GetManufacturingQueue().Remove(type);
            }

            bool started = QueueManufacturingItems(producer, template, destination, count) > 0;
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
        /// <returns>The number of copies queued.</returns>
        private int QueueManufacturingItems(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count
        )
        {
            int queued = 0;
            Fleet capitalShipDestination = null;
            Planet destinationPlanet = destination as Planet;
            Fleet destinationFleet = destination as Fleet;
            CapitalShip destinationShip = destination as CapitalShip;

            for (int index = 0; index < count; index++)
            {
                if (template is not ISceneNode templateNode)
                    return queued;

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
                    enqueued = EnqueueValidated(producer, item, destinationFleet);
                }
                else if (destinationShip != null)
                {
                    enqueued = EnqueueValidated(producer, item, destinationShip);
                }
                else if (destinationPlanet != null && item is CapitalShip)
                {
                    capitalShipDestination ??= GetProductionFleet(
                        destinationPlanet,
                        producer.GetOwnerInstanceID()
                    );
                    if (capitalShipDestination == null)
                        return queued;

                    enqueued = EnqueueValidated(producer, item, capitalShipDestination);
                }
                else if (destinationPlanet != null)
                {
                    enqueued = EnqueueValidated(producer, item, destinationPlanet);
                }
                else
                {
                    return queued;
                }

                if (!enqueued)
                {
                    FleetLifecycle.RemoveEmptyFleet(_game, capitalShipDestination);
                    return queued;
                }

                queued++;
            }

            return queued;
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
            producer = _queries.ResolveRegisteredPlanet(producer);
            destination = _queries.ResolveRegisteredNode(destination) as ContainerNode;
            if (
                producer?.IsDestroyed != false
                || type == ManufacturingType.None
                || destination == null
                || destination is Planet { IsDestroyed: true }
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
        /// Toggles whether one manufacturing lane is reserved for manual production.
        /// </summary>
        /// <param name="producer">The producing planet or visible snapshot.</param>
        /// <param name="type">The manufacturing lane.</param>
        /// <param name="ownerInstanceId">The faction requesting the change.</param>
        /// <returns>True when the reservation was changed.</returns>
        public bool ToggleManufacturingReservation(
            Planet producer,
            ManufacturingType type,
            string ownerInstanceId
        )
        {
            producer = _queries.ResolveRegisteredPlanet(producer);
            if (
                producer?.IsDestroyed != false
                || type == ManufacturingType.None
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

            producer.SetManufacturingReserved(type, !producer.IsManufacturingReserved(type));
            return true;
        }

        /// <summary>
        /// Cancels the active project in a production lane when a different unit type is ordered.
        /// </summary>
        /// <param name="producer">The producer.</param>
        /// <param name="template">The template.</param>
        /// <returns>The maintenance released by the canceled project.</returns>
        private int CancelConflictingProject(Planet producer, IManufacturable template)
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
                return 0;
            }

            int releasedMaintenance = GetMaintenanceTotal(activeProject);
            ClearQueueItems(producer, activeProject);
            producer.GetManufacturingQueue().Remove(type);
            return releasedMaintenance;
        }

        /// <summary>
        /// Returns maintenance released when a new order replaces the active project.
        /// </summary>
        /// <param name="producer">The producer whose active lane is inspected.</param>
        /// <param name="template">The requested replacement product.</param>
        /// <returns>The non-negative maintenance released by the replacement.</returns>
        private static int GetConflictingProjectMaintenance(
            Planet producer,
            IManufacturable template
        )
        {
            if (
                producer
                    ?.GetManufacturingQueue()
                    .TryGetValue(
                        template.GetManufacturingType(),
                        out List<IManufacturable> activeProject
                    ) != true
                || activeProject == null
                || activeProject.Count == 0
                || activeProject.All(item => item.GetTypeID() == template.GetTypeID())
            )
                return 0;

            return GetMaintenanceTotal(activeProject);
        }

        /// <summary>
        /// Totals non-negative maintenance costs without overflowing.
        /// </summary>
        /// <param name="items">The queued items to total.</param>
        /// <returns>The saturated maintenance total.</returns>
        private static int GetMaintenanceTotal(IEnumerable<IManufacturable> items)
        {
            long total = items.Sum(item => (long)Math.Max(0, item.GetMaintenanceCost()));
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>
        /// Multiplies an item's maintenance cost by a queued count without overflowing.
        /// </summary>
        /// <param name="maintenanceCost">The maintenance cost of one item.</param>
        /// <param name="count">The number of queued items.</param>
        /// <returns>The saturated non-negative maintenance total.</returns>
        private static int MultiplyMaintenance(int maintenanceCost, int count)
        {
            long total = (long)Math.Max(0, maintenanceCost) * Math.Max(0, count);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>
        /// Adds two non-negative maintenance values without overflowing.
        /// </summary>
        /// <param name="left">The first maintenance value.</param>
        /// <param name="right">The second maintenance value.</param>
        /// <returns>The saturated non-negative sum.</returns>
        private static int SaturatingAdd(int left, int right)
        {
            long total = (long)Math.Max(0, left) + Math.Max(0, right);
            return total > int.MaxValue ? int.MaxValue : (int)total;
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
            return faction != null
                && HasMaintenanceHeadroom(faction, item)
                && EnqueueValidated(planet, item, destination);
        }

        /// <summary>
        /// Queues an item for delivery to a planet.
        /// </summary>
        /// <param name="planet">The planet where production occurs.</param>
        /// <param name="item">The item to manufacture.</param>
        /// <param name="destination">The planet receiving the completed item.</param>
        /// <returns>True when the item was queued; otherwise, false.</returns>
        private bool EnqueueValidated(Planet planet, IManufacturable item, Planet destination)
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
            return faction != null
                && HasMaintenanceHeadroom(faction, item)
                && EnqueueValidated(planet, item, destination);
        }

        /// <summary>
        /// Queues an item for delivery to a fleet.
        /// </summary>
        /// <param name="planet">The planet where production occurs.</param>
        /// <param name="item">The item to manufacture.</param>
        /// <param name="destination">The fleet receiving the completed item.</param>
        /// <returns>True when the item was queued; otherwise, false.</returns>
        private bool EnqueueValidated(Planet planet, IManufacturable item, Fleet destination)
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
            return faction != null
                && HasMaintenanceHeadroom(faction, item)
                && EnqueueValidated(planet, item, destination);
        }

        /// <summary>
        /// Queues an item for delivery to a capital ship.
        /// </summary>
        /// <param name="planet">The planet where production occurs.</param>
        /// <param name="item">The item to manufacture.</param>
        /// <param name="destination">The capital ship receiving the completed item.</param>
        /// <returns>True when the item was queued; otherwise, false.</returns>
        private bool EnqueueValidated(Planet planet, IManufacturable item, CapitalShip destination)
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
        /// Queues an item within a caller-supplied maintenance budget.
        /// </summary>
        /// <param name="planet">The planet where production occurs.</param>
        /// <param name="item">The item to manufacture.</param>
        /// <param name="destination">The node receiving the completed item.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <param name="availableMaintenanceHeadroom">Maintenance headroom available before this order.</param>
        /// <param name="minimumHeadroom">The headroom that must remain after the order.</param>
        /// <param name="addedMaintenance">Receives maintenance committed by the queued item.</param>
        /// <returns>True when the item was queued; otherwise, false.</returns>
        internal bool Enqueue(
            Planet planet,
            IManufacturable item,
            ISceneNode destination,
            string ownerInstanceId,
            int availableMaintenanceHeadroom,
            int minimumHeadroom,
            out int addedMaintenance
        )
        {
            addedMaintenance = 0;
            if (
                !string.Equals(
                    planet?.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                ) || !FitsMaintenanceBudget(item, 1, availableMaintenanceHeadroom, minimumHeadroom)
            )
                return false;

            bool enqueued = destination switch
            {
                Planet destinationPlanet => EnqueueValidated(planet, item, destinationPlanet),
                Fleet destinationFleet => EnqueueValidated(planet, item, destinationFleet),
                CapitalShip destinationShip => EnqueueValidated(planet, item, destinationShip),
                _ => false,
            };
            if (enqueued)
                addedMaintenance = Math.Max(0, item.GetMaintenanceCost());
            return enqueued;
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

            return destination is Planet planet
                ? !planet.IsDestroyed
                : destination.GetParentOfType<Planet>() != null;
        }

        /// <summary>
        /// Validates the planet's owner for production.
        /// </summary>
        /// <param name="planet">The planet where production would occur.</param>
        /// <param name="item">The item to produce.</param>
        /// <returns>The owning faction, or null if validation fails.</returns>
        private Faction GetValidatedFaction(Planet planet, IManufacturable item)
        {
            if (
                planet?.IsDestroyed != false
                || item == null
                || !ReferenceEquals(_queries.ResolveRegisteredPlanet(planet), planet)
            )
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
        /// Returns whether one order fits within a supplied maintenance budget.
        /// </summary>
        /// <param name="item">The item being manufactured.</param>
        /// <param name="count">The number of copies being committed.</param>
        /// <param name="availableMaintenanceHeadroom">Headroom available before the order.</param>
        /// <param name="minimumHeadroom">Headroom that must remain after the order.</param>
        /// <param name="releasedMaintenance">Maintenance released by the same order.</param>
        /// <returns>True when the order may be committed.</returns>
        private static bool FitsMaintenanceBudget(
            IManufacturable item,
            int count,
            int availableMaintenanceHeadroom,
            int minimumHeadroom,
            int releasedMaintenance = 0
        )
        {
            if (item == null || count <= 0)
                return false;
            if (item.GetMaintenanceCost() <= 0 || ManufacturingRules.IsResourceFacility(item))
                return true;

            long addedMaintenance = (long)Math.Max(0, item.GetMaintenanceCost()) * count;
            long released = Math.Max(0, releasedMaintenance);
            if (addedMaintenance <= released)
                return true;

            long remaining = (long)availableMaintenanceHeadroom + released - addedMaintenance;
            return remaining >= minimumHeadroom;
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
