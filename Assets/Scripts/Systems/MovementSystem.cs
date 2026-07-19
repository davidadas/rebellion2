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
        private readonly FleetSystem _fleetSystem;
        private readonly List<GameResult> _pendingResults = new List<GameResult>();

        /// <summary>
        /// Initializes a new instance of the MovementSystem class.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="fogOfWar">The fog of war system for capturing snapshots on arrival.</param>
        /// <param name="fleetSystem">Owns fleet formation and empty-fleet cleanup.</param>
        /// <param name="blockade">The blockade system for evacuation loss rolls.</param>
        public MovementSystem(
            GameRoot game,
            FogOfWarSystem fogOfWar,
            FleetSystem fleetSystem,
            BlockadeSystem blockade = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fogOfWar = fogOfWar ?? throw new ArgumentNullException(nameof(fogOfWar));
            _fleetSystem = fleetSystem ?? throw new ArgumentNullException(nameof(fleetSystem));
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
        /// <param name="destination">The target container to move toward.</param>
        public void RequestMove(IMovable unit, ContainerNode destination)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination = ResolveLiveContainer(destination);

            if (!CanReceiveMoveOrder(unit, allowManufacturingRetarget: true))
                return;

            if (IsUnderConstruction(unit))
            {
                RetargetManufacturingDestination(unit, destination);
                return;
            }

            if (unit is Officer { IsCaptured: true })
            {
                Planet originPlanet = unit.GetParentOfType<Planet>();
                Planet destinationPlanet =
                    destination as Planet ?? destination.GetParentOfType<Planet>();
                if (originPlanet != destinationPlanet)
                {
                    GameLogger.Warning(
                        $"RequestMove rejected: {unit.GetDisplayName()} is captured and cannot be ordered to move."
                    );
                    return;
                }
            }

            ExecuteMove(unit, destination);
        }

        /// <summary>
        /// Sets up visual transit for a manufactured unit that is already parented to its
        /// destination in the scene graph.
        /// </summary>
        /// <param name="unit">The unit to set in transit.</param>
        /// <param name="destination">The pre-assigned destination container.</param>
        /// <param name="origin">The production planet the unit departs from visually.</param>
        public void RequestMove(IMovable unit, ContainerNode destination, Planet origin)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (origin == null)
                throw new ArgumentNullException(nameof(origin));

            destination = ResolveLiveContainer(destination);

            Planet destinationPlanet = RequireDestinationPlanet(destination);

            if (
                destinationPlanet.GetOwnerInstanceID() != unit.GetOwnerInstanceID()
                && !CanEnterHostileOrbit(unit, destination)
            )
            {
                ExecuteMove(unit, origin);
                return;
            }

            if (destinationPlanet == origin)
            {
                unit.Movement = null;
                return;
            }

            int transitTicks = CalculateTransitTicks(unit, origin, destinationPlanet);
            unit.Movement = new MovementState
            {
                TransitTicks = transitTicks,
                TicksElapsed = 0,
                MovementGroupID = Guid.NewGuid().ToString("N"),
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
        /// <param name="destination">The shared target container.</param>
        public void RequestMove(List<IMovable> units, ContainerNode destination)
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            TryRequestMoveGroup(units, destination);
        }

        /// <summary>
        /// Validates and executes a player-controlled movement selection.
        /// </summary>
        /// <param name="items">The selected scene nodes or their snapshots.</param>
        /// <param name="destination">The requested destination or its snapshot.</param>
        /// <param name="ownerInstanceId">The faction authorized to move the selection.</param>
        /// <returns>True when the complete movement order was accepted.</returns>
        public bool TryRequestMove(
            IReadOnlyList<ISceneNode> items,
            ContainerNode destination,
            string ownerInstanceId
        )
        {
            ContainerNode liveDestination = ResolveRegisteredContainer(destination);
            if (
                liveDestination == null
                || !TryResolveControlledSelection(
                    items,
                    ownerInstanceId,
                    out List<ISceneNode> liveItems
                )
            )
                return false;

            Fleet createdDestinationFleet = null;
            if (liveDestination is Planet planet && liveItems.Any(item => item is CapitalShip))
            {
                createdDestinationFleet = _fleetSystem.CreateAtPlanet(planet, ownerInstanceId);
                if (createdDestinationFleet == null)
                    return false;

                liveDestination = createdDestinationFleet;
            }

            if (
                !TryBuildMoveGroup(
                    liveItems,
                    liveDestination,
                    ownerInstanceId,
                    out List<IMovable> movables,
                    out List<Fleet> sourceFleets
                )
            )
            {
                _fleetSystem.RemoveIfEmpty(createdDestinationFleet);
                return false;
            }

            bool accepted = TryRequestMoveGroup(movables, liveDestination);
            if (accepted)
            {
                foreach (Fleet sourceFleet in sourceFleets.Distinct())
                    _fleetSystem.RemoveIfEmpty(sourceFleet);
            }

            _fleetSystem.RemoveIfEmpty(createdDestinationFleet);
            return accepted;
        }

        /// <summary>
        /// Estimates transit time for a player-controlled selection without mutating it.
        /// </summary>
        /// <param name="items">The selected scene nodes or their snapshots.</param>
        /// <param name="destination">The requested destination or its snapshot.</param>
        /// <param name="ownerInstanceId">The faction authorized to move the selection.</param>
        /// <param name="transitTicks">Receives the maximum transit duration.</param>
        /// <returns>True when the complete movement order can be estimated.</returns>
        public bool TryGetSelectionTransitTicks(
            IReadOnlyList<ISceneNode> items,
            ContainerNode destination,
            string ownerInstanceId,
            out int transitTicks
        )
        {
            transitTicks = 0;
            ContainerNode liveDestination = ResolveRegisteredContainer(destination);
            if (
                liveDestination == null
                || !TryResolveControlledSelection(
                    items,
                    ownerInstanceId,
                    out List<ISceneNode> liveItems
                )
                || !TryBuildMoveGroup(
                    liveItems,
                    liveDestination,
                    ownerInstanceId,
                    out List<IMovable> movables,
                    out _
                )
            )
                return false;

            return TryGetTransitTicks(movables, liveDestination, out transitTicks);
        }

        /// <summary>
        /// Estimates the longest transit time for a group movement without changing scene state.
        /// </summary>
        /// <param name="units">The units that would receive the move order.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="transitTicks">The estimated movement duration when the estimate succeeds.</param>
        /// <returns>True if every unit can be evaluated for the destination; otherwise false.</returns>
        public bool TryGetTransitTicks(
            IReadOnlyList<IMovable> units,
            ContainerNode destination,
            out int transitTicks
        )
        {
            transitTicks = 0;
            if (units == null || units.Count == 0 || destination == null)
                return false;

            destination = ResolveLiveContainer(destination);
            int maxTransitTicks = 0;
            foreach (IMovable unit in units)
            {
                IMovable liveUnit = ResolveLiveNode(unit as ISceneNode) as IMovable;
                if (liveUnit == null)
                    return false;

                if (!CanReceiveMoveOrder(liveUnit, allowManufacturingRetarget: false))
                    return false;

                Planet origin = liveUnit.GetParentOfType<Planet>();
                if (origin == null)
                    return false;

                if (
                    !TryGetDestinationPlanetForTransit(
                        liveUnit,
                        destination,
                        out Planet destinationPlanet
                    )
                )
                    return false;

                int unitTransitTicks = ReferenceEquals(destinationPlanet, origin)
                    ? 0
                    : CalculateTransitTicks(liveUnit, origin, destinationPlanet);
                maxTransitTicks = Math.Max(maxTransitTicks, unitTransitTicks);
            }

            transitTicks = maxTransitTicks;
            return true;
        }

        /// <summary>
        /// Estimates delivery transit time for a manufactured unit without assigning movement state.
        /// </summary>
        /// <param name="unit">The manufactured unit to evaluate.</param>
        /// <param name="origin">The production planet.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="transitTicks">The estimated delivery duration when transit is required.</param>
        /// <returns>True if the destination can be evaluated; otherwise false.</returns>
        public bool TryEstimateManufacturedTransitTicks(
            IMovable unit,
            Planet origin,
            ContainerNode destination,
            out int transitTicks
        )
        {
            transitTicks = 0;
            if (unit == null || origin == null || destination == null)
                return false;

            destination = ResolveLiveContainer(destination);

            Planet destinationPlanet;
            try
            {
                destinationPlanet = RequireDestinationPlanet(destination);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (
                destinationPlanet.GetOwnerInstanceID() != unit.GetOwnerInstanceID()
                && !CanEnterHostileOrbit(unit, destination)
            )
            {
                return false;
            }

            if (destinationPlanet == origin)
                return true;

            transitTicks = CalculateTransitTicks(unit, origin, destinationPlanet);
            return true;
        }

        /// <summary>
        /// Resolves the planet used for transit calculations for a requested destination.
        /// </summary>
        /// <param name="unit">The unit being evaluated.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="destinationPlanet">The resolved destination planet.</param>
        /// <returns>True if a valid transit destination planet was found; otherwise false.</returns>
        private bool TryGetDestinationPlanetForTransit(
            IMovable unit,
            ContainerNode destination,
            out Planet destinationPlanet
        )
        {
            destinationPlanet = null;
            if (unit is CapitalShip && destination is Planet planet)
            {
                destinationPlanet = planet;
                return true;
            }

            if (TryResolveAcceptedDestination(unit, destination, out ContainerNode accepted))
            {
                destinationPlanet = RequireDestinationPlanet(accepted);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns whether every unit in a group can receive the move order.
        /// </summary>
        /// <param name="units">The units being moved together.</param>
        /// <param name="destination">The shared destination.</param>
        /// <returns>True if the whole group can move.</returns>
        private bool CanMoveGroup(List<IMovable> units, ContainerNode destination)
        {
            Planet groupOrigin = null;
            foreach (IMovable unit in units)
            {
                if (unit == null)
                {
                    GameLogger.Warning("RequestMove rejected: group contains a null unit.");
                    return false;
                }

                if (!CanReceiveMoveOrder(unit, allowManufacturingRetarget: false))
                    return false;

                Planet unitOrigin = unit.GetParentOfType<Planet>();
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
                if (HasEscortForCapturedOfficer(capturedOfficer, units))
                    continue;

                GameLogger.Warning(
                    $"RequestMove rejected: {capturedOfficer.GetDisplayName()} has no captor escort."
                );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Executes a validated movement group through the existing movement pipeline.
        /// </summary>
        /// <param name="units">The movable units in execution order.</param>
        /// <param name="destination">The shared destination.</param>
        /// <returns>True when the movement group was accepted.</returns>
        private bool TryRequestMoveGroup(List<IMovable> units, ContainerNode destination)
        {
            if (units == null || units.Count == 0 || destination == null)
                return false;

            destination = ResolveLiveContainer(destination);
            if (!CanMoveGroup(units, destination))
                return false;

            string movementGroupID = Guid.NewGuid().ToString("N");
            foreach (IMovable unit in units)
                ExecuteMove(unit, destination, movementGroupID);

            return true;
        }

        /// <summary>
        /// Resolves and validates a player-controlled selection before movement planning.
        /// </summary>
        /// <param name="items">The selected scene nodes or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to move the selection.</param>
        /// <param name="liveItems">Receives registered scene nodes in selection order.</param>
        /// <returns>True when the complete selection can receive movement orders.</returns>
        private bool TryResolveControlledSelection(
            IReadOnlyList<ISceneNode> items,
            string ownerInstanceId,
            out List<ISceneNode> liveItems
        )
        {
            liveItems = new List<ISceneNode>();
            if (items == null || items.Count == 0 || string.IsNullOrEmpty(ownerInstanceId))
                return false;

            HashSet<string> instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ISceneNode item in items)
            {
                ISceneNode liveItem = ResolveRegisteredNode(item);
                if (
                    liveItem is not IMovable movable
                    || !instanceIds.Add(liveItem.InstanceID)
                    || !string.Equals(
                        GetMovementControlOwner(movable),
                        ownerInstanceId,
                        StringComparison.Ordinal
                    )
                    || !CanReceiveMoveOrder(movable, allowManufacturingRetarget: false)
                )
                    return false;

                liveItems.Add(liveItem);
            }

            return true;
        }

        /// <summary>
        /// Expands selected fleets and records source fleets for post-move cleanup.
        /// </summary>
        /// <param name="items">The registered selected scene nodes.</param>
        /// <param name="destination">The registered movement destination.</param>
        /// <param name="ownerInstanceId">The faction authorized to move the selection.</param>
        /// <param name="movables">Receives the concrete units to move.</param>
        /// <param name="sourceFleets">Receives fleets that may become empty.</param>
        /// <returns>True when at least one unique movable was produced.</returns>
        private static bool TryBuildMoveGroup(
            IReadOnlyList<ISceneNode> items,
            ContainerNode destination,
            string ownerInstanceId,
            out List<IMovable> movables,
            out List<Fleet> sourceFleets
        )
        {
            movables = new List<IMovable>();
            sourceFleets = new List<Fleet>();
            HashSet<ISceneNode> movableNodes = new HashSet<ISceneNode>();
            Fleet destinationFleet = destination as Fleet;

            foreach (ISceneNode item in items)
            {
                if (item is Fleet fleet && destinationFleet != null)
                {
                    if (ReferenceEquals(fleet, destinationFleet) || fleet.CapitalShips.Count == 0)
                        return false;

                    sourceFleets.Add(fleet);
                    foreach (CapitalShip capitalShip in fleet.CapitalShips)
                    {
                        if (
                            !string.Equals(
                                capitalShip.GetOwnerInstanceID(),
                                ownerInstanceId,
                                StringComparison.Ordinal
                            ) || !movableNodes.Add(capitalShip)
                        )
                            return false;

                        movables.Add(capitalShip);
                    }

                    continue;
                }

                if (
                    item is not IMovable movable
                    || ReferenceEquals(item, destination)
                    || !movableNodes.Add(item)
                )
                    return false;

                if (
                    item is CapitalShip selectedCapitalShip
                    && selectedCapitalShip.GetParent() is Fleet sourceFleet
                )
                    sourceFleets.Add(sourceFleet);

                movables.Add(movable);
            }

            return movables.Count > 0;
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
        private static bool HasEscortForCapturedOfficer(
            Officer capturedOfficer,
            List<IMovable> units
        )
        {
            string captorId = capturedOfficer.CaptorInstanceID;
            return !string.IsNullOrEmpty(captorId)
                && units.Any(escort => CanEscortCapturedOfficer(escort, capturedOfficer));
        }

        /// <summary>
        /// Returns whether a unit can escort a specific captured officer during group movement.
        /// </summary>
        /// <param name="escort">The possible escort.</param>
        /// <param name="capturedOfficer">The captured officer that needs an escort.</param>
        /// <returns>True if the unit can escort the captured officer.</returns>
        private static bool CanEscortCapturedOfficer(IMovable escort, Officer capturedOfficer)
        {
            return !ReferenceEquals(escort, capturedOfficer)
                && escort.GetOwnerInstanceID() == capturedOfficer.CaptorInstanceID
                && (escort is SpecialForces || escort is Officer officer && !officer.IsCaptured);
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

            Planet destinationPlanet = movable.GetParentOfType<Planet>();
            if (destinationPlanet == null)
                throw new InvalidOperationException(
                    $"Unit {movable.GetDisplayName()} is in transit but has no parent planet."
                );

            ContainerNode destination =
                movable.GetParent() as ContainerNode
                ?? throw new InvalidOperationException(
                    $"Unit {movable.GetDisplayName()} is in transit but has no container destination."
                );

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
        /// <param name="destination">The destination container.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <param name="results">The results generated this tick.</param>
        private void CheckArrival(
            IMovable movable,
            ContainerNode destination,
            Planet destinationPlanet,
            List<GameResult> results
        )
        {
            string movementGroupID = movable.Movement?.MovementGroupID;

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
                AddArrivalResults(movable, destinationPlanet, movementGroupID, results);
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
        private bool TryFollowMovingFleetDestination(IMovable movable, ContainerNode destination)
        {
            Fleet movingFleet = destination is Fleet fleet
                ? fleet
                : (destination is CapitalShip ship ? ship.GetParent() as Fleet : null);
            if (movingFleet?.Movement == null)
                return false;

            Planet newDestination = movingFleet.GetParentOfType<Planet>();
            if (newDestination == null)
                return true;

            RetargetMovement(movable, newDestination);
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
        /// <param name="destination">The destination container.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <returns>True if the unit cannot complete arrival.</returns>
        private static bool HasArrivalOwnerConflict(
            IMovable movable,
            ContainerNode destination,
            Planet destinationPlanet
        )
        {
            string destinationOwner = destinationPlanet.GetOwnerInstanceID();
            string movableOwner = GetMovementControlOwner(movable);
            return !string.IsNullOrEmpty(destinationOwner)
                && destinationOwner != movableOwner
                && !CanEnterHostileOrbit(movable, destination);
        }

        /// <summary>
        /// Returns the faction that controls movement and arrival visibility for a unit.
        /// </summary>
        /// <param name="movable">The unit whose controlling owner should be resolved.</param>
        /// <returns>The controlling owner instance id, or null when none is available.</returns>
        private static string GetMovementControlOwner(IMovable movable)
        {
            if (
                movable is Officer { IsCaptured: true } capturedOfficer
                && !string.IsNullOrEmpty(capturedOfficer.CaptorInstanceID)
            )
                return capturedOfficer.CaptorInstanceID;

            return movable.GetOwnerInstanceID();
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

            _game.DetachNode(movable);
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
        /// <param name="destination">The destination container.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        private void CompleteArrival(
            IMovable movable,
            ContainerNode destination,
            Planet destinationPlanet
        )
        {
            _game.MoveNode(movable, destination);
            movable.Movement = null;
            GameLogger.Log($"{movable.GetDisplayName()} arrived at {destination.GetDisplayName()}");

            if (movable is Building && destination is Planet arrivalPlanet)
                arrivalPlanet.IsColonized = true;

            string arrivingOwner = GetMovementControlOwner(movable);
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
        /// <param name="movementGroupID">The movement order id that produced the arrival.</param>
        /// <param name="results">The results generated this tick.</param>
        private void AddArrivalResults(
            IMovable movable,
            Planet destinationPlanet,
            string movementGroupID,
            List<GameResult> results
        )
        {
            results.Add(
                new GameObjectEnrouteActiveResult
                {
                    GameObject = movable,
                    IsActive = false,
                    Tick = _game.CurrentTick,
                }
            );
            results.Add(
                new UnitArrivedResult
                {
                    Unit = movable,
                    Destination = destinationPlanet,
                    MovementGroupID = movementGroupID,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Returns true when the unit type is allowed to finish movement at a hostile planet.
        /// </summary>
        /// <param name="movable">The unit completing movement.</param>
        /// <param name="destination">The destination container receiving the unit.</param>
        /// <returns>True if hostile arrival is a valid end state for this unit.</returns>
        private static bool CanEnterHostileOrbit(IMovable movable, ContainerNode destination)
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
            Planet currentPlanet = unit.GetParentOfType<Planet>();
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
        /// <param name="destination">The target container to reparent into.</param>
        /// <param name="movementGroupID">The shared movement order id for grouped moves.</param>
        private void ExecuteMove(
            IMovable unit,
            ContainerNode destination,
            string movementGroupID = null
        )
        {
            movementGroupID ??= Guid.NewGuid().ToString("N");
            destination = ResolveLiveContainer(destination);

            if (
                !TryResolveAcceptedDestination(
                    unit,
                    destination,
                    out ContainerNode resolvedDestination
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
            int transitTicks = CalculateTransitTicks(
                unit,
                originPosition,
                originPlanet,
                destinationPlanet
            );

            if (unit.GetParent() == destination)
            {
                unit.Movement = null;
                return;
            }

            if (destinationPlanet == originPlanet)
            {
                _game.MoveNode(unit, destination);
                ClaimUncolonizedDestinationFromRegiment(unit, destinationPlanet);
                unit.Movement = null;
                return;
            }

            _game.MoveNode(unit, destination);
            ClaimUncolonizedDestinationFromRegiment(unit, destinationPlanet);

            unit.Movement = new MovementState
            {
                TransitTicks = transitTicks,
                TicksElapsed = 0,
                MovementGroupID = movementGroupID,
                OriginPosition = originPosition,
                CurrentPosition = originPosition,
            };

            if (unit is Fleet movingFleet)
                RetargetInTransitFleetJoiners(movingFleet, destinationPlanet);

            _pendingResults.Add(
                new GameObjectEnrouteResult { GameObject = unit, Tick = _game.CurrentTick }
            );
            _pendingResults.Add(
                new GameObjectEnrouteActiveResult
                {
                    GameObject = unit,
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
        /// Retargets units that are already moving to join a fleet after the fleet receives a new destination.
        /// </summary>
        /// <param name="fleet">The fleet whose inbound units should be retargeted.</param>
        /// <param name="destinationPlanet">The fleet's new destination planet.</param>
        private void RetargetInTransitFleetJoiners(Fleet fleet, Planet destinationPlanet)
        {
            foreach (
                IMovable joiner in fleet.GetChildren<IMovable>(movable => movable.Movement != null)
            )
                RetargetMovement(joiner, destinationPlanet);
        }

        /// <summary>
        /// Replaces a unit's active movement state with a route from its current position to a new destination.
        /// </summary>
        /// <param name="movable">The moving unit to retarget.</param>
        /// <param name="destinationPlanet">The new destination planet.</param>
        private void RetargetMovement(IMovable movable, Planet destinationPlanet)
        {
            Point currentPosition = movable.Movement.CurrentPosition;
            string movementGroupID = movable.Movement.MovementGroupID;
            movable.Movement = new MovementState
            {
                TransitTicks = CalculateTransitTicks(
                    movable,
                    currentPosition,
                    destinationPlanet,
                    sameSystem: false
                ),
                TicksElapsed = 0,
                MovementGroupID = movementGroupID,
                OriginPosition = currentPosition,
                CurrentPosition = currentPosition,
            };
        }

        /// <summary>
        /// Resolves a requested destination into the node that should receive the unit.
        /// </summary>
        /// <param name="unit">The unit being moved.</param>
        /// <param name="destination">The requested destination.</param>
        /// <returns>The node that should receive the unit, or null if none is available.</returns>
        private ContainerNode ResolveMoveDestination(IMovable unit, ContainerNode destination)
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
        private void RetargetManufacturingDestination(IMovable unit, ContainerNode destination)
        {
            if (
                !TryResolveAcceptedDestination(
                    unit,
                    destination,
                    out ContainerNode resolvedDestination
                )
            )
                return;

            _game.MoveNode(unit, resolvedDestination);
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
            ContainerNode destination,
            out ContainerNode resolvedDestination
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

            if (!CanMoveToUncolonizedPlanet(unit, resolvedDestination))
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} cannot land at uncolonized {resolvedDestination.GetDisplayName()}."
                );
                return false;
            }

            if (resolvedDestination.CanAcceptChild(unit))
                return true;

            GameLogger.Warning(
                $"RequestMove rejected: {resolvedDestination.GetDisplayName()} cannot accept {unit.GetDisplayName()}"
            );
            return false;
        }

        /// <summary>
        /// Checks whether a move can place the unit on an uncolonized planet.
        /// </summary>
        /// <param name="unit">The unit being moved.</param>
        /// <param name="destination">The requested destination.</param>
        /// <returns>True if the move is allowed for the uncolonized planet rule.</returns>
        private static bool CanMoveToUncolonizedPlanet(IMovable unit, ContainerNode destination)
        {
            if (unit.GetParent() == destination)
                return true;

            if (destination is not Planet destinationPlanet || destinationPlanet.IsColonized)
                return true;

            if (unit is Fleet)
                return true;

            if (unit is not Regiment)
                return false;

            Fleet originFleet = unit.GetParentOfType<Fleet>();
            return originFleet?.GetParentOfType<Planet>() == destinationPlanet;
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
        private ContainerNode ResolveFleetTarget(IMovable unit, Fleet fleet)
        {
            if (unit is Starfighter)
                return fleet.FindShipForStarfighter();
            if (unit is Regiment)
                return fleet.FindShipForRegiment();
            if ((unit is Officer || unit is SpecialForces) && fleet.CapitalShips.Count > 0)
                return fleet.CapitalShips[0];
            return null;
        }

        /// <summary>
        /// Calculates movement duration from one planet to another.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="origin">The origin planet.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>The movement duration in ticks.</returns>
        private int CalculateTransitTicks(IMovable unit, Planet origin, Planet destination)
        {
            return CalculateTransitTicks(
                unit,
                origin.GetPosition(),
                destination,
                IsSameSystem(origin, destination)
            );
        }

        /// <summary>
        /// Calculates movement duration from a current position with a known origin planet.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="originPos">The current movement origin position.</param>
        /// <param name="origin">The origin planet used for local movement rules.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>The movement duration in ticks.</returns>
        private int CalculateTransitTicks(
            IMovable unit,
            Point originPos,
            Planet origin,
            Planet destination
        )
        {
            return CalculateTransitTicks(
                unit,
                originPos,
                destination,
                IsSameSystem(origin, destination)
            );
        }

        /// <summary>
        /// Calculates movement duration using a caller-supplied local movement classification.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="originPos">The current movement origin position.</param>
        /// <param name="destination">The destination planet.</param>
        /// <param name="sameSystem">Whether the movement remains within one planet system.</param>
        /// <returns>The movement duration in ticks.</returns>
        private int CalculateTransitTicks(
            IMovable unit,
            Point originPos,
            Planet destination,
            bool sameSystem
        )
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
                    distance * _game.GetConfig().Movement.DistanceScale / slowestHyperdrive
                );

            int minimumTransitTicks = sameSystem
                ? _game.GetConfig().Movement.SameSystemMinTransitTicks
                : _game.GetConfig().Movement.MinTransitTicks;

            return Math.Max(baseTicks - speedBonus, minimumTransitTicks);
        }

        /// <summary>
        /// Returns whether two planets belong to the same planet system.
        /// </summary>
        /// <param name="origin">The origin planet.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>True if both planets share a parent planet system; otherwise false.</returns>
        private static bool IsSameSystem(Planet origin, Planet destination)
        {
            PlanetSystem originSystem = origin?.GetParentOfType<PlanetSystem>();
            PlanetSystem destinationSystem = destination?.GetParentOfType<PlanetSystem>();
            return originSystem != null && ReferenceEquals(originSystem, destinationSystem);
        }

        /// <summary>
        /// Resolves a possibly copied scene node to the live scene node when available.
        /// </summary>
        /// <param name="node">The scene node to resolve.</param>
        /// <returns>The live scene node with the same instance ID, or the supplied node.</returns>
        private ISceneNode ResolveLiveNode(ISceneNode node)
        {
            if (node == null)
                return null;

            return _game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID) ?? node;
        }

        /// <summary>
        /// Resolves a scene-node snapshot only when it is registered in the active game.
        /// </summary>
        /// <param name="node">The scene node to resolve.</param>
        /// <returns>The registered node, or null.</returns>
        private ISceneNode ResolveRegisteredNode(ISceneNode node)
        {
            return string.IsNullOrEmpty(node?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID);
        }

        /// <summary>
        /// Resolves a container snapshot only when it is registered in the active game.
        /// </summary>
        /// <param name="node">The container to resolve.</param>
        /// <returns>The registered container, or null.</returns>
        private ContainerNode ResolveRegisteredContainer(ContainerNode node)
        {
            return string.IsNullOrEmpty(node?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<ContainerNode>(node.InstanceID);
        }

        /// <summary>
        /// Resolves a possibly copied container to the live scene container when available.
        /// </summary>
        /// <param name="node">The container to resolve.</param>
        /// <returns>The live container with the same instance ID, or the supplied container.</returns>
        private ContainerNode ResolveLiveContainer(ContainerNode node)
        {
            if (node == null)
                return null;

            return _game.GetSceneNodeByInstanceID<ContainerNode>(node.InstanceID) ?? node;
        }

        /// <summary>
        /// Returns the planet that contains a movement destination.
        /// </summary>
        /// <param name="destination">The destination to resolve.</param>
        /// <returns>The planet containing the destination.</returns>
        private static Planet RequireDestinationPlanet(ContainerNode destination)
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
