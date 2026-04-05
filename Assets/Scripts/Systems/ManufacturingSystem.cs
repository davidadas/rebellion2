using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

/// <summary>
/// Manages unit and facility production during each game tick.
/// </summary>
namespace Rebellion.Systems
{
    public class ManufacturingSystem
    {
        private readonly GameRoot game;

        /// <summary>
        /// Creates a new ManufacturingSystem.
        /// </summary>
        public ManufacturingSystem(GameRoot game)
        {
            this.game = game;
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

            game.AttachNode((ISceneNode)item, destination);

            CommitToQueue(planet, item);
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

            game.AttachNode((ISceneNode)item, parent);

            CommitToQueue(planet, item);
            return true;
        }

        private Faction GetValidatedFaction(Planet planet, IManufacturable item, bool ignoreCost)
        {
            if (planet == null || item == null)
                return null;

            string ownerInstanceId = planet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerInstanceId))
                return null;

            Faction faction = game.GetFactionByOwnerInstanceID(ownerInstanceId);
            if (faction == null)
                return null;

            if (!ignoreCost)
            {
                if (game.GetRefinedMaterials(faction) < item.GetConstructionCost())
                    return null;
            }

            return faction;
        }

        private void CommitToQueue(Planet planet, IManufacturable item)
        {
            item.ManufacturingStatus = ManufacturingStatus.Building;
            item.ManufacturingProgress = 0;
            item.ProducerOwnerID = planet.GetOwnerInstanceID();
            item.ProducerPlanetID = planet.GetInstanceID();

            planet.AddToManufacturingQueue(item);

            GameLogger.Log(
                $"Enqueued {item.GetDisplayName()} for production at {planet.GetDisplayName()} (cost: {item.GetConstructionCost()})"
            );
        }

        /// <summary>
        /// Processes manufacturing for the current tick.
        /// </summary>
        /// <param name="movementSystem">Used to dispatch completed units to their destinations.</param>
        /// <param name="provider">Random number provider for capital ship progress rolls.</param>
        public void ProcessTick(MovementSystem movementSystem, IRandomNumberProvider provider)
        {
            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
            {
                ProcessPlanetManufacturing(planet, movementSystem, provider);
            }
        }

        /// <summary>
        /// Determines how much manufacturing progress a capital ship makes this tick using the
        /// CSCRHT table. Performs a success roll first; returns 0 if it fails. On success,
        /// returns the progress amount from the table.
        /// </summary>
        /// <param name="provider">Random number provider for the rolls.</param>
        /// <returns>Progress to apply this tick, or 0 if the success check fails.</returns>
        private int RollCapitalShipProgress(IRandomNumberProvider provider)
        {
            GameConfig.ProductionConfig config = game.Config.Production;

            // Success check: roll must be below threshold
            int successRoll = provider.NextInt(0, config.CapitalShipSuccessRollRange);
            if (successRoll >= config.CapitalShipSuccessThreshold)
                return 0;

            // Progress roll: look up in CSCRHT table
            int progressRoll = provider.NextInt(0, config.CapitalShipProgressRollRange);
            ProbabilityTable cscrht = new ProbabilityTable(config.CapitalShipProgressTable);
            return cscrht.Lookup(progressRoll);
        }

        private void ProcessPlanetManufacturing(
            Planet planet,
            MovementSystem movementSystem,
            IRandomNumberProvider provider
        )
        {
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (queue == null || queue.Count == 0)
            {
                return;
            }

            // Process each manufacturing type queue
            foreach (KeyValuePair<ManufacturingType, List<IManufacturable>> kvp in queue.ToList()) // ToList to avoid modification during iteration
            {
                ManufacturingType type = kvp.Key;
                List<IManufacturable> items = kvp.Value;

                if (items == null || items.Count == 0)
                {
                    continue;
                }

                // Calculate progress increment based on planet's production rate
                int progressIncrement = planet.GetProductionRate(type);

                // Apply blockade production penalty
                if (planet.IsBlockaded())
                {
                    int modifier = planet.GetBlockadeModifier(
                        game.Config.Production.BlockadeCapitalShipPenalty,
                        game.Config.Production.BlockadeFighterPenalty
                    );
                    // modifier is a percentage (0–100); divide by 100 to scale progressIncrement
                    progressIncrement = (progressIncrement * modifier) / 100;
                }

                DistributeProgress(planet, items, type, progressIncrement, movementSystem, provider);
            }
        }

        /// <summary>
        /// Distributes a production progress increment across the queue for one manufacturing type.
        /// Surplus progress from a completed item carries over to the next item in the queue.
        /// Capital ships consume the entire tick slot and do not overflow.
        /// </summary>
        /// <param name="planet">The planet whose queue is being processed.</param>
        /// <param name="items">The ordered list of items in this type's queue.</param>
        /// <param name="type">The manufacturing type being processed.</param>
        /// <param name="progressIncrement">Total progress available this tick.</param>
        /// <param name="movementSystem">Used to dispatch completed items.</param>
        /// <param name="provider">Random number provider for capital ship rolls.</param>
        private void DistributeProgress(
            Planet planet,
            List<IManufacturable> items,
            ManufacturingType type,
            int progressIncrement,
            MovementSystem movementSystem,
            IRandomNumberProvider provider
        )
        {
            while (progressIncrement > 0 && items.Count > 0)
            {
                IManufacturable activeItem = items[0];

                // Capital ships use probabilistic CSCRHT progression
                if (activeItem is CapitalShip)
                {
                    int csProgress = RollCapitalShipProgress(provider);
                    if (csProgress > 0)
                        activeItem.IncrementManufacturingProgress(csProgress);

                    if (activeItem.IsManufacturingComplete())
                        CompleteManufacturing(planet, activeItem, type, movementSystem);

                    // Capital ships consume the entire queue slot per tick
                    break;
                }

                // Non-capital-ship items use linear progression with overflow
                int remainingProgress =
                    activeItem.GetConstructionCost() - activeItem.ManufacturingProgress;
                int appliedProgress = Math.Min(progressIncrement, remainingProgress);
                activeItem.IncrementManufacturingProgress(appliedProgress);
                progressIncrement -= appliedProgress;

                if (activeItem.IsManufacturingComplete())
                    CompleteManufacturing(planet, activeItem, type, movementSystem);
                else
                    break;
            }
        }

        /// <summary>
        /// Completes manufacturing of an item. Marks it complete, removes from queue,
        /// and delivers to the intended destination via MovementSystem.
        /// If the destination no longer exists or changed sides, falls back gracefully.
        /// </summary>
        private void CompleteManufacturing(
            Planet productionPlanet,
            IManufacturable item,
            ManufacturingType type,
            MovementSystem movementSystem
        )
        {
            item.ManufacturingStatus = ManufacturingStatus.Complete;

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                productionPlanet.GetManufacturingQueue();
            if (queue.TryGetValue(type, out List<IManufacturable> items))
                items.Remove(item);

            if (item is CapitalShip cs)
                CompleteCapitalShip(productionPlanet, cs, movementSystem);
            else if (item is Building building)
                CompleteBuilding(productionPlanet, building, movementSystem);
            else if (item is IMovable movable)
                CompleteMovable(productionPlanet, movable, movementSystem);

            // Apply popular support shift at the production planet
            ApplyCompletionSupportShift(productionPlanet, item);

            GameLogger.Log(
                $"Completed manufacturing: {item.GetDisplayName()} at {productionPlanet.GetDisplayName()}"
            );
        }

        /// <summary>
        /// Completes a capital ship. The ship stays in its destination fleet.
        /// If the fleet is at the production planet, nothing else happens.
        /// If the fleet is elsewhere, a shipping movement is set up so the fleet
        /// visually travels from the production planet to its current location.
        /// If the fleet no longer exists, the ship is placed in a local fleet.
        /// </summary>
        private void CompleteCapitalShip(
            Planet productionPlanet,
            CapitalShip cs,
            MovementSystem movementSystem
        )
        {
            Fleet currentFleet = cs.GetParent() as Fleet;
            bool fleetExists =
                currentFleet != null
                && game.GetSceneNodeByInstanceID<Fleet>(currentFleet.InstanceID) != null
                && currentFleet.GetOwnerInstanceID() == cs.GetOwnerInstanceID();

            if (fleetExists)
            {
                Planet fleetPlanet = currentFleet.GetParentOfType<Planet>();
                if (fleetPlanet == productionPlanet)
                {
                    // Fleet is at production planet — ship is already in it, nothing to do.
                    return;
                }

                // Fleet is elsewhere — ship stays in fleet, set up shipping movement.
                if (currentFleet.IsMovable())
                {
                    movementSystem.RequestMove(
                        currentFleet,
                        currentFleet.GetParent(),
                        productionPlanet
                    );
                }
                return;
            }

            // Fleet no longer exists or changed ownership — place in a local fleet.
            if (cs.GetParent() != null)
            {
                game.DetachNode(cs);
            }

            Faction faction = game.GetFactionByOwnerInstanceID(cs.GetOwnerInstanceID());
            if (faction == null)
                return;

            Fleet localFleet = productionPlanet
                .GetFleets()
                .FirstOrDefault(f =>
                    f.GetOwnerInstanceID() == cs.GetOwnerInstanceID() && f.IsMovable()
                );

            if (localFleet != null)
            {
                game.AttachNode(cs, localFleet);
            }
            else
            {
                Fleet newFleet = faction.CreateFleet(game, new[] { cs }, FleetRoleType.Battle);
                game.AttachNode(newFleet, productionPlanet);
            }
        }

        /// <summary>
        /// Completes a movable item (starfighter, regiment). If the destination planet is still
        /// friendly, sends via MovementSystem. Otherwise redirects to production planet.
        /// </summary>
        private void CompleteMovable(
            Planet productionPlanet,
            IMovable movable,
            MovementSystem movementSystem
        )
        {
            Planet currentPlanet = ((ISceneNode)movable).GetParentOfType<Planet>();
            bool destFriendly =
                currentPlanet != null
                && currentPlanet.GetOwnerInstanceID() == movable.GetOwnerInstanceID();

            if (destFriendly)
            {
                movementSystem.RequestMove(
                    movable,
                    ((ISceneNode)movable).GetParent(),
                    productionPlanet
                );
            }
            else
            {
                try
                {
                    game.MoveNode((ISceneNode)movable, productionPlanet);
                    movable.Movement = null;
                }
                catch (SceneAccessException)
                {
                    game.DetachNode((ISceneNode)movable);
                }
            }
        }

        /// <summary>
        /// Completes a building. If the destination planet is still friendly, sends via
        /// MovementSystem. Otherwise redirects to production planet if capacity allows.
        /// </summary>
        private void CompleteBuilding(
            Planet productionPlanet,
            Building building,
            MovementSystem movementSystem
        )
        {
            Planet destPlanet = building.GetParentOfType<Planet>();
            bool destFriendly =
                destPlanet != null
                && destPlanet.GetOwnerInstanceID() == building.GetOwnerInstanceID();

            if (destFriendly)
            {
                movementSystem.RequestMove(
                    (IMovable)building,
                    (ISceneNode)building.GetParent(),
                    productionPlanet
                );
                return;
            }

            // Destination changed sides — try to redirect to production planet.
            if (productionPlanet.GetAvailableEnergy() > 0)
            {
                try
                {
                    game.MoveNode(building, productionPlanet);
                }
                catch (SceneAccessException)
                {
                    if (((ISceneNode)building).GetParent() != null)
                        game.DetachNode(building);
                }
            }
            else
            {
                game.DetachNode(building);
            }
        }

        /// <summary>
        /// Applies a popular support shift at the production planet when an item completes.
        /// Matches the original game's behavior of boosting faction support on completion.
        /// </summary>
        private void ApplyCompletionSupportShift(Planet planet, IManufacturable item)
        {
            string ownerID = item.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerID))
                return;

            GameConfig.ProductionConfig config = game.Config.Production;
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
                game.Config.Planet.MaxPopularSupport
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

                    game.DetachNode((ISceneNode)item);

                    // Clean up empty fleet after cancelling last capital ship.
                    if (
                        parentFleet != null
                        && parentFleet.CapitalShips.Count == 0
                        && parentFleet.GetParent() != null
                    )
                    {
                        game.DetachNode(parentFleet);
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
            foreach (var planet in game.GetSceneNodesByType<Planet>())
            {
                Dictionary<ManufacturingType, List<IManufacturable>> queue =
                    planet.GetManufacturingQueue();
                foreach (var kvp in queue)
                {
                    kvp.Value.Clear();
                }
            }

            // Scan all scene nodes for manufacturable items in Building status
            game.GetGalaxyMap()
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
                        Planet producerPlanet = game.GetSceneNodeByInstanceID<Planet>(
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
            foreach (var planet in game.GetSceneNodesByType<Planet>())
            {
                Dictionary<ManufacturingType, List<IManufacturable>> queue =
                    planet.GetManufacturingQueue();
                foreach (var kvp in queue)
                {
                    kvp.Value.Sort(
                        (a, b) => a.ManufacturingProgress.CompareTo(b.ManufacturingProgress)
                    );
                }
            }
        }
    }
}
