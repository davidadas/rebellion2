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
    /// Advances manufacturing queues during a game tick.
    /// </summary>
    internal sealed class ManufacturingTickProcessor : ITickProcessor
    {
        private const int _productionRateScale = 100;

        private readonly ManufacturingCommands _commands;
        private readonly MovementCommands _movementSystem;
        private GameRoot _game;

        /// <summary>
        /// Creates manufacturing tick processing.
        /// </summary>
        /// <param name="commands">The manufacturing order operations and pending results.</param>
        /// <param name="movementSystem">Dispatches completed items to their destinations.</param>
        public ManufacturingTickProcessor(
            ManufacturingCommands commands,
            MovementCommands movementSystem
        )
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _movementSystem =
                movementSystem ?? throw new ArgumentNullException(nameof(movementSystem));
        }

        /// <summary>
        /// Advances every active manufacturing queue.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The manufacturing results produced during the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            List<GameResult> results = _commands.TakePendingResults();
            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
                results.AddRange(ProcessPlanet(planet));

            return results;
        }

        /// <summary>
        /// Processes all manufacturing queues on one planet.
        /// </summary>
        /// <param name="planet">The planet whose queues are processed.</param>
        /// <returns>Manufacturing results produced by the planet.</returns>
        private List<GameResult> ProcessPlanet(Planet planet)
        {
            List<GameResult> results = new List<GameResult>();
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (queue == null)
                return results;

            string ownerInstanceId = planet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerInstanceId))
                return results;

            double cycleIncrement = GetProductionCycleIncrement(planet);
            foreach (ManufacturingType type in GetActiveManufacturingTypes(planet, queue))
            {
                queue.TryGetValue(type, out List<IManufacturable> items);
                bool hadQueuedItems = items?.Count > 0;

                items?.RemoveAll(item => !IsQueuedItemActive(item));
                RemoveInvalidPlanetDestinationItems(planet, items);
                bool hasQueuedItems = items?.Count > 0;
                if (hasQueuedItems && planet.GetProductionFacilityCount(type) == 0)
                {
                    _commands.ClearQueue(planet, type);
                    results.Add(CreateQueueIdleResult(planet, type));
                    continue;
                }

                List<Building> readyFacilities = AdvanceProductionFacilities(
                    planet,
                    type,
                    hasQueuedItems,
                    cycleIncrement
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
                .GetChildren<Building>()
                .Where(facility =>
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
        /// Cancels unfinished building and troop orders whose planet destination is no longer
        /// controlled by the producing faction.
        /// </summary>
        /// <param name="producer">The planet performing the manufacturing.</param>
        /// <param name="items">The production lane to validate.</param>
        private void RemoveInvalidPlanetDestinationItems(
            Planet producer,
            List<IManufacturable> items
        )
        {
            if (producer == null || items == null || items.Count == 0)
                return;

            foreach (IManufacturable item in items.ToList())
            {
                if (!HasInvalidPlanetDestination(item))
                    continue;

                _commands.CancelManufacturing(item, producer.GetOwnerInstanceID());
            }
        }

        /// <summary>
        /// Returns whether an unfinished building or troop has a directly assigned planet that
        /// is not controlled by its producer. Fleet and capital-ship destinations are excluded.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>True when the invalid planet destination condition is met; otherwise false.</returns>
        private static bool HasInvalidPlanetDestination(IManufacturable item)
        {
            if (
                item is not ISceneNode sceneNode
                || item is CapitalShip
                || item is Starfighter
                || sceneNode.GetParent() is not Planet destination
            )
            {
                return false;
            }

            return !string.Equals(
                destination.GetOwnerInstanceID(),
                item.ProducerOwnerID,
                StringComparison.Ordinal
            );
        }

        /// <summary>
        /// Advances production facility timers and returns facilities ready to spend progress.
        /// </summary>
        /// <param name="planet">The production planet.</param>
        /// <param name="type">The manufacturing type being processed.</param>
        /// <param name="hasQueuedItems">Whether the facility type currently has work.</param>
        /// <param name="cycleIncrement">The production progress available this tick.</param>
        /// <returns>Facilities with a ready production point.</returns>
        private List<Building> AdvanceProductionFacilities(
            Planet planet,
            ManufacturingType type,
            bool hasQueuedItems,
            double cycleIncrement
        )
        {
            List<Building> productionFacilities = planet
                .GetChildren<Building>()
                .Where(facility =>
                    facility.ProductionType == type
                    && facility.ManufacturingStatus == ManufacturingStatus.Complete
                    && facility.Movement == null
                    && facility.ProcessRate > 0
                )
                .ToList();

            List<Building> readyFacilities = new List<Building>();
            foreach (Building facility in productionFacilities)
            {
                int readyPointCount = AdvanceProductionFacility(
                    facility,
                    hasQueuedItems,
                    cycleIncrement
                );
                for (int pointIndex = 0; pointIndex < readyPointCount; pointIndex++)
                    readyFacilities.Add(facility);
            }

            return readyFacilities;
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
            int modifier = planet.GetBlockadeProductionModifier(
                config.CapitalShipProductionPenaltyPercent,
                config.FighterProductionPenaltyPercent
            );
            int difficultyPercent = _game
                .GetDifficultyModifier(planet.GetOwnerInstanceID())
                .ManufacturingSpeedPercent;
            return (double)modifier
                * difficultyPercent
                / (_productionRateScale * _productionRateScale);
        }

        /// <summary>
        /// Advances one production facility toward its next production point.
        /// </summary>
        /// <param name="facility">The facility to advance.</param>
        /// <param name="hasQueuedItems">Whether the facility type currently has work.</param>
        /// <param name="cycleIncrement">The production progress available this tick.</param>
        /// <returns>The number of production points made ready this tick.</returns>
        private int AdvanceProductionFacility(
            Building facility,
            bool hasQueuedItems,
            double cycleIncrement
        )
        {
            if (facility.ProductionPointReady)
                return 1;

            if (cycleIncrement <= 0)
                return 0;

            if (!hasQueuedItems)
                return 0;

            int processRate = facility.GetProcessRate();
            if (processRate <= 0)
            {
                facility.ProductionCycleProgress = 0;
                facility.ProductionPointReady = false;
                return 0;
            }

            facility.ProductionCycleProgress += cycleIncrement;
            int readyPointCount = (int)(facility.ProductionCycleProgress / processRate);
            if (readyPointCount <= 0)
                return 0;

            facility.ProductionCycleProgress -= readyPointCount * processRate;
            facility.ProductionPointReady = true;
            return readyPointCount;
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
        /// Finishes construction of an item and dispatches it from the production planet.
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

            List<GameResult> results = new List<GameResult>
            {
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
            if (item is not IMovable movable || movable.Movement == null)
            {
                results.Insert(
                    0,
                    new GameObjectDeployedResult { GameObject = item, Tick = _game.CurrentTick }
                );
            }

            return results;
        }

        /// <summary>
        /// Advances a finished item into delivery and starts movement when needed.
        /// </summary>
        /// <param name="productionPlanet">The production planet.</param>
        /// <param name="item">The completed item.</param>
        private void MarkCompleteAndDispatch(Planet productionPlanet, IManufacturable item)
        {
            item.ManufacturingQueueSequence = 0;

            ContainerNode destination = item.GetParent() as ContainerNode;
            if (destination == null)
            {
                item.ManufacturingStatus = ManufacturingStatus.Complete;
                return;
            }

            item.ManufacturingStatus = ManufacturingStatus.Delivering;
            _movementSystem.RequestMove(item, destination, productionPlanet);
            if (item.Movement == null)
                item.ManufacturingStatus = ManufacturingStatus.Complete;
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
    }
}
