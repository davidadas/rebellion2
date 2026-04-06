using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages unit movement during each game tick.
    /// The only system that calls game.MoveNode() for movement purposes.
    /// Other systems request movement via RequestMove() — never call MoveNode() directly.
    /// </summary>
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
        /// Processes movement for the current tick.
        /// </summary>
        public void ProcessTick()
        {
            game.GetGalaxyMap()
                .Traverse(node =>
                {
                    if (node is IMovable movable)
                        UpdateMovement(movable);
                });
        }

        /// <summary>
        /// Immediately reparents the unit to the destination and marks it in transit.
        /// The unit physically travels over multiple ticks but is logically at the destination.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="destination">The target destination (Planet or other container).</param>
        public bool RequestMove(IMovable unit, ISceneNode destination)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            if (unit is Officer capturedOfficer && capturedOfficer.IsCaptured)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is captured and cannot be ordered to move."
                );
                return false;
            }

            if (unit is IManufacturable m && m.ManufacturingStatus == ManufacturingStatus.Building)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is under construction."
                );
                return false;
            }

            return ExecuteMove(unit, destination);
        }

        /// <summary>
        /// Sets up a movement state for a unit that is already at its destination in the scene
        /// graph but needs to visually travel from a known origin (e.g. its production planet).
        /// No reparenting occurs — the unit is already logically placed.
        /// </summary>
        public bool RequestMove(IMovable unit, ISceneNode destination, Planet origin)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (origin == null)
                throw new ArgumentNullException(nameof(origin));

            Planet destinationPlanet = destination is Planet p
                ? p
                : destination.GetParentOfType<Planet>();

            // Destination no longer exists in the scene graph (e.g. fleet was destroyed).
            if (destinationPlanet == null)
                return false;

            // Destination changed sides since enqueue — route the same way as an in-transit
            // arrival rejection.
            if (destinationPlanet.GetOwnerInstanceID() != unit.GetOwnerInstanceID())
            {
                HandleArrivalRejection(unit, destinationPlanet);
                return false;
            }

            if (destinationPlanet == origin)
            {
                unit.Movement = null;
                return true;
            }

            int transitTicks = CalculateTransitTicks(unit, origin.GetPosition(), destinationPlanet);
            unit.Movement = new MovementState
            {
                TransitTicks = transitTicks,
                TicksElapsed = 0,
                OriginPosition = origin.GetPosition(),
                CurrentPosition = origin.GetPosition(),
            };

            GameLogger.Log(
                $"{unit.GetDisplayName()} departing {origin.GetDisplayName()} for {destination.GetDisplayName()} (ETA: {transitTicks} ticks)"
            );
            return true;
        }

        public void RequestGroupMove(List<IMovable> units, ISceneNode destination)
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            foreach (IMovable unit in units)
            {
                if (unit is Officer o && o.IsCaptured)
                {
                    bool hasEscort = units.Any(u =>
                        !ReferenceEquals(u, unit)
                        && !(u is Officer uo && uo.IsCaptured)
                        && u.GetOwnerInstanceID() == o.CaptorInstanceID
                    );

                    if (!hasEscort)
                    {
                        GameLogger.Warning(
                            $"RequestGroupMove: {unit.GetDisplayName()} has no escort from capturing faction — excluded."
                        );
                        continue;
                    }
                }

                ExecuteMove(unit, destination);
            }
        }

        private bool ExecuteMove(IMovable unit, ISceneNode destination)
        {
            // Fleet only accepts CapitalShips directly. Resolve other unit types
            // to an appropriate CapitalShip within the destination fleet.
            if (destination is Fleet targetFleet && !(unit is Fleet) && !(unit is CapitalShip))
            {
                destination = ResolveFleetTarget(unit, targetFleet);
                if (destination == null)
                {
                    GameLogger.Warning(
                        $"RequestMove rejected: no capacity in {targetFleet.GetDisplayName()} for {unit.GetDisplayName()}"
                    );
                    return false;
                }
            }

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

            Planet originPlanet = unit.GetParentOfType<Planet>();
            if (originPlanet == null)
            {
                throw new InvalidOperationException(
                    $"Unit {unit.GetDisplayName()} is not at a planet location."
                );
            }

            // If the unit is mid-flight, start the new journey from its current visual position
            // rather than the planet it is logically parented to.
            Point originPosition = unit.Movement?.CurrentPosition ?? originPlanet.GetPosition();
            int transitTicks = CalculateTransitTicks(unit, originPosition, destinationPlanet);

            ISceneNode currentParent = ((ISceneNode)unit).GetParent();
            if (currentParent == destination)
            {
                unit.Movement = null;
                return true;
            }

            if (!destination.CanAcceptChild((ISceneNode)unit))
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {destination.GetDisplayName()} cannot accept {unit.GetDisplayName()}"
                );
                return false;
            }

            game.MoveNode((ISceneNode)unit, destination);

            unit.Movement = new MovementState
            {
                TransitTicks = transitTicks,
                TicksElapsed = 0,
                OriginPosition = originPosition,
                CurrentPosition = originPosition,
            };

            GameLogger.Log(
                $"{unit.GetDisplayName()} ordered to move to {destination.GetDisplayName()} (ETA: {transitTicks} ticks)"
            );
            return true;
        }

        /// <summary>
        /// Resolves a Fleet destination to the appropriate CapitalShip for non-fleet units.
        /// </summary>
        private ISceneNode ResolveFleetTarget(IMovable unit, Fleet fleet)
        {
            if (unit is Starfighter)
                return fleet.FindShipForStarfighter();
            if (unit is Regiment)
                return fleet.FindShipForRegiment();
            if (unit is Officer && fleet.CapitalShips.Count > 0)
                return fleet.CapitalShips[0];
            return null;
        }

        /// <summary>
        /// Calculates transit time in ticks based on distance and hyperdrive rating.
        /// Result is clamped to MinTransitTicks.
        /// </summary>
        private int CalculateTransitTicks(IMovable unit, Point originPos, Planet destination)
        {
            Point destPos = destination.GetPosition();
            double dx = destPos.X - originPos.X;
            double dy = destPos.Y - originPos.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            int slowestHyperdrive = game.GetConfig().Movement.DefaultFighterHyperdrive;
            int speedBonus = 0;

            if (unit is Fleet fleet)
            {
                if (fleet.CapitalShips.Count > 0)
                {
                    slowestHyperdrive = fleet
                        .CapitalShips.Select(ship => ship.Hyperdrive)
                        .Where(h => h > 0)
                        .DefaultIfEmpty(1)
                        .Min();
                    slowestHyperdrive = Math.Max(slowestHyperdrive, 1);
                }

                IEnumerable<Officer> officers = fleet.GetOfficers();
                if (officers.Any())
                {
                    speedBonus = officers.Select(o => Math.Max(o.HyperdriveModifier, 0)).Max();
                }
            }
            else if (unit is CapitalShip capitalShip)
            {
                slowestHyperdrive = Math.Max(capitalShip.Hyperdrive, 1);
            }

            int baseTicks = (int)
                Math.Ceiling(
                    (distance * game.GetConfig().Movement.DistanceScale) / slowestHyperdrive
                );

            return Math.Max(baseTicks - speedBonus, game.GetConfig().Movement.MinTransitTicks);
        }

        /// <summary>
        /// Updates the movement of a movable unit for the current tick.
        /// </summary>
        public void UpdateMovement(IMovable movable)
        {
            if (ShouldSkipMovement(movable))
                return;

            Planet destinationPlanet = ((ISceneNode)movable).GetParentOfType<Planet>();
            if (destinationPlanet == null)
            {
                throw new InvalidOperationException(
                    $"Unit {movable.GetDisplayName()} is in transit but has no parent planet."
                );
            }

            ISceneNode destination = (ISceneNode)movable.GetParent();

            movable.Movement.TicksElapsed++;
            movable.SetPosition(CalculateInterpolatedPosition(movable, destinationPlanet));

            GameLogger.Log(
                $"{movable.GetDisplayName()} in transit ({movable.Movement.TicksElapsed}/{movable.Movement.TransitTicks} ticks)"
            );

            if (movable.Movement.IsComplete())
                CheckArrival(movable, destination, destinationPlanet);
        }

        private Point CalculateInterpolatedPosition(IMovable movable, Planet destinationPlanet)
        {
            float progress = movable.Movement.Progress();
            Point originPos = movable.Movement.OriginPosition;
            Point destPos = destinationPlanet.GetPosition();

            return new Point(
                (int)(originPos.X + (destPos.X - originPos.X) * progress),
                (int)(originPos.Y + (destPos.Y - originPos.Y) * progress)
            );
        }

        private bool ShouldSkipMovement(IMovable movable)
        {
            if (movable.Movement == null)
                return true;

            if (
                movable is IManufacturable manufacturable
                && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building
            )
                return true;

            return false;
        }

        /// <summary>
        /// Handles unit arrival at destination after transit completes.
        /// Falls back to nearest friendly planet if the destination rejects arrival.
        /// </summary>
        private void CheckArrival(
            IMovable movable,
            ISceneNode destination,
            Planet destinationPlanet
        )
        {
            // Check if the destination (or its parent fleet) is still in transit.
            // If so, recalculate the route to chase the moving target.
            Fleet movingFleet = destination is Fleet f ? f
                : (destination is CapitalShip cs ? cs.GetParent() as Fleet : null);
            if (movingFleet != null && movingFleet.Movement != null)
            {
                Planet newDest = ((ISceneNode)movingFleet).GetParentOfType<Planet>();
                if (newDest != null)
                {
                    Point currentPos = movable.Movement.CurrentPosition;
                    int newTicks = CalculateTransitTicks(movable, currentPos, newDest);
                    movable.Movement = new MovementState
                    {
                        TransitTicks = newTicks,
                        TicksElapsed = 0,
                        OriginPosition = currentPos,
                        CurrentPosition = currentPos,
                    };
                }
                return;
            }

            // If destination changed sides since dispatch, handle before attempting MoveNode.
            if (destinationPlanet.GetOwnerInstanceID() != movable.GetOwnerInstanceID())
            {
                if (movable is Building)
                {
                    game.DetachNode((ISceneNode)movable);
                    GameLogger.Log(
                        $"Building {movable.GetDisplayName()} destroyed: destination changed sides during transit."
                    );
                }
                else
                {
                    HandleArrivalRejection(movable, destinationPlanet);
                }
                return;
            }

            try
            {
                game.MoveNode(movable, destination);
                movable.Movement = null;
                GameLogger.Log(
                    $"{movable.GetDisplayName()} arrived at {destination.GetDisplayName()}"
                );

                if (movable is Fleet fleet && fogOfWar != null)
                {
                    Faction faction = game.Factions.FirstOrDefault(f =>
                        f.InstanceID == fleet.OwnerInstanceID
                    );
                    if (faction != null && fogOfWar.IsPlanetVisible(destinationPlanet, faction))
                    {
                        PlanetSystem system = destinationPlanet.GetParentOfType<PlanetSystem>();
                        if (system != null)
                            fogOfWar.CaptureSnapshot(
                                faction,
                                destinationPlanet,
                                system,
                                game.CurrentTick
                            );
                    }
                }
            }
            catch (SceneAccessException ex)
            {
                GameLogger.Warning(
                    $"Arrival rejected for {movable.GetDisplayName()} at {destination.GetDisplayName()}: {ex.Message}. "
                        + "Attempting fallback to nearest friendly planet."
                );
                HandleArrivalRejection(movable, destinationPlanet);
            }
        }

        /// <summary>
        /// Handles arrival rejection by redirecting to nearest friendly planet.
        /// </summary>
        private void HandleArrivalRejection(IMovable movable, Planet rejectedDestination)
        {
            string ownerID = movable.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerID))
            {
                movable.Movement = null;
                GameLogger.Warning(
                    $"{movable.GetDisplayName()} has no owner, cannot find fallback."
                );
                return;
            }

            Planet fallback = FindNearestFactionPlanet(ownerID, movable.GetPosition());

            if (fallback != null && fallback != rejectedDestination && RequestMove(movable, fallback))
            {
                GameLogger.Log(
                    $"{movable.GetDisplayName()} redirected to fallback: {fallback.GetDisplayName()}"
                );
            }
            else
            {
                movable.Movement = null;
                GameLogger.Warning(
                    $"{movable.GetDisplayName()} has no valid fallback. Staying at {movable.GetParent()?.GetDisplayName() ?? "current location"}."
                );
            }
        }

        private Planet FindNearestFactionPlanet(string factionOwnerID, Point fromPosition)
        {
            return game.GetSceneNodesByType<Planet>()
                .Where(p => p.GetOwnerInstanceID() == factionOwnerID)
                .OrderBy(p =>
                {
                    Point pos = p.GetPosition();
                    double dx = pos.X - fromPosition.X;
                    double dy = pos.Y - fromPosition.Y;
                    return dx * dx + dy * dy;
                })
                .FirstOrDefault();
        }
    }
}
