using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Executes movement orders, custody transfers, arrivals, and evacuation while owning queued movement results.
    /// </summary>
    public class MovementCommands
    {
        private readonly GameRoot _game;
        private readonly FogOfWarCommands _fogOfWar;
        private readonly FogOfWarQueries _fogOfWarQueries;
        private readonly BlockadeCommands _blockade;
        private readonly FleetCommands _fleetSystem;
        private readonly MovementQueries _queries;
        private readonly List<GameResult> _pendingResults = new List<GameResult>();

        /// <summary>
        /// Raised after an immediate movement command produces results.
        /// </summary>
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Initializes a new instance of the MovementCommands class.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="fogOfWar">The commands for capturing snapshots on arrival.</param>
        /// <param name="fleetSystem">Owns fleet formation and empty-fleet cleanup.</param>
        /// <param name="fogOfWarQueries">The visibility rules for arrival observations.</param>
        /// <param name="queries">The shared movement eligibility and destination rules.</param>
        /// <param name="blockade">The blockade system for evacuation loss rolls.</param>
        public MovementCommands(
            GameRoot game,
            FogOfWarCommands fogOfWar,
            FleetCommands fleetSystem,
            FogOfWarQueries fogOfWarQueries,
            MovementQueries queries,
            BlockadeCommands blockade = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fogOfWar = fogOfWar ?? throw new ArgumentNullException(nameof(fogOfWar));
            _fleetSystem = fleetSystem ?? throw new ArgumentNullException(nameof(fleetSystem));
            _blockade = blockade;
            _fogOfWarQueries =
                fogOfWarQueries ?? throw new ArgumentNullException(nameof(fogOfWarQueries));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Returns and clears results queued by immediate movement operations.
        /// </summary>
        /// <returns>The pending movement results.</returns>
        internal List<GameResult> TakePendingResults()
        {
            List<GameResult> results = new List<GameResult>(_pendingResults);
            _pendingResults.Clear();
            return results;
        }

        /// <summary>
        /// Attempts to move a complete unit group to the first destination that accepts it.
        /// </summary>
        /// <param name="units">The units that must move together.</param>
        /// <param name="destinations">Candidate destinations in preference order.</param>
        /// <param name="sourceEventInstanceID">The event requesting movement, when applicable.</param>
        /// <param name="reactions">The result collection receiving accepted movement outcomes.</param>
        /// <returns>True when a destination accepted and received the movement request.</returns>
        public bool TryRequestMove(
            IReadOnlyList<IMovable> units,
            IReadOnlyList<ContainerNode> destinations,
            string sourceEventInstanceID,
            List<GameResult> reactions
        )
        {
            if (units == null || units.Count == 0 || destinations == null)
                return false;

            foreach (
                ContainerNode destination in destinations.Where(candidate => candidate != null)
            )
            {
                if (
                    TryExecuteMoveGroupClearingWaypoints(
                        units.ToList(),
                        destination,
                        reactions,
                        sourceEventInstanceID
                    )
                )
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Places a complete unit group at the first accepting destination without transit.
        /// </summary>
        /// <param name="units">The units that must be placed together.</param>
        /// <param name="destinations">Candidate destinations in preference order.</param>
        /// <returns>True when a destination accepts the complete group.</returns>
        public bool TryPlaceUnits(List<IMovable> units, IReadOnlyList<ContainerNode> destinations)
        {
            if (units == null || destinations == null)
                return false;

            foreach (ContainerNode candidate in destinations)
            {
                if (TryPlaceGroup(units, candidate))
                    return true;
            }

            return false;
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
            TryRequestMove(unit, destination);
        }

        /// <summary>
        /// Attempts to move one unit through normal movement validation.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="destination">The requested destination.</param>
        /// <returns>True when the movement request was accepted.</returns>
        internal bool TryRequestMove(IMovable unit, ContainerNode destination)
        {
            return TryRequestMove(unit, destination, sourceEventInstanceID: null);
        }

        /// <summary>
        /// Establishes a captured officer's custody in a captor-controlled container.
        /// </summary>
        /// <param name="officer">The captured officer to transfer.</param>
        /// <param name="destination">The captor-controlled ship or planet receiving the officer.</param>
        /// <param name="escort">The captor unit accompanying a remote transfer, if one exists.</param>
        /// <param name="results">The collection receiving movement results.</param>
        /// <returns>True when custody is established or the officer is already there.</returns>
        internal bool TryEstablishCapturedOfficerCustody(
            Officer officer,
            ContainerNode destination,
            IMovable escort,
            ICollection<GameResult> results
        )
        {
            if (officer == null)
                throw new ArgumentNullException(nameof(officer));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (!officer.IsCaptured || string.IsNullOrEmpty(officer.CaptorInstanceID))
                return false;

            destination = _queries.ResolveLiveContainer(destination);
            if (
                !_queries.TryResolveAcceptedDestination(
                    officer,
                    destination,
                    out ContainerNode resolvedDestination
                )
                || !string.Equals(
                    resolvedDestination.GetOwnerInstanceID(),
                    officer.CaptorInstanceID,
                    StringComparison.Ordinal
                )
            )
                return false;

            if (ReferenceEquals(officer.GetParent(), resolvedDestination))
                return true;

            Planet originPlanet = officer.GetParentOfType<Planet>();
            Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(
                resolvedDestination
            );
            if (!officer.IsActive() || ReferenceEquals(originPlanet, destinationPlanet))
            {
                officer.Movement = null;
                _game.MoveNode(officer, resolvedDestination);
                return true;
            }

            if (escort == null)
            {
                officer.Movement = null;
                _game.MoveNode(officer, resolvedDestination);
                return true;
            }

            return TryExecuteMoveGroup(
                new List<IMovable> { escort, officer },
                resolvedDestination,
                results
            );
        }

        /// <summary>
        /// Requests move.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="destination">The movement destination.</param>
        /// <param name="sourceEventInstanceID">The originating event identifier.</param>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
        private bool TryRequestMove(
            IMovable unit,
            ContainerNode destination,
            string sourceEventInstanceID
        )
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination = _queries.ResolveLiveContainer(destination);

            if (!_queries.CanReceiveMoveOrder(unit))
                return false;

            if (MovementQueries.IsManufacturingDestinationChange(unit))
            {
                return TryRetargetManufacturingDestination(unit, destination);
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
                    return false;
                }
            }

            Planet requestedPlanet = MovementQueries.RequireDestinationPlanet(destination);
            if (MovementQueries.IsBlockedFromDestinationByBlockade(unit, requestedPlanet))
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} cannot enter the enemy blockade at {requestedPlanet.GetDisplayName()}."
                );
                return false;
            }

            bool moved = ExecuteMove(
                unit,
                destination,
                _pendingResults,
                sourceEventInstanceID: sourceEventInstanceID
            );
            if (moved && unit is Fleet fleet)
                fleet.Waypoints.Clear();
            return moved;
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

            destination = _queries.ResolveLiveContainer(destination);

            Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(destination);

            if (
                destinationPlanet.GetOwnerInstanceID() != unit.GetOwnerInstanceID()
                && !MovementQueries.CanEnterHostileOrbit(unit, destination)
            )
            {
                ExecuteMove(unit, origin, _pendingResults);
                return;
            }

            if (destinationPlanet == origin)
            {
                unit.Movement = null;
                CompleteManufacturingDelivery(unit);
                AddPlanetGarrisonChangedResults(_pendingResults, unit, destinationPlanet);
                return;
            }

            int transitTicks = _queries.CalculateTransitTicks(unit, origin, destinationPlanet);
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
            RequestMove(units, destination, null);
        }

        /// <summary>
        /// Requests move.
        /// </summary>
        /// <param name="units">The units to move.</param>
        /// <param name="destination">The movement destination.</param>
        /// <param name="sourceEventInstanceID">The originating event identifier.</param>
        private void RequestMove(
            List<IMovable> units,
            ContainerNode destination,
            string sourceEventInstanceID
        )
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            TryExecuteMoveGroupClearingWaypoints(
                units,
                destination,
                _pendingResults,
                sourceEventInstanceID
            );
        }

        /// <summary>
        /// Records a participant's current container and planet before sending it to a mission.
        /// </summary>
        /// <param name="participant">The participant departing for the mission.</param>
        /// <param name="mission">The mission that will contain the participant.</param>
        internal void SendToMission(IMissionParticipant participant, Mission mission)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));

            participant.MissionReturnParentInstanceID = participant.GetParent()?.InstanceID;
            participant.MissionReturnLocationInstanceID = participant
                .GetParentOfType<Planet>()
                ?.InstanceID;
            RequestMove(participant, mission);
        }

        /// <summary>
        /// Returns mission participants and passengers in destination-specific movement groups.
        /// </summary>
        /// <param name="participants">The mission participants that are free to return.</param>
        /// <param name="additionalPassengers">Additional units that must travel with the first return group.</param>
        /// <returns>Units that could not be assigned to a return destination.</returns>
        internal List<IMovable> ReturnFromMission(
            IReadOnlyList<IMissionParticipant> participants,
            IReadOnlyList<IMovable> additionalPassengers
        )
        {
            if (participants == null)
                throw new ArgumentNullException(nameof(participants));
            if (additionalPassengers == null)
                throw new ArgumentNullException(nameof(additionalPassengers));

            Dictionary<ContainerNode, List<IMovable>> returnGroups =
                new Dictionary<ContainerNode, List<IMovable>>();
            HashSet<IMovable> returningUnits = new HashSet<IMovable>();
            List<IMovable> strandedUnits = new List<IMovable>();

            foreach (IMissionParticipant participant in participants)
            {
                if (participant == null)
                    continue;

                if (!returningUnits.Add(participant))
                    continue;

                ContainerNode destination = _queries.ResolveMissionReturnDestination(participant);
                if (destination == null)
                {
                    strandedUnits.Add(participant);
                    continue;
                }

                AddToReturnGroup(returnGroups, destination, participant);
            }

            Dictionary<IMovable, MovementState> interruptedMovements = returningUnits
                .Where(unit => unit.Movement != null)
                .ToDictionary(unit => unit, unit => unit.Movement);
            foreach (IMovable unit in interruptedMovements.Keys)
                unit.Movement = null;

            foreach (
                KeyValuePair<ContainerNode, List<IMovable>> returnGroup in returnGroups.ToList()
            )
            {
                if (!_queries.CanMoveGroup(returnGroup.Value, returnGroup.Key))
                {
                    strandedUnits.AddRange(returnGroup.Value);
                    returnGroups.Remove(returnGroup.Key);
                }
            }

            ContainerNode passengerDestination = returnGroups.Keys.FirstOrDefault();
            List<IMovable> passengers = additionalPassengers
                .Where(passenger => passenger != null && returningUnits.Add(passenger))
                .ToList();
            if (passengerDestination == null)
            {
                strandedUnits.AddRange(passengers);
            }
            else if (passengers.Count > 0)
            {
                List<IMovable> passengerGroup = returnGroups[passengerDestination]
                    .Concat(passengers)
                    .ToList();
                if (_queries.CanMoveGroup(passengerGroup, passengerDestination))
                    returnGroups[passengerDestination] = passengerGroup;
                else
                    strandedUnits.AddRange(passengers);
            }

            RestoreMovement(interruptedMovements);
            foreach (KeyValuePair<ContainerNode, List<IMovable>> returnGroup in returnGroups)
            {
                string movementGroupID = Guid.NewGuid().ToString("N");
                foreach (IMovable unit in returnGroup.Value)
                    ExecuteMove(unit, returnGroup.Key, _pendingResults, movementGroupID);
            }

            return strandedUnits.Distinct().ToList();
        }

        /// <summary>
        /// Places mission participants at the mission planet without initiating return travel.
        /// </summary>
        /// <param name="participants">The participants remaining at the mission location.</param>
        /// <param name="missionPlanet">The planet where the mission ended.</param>
        /// <returns>Participants that could not be placed at the mission planet.</returns>
        internal List<IMovable> CompleteMissionAtLocation(
            IReadOnlyList<IMissionParticipant> participants,
            Planet missionPlanet
        )
        {
            if (participants == null)
                throw new ArgumentNullException(nameof(participants));

            List<IMovable> strandedUnits = new List<IMovable>();
            foreach (IMissionParticipant participant in participants)
            {
                if (
                    participant == null
                    || missionPlanet?.IsDestroyed != false
                    || !missionPlanet.CanAcceptChild(participant)
                )
                {
                    if (participant != null)
                        strandedUnits.Add(participant);
                    continue;
                }

                _game.MoveNode(participant, missionPlanet);
                participant.Movement = null;
            }

            return strandedUnits;
        }

        /// <summary>
        /// Restores movement states temporarily cleared during return-group validation.
        /// </summary>
        /// <param name="interruptedMovements">The units and movement states to restore.</param>
        private static void RestoreMovement(
            IReadOnlyDictionary<IMovable, MovementState> interruptedMovements
        )
        {
            foreach (KeyValuePair<IMovable, MovementState> movement in interruptedMovements)
                movement.Key.Movement = movement.Value;
        }

        /// <summary>
        /// Adds a unit to the movement group for a return destination.
        /// </summary>
        /// <param name="returnGroups">The return groups keyed by destination.</param>
        /// <param name="destination">The destination that identifies the group.</param>
        /// <param name="unit">The unit to add.</param>
        private static void AddToReturnGroup(
            IDictionary<ContainerNode, List<IMovable>> returnGroups,
            ContainerNode destination,
            IMovable unit
        )
        {
            if (!returnGroups.TryGetValue(destination, out List<IMovable> group))
            {
                group = new List<IMovable>();
                returnGroups.Add(destination, group);
            }

            group.Add(unit);
        }

        /// <summary>
        /// Validates and executes an owner-controlled movement selection.
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
            bool accepted = TryExecuteSelectionMove(
                items,
                destination,
                ownerInstanceId,
                out _,
                out List<GameResult> results
            );
            if (accepted)
                ResultsProduced?.Invoke(results);
            return accepted;
        }

        /// <summary>
        /// Validates and executes an owner-controlled selection without publishing its results.
        /// </summary>
        /// <param name="items">The selected scene nodes or their snapshots.</param>
        /// <param name="destination">The requested destination or its snapshot.</param>
        /// <param name="ownerInstanceId">The faction authorized to move the selection.</param>
        /// <param name="createdDestinationFleet">Receives a fleet created for capital ships.</param>
        /// <param name="results">Receives the movement results produced by the accepted order.</param>
        /// <returns>True when the complete movement order was accepted.</returns>
        private bool TryExecuteSelectionMove(
            IReadOnlyList<ISceneNode> items,
            ContainerNode destination,
            string ownerInstanceId,
            out Fleet createdDestinationFleet,
            out List<GameResult> results
        )
        {
            createdDestinationFleet = null;
            results = new List<GameResult>();
            ContainerNode liveDestination = _queries.ResolveRegisteredContainer(destination);
            if (
                liveDestination == null
                || !_queries.TryResolveControlledSelection(
                    items,
                    ownerInstanceId,
                    out List<ISceneNode> liveItems
                )
            )
                return false;

            if (liveDestination is Planet planet && liveItems.Any(item => item is CapitalShip))
            {
                createdDestinationFleet = _fleetSystem.CreateAtPlanet(planet, ownerInstanceId);
                if (createdDestinationFleet == null)
                    return false;

                liveDestination = createdDestinationFleet;
            }

            if (
                !MovementQueries.TryResolveSelectionMoveGroup(
                    liveItems,
                    liveDestination,
                    ownerInstanceId,
                    out List<IMovable> movables,
                    out List<Fleet> sourceFleets
                )
            )
            {
                _fleetSystem.RemoveIfEmpty(createdDestinationFleet, "transfer");
                return false;
            }

            bool accepted = TryExecuteMoveGroupClearingWaypoints(
                movables,
                liveDestination,
                results
            );
            if (accepted)
            {
                foreach (Fleet sourceFleet in sourceFleets.Distinct())
                    _fleetSystem.RemoveIfEmpty(sourceFleet, "transfer");
            }

            _fleetSystem.RemoveIfEmpty(createdDestinationFleet, "transfer");
            return accepted;
        }

        /// <summary>
        /// Commits one complete waypoint route and starts its first leg.
        /// </summary>
        /// <param name="items">The selected fleets, capital ships, or their visible snapshots.</param>
        /// <param name="waypointPlanetIds">The ordered destination planet identifiers.</param>
        /// <param name="ownerInstanceId">The faction authorized to command the fleets.</param>
        /// <returns>True when the route was committed to every selected fleet.</returns>
        public bool TrySetFleetWaypointRoute(
            IReadOnlyList<ISceneNode> items,
            IReadOnlyList<string> waypointPlanetIds,
            string ownerInstanceId
        )
        {
            if (
                _queries.TryResolveCapitalShipWaypointRoute(
                    items,
                    waypointPlanetIds,
                    ownerInstanceId,
                    out List<CapitalShip> capitalShips,
                    out List<Planet> capitalShipDestinations
                )
            )
            {
                bool moveAccepted = TryExecuteSelectionMove(
                    capitalShips.Cast<ISceneNode>().ToList(),
                    capitalShipDestinations[0],
                    ownerInstanceId,
                    out Fleet routeFleet,
                    out List<GameResult> capitalShipResults
                );
                if (!moveAccepted)
                    return false;

                if (routeFleet?.GetParent() != null)
                    routeFleet.Waypoints.AddRange(waypointPlanetIds);
                ResultsProduced?.Invoke(capitalShipResults);
                return true;
            }

            if (
                !_queries.TryResolveFleetWaypointRoute(
                    items,
                    waypointPlanetIds,
                    ownerInstanceId,
                    out List<Fleet> fleets,
                    out List<Planet> destinations
                )
            )
                return false;

            bool startsFirstLeg = fleets[0].Movement == null;
            foreach (Fleet fleet in fleets)
            {
                if (fleet.Movement != null)
                    fleet.Waypoints.Add(fleet.GetParentOfType<Planet>().InstanceID);

                fleet.Waypoints.AddRange(waypointPlanetIds);
            }

            if (!startsFirstLeg)
                return true;

            List<GameResult> results = new List<GameResult>();
            bool accepted = TryExecuteMoveGroup(
                fleets.Cast<IMovable>().ToList(),
                destinations[0],
                results
            );
            if (!accepted)
            {
                foreach (Fleet fleet in fleets)
                    fleet.Waypoints.Clear();
                return false;
            }

            ResultsProduced?.Invoke(results);
            return true;
        }

        /// <summary>
        /// Clears queued waypoint continuation without changing an active movement leg.
        /// </summary>
        /// <param name="items">The selected fleets or their visible snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to command the fleets.</param>
        /// <returns>True when at least one waypoint was removed.</returns>
        public bool ClearFleetWaypoints(IReadOnlyList<ISceneNode> items, string ownerInstanceId)
        {
            if (
                !_queries.TryResolveControlledFleets(items, ownerInstanceId, out List<Fleet> fleets)
            )
                return false;

            bool cleared = false;
            foreach (Fleet fleet in fleets)
            {
                if (!fleet.HasWaypoints())
                    continue;

                fleet.Waypoints.Clear();
                cleared = true;
            }

            return cleared;
        }

        /// <summary>
        /// Starts the next queued fleet legs after the combat phase has had an opportunity to
        /// interrupt newly arrived fleets.
        /// </summary>
        /// <returns>The movement results produced by accepted waypoint legs.</returns>
        internal List<GameResult> ContinueFleetWaypointRoutes()
        {
            List<GameResult> results = new List<GameResult>();
            List<Fleet> fleets = _game.GetSceneNodesByType<Fleet>().ToList();
            foreach (Fleet fleet in fleets)
            {
                if (fleet == null)
                    continue;

                if (HasPendingCapitalShipsAtCurrentWaypoint(fleet))
                    continue;

                if (!fleet.HasOperationalCapitalShips())
                {
                    fleet.Waypoints.Clear();
                    continue;
                }

                if (fleet.Movement != null || fleet.IsInCombat || !fleet.HasWaypoints())
                    continue;

                AdvanceFleetWaypointRoute(fleet, results);
            }

            return results;
        }

        /// <summary>
        /// Executes a movement group and clears any existing fleet waypoint routes.
        /// </summary>
        /// <param name="units">The movable units in execution order.</param>
        /// <param name="destination">The shared destination.</param>
        /// <param name="results">The collection receiving movement results.</param>
        /// <param name="sourceEventInstanceID">The event that requested the movement, if any.</param>
        /// <returns>True when the movement group was accepted.</returns>
        private bool TryExecuteMoveGroupClearingWaypoints(
            List<IMovable> units,
            ContainerNode destination,
            ICollection<GameResult> results,
            string sourceEventInstanceID = null
        )
        {
            if (!TryExecuteMoveGroup(units, destination, results, sourceEventInstanceID))
                return false;

            foreach (Fleet fleet in units.OfType<Fleet>())
                fleet.Waypoints.Clear();

            return true;
        }

        /// <summary>
        /// Validates and executes a movement group without applying command-specific route policy.
        /// </summary>
        /// <param name="units">The movable units in execution order.</param>
        /// <param name="destination">The shared destination.</param>
        /// <param name="results">The collection receiving movement results.</param>
        /// <param name="sourceEventInstanceID">The event that requested the movement, if any.</param>
        /// <returns>True when the movement group was accepted.</returns>
        private bool TryExecuteMoveGroup(
            List<IMovable> units,
            ContainerNode destination,
            ICollection<GameResult> results,
            string sourceEventInstanceID = null
        )
        {
            if (units == null || units.Count == 0 || destination == null || results == null)
                return false;

            destination = _queries.ResolveLiveContainer(destination);
            if (
                !_queries.TryResolveMoveGroupDestinations(
                    units,
                    destination,
                    out List<ContainerNode> destinations
                )
            )
                return false;

            string movementGroupID = Guid.NewGuid().ToString("N");
            for (int index = 0; index < units.Count; index++)
            {
                IMovable unit = units[index];
                ContainerNode resolvedDestination = destinations[index];
                if (MovementQueries.IsManufacturingDestinationChange(unit))
                    ApplyManufacturingDestination(unit, resolvedDestination);
                else
                    ExecuteAcceptedMove(
                        unit,
                        resolvedDestination,
                        results,
                        movementGroupID,
                        sourceEventInstanceID
                    );
            }

            return true;
        }

        /// <summary>
        /// Places a complete group immediately after validating every destination reservation.
        /// </summary>
        /// <param name="units">The units to place together.</param>
        /// <param name="destination">The shared requested destination.</param>
        /// <returns>True when the complete group was placed.</returns>
        private bool TryPlaceGroup(List<IMovable> units, ContainerNode destination)
        {
            if (units == null || units.Count == 0 || destination == null)
                return false;

            destination = _queries.ResolveLiveContainer(destination);
            List<IMovable> liveUnits = new List<IMovable>();
            HashSet<string> instanceIDs = new HashSet<string>(StringComparer.Ordinal);
            foreach (IMovable unit in units)
            {
                if (unit is not ISceneNode node || !instanceIDs.Add(node.InstanceID))
                    return false;

                ISceneNode registered = _queries.ResolveRegisteredNode(node);
                if (registered is IMovable live)
                {
                    if (!node.IsActive())
                        return false;
                    liveUnits.Add(live);
                    continue;
                }

                if (node.GetParent() != null)
                    return false;
                liveUnits.Add(unit);
            }

            if (
                !_queries.TryResolvePlacementGroupDestinations(
                    liveUnits,
                    destination,
                    out List<ContainerNode> destinations
                )
            )
                return false;

            for (int index = 0; index < liveUnits.Count; index++)
            {
                ISceneNode unit = liveUnits[index];
                ContainerNode resolvedDestination = destinations[index];
                if (unit.GetParent() == null)
                {
                    if (_game.NodesByInstanceID.ContainsKey(unit.InstanceID))
                        return false;
                    _game.AttachNode(unit, resolvedDestination);
                }
                else
                    _game.MoveNode(unit, resolvedDestination);
                liveUnits[index].Movement = null;
            }

            return true;
        }

        /// <summary>
        /// Advances the movement of a single unit by one tick and handles arrival.
        /// </summary>
        /// <param name="movable">The movable unit to update.</param>
        /// <param name="results">The results generated this tick.</param>
        internal void UpdateMovement(IMovable movable, List<GameResult> results)
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
            string sourceEventInstanceID = movable.Movement?.SourceEventInstanceID;
            bool completesManufacturingDelivery =
                movable is IManufacturable { ManufacturingStatus: ManufacturingStatus.Delivering };

            if (TryFollowMovingFleetDestination(movable, destination))
                return;

            if (destination is Mission)
            {
                CompleteMissionParticipantArrival(movable);
                AddArrivalResults(
                    movable,
                    destinationPlanet,
                    movementGroupID,
                    results,
                    sourceEventInstanceID
                );
                return;
            }

            if (TryRejectBlockadedArrival(movable, destinationPlanet, results))
                return;

            if (HasArrivalOwnerConflict(movable, destination, destinationPlanet))
            {
                RejectArrivalAtChangedOwner(movable, destinationPlanet, results);
                return;
            }

            try
            {
                CompleteArrival(movable, destination, destinationPlanet, results);
                if (completesManufacturingDelivery)
                    CompleteManufacturingDelivery(movable);
                AddArrivalResults(
                    movable,
                    destinationPlanet,
                    movementGroupID,
                    results,
                    sourceEventInstanceID,
                    completesManufacturingDelivery
                );
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
        private void CompleteMissionParticipantArrival(IMovable movable)
        {
            movable.Movement = null;
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
            string movableOwner = MovementQueries.GetMovementControlOwner(movable);
            return !string.IsNullOrEmpty(destinationOwner)
                && destinationOwner != movableOwner
                && !MovementQueries.CanEnterHostileOrbit(movable, destination);
        }

        /// <summary>
        /// Destroys an arriving building or regiment when an opposing fleet still blockades its destination.
        /// </summary>
        /// <param name="movable">The reinforcement completing transit.</param>
        /// <param name="destinationPlanet">The planet receiving the reinforcement.</param>
        /// <param name="results">The collection receiving the destruction result.</param>
        /// <returns>True when the reinforcement was destroyed; otherwise false.</returns>
        private bool TryRejectBlockadedArrival(
            IMovable movable,
            Planet destinationPlanet,
            ICollection<GameResult> results
        )
        {
            if (movable is not Building && movable is not Regiment)
                return false;

            string movableOwner = MovementQueries.GetMovementControlOwner(movable);
            if (!destinationPlanet.IsBlockadedFor(movableOwner))
                return false;

            _game.DeleteNode(movable);
            GameLogger.Log(
                $"{movable.GetDisplayName()} destroyed on arrival at blockaded {destinationPlanet.GetDisplayName()}."
            );
            results.Add(
                new GameObjectDestroyedOnArrivalResult
                {
                    DestroyedObject = movable,
                    Context = destinationPlanet,
                    Tick = _game.CurrentTick,
                }
            );
            return true;
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

            _game.DeleteNode(movable);
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
        /// <param name="results">The collection receiving deployment results.</param>
        private void CompleteArrival(
            IMovable movable,
            ContainerNode destination,
            Planet destinationPlanet,
            ICollection<GameResult> results
        )
        {
            _game.MoveNode(movable, destination);
            movable.Movement = null;
            GameLogger.Log($"{movable.GetDisplayName()} arrived at {destination.GetDisplayName()}");

            if (movable is Building && destination is Planet arrivalPlanet)
                arrivalPlanet.IsColonized = true;

            string arrivingOwner = MovementQueries.GetMovementControlOwner(movable);
            if (!string.IsNullOrEmpty(arrivingOwner))
                destinationPlanet.AddVisitor(arrivingOwner);

            AddPlanetGarrisonChangedResults(results, movable, destinationPlanet);

            if (movable is Fleet fleet)
            {
                if (ConsumeReachedFleetWaypoint(fleet, destinationPlanet))
                {
                    results.Add(
                        new FleetWaypointsCompletedResult
                        {
                            Fleet = fleet,
                            Destination = destinationPlanet,
                            Tick = _game.CurrentTick,
                        }
                    );
                }
                CaptureFleetArrivalSnapshot(fleet, destinationPlanet);
            }
        }

        /// <summary>
        /// Removes the active waypoint after its fleet successfully reaches that planet.
        /// </summary>
        /// <param name="fleet">The arriving fleet.</param>
        /// <param name="destinationPlanet">The planet that accepted the arrival.</param>
        /// <returns>True when the consumed waypoint completed the assigned route.</returns>
        private static bool ConsumeReachedFleetWaypoint(Fleet fleet, Planet destinationPlanet)
        {
            if (
                fleet?.HasWaypoints() != true
                || destinationPlanet == null
                || !string.Equals(
                    fleet.Waypoints[0],
                    destinationPlanet.InstanceID,
                    StringComparison.Ordinal
                )
            )
                return false;

            fleet.Waypoints.RemoveAt(0);
            return !fleet.HasWaypoints();
        }

        /// <summary>
        /// Advances one stationary fleet to its next valid waypoint.
        /// </summary>
        /// <param name="fleet">The fleet whose route should continue.</param>
        /// <param name="results">The result collection receiving route advancement results.</param>
        private void AdvanceFleetWaypointRoute(Fleet fleet, List<GameResult> results)
        {
            int waypointCount = fleet.Waypoints.Count;
            for (int index = 0; index < waypointCount; index++)
            {
                string waypointId = fleet.Waypoints[0];
                Planet destination = _game.GetSceneNodeByInstanceID<Planet>(waypointId);
                if (destination?.IsDestroyed != false)
                {
                    fleet.Waypoints.RemoveAt(0);
                    continue;
                }

                if (ReferenceEquals(fleet.GetParentOfType<Planet>(), destination))
                {
                    fleet.Waypoints.RemoveAt(0);
                    if (!fleet.HasWaypoints())
                    {
                        results.Add(
                            new FleetWaypointsCompletedResult
                            {
                                Fleet = fleet,
                                Destination = destination,
                                Tick = _game.CurrentTick,
                            }
                        );
                        return;
                    }

                    continue;
                }

                bool accepted = TryExecuteMoveGroup(
                    new List<IMovable> { fleet },
                    destination,
                    results
                );
                if (!accepted)
                    fleet.Waypoints.Clear();
                return;
            }
        }

        /// <summary>
        /// Returns whether a fleet has capital ships still reaching or completing at its current
        /// waypoint before the fleet may continue.
        /// </summary>
        /// <param name="fleet">The fleet that owns the committed route.</param>
        /// <returns>True when at least one capital ship is still moving or under construction.</returns>
        private static bool HasPendingCapitalShipsAtCurrentWaypoint(Fleet fleet)
        {
            if (fleet?.HasWaypoints() != true)
                return false;

            Planet currentPlanet = fleet.GetParentOfType<Planet>();
            if (
                currentPlanet == null
                || !string.Equals(
                    fleet.Waypoints[0],
                    currentPlanet.InstanceID,
                    StringComparison.Ordinal
                )
            )
                return false;

            return fleet
                .GetChildren<CapitalShip>()
                .Any(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Building
                    || ship.Movement != null
                );
        }

        /// <summary>
        /// Captures fog-of-war state for an arriving fleet if visible.
        /// </summary>
        /// <param name="fleet">The arriving fleet.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        private void CaptureFleetArrivalSnapshot(Fleet fleet, Planet destinationPlanet)
        {
            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(f => f.InstanceID == fleet.OwnerInstanceID);
            if (faction == null || !_fogOfWarQueries.IsPlanetVisible(destinationPlanet, faction))
                return;

            PlanetSector sector = destinationPlanet.GetParentOfType<PlanetSector>();
            if (sector != null)
                _fogOfWar.CaptureSnapshot(faction, destinationPlanet, sector, _game.CurrentTick);
        }

        /// <summary>
        /// Adds the standard arrival results for a unit.
        /// </summary>
        /// <param name="movable">The arriving unit.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <param name="movementGroupID">The movement order id that produced the arrival.</param>
        /// <param name="results">The results generated this tick.</param>
        /// <param name="sourceEventInstanceID">The event that requested the movement, if any.</param>
        /// <param name="completedManufacturingDelivery">Whether the arrival completed a manufactured delivery.</param>
        private void AddArrivalResults(
            IMovable movable,
            Planet destinationPlanet,
            string movementGroupID,
            ICollection<GameResult> results,
            string sourceEventInstanceID = null,
            bool completedManufacturingDelivery = false
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
                    SourceEventInstanceID = sourceEventInstanceID,
                    Tick = _game.CurrentTick,
                }
            );
            if (completedManufacturingDelivery)
            {
                results.Add(
                    new GameObjectDeployedResult { GameObject = movable, Tick = _game.CurrentTick }
                );
            }
        }

        /// <summary>
        /// Marks a manufactured item as complete after its initial delivery finishes.
        /// </summary>
        /// <param name="movable">The delivered unit.</param>
        private static void CompleteManufacturingDelivery(IMovable movable)
        {
            if (
                movable is IManufacturable
                {
                    ManufacturingStatus: ManufacturingStatus.Delivering
                } manufacturable
            )
            {
                manufacturable.ManufacturingStatus = ManufacturingStatus.Complete;
            }
        }

        /// <summary>
        /// Resolves every independently moving unit headed toward a newly blockaded planet.
        /// </summary>
        /// <param name="result">The blockade-start result containing the planet and blockader.</param>
        /// <param name="reactions">The collection receiving generated results.</param>
        internal void HandleBlockadeStarted(
            BlockadeChangedResult result,
            ICollection<GameResult> reactions
        )
        {
            string blockadingOwner = result.BlockadingFleet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(blockadingOwner))
                return;

            List<IMovable> inboundUnits = result
                .Planet.GetChildren<IMovable>(recursive: true)
                .Where(unit => unit.Movement != null)
                .Where(unit => unit.GetOwnerInstanceID() != blockadingOwner)
                .ToList();

            foreach (IMovable unit in inboundUnits)
            {
                if (unit is Building)
                {
                    DestroyBlockadeInboundUnit(unit, result.Planet, reactions);
                    continue;
                }

                if (!ShouldAutorouteFromBlockade(unit))
                    continue;

                ContainerNode destination = _queries.FindBlockadeAutorouteDestination(
                    unit,
                    result.Planet
                );
                if (destination == null)
                {
                    DestroyBlockadeInboundUnit(unit, result.Planet, reactions);
                    continue;
                }

                Planet destinationPlanet =
                    destination as Planet ?? destination.GetParentOfType<Planet>();
                if (destinationPlanet == null)
                    continue;

                _game.MoveNode(unit, destination);
                RetargetMovement(unit, destinationPlanet);
                reactions.Add(
                    new GameObjectEnrouteResult { GameObject = unit, Tick = _game.CurrentTick }
                );
            }
        }

        /// <summary>
        /// Returns whether an independently moving unit must seek another destination.
        /// </summary>
        /// <param name="unit">The inbound unit to evaluate.</param>
        /// <returns>True when the unit must be autorouted.</returns>
        private static bool ShouldAutorouteFromBlockade(IMovable unit)
        {
            return unit is Starfighter
                || unit is Regiment
                || unit is SpecialForces specialForces && !specialForces.IsOnMission();
        }

        /// <summary>
        /// Removes an inbound unit and records its destruction at the blockaded planet.
        /// </summary>
        /// <param name="unit">The unit to destroy.</param>
        /// <param name="blockadedPlanet">The destination responsible for the destruction.</param>
        /// <param name="reactions">The collection receiving the destruction result.</param>
        private void DestroyBlockadeInboundUnit(
            IMovable unit,
            Planet blockadedPlanet,
            ICollection<GameResult> reactions
        )
        {
            _game.DeleteNode(unit);
            reactions.Add(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = unit,
                    Context = blockadedPlanet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Destroys a unit that cannot remain at a planet whose ownership changed and records
        /// its destruction.
        /// </summary>
        /// <param name="unit">The unit to destroy.</param>
        /// <param name="planet">The planet responsible for the destruction.</param>
        public void DestroyEvictedUnit(IMovable unit, Planet planet)
        {
            GameLogger.Log(
                $"{unit.GetDisplayName()} was destroyed when {planet.GetDisplayName()} changed hands."
            );
            _game.DeleteNode(unit);
            _pendingResults.Add(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = unit,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Moves a unit to the nearest planet owned by its faction that accepts it. When every
        /// destination refuses the unit and <paramref name="evictingOwnerInstanceID"/> is set,
        /// stranded starfighters and regiments are destroyed and stranded officers are captured
        /// by the evicting faction; all other units remain in place.
        /// </summary>
        /// <param name="unit">The unit to evacuate.</param>
        /// <param name="evictingOwnerInstanceID">The faction claiming the planet, when evicting.</param>
        public void EvacuateToNearestFriendlyPlanet(
            IMovable unit,
            string evictingOwnerInstanceID = null
        )
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            if (!MovementQueries.CanTravelBetweenPlanets(unit))
            {
                unit.Movement = null;
                GameLogger.Warning(
                    $"{unit.GetDisplayName()} has no hyperdrive or carrier and cannot evacuate."
                );
                return;
            }

            string ownerID = MovementQueries.GetMovementControlOwner(unit);
            if (string.IsNullOrEmpty(ownerID))
            {
                unit.Movement = null;
                GameLogger.Warning($"{unit.GetDisplayName()} has no owner — cannot evacuate.");
                return;
            }

            Faction owner = _game.GetFactionByOwnerInstanceID(ownerID);
            Planet currentPlanet = unit.GetParentOfType<Planet>();
            foreach (
                Planet fallback in MovementQueries.FindEvacuationDestinations(
                    owner,
                    unit,
                    currentPlanet
                )
            )
            {
                if (ExecuteMove(unit, fallback, _pendingResults))
                    return;
            }

            unit.Movement = null;
            if (!string.IsNullOrEmpty(evictingOwnerInstanceID))
            {
                if (unit is Officer officer)
                {
                    CaptureStrandedOfficer(officer, currentPlanet, evictingOwnerInstanceID);
                    return;
                }

                if (unit is Starfighter or Regiment)
                {
                    DestroyEvictedUnit(unit, currentPlanet);
                    return;
                }
            }

            GameLogger.Warning($"{unit.GetDisplayName()} has no friendly planet to evacuate to.");
        }

        /// <summary>
        /// Captures an officer stranded on a planet claimed by an enemy faction.
        /// </summary>
        /// <param name="officer">The stranded officer.</param>
        /// <param name="planet">The planet the officer is stranded on.</param>
        /// <param name="captorInstanceID">The instance ID of the capturing faction.</param>
        private void CaptureStrandedOfficer(Officer officer, Planet planet, string captorInstanceID)
        {
            if (!officer.TryCapture(captorInstanceID))
                return;

            _pendingResults.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    ParentAtCapture = officer.GetParent(),
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
            GameLogger.Log(
                $"{officer.GetDisplayName()} was captured when {planet.GetDisplayName()} changed hands."
            );
        }

        /// <summary>
        /// Relocates units to a compatible ship in their current fleet or, when none can accept
        /// them, sends them toward the nearest eligible planet owned by their movement controller.
        /// </summary>
        /// <param name="units">The units to relocate from their current containers.</param>
        public void RelocateUnits(IEnumerable<IMovable> units)
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));

            foreach (
                IMovable unit in units
                    .Where(unit => unit != null)
                    .OrderBy(unit => unit is Starfighter fighter && fighter.Hyperdrive <= 0 ? 0 : 1)
                    .ToList()
            )
            {
                ISceneNode node = unit;
                CapitalShip currentShip = node?.GetParentOfType<CapitalShip>();
                Fleet fleet = node?.GetParentOfType<Fleet>();
                List<CapitalShip> availableShips = (
                    fleet?.GetChildren<CapitalShip>() ?? Array.Empty<CapitalShip>()
                )
                    .Where(ship =>
                        ship != currentShip
                        && ship.ManufacturingStatus == ManufacturingStatus.Complete
                        && ship.Movement == null
                        && ship.CurrentHullStrength > 0
                    )
                    .ToList();
                CapitalShip destination = availableShips.FirstOrDefault(ship =>
                    ship.CanAcceptChild(node)
                );
                Starfighter independentlyMobileOccupant = null;
                if (
                    destination == null
                    && unit is Starfighter starfighter
                    && starfighter.Hyperdrive <= 0
                )
                {
                    foreach (CapitalShip availableShip in availableShips)
                    {
                        independentlyMobileOccupant = availableShip
                            .GetChildren<Starfighter>()
                            .FirstOrDefault(fighter =>
                                fighter.ManufacturingStatus == ManufacturingStatus.Complete
                                && fighter.Movement == null
                                && fighter.Hyperdrive > 0
                                && _queries.CanEvacuateToNearestFriendlyPlanet(fighter)
                            );
                        if (independentlyMobileOccupant == null)
                            continue;

                        destination = availableShip;
                        break;
                    }
                }

                if (independentlyMobileOccupant != null)
                    EvacuateToNearestFriendlyPlanet(independentlyMobileOccupant);

                if (destination?.CanAcceptChild(node) == true)
                    _game.MoveNode(node, destination);
                else if (MovementQueries.CanTravelBetweenPlanets(unit))
                    EvacuateToNearestFriendlyPlanet(unit);
            }
        }

        /// <summary>
        /// Redirects a unit when its destination is unavailable.
        /// </summary>
        /// <param name="movable">The unit whose arrival was rejected.</param>
        /// <param name="rejectedDestination">The planet that refused the unit.</param>
        private void HandleArrivalRejection(IMovable movable, Planet rejectedDestination)
        {
            string ownerID = MovementQueries.GetMovementControlOwner(movable);
            if (string.IsNullOrEmpty(ownerID))
            {
                movable.Movement = null;
                GameLogger.Warning(
                    $"{movable.GetDisplayName()} has no owner, cannot find fallback."
                );
                return;
            }

            Faction owner = _game.GetFactionByOwnerInstanceID(ownerID);
            Planet fallback = MovementQueries
                .FindEvacuationDestinations(owner, movable, rejectedDestination)
                .FirstOrDefault();

            if (fallback != null)
            {
                movable.Movement = null;
                ExecuteMove(movable, fallback, _pendingResults);
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
        /// <param name="results">The collection receiving movement results.</param>
        /// <param name="movementGroupID">The shared movement order id for grouped moves.</param>
        /// <param name="sourceEventInstanceID">The event that requested the movement, if any.</param>
        /// <returns>True when the movement order was accepted; otherwise false.</returns>
        private bool ExecuteMove(
            IMovable unit,
            ContainerNode destination,
            ICollection<GameResult> results,
            string movementGroupID = null,
            string sourceEventInstanceID = null
        )
        {
            movementGroupID ??= Guid.NewGuid().ToString("N");
            destination = _queries.ResolveLiveContainer(destination);

            if (
                !_queries.TryResolveAcceptedDestination(
                    unit,
                    destination,
                    out ContainerNode resolvedDestination
                )
            )
                return false;

            return ExecuteAcceptedMove(
                unit,
                resolvedDestination,
                results,
                movementGroupID,
                sourceEventInstanceID
            );
        }

        /// <summary>
        /// Executes a move whose destination and capacity have already been validated.
        /// </summary>
        /// <param name="unit">The unit receiving the movement order.</param>
        /// <param name="destination">The accepted destination.</param>
        /// <param name="results">The collection receiving movement results.</param>
        /// <param name="movementGroupID">The shared movement order identifier.</param>
        /// <param name="sourceEventInstanceID">The event that requested the movement, if any.</param>
        /// <returns>True when the movement order was accepted; otherwise false.</returns>
        private bool ExecuteAcceptedMove(
            IMovable unit,
            ContainerNode destination,
            ICollection<GameResult> results,
            string movementGroupID,
            string sourceEventInstanceID = null
        )
        {
            Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(destination);

            Planet originPlanet = unit.GetParentOfType<Planet>();
            if (originPlanet == null)
            {
                GameLogger.Warning(
                    $"RequestMove rejected: {unit.GetDisplayName()} is not at a planet location and cannot move."
                );
                return false;
            }

            if (_blockade != null)
            {
                EvacuationLossesResult evacResult = _blockade.ApplyEvacuationLosses(
                    unit,
                    originPlanet
                );
                if (evacResult != null)
                {
                    results.Add(evacResult);
                    AddPlanetGarrisonChangedResults(results, unit, originPlanet);
                    return true;
                }
            }

            Point originPosition = unit.Movement?.CurrentPosition ?? originPlanet.GetPosition();
            int transitTicks = _queries.CalculateTransitTicks(
                unit,
                originPosition,
                originPlanet,
                destinationPlanet
            );

            if (unit.GetParent() == destination)
            {
                unit.Movement = null;
                if (!string.IsNullOrWhiteSpace(sourceEventInstanceID))
                    AddArrivalResults(
                        unit,
                        destinationPlanet,
                        movementGroupID,
                        results,
                        sourceEventInstanceID
                    );
                return true;
            }

            if (destinationPlanet == originPlanet)
            {
                _game.MoveNode(unit, destination);
                ClaimUncolonizedDestinationFromRegiment(unit, destinationPlanet, results);
                unit.Movement = null;
                AddPlanetGarrisonChangedResults(results, unit, originPlanet);
                if (!string.IsNullOrWhiteSpace(sourceEventInstanceID))
                    AddArrivalResults(
                        unit,
                        destinationPlanet,
                        movementGroupID,
                        results,
                        sourceEventInstanceID
                    );
                return true;
            }

            _game.MoveNode(unit, destination);
            ClaimUncolonizedDestinationFromRegiment(unit, destinationPlanet, results);

            unit.Movement = new MovementState
            {
                TransitTicks = transitTicks,
                TicksElapsed = 0,
                MovementGroupID = movementGroupID,
                SourceEventInstanceID = sourceEventInstanceID,
                OriginPosition = originPosition,
                CurrentPosition = originPosition,
            };

            AddPlanetGarrisonChangedResults(results, unit, originPlanet);

            if (unit is Fleet movingFleet)
                RetargetInTransitFleetJoiners(movingFleet, destinationPlanet);

            results.Add(
                new GameObjectEnrouteResult { GameObject = unit, Tick = _game.CurrentTick }
            );
            results.Add(
                new GameObjectEnrouteActiveResult
                {
                    GameObject = unit,
                    IsActive = true,
                    Tick = _game.CurrentTick,
                }
            );

            GameLogger.Log(
                $"{unit.GetDisplayName()} ordered to move to {destination.GetDisplayName()} (ETA: {transitTicks} ticks)"
            );
            return true;
        }

        /// <summary>
        /// Retargets units that are already moving to join a fleet after the fleet receives a new destination.
        /// </summary>
        /// <param name="fleet">The fleet whose inbound units should be retargeted.</param>
        /// <param name="destinationPlanet">The fleet's new destination planet.</param>
        private void RetargetInTransitFleetJoiners(Fleet fleet, Planet destinationPlanet)
        {
            foreach (
                IMovable joiner in fleet
                    .GetChildren<IMovable>(recursive: true)
                    .Where(movable => movable.Movement != null)
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
            string sourceEventInstanceID = movable.Movement.SourceEventInstanceID;
            movable.Movement = new MovementState
            {
                TransitTicks = _queries.CalculateTransitTicks(
                    movable,
                    currentPosition,
                    destinationPlanet,
                    sameSector: false
                ),
                TicksElapsed = 0,
                MovementGroupID = movementGroupID,
                SourceEventInstanceID = sourceEventInstanceID,
                OriginPosition = currentPosition,
                CurrentPosition = currentPosition,
            };
        }

        /// <summary>
        /// Changes the destination of an item that is still under construction.
        /// </summary>
        /// <param name="unit">The item being manufactured.</param>
        /// <param name="destination">The requested destination.</param>
        /// <returns>True when the destination change was accepted.</returns>
        private bool TryRetargetManufacturingDestination(IMovable unit, ContainerNode destination)
        {
            if (
                !_queries.TryResolveAcceptedDestination(
                    unit,
                    destination,
                    out ContainerNode resolvedDestination
                )
            )
                return false;

            ApplyManufacturingDestination(unit, resolvedDestination);
            return true;
        }

        /// <summary>
        /// Reparents an under-construction unit to its accepted delivery destination.
        /// </summary>
        /// <param name="unit">The unit being manufactured.</param>
        /// <param name="resolvedDestination">The accepted delivery destination.</param>
        private void ApplyManufacturingDestination(IMovable unit, ContainerNode resolvedDestination)
        {
            _game.MoveNode(unit, resolvedDestination);
        }

        /// <summary>
        /// Claims an uncolonized destination when a regiment becomes its only owner presence.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <param name="results">The collection receiving ownership results.</param>
        private void ClaimUncolonizedDestinationFromRegiment(
            IMovable unit,
            Planet destinationPlanet,
            ICollection<GameResult> results
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

            _game.ChangeOwnership(destinationPlanet, ownerInstanceId);
            destinationPlanet.PopularSupport.Clear();

            foreach (Faction supportFaction in _game.GetFactions())
            {
                if (supportFaction.InstanceID == ownerInstanceId)
                    destinationPlanet.SetFullPopularSupport(supportFaction.InstanceID);
                else
                    destinationPlanet.SetPopularSupport(supportFaction.InstanceID, 0);
            }

            results.Add(
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
        /// Records each planet where the active regiment garrison changed.
        /// </summary>
        /// <param name="results">The collection receiving garrison results.</param>
        /// <param name="unit">The moved unit.</param>
        /// <param name="planets">The planets whose regiment presence may have changed.</param>
        private void AddPlanetGarrisonChangedResults(
            ICollection<GameResult> results,
            IMovable unit,
            params Planet[] planets
        )
        {
            if (results == null || unit is not Regiment)
                return;

            foreach (Planet planet in planets.Where(planet => planet != null).Distinct())
            {
                results.Add(
                    new PlanetGarrisonChangedResult { Planet = planet, Tick = _game.CurrentTick }
                );
            }
        }
    }
}
