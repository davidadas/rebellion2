using System;
using System.Collections.Generic;
using System.Drawing;
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

        /// <summary>
        /// Creates a new ManufacturingSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public ManufacturingSystem(GameRoot game)
        {
            this.game = game;
        }

        /// <summary>
        /// Attempts to enqueue an item for production.
        /// AI factions should pass ignoreCost=true to bypass budget constraints.
        /// Player factions use default (ignoreCost=false) for real economy validation.
        /// </summary>
        /// <param name="planet">The planet where production will occur.</param>
        /// <param name="item">The item to manufacture.</param>
        /// <param name="ignoreCost">If true, bypasses cost validation (AI behavior).</param>
        /// <returns>True if enqueued successfully, false if insufficient materials or invalid state.</returns>
        public bool Enqueue(Planet planet, IManufacturable item, bool ignoreCost = false)
        {
            if (planet == null || item == null)
            {
                return false;
            }

            // Get faction that owns this planet
            Faction faction = game.GetFactionByOwnerInstanceID(planet.GetOwnerInstanceID());
            if (faction == null)
            {
                return false;
            }

            // Validate cost (unless AI is bypassing)
            if (!ignoreCost)
            {
                int cost = item.GetConstructionCost();
                int available = faction.GetTotalAvailableMaterialsRaw();

                if (available < cost)
                {
                    return false; // Cannot afford
                }
            }

            // Attach to scene graph (centralizes parent/child, registration, ownership)
            if (item is ISceneNode node)
            {
                game.AttachNode(node, planet);
            }

            item.ManufacturingStatus = ManufacturingStatus.Building;
            item.ManufacturingProgress = 0;
            item.ProducerOwnerID = planet.GetOwnerInstanceID();

            // Add to queue (cost automatically counted via faction.GetTotalUnitCost)
            planet.AddToManufacturingQueue(item);

            return true;
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
        /// Item is already attached to planet from Enqueue - just updates status.
        /// Capital ships get special handling (create fleet).
        /// </summary>
        private void CompleteManufacturing(
            Planet planet,
            IManufacturable item,
            ManufacturingType type
        )
        {
            // Update status
            item.ManufacturingStatus = ManufacturingStatus.Complete;

            // Remove from queue
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (queue.TryGetValue(type, out List<IManufacturable> items))
            {
                items.Remove(item);
            }

            // Capital ships: create fleet and attach
            if (item is CapitalShip capitalShip)
            {
                CompleteCapitalShipManufacturing(capitalShip, planet);
                return;
            }

            // All other items: already attached from Enqueue, ensure no movement state
            if (item is IMovable movable)
            {
                movable.Movement = null;
            }

            GameLogger.Debug(
                $"Completed manufacturing: {item.GetType().Name} at {planet.GetDisplayName()}"
            );
        }

        /// <summary>
        /// Completes capital ship manufacturing by creating a fleet.
        /// </summary>
        private void CompleteCapitalShipManufacturing(CapitalShip capitalShip, Planet planet)
        {
            Faction faction = game.GetFactionByOwnerInstanceID(capitalShip.GetOwnerInstanceID());
            if (faction == null)
            {
                GameLogger.Error(
                    $"Cannot create fleet for {capitalShip.GetDisplayName()}: faction not found"
                );
                game.DetachNode(capitalShip);
                return;
            }

            // Create fleet (pure creation, no side effects)
            Fleet fleet = faction.CreateFleet(game);

            // Attach fleet to planet (new node)
            game.AttachNode(fleet, planet);

            // Move capital ship from planet to fleet (existing node relocation)
            game.MoveNode(capitalShip, fleet);

            // New fleet starts at rest (no movement)
            fleet.Movement = null;

            GameLogger.Log(
                $"Created fleet {fleet.GetDisplayName()} with capital ship {capitalShip.GetDisplayName()} at {planet.GetDisplayName()}"
            );
        }

        /// <summary>
        /// Places node at planet using centralized graph operations.
        /// Uses MoveNode for existing nodes, AttachNode for new nodes.
        /// May throw SceneAccessException if ownership validation fails.
        /// </summary>
        private void PlaceAtPlanet(ISceneNode node, Planet planet)
        {
            ISceneNode currentParent = node.GetParent();

            // Already at destination - ensure no movement state
            if (currentParent == planet)
            {
                if (node is IMovable earlyMovable)
                {
                    earlyMovable.Movement = null;
                }
                return;
            }

            // Relocate existing node or attach new node
            if (currentParent != null)
            {
                // Existing node - use MoveNode (atomic with rollback)
                game.MoveNode(node, planet);
            }
            else
            {
                // New node - use AttachNode
                game.AttachNode(node, planet);
            }

            // For movable items, ensure no movement state (at rest)
            if (node is IMovable movable)
            {
                movable.Movement = null;
            }
        }

        /// <summary>
        /// Handles placement rejection with type-specific fallback logic.
        /// </summary>
        private void HandlePlacementRejection(ISceneNode item, Planet rejectionSite)
        {
            // Officers, Special Forces, Starfighters, Regiments → nearest friendly base
            if (item is Officer || item is SpecialForces || item is Starfighter || item is Regiment)
            {
                RedirectToNearestFriendlyPlanet(item);
                return;
            }

            // Capital Ships → should never reach here (blocked in CompleteManufacturing)
            if (item is CapitalShip)
            {
                GameLogger.Error(
                    $"Capital ship {item.GetDisplayName()} reached rejection handler - should not happen"
                );
                game.DetachNode(item);
                return;
            }

            // Buildings → return to origin or destroy
            if (item is Building building)
            {
                HandleBuildingRejection(building);
                return;
            }

            // Unknown type → destroy
            GameLogger.Warning($"Unknown item type {item.GetType().Name} rejected: destroying");
            game.DetachNode(item);
        }

        /// <summary>
        /// Redirects rejected unit to nearest faction-owned planet.
        /// Destroys if no friendly planet exists.
        /// </summary>
        private void RedirectToNearestFriendlyPlanet(ISceneNode item)
        {
            string ownerID = item.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerID))
            {
                game.DetachNode(item);
                return;
            }

            // TODO: Cache faction-owned planets for performance and deterministic ordering
            Planet nearest = FindNearestFactionPlanet(ownerID, item);

            if (nearest != null)
            {
                try
                {
                    PlaceAtPlanet(item, nearest);
                    GameLogger.Log(
                        $"Redirected {item.GetDisplayName()} to {nearest.GetDisplayName()}"
                    );
                }
                catch (SceneAccessException)
                {
                    // Fallback also failed - destroy
                    GameLogger.Warning($"Redirect failed for {item.GetDisplayName()}: destroying");
                    game.DetachNode(item);
                }
            }
            else
            {
                // No friendly planet exists - destroy
                GameLogger.Warning($"No friendly planet for {item.GetDisplayName()}: destroying");
                game.DetachNode(item);
            }
        }

        /// <summary>
        /// Finds nearest planet owned by specified faction.
        /// Uses current item position (from parent or last known location).
        /// </summary>
        private Planet FindNearestFactionPlanet(string factionOwnerID, ISceneNode item)
        {
            // Get item's current position
            Point fromPosition;
            if (item is IMovable movable)
            {
                fromPosition = movable.GetPosition();
            }
            else
            {
                ISceneNode parent = item.GetParent();
                fromPosition = parent is Planet p ? p.GetPosition() : new Point(0, 0);
            }

            List<Planet> candidates = game.GetSceneNodesByType<Planet>()
                .Where(p => p.GetOwnerInstanceID() == factionOwnerID)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            // Find nearest by Euclidean distance
            Planet nearest = null;
            double minDistance = double.MaxValue;

            foreach (Planet planet in candidates)
            {
                Point pos = planet.GetPosition();
                double dx = pos.X - fromPosition.X;
                double dy = pos.Y - fromPosition.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = planet;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Handles rejected building: return to origin or destroy if no capacity.
        /// Origin is determined by GetLastParent() (where it was manufactured).
        /// </summary>
        private void HandleBuildingRejection(Building building)
        {
            // Origin = where it was manufactured (GetLastParent tracks previous parent)
            Planet origin = building.GetLastParent() as Planet;

            if (origin == null)
            {
                GameLogger.Warning(
                    $"Building {building.GetDisplayName()} has no origin planet: destroying"
                );
                game.DetachNode(building);
                return;
            }

            // Check capacity using GetBuildingSlotCapacity
            BuildingSlot slot = building.GetBuildingSlot();
            int availableCapacity = origin.GetBuildingSlotCapacity(slot);

            if (availableCapacity > 0)
            {
                try
                {
                    PlaceAtPlanet(building, origin);
                    GameLogger.Log(
                        $"Returned building {building.GetDisplayName()} to origin {origin.GetDisplayName()}"
                    );
                }
                catch (SceneAccessException)
                {
                    // Can't even return to origin - destroy
                    GameLogger.Warning(
                        $"Cannot return {building.GetDisplayName()} to origin: destroying"
                    );
                    game.DetachNode(building);
                }
            }
            else
            {
                GameLogger.Warning(
                    $"Origin planet {origin.GetDisplayName()} has no capacity: destroying {building.GetDisplayName()}"
                );
                game.DetachNode(building);
            }
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
                    // Remove from scene graph using centralized global state management
                    if (item is ISceneNode node)
                    {
                        game.DetachNode(node);
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
                var queue = planet.GetManufacturingQueue();
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
                var queue = planet.GetManufacturingQueue();
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
