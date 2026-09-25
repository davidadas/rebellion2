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
    /// <summary>Evaluates manufacturing eligibility, capacity and completion estimates without changing the game.</summary>
    public sealed class ManufacturingQueries
    {
        private readonly GameRoot _game;

        /// <summary>Creates manufacturing queries for the supplied game.</summary>
        /// <param name="game">The game whose faction maintenance is evaluated.</param>
        public ManufacturingQueries(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
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
        /// <returns>True when the order is structurally valid without considering maintenance.</returns>
        public static bool CanAcceptManufacturingOrder(
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
                && producer.GetProductionFacilityCount(template.GetManufacturingType()) > 0
                && HasDestinationCapacity(producer, destination, template, count);
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

                if (
                    !string.Equals(
                        planet.GetOwnerInstanceID(),
                        ownerInstanceId,
                        StringComparison.Ordinal
                    )
                )
                {
                    return template is Regiment
                        && !planet.IsColonized
                        && string.IsNullOrEmpty(planet.GetOwnerInstanceID());
                }

                return template switch
                {
                    Regiment _ => true,
                    SpecialForces _ => planet.IsColonized,
                    Starfighter _ => planet.IsColonized,
                    Building _ => planet.GetAvailableEnergy() >= count,
                    _ => false,
                };
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

            IEnumerable<CapitalShip> carriers = fleet
                .GetChildren<CapitalShip>()
                .Where(IsManufacturingCarrierAvailable);
            if (template is Starfighter)
                return carriers.Sum(ship => ship.GetExcessStarfighterCapacity()) >= count;

            return template is Regiment
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

            if (template is Starfighter)
                return capitalShip.GetExcessStarfighterCapacity() >= count;

            return template is Regiment && capitalShip.GetExcessRegimentCapacity() >= count;
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
        /// Returns whether a capital ship can receive manufacturing output.
        /// </summary>
        /// <param name="ship">The destination ship to inspect.</param>
        /// <returns>True when the ship is complete and not in transit.</returns>
        internal static bool IsManufacturingCarrierAvailable(CapitalShip ship)
        {
            return ship?.ManufacturingStatus == ManufacturingStatus.Complete
                && ((IMovable)ship).GetTransitMovement() == null;
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
            if (maintenanceCost <= 0)
                return true;

            return faction.ProjectedMaintenanceHeadroom
                    + releasedMaintenance
                    - maintenanceCost * count
                >= 0;
        }
    }
}
