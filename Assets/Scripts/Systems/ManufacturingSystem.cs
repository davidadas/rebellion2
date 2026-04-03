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

            if (item is CapitalShip capitalShipFleet)
            {
                game.AttachNode(capitalShipFleet, destination);
            }
            else
            {
                game.AttachNode((ISceneNode)item, planet);
                item.DestinationInstanceID = destination.GetInstanceID();
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
        /// Item is already attached to planet from Enqueue - just updates status.
        /// Capital ships get special handling (create fleet).
        /// </summary>
        private void CompleteManufacturing(
            Planet planet,
            IManufacturable item,
            ManufacturingType type
        )
        {
            item.ManufacturingStatus = ManufacturingStatus.Complete;

            // Remove from queue
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            if (queue.TryGetValue(type, out List<IManufacturable> items))
            {
                items.Remove(item);
            }

            if (item is IMovable movable)
            {
                // For capital ships, the movable unit is the fleet they're in
                IMovable unitToShip = item is CapitalShip cs
                    ? (IMovable)(cs.GetParent() as Fleet)
                    : movable;

                if (unitToShip != null)
                {
                    ISceneNode destinationNode = ResolveDestination(item, unitToShip);

                    Planet destinationPlanet =
                        destinationNode as Planet
                        ?? destinationNode?.GetParentOfType<Planet>();

                    if (destinationPlanet != null && destinationPlanet != planet)
                    {
                        try
                        {
                            ShipToDestination(unitToShip, planet, destinationNode);
                        }
                        catch (SceneAccessException)
                        {
                            if (
                                planet.GetOwnerInstanceID()
                                != ((ISceneNode)unitToShip).GetOwnerInstanceID()
                            )
                            {
                                game.DetachNode((ISceneNode)unitToShip);
                                return;
                            }
                            try
                            {
                                PlaceAtPlanet((ISceneNode)unitToShip, planet);
                            }
                            catch (SceneAccessException)
                            {
                                game.DetachNode((ISceneNode)unitToShip);
                                return;
                            }
                        }
                    }
                    else
                    {
                        unitToShip.Movement = null;
                    }
                }
            }

            GameLogger.Log(
                $"Completed manufacturing: {item.GetDisplayName()} at {planet.GetDisplayName()}"
            );
        }

        /// <summary>
        /// Resolves the destination scene node for a completed item.
        /// For items with an explicit DestinationInstanceID (fleet-targeted units),
        /// finds the appropriate capital ship within that fleet.
        /// Falls back to the unit's current parent otherwise.
        /// </summary>
        private ISceneNode ResolveDestination(IManufacturable item, IMovable unitToShip)
        {
            if (!string.IsNullOrEmpty(item.DestinationInstanceID))
            {
                Fleet fleet = game.GetSceneNodeByInstanceID<Fleet>(item.DestinationInstanceID);
                if (fleet != null)
                    return fleet;
            }
            return ((ISceneNode)unitToShip).GetParent();
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

            // Refuse to ship to enemy-controlled territory
            string unitOwner = ((ISceneNode)unit).GetOwnerInstanceID();
            if (destinationPlanet.GetOwnerInstanceID() != unitOwner)
            {
                throw new SceneAccessException((ISceneNode)unit, destinationPlanet);
            }

            // Reparent to destination in scene graph
            ISceneNode unitNode = (ISceneNode)unit;
            if (unitNode.GetParent() != null)
                game.MoveNode(unitNode, destination);
            else
                game.AttachNode(unitNode, destination);

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
