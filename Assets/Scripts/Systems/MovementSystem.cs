using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
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
    public class MovementSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly FogOfWarSystem _fogOfWar;
        private readonly BlockadeSystem _blockade;
        private readonly List<GameResult> _pendingResults = new List<GameResult>();

        /// <summary>
        /// Initializes a new instance of the MovementSystem class.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="fogOfWar">The fog of war system for capturing snapshots on arrival.</param>
        /// <param name="blockade">The blockade system for evacuation loss rolls.</param>
        public MovementSystem(
            GameRoot game,
            FogOfWarSystem fogOfWar,
            BlockadeSystem blockade = null
        )
        {
            _game = game;
            _fogOfWar = fogOfWar;
            _blockade = blockade;
        }

        /// <summary>
        /// Processes movement for the current tick.
        /// </summary>
        /// <returns>Movement-related events generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            results.AddRange(_pendingResults);
            _pendingResults.Clear();
            _game
                .GetGalaxyMap()
                .Traverse(node =>
                {
                    if (node is IMovable movable)
                        results.AddRange(UpdateMovement(movable));
                });
            return results;
        }

        /// <summary>
        /// Moves a unit to a destination. Immediately reparents the unit in the scene graph
        /// and marks it in visual transit. The unit is logically at the destination from this
        /// point; its position interpolates over subsequent ticks.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="destination">The target scene node to move toward.</param>
        public void RequestMove(IMovable unit, ISceneNode destination)
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
                return;
            }

            if (unit is IManufacturable m && m.ManufacturingStatus == ManufacturingStatus.Building)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is under construction."
                );
                return;
            }

            ExecuteMove(unit, destination);
        }

        /// <summary>
        /// Sets up visual transit for a unit that is already at its destination in the scene
        /// graph. Used after manufacturing completes: the unit was pre-placed at its destination
        /// during enqueue, and this sets it travelling visually from the production planet.
        /// If the destination has changed sides since enqueue, routes via HandleArrivalRejection.
        /// </summary>
        /// <param name="unit">The unit to set in transit.</param>
        /// <param name="destination">The pre-assigned destination scene node.</param>
        /// <param name="origin">The production planet the unit departs from visually.</param>
        public void RequestMove(IMovable unit, ISceneNode destination, Planet origin)
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
                return;

            // Destination changed sides since enqueue — same handling as mid-transit rejection.
            if (destinationPlanet.GetOwnerInstanceID() != unit.GetOwnerInstanceID())
            {
                // Unit is already inside a fleet — the fleet handles its own routing.
                if (((ISceneNode)unit).GetParent() is Fleet)
                {
                    unit.Movement = null;
                    return;
                }
                HandleArrivalRejection(unit, destinationPlanet);
                return;
            }

            if (destinationPlanet == origin)
            {
                unit.Movement = null;
                return;
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
        }

        /// <summary>
        /// Moves a group of units to the same destination. Captured officers are excluded
        /// unless their captor faction has an escort in the group.
        /// </summary>
        /// <param name="units">The units to move as a group.</param>
        /// <param name="destination">The shared target scene node.</param>
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

        /// <summary>
        /// Advances the movement of a single unit by one tick and handles arrival.
        /// </summary>
        /// <param name="movable">The movable unit to update.</param>
        /// <returns>Events generated on arrival or during transit.</returns>
        private List<GameResult> UpdateMovement(IMovable movable)
        {
            if (movable.Movement == null)
                return new List<GameResult>();

            if (
                movable is IManufacturable m
                && m.GetManufacturingStatus() == ManufacturingStatus.Building
            )
                return new List<GameResult>();

            Planet destinationPlanet = ((ISceneNode)movable).GetParentOfType<Planet>();
            if (destinationPlanet == null)
                throw new InvalidOperationException(
                    $"Unit {movable.GetDisplayName()} is in transit but has no parent planet."
                );

            ISceneNode destination = (ISceneNode)movable.GetParent();

            movable.Movement.TicksElapsed++;
            movable.SetPosition(CalculateInterpolatedPosition(movable, destinationPlanet));

            GameLogger.Log(
                $"{movable.GetDisplayName()} in transit ({movable.Movement.TicksElapsed}/{movable.Movement.TransitTicks} ticks)"
            );

            if (movable.Movement.IsComplete())
                return CheckArrival(movable, destination, destinationPlanet);

            return new List<GameResult>();
        }

        /// <summary>
        /// Returns the interpolated screen position of a unit between its origin and its destination planet.
        /// </summary>
        /// <param name="movable"></param>
        /// <param name="destinationPlanet"></param>
        /// <returns>The interpolated position.</returns>
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

        /// <summary>
        /// Handles unit arrival at its destination. Destroys buildings if destination changed
        /// sides; reroutes other units. Falls back to HandleArrivalRejection on scene rejection.
        /// </summary>
        private List<GameResult> CheckArrival(
            IMovable movable,
            ISceneNode destination,
            Planet destinationPlanet
        )
        {
            // If the destination fleet is still in transit, chase it.
            Fleet movingFleet = destination is Fleet f
                ? f
                : (destination is CapitalShip cs ? cs.GetParent() as Fleet : null);
            if (movingFleet?.Movement != null)
            {
                Planet newDest = ((ISceneNode)movingFleet).GetParentOfType<Planet>();
                if (newDest != null)
                {
                    Point currentPos = movable.Movement.CurrentPosition;
                    movable.Movement = new MovementState
                    {
                        TransitTicks = CalculateTransitTicks(movable, currentPos, newDest),
                        TicksElapsed = 0,
                        OriginPosition = currentPos,
                        CurrentPosition = currentPos,
                    };
                }
                return new List<GameResult>();
            }

            // Mission participants are managed by MissionSystem, not by arrival logic.
            if (destination is Mission)
            {
                movable.Movement = null;
                if (movable is Officer missionOfficer)
                {
                    return new List<GameResult>
                    {
                        new RoleEnrouteActiveResult
                        {
                            Officer = missionOfficer,
                            IsActive = false,
                            Tick = _game.CurrentTick,
                        },
                    };
                }
                return new List<GameResult>();
            }

            // Destination changed sides since dispatch.
            if (destinationPlanet.GetOwnerInstanceID() != movable.GetOwnerInstanceID())
            {
                if (movable is Building building)
                {
                    _game.DetachNode((ISceneNode)movable);
                    GameLogger.Log(
                        $"Building {movable.GetDisplayName()} destroyed: destination changed sides during transit."
                    );
                    return new List<GameResult>
                    {
                        new GameObjectDestroyedOnArrivalResult
                        {
                            DestroyedObject = building,
                            Context = destinationPlanet,
                            Tick = _game.CurrentTick,
                        },
                    };
                }
                else
                {
                    HandleArrivalRejection(movable, destinationPlanet);
                }
                return new List<GameResult>();
            }

            try
            {
                _game.MoveNode(movable, destination);
                movable.Movement = null;
                GameLogger.Log(
                    $"{movable.GetDisplayName()} arrived at {destination.GetDisplayName()}"
                );

                if (movable is Fleet fleet && _fogOfWar != null)
                {
                    Faction faction = _game.Factions.FirstOrDefault(f =>
                        f.InstanceID == fleet.OwnerInstanceID
                    );
                    if (faction != null && _fogOfWar.IsPlanetVisible(destinationPlanet, faction))
                    {
                        PlanetSystem system = destinationPlanet.GetParentOfType<PlanetSystem>();
                        if (system != null)
                            _fogOfWar.CaptureSnapshot(
                                faction,
                                destinationPlanet,
                                system,
                                _game.CurrentTick
                            );
                    }
                }

                return new List<GameResult>
                {
                    new GameObjectEnrouteActiveResult
                    {
                        GameObject = movable as IGameEntity,
                        IsActive = false,
                        Tick = _game.CurrentTick,
                    },
                    new UnitArrivedResult
                    {
                        Unit = movable as IGameEntity,
                        Destination = destinationPlanet,
                        Tick = _game.CurrentTick,
                    },
                };
            }
            catch (SceneAccessException ex)
            {
                GameLogger.Warning(
                    $"Arrival rejected for {movable.GetDisplayName()} at {destination.GetDisplayName()}: {ex.Message}. "
                        + "Attempting fallback to nearest friendly planet."
                );
                HandleArrivalRejection(movable, destinationPlanet);
                return new List<GameResult>();
            }
        }

        /// <summary>
        /// Moves a unit to the nearest planet owned by its faction.
        /// Used when a unit's parent is being destroyed and it has no other refuge.
        /// Clears movement state if no friendly planet exists.
        /// </summary>
        /// <param name="unit">The unit to evacuate.</param>
        public void EvacuateToNearestFriendlyPlanet(IMovable unit)
        {
            string ownerID = unit.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerID))
            {
                unit.Movement = null;
                GameLogger.Warning($"{unit.GetDisplayName()} has no owner — cannot evacuate.");
                return;
            }

            Planet currentPlanet = ((ISceneNode)unit).GetParentOfType<Planet>();
            Planet fallback = FindNearestFactionPlanet(ownerID, unit.GetPosition(), currentPlanet);
            if (fallback != null)
            {
                RequestMove(unit, fallback);
            }
            else
            {
                unit.Movement = null;
                GameLogger.Warning(
                    $"{unit.GetDisplayName()} has no friendly planet to evacuate to."
                );
            }
        }

        /// <summary>
        /// Redirects a unit to the nearest friendly planet when its destination is unavailable.
        /// Clears movement state if no valid fallback exists.
        /// </summary>
        /// <param name="movable">The unit whose arrival was rejected.</param>
        /// <param name="rejectedDestination">The planet that refused the unit.</param>
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

            if (fallback != null && fallback != rejectedDestination)
            {
                RequestMove(movable, fallback);
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

        /// <summary>
        /// Returns the nearest planet owned by the specified faction to the given position.
        /// </summary>
        private Planet FindNearestFactionPlanet(
            string factionOwnerID,
            Point fromPosition,
            Planet exclude = null
        )
        {
            return _game
                .GetSceneNodesByType<Planet>()
                .Where(p => p.GetOwnerInstanceID() == factionOwnerID && p != exclude)
                .OrderBy(p =>
                {
                    Point pos = p.GetPosition();
                    double dx = pos.X - fromPosition.X;
                    double dy = pos.Y - fromPosition.Y;
                    return dx * dx + dy * dy;
                })
                .FirstOrDefault();
        }

        /// <summary>
        /// Reparents the unit to the destination, sets up its visual transit state, and logs
        /// the departure. Returns without moving if the destination cannot accept the unit.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="destination">The target scene node to reparent into.</param>
        private void ExecuteMove(IMovable unit, ISceneNode destination)
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
                    return;
                }
            }

            Planet destinationPlanet = destination is Planet planet
                ? planet
                : destination.GetParentOfType<Planet>();
            if (destinationPlanet == null)
                throw new InvalidOperationException(
                    $"Destination {destination.GetDisplayName()} is not at a planet location. "
                        + "All movement must resolve to a planet."
                );

            Planet originPlanet = unit.GetParentOfType<Planet>();
            if (originPlanet == null)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is not at a planet location and cannot move."
                );
                return;
            }

            if (_blockade != null)
            {
                EvacuationLossesResult evacResult = _blockade.ApplyEvacuationLosses(
                    unit,
                    originPlanet
                );
                if (evacResult != null)
                {
                    _pendingResults.Add(evacResult);
                    return;
                }
            }

            // If the unit is mid-flight, start the new journey from its current visual position.
            Point originPosition = unit.Movement?.CurrentPosition ?? originPlanet.GetPosition();
            int transitTicks = CalculateTransitTicks(unit, originPosition, destinationPlanet);

            if (((ISceneNode)unit).GetParent() == destination)
            {
                unit.Movement = null;
                return;
            }

            if (!destination.CanAcceptChild((ISceneNode)unit))
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {destination.GetDisplayName()} cannot accept {unit.GetDisplayName()}"
                );
                return;
            }

            _game.MoveNode((ISceneNode)unit, destination);

            unit.Movement = new MovementState
            {
                TransitTicks = transitTicks,
                TicksElapsed = 0,
                OriginPosition = originPosition,
                CurrentPosition = originPosition,
            };

            _pendingResults.Add(
                new GameObjectEnrouteResult
                {
                    GameObject = unit as IGameEntity,
                    Tick = _game.CurrentTick,
                }
            );
            _pendingResults.Add(
                new GameObjectEnrouteActiveResult
                {
                    GameObject = unit as IGameEntity,
                    IsActive = true,
                    Tick = _game.CurrentTick,
                }
            );

            if (unit is Officer officerEnroute && destination is Mission)
            {
                _pendingResults.Add(
                    new RoleEnrouteActiveResult
                    {
                        Officer = officerEnroute,
                        IsActive = true,
                        Tick = _game.CurrentTick,
                    }
                );
            }

            GameLogger.Log(
                $"{unit.GetDisplayName()} ordered to move to {destination.GetDisplayName()} (ETA: {transitTicks} ticks)"
            );
        }

        /// <summary>
        /// Resolves a Fleet destination to the appropriate CapitalShip for non-fleet units.
        /// </summary>
        /// <param name="unit">The non-fleet unit being assigned.</param>
        /// <param name="fleet">The fleet to find a suitable ship within.</param>
        /// <returns>The target ship, or null if no valid ship exists.</returns>
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
        /// <param name="unit">The unit whose hyperdrive rating determines speed.</param>
        /// <param name="originPos">The starting position.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>Number of ticks the transit will take.</returns>
        private int CalculateTransitTicks(IMovable unit, Point originPos, Planet destination)
        {
            Point destPos = destination.GetPosition();
            double dx = destPos.X - originPos.X;
            double dy = destPos.Y - originPos.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            int slowestHyperdrive = _game.GetConfig().Movement.DefaultFighterHyperdrive;
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
                    speedBonus = officers.Max(o => Math.Max(o.HyperdriveModifier, 0));
            }
            else if (unit is CapitalShip capitalShip)
            {
                slowestHyperdrive = Math.Max(capitalShip.Hyperdrive, 1);
            }

            int baseTicks = (int)
                Math.Ceiling(
                    (distance * _game.GetConfig().Movement.DistanceScale) / slowestHyperdrive
                );

            return Math.Max(baseTicks - speedBonus, _game.GetConfig().Movement.MinTransitTicks);
        }
    }
}
