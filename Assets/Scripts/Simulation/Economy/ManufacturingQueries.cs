using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates manufacturing eligibility, capacity and completion estimates without changing the game.
    /// </summary>
    public sealed class ManufacturingQueries
    {
        /// <summary>Describes a projected manufacturing lane without retaining or changing live facilities.</summary>
        public readonly struct ProductionForecast
        {
            public ManufacturingType Type { get; }
            public int AvailableTick { get; }
            public double Increment { get; }
            public IReadOnlyList<(
                int Rate,
                double CycleProgress,
                bool Ready,
                int ActivationTicks
            )> Facilities { get; }

            /// <summary>Creates a reusable production-capacity snapshot.</summary>
            /// <param name="type">The manufacturing category.</param>
            /// <param name="availableTick">The earliest tick after previously forecast work.</param>
            /// <param name="increment">Cycle progress per active tick.</param>
            /// <param name="facilities">Facility phases and absolute activation dates.</param>
            internal ProductionForecast(
                ManufacturingType type,
                int availableTick,
                double increment,
                IEnumerable<(
                    int Rate,
                    double CycleProgress,
                    bool Ready,
                    int ActivationTicks
                )> facilities
            )
            {
                Type = type;
                AvailableTick = availableTick;
                Increment = increment;
                Facilities = Array.AsReadOnly(facilities.ToArray());
            }
        }

        private const int _percentageScale = 100;
        private readonly GameRoot _game;

        /// <summary>
        /// Creates manufacturing queries for the supplied game.
        /// </summary>
        /// <param name="game">The game whose faction maintenance is evaluated.</param>
        public ManufacturingQueries(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Gets the active order assigned to one manufacturing lane.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="type">The manufacturing lane to inspect.</param>
        /// <returns>The active order, or null when the lane is empty.</returns>
        public static ManufacturingOrder GetManufacturingOrder(
            Planet producer,
            ManufacturingType type
        )
        {
            if (
                producer?.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> items)
                    != true
                || items == null
                || items.Count == 0
            )
            {
                return null;
            }

            return new ManufacturingOrder(
                type,
                ManufacturingRules.GetProductTypeID(items),
                items.Count
            );
        }

        /// <summary>Gets a single unfinished filler order that may be interrupted by AI-demanded work.</summary>
        /// <param name="producer">The production planet.</param>
        /// <param name="type">The production lane.</param>
        /// <returns>The interruptible order, or null when the lane contains protected work.</returns>
        public static IManufacturable GetInterruptibleManufacturingOrder(
            Planet producer,
            ManufacturingType type
        )
        {
            if (
                producer?.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> queue)
                    != true
                || queue == null
                || queue.Count != 1
            )
                return null;
            IManufacturable unit = queue[0];
            return
                unit is ISceneNode node
                && unit.IsAIProductionFiller
                && unit.ManufacturingStatus == ManufacturingStatus.Building
                && !unit.IsManufacturingComplete()
                && node.GetOwnerInstanceID() == producer.GetOwnerInstanceID()
                && unit.ProducerPlanetID == producer.InstanceID
                ? unit
                : null;
        }

        /// <summary>Returns building space, optionally including confirmed interruptible projects.</summary>
        /// <param name="game">The game containing the destination and production queues.</param>
        /// <param name="destination">The planet receiving construction.</param>
        /// <param name="includeInterruptibleOrders">Whether replaceable unfinished projects count as available space.</param>
        /// <returns>The actual or reclaimable building space.</returns>
        public static int GetAvailableBuildingSpace(
            GameRoot game,
            Planet destination,
            bool includeInterruptibleOrders = false
        )
        {
            int available = destination?.GetAvailableEnergy() ?? 0;
            if (game == null || destination == null || !includeInterruptibleOrders)
                return available;
            foreach (Building building in destination.GetAllBuildings())
            {
                if (
                    !building.IsAIProductionFiller
                    || string.IsNullOrEmpty(building.ProducerPlanetID)
                )
                    continue;
                Planet producer = game.GetSceneNodeByInstanceID<Planet>(building.ProducerPlanetID);
                if (
                    ReferenceEquals(
                        GetInterruptibleManufacturingOrder(producer, ManufacturingType.Building),
                        building
                    )
                )
                    available++;
            }
            return available;
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
                !CanAcceptManufacturingOrder(
                    producer,
                    template,
                    destination,
                    count,
                    ownerInstanceId
                )
            )
                return false;

            Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            return HasMaintenanceHeadroom(
                faction,
                template,
                count,
                GetMaintenanceRefund(producer, template)
            );
        }

        /// <summary>
        /// Determines whether an owner can set the total order for one manufacturing lane.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The requested total number of unfinished copies.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <returns>True when the lane can be adjusted to the requested order.</returns>
        public bool CanSetManufacturingOrder(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceId
        )
        {
            if (
                !HasValidManufacturingOrderContext(
                    producer,
                    template,
                    destination,
                    count,
                    ownerInstanceId
                )
            )
                return false;

            ManufacturingType type = template.GetManufacturingType();
            producer
                .GetManufacturingQueue()
                .TryGetValue(type, out List<IManufacturable> activeItems);
            ManufacturingOrder activeOrder = GetManufacturingOrder(producer, type);
            Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            if (faction == null)
                return false;

            if (ManufacturingRules.MatchesProduct(activeOrder, template))
            {
                int addedCount = count - activeOrder.Quantity;
                return addedCount <= 0
                    || (
                        HasDestinationCapacity(producer, destination, template, addedCount)
                        && HasMaintenanceHeadroom(faction, template, addedCount)
                    );
            }

            if (!HasDestinationCapacity(producer, destination, template, count, activeItems))
                return false;

            return HasMaintenanceHeadroom(
                faction,
                template,
                count,
                GetReplacementMaintenanceRefund(activeItems)
            );
        }

        /// <summary>
        /// Validates the invariant parts of a manufacturing order without reserving capacity.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The requested total number of unfinished copies.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <returns>True when the order context is valid.</returns>
        private static bool HasValidManufacturingOrderContext(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceId
        )
        {
            return producer != null
                && template != null
                && destination != null
                && count > 0
                && !string.IsNullOrEmpty(ownerInstanceId)
                && string.Equals(
                    producer.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                )
                && IManufacturable.CanBeManufacturedBy(template, ownerInstanceId)
                && producer.GetProductionFacilityCount(template.GetManufacturingType()) > 0;
        }

        /// <summary>
        /// Returns maintenance released when the active lane is replaced.
        /// </summary>
        /// <param name="items">The unfinished items removed from the lane.</param>
        /// <returns>The non-negative maintenance released by removing the items.</returns>
        private static int GetReplacementMaintenanceRefund(
            IReadOnlyCollection<IManufacturable> items
        )
        {
            return items?.Sum(item => Math.Max(0, item?.GetMaintenanceCost() ?? 0)) ?? 0;
        }

        /// <summary>
        /// Returns maintenance reserved by the active lane when the requested template would replace it.
        /// </summary>
        /// <param name="producer">The producer.</param>
        /// <param name="template">The template.</param>
        /// <returns>The requested maintenance refund.</returns>
        private static int GetMaintenanceRefund(Planet producer, IManufacturable template)
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
            {
                return 0;
            }

            return activeProject.Sum(item => Math.Max(0, item.GetMaintenanceCost()));
        }

        /// <summary>
        /// Determines whether a producer and destination can structurally accept a manufacturing order.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <param name="replacedOrder">An unfinished order assumed to be removed before construction.</param>
        /// <returns>True when the order is structurally valid without considering maintenance.</returns>
        public static bool CanAcceptManufacturingOrder(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceId,
            IManufacturable replacedOrder = null
        )
        {
            return HasValidManufacturingOrderContext(
                    producer,
                    template,
                    destination,
                    count,
                    ownerInstanceId
                )
                && HasDestinationCapacity(
                    producer,
                    destination,
                    template,
                    count,
                    replacedOrder == null ? null : new[] { replacedOrder }
                );
        }

        /// <summary>
        /// Determines whether a destination has capacity for a complete manufacturing order.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="destination">The node that receives completed items.</param>
        /// <param name="template">The unit or facility template to manufacture.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="replacedItems">The active lane items removed before capacity is consumed.</param>
        /// <returns>True when the destination has sufficient capacity.</returns>
        private static bool HasDestinationCapacity(
            Planet producer,
            ISceneNode destination,
            IManufacturable template,
            int count,
            IReadOnlyCollection<IManufacturable> replacedItems = null
        )
        {
            string ownerInstanceId = producer.GetOwnerInstanceID();
            if (destination is Planet planet)
            {
                if (template is CapitalShip)
                    return true;

                if (
                    !string.Equals(
                        planet.GetOwnerInstanceID(),
                        ownerInstanceId,
                        StringComparison.Ordinal
                    )
                )
                    return false;

                return template switch
                {
                    Regiment _ => true,
                    SpecialForces _ => planet.CanHostOwnedUnits(),
                    Starfighter _ => planet.CanHostOwnedUnits()
                        && !planet.IsBlockadedFor(ownerInstanceId),
                    Building building => planet.GetAvailableEnergy()
                        + CountReplacedItems<Building>(
                            replacedItems,
                            item => ReferenceEquals(item.GetParent(), planet)
                        )
                        >= count
                        && (
                            building.BuildingType != BuildingType.Mine
                            || planet.GetUnminedResourceNodeCount()
                                + CountReplacedItems<Building>(
                                    replacedItems,
                                    item =>
                                        item.BuildingType == BuildingType.Mine
                                        && ReferenceEquals(item.GetParent(), planet)
                                )
                                >= count
                        ),
                    _ => false,
                };
            }

            if (destination is Fleet fleet)
                return HasFleetCapacity(fleet, template, count, ownerInstanceId, replacedItems);

            return destination is CapitalShip capitalShip
                && HasCapitalShipCapacity(
                    capitalShip,
                    template,
                    count,
                    ownerInstanceId,
                    replacedItems
                );
        }

        /// <summary>
        /// Counts replaced items of one type that satisfy a destination predicate.
        /// </summary>
        /// <typeparam name="T">The item type to count.</typeparam>
        /// <param name="items">The items removed by the replacement order.</param>
        /// <param name="isAtDestination">Tests whether an item consumes the requested destination's capacity.</param>
        /// <returns>The matching item count.</returns>
        private static int CountReplacedItems<T>(
            IReadOnlyCollection<IManufacturable> items,
            Func<T, bool> isAtDestination
        )
            where T : class, IManufacturable
        {
            return items?.OfType<T>().Count(isAtDestination) ?? 0;
        }

        /// <summary>
        /// Determines whether a fleet has capacity for a complete manufacturing order.
        /// </summary>
        /// <param name="fleet">The fleet that receives completed items.</param>
        /// <param name="template">The unit template to manufacture.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <param name="replacedItems">The active lane items removed before capacity is consumed.</param>
        /// <returns>True when the fleet has sufficient capacity.</returns>
        private static bool HasFleetCapacity(
            Fleet fleet,
            IManufacturable template,
            int count,
            string ownerInstanceId,
            IReadOnlyCollection<IManufacturable> replacedItems = null
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

            if (template is SpecialForces)
                return FindSpecialForcesCarrier(fleet, ownerInstanceId) != null;

            IEnumerable<CapitalShip> carriers = fleet
                .GetChildren<CapitalShip>()
                .Where(ManufacturingRules.IsCarrierAvailable);
            if (template is Starfighter)
                return carriers.Sum(ship => ship.GetExcessStarfighterCapacity())
                        + CountReplacedItems<Starfighter>(
                            replacedItems,
                            item => IsOnAvailableCarrierInFleet(item, fleet)
                        )
                    >= count;

            return template is Regiment
                && carriers.Sum(ship => ship.GetExcessRegimentCapacity())
                    + CountReplacedItems<Regiment>(
                        replacedItems,
                        item => IsOnAvailableCarrierInFleet(item, fleet)
                    )
                    >= count;
        }

        /// <summary>
        /// Determines whether a queued unit occupies capacity on a carrier that can receive its replacement.
        /// </summary>
        /// <param name="item">The queued unit being replaced.</param>
        /// <param name="fleet">The requested destination fleet.</param>
        /// <returns>True when removing the unit releases usable capacity in the destination fleet.</returns>
        private static bool IsOnAvailableCarrierInFleet(IManufacturable item, Fleet fleet)
        {
            CapitalShip carrier = item?.GetParentOfType<CapitalShip>();
            return ManufacturingRules.IsCarrierAvailable(carrier)
                && ReferenceEquals(carrier.GetParentOfType<Fleet>(), fleet);
        }

        /// <summary>
        /// Determines whether a capital ship has capacity for a complete manufacturing order.
        /// </summary>
        /// <param name="capitalShip">The capital ship that receives completed items.</param>
        /// <param name="template">The unit template to manufacture.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="ownerInstanceId">The faction requesting the order.</param>
        /// <param name="replacedItems">The active lane items removed before capacity is consumed.</param>
        /// <returns>True when the capital ship has sufficient capacity.</returns>
        private static bool HasCapitalShipCapacity(
            CapitalShip capitalShip,
            IManufacturable template,
            int count,
            string ownerInstanceId,
            IReadOnlyCollection<IManufacturable> replacedItems = null
        )
        {
            if (
                !string.Equals(
                    capitalShip.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                ) || !ManufacturingRules.IsCarrierAvailable(capitalShip)
            )
                return false;

            if (template is SpecialForces)
                return true;

            if (template is Starfighter)
                return capitalShip.GetExcessStarfighterCapacity()
                        + CountReplacedItems<Starfighter>(
                            replacedItems,
                            item => ReferenceEquals(item.GetParent(), capitalShip)
                        )
                    >= count;

            return template is Regiment
                && capitalShip.GetExcessRegimentCapacity()
                    + CountReplacedItems<Regiment>(
                        replacedItems,
                        item => ReferenceEquals(item.GetParent(), capitalShip)
                    )
                    >= count;
        }

        /// <summary>
        /// Finds a complete stationary capital ship that can receive manufactured special forces.
        /// </summary>
        /// <param name="fleet">The destination fleet.</param>
        /// <param name="ownerInstanceId">The owner of the manufacturing order.</param>
        /// <returns>An available carrier, or null when the fleet cannot receive special forces.</returns>
        internal static CapitalShip FindSpecialForcesCarrier(Fleet fleet, string ownerInstanceId)
        {
            if (
                fleet == null
                || fleet.Movement != null
                || !string.Equals(
                    fleet.GetOwnerInstanceID(),
                    ownerInstanceId,
                    StringComparison.Ordinal
                )
            )
                return null;

            return fleet
                .GetChildren<CapitalShip>()
                .FirstOrDefault(ship =>
                    ManufacturingRules.IsCarrierAvailable(ship)
                    && string.Equals(
                        ship.GetOwnerInstanceID(),
                        ownerInstanceId,
                        StringComparison.Ordinal
                    )
                );
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
        /// Estimates completion after setting the total order for one manufacturing lane.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The requested product template.</param>
        /// <param name="count">The requested total number of unfinished copies.</param>
        /// <returns>The estimated completion ticks, or null when no facility can produce the item.</returns>
        public static int? EstimateManufacturingOrderTicks(
            Planet producer,
            IManufacturable template,
            int count
        )
        {
            if (producer == null || template == null || count <= 0)
                return null;

            ManufacturingType type = template.GetManufacturingType();
            producer
                .GetManufacturingQueue()
                .TryGetValue(type, out List<IManufacturable> activeItems);
            ManufacturingOrder activeOrder = GetManufacturingOrder(producer, type);
            if (!ManufacturingRules.MatchesProduct(activeOrder, template))
                return EstimateManufacturingTicks(producer, template, count);

            int retainedCount = Math.Min(count, activeOrder.Quantity);
            long requiredProgress = activeItems
                .Take(retainedCount)
                .Sum(item =>
                    (long)Math.Max(item.GetConstructionCost() - item.GetManufacturingProgress(), 0)
                );
            requiredProgress +=
                (long)Math.Max(template.GetConstructionCost(), 0)
                * Math.Max(count - activeOrder.Quantity, 0);
            return EstimateManufacturingTicks(producer, type, requiredProgress);
        }

        /// <summary>
        /// Estimates when newly appended copies would finish after the producer's current queue.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The item template to append.</param>
        /// <param name="count">The number of copies to append.</param>
        /// <returns>The estimated completion ticks, or null when no facility can produce the item.</returns>
        public static int? EstimateAppendedCompletionTicks(
            Planet producer,
            IManufacturable template,
            int count
        )
        {
            if (producer == null || template == null || count <= 0)
                return null;

            ManufacturingType type = template.GetManufacturingType();
            long queuedProgress =
                producer.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> queue)
                && queue != null
                    ? queue.Sum(item =>
                        (long)
                            Math.Max(
                                item.GetConstructionCost() - item.GetManufacturingProgress(),
                                0
                            )
                    )
                    : 0;
            long appendedProgress = (long)Math.Max(template.GetConstructionCost(), 0) * count;
            long requiredProgress = Math.Min(int.MaxValue, queuedProgress + appendedProgress);
            return EstimateManufacturingTicks(producer, type, requiredProgress);
        }

        /// <summary>
        /// Estimates completion of one future order using current work and committed facility construction.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="template">The single item to manufacture.</param>
        /// <param name="decisionInterval">Ticks between opportunities to start a new order.</param>
        /// <param name="precedingOrders">Uncommitted earlier choices in this lane, used only for forecasting.</param>
        /// <param name="earliestStartTicks">The earliest decision at which the destination can receive this item.</param>
        /// <param name="precedingStartTicks">Destination-ready ticks for the preceding orders, when supplied.</param>
        /// <returns>Completion ticks, or null when the producer cannot make progress.</returns>
        public int? EstimateScheduledCompletionTicks(
            Planet producer,
            IManufacturable template,
            int decisionInterval,
            IReadOnlyList<IManufacturable> precedingOrders = null,
            int earliestStartTicks = 0,
            IReadOnlyList<int> precedingStartTicks = null
        )
        {
            List<IManufacturable> orders = precedingOrders?.ToList() ?? new();
            List<int> starts = orders
                .Select(
                    (_, index) =>
                        precedingStartTicks != null && index < precedingStartTicks.Count
                            ? precedingStartTicks[index]
                            : 0
                )
                .ToList();
            orders.Add(template);
            starts.Add(earliestStartTicks);
            return EstimateScheduledCompletionTicks(producer, orders, decisionInterval, starts)
                ?.Last();
        }

        /// <summary>Estimates each item in a temporary lane schedule with one production forecast.</summary>
        /// <param name="producer">The production planet.</param>
        /// <param name="orders">The ordered templates, without creating or queueing units.</param>
        /// <param name="decisionInterval">Ticks between opportunities to start each new order.</param>
        /// <param name="earliestStartTicks">Destination-ready ticks for the corresponding orders.</param>
        /// <returns>Each projected completion tick, or null when the schedule cannot make progress.</returns>
        public IReadOnlyList<int> EstimateScheduledCompletionTicks(
            Planet producer,
            IReadOnlyList<IManufacturable> orders,
            int decisionInterval,
            IReadOnlyList<int> earliestStartTicks = null
        )
        {
            return EstimateProductionSchedule(
                producer,
                orders,
                decisionInterval,
                earliestStartTicks
            )
                ?.Select(order => order.Completion)
                .ToList();
        }

        /// <summary>Projects lane availability and facility phases after committed work finishes.</summary>
        /// <param name="producer">The production planet.</param>
        /// <param name="type">The production lane category.</param>
        /// <param name="ignoreInterruptibleWork">Whether an unfinished filler order will be replaced before production starts.</param>
        /// <returns>Availability, cycle increment, and facilities with absolute activation ticks, or null when unavailable.</returns>
        public ProductionForecast? GetProductionCapacityForecast(
            Planet producer,
            ManufacturingType type,
            bool ignoreInterruptibleWork = false
        )
        {
            if (producer == null || producer.IsDestroyed || !producer.IsColonized)
                return null;
            var facilities = GetProjectedFacilities(producer, type);
            double increment = GetProductionCycleIncrement(_game, producer);
            if (facilities.Count == 0 || increment <= 0)
                return null;
            producer.GetManufacturingQueue().TryGetValue(type, out List<IManufacturable> queue);
            if (
                ignoreInterruptibleWork
                && GetInterruptibleManufacturingOrder(producer, type) != null
            )
                queue = null;
            long queuedWork =
                queue?.Sum(item =>
                    (long)Math.Max(0, item.GetConstructionCost() - item.GetManufacturingProgress())
                )
                ?? 0;
            int? queueTicks = EstimateProjectedTicks(facilities, queuedWork, increment);
            if (!queueTicks.HasValue || queueTicks == int.MaxValue)
                return null;
            int elapsed = queue?.Count > 0 ? Math.Max(1, queueTicks.Value) : 0;
            if (elapsed > 0)
                facilities = AdvanceProjectedFacilities(facilities, elapsed, increment);
            return new ProductionForecast(
                type,
                elapsed,
                increment,
                facilities.ConvertAll(facility =>
                    (
                        facility.Rate,
                        facility.CycleProgress,
                        facility.Ready,
                        (int)Math.Min(int.MaxValue, (long)facility.ActivationTicks + elapsed)
                    )
                )
            );
        }

        /// <summary>Projects starts and completions using the shared manufacturing capacity forecast.</summary>
        /// <param name="producer">The production planet.</param>
        /// <param name="orders">Uncommitted templates in their proposed lane order.</param>
        /// <param name="decisionInterval">Ticks between opportunities to start work.</param>
        /// <param name="earliestStartTicks">Destination-ready ticks for the corresponding orders.</param>
        /// <returns>Start and completion ticks for each order, or null when the lane cannot progress.</returns>
        public IReadOnlyList<(int Start, int Completion)> EstimateProductionSchedule(
            Planet producer,
            IReadOnlyList<IManufacturable> orders,
            int decisionInterval,
            IReadOnlyList<int> earliestStartTicks = null
        )
        {
            if (
                producer == null
                || orders == null
                || orders.Count == 0
                || orders[0] == null
                || producer.IsDestroyed
                || !producer.IsColonized
            )
                return null;

            ManufacturingType type = orders[0].GetManufacturingType();
            var capacity = GetProductionCapacityForecast(producer, type);
            if (!capacity.HasValue)
                return null;
            int orderIndex = 0;
            List<(int Start, int Completion)> completion = new();
            foreach (IManufacturable order in orders)
            {
                int release =
                    earliestStartTicks != null && orderIndex < earliestStartTicks.Count
                        ? earliestStartTicks[orderIndex]
                        : 0;
                orderIndex++;
                var next = EstimateNextProduction(capacity.Value, order, decisionInterval, release);
                if (!next.HasValue)
                    return null;
                capacity = next.Value.Forecast;
                if (capacity.Value.AvailableTick == int.MaxValue)
                    return completion
                        .Concat(
                            Enumerable.Repeat(
                                (int.MaxValue, int.MaxValue),
                                orders.Count - completion.Count
                            )
                        )
                        .ToList();
                completion.Add((next.Value.Start, capacity.Value.AvailableTick));
            }
            return completion;
        }

        /// <summary>Appends one unit to a projected lane, retaining phases so later estimates can reuse this prefix.</summary>
        /// <param name="capacity">The lane state after previously forecast work.</param>
        /// <param name="order">The next unit template.</param>
        /// <param name="decisionInterval">Ticks between opportunities to start an order.</param>
        /// <param name="earliestStartTicks">The earliest permitted start.</param>
        /// <returns>The projected start and resulting lane state, or null when production is invalid.</returns>
        public static (int Start, ProductionForecast Forecast)? EstimateNextProduction(
            ProductionForecast capacity,
            IManufacturable order,
            int decisionInterval,
            int earliestStartTicks = 0
        )
        {
            if (
                order == null
                || order.GetManufacturingType() != capacity.Type
                || capacity.Facilities == null
                || capacity.Facilities.Count == 0
                || capacity.Increment <= 0
            )
                return null;
            long available = Math.Max(
                capacity.AvailableTick,
                capacity.Facilities.Min(facility => facility.ActivationTicks)
            );
            available = Math.Max(available, earliestStartTicks);
            int interval = Math.Max(1, decisionInterval);
            long start = (available + interval - 1) / interval * interval;
            if (start >= int.MaxValue)
                return (
                    int.MaxValue,
                    new ProductionForecast(
                        capacity.Type,
                        int.MaxValue,
                        capacity.Increment,
                        capacity.Facilities
                    )
                );
            var facilities = capacity
                .Facilities.Select(facility =>
                    (
                        facility.Rate,
                        facility.CycleProgress,
                        facility.Ready,
                        ActivationTicks: (int)Math.Max(0, facility.ActivationTicks - start)
                    )
                )
                .ToList();
            int? duration = EstimateProjectedTicks(
                facilities,
                Math.Max(0, order.GetConstructionCost()),
                capacity.Increment
            );
            if (!duration.HasValue)
                return null;
            int workTicks = Math.Max(1, duration.Value);
            int completion = (int)Math.Min(int.MaxValue, start + workTicks);
            if (completion == int.MaxValue)
                return (
                    int.MaxValue,
                    new ProductionForecast(
                        capacity.Type,
                        int.MaxValue,
                        capacity.Increment,
                        capacity.Facilities
                    )
                );
            var nextFacilities = AdvanceProjectedFacilities(
                    facilities,
                    workTicks,
                    capacity.Increment
                )
                .Select(facility =>
                    (
                        facility.Rate,
                        facility.CycleProgress,
                        facility.Ready,
                        ActivationTicks: (int)
                            Math.Min(int.MaxValue, (long)facility.ActivationTicks + completion)
                    )
                );
            return (
                (int)start,
                new ProductionForecast(
                    capacity.Type,
                    completion,
                    capacity.Increment,
                    nextFacilities
                )
            );
        }

        /// <summary>
        /// Advances projected cycle phases through work while discarding unused completion points.
        /// </summary>
        /// <param name="facilities">The capacity at the start of work.</param>
        /// <param name="ticks">The work duration.</param>
        /// <param name="increment">The current cycle increment.</param>
        /// <returns>The capacity and cycle phases after completion.</returns>
        private static List<(
            int Rate,
            double CycleProgress,
            bool Ready,
            int ActivationTicks
        )> AdvanceProjectedFacilities(
            IEnumerable<(
                int Rate,
                double CycleProgress,
                bool Ready,
                int ActivationTicks
            )> facilities,
            int ticks,
            double increment
        )
        {
            return facilities
                .Select(facility =>
                    (
                        facility.Rate,
                        (
                            facility.CycleProgress
                            + Math.Max(
                                0,
                                ticks - facility.ActivationTicks - (facility.Ready ? 1 : 0)
                            ) * increment
                        ) % facility.Rate,
                        facility.Ready && ticks <= facility.ActivationTicks,
                        Math.Max(0, facility.ActivationTicks - ticks)
                    )
                )
                .ToList();
        }

        /// <summary>
        /// Collects usable facilities and known activation dates without changing production state.
        /// </summary>
        /// <param name="producer">The planet whose capacity is projected.</param>
        /// <param name="type">The required production category.</param>
        /// <returns>Facility capacity with activation times.</returns>
        private List<(
            int Rate,
            double CycleProgress,
            bool Ready,
            int ActivationTicks
        )> GetProjectedFacilities(Planet producer, ManufacturingType type)
        {
            List<(int Rate, double CycleProgress, bool Ready, int ActivationTicks)> facilities =
                new();
            foreach (Building facility in producer.GetChildren<Building>())
            {
                if (
                    facility.GetProductionType() != type
                    || facility.GetProcessRate() <= 0
                    || facility.GetOwnerInstanceID() != producer.GetOwnerInstanceID()
                )
                    continue;

                if (
                    facility.ManufacturingStatus == ManufacturingStatus.Complete
                    && facility.Movement == null
                )
                {
                    facilities.Add(
                        (
                            facility.GetProcessRate(),
                            facility.ProductionCycleProgress,
                            facility.ProductionPointReady,
                            0
                        )
                    );
                    continue;
                }

                if (type == ManufacturingType.Building)
                    continue;

                int? activation = GetFacilityActivationTicks(producer, facility);
                if (activation.HasValue && activation.Value < int.MaxValue)
                    facilities.Add((facility.GetProcessRate(), 0, false, activation.Value));
            }
            return facilities;
        }

        /// <summary>
        /// Estimates activation only for a live construction order or an actual delivery.
        /// </summary>
        /// <param name="destination">The planet receiving the facility.</param>
        /// <param name="facility">The committed facility.</param>
        /// <returns>Activation ticks, or null when there is no supported completion path.</returns>
        private int? GetFacilityActivationTicks(Planet destination, Building facility)
        {
            if (
                facility.Movement != null
                && facility.ManufacturingStatus != ManufacturingStatus.Building
            )
                return Math.Max(0, facility.Movement.TicksRemaining());

            Planet constructionProducer = _game.GetSceneNodeByInstanceID<Planet>(
                facility.ProducerPlanetID
            );
            if (
                constructionProducer == null
                || constructionProducer.GetOwnerInstanceID() != destination.GetOwnerInstanceID()
                || constructionProducer.IsDestroyed
                || !constructionProducer.IsColonized
                || facility.ManufacturingStatus != ManufacturingStatus.Building
                || !constructionProducer
                    .GetManufacturingQueue()
                    .TryGetValue(ManufacturingType.Building, out List<IManufacturable> queue)
                || queue == null
            )
                return null;

            long remainingWork = 0;
            foreach (IManufacturable item in queue)
            {
                remainingWork += Math.Max(
                    0,
                    item.GetConstructionCost() - item.GetManufacturingProgress()
                );
                if (!ReferenceEquals(item, facility))
                    continue;

                int? completion = EstimateProjectedTicks(
                    GetProjectedFacilities(constructionProducer, ManufacturingType.Building),
                    remainingWork,
                    GetProductionCycleIncrement(_game, constructionProducer)
                );
                if (
                    !completion.HasValue
                    || !new MovementQueries(_game).TryEstimateManufacturedTransitTicks(
                        facility,
                        constructionProducer,
                        destination,
                        out int transitTicks
                    )
                )
                    return null;
                return (int)Math.Min(int.MaxValue, (long)completion.Value + transitTicks);
            }
            return null;
        }

        /// <summary>
        /// Computes the current production-cycle rate, holding existing conditions constant.
        /// </summary>
        /// <param name="game">The game supplying production modifiers.</param>
        /// <param name="producer">The production planet.</param>
        /// <returns>The production-cycle increment per tick.</returns>
        public static double GetProductionCycleIncrement(GameRoot game, Planet producer)
        {
            if (producer.IsInUprising)
                return 0;

            int blockadePercent = producer.GetBlockadeProductionModifier(
                game.Config.Blockade.CapitalShipProductionPenaltyPercent,
                game.Config.Blockade.FighterProductionPenaltyPercent
            );
            int difficultyPercent = game.GetDifficultyModifier(
                producer.GetOwnerInstanceID()
            ).ManufacturingSpeedPercent;
            return (double)blockadePercent
                * difficultyPercent
                / (_percentageScale * _percentageScale);
        }

        /// <summary>
        /// Finds the earliest tick supplying the required work from time-varying capacity.
        /// </summary>
        /// <param name="facilities">Facilities and their activation dates.</param>
        /// <param name="work">The remaining production points.</param>
        /// <param name="increment">The production-cycle increase per tick.</param>
        /// <returns>The required ticks, or null when no capacity can progress.</returns>
        private static int? EstimateProjectedTicks(
            IReadOnlyList<(
                int Rate,
                double CycleProgress,
                bool Ready,
                int ActivationTicks
            )> facilities,
            long work,
            double increment
        )
        {
            if (work <= 0)
                return 0;
            if (facilities.Count == 0 || increment <= 0)
                return null;

            int low = 1;
            double upperBound = facilities.Min(facility =>
                facility.ActivationTicks
                + (facility.Ready ? 1 : 0)
                + Math.Ceiling((double)work * facility.Rate / increment)
            );
            int high = (int)Math.Max(1, Math.Min(int.MaxValue, upperBound));
            while (
                high < int.MaxValue
                && GetProjectedProgress(facilities, high, increment, work) < work
            )
                high = (int)Math.Min(int.MaxValue, (long)high * 2);
            if (GetProjectedProgress(facilities, high, increment, work) < work)
                return int.MaxValue;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (GetProjectedProgress(facilities, middle, increment, work) >= work)
                    high = middle;
                else
                    low = middle + 1;
            }
            return low;
        }

        /// <summary>
        /// Counts production points from facilities only after their estimated activation.
        /// </summary>
        /// <param name="facilities">The projected facility capacity.</param>
        /// <param name="tick">The future tick being inspected.</param>
        /// <param name="increment">The cycle increase per tick.</param>
        /// <param name="requiredWork">The point count at which counting may stop.</param>
        /// <returns>The supplied points, capped at the required work.</returns>
        private static long GetProjectedProgress(
            IReadOnlyList<(
                int Rate,
                double CycleProgress,
                bool Ready,
                int ActivationTicks
            )> facilities,
            int tick,
            double increment,
            long requiredWork
        )
        {
            long progress = 0;
            foreach (
                (int rate, double cycleProgress, bool ready, int activationTicks) in facilities
            )
            {
                if (tick <= activationTicks)
                    continue;
                progress += GetFacilityProductionPoints(
                    rate,
                    cycleProgress,
                    ready,
                    increment,
                    tick - activationTicks
                );
                if (progress >= requiredWork)
                    return requiredWork;
            }
            return progress;
        }

        /// <summary>Projects cumulative output for each active tick of one facility.</summary>
        /// <param name="rate">The process rate.</param>
        /// <param name="cycleProgress">The initial cycle progress.</param>
        /// <param name="ready">Whether a production point is already ready.</param>
        /// <param name="increment">Cycle progress per active tick.</param>
        /// <param name="ticks">The active-time horizon.</param>
        /// <returns>Cumulative output from tick zero through the horizon.</returns>
        public static IReadOnlyList<long> GetProductionPointForecast(
            int rate,
            double cycleProgress,
            bool ready,
            double increment,
            int ticks
        )
        {
            return Enumerable
                .Range(0, ticks + 1)
                .Select(tick =>
                    GetFacilityProductionPoints(rate, cycleProgress, ready, increment, tick)
                )
                .ToArray();
        }

        /// <summary>Counts facility output with the same arithmetic used by lane completion estimates.</summary>
        /// <param name="rate">The process rate.</param>
        /// <param name="cycleProgress">The initial cycle progress.</param>
        /// <param name="ready">Whether a point is ready at the first active tick.</param>
        /// <param name="increment">Cycle progress per tick.</param>
        /// <param name="ticks">The number of active ticks.</param>
        /// <returns>The cumulative production-point count.</returns>
        public static long GetFacilityProductionPoints(
            int rate,
            double cycleProgress,
            bool ready,
            double increment,
            int ticks
        )
        {
            return ticks <= 0
                ? 0
                : (ready ? 1L : 0L)
                    + (long)
                        Math.Floor(
                            (cycleProgress + Math.Max(0, ticks - (ready ? 1 : 0)) * increment)
                                / rate
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
        /// Estimates when one item in a planet's manufacturing queue will be completed.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="item">The queued item to inspect.</param>
        /// <returns>The estimated remaining ticks through the item, or null when unavailable.</returns>
        public static int? EstimateCompletionTicks(Planet producer, IManufacturable item)
        {
            if (
                producer == null
                || item == null
                || item.GetManufacturingStatus() != ManufacturingStatus.Building
                || !producer
                    .GetManufacturingQueue()
                    .TryGetValue(item.GetManufacturingType(), out List<IManufacturable> queue)
                || queue == null
            )
                return null;

            long requiredProgress = 0;
            foreach (IManufacturable queuedItem in queue)
            {
                requiredProgress += Math.Max(
                    queuedItem.GetConstructionCost() - queuedItem.GetManufacturingProgress(),
                    0
                );
                if (
                    ReferenceEquals(queuedItem, item)
                    || (
                        !string.IsNullOrEmpty(item.InstanceID)
                        && string.Equals(
                            queuedItem.InstanceID,
                            item.InstanceID,
                            StringComparison.Ordinal
                        )
                    )
                )
                    return EstimateManufacturingTicks(
                        producer,
                        item.GetManufacturingType(),
                        requiredProgress
                    );
            }

            return null;
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

            List<Building> facilities = producer
                .GetProductionFacilities(type)
                .Where(facility => facility.GetProcessRate() > 0)
                .ToList();
            if (facilities.Count == 0)
                return null;

            long fastestUpperBound = GetNextProductionTick(facilities[0]);
            long fastestRate = facilities.Min(facility => facility.GetProcessRate());
            if (requiredProgress > 1)
            {
                long remainingPoints = requiredProgress - 1;
                fastestUpperBound =
                    remainingPoints > (int.MaxValue - fastestUpperBound) / fastestRate
                        ? int.MaxValue
                        : fastestUpperBound + remainingPoints * fastestRate;
            }

            long low = 1;
            long high = Math.Min(fastestUpperBound, int.MaxValue);
            if (GetProductionProgressAtTick(facilities, high, requiredProgress) < requiredProgress)
                return int.MaxValue;

            // Each pass strictly shrinks this int-sized interval, so it converges within 31 passes.
            while (low < high)
            {
                long middle = low + (high - low) / 2;
                if (
                    GetProductionProgressAtTick(facilities, middle, requiredProgress)
                    >= requiredProgress
                )
                    high = middle;
                else
                    low = middle + 1;
            }

            return (int)low;
        }

        /// <summary>
        /// Counts production points available by one future tick under uninterrupted conditions.
        /// </summary>
        /// <param name="facilities">The active production facilities.</param>
        /// <param name="tick">The one-based future tick to inspect.</param>
        /// <param name="requiredProgress">The point count at which counting may stop.</param>
        /// <returns>The available production points, capped at the required amount.</returns>
        private static long GetProductionProgressAtTick(
            IReadOnlyList<Building> facilities,
            long tick,
            long requiredProgress
        )
        {
            long points = 0;
            foreach (Building facility in facilities)
            {
                long firstTick = GetNextProductionTick(facility);
                if (tick < firstTick)
                    continue;

                points += 1 + (tick - firstTick) / facility.GetProcessRate();
                if (points >= requiredProgress)
                    return requiredProgress;
            }

            return points;
        }

        /// <summary>
        /// Returns the next future tick on which a facility can supply a production point.
        /// </summary>
        /// <param name="facility">The active production facility.</param>
        /// <returns>A one-based future tick.</returns>
        private static long GetNextProductionTick(Building facility)
        {
            if (facility.ProductionPointReady)
                return 1;

            return Math.Max(
                1L,
                (long)Math.Ceiling(facility.GetProcessRate() - facility.ProductionCycleProgress)
            );
        }

        /// <summary>
        /// Determines whether a faction can afford a complete manufacturing order.
        /// </summary>
        /// <param name="faction">The faction committing the order.</param>
        /// <param name="item">The item template being manufactured.</param>
        /// <param name="count">The number of copies to queue.</param>
        /// <param name="releasedMaintenance">Maintenance released by a replaced project.</param>
        /// <returns>True when the complete order remains within maintenance capacity.</returns>
        private bool HasMaintenanceHeadroom(
            Faction faction,
            IManufacturable item,
            int count,
            int releasedMaintenance = 0
        )
        {
            if (faction == null)
                return false;

            int maintenanceCost = item.GetMaintenanceCost();
            if (maintenanceCost <= 0 || ManufacturingRules.IsResourceFacility(item))
                return true;

            int projectedHeadroom =
                ResourceProductionQueries.CalculateMaintenanceCapacity(_game, faction)
                - faction.GetTotalProjectedMaintenanceCost();
            return projectedHeadroom + releasedMaintenance - maintenanceCost * count >= 0;
        }
    }
}
