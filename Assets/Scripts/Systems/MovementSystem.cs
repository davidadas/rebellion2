using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
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
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fogOfWar = fogOfWar ?? throw new ArgumentNullException(nameof(fogOfWar));
            _blockade = blockade;
        }

        /// <summary>
        /// Processes movement for the current tick.
        /// </summary>
        /// <returns>Movement-related events generated this tick.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>(_pendingResults);
            _pendingResults.Clear();
            _game
                .GetGalaxyMap()
                .Traverse(node =>
                {
                    if (node is IMovable movable)
                        UpdateMovement(movable, results);
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

            if (!CanReceiveMoveOrder(unit, allowManufacturingRetarget: true))
                return;

            if (IsUnderConstruction(unit))
            {
                RetargetManufacturingDestination(unit, destination);
                return;
            }

            if (unit is Officer capturedOfficer && capturedOfficer.IsCaptured)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is captured and cannot be ordered to move."
                );
                return;
            }

            ExecuteMove(unit, destination);
        }

        /// <summary>
        /// Sets up visual transit for a manufactured unit that is already parented to its
        /// destination in the scene graph.
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

            // Determine the destination planet for the unit.
            // All movement must resolve to a planet location for transit purposes.
            Planet destinationPlanet = RequireDestinationPlanet(destination);

            // If the destination planet is hostile and the unit cannot enter hostile orbit, reject the move order.
            if (
                destinationPlanet.GetOwnerInstanceID() != unit.GetOwnerInstanceID()
                && !CanEnterHostileOrbit(unit, destination)
            )
            {
                ExecuteMove(unit, origin);
                return;
            }

            // If the unit is already at the destination, do nothing.
            if (destinationPlanet == origin)
            {
                unit.Movement = null;
                return;
            }

            // Calculate transit time and set the unit in motion.
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
        /// Moves a group of units to the same destination after validating the whole group.
        /// </summary>
        /// <param name="units">The units to move as a group.</param>
        /// <param name="destination">The shared target scene node.</param>
        public void RequestMove(List<IMovable> units, ISceneNode destination)
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            if (!CanMoveGroup(units, destination))
                return;

            foreach (IMovable unit in units)
                ExecuteMove(unit, destination);
        }

        /// <summary>
        /// Returns whether every unit in a group can receive the move order.
        /// </summary>
        /// <param name="units">The units being moved together.</param>
        /// <param name="destination">The shared destination.</param>
        /// <returns>True if the whole group can move.</returns>
        private bool CanMoveGroup(List<IMovable> units, ISceneNode destination)
        {
            ISceneNode groupOrigin = null;
            foreach (IMovable unit in units)
            {
                if (unit == null)
                {
                    GameLogger.Warning("RequestMove rejected: group contains a null unit.");
                    return false;
                }

                if (!CanReceiveMoveOrder(unit, allowManufacturingRetarget: false))
                    return false;

                ISceneNode unitOrigin = ((ISceneNode)unit).GetParent();
                if (unitOrigin == null)
                {
                    GameLogger.Warning(
                        $"RequestMove rejected: {unit.GetDisplayName()} is not at a movable location."
                    );
                    return false;
                }

                if (groupOrigin == null)
                    groupOrigin = unitOrigin;
                else if (!ReferenceEquals(groupOrigin, unitOrigin))
                {
                    GameLogger.Warning(
                        "RequestMove rejected: group units are not at the same location."
                    );
                    return false;
                }

                if (!TryResolveAcceptedDestination(unit, destination, out _))
                    return false;
            }

            foreach (Officer capturedOfficer in units.OfType<Officer>().Where(o => o.IsCaptured))
            {
                if (HasCaptorEscortInGroup(capturedOfficer, units))
                    continue;

                GameLogger.Warning(
                    $"RequestMove rejected: {capturedOfficer.GetDisplayName()} has no capturing officer escort."
                );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether a unit can receive a movement order.
        /// </summary>
        /// <param name="unit">The unit to check.</param>
        /// <param name="allowManufacturingRetarget">Whether an unfinished manufactured unit may change destination.</param>
        /// <returns>True if the unit can receive the order.</returns>
        private static bool CanReceiveMoveOrder(IMovable unit, bool allowManufacturingRetarget)
        {
            if (unit.Movement != null)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is already in transit."
                );
                return false;
            }

            if (IsUnderConstruction(unit))
            {
                if (allowManufacturingRetarget)
                    return true;

                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is under construction."
                );
                return false;
            }

            if (IsCompletedBuilding(unit))
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is a completed building."
                );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether a movable unit is still being manufactured.
        /// </summary>
        /// <param name="unit">The unit to inspect.</param>
        /// <returns>True if the unit is still under construction.</returns>
        private static bool IsUnderConstruction(IMovable unit)
        {
            return unit is IManufacturable manufacturable
                && manufacturable.ManufacturingStatus == ManufacturingStatus.Building;
        }

        /// <summary>
        /// Returns whether a movable unit is a completed building.
        /// </summary>
        /// <param name="unit">The unit to inspect.</param>
        /// <returns>True if the unit is a completed building.</returns>
        private static bool IsCompletedBuilding(IMovable unit)
        {
            return unit is Building building
                && building.ManufacturingStatus == ManufacturingStatus.Complete;
        }

        /// <summary>
        /// Returns whether a captured officer has a valid escort in the movement group.
        /// </summary>
        /// <param name="capturedOfficer">The captured officer to check.</param>
        /// <param name="units">The units being moved together.</param>
        /// <returns>True if the group includes an escort from the captor faction.</returns>
        private static bool HasCaptorEscortInGroup(Officer capturedOfficer, List<IMovable> units)
        {
            string captorId = capturedOfficer.CaptorInstanceID;
            return !string.IsNullOrEmpty(captorId)
                && units
                    .OfType<Officer>()
                    .Any(escort =>
                        !ReferenceEquals(escort, capturedOfficer)
                        && !escort.IsCaptured
                        && escort.GetOwnerInstanceID() == captorId
                    );
        }

        /// <summary>
        /// Advances the movement of a single unit by one tick and handles arrival.
        /// </summary>
        /// <param name="movable">The movable unit to update.</param>
        /// <param name="results">The results generated this tick.</param>
        private void UpdateMovement(IMovable movable, List<GameResult> results)
        {
            if (movable.Movement == null)
                return;

            if (
                movable is IManufacturable m
                && m.GetManufacturingStatus() == ManufacturingStatus.Building
            )
                return;

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
                CheckArrival(movable, destination, destinationPlanet, results);
        }

        /// <summary>
        /// Returns the interpolated screen position of a unit between its origin and its destination planet.
        /// </summary>
        /// <param name="movable">The moving unit.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
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
        /// Handles unit arrival at its destination.
        /// </summary>
        /// <param name="movable">The moving unit.</param>
        /// <param name="destination">The destination node.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <param name="results">The results generated this tick.</param>
        private void CheckArrival(
            IMovable movable,
            ISceneNode destination,
            Planet destinationPlanet,
            List<GameResult> results
        )
        {
            if (TryFollowMovingFleetDestination(movable, destination))
                return;

            if (destination is Mission)
            {
                CompleteMissionParticipantArrival(movable, results);
                return;
            }

            if (HasArrivalOwnerConflict(movable, destination, destinationPlanet))
            {
                RejectArrivalAtChangedOwner(movable, destinationPlanet, results);
                return;
            }

            try
            {
                CompleteArrival(movable, destination, destinationPlanet);
                AddArrivalResults(movable, destinationPlanet, results);
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
        /// Redirects a unit when its fleet destination is still moving.
        /// </summary>
        /// <param name="movable">The moving unit.</param>
        /// <param name="destination">The requested destination.</param>
        /// <returns>True if the unit was redirected to continue chasing the fleet.</returns>
        private bool TryFollowMovingFleetDestination(IMovable movable, ISceneNode destination)
        {
            Fleet movingFleet = destination is Fleet fleet
                ? fleet
                : (destination is CapitalShip ship ? ship.GetParent() as Fleet : null);
            if (movingFleet?.Movement == null)
                return false;

            Planet newDestination = ((ISceneNode)movingFleet).GetParentOfType<Planet>();
            if (newDestination == null)
                return true;

            Point currentPosition = movable.Movement.CurrentPosition;
            movable.Movement = new MovementState
            {
                TransitTicks = CalculateTransitTicks(movable, currentPosition, newDestination),
                TicksElapsed = 0,
                OriginPosition = currentPosition,
                CurrentPosition = currentPosition,
            };
            return true;
        }

        /// <summary>
        /// Completes arrival into a mission node.
        /// </summary>
        /// <param name="movable">The arriving unit.</param>
        /// <param name="results">The results generated this tick.</param>
        private void CompleteMissionParticipantArrival(IMovable movable, List<GameResult> results)
        {
            movable.Movement = null;
            if (movable is not IMissionParticipant missionParticipant)
                return;

            results.Add(
                new RoleEnrouteActiveResult
                {
                    Participant = missionParticipant,
                    IsActive = false,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Returns true when a destination's ownership now rejects the arriving unit.
        /// </summary>
        /// <param name="movable">The arriving unit.</param>
        /// <param name="destination">The destination node.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <returns>True if the unit cannot complete arrival.</returns>
        private static bool HasArrivalOwnerConflict(
            IMovable movable,
            ISceneNode destination,
            Planet destinationPlanet
        )
        {
            string destinationOwner = destinationPlanet.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(destinationOwner)
                && destinationOwner != movable.GetOwnerInstanceID()
                && !CanEnterHostileOrbit(movable, destination);
        }

        /// <summary>
        /// Handles arrival rejection after a destination changes owner.
        /// </summary>
        /// <param name="movable">The arriving unit.</param>
        /// <param name="destinationPlanet">The rejecting planet.</param>
        /// <param name="results">The results generated this tick.</param>
        private void RejectArrivalAtChangedOwner(
            IMovable movable,
            Planet destinationPlanet,
            List<GameResult> results
        )
        {
            if (movable is not Building building)
            {
                HandleArrivalRejection(movable, destinationPlanet);
                return;
            }

            _game.DetachNode((ISceneNode)movable);
            GameLogger.Log(
                $"Building {movable.GetDisplayName()} destroyed: destination changed sides during transit."
            );
            results.Add(
                new GameObjectDestroyedOnArrivalResult
                {
                    DestroyedObject = building,
                    Context = destinationPlanet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Applies the scene-graph and visibility effects of a successful arrival.
        /// </summary>
        /// <param name="movable">The arriving unit.</param>
        /// <param name="destination">The destination node.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        private void CompleteArrival(
            IMovable movable,
            ISceneNode destination,
            Planet destinationPlanet
        )
        {
            _game.MoveNode(movable, destination);
            movable.Movement = null;
            GameLogger.Log($"{movable.GetDisplayName()} arrived at {destination.GetDisplayName()}");

            if (movable is Building && destination is Planet arrivalPlanet)
                arrivalPlanet.IsColonized = true;

            string arrivingOwner = movable.GetOwnerInstanceID();
            if (!string.IsNullOrEmpty(arrivingOwner))
                destinationPlanet.AddVisitor(arrivingOwner);

            if (movable is Fleet fleet)
                CaptureFleetArrivalSnapshot(fleet, destinationPlanet);
        }

        /// <summary>
        /// Captures fog-of-war state for an arriving fleet if visible.
        /// </summary>
        /// <param name="fleet">The arriving fleet.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        private void CaptureFleetArrivalSnapshot(Fleet fleet, Planet destinationPlanet)
        {
            Faction faction = _game.Factions.FirstOrDefault(f =>
                f.InstanceID == fleet.OwnerInstanceID
            );
            if (faction == null || !_fogOfWar.IsPlanetVisible(destinationPlanet, faction))
                return;

            PlanetSystem system = destinationPlanet.GetParentOfType<PlanetSystem>();
            if (system != null)
                _fogOfWar.CaptureSnapshot(faction, destinationPlanet, system, _game.CurrentTick);
        }

        /// <summary>
        /// Adds the standard arrival results for a unit.
        /// </summary>
        /// <param name="movable">The arriving unit.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <param name="results">The results generated this tick.</param>
        private void AddArrivalResults(
            IMovable movable,
            Planet destinationPlanet,
            List<GameResult> results
        )
        {
            results.Add(
                new GameObjectEnrouteActiveResult
                {
                    GameObject = movable as IGameEntity,
                    IsActive = false,
                    Tick = _game.CurrentTick,
                }
            );
            results.Add(
                new UnitArrivedResult
                {
                    Unit = movable as IGameEntity,
                    Destination = destinationPlanet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Returns true when the unit type is allowed to finish movement at a hostile planet.
        /// </summary>
        /// <param name="movable">The unit completing movement.</param>
        /// <param name="destination">The destination node receiving the unit.</param>
        /// <returns>True if hostile arrival is a valid end state for this unit.</returns>
        private static bool CanEnterHostileOrbit(IMovable movable, ISceneNode destination)
        {
            if (movable is Fleet)
                return true;

            string movableOwner = movable.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(movableOwner))
                return false;

            return (destination as Fleet)?.GetOwnerInstanceID() == movableOwner
                || (destination as CapitalShip)?.GetOwnerInstanceID() == movableOwner;
        }

        /// <summary>
        /// Moves a unit to the nearest planet owned by its faction.
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

            Faction owner = _game.GetFactionByOwnerInstanceID(ownerID);
            Planet currentPlanet = ((ISceneNode)unit).GetParentOfType<Planet>();
            Planet fallback = owner?.GetNearestOwnedPlanetTo(unit.GetPosition(), currentPlanet);
            if (fallback != null)
            {
                ExecuteMove(unit, fallback);
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
        /// Redirects a unit when its destination is unavailable.
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

            Faction owner = _game.GetFactionByOwnerInstanceID(ownerID);
            Planet fallback = owner?.GetNearestOwnedPlanetTo(movable.GetPosition());

            if (fallback != null && fallback != rejectedDestination)
            {
                movable.Movement = null;
                ExecuteMove(movable, fallback);
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
        /// Reparents the unit to the destination and starts visual transit.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="destination">The target scene node to reparent into.</param>
        private void ExecuteMove(IMovable unit, ISceneNode destination)
        {
            if (
                !TryResolveAcceptedDestination(
                    unit,
                    destination,
                    out ISceneNode resolvedDestination
                )
            )
                return;

            destination = resolvedDestination;

            Planet destinationPlanet = RequireDestinationPlanet(destination);

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

            Point originPosition = unit.Movement?.CurrentPosition ?? originPlanet.GetPosition();
            int transitTicks = CalculateTransitTicks(unit, originPosition, destinationPlanet);

            if (((ISceneNode)unit).GetParent() == destination)
            {
                unit.Movement = null;
                return;
            }

            _game.MoveNode((ISceneNode)unit, destination);
            ClaimUncolonizedDestinationFromRegiment(unit, destinationPlanet);

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

            if (unit is IMissionParticipant missionParticipant && destination is Mission)
            {
                _pendingResults.Add(
                    new RoleEnrouteActiveResult
                    {
                        Participant = missionParticipant,
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
        /// Resolves a requested destination into the node that should receive the unit.
        /// </summary>
        /// <param name="unit">The unit being moved.</param>
        /// <param name="destination">The requested destination.</param>
        /// <returns>The node that should receive the unit, or null if none is available.</returns>
        private ISceneNode ResolveMoveDestination(IMovable unit, ISceneNode destination)
        {
            if (destination is Fleet targetFleet && !(unit is Fleet) && !(unit is CapitalShip))
                return ResolveFleetTarget(unit, targetFleet);

            return destination;
        }

        /// <summary>
        /// Changes the destination of an item that is still under construction.
        /// </summary>
        /// <param name="unit">The item being manufactured.</param>
        /// <param name="destination">The requested destination.</param>
        private void RetargetManufacturingDestination(IMovable unit, ISceneNode destination)
        {
            if (
                !TryResolveAcceptedDestination(
                    unit,
                    destination,
                    out ISceneNode resolvedDestination
                )
            )
                return;

            _game.MoveNode((ISceneNode)unit, resolvedDestination);
        }

        /// <summary>
        /// Resolves a destination and verifies that it can receive the unit.
        /// </summary>
        /// <param name="unit">The unit being moved.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="resolvedDestination">The resolved destination when accepted.</param>
        /// <returns>True if the destination can receive the unit.</returns>
        private bool TryResolveAcceptedDestination(
            IMovable unit,
            ISceneNode destination,
            out ISceneNode resolvedDestination
        )
        {
            resolvedDestination = ResolveMoveDestination(unit, destination);
            if (resolvedDestination == null)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: no capacity in {destination.GetDisplayName()} for {unit.GetDisplayName()}"
                );
                return false;
            }

            if (resolvedDestination.CanAcceptChild((ISceneNode)unit))
                return true;

            GameLogger.Warning(
                $"RequestMove rejected: {resolvedDestination.GetDisplayName()} cannot accept {unit.GetDisplayName()}"
            );
            return false;
        }

        /// <summary>
        /// Claims an uncolonized destination when a regiment becomes its only owner presence.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        private void ClaimUncolonizedDestinationFromRegiment(
            IMovable unit,
            Planet destinationPlanet
        )
        {
            if (unit is not Regiment regiment)
                return;

            if (destinationPlanet.IsColonized)
                return;

            if (!string.IsNullOrEmpty(destinationPlanet.GetOwnerInstanceID()))
                return;

            string ownerInstanceId = regiment.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerInstanceId))
                return;

            Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            if (faction == null)
                return;

            _game.ChangeUnitOwnership(destinationPlanet, ownerInstanceId);
            destinationPlanet.PopularSupport.Clear();

            foreach (Faction supportFaction in _game.GetFactions())
            {
                if (supportFaction.InstanceID == ownerInstanceId)
                    destinationPlanet.SetFullPopularSupport(supportFaction.InstanceID);
                else
                    destinationPlanet.SetPopularSupport(supportFaction.InstanceID, 0);
            }

            _fogOfWar.CapturePlanetSnapshotForAllFactions(destinationPlanet, _game.CurrentTick);

            _pendingResults.Add(
                new PlanetOwnershipChangedResult
                {
                    Planet = destinationPlanet,
                    PreviousOwner = null,
                    NewOwner = faction,
                    Tick = _game.CurrentTick,
                }
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
        /// Calculates transit time in ticks.
        /// </summary>
        /// <param name="unit">The unit whose hyperdrive rating determines speed.</param>
        /// <param name="originPos">The starting position.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>Number of ticks the transit will take.</returns>
        private int CalculateTransitTicks(IMovable unit, Point originPos, Planet destination)
        {
            double distance = destination.GetRawDistanceTo(originPos);

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

                speedBonus = fleet
                    .GetOfficers()
                    .Select(o => Math.Max(o.HyperdriveModifier, 0))
                    .DefaultIfEmpty(0)
                    .Max();
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

        /// <summary>
        /// Returns the planet that contains a movement destination.
        /// </summary>
        /// <param name="destination">The destination to resolve.</param>
        /// <returns>The planet containing the destination.</returns>
        private static Planet RequireDestinationPlanet(ISceneNode destination)
        {
            Planet destinationPlanet =
                destination as Planet ?? destination.GetParentOfType<Planet>();
            if (destinationPlanet != null)
                return destinationPlanet;

            throw new InvalidOperationException(
                $"Destination {destination.GetDisplayName()} is not at a planet location. "
                    + "All movement must resolve to a planet."
            );
        }
    }
}
