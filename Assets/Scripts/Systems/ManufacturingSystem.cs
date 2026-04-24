using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages unit and facility production during each game tick.
    /// </summary>
    public class ManufacturingSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movementSystem;
        private readonly List<GameResult> _pendingResults = new List<GameResult>();

        /// <summary>
        /// Creates a new ManufacturingSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for capital ship progress rolls.</param>
        /// <param name="movementSystem">Used to dispatch completed units to their destinations.</param>
        public ManufacturingSystem(
            GameRoot game,
            IRandomNumberProvider provider = null,
            MovementSystem movementSystem = null
        )
        {
            _game = game;
            _provider = provider;
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
        /// Enqueues an item for production at <paramref name="planet"/>, delivering to
        /// <paramref name="destination"/> on completion. Capital ships are placed in a new
        /// fleet created at the destination planet.
        /// </summary>
        public bool Enqueue(
            Planet planet,
            IManufacturable item,
            Planet destination,
            bool ignoreCost = false
        )
        {
            Faction faction = GetValidatedFaction(planet, item, ignoreCost);
            if (faction == null)
                return false;

            if (item is CapitalShip)
            {
                GameLogger.Warning(
                    "Capital ship production requires an existing fleet at the destination."
                );
                return false;
            }

            if (!destination.CanAcceptChild((ISceneNode)item))
                return false;

            _game.AttachNode((ISceneNode)item, destination);

            _pendingResults.Add(
                new ManufacturingDeployedResult
                {
                    Faction = faction,
                    DeployedObject = item as IGameEntity,
                    Location = destination as IGameEntity,
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
        public bool Enqueue(
            Planet planet,
            IManufacturable item,
            Fleet destination,
            bool ignoreCost = false
        )
        {
            Faction faction = GetValidatedFaction(planet, item, ignoreCost);
            if (faction == null)
                return false;

            ISceneNode parent = destination;

            // Fleet only accepts CapitalShips directly. Route other unit types
            // to an appropriate CapitalShip within the destination fleet.
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

            if (!parent.CanAcceptChild((ISceneNode)item))
                return false;

            _game.AttachNode((ISceneNode)item, parent);

            _pendingResults.Add(
                new ManufacturingDeployedResult
                {
                    Faction = faction,
                    DeployedObject = item as IGameEntity,
                    Location = destination as IGameEntity,
                    Tick = _game.CurrentTick,
                }
            );

            CommitToQueue(planet, item, ignoreCost);
            return true;
        }

        /// <summary>
        /// Validates the planet's owner and checks production cost affordability.
        /// </summary>
        /// <param name="planet">The planet where production would occur.</param>
        /// <param name="item">The item to produce.</param>
        /// <param name="ignoreCost">If true, skips the cost affordability check.</param>
        /// <returns>The owning faction, or null if validation fails.</returns>
        private Faction GetValidatedFaction(Planet planet, IManufacturable item, bool ignoreCost)
        {
            if (planet == null || item == null)
                return null;

            string ownerInstanceId = planet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerInstanceId))
                return null;

            Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            if (faction == null)
                return null;

            if (!ignoreCost)
            {
                if (_game.GetRefinedMaterials(faction) < item.GetConstructionCost())
                    return null;
            }

            return faction;
        }

        /// <summary>
        /// Initializes the item's manufacturing state and adds it to the planet's queue.
        /// Deducts the full construction cost from the producing faction's stockpile upfront,
        /// unless <paramref name="ignoreCost"/> is true.
        /// </summary>
        /// <param name="planet">The planet producing the item.</param>
        /// <param name="item">The item to enqueue for production.</param>
        /// <param name="ignoreCost">If true, skips stockpile deduction (free build).</param>
        private void CommitToQueue(Planet planet, IManufacturable item, bool ignoreCost)
        {
            item.ManufacturingStatus = ManufacturingStatus.Building;
            item.ManufacturingProgress = 0;
            item.ProducerOwnerID = planet.GetOwnerInstanceID();
            item.ProducerPlanetID = planet.GetInstanceID();

            if (!ignoreCost)
            {
                Faction producer = _game.GetFactionByOwnerInstanceID(planet.GetOwnerInstanceID());
                if (producer != null)
                    producer.RefinedMaterialStockpile -= item.GetConstructionCost();
            }

            planet.AddToManufacturingQueue(item);

            _pendingResults.Add(
                new GameObjectCreatedResult
                {
                    GameObject = item as IGameEntity,
                    Tick = _game.CurrentTick,
                }
            );

            GameLogger.Log(
                $"Enqueued {item.GetDisplayName()} for production at {planet.GetDisplayName()} (cost: {item.GetConstructionCost()})"
            );
        }

        /// <summary>
        /// Determines how much manufacturing progress a capital ship makes this tick using the
        /// CSCRHT table. Performs a success roll first; returns 0 if it fails. On success,
        /// returns the progress amount from the table.
        /// </summary>
        /// <returns>Progress to apply this tick, or 0 if the success check fails.</returns>
        private int RollCapitalShipProgress()
        {
            GameConfig.ProductionConfig config = _game.Config.Production;

            // Success check: roll must be below threshold
            int successRoll = _provider.NextInt(0, config.CapitalShipSuccessRollRange);
            if (successRoll >= config.CapitalShipSuccessThreshold)
                return 0;

            // Progress roll: look up in CSCRHT table
            int progressRoll = _provider.NextInt(0, config.CapitalShipProgressRollRange);
            ProbabilityTable cscrht = new ProbabilityTable(config.CapitalShipProgressTable);
            return cscrht.Lookup(progressRoll);
        }

        private List<GameResult> ProcessPlanetManufacturing(Planet planet)
        {
            List<GameResult> results = new List<GameResult>();
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (queue == null || queue.Count == 0)
                return results;

            // Process each manufacturing type queue
            foreach (KeyValuePair<ManufacturingType, List<IManufacturable>> kvp in queue.ToList())
            {
                ManufacturingType type = kvp.Key;
                List<IManufacturable> items = kvp.Value;

                if (items == null || items.Count == 0)
                    continue;

                // Calculate progress increment based on planet's production rate
                int progressIncrement = planet.GetProductionRate(type);

                // Apply blockade production penalty (KDY defense negates it)
                if (planet.IsBlockadePenalized())
                {
                    int modifier = planet.GetBlockadeModifier(
                        _game.Config.Production.BlockadeCapitalShipPenalty,
                        _game.Config.Production.BlockadeFighterPenalty
                    );
                    progressIncrement = (progressIncrement * modifier) / 100;
                }

                List<IManufacturable> completed = DistributeProgress(
                    items,
                    progressIncrement,
                    planet,
                    results
                );
                IManufacturable lastCompleted = null;
                foreach (IManufacturable item in completed)
                {
                    results.AddRange(CompleteManufacturing(planet, item));
                    lastCompleted = item;
                }

                if (lastCompleted != null)
                {
                    List<IManufacturable> remaining = planet
                        .GetManufacturingQueue()
                        .TryGetValue(type, out List<IManufacturable> rem)
                        ? rem
                        : new List<IManufacturable>();
                    if (remaining.Count == 0)
                    {
                        results.Add(
                            new ManufacturingCompletedResult
                            {
                                GameObject = lastCompleted as IGameEntity,
                                ProductionPlanet = planet,
                                Faction = _game.GetFactionByOwnerInstanceID(
                                    planet.GetOwnerInstanceID()
                                ),
                                ProductType = type,
                                ProductName = lastCompleted.GetDisplayName(),
                                Tick = _game.CurrentTick,
                            }
                        );
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Distributes a production progress increment across the queue for one manufacturing type.
        /// Surplus progress from a completed item carries over to the next item in the queue.
        /// Capital ships consume the entire tick slot and do not overflow.
        /// Returns the list of items that completed this tick; the caller is responsible for
        /// calling CompleteManufacturing on each.
        /// </summary>
        /// <param name="items">The ordered list of items in this type's queue.</param>
        /// <param name="progressIncrement">Total progress available this tick.</param>
        /// <param name="planet">The planet where production is occurring.</param>
        /// <param name="results">Result list to append progress events to.</param>
        private List<IManufacturable> DistributeProgress(
            List<IManufacturable> items,
            int progressIncrement,
            Planet planet,
            List<GameResult> results
        )
        {
            List<IManufacturable> completed = new List<IManufacturable>();
            Faction faction = _game.GetFactionByOwnerInstanceID(planet.GetOwnerInstanceID());

            while (progressIncrement > 0 && items.Count > 0)
            {
                IManufacturable activeItem = items[0];

                // Capital ships use probabilistic CSCRHT progression
                if (activeItem is CapitalShip)
                {
                    int csProgress = RollCapitalShipProgress();
                    if (csProgress > 0)
                    {
                        activeItem.IncrementManufacturingProgress(csProgress);
                        results.Add(
                            new ManufacturingPointsCompletedResult
                            {
                                Faction = faction,
                                Points = csProgress,
                                Context = planet,
                                Tick = _game.CurrentTick,
                            }
                        );
                    }

                    if (activeItem.IsManufacturingComplete())
                    {
                        items.RemoveAt(0);
                        completed.Add(activeItem);
                    }

                    // Capital ships consume the entire queue slot per tick
                    break;
                }

                // Non-capital-ship items use linear progression with overflow
                int remainingProgress =
                    activeItem.GetConstructionCost() - activeItem.ManufacturingProgress;
                int appliedProgress = Math.Min(progressIncrement, remainingProgress);
                activeItem.IncrementManufacturingProgress(appliedProgress);
                progressIncrement -= appliedProgress;

                results.Add(
                    new ManufacturingPointsCompletedResult
                    {
                        Faction = faction,
                        Points = appliedProgress,
                        Context = planet,
                        Tick = _game.CurrentTick,
                    }
                );

                if (activeItem.IsManufacturingComplete())
                {
                    items.RemoveAt(0);
                    completed.Add(activeItem);
                }
                else
                    break;
            }

            return completed;
        }

        /// <summary>
        /// Completes manufacturing of an item. Marks it complete and hands it off to
        /// MovementSystem to route from the production planet to its destination.
        /// </summary>
        private List<GameResult> CompleteManufacturing(
            Planet productionPlanet,
            IManufacturable item
        )
        {
            item.ManufacturingStatus = ManufacturingStatus.Complete;
            ISceneNode dest = ((ISceneNode)item).GetParent();
            if (dest != null)
                _movementSystem.RequestMove((IMovable)item, dest, productionPlanet);
            ApplyCompletionSupportShift(productionPlanet, item);
            GameLogger.Log(
                $"Completed manufacturing: {item.GetDisplayName()} at {productionPlanet.GetDisplayName()}"
            );

            Faction faction = _game.GetFactionByOwnerInstanceID(
                productionPlanet.GetOwnerInstanceID()
            );

            ManufacturingType type = item.GetManufacturingType();
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                productionPlanet.GetManufacturingQueue();
            List<IManufacturable> remaining = queue.TryGetValue(
                type,
                out List<IManufacturable> list
            )
                ? list
                : new List<IManufacturable>();
            int remainingPoints = remaining.Sum(i =>
                i.GetConstructionCost() - i.ManufacturingProgress
            );

            return new List<GameResult>
            {
                new GameObjectDeployedResult
                {
                    GameObject = item as IGameEntity,
                    Tick = _game.CurrentTick,
                },
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
        /// Applies a popular support shift at the production planet when an item completes.
        /// Boosts faction support on completion.
        /// </summary>
        /// <param name="planet">The planet where manufacturing completed.</param>
        /// <param name="item">The completed manufacturable item.</param>
        private void ApplyCompletionSupportShift(Planet planet, IManufacturable item)
        {
            string ownerID = item.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerID))
                return;

            GameConfig.ProductionConfig config = _game.Config.Production;
            int shift = item switch
            {
                CapitalShip => config.CapitalShipCompletionSupportShift,
                Building => config.BuildingCompletionSupportShift,
                Regiment => config.TroopCompletionSupportShift,
                _ => 0,
            };

            if (shift <= 0)
                return;

            int current = planet.GetPopularSupport(ownerID);
            planet.SetPopularSupport(
                ownerID,
                current + shift,
                _game.Config.Planet.MaxPopularSupport
            );
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

            // Iterate all manufacturing types and clear their queues
            foreach (KeyValuePair<ManufacturingType, List<IManufacturable>> kvp in queue.ToList())
            {
                ManufacturingType type = kvp.Key;
                List<IManufacturable> items = kvp.Value;

                if (items == null || items.Count == 0)
                {
                    continue;
                }

                // Destroy all items in this queue
                foreach (IManufacturable item in items.ToList())
                {
                    // Track parent fleet before detaching — may need cleanup if last ship removed.
                    Fleet parentFleet =
                        item is CapitalShip ? ((ISceneNode)item).GetParent() as Fleet : null;

                    _game.DetachNode((ISceneNode)item);

                    // Clean up empty fleet after cancelling last capital ship.
                    if (parentFleet?.CapitalShips.Count == 0 && parentFleet.GetParent() != null)
                    {
                        _game.DetachNode(parentFleet);
                    }

                    GameLogger.Debug(
                        $"Cancelled manufacturing: {item.GetType().Name} at {planet.GetDisplayName()} due to ownership change"
                    );
                }

                // Clear the list but leave dictionary key intact
                items.Clear();
            }

            // DON'T call queue.Clear() - leave dictionary structure intact
            // Other code may expect keys to exist even with empty lists
        }

        /// <summary>
        /// Rebuilds manufacturing queues for all planets from scene graph state.
        /// Called after loading a saved game to reconstruct queue state from serialized items.
        /// </summary>
        public void RebuildQueues()
        {
            // First, clear all existing queues
            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                Dictionary<ManufacturingType, List<IManufacturable>> queue =
                    planet.GetManufacturingQueue();
                foreach (KeyValuePair<ManufacturingType, List<IManufacturable>> kvp in queue)
                {
                    kvp.Value.Clear();
                }
            }

            // Scan all scene nodes for manufacturable items in Building status
            _game
                .GetGalaxyMap()
                .Traverse(node =>
                {
                    if (node is IManufacturable manufacturable)
                    {
                        // Skip items that aren't being built
                        if (manufacturable.ManufacturingStatus != ManufacturingStatus.Building)
                        {
                            return;
                        }

                        // Skip items without a producer planet
                        if (string.IsNullOrEmpty(manufacturable.ProducerPlanetID))
                        {
                            return;
                        }

                        // Find the producer planet
                        Planet producerPlanet = _game.GetSceneNodeByInstanceID<Planet>(
                            manufacturable.ProducerPlanetID
                        );
                        if (producerPlanet == null)
                        {
                            return; // Skip invalid producer references
                        }

                        // Add to the planet's queue
                        producerPlanet.AddToManufacturingQueue(manufacturable);
                    }
                });

            // Sort each queue by manufacturing progress (lowest progress first)
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
