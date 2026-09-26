using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates movement eligibility, routes, destinations, and travel times without moving units.
    /// </summary>
    public class MovementQueries
    {
        private readonly GameRoot _game;
        private Func<Building, bool> _completedBuildingMovementPolicy;

        /// <summary>
        /// Creates movement queries for the authoritative game state.
        /// </summary>
        /// <param name="game">The game containing the units and destinations.</param>
        public MovementQueries(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Registers the domain policy that may authorize movement for a completed building.
        /// Completed buildings remain immobile when no policy is registered.
        /// </summary>
        /// <param name="policy">The completed-building movement policy.</param>
        internal void SetCompletedBuildingMovementPolicy(Func<Building, bool> policy)
        {
            _completedBuildingMovementPolicy =
                policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>
        /// Resolves a participant's recorded container, recorded planet, or nearest friendly planet.
        /// </summary>
        /// <param name="participant">The participant whose return destination is required.</param>
        /// <returns>The first valid return container, or null when none can receive the participant.</returns>
        internal ContainerNode ResolveMissionReturnDestination(IMissionParticipant participant)
        {
            Planet returnLocation = _game.GetSceneNodeByInstanceID<Planet>(
                participant.MissionReturnLocationInstanceID
            );
            ContainerNode returnParent = _game.GetSceneNodeByInstanceID<ContainerNode>(
                participant.MissionReturnParentInstanceID
            );

            Planet returnParentPlanet =
                returnParent as Planet ?? returnParent?.GetParentOfType<Planet>();
            if (
                returnParentPlanet?.IsDestroyed == false
                && returnParent.CanAcceptChild(participant)
            )
                return returnParent;

            if (returnLocation?.IsDestroyed == false)
            {
                if (returnLocation.CanAcceptChild(participant))
                    return returnLocation;
            }

            string ownerInstanceID = GetMovementControlOwner(participant);
            if (string.IsNullOrEmpty(ownerInstanceID))
                return null;

            Faction owner = _game.GetFactionByOwnerInstanceID(ownerInstanceID);
            Planet missionPlanet = participant.GetParentOfType<Planet>();
            return FindEvacuationDestinations(owner, participant, missionPlanet).FirstOrDefault();
        }

        /// <summary>
        /// Determines whether an owner-controlled fleet or capital-ship selection can accept one
        /// complete waypoint route without changing game state.
        /// </summary>
        /// <param name="items">The selected fleets, capital ships, or their visible snapshots.</param>
        /// <param name="waypointPlanetIds">The ordered destination planet identifiers.</param>
        /// <param name="ownerInstanceId">The faction authorized to command the fleets.</param>
        /// <returns>True when the complete route is valid.</returns>
        public bool CanSetFleetWaypointRoute(
            IReadOnlyList<ISceneNode> items,
            IReadOnlyList<string> waypointPlanetIds,
            string ownerInstanceId
        )
        {
            return TryResolveFleetWaypointRoute(
                    items,
                    waypointPlanetIds,
                    ownerInstanceId,
                    out _,
                    out _
                )
                || TryResolveCapitalShipWaypointRoute(
                    items,
                    waypointPlanetIds,
                    ownerInstanceId,
                    out _,
                    out _
                );
        }

        /// <summary>
        /// Estimates transit time for an owner-controlled selection without mutating it.
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
                || !TryResolveSelectionMoveGroup(
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
                IMovable liveUnit = ResolveLiveNode(unit) as IMovable;
                if (liveUnit == null)
                    return false;

                if (!CanReceiveMoveOrder(liveUnit))
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

                int unitTransitTicks =
                    IsManufacturingDestinationChange(liveUnit)
                    || ReferenceEquals(destinationPlanet, origin)
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

            if (
                unit is Starfighter
                && destinationPlanet.IsBlockadedFor(GetMovementControlOwner(unit))
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
        /// Estimates how long an in-transit unit would take if retargeted from its current position.
        /// </summary>
        /// <param name="unit">The in-transit unit to evaluate.</param>
        /// <param name="destination">The proposed retarget destination.</param>
        /// <param name="transitTicks">The estimated transit duration after retargeting.</param>
        /// <returns>True when the live route can be estimated.</returns>
        public bool TryEstimateRetargetedTransitTicks(
            IMovable unit,
            Planet destination,
            out int transitTicks
        )
        {
            transitTicks = 0;
            IMovable liveUnit = ResolveLiveNode(unit) as IMovable;
            Planet liveDestination = ResolveLiveNode(destination) as Planet;
            if (liveUnit?.Movement == null || liveDestination == null)
                return false;

            transitTicks = CalculateTransitTicks(
                liveUnit,
                liveUnit.Movement.CurrentPosition,
                liveDestination,
                sameSector: false
            );
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
        internal bool CanMoveGroup(List<IMovable> units, ContainerNode destination)
        {
            return TryResolveMoveGroupDestinations(units, destination, out _);
        }

        /// <summary>
        /// Resolves destinations for a movement group without mutating scene state.
        /// </summary>
        /// <param name="units">The units being validated together.</param>
        /// <param name="destination">The shared requested destination.</param>
        /// <param name="resolvedDestinations">The accepted destination for each unit in order.</param>
        /// <returns>True when every unit has an accepted destination.</returns>
        internal bool TryResolveMoveGroupDestinations(
            List<IMovable> units,
            ContainerNode destination,
            out List<ContainerNode> resolvedDestinations
        )
        {
            resolvedDestinations = new List<ContainerNode>();
            Dictionary<ContainerNode, List<ISceneNode>> reservedChildren =
                new Dictionary<ContainerNode, List<ISceneNode>>();
            Planet groupOrigin = null;
            foreach (IMovable unit in units)
            {
                if (unit == null)
                {
                    GameLogger.Warning("RequestMove rejected: group contains a null unit.");
                    return false;
                }

                if (!CanReceiveMoveOrder(unit))
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

                if (
                    !TryResolveAcceptedDestination(
                        unit,
                        destination,
                        reservedChildren,
                        out ContainerNode resolvedDestination
                    )
                )
                    return false;

                if (
                    !reservedChildren.TryGetValue(
                        resolvedDestination,
                        out List<ISceneNode> destinationChildren
                    )
                )
                {
                    destinationChildren = new List<ISceneNode>();
                    reservedChildren.Add(resolvedDestination, destinationChildren);
                }

                destinationChildren.Add(unit);
                resolvedDestinations.Add(resolvedDestination);
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
        /// Resolves a valid destination for every unit without mutating scene state.
        /// </summary>
        /// <param name="units">The units being placed together.</param>
        /// <param name="destination">The shared requested destination.</param>
        /// <param name="resolvedDestinations">The accepted destination for each unit in order.</param>
        /// <returns>True when every unit has an accepted destination.</returns>
        internal bool TryResolvePlacementGroupDestinations(
            IReadOnlyList<IMovable> units,
            ContainerNode destination,
            out List<ContainerNode> resolvedDestinations
        )
        {
            resolvedDestinations = new List<ContainerNode>();
            Dictionary<ContainerNode, List<ISceneNode>> reservedChildren =
                new Dictionary<ContainerNode, List<ISceneNode>>();
            foreach (IMovable unit in units)
            {
                if (
                    unit == null
                    || !TryResolveAcceptedDestination(
                        unit,
                        destination,
                        reservedChildren,
                        out ContainerNode resolvedDestination
                    )
                )
                    return false;

                if (
                    !reservedChildren.TryGetValue(
                        resolvedDestination,
                        out List<ISceneNode> children
                    )
                )
                {
                    children = new List<ISceneNode>();
                    reservedChildren.Add(resolvedDestination, children);
                }
                children.Add(unit);
                resolvedDestinations.Add(resolvedDestination);
            }
            return true;
        }

        /// <summary>
        /// Resolves and validates an owner-controlled selection before movement execution.
        /// </summary>
        /// <param name="items">The selected scene nodes or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to move the selection.</param>
        /// <param name="liveItems">Receives registered scene nodes in selection order.</param>
        /// <returns>True when the complete selection can receive movement orders.</returns>
        internal bool TryResolveControlledSelection(
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
                    || !CanReceiveMoveOrder(movable)
                )
                    return false;

                liveItems.Add(liveItem);
            }

            return true;
        }

        /// <summary>
        /// Resolves a selection containing only uniquely identified fleets controlled by one faction.
        /// </summary>
        /// <param name="items">The selected fleets or their snapshots.</param>
        /// <param name="ownerInstanceId">The required controlling faction.</param>
        /// <param name="fleets">Receives the registered fleets in selection order.</param>
        /// <returns>True when the complete selection resolves to controlled fleets.</returns>
        internal bool TryResolveControlledFleets(
            IReadOnlyList<ISceneNode> items,
            string ownerInstanceId,
            out List<Fleet> fleets
        )
        {
            fleets = new List<Fleet>();
            if (items == null || items.Count == 0 || string.IsNullOrEmpty(ownerInstanceId))
                return false;

            HashSet<string> instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ISceneNode item in items)
            {
                Fleet fleet = ResolveRegisteredNode(item) as Fleet;
                if (
                    fleet == null
                    || !instanceIds.Add(fleet.InstanceID)
                    || !string.Equals(
                        GetMovementControlOwner(fleet),
                        ownerInstanceId,
                        StringComparison.Ordinal
                    )
                )
                    return false;

                fleets.Add(fleet);
            }

            return true;
        }

        /// <summary>
        /// Validates and resolves one complete fleet waypoint route without mutating it.
        /// </summary>
        /// <param name="items">The selected fleets or their snapshots.</param>
        /// <param name="waypointPlanetIds">The ordered destination planet identifiers.</param>
        /// <param name="ownerInstanceId">The faction authorized to command the fleets.</param>
        /// <param name="fleets">Receives the registered controlled fleets.</param>
        /// <param name="destinations">Receives the registered destination planets.</param>
        /// <returns>True when the complete route is valid.</returns>
        internal bool TryResolveFleetWaypointRoute(
            IReadOnlyList<ISceneNode> items,
            IReadOnlyList<string> waypointPlanetIds,
            string ownerInstanceId,
            out List<Fleet> fleets,
            out List<Planet> destinations
        )
        {
            fleets = new List<Fleet>();
            destinations = new List<Planet>();
            if (
                !TryResolveControlledFleets(items, ownerInstanceId, out fleets)
                || !TryResolveWaypointDestinations(waypointPlanetIds, out destinations)
            )
                return false;

            bool fleetsAreMoving = fleets[0].Movement != null;
            if (
                fleets.Any(fleet =>
                    fleet.IsInCombat
                    || !fleet.HasOperationalCapitalShips()
                    || fleet.HasWaypoints()
                    || fleet.Movement != null != fleetsAreMoving
                    || fleet.GetParentOfType<Planet>() == null
                )
            )
                return false;

            Planet firstDestination = destinations[0];
            if (
                fleetsAreMoving
                && fleets.Any(fleet =>
                    ReferenceEquals(fleet.GetParentOfType<Planet>(), firstDestination)
                )
            )
                return false;
            if (
                !fleetsAreMoving
                && fleets.All(fleet =>
                    ReferenceEquals(fleet.GetParentOfType<Planet>(), firstDestination)
                )
            )
                return false;

            return true;
        }

        /// <summary>
        /// Validates and resolves a capital-ship waypoint route without changing fleet membership.
        /// </summary>
        /// <param name="items">The selected capital ships or their snapshots.</param>
        /// <param name="waypointPlanetIds">The ordered destination planet identifiers.</param>
        /// <param name="ownerInstanceId">The faction authorized to command the ships.</param>
        /// <param name="capitalShips">Receives the registered controlled capital ships.</param>
        /// <param name="destinations">Receives the registered destination planets.</param>
        /// <returns>True when the complete route is valid.</returns>
        internal bool TryResolveCapitalShipWaypointRoute(
            IReadOnlyList<ISceneNode> items,
            IReadOnlyList<string> waypointPlanetIds,
            string ownerInstanceId,
            out List<CapitalShip> capitalShips,
            out List<Planet> destinations
        )
        {
            capitalShips = new List<CapitalShip>();
            destinations = new List<Planet>();
            if (
                !TryResolveControlledSelection(
                    items,
                    ownerInstanceId,
                    out List<ISceneNode> liveItems
                )
                || liveItems.Any(item => item is not CapitalShip)
                || !TryResolveWaypointDestinations(waypointPlanetIds, out destinations)
            )
                return false;

            capitalShips = liveItems.Cast<CapitalShip>().ToList();
            Planet origin = capitalShips[0].GetParentOfType<Planet>();
            if (
                origin == null
                || capitalShips.Any(ship =>
                    !ReferenceEquals(ship.GetParentOfType<Planet>(), origin)
                )
                || ReferenceEquals(origin, destinations[0])
            )
                return false;

            return TryGetTransitTicks(
                capitalShips.Cast<IMovable>().ToList(),
                destinations[0],
                out _
            );
        }

        /// <summary>
        /// Resolves an ordered waypoint identifier list into live, valid destination planets.
        /// </summary>
        /// <param name="waypointPlanetIds">The ordered destination planet identifiers.</param>
        /// <param name="destinations">Receives the registered destination planets.</param>
        /// <returns>True when every destination is valid and consecutive stops are distinct.</returns>
        private bool TryResolveWaypointDestinations(
            IReadOnlyList<string> waypointPlanetIds,
            out List<Planet> destinations
        )
        {
            destinations = new List<Planet>();
            if (waypointPlanetIds == null || waypointPlanetIds.Count == 0)
                return false;

            string previousDestinationId = null;
            foreach (string waypointPlanetId in waypointPlanetIds)
            {
                Planet destination = _game.GetSceneNodeByInstanceID<Planet>(waypointPlanetId);
                if (
                    destination?.IsDestroyed != false
                    || string.Equals(
                        previousDestinationId,
                        destination.InstanceID,
                        StringComparison.Ordinal
                    )
                )
                    return false;

                destinations.Add(destination);
                previousDestinationId = destination.InstanceID;
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
        internal static bool TryResolveSelectionMoveGroup(
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
                    if (
                        ReferenceEquals(fleet, destinationFleet)
                        || fleet.GetChildren<CapitalShip>().Count == 0
                    )
                        return false;

                    sourceFleets.Add(fleet);
                    foreach (CapitalShip capitalShip in fleet.GetChildren<CapitalShip>())
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
        /// <returns>True if the unit can receive the order.</returns>
        internal bool CanReceiveMoveOrder(IMovable unit)
        {
            bool changesManufacturingDestination = IsManufacturingDestinationChange(unit);
            if (!changesManufacturingDestination && unit.GetTransitMovement() != null)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is already in transit."
                );
                return false;
            }

            if (
                IsCompletedBuilding(unit)
                && (
                    unit is not Building building
                    || _completedBuildingMovementPolicy?.Invoke(building) != true
                )
            )
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
        /// Returns whether an order changes a manufacturing destination instead of initiating
        /// physical travel. A fleet containing only unfinished ships represents their shared
        /// delivery destination and therefore follows the same rule as one unfinished item.
        /// </summary>
        /// <param name="unit">The unit or fleet receiving the order.</param>
        /// <returns>True when the order only retargets manufacturing delivery.</returns>
        internal static bool IsManufacturingDestinationChange(IMovable unit)
        {
            if (IsUnderConstruction(unit))
                return true;

            if (unit is not Fleet fleet)
                return false;

            IReadOnlyList<CapitalShip> ships = fleet.GetChildren<CapitalShip>();
            return ships.Count > 0 && ships.All(IsUnderConstruction);
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
        /// Returns the faction that controls movement and arrival visibility for a unit.
        /// </summary>
        /// <param name="movable">The unit whose controlling owner should be resolved.</param>
        /// <returns>The controlling owner instance id, or null when none is available.</returns>
        internal static string GetMovementControlOwner(IMovable movable)
        {
            if (
                movable is Officer { IsCaptured: true } capturedOfficer
                && !string.IsNullOrEmpty(capturedOfficer.CaptorInstanceID)
            )
                return capturedOfficer.CaptorInstanceID;

            return movable.GetOwnerInstanceID();
        }

        /// <summary>
        /// Returns whether a unit type requires an unobstructed destination.
        /// </summary>
        /// <param name="unit">The unit requesting access.</param>
        /// <param name="destinationPlanet">The planet receiving the unit.</param>
        /// <returns>True when an opposing blockade prevents the unit from entering.</returns>
        internal static bool IsBlockedFromDestinationByBlockade(
            IMovable unit,
            Planet destinationPlanet
        )
        {
            return unit is Starfighter or Regiment or Building
                && destinationPlanet.IsBlockadedFor(GetMovementControlOwner(unit));
        }

        /// <summary>
        /// Returns true when the unit type is allowed to finish movement at a hostile planet.
        /// </summary>
        /// <param name="movable">The unit completing movement.</param>
        /// <param name="destination">The destination container receiving the unit.</param>
        /// <returns>True if hostile arrival is a valid end state for this unit.</returns>
        internal static bool CanEnterHostileOrbit(IMovable movable, ContainerNode destination)
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
        /// Finds the nearest safe planet or stationary carrier that can receive an inbound unit.
        /// </summary>
        /// <param name="unit">The unit requiring a new destination.</param>
        /// <param name="blockadedPlanet">The destination that became blockaded.</param>
        /// <returns>The nearest valid destination, or null when none exists.</returns>
        internal ContainerNode FindBlockadeAutorouteDestination(
            IMovable unit,
            Planet blockadedPlanet
        )
        {
            string ownerInstanceID = GetMovementControlOwner(unit);
            if (string.IsNullOrEmpty(ownerInstanceID))
                return null;

            List<(ContainerNode Destination, Planet Planet)> candidates = _game
                .GetSceneNodesByType<Planet>()
                .Where(planet =>
                    planet != blockadedPlanet
                    && planet.GetOwnerInstanceID() == ownerInstanceID
                    && planet.IsColonized
                    && !planet.IsDestroyed
                    && !planet.IsBlockaded()
                    && planet.CanAcceptChild(unit)
                )
                .Select(planet => (Destination: (ContainerNode)planet, Planet: planet))
                .ToList();

            candidates.AddRange(
                _game
                    .GetSceneNodesByType<CapitalShip>()
                    .Where(ship =>
                        ship.GetOwnerInstanceID() == ownerInstanceID
                        && ship.ManufacturingStatus == ManufacturingStatus.Complete
                        && ((IMovable)ship).GetTransitMovement() == null
                        && ship.CanAcceptChild(unit)
                    )
                    .Select(ship => new
                    {
                        Destination = (ContainerNode)ship,
                        Planet = ship.GetParentOfType<Planet>(),
                    })
                    .Where(candidate => candidate.Planet?.IsDestroyed == false)
                    .Select(candidate =>
                        (Destination: candidate.Destination, Planet: candidate.Planet)
                    )
            );

            return candidates
                .OrderBy(candidate => candidate.Planet.GetRawDistanceTo(blockadedPlanet))
                .ThenBy(candidate => candidate.Destination is Planet ? 0 : 1)
                .ThenBy(candidate => candidate.Destination.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Destination)
                .FirstOrDefault();
        }

        /// <summary>
        /// Determines whether a unit has a valid friendly evacuation destination.
        /// </summary>
        /// <param name="unit">The unit that would evacuate.</param>
        /// <returns>True when at least one owned colonized planet can receive the unit.</returns>
        internal bool CanEvacuateToNearestFriendlyPlanet(IMovable unit)
        {
            if (unit == null)
                return false;
            if (!CanTravelBetweenPlanets(unit))
                return false;

            string ownerId = GetMovementControlOwner(unit);
            if (string.IsNullOrEmpty(ownerId))
                return false;

            Faction owner = _game.GetFactionByOwnerInstanceID(ownerId);
            Planet currentPlanet = unit.GetParentOfType<Planet>();
            return FindEvacuationDestinations(owner, unit, currentPlanet).Any();
        }

        /// <summary>
        /// Returns whether a unit can cross interplanetary space under its own power or as part
        /// of a fleet with at least one operational hyperdrive.
        /// </summary>
        /// <param name="unit">The unit attempting to travel.</param>
        /// <returns>True when the unit has a valid interplanetary movement source.</returns>
        internal static bool CanTravelBetweenPlanets(IMovable unit)
        {
            return unit switch
            {
                Starfighter fighter => fighter.Hyperdrive > 0,
                CapitalShip capitalShip => capitalShip.Hyperdrive > 0,
                Fleet fleet => fleet
                    .GetChildren<CapitalShip>()
                    .Any(ship =>
                        ship.ManufacturingStatus == ManufacturingStatus.Complete
                        && ship.Movement == null
                        && ship.CurrentHullStrength > 0
                        && ship.Hyperdrive > 0
                    ),
                _ => unit != null,
            };
        }

        /// <summary>
        /// Finds all valid colonized planets controlled by the unit's movement owner, ordered
        /// nearest first.
        /// </summary>
        /// <param name="owner">The faction controlling the unit's movement.</param>
        /// <param name="unit">The unit that must be accepted at the destination.</param>
        /// <param name="excludedPlanet">The current or rejected planet to exclude.</param>
        /// <returns>The valid evacuation destinations, nearest first.</returns>
        internal static IEnumerable<Planet> FindEvacuationDestinations(
            Faction owner,
            IMovable unit,
            Planet excludedPlanet
        )
        {
            return owner
                    ?.GetOwnedColonizedPlanets()
                    .Where(planet =>
                        planet != excludedPlanet
                        && !planet.IsDestroyed
                        && planet.GetOwnerInstanceID() == owner.InstanceID
                        && planet.CanAcceptChild(unit)
                    )
                    .OrderBy(planet => planet.GetRawDistanceTo(unit.GetPosition()))
                    .ThenBy(planet => planet.InstanceID)
                ?? Enumerable.Empty<Planet>();
        }

        /// <summary>
        /// Resolves a requested destination into the node that should receive the unit.
        /// </summary>
        /// <param name="unit">The unit being moved.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="reservedChildren">The children already reserved by the movement group.</param>
        /// <returns>The node that should receive the unit, or null if none is available.</returns>
        private ContainerNode ResolveMoveDestination(
            IMovable unit,
            ContainerNode destination,
            IReadOnlyDictionary<ContainerNode, List<ISceneNode>> reservedChildren
        )
        {
            if (destination is Fleet targetFleet && !(unit is Fleet) && !(unit is CapitalShip))
                return ResolveFleetTarget(unit, targetFleet, reservedChildren);

            return destination;
        }

        /// <summary>
        /// Resolves a destination and verifies that it can receive the unit.
        /// </summary>
        /// <param name="unit">The unit being moved.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="resolvedDestination">The resolved destination when accepted.</param>
        /// <returns>True if the destination can receive the unit.</returns>
        internal bool TryResolveAcceptedDestination(
            IMovable unit,
            ContainerNode destination,
            out ContainerNode resolvedDestination
        )
        {
            return TryResolveAcceptedDestination(unit, destination, null, out resolvedDestination);
        }

        /// <summary>
        /// Resolves a destination against children already reserved by the movement group.
        /// </summary>
        /// <param name="unit">The unit being moved.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="reservedChildren">The children already reserved by the movement group.</param>
        /// <param name="resolvedDestination">The resolved destination when accepted.</param>
        /// <returns>True when the destination can receive the unit.</returns>
        internal bool TryResolveAcceptedDestination(
            IMovable unit,
            ContainerNode destination,
            IReadOnlyDictionary<ContainerNode, List<ISceneNode>> reservedChildren,
            out ContainerNode resolvedDestination
        )
        {
            resolvedDestination = ResolveMoveDestination(unit, destination, reservedChildren);
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

            Planet destinationPlanet = RequireDestinationPlanet(resolvedDestination);
            if (IsBlockedFromDestinationByBlockade(unit, destinationPlanet))
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} cannot enter the enemy blockade at {destinationPlanet.GetDisplayName()}."
                );
                return false;
            }

            if (CanAcceptReservedChild(resolvedDestination, unit, reservedChildren))
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
        /// Resolves a Fleet destination to the appropriate CapitalShip for non-fleet units.
        /// </summary>
        /// <param name="unit">The non-fleet unit being assigned.</param>
        /// <param name="fleet">The fleet to find a suitable ship within.</param>
        /// <param name="reservedChildren">The children already reserved by the movement group.</param>
        /// <returns>The target ship, or null if no valid ship exists.</returns>
        private ContainerNode ResolveFleetTarget(
            IMovable unit,
            Fleet fleet,
            IReadOnlyDictionary<ContainerNode, List<ISceneNode>> reservedChildren
        )
        {
            if (unit is Starfighter)
            {
                if (fleet.Movement != null)
                    return null;

                return fleet
                    .GetChildren<CapitalShip>()
                    .FirstOrDefault(ship =>
                        ship.ManufacturingStatus == ManufacturingStatus.Complete
                        && ship.Movement == null
                        && CanAcceptReservedChild(ship, unit, reservedChildren)
                    );
            }

            if (unit is Regiment)
            {
                if (fleet.Movement != null)
                    return null;

                return fleet
                    .GetChildren<CapitalShip>()
                    .FirstOrDefault(ship =>
                        ship.ManufacturingStatus == ManufacturingStatus.Complete
                        && ship.Movement == null
                        && CanAcceptReservedChild(ship, unit, reservedChildren)
                    );
            }

            if (unit is Officer || unit is SpecialForces)
            {
                return fleet
                    .GetChildren<CapitalShip>()
                    .FirstOrDefault(ship =>
                        ship.ManufacturingStatus == ManufacturingStatus.Complete
                        && ship.Movement == null
                        && CanAcceptReservedChild(ship, unit, reservedChildren)
                    );
            }

            return null;
        }

        /// <summary>
        /// Returns whether a destination can accept a child after current group reservations.
        /// </summary>
        /// <param name="destination">The destination being evaluated.</param>
        /// <param name="child">The child proposed for the destination.</param>
        /// <param name="reservedChildren">The children already reserved by the movement group.</param>
        /// <returns>True when the destination has capacity for the proposed child.</returns>
        private static bool CanAcceptReservedChild(
            ContainerNode destination,
            ISceneNode child,
            IReadOnlyDictionary<ContainerNode, List<ISceneNode>> reservedChildren
        )
        {
            IReadOnlyCollection<ISceneNode> destinationChildren =
                reservedChildren != null
                && reservedChildren.TryGetValue(
                    destination,
                    out List<ISceneNode> existingDestinationChildren
                )
                    ? existingDestinationChildren
                    : Array.Empty<ISceneNode>();
            return destination.CanAcceptChild(child, destinationChildren);
        }

        /// <summary>
        /// Calculates movement duration from one planet to another.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="origin">The origin planet.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>The movement duration in ticks.</returns>
        internal int CalculateTransitTicks(IMovable unit, Planet origin, Planet destination)
        {
            return CalculateTransitTicks(
                unit,
                origin.GetPosition(),
                destination,
                IsSameSector(origin, destination)
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
        internal int CalculateTransitTicks(
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
                IsSameSector(origin, destination)
            );
        }

        /// <summary>
        /// Calculates movement duration using a caller-supplied local movement classification.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="originPos">The current movement origin position.</param>
        /// <param name="destination">The destination planet.</param>
        /// <param name="sameSector">Whether the movement remains within one planet sector.</param>
        /// <returns>The movement duration in ticks.</returns>
        internal int CalculateTransitTicks(
            IMovable unit,
            Point originPos,
            Planet destination,
            bool sameSector
        )
        {
            double distance = destination.GetRawDistanceTo(originPos);

            int slowestHyperdrive = Math.Max(GetMovementHyperdrive(unit), 1);

            int baseTicks = (int)
                Math.Ceiling(
                    distance * _game.GetConfig().Movement.DistanceScale / slowestHyperdrive
                );

            int minimumTransitTicks = sameSector
                ? _game.GetConfig().Movement.SameSectorMinTransitTicks
                : _game.GetConfig().Movement.MinTransitTicks;

            return Math.Max(baseTicks, minimumTransitTicks);
        }

        /// <summary>
        /// Returns the hyperdrive available to a strategic movement order.
        /// </summary>
        /// <param name="unit">The unit receiving the order.</param>
        /// <returns>The usable hyperdrive rating, or zero when the unit cannot travel.</returns>
        private int GetMovementHyperdrive(IMovable unit)
        {
            if (unit is Fleet fleet)
            {
                List<CapitalShip> completedShips = fleet
                    .GetChildren<CapitalShip>()
                    .Where(ship =>
                        ship.ManufacturingStatus == ManufacturingStatus.Complete
                        && ship.Movement == null
                    )
                    .ToList();
                if (completedShips.Count > 0)
                {
                    return completedShips
                        .Select(ship => ship.Hyperdrive)
                        .Where(hyperdrive => hyperdrive > 0)
                        .DefaultIfEmpty(1)
                        .Min();
                }
            }

            if (unit is CapitalShip capitalShip)
                return Math.Max(capitalShip.Hyperdrive, 1);

            return Math.Max(_game.GetConfig().Movement.DefaultFighterHyperdrive, 1);
        }

        /// <summary>
        /// Returns whether two planets belong to the same planet sector.
        /// </summary>
        /// <param name="origin">The origin planet.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>True if both planets share a parent planet sector; otherwise false.</returns>
        private static bool IsSameSector(Planet origin, Planet destination)
        {
            PlanetSector originSector = origin?.GetParentOfType<PlanetSector>();
            PlanetSector destinationSector = destination?.GetParentOfType<PlanetSector>();
            return originSector != null && ReferenceEquals(originSector, destinationSector);
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
        internal ISceneNode ResolveRegisteredNode(ISceneNode node)
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
        internal ContainerNode ResolveRegisteredContainer(ContainerNode node)
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
        internal ContainerNode ResolveLiveContainer(ContainerNode node)
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
        internal static Planet RequireDestinationPlanet(ContainerNode destination)
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
