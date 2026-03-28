using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

/// <summary>
/// Manages unit movement during each game tick.
///
/// <remarks>
/// MovementSystem is the ONLY system that calls game.MoveNode() for movement purposes.
/// Other systems request movement via RequestMove() - they never call MoveNode() directly.
/// (Manufacturing/spawning is exempt - they use MoveNode() for initial placement, not movement).
/// </remarks>
/// </summary>
namespace Rebellion.Systems
{
    public class MovementSystem
    {
        private readonly GameRoot game;
        private readonly FogOfWarSystem fogOfWar;

/// <summary>
        /// Initializes a new instance of the MovementSystem class.
        /// </summary>
        /// <param name="game">The game instance this manager is associated with.</param>
        /// <param name="fogOfWar">The fog of war system for capturing snapshots on arrival.</param>
        public MovementSystem(GameRoot game, FogOfWarSystem fogOfWar)
        {
            this.game = game;
            this.fogOfWar = fogOfWar;
        }

        /// <summary>
        /// Requests a unit to move to a destination.
        /// Sets the destination and movement status without relocating the node.
        /// The unit will travel toward the destination over multiple ticks.
        ///
        /// IMPORTANT: This is the ONLY valid way to initiate movement.
        /// Do not call game.MoveNode() directly for movement.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="destination">The target destination (Planet or other container).</param>
        public void RequestMove(IMovable unit, ISceneNode destination)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (!unit.IsMovable())
            {
                throw new InvalidOperationException(
                    $"Unit {unit.GetDisplayName()} cannot be moved (IsMovable() returned false)."
                );
            }

            // Get destination planet (all movement is planet-based)
            Planet destinationPlanet = destination is Planet planet
                ? planet
                : destination.GetParentOfType<Planet>();
            if (destinationPlanet == null)
            {
                throw new InvalidOperationException(
                    $"Destination {destination.GetDisplayName()} is not at a planet location. "
                        + "All movement must resolve to a planet."
                );
            }

            // Get origin planet for distance calculation
            Planet originPlanet = unit.GetParentOfType<Planet>();
            if (originPlanet == null)
            {
                throw new InvalidOperationException(
                    $"Unit {unit.GetDisplayName()} is not at a planet location."
                );
            }

            // Calculate transit time based on distance and hyperdrive
            int transitTicks = CalculateTransitTicks(unit, originPlanet, destinationPlanet);

            // Create movement state (replaces null/Idle with active transit)
            unit.Movement = new MovementState
            {
                DestinationInstanceID = destination.GetInstanceID(),
                TransitTicks = transitTicks,
                TicksElapsed = 0,
                OriginPosition = originPlanet.GetPosition(),
                CurrentPosition = originPlanet.GetPosition(), // Start at origin coordinates
            };

            GameLogger.Log(
                $"{unit.GetDisplayName()} ordered to move to {destination.GetDisplayName()} (ETA: {transitTicks} ticks)"
            );
        }

        /// <summary>
        /// Calculates transit time in ticks based on distance and hyperdrive rating.
        /// Uses Euclidean distance between systems: transit_ticks = ceil((distance * DistanceScale) / slowest_hyperdrive)
        /// Han Solo's hyperdrive_modifier subtracts from the total.
        /// Result is clamped to MinTransitTicks.
        /// </summary>
        private int CalculateTransitTicks(IMovable unit, Planet origin, Planet destination)
        {
            // Calculate Euclidean distance between origin and destination
            Point originPos = origin.GetPosition();
            Point destPos = destination.GetPosition();
            double dx = destPos.X - originPos.X;
            double dy = destPos.Y - originPos.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // Determine slowest hyperdrive rating
            int slowestHyperdrive = game.GetConfig().Movement.DefaultFighterHyperdrive;

            if (unit is Fleet fleet)
            {
                // Fleet speed is determined by slowest capital ship
                if (fleet.CapitalShips.Count > 0)
                {
                    slowestHyperdrive = fleet
                        .CapitalShips.Select(ship => ship.Hyperdrive)
                        .Where(h => h > 0) // Guard against 0 in data
                        .DefaultIfEmpty(1)
                        .Min();
                    slowestHyperdrive = Math.Max(slowestHyperdrive, 1); // Ensure at least 1
                }
            }
            else if (unit is CapitalShip capitalShip)
            {
                // Single capital ship uses its own hyperdrive
                slowestHyperdrive = Math.Max(capitalShip.Hyperdrive, 1);
            }
            // Other units (fighters, troops, buildings) use DEFAULT_FIGHTER_HYPERDRIVE

            // Calculate base transit time
            int baseTicks = (int)Math.Ceiling((distance * game.GetConfig().Movement.DistanceScale) / slowestHyperdrive);

            // Han Solo speed bonus: best hyperdrive_modifier among fleet characters
            int hanBonus = 0;
            if (unit is Fleet fleetWithChars)
            {
                IEnumerable<Officer> officers = fleetWithChars.GetOfficers();
                if (officers.Any())
                {
                    hanBonus = officers.Select(o => Math.Max(o.HyperdriveModifier, 0)).Max();
                }
            }

            // Apply bonus and clamp to minimum
            int transitTicks = Math.Max(baseTicks - hanBonus, game.GetConfig().Movement.MinTransitTicks);

            return transitTicks;
        }

        /// <summary>
        /// Processes movement for the current tick.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public void ProcessTick(GameRoot game)
        {
            // Traverse all movable units and update their movement
            game.GetGalaxyMap()
                .Traverse(node =>
                {
                    if (node is IMovable movable)
                    {
                        UpdateMovement(movable);
                    }
                });
        }

        /// <summary>
        /// Updates the movement of a movable unit.
        /// </summary>
        /// <param name="movable">The movable unit to update.</param>
        public void UpdateMovement(IMovable movable)
        {
            // Early returns for units that shouldn't move.
            if (ShouldSkipMovement(movable))
            {
                return;
            }

            // Strict validation: InTransit requires valid destination
            if (string.IsNullOrWhiteSpace(movable.Movement?.DestinationInstanceID))
            {
                throw new InvalidOperationException(
                    $"Unit {movable.GetDisplayName()} is InTransit but has no destination. "
                        + "This indicates corrupted game state. Movement must be requested via RequestMove()."
                );
            }

            ISceneNode destination = game.GetSceneNodeByInstanceID<ISceneNode>(
                movable.Movement.DestinationInstanceID
            );
            if (destination == null)
            {
                throw new InvalidOperationException(
                    $"Unit {movable.GetDisplayName()} destination {movable.Movement.DestinationInstanceID} not found. "
                        + "Destination was deleted or never existed. This indicates corrupted game state."
                );
            }

            // Get destination planet (all movement is planet-based)
            Planet destinationPlanet = destination is Planet planet
                ? planet
                : destination.GetParentOfType<Planet>();
            if (destinationPlanet == null)
            {
                throw new InvalidOperationException(
                    $"Destination {destination.GetDisplayName()} is not at a planet location. "
                        + "All movement must resolve to a planet. Deep-space destinations are not supported."
                );
            }

            // Increment elapsed ticks
            movable.Movement.TicksElapsed++;

            // Calculate and apply interpolated position based on progress
            Point newPosition = CalculateInterpolatedPosition(movable, destinationPlanet);
            ApplyMovementProgress(movable, newPosition);

            GameLogger.Log(
                $"{movable.GetDisplayName()} in transit ({movable.Movement.TicksElapsed}/{movable.Movement.TransitTicks} ticks)"
            );

            // Check if the unit has arrived at its destination.
            if (movable.Movement.IsComplete())
            {
                CheckArrival(movable, destination, destinationPlanet);
            }
        }

        /// <summary>
        /// Calculates the interpolated position for a unit in transit.
        /// Pure function - no side effects.
        /// </summary>
        /// <param name="movable">The unit in transit.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <returns>The interpolated position based on transit progress.</returns>
        private Point CalculateInterpolatedPosition(IMovable movable, Planet destinationPlanet)
        {
            float progress = movable.Movement.Progress();
            Point originPos = movable.Movement.OriginPosition;
            Point destPos = destinationPlanet.GetPosition();

            int newX = (int)(originPos.X + (destPos.X - originPos.X) * progress);
            int newY = (int)(originPos.Y + (destPos.Y - originPos.Y) * progress);

            return new Point(newX, newY);
        }

        /// <summary>
        /// Applies the movement progress by updating the unit's current position.
        /// Uses encapsulated SetPosition method instead of direct mutation.
        /// </summary>
        /// <param name="movable">The unit to update.</param>
        /// <param name="newPosition">The new interpolated position.</param>
        private void ApplyMovementProgress(IMovable movable, Point newPosition)
        {
            movable.SetPosition(newPosition);
        }

        /// <summary>
        /// Determines if movement should be skipped for the given movable.
        /// </summary>
        private bool ShouldSkipMovement(IMovable movable)
        {
            // Movement == null means not moving
            if (movable.Movement == null)
            {
                return true;
            }

            // Units still being manufactured shouldn't move
            if (
                movable is IManufacturable manufacturable
                && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building
            )
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles unit arrival at destination after transit completes.
        /// Uses Game.MoveNode() to perform the final relocation to the destination.
        ///
        /// REJECTION HANDLING:
        /// If destination rejects arrival (SceneAccessException), falls back to nearest friendly planet.
        /// This prevents infinite retry loops and stuck units.
        /// </summary>
        private void CheckArrival(
            IMovable movable,
            ISceneNode destination,
            Planet destinationPlanet
        )
        {
            // Unit has arrived - relocate to destination using centralized graph mutation
            try
            {
                game.MoveNode(movable, destination);
                // Destroy movement state (arrival complete)
                movable.Movement = null;
                GameLogger.Log(
                    $"{movable.GetDisplayName()} arrived at {destination.GetDisplayName()}"
                );

                // Event-driven fog of war: capture snapshot on arrival if fleet grants visibility
                if (movable is Fleet fleet && fogOfWar != null)
                {
                    Faction faction = game.Factions.FirstOrDefault(f =>
                        f.InstanceID == fleet.OwnerInstanceID
                    );
                    if (faction != null && fogOfWar.IsPlanetVisible(destinationPlanet, faction))
                    {
                        PlanetSystem system = destinationPlanet.GetParentOfType<PlanetSystem>();
                        if (system != null)
                        {
                            fogOfWar.CaptureSnapshot(
                                faction,
                                destinationPlanet,
                                system,
                                game.CurrentTick
                            );
                        }
                    }
                }
            }
            catch (SceneAccessException ex)
            {
                // Destination rejected the unit (e.g., no capacity, enemy ownership)
                GameLogger.Warning(
                    $"Arrival rejected for {movable.GetDisplayName()} at {destination.GetDisplayName()}: {ex.Message}. "
                        + "Attempting fallback to nearest friendly planet."
                );

                // Immediate fallback - don't retry infinitely
                HandleArrivalRejection(movable, destinationPlanet);
            }
        }

        /// <summary>
        /// Handles arrival rejection by redirecting to nearest friendly planet.
        /// If no friendly planet exists, redirects to current parent (stay in place).
        /// </summary>
        private void HandleArrivalRejection(IMovable movable, Planet rejectedDestination)
        {
            string ownerID = movable.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerID))
            {
                // No owner - destroy movement state and stay at current parent
                movable.Movement = null;
                GameLogger.Warning(
                    $"{movable.GetDisplayName()} has no owner, cannot find fallback. Staying at current location."
                );
                return;
            }

            // Find nearest friendly planet (same logic as ManufacturingSystem uses)
            Planet fallback = FindNearestFactionPlanet(ownerID, movable.GetPosition());

            if (fallback != null && fallback != rejectedDestination)
            {
                // Redirect to fallback planet (RequestMove creates new MovementState)
                GameLogger.Log(
                    $"{movable.GetDisplayName()} redirected to fallback: {fallback.GetDisplayName()}"
                );
                RequestMove(movable, fallback);
            }
            else
            {
                // No valid fallback - destroy movement state and stay at current parent
                ISceneNode currentParent = movable.GetParent();
                movable.Movement = null;
                GameLogger.Warning(
                    $"{movable.GetDisplayName()} has no valid fallback. Staying at {currentParent?.GetDisplayName() ?? "current location"}."
                );
            }
        }

        /// <summary>
        /// Finds nearest planet owned by specified faction.
        /// Uses Euclidean distance from given position.
        /// </summary>
        private Planet FindNearestFactionPlanet(string factionOwnerID, Point fromPosition)
        {
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
    }
}
