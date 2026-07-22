using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages unit and facility production during each game tick.
    /// </summary>
    public class ManufacturingSystem : IGameSystem
    {
        private const int _productionRateScale = 100;

        private readonly GameRoot _game;
        private readonly MovementSystem _movementSystem;
        private readonly FleetSystem _fleetSystem;
        private readonly List<GameResult> _pendingResults = new List<GameResult>();

        /// <summary>
        /// Creates a new ManufacturingSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="fleetSystem">Owns fleet creation and empty-fleet cleanup.</param>
        /// <param name="movementSystem">Used to dispatch completed units to their destinations.</param>
        public ManufacturingSystem(
            GameRoot game,
            FleetSystem fleetSystem,
            MovementSystem movementSystem = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fleetSystem = fleetSystem ?? throw new ArgumentNullException(nameof(fleetSystem));
            _movementSystem = movementSystem;
        }

        /// <summary>
        /// Processes manufacturing for the current tick.
        /// </summary>
        /// <returns>Manufacturing results produced this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            results.AddRange(_pendingResults);
            _pendingResults.Clear();
            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                results.AddRange(ProcessPlanetManufacturing(planet));
            }
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
            if (!CanStartManufacturing(producer, template, destination, count, ownerInstanceId))
                return false;

            bool started = false;
            Fleet capitalShipDestination = null;
            Planet destinationPlanet = destination as Planet;
            Fleet destinationFleet = destination as Fleet;
            CapitalShip destinationShip = destination as CapitalShip;

            for (int index = 0; index < count; index++)
            {
                IManufacturable item = template.GetDeepCopy();
                if (item is not ISceneNode sceneNode)
                    return started;

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
                    capitalShipDestination ??= _fleetSystem.CreateAtPlanet(
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
                    _fleetSystem.RemoveIfEmpty(capitalShipDestination);
                    return started;
                }

                started = true;
            }

            return started;
        }

        /// <summary>
        /// Determines whether an owner can queue copies of a manufacturing template for one destination.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <returns>True when the complete order can be queued.</returns>
        public bool CanStartManufacturing(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceId
        )
        {
            if (
                producer == null
                || template == null
                || destination == null
                || count <= 0
                || string.IsNullOrEmpty(ownerInstanceId)
                || !string.Equals(
                    producer.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                )
                || !template.HasAllowedOwnerInstanceID(ownerInstanceId)
                || producer.GetProductionFacilityCount(template.GetManufacturingType()) <= 0
                || !HasDestinationCapacity(producer, destination, template, count)
            )
                return false;

            Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            return HasMaintenanceHeadroom(faction, template, count);
        }

        /// <summary>
        /// Determines whether a destination has capacity for a complete manufacturing order.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <returns>True when the destination has sufficient capacity.</returns>
        private static bool HasDestinationCapacity(
            Planet producer,
            ISceneNode destination,
            IManufacturable template,
            int count
        )
        {
            string ownerInstanceId = producer.GetOwnerInstanceID();
            if (destination is Planet planet)
            {
                if (template is CapitalShip)
                    return true;

                ISceneNode candidate = CreateManufacturingCandidate(ownerInstanceId, template);
                return candidate != null
                    && planet.CanAcceptChild(candidate)
                    && (template is not Building || planet.GetAvailableEnergy() >= count);
            }

            if (destination is Fleet fleet)
                return HasFleetCapacity(fleet, template, count, ownerInstanceId);

            return destination is CapitalShip capitalShip
                && HasCapitalShipCapacity(capitalShip, template, count, ownerInstanceId);
        }

        /// <summary>
        /// Determines whether a fleet has capacity for a complete manufacturing order.
        /// </summary>
        /// <param name="fleet">The fleet that receives completed items.</param>
        /// <param name="template">The unit template to manufacture.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <returns>True when the fleet has sufficient capacity.</returns>
        private static bool HasFleetCapacity(
            Fleet fleet,
            IManufacturable template,
            int count,
            string ownerInstanceId
        )
        {
            if (
                !string.Equals(
                    fleet.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                )
                || fleet.Movement != null
            )
                return false;

            if (template is CapitalShip)
                return true;

            ISceneNode candidate = CreateManufacturingCandidate(ownerInstanceId, template);
            IEnumerable<CapitalShip> carriers = fleet.CapitalShips.Where(
                IsManufacturingCarrierAvailable
            );
            if (candidate is Starfighter)
                return carriers.Sum(ship => ship.GetExcessStarfighterCapacity()) >= count;

            return candidate is Regiment
                && carriers.Sum(ship => ship.GetExcessRegimentCapacity()) >= count;
        }

        /// <summary>
        /// Determines whether a capital ship has capacity for a complete manufacturing order.
        /// </summary>
        /// <param name="capitalShip">The capital ship that receives completed items.</param>
        /// <param name="template">The unit template to manufacture.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <returns>True when the capital ship has sufficient capacity.</returns>
        private static bool HasCapitalShipCapacity(
            CapitalShip capitalShip,
            IManufacturable template,
            int count,
            string ownerInstanceId
        )
        {
            if (
                !string.Equals(
                    capitalShip.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                ) || !IsManufacturingCarrierAvailable(capitalShip)
            )
                return false;

            ISceneNode candidate = CreateManufacturingCandidate(ownerInstanceId, template);
            if (candidate is Starfighter)
                return capitalShip.GetExcessStarfighterCapacity() >= count;

            return candidate is Regiment && capitalShip.GetExcessRegimentCapacity() >= count;
        }

        /// <summary>
        /// Creates a detached manufactured item for destination-capacity validation.
        /// </summary>
        /// <param name="ownerInstanceId">The owner assigned to the candidate.</param>
        /// <param name="template">The manufacturing template to copy.</param>
        /// <returns>The detached scene node, or null when the template is not a scene node.</returns>
        private static ISceneNode CreateManufacturingCandidate(
            string ownerInstanceId,
            IManufacturable template
        )
        {
            IManufacturable item = template.GetDeepCopy();
            if (item is not ISceneNode sceneNode)
                return null;

            sceneNode.OwnerInstanceID = ownerInstanceId;
            item.ManufacturingStatus = ManufacturingStatus.Building;
            item.ManufacturingProgress = 0;
            if (item is IMovable movable)
                movable.Movement = null;

            return sceneNode;
        }

        /// <summary>
        /// Estimates the production time for copies of one manufacturing template.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The item template to manufacture.</param>
        /// <param name="count">The number of copies to manufacture.</param>
        /// <returns>The estimated production ticks, or null when no facility can produce the item.</returns>
        public static int? EstimateManufacturingTicks(
            Planet producer,
            IManufacturable template,
            int count
        )
        {
            if (producer == null || template == null || count <= 0)
                return null;

            long requiredProgress = (long)Math.Max(template.GetConstructionCost(), 0) * count;
            return EstimateManufacturingTicks(
                producer,
                template.GetManufacturingType(),
                requiredProgress
            );
        }

        /// <summary>
        /// Estimates when the current queue for one manufacturing category will finish.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="type">The manufacturing category to inspect.</param>
        /// <returns>The estimated remaining production ticks, or null when no estimate is available.</returns>
        public static int? EstimateQueueCompletionTicks(Planet producer, ManufacturingType type)
        {
            if (
                producer == null
                || type == ManufacturingType.None
                || !producer
                    .GetManufacturingQueue()
                    .TryGetValue(type, out List<IManufacturable> queue)
                || queue == null
                || queue.Count == 0
            )
                return null;

            long requiredProgress = queue.Sum(item =>
                (long)Math.Max(item.GetConstructionCost() - item.GetManufacturingProgress(), 0)
            );
            return EstimateManufacturingTicks(producer, type, requiredProgress);
        }

        /// <summary>
        /// Estimates production ticks from remaining progress and active facility rates.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="type">The manufacturing category to inspect.</param>
        /// <param name="requiredProgress">The remaining production progress.</param>
        /// <returns>The estimated production ticks, or null when no progress can be made.</returns>
        private static int? EstimateManufacturingTicks(
            Planet producer,
            ManufacturingType type,
            long requiredProgress
        )
        {
            if (requiredProgress <= 0)
                return 0;

            int productionRate = producer
                .GetProductionFacilities(type)
                .Where(facility => facility.GetProcessRate() > 0)
                .Sum(facility => _productionRateScale / facility.GetProcessRate());
            if (productionRate <= 0)
                return null;

            long scaledProgress = requiredProgress * _productionRateScale;
            long estimatedTicks = (scaledProgress + productionRate - 1) / productionRate;
            return estimatedTicks <= int.MaxValue ? (int)estimatedTicks : int.MaxValue;
        }

        /// <summary>
        /// Enqueues an item for production at <paramref name="planet"/>, delivering to
        /// <paramref name="destination"/> on completion.
        /// </summary>
        /// <param name="planet">The planet where production occurs.</param>
        /// <param name="item">The item to manufacture.</param>
        /// <param name="destination">The planet receiving the completed item.</param>
        /// <param name="ignoreCost">Whether to queue the item without charging resources.</param>
        /// <returns>True when the item was queued; otherwise, false.</returns>
        public bool Enqueue(
            Planet planet,
            IManufacturable item,
            Planet destination,
            bool ignoreCost = false
        )
        {
            Faction faction = GetValidatedFaction(planet, item);
            if (faction == null || !IsLiveDestination(destination))
                return false;

            if (item is CapitalShip)
                return false;

            if (!destination.IsColonized && item is not Building)
                return false;

            if (!destination.CanAcceptChild(item))
                return false;

            if (!HasMaintenanceHeadroom(faction, item, ignoreCost))
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

            CommitToQueue(planet, item, ignoreCost);
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
        /// <param name="ignoreCost">Whether to queue the item without charging resources.</param>
        /// <returns>True when the item was queued; otherwise, false.</returns>
        public bool Enqueue(
            Planet planet,
            IManufacturable item,
            Fleet destination,
            bool ignoreCost = false
        )
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

            if (!parent.CanAcceptChild(item))
                return false;

            if (!HasMaintenanceHeadroom(faction, item, ignoreCost))
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

            CommitToQueue(planet, item, ignoreCost);
            return true;
        }

        /// <summary>
        /// Queues a starfighter or regiment for deployment to a specific capital ship.
        /// </summary>
        /// <param name="planet">The planet where production is queued.</param>
        /// <param name="item">The item to produce.</param>
        /// <param name="destination">The capital ship receiving the completed item.</param>
        /// <param name="ignoreCost">Whether to queue the item without checking maintenance headroom.</param>
        /// <returns>True when the item was queued; otherwise false.</returns>
        public bool Enqueue(
            Planet planet,
            IManufacturable item,
            CapitalShip destination,
            bool ignoreCost = false
        )
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

            if (item is Starfighter or Regiment && !IsManufacturingCarrierAvailable(destination))
                return false;

            if (!destination.CanAcceptChild((ISceneNode)item))
                return false;

            if (!HasMaintenanceHeadroom(faction, item, ignoreCost))
                return false;

            _game.AttachNode((ISceneNode)item, destination);

            _pendingResults.Add(
                new ManufacturingDeployedResult
                {
                    Faction = faction,
                    DeployedObject = item as IGameEntity,
                    Location = destination,
                    Tick = _game.CurrentTick,
                }
            );

            CommitToQueue(planet, item, ignoreCost);
            return true;
        }

        /// <summary>
        /// Returns whether a capital ship can receive manufacturing output.
        /// </summary>
        /// <param name="ship">The destination ship to inspect.</param>
        /// <returns>True when the ship is complete and not in transit.</returns>
        private static bool IsManufacturingCarrierAvailable(CapitalShip ship)
        {
            return ship?.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.GetTransitMovement() == null;
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
        /// <param name="ignoreCost">Whether maintenance checks should be skipped.</param>
        /// <returns>True when the item can be queued.</returns>
        private bool HasMaintenanceHeadroom(Faction faction, IManufacturable item, bool ignoreCost)
        {
            if (ignoreCost || item.GetMaintenanceCost() <= 0)
                return true;

            int projectedHeadroom = faction.GetProjectedMaintenanceHeadroom(item);

            return projectedHeadroom >= 0;
        }

        /// <summary>
        /// Determines whether a faction can afford a complete manufacturing order.
        /// </summary>
        /// <param name="faction">The faction committing the order.</param>
        /// <param name="item">The item template being manufactured.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <returns>True when the complete order remains within maintenance capacity.</returns>
        private bool HasMaintenanceHeadroom(Faction faction, IManufacturable item, int count)
        {
            if (faction == null)
                return false;

            int maintenanceCost = item.GetMaintenanceCost();
            if (maintenanceCost <= 0)
                return true;

            return faction.ProjectedMaintenanceHeadroom - maintenanceCost * count >= 0;
        }

        /// <summary>
        /// Initializes the item's manufacturing state and adds it to the planet's queue.
        /// </summary>
        /// <param name="planet">The planet producing the item.</param>
        /// <param name="item">The item to enqueue for production.</param>
        /// <param name="ignoreCost">Reserved for callers that bypass external production costs.</param>
        private void CommitToQueue(Planet planet, IManufacturable item, bool ignoreCost)
        {
            _ = ignoreCost;
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
        /// Processes all manufacturing queues on one planet.
        /// </summary>
        /// <param name="planet">The planet whose queues are processed.</param>
        /// <returns>Manufacturing results produced by the planet.</returns>
        private List<GameResult> ProcessPlanetManufacturing(Planet planet)
        {
            List<GameResult> results = new List<GameResult>();
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (queue == null)
                return results;

            string ownerInstanceId = planet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerInstanceId))
                return results;

            Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            foreach (ManufacturingType type in GetActiveManufacturingTypes(planet, queue))
            {
                queue.TryGetValue(type, out List<IManufacturable> items);
                bool hadQueuedItems = items?.Count > 0;

                items?.RemoveAll(item => !IsQueuedItemActive(item));
                bool hasQueuedItems = items?.Count > 0;
                List<Building> readyFacilities = AdvanceProductionFacilities(
                    planet,
                    type,
                    faction,
                    hasQueuedItems
                );
                if (!hasQueuedItems)
                {
                    if (queue.Remove(type) && hadQueuedItems)
                        results.Add(CreateQueueIdleResult(planet, type));

                    DiscardReadyProductionPoints(readyFacilities);
                    continue;
                }

                List<IManufacturable> completed = DistributeProgress(
                    items,
                    readyFacilities,
                    planet,
                    results
                );
                ReserveInputsForSpentProductionPoints(readyFacilities, faction, items.Count > 0);
                CompleteManufacturedItems(planet, type, completed, results);
                if (items.Count == 0)
                    DiscardReadyProductionPoints(readyFacilities);
            }

            return results;
        }

        /// <summary>
        /// Gets manufacturing types that have a queue or a live production facility.
        /// </summary>
        /// <param name="planet">The production planet.</param>
        /// <param name="queue">The planet's manufacturing queues.</param>
        /// <returns>The active manufacturing types in stable facility and queue order.</returns>
        private static List<ManufacturingType> GetActiveManufacturingTypes(
            Planet planet,
            Dictionary<ManufacturingType, List<IManufacturable>> queue
        )
        {
            List<ManufacturingType> types = planet
                .Buildings.Where(facility =>
                    facility.ManufacturingStatus == ManufacturingStatus.Complete
                    && facility.Movement == null
                    && facility.ProcessRate > 0
                    && facility.ProductionType != ManufacturingType.None
                )
                .Select(facility => facility.ProductionType)
                .Distinct()
                .ToList();
            foreach (ManufacturingType type in queue.Keys)
            {
                if (type != ManufacturingType.None && !types.Contains(type))
                    types.Add(type);
            }

            return types;
        }

        /// <summary>
        /// Returns whether a queued item remains registered as the same live scene node.
        /// </summary>
        /// <param name="item">The queued item to validate.</param>
        /// <returns>True when the queued item remains active in the scene graph.</returns>
        private bool IsQueuedItemActive(IManufacturable item)
        {
            return item != null
                && _game.GetSceneNodeByInstanceID<ISceneNode>(item.InstanceID) == item;
        }

        /// <summary>
        /// Advances production facility timers and returns facilities ready to spend progress.
        /// </summary>
        /// <param name="planet">The production planet.</param>
        /// <param name="type">The manufacturing type being processed.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="hasQueuedItems">Whether the facility type currently has work.</param>
        /// <returns>Facilities with a ready production point.</returns>
        private List<Building> AdvanceProductionFacilities(
            Planet planet,
            ManufacturingType type,
            Faction faction,
            bool hasQueuedItems
        )
        {
            List<Building> productionFacilities = planet
                .Buildings.Where(facility =>
                    facility.ProductionType == type
                    && facility.ManufacturingStatus == ManufacturingStatus.Complete
                    && facility.Movement == null
                    && facility.ProcessRate > 0
                )
                .ToList();

            double cycleIncrement = GetProductionCycleIncrement(planet);
            foreach (Building facility in productionFacilities)
                AdvanceProductionFacility(facility, faction, hasQueuedItems, cycleIncrement);

            return productionFacilities.Where(facility => facility.ProductionPointReady).ToList();
        }

        /// <summary>
        /// Gets the production-cycle progress available to a planet during this tick.
        /// </summary>
        /// <param name="planet">The production planet.</param>
        /// <returns>The progress available after uprising and blockade effects.</returns>
        private double GetProductionCycleIncrement(Planet planet)
        {
            if (planet.IsInUprising)
                return 0;

            GameConfig.BlockadeConfig config = _game.Config.Blockade;
            int modifier = planet.GetBlockadeModifier(
                config.CapitalShipProductionPenaltyPercent,
                config.FighterProductionPenaltyPercent
            );
            return (double)modifier / _productionRateScale;
        }

        /// <summary>
        /// Advances one production facility toward its next production point.
        /// </summary>
        /// <param name="facility">The facility to advance.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="hasQueuedItems">Whether the facility type currently has work.</param>
        /// <param name="cycleIncrement">The production progress available this tick.</param>
        private static void AdvanceProductionFacility(
            Building facility,
            Faction faction,
            bool hasQueuedItems,
            double cycleIncrement
        )
        {
            if (facility.ProductionPointReady)
                return;

            if (cycleIncrement <= 0)
                return;

            if (!facility.ProductionInputReserved)
            {
                if (!hasQueuedItems || !faction.RequestRefinedMaterial(facility))
                    return;
            }

            int processRate = facility.GetProcessRate();
            if (processRate <= 0)
            {
                facility.ProductionCycleProgress = 0;
                facility.ProductionPointReady = false;
                return;
            }

            facility.ProductionCycleProgress += cycleIncrement;
            if (facility.ProductionCycleProgress >= processRate)
            {
                facility.ProductionCycleProgress = 0;
                facility.ProductionInputReserved = false;
                facility.ProductionPointReady = true;
            }
        }

        /// <summary>
        /// Reserves replacement inputs for production points spent during this tick.
        /// </summary>
        /// <param name="facilities">The facilities whose ready points were distributed.</param>
        /// <param name="faction">The faction supplying refined material.</param>
        /// <param name="hasQueuedItems">Whether work remains after point distribution.</param>
        private static void ReserveInputsForSpentProductionPoints(
            List<Building> facilities,
            Faction faction,
            bool hasQueuedItems
        )
        {
            if (!hasQueuedItems)
                return;

            foreach (Building facility in facilities)
            {
                if (!facility.ProductionPointReady && !facility.ProductionInputReserved)
                    faction.RequestRefinedMaterial(facility);
            }
        }

        /// <summary>
        /// Discards completed production points that have no queued item to receive them.
        /// </summary>
        /// <param name="facilities">The ready facilities to reset.</param>
        private static void DiscardReadyProductionPoints(List<Building> facilities)
        {
            foreach (Building facility in facilities)
                facility.ProductionPointReady = false;
        }

        /// <summary>
        /// Distributes ready production facility points across the queue for one manufacturing type.
        /// </summary>
        /// <param name="items">The ordered list of items in this type's queue.</param>
        /// <param name="productionFacilities">Facilities with a ready production point.</param>
        /// <param name="planet">The planet where production is occurring.</param>
        /// <param name="results">Result list to append progress events to.</param>
        /// <returns>Items that completed this tick.</returns>
        private List<IManufacturable> DistributeProgress(
            List<IManufacturable> items,
            List<Building> productionFacilities,
            Planet planet,
            List<GameResult> results
        )
        {
            List<IManufacturable> completed = new List<IManufacturable>();
            Faction faction = _game.GetFactionByOwnerInstanceID(planet.GetOwnerInstanceID());
            if (faction == null)
                return completed;

            MoveFinishedItemsToCompleted(items, completed);

            int facilityIndex = 0;
            while (facilityIndex < productionFacilities.Count && items.Count > 0)
            {
                IManufacturable activeItem = items[0];
                if (GetRemainingProgress(activeItem) <= 0)
                {
                    items.RemoveAt(0);
                    completed.Add(activeItem);
                    continue;
                }

                ApplyStandardProgress(
                    activeItem,
                    productionFacilities[facilityIndex],
                    faction,
                    planet,
                    results
                );
                facilityIndex++;
                MoveCompletedActiveItem(items, completed, activeItem);
            }

            return completed;
        }

        /// <summary>
        /// Moves already-complete queue entries into the completion list.
        /// </summary>
        /// <param name="items">The queue being processed.</param>
        /// <param name="completed">The completion list for this tick.</param>
        private static void MoveFinishedItemsToCompleted(
            List<IManufacturable> items,
            List<IManufacturable> completed
        )
        {
            while (items.Count > 0)
            {
                IManufacturable activeItem = items[0];
                if (!activeItem.IsManufacturingComplete())
                    return;

                items.RemoveAt(0);
                completed.Add(activeItem);
            }
        }

        /// <summary>
        /// Applies one standard production point to an item.
        /// </summary>
        /// <param name="activeItem">The item being built.</param>
        /// <param name="productionFacility">The facility spending its ready point.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="planet">The production planet.</param>
        /// <param name="results">Result list to append progress events to.</param>
        private void ApplyStandardProgress(
            IManufacturable activeItem,
            Building productionFacility,
            Faction faction,
            Planet planet,
            List<GameResult> results
        )
        {
            productionFacility.ProductionPointReady = false;
            int appliedProgress = Math.Min(1, GetRemainingProgress(activeItem));
            activeItem.IncrementManufacturingProgress(appliedProgress);
            AddProgressResult(results, faction, appliedProgress, planet);
        }

        /// <summary>
        /// Moves the active queue item to completed if its manufacturing is finished.
        /// </summary>
        /// <param name="items">The queue being processed.</param>
        /// <param name="completed">The completion list for this tick.</param>
        /// <param name="activeItem">The active item from the front of the queue.</param>
        private static void MoveCompletedActiveItem(
            List<IManufacturable> items,
            List<IManufacturable> completed,
            IManufacturable activeItem
        )
        {
            if (!activeItem.IsManufacturingComplete())
                return;

            items.RemoveAt(0);
            completed.Add(activeItem);
        }

        /// <summary>
        /// Gets the remaining progress required for an item.
        /// </summary>
        /// <param name="item">The item being built.</param>
        /// <returns>The remaining progress required.</returns>
        private static int GetRemainingProgress(IManufacturable item)
        {
            return item.GetConstructionCost() - item.ManufacturingProgress;
        }

        /// <summary>
        /// Records applied manufacturing progress.
        /// </summary>
        /// <param name="results">Result list to append to.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="points">The progress points applied.</param>
        /// <param name="planet">The production planet.</param>
        private void AddProgressResult(
            List<GameResult> results,
            Faction faction,
            int points,
            Planet planet
        )
        {
            results.Add(
                new ManufacturingPointsCompletedResult
                {
                    Faction = faction,
                    Points = points,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Completes manufactured items and emits queue-idle results.
        /// </summary>
        /// <param name="planet">The production planet.</param>
        /// <param name="type">The manufacturing type.</param>
        /// <param name="completed">Items completed this tick.</param>
        /// <param name="results">Result list to append to.</param>
        private void CompleteManufacturedItems(
            Planet planet,
            ManufacturingType type,
            List<IManufacturable> completed,
            List<GameResult> results
        )
        {
            foreach (IManufacturable item in completed)
                results.AddRange(CompleteManufacturing(planet, item));

            if (completed.Count == 0 || GetQueuedItems(planet, type).Count > 0)
                return;

            results.Add(CreateQueueIdleResult(planet, type));
        }

        /// <summary>
        /// Gets the current queue list for a planet and manufacturing type.
        /// </summary>
        /// <param name="planet">The production planet.</param>
        /// <param name="type">The manufacturing type.</param>
        /// <returns>The current queue list, or an empty list.</returns>
        private static List<IManufacturable> GetQueuedItems(Planet planet, ManufacturingType type)
        {
            return planet.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> items)
                ? items
                : new List<IManufacturable>();
        }

        /// <summary>
        /// Creates the result emitted when a manufacturing queue becomes idle.
        /// </summary>
        /// <param name="planet">The production planet.</param>
        /// <param name="type">The idle manufacturing type.</param>
        /// <returns>The queue-idle result.</returns>
        private ManufacturingIdleResult CreateQueueIdleResult(Planet planet, ManufacturingType type)
        {
            return new ManufacturingIdleResult
            {
                ProductionPlanet = planet,
                Faction = _game.GetFactionByOwnerInstanceID(planet.GetOwnerInstanceID()),
                ManufacturingType = type,
                Tick = _game.CurrentTick,
            };
        }

        /// <summary>
        /// Completes manufacturing of an item and dispatches it from the production planet.
        /// </summary>
        /// <param name="productionPlanet">The production planet.</param>
        /// <param name="item">The completed item.</param>
        /// <returns>Completion results for the item.</returns>
        private List<GameResult> CompleteManufacturing(
            Planet productionPlanet,
            IManufacturable item
        )
        {
            MarkCompleteAndDispatch(productionPlanet, item);
            GameLogger.Log(
                $"Completed manufacturing: {item.GetDisplayName()} at {productionPlanet.GetDisplayName()}"
            );

            ManufacturingType type = item.GetManufacturingType();
            List<IManufacturable> remaining = GetQueuedItems(productionPlanet, type);
            int remainingPoints = GetRemainingQueuePoints(remaining);
            Faction faction = _game.GetFactionByOwnerInstanceID(
                productionPlanet.GetOwnerInstanceID()
            );

            return new List<GameResult>
            {
                new GameObjectDeployedResult { GameObject = item, Tick = _game.CurrentTick },
                new ManufacturingRemainingResult
                {
                    Faction = faction,
                    RemainingCount = remaining.Count,
                    Context = productionPlanet,
                    Tick = _game.CurrentTick,
                },
                new ManufacturingPointsRequiredResult
                {
                    Faction = faction,
                    RequiredPoints = remainingPoints,
                    Context = productionPlanet,
                    Tick = _game.CurrentTick,
                },
            };
        }

        /// <summary>
        /// Marks a completed item and starts movement from the production planet if needed.
        /// </summary>
        /// <param name="productionPlanet">The production planet.</param>
        /// <param name="item">The completed item.</param>
        private void MarkCompleteAndDispatch(Planet productionPlanet, IManufacturable item)
        {
            item.ManufacturingStatus = ManufacturingStatus.Complete;

            ContainerNode destination = item.GetParent() as ContainerNode;
            if (destination != null)
                _movementSystem.RequestMove(item, destination, productionPlanet);
        }

        /// <summary>
        /// Gets the remaining progress required by all queued items.
        /// </summary>
        /// <param name="remaining">The remaining queue items.</param>
        /// <returns>The remaining progress points required.</returns>
        private static int GetRemainingQueuePoints(List<IManufacturable> remaining)
        {
            return remaining.Sum(GetRemainingProgress);
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
            DetachQueuedItem(queuedItem);
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
            foreach (IManufacturable item in items.ToList())
            {
                DetachQueuedItem(item);
                GameLogger.Debug(
                    $"Cancelled manufacturing: {item.GetType().Name} at {planet.GetDisplayName()}"
                );
            }

            items.Clear();
        }

        /// <summary>
        /// Detaches one queued item and removes an empty destination fleet.
        /// </summary>
        /// <param name="item">The queued item to detach.</param>
        private void DetachQueuedItem(IManufacturable item)
        {
            ISceneNode sceneNode = item;
            ISceneNode parent = sceneNode.GetParent();
            if (parent != null)
                _game.DetachNode(sceneNode);

            if (parent is Fleet fleet)
                _fleetSystem.RemoveIfEmpty(fleet);
        }

        /// <summary>
        /// Clears all manufacturing queues for a planet and destroys items being built.
        /// Called when planet ownership changes (capture, uprising, diplomacy).
        /// </summary>
        /// <param name="planet">The planet whose queues should be cleared.</param>
        public void ClearQueuesOnOwnershipChange(Planet planet)
        {
            if (planet == null)
            {
                return;
            }

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (queue == null || queue.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<ManufacturingType, List<IManufacturable>> kvp in queue.ToList())
            {
                ManufacturingType type = kvp.Key;
                List<IManufacturable> items = kvp.Value;

                if (items == null || items.Count == 0)
                {
                    continue;
                }

                ClearQueueItems(planet, items);
                queue.Remove(type);
            }
        }

        /// <summary>
        /// Rebuilds manufacturing queues for all planets from scene graph state.
        /// Called after loading a saved game to reconstruct queue state from serialized items.
        /// </summary>
        public void RebuildQueues()
        {
            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                Dictionary<ManufacturingType, List<IManufacturable>> queue =
                    planet.GetManufacturingQueue();
                foreach (KeyValuePair<ManufacturingType, List<IManufacturable>> kvp in queue)
                {
                    kvp.Value.Clear();
                }
            }

            _game
                .GetGalaxyMap()
                .Traverse(node =>
                {
                    if (node is IManufacturable manufacturable)
                    {
                        if (manufacturable.ManufacturingStatus != ManufacturingStatus.Building)
                        {
                            return;
                        }

                        if (string.IsNullOrEmpty(manufacturable.ProducerPlanetID))
                        {
                            return;
                        }

                        Planet producerPlanet = _game.GetSceneNodeByInstanceID<Planet>(
                            manufacturable.ProducerPlanetID
                        );
                        if (producerPlanet == null)
                        {
                            return;
                        }

                        producerPlanet.AddToManufacturingQueue(manufacturable);
                    }
                });

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                Dictionary<ManufacturingType, List<IManufacturable>> queue =
                    planet.GetManufacturingQueue();
                foreach (KeyValuePair<ManufacturingType, List<IManufacturable>> kvp in queue)
                {
                    kvp.Value.Sort(
                        (a, b) => a.ManufacturingProgress.CompareTo(b.ManufacturingProgress)
                    );
                }
            }
        }
    }
}
