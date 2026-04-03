using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly MovementSystem movementSystem;

        /// <summary>
        /// Creates a new ManufacturingSystem.
        /// </summary>
        public ManufacturingSystem(GameRoot game, MovementSystem movementSystem)
        {
            this.game = game;
            this.movementSystem = movementSystem;
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

            if (item is CapitalShip capitalShip)
            {
                Fleet newFleet = faction.CreateFleet(game);
                game.AttachNode(newFleet, destination);
                newFleet.Movement = null;
                game.AttachNode(capitalShip, newFleet);
            }
            else
            {
                game.AttachNode((ISceneNode)item, destination);
            }

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

            try
            {
                game.AttachNode((ISceneNode)item, destination);
            }
            catch (SceneAccessException)
            {
                return false;
            }

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
        /// Advances manufacturing progress on all planets.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public void ProcessTick(GameRoot game)
        {
            // Iterate all planets
            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
            {
                ProcessPlanetManufacturing(planet);
            }
        }

        /// <summary>
        /// Processes manufacturing for a single planet.
        /// </summary>
        private void ProcessPlanetManufacturing(Planet planet)
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
                    int hostileCapitalShips = planet
                        .GetFleets()
                        .Where(f =>
                            f.GetOwnerInstanceID() != null
                            && f.GetOwnerInstanceID() != planet.GetOwnerInstanceID()
                        )
                        .Sum(f => f.CapitalShips.Count);
                    int hostileFighters = planet
                        .GetAllStarfighters()
                        .Count(s =>
                            s.GetOwnerInstanceID() != null
                            && s.GetOwnerInstanceID() != planet.GetOwnerInstanceID()
                        );

                    int modifier =
                        100
                        - hostileCapitalShips * game.Config.Production.BlockadeCapitalShipPenalty
                        - hostileFighters * game.Config.Production.BlockadeFighterPenalty;
                    modifier = Math.Max(0, modifier);

                    progressIncrement = (progressIncrement * modifier) / 100;
                }

                // Process with overflow handling
                while (progressIncrement > 0 && items.Count > 0)
                {
                    // Get the first item in queue (active item)
                    IManufacturable activeItem = items[0];

                    // Calculate how much progress this item needs
                    int requiredProgress = activeItem.GetConstructionCost();
                    int currentProgress = activeItem.ManufacturingProgress;
                    int remainingProgress = requiredProgress - currentProgress;

                    // Apply progress
                    int appliedProgress = Math.Min(progressIncrement, remainingProgress);
                    activeItem.IncrementManufacturingProgress(appliedProgress);
                    progressIncrement -= appliedProgress;

                    // Check if complete
                    if (activeItem.IsManufacturingComplete())
                    {
                        CompleteManufacturing(planet, activeItem, type);
                        // Continue with overflow progress to next item
                    }
                    else
                    {
                        // Item not complete, stop processing this queue
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Completes manufacturing of an item.
        /// Handles destination recovery when the destination was destroyed or changed sides.
        /// </summary>
        private void CompleteManufacturing(
            Planet planet,
            IManufacturable item,
            ManufacturingType type
        )
        {
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            queue.TryGetValue(type, out List<IManufacturable> items);

            if (item is Building building)
            {
                CompleteBuildingWithRedirect(planet, building, items);
                return;
            }

            item.ManufacturingStatus = ManufacturingStatus.Complete;
            items?.Remove(item);

            if (item is CapitalShip cs)
                CompleteCapitalShip(planet, cs);
            else if (item is IMovable movable)
                CompleteMovableWithRedirect(planet, movable);

            GameLogger.Log(
                $"Completed manufacturing: {item.GetDisplayName()} at {planet.GetDisplayName()}"
            );
        }

        /// <summary>
        /// Completes a capital ship. If its destination fleet was destroyed, creates a rescue fleet
        /// at the production planet and re-registers the capital ship and all its cargo.
        /// </summary>
        private void CompleteCapitalShip(Planet productionPlanet, CapitalShip cs)
        {
            Fleet currentFleet = cs.GetParent() as Fleet;
            bool fleetAlive =
                currentFleet != null
                && game.GetSceneNodeByInstanceID<Fleet>(currentFleet.InstanceID) != null;

            if (!fleetAlive)
            {
                // Destination fleet was destroyed — rescue the capital ship at the production planet.
                // Children's parent pointers are not cleared by DetachNode, so we must clean up manually
                // before re-attaching to avoid the AttachNode "already has a parent" guard.
                ISceneNode staleParent = cs.GetParent();
                if (staleParent != null)
                {
                    staleParent.RemoveChild(cs);
                    cs.SetParent(null);
                }

                Faction faction = game.GetFactionByOwnerInstanceID(cs.GetOwnerInstanceID());
                if (faction == null)
                    return;

                Fleet rescueFleet = faction.CreateFleet(game);
                game.AttachNode(rescueFleet, productionPlanet);

                // Bypass game.AttachNode to avoid duplicating faction owned-units registration
                // (cs and its children were never removed from faction owned-units when the fleet died).
                rescueFleet.AddChild(cs);
                cs.SetParent(rescueFleet);
                cs.Traverse(game.AddSceneNodeByInstanceID);

                GameLogger.Log(
                    $"{cs.GetDisplayName()} rescued to new fleet at {productionPlanet.GetDisplayName()}"
                );
            }
            else
            {
                movementSystem.RequestMove(
                    currentFleet,
                    (ISceneNode)currentFleet.GetParent(),
                    productionPlanet
                );
            }
        }

        /// <summary>
        /// Completes a non-capital-ship movable (starfighter, regiment, special forces).
        /// If the destination's planet changed sides, redirects the unit to the production planet.
        /// If the production planet cannot accept the unit, the unit is cancelled.
        /// </summary>
        private void CompleteMovableWithRedirect(Planet productionPlanet, IMovable movable)
        {
            Planet currentPlanet = ((ISceneNode)movable).GetParentOfType<Planet>();
            bool destFriendly =
                currentPlanet != null
                && currentPlanet.GetOwnerInstanceID() == movable.GetOwnerInstanceID();

            if (!destFriendly)
            {
                try
                {
                    game.MoveNode((ISceneNode)movable, productionPlanet);
                    movable.Movement = null;
                    GameLogger.Log(
                        $"{((ISceneNode)movable).GetDisplayName()} redirected to {productionPlanet.GetDisplayName()} — destination changed sides."
                    );
                }
                catch (Exception)
                {
                    game.DetachNode((ISceneNode)movable);
                    GameLogger.Log(
                        $"{((ISceneNode)movable).GetDisplayName()} cancelled — destination changed sides and production planet cannot accept it."
                    );
                }
            }
            else
            {
                movementSystem.RequestMove(
                    movable,
                    ((ISceneNode)movable).GetParent(),
                    productionPlanet
                );
            }
        }

        /// <summary>
        /// Completes a building. If its destination planet changed sides, performs a batch check:
        /// all buildings of the same slot type in the queue are redirected to the production planet
        /// if it has sufficient capacity, or all are cancelled if it does not.
        /// </summary>
        private void CompleteBuildingWithRedirect(
            Planet productionPlanet,
            Building building,
            List<IManufacturable> items
        )
        {
            Planet destPlanet = building.GetParentOfType<Planet>();
            bool destFriendly =
                destPlanet != null
                && destPlanet.GetOwnerInstanceID() == building.GetOwnerInstanceID();

            if (!destFriendly)
            {
                BuildingSlot slot = building.GetBuildingSlot();

                // Collect the entire batch (this building + same-slot siblings still in queue).
                List<Building> batch = new List<Building> { building };
                if (items != null)
                    batch.AddRange(
                        items.OfType<Building>().Where(b => b.GetBuildingSlot() == slot)
                    );

                // Remove all from queue before acting.
                foreach (Building b in batch)
                    items?.Remove(b);

                bool hasCapacity = productionPlanet.GetBuildingSlotCapacity(slot) >= batch.Count;

                foreach (Building b in batch)
                {
                    if (hasCapacity)
                    {
                        b.ManufacturingStatus = ManufacturingStatus.Complete;
                        try
                        {
                            game.MoveNode(b, productionPlanet);
                        }
                        catch (Exception)
                        {
                            // MoveNode may have left b detached if its rollback also failed.
                            if (((ISceneNode)b).GetParent() != null)
                                game.DetachNode(b);
                        }
                    }
                    else
                    {
                        game.DetachNode(b);
                    }
                }

                GameLogger.Log(
                    hasCapacity
                        ? $"Redirected {batch.Count} building(s) to {productionPlanet.GetDisplayName()} — destination changed sides."
                        : $"Cancelled {batch.Count} building(s) — destination changed sides and production planet is at capacity."
                );
                return;
            }

            // Normal completion: destination is still friendly.
            building.ManufacturingStatus = ManufacturingStatus.Complete;
            items?.Remove(building);
            movementSystem.RequestMove(
                (IMovable)building,
                (ISceneNode)building.GetParent(),
                productionPlanet
            );

            GameLogger.Log(
                $"Completed manufacturing: {building.GetDisplayName()} at {productionPlanet.GetDisplayName()}"
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
                    game.DetachNode((ISceneNode)item);

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
