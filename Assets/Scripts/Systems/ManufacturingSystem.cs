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
            string ownerInstanceId = planet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerInstanceId))
                return false;

            Faction faction = game.GetFactionByOwnerInstanceID(ownerInstanceId);
            if (faction == null)
            {
                return false;
            }

            // Validate cost (unless caller is bypassing)
            if (!ignoreCost)
            {
                int cost = item.GetConstructionCost();
                int available = game.GetRefinedMaterials(faction);

                if (available < cost)
                {
                    return false; // Cannot afford
                }
            }

            // Attach to scene graph. Capital ships go into a fleet at the
            // production planet; everything else attaches to the planet directly.
            if (item is ISceneNode node)
            {
                if (item is CapitalShip capitalShip)
                {
                    Fleet fleet = FindOrCreateFleet(planet, faction);
                    game.AttachNode(capitalShip, fleet);
                }
                else
                {
                    game.AttachNode(node, planet);
                }
            }

            item.ManufacturingStatus = ManufacturingStatus.Building;
            item.ManufacturingProgress = 0;
            item.ProducerOwnerID = planet.GetOwnerInstanceID();
            item.ProducerPlanetID = planet.GetInstanceID();

            // Add to queue (cost automatically counted via faction.GetTotalUnitCost)
            planet.AddToManufacturingQueue(item);

            GameLogger.Log(
                $"Enqueued {item.GetDisplayName()} for production at {planet.GetDisplayName()} (cost: {item.GetConstructionCost()})"
            );

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

            // Ship to destination if one was specified
            if (item is IMovable movable && !string.IsNullOrEmpty(item.ManufacturingDestinationID))
            {
                ISceneNode destination = game.GetSceneNodeByInstanceID<ISceneNode>(
                    item.ManufacturingDestinationID
                );

                if (destination != null)
                {
                    // For capital ships, ship the fleet they're in
                    IMovable unitToShip = item is CapitalShip cs
                        ? (IMovable)(cs.GetParent() as Fleet ?? (ISceneNode)cs)
                        : movable;

                    ShipToDestination(unitToShip, planet, destination);
                    return;
                }
            }

            // No valid destination — default to production planet, scrap if rejected
            if (item is ISceneNode orphanNode)
            {
                try
                {
                    if (orphanNode.GetParent() != planet)
                    {
                        if (orphanNode.GetParent() != null)
                            game.MoveNode(orphanNode, planet);
                        else
                            game.AttachNode(orphanNode, planet);
                    }

                    if (item is IMovable idleMovable)
                        idleMovable.Movement = null;

                    GameLogger.Log(
                        $"Completed manufacturing: {item.GetDisplayName()} at {planet.GetDisplayName()}"
                    );
                }
                catch
                {
                    GameLogger.Warning(
                        $"Scrapped {item.GetDisplayName()}: planet {planet.GetDisplayName()} cannot accept it"
                    );
                    if (orphanNode.GetParent() != null)
                        game.DetachNode(orphanNode);
                }
                return;
            }

            GameLogger.Log(
                $"Completed manufacturing: {item.GetDisplayName()} at {planet.GetDisplayName()}"
            );
        }

        /// <summary>
        /// Finds an existing idle friendly fleet at the planet, or creates a new one.
        /// </summary>
        private Fleet FindOrCreateFleet(Planet planet, Faction faction)
        {
            string ownerId = faction.GetInstanceID();

            Fleet existingFleet = planet
                .GetFleets()
                .FirstOrDefault(f => f.GetOwnerInstanceID() == ownerId && f.IsMovable());

            if (existingFleet != null)
                return existingFleet;

            Fleet fleet = faction.CreateFleet(game);
            game.AttachNode(fleet, planet);
            fleet.Movement = null;
            return fleet;
        }

        /// <summary>
        /// Ships a completed unit to its destination by reparenting it and setting up
        /// a MovementState for travel. If the destination is the same planet, no movement.
        /// </summary>
        private void ShipToDestination(IMovable unit, Planet originPlanet, ISceneNode destination)
        {
            Planet destinationPlanet = destination is Planet dp
                ? dp
                : destination.GetParentOfType<Planet>();

            if (destinationPlanet == null || destinationPlanet == originPlanet)
            {
                unit.Movement = null;
                return;
            }

            // Reparent to destination in scene graph
            if (unit is ISceneNode node)
            {
                if (node.GetParent() != null)
                    game.MoveNode(node, destination);
                else
                    game.AttachNode(node, destination);
            }

            // Calculate transit using same formula as MovementSystem
            Point originPos = originPlanet.GetPosition();
            Point destPos = destinationPlanet.GetPosition();
            double dx = destPos.X - originPos.X;
            double dy = destPos.Y - originPos.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            int hyperdrive = game.GetConfig().Movement.DefaultFighterHyperdrive;
            if (unit is Fleet fleet && fleet.CapitalShips.Count > 0)
            {
                hyperdrive = fleet
                    .CapitalShips.Select(s => s.Hyperdrive)
                    .Where(h => h > 0)
                    .DefaultIfEmpty(1)
                    .Min();
            }
            else if (unit is CapitalShip capitalShip)
            {
                hyperdrive = Math.Max(capitalShip.Hyperdrive, 1);
            }

            int baseTicks = (int)
                Math.Ceiling((distance * game.GetConfig().Movement.DistanceScale) / hyperdrive);
            int transitTicks = Math.Max(baseTicks, game.GetConfig().Movement.MinTransitTicks);

            unit.Movement = new MovementState
            {
                DestinationInstanceID = destination.GetInstanceID(),
                TransitTicks = transitTicks,
                TicksElapsed = 0,
                OriginPosition = originPos,
                CurrentPosition = originPos,
            };

            GameLogger.Log(
                $"{((ISceneNode)unit).GetDisplayName()} shipping to {destination.GetDisplayName()} (ETA: {transitTicks} ticks)"
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

            // Capital Ships → should never reach here (attached to fleet at Enqueue time)
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
