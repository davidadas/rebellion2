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
using Rebellion.Util.Random;

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
        private readonly EvacuationLossResolver _evacuationLosses;
        private readonly FleetCommands _fleetSystem;
        private readonly PersonnelCommands _personnelCommands;
        private readonly MovementQueries _queries;
        private readonly IRandomNumberProvider _movementRandom;
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
        /// <param name="random">The random source used for movement consequences.</param>
        /// <param name="personnelCommands">The personnel lifecycle operations.</param>
        public MovementCommands(
            GameRoot game,
            FogOfWarCommands fogOfWar,
            FleetCommands fleetSystem,
            FogOfWarQueries fogOfWarQueries,
            MovementQueries queries,
            IRandomNumberProvider random = null,
            PersonnelCommands personnelCommands = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fogOfWar = fogOfWar ?? throw new ArgumentNullException(nameof(fogOfWar));
            _fleetSystem = fleetSystem ?? throw new ArgumentNullException(nameof(fleetSystem));
            _fogOfWarQueries =
                fogOfWarQueries ?? throw new ArgumentNullException(nameof(fogOfWarQueries));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _movementRandom = random ?? game.Random;
            _evacuationLosses = new EvacuationLossResolver(game, _movementRandom);
            _personnelCommands =
                personnelCommands ?? new PersonnelCommands(new PersonnelQueries(game));
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
        /// Moves a fleet and keeps units already joining it synchronized with its arrival.
        /// </summary>
        /// <param name="fleet">The fleet receiving the movement order.</param>
        /// <param name="destination">The requested destination.</param>
        internal void RequestCoordinatedMove(Fleet fleet, ContainerNode destination)
        {
            RequestMove(fleet, destination);
            AlignInboundUnits(fleet);
        }

        /// <summary>
        /// Delays units already joining a moving fleet so they arrive with it.
        /// </summary>
        /// <param name="fleet">The moving destination fleet.</param>
        private static void AlignInboundUnits(Fleet fleet)
        {
            if (fleet?.Movement == null)
                return;

            int fleetTransitTicks = fleet.Movement.TicksRemaining();
            foreach (IMovable joiner in fleet.GetChildren<IMovable>(recursive: true))
            {
                if (joiner.Movement == null)
                    continue;

                joiner.Movement.TransitTicks = fleetTransitTicks;
                joiner.Movement.TicksElapsed = 0;
            }
        }

        /// <summary>
        /// Attempts to move a complete unit group to the first destination that accepts it.
        /// </summary>
        /// <param name="units">The units that must move together.</param>
        /// <param name="destinations">Candidate destinations in preference order.</param>
        /// <param name="sourceEventInstanceID">The event requesting movement, when applicable.</param>
        /// <param name="reactions">The result collection receiving accepted movement outcomes.</param>
        /// <returns>True when a destination accepted and received the movement request.</returns>
        internal bool TryRequestMove(
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
        internal bool TryPlaceUnits(List<IMovable> units, IReadOnlyList<ContainerNode> destinations)
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
        internal void RequestMove(IMovable unit, ContainerNode destination)
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
        internal void RequestMove(IMovable unit, ContainerNode destination, Planet origin)
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
                unit is Starfighter
                && destinationPlanet != origin
                && MovementQueries.IsBlockedFromDestinationByBlockade(unit, destinationPlanet)
            )
            {
                _game.MoveNode(unit, origin);
                unit.Movement = null;
                CompleteManufacturingDelivery(unit);
                return;
            }

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
        internal void RequestMove(List<IMovable> units, ContainerNode destination)
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
        /// <param name="returnLocation">Receives the planet paired with the first accepted return destination.</param>
        /// <returns>Units that could not be assigned to a return destination.</returns>
        internal List<IMovable> ReturnFromMission(
            IReadOnlyList<IMissionParticipant> participants,
            IReadOnlyList<IMovable> additionalPassengers,
            out Planet returnLocation
        )
        {
            if (participants == null)
                throw new ArgumentNullException(nameof(participants));
            if (additionalPassengers == null)
                throw new ArgumentNullException(nameof(additionalPassengers));

            returnLocation = null;

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
            ContainerNode firstReturnDestination = returnGroups.Keys.FirstOrDefault();
            if (firstReturnDestination != null)
                returnLocation = MovementQueries.RequireDestinationPlanet(firstReturnDestination);

            foreach (KeyValuePair<ContainerNode, List<IMovable>> returnGroup in returnGroups)
            {
                string movementGroupID = Guid.NewGuid().ToString("N");
                int groupTransitTicks = CalculateGroupTransitTicks(
                    returnGroup.Value,
                    Enumerable.Repeat(returnGroup.Key, returnGroup.Value.Count).ToList()
                );
                foreach (IMovable unit in returnGroup.Value)
                {
                    ExecuteMoveAfterDepartureEncounter(
                        unit,
                        returnGroup.Key,
                        _pendingResults,
                        movementGroupID,
                        groupTransitTicks
                    );
                }
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
                FleetLifecycle.RemoveEmptyFleet(_game, createdDestinationFleet);
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
                    FleetLifecycle.RemoveEmptyFleet(_game, sourceFleet);
            }

            FleetLifecycle.RemoveEmptyFleet(_game, createdDestinationFleet);
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

            int groupTransitTicks = CalculateGroupTransitTicks(units, destinations);
            string movementGroupID = Guid.NewGuid().ToString("N");
            HashSet<IMovable> stoppedPersonnel = ResolvePersonnelTransitDepartures(
                units,
                destinations,
                results
            );
            for (int index = 0; index < units.Count; index++)
            {
                IMovable unit = units[index];
                if (stoppedPersonnel.Contains(unit))
                    continue;

                ContainerNode resolvedDestination = destinations[index];
                if (MovementQueries.IsManufacturingDestinationChange(unit))
                    ApplyManufacturingDestination(unit, resolvedDestination);
                else
                    ExecuteAcceptedMove(
                        unit,
                        resolvedDestination,
                        results,
                        movementGroupID,
                        sourceEventInstanceID,
                        groupTransitTicks
                    );
            }

            return true;
        }

        /// <summary>
        /// Resolves hostile-orbit encounters before a grouped personnel movement order departs.
        /// </summary>
        /// <param name="units">The units in the accepted movement group.</param>
        /// <param name="destinations">The resolved destination for each unit.</param>
        /// <param name="results">The collection receiving encounter results.</param>
        /// <returns>The personnel that can no longer depart.</returns>
        private HashSet<IMovable> ResolvePersonnelTransitDepartures(
            IReadOnlyList<IMovable> units,
            IReadOnlyList<ContainerNode> destinations,
            ICollection<GameResult> results
        )
        {
            List<IMovable> personnel = new List<IMovable>();
            for (int index = 0; index < units.Count; index++)
            {
                IMovable unit = units[index];
                if (
                    !IsFreePersonnel(unit)
                    || destinations[index] is Mission
                    || unit.GetParent() is Mission
                )
                    continue;

                Planet originPlanet = unit.GetParentOfType<Planet>();
                Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(
                    destinations[index]
                );
                if (originPlanet != null && originPlanet != destinationPlanet)
                    personnel.Add(unit);
            }

            foreach (
                IGrouping<Planet, IMovable> originGroup in personnel.GroupBy(unit =>
                    unit.GetParentOfType<Planet>()
                )
            )
                ResolvePersonnelEncounters(originGroup.ToList(), originGroup.Key, results);

            HashSet<IMovable> stopped = personnel
                .Where(unit => !CanContinuePersonnelMovement(unit))
                .ToHashSet();
            foreach (
                Officer captive in units.OfType<Officer>().Where(officer => officer.IsCaptured)
            )
            {
                if (!HasAvailableEscort(captive, units))
                    stopped.Add(captive);
            }

            return stopped;
        }

        /// <summary>
        /// Calculates the common duration for units issued one movement-group order.
        /// </summary>
        /// <param name="units">The validated movable units.</param>
        /// <param name="destinations">The resolved destination for each unit.</param>
        /// <returns>The longest individual transit duration in the group.</returns>
        private int CalculateGroupTransitTicks(
            IReadOnlyList<IMovable> units,
            IReadOnlyList<ContainerNode> destinations
        )
        {
            int groupTransitTicks = 0;
            for (int index = 0; index < units.Count; index++)
            {
                IMovable unit = units[index];
                ContainerNode destination = destinations[index];
                if (
                    MovementQueries.IsManufacturingDestinationChange(unit)
                    || ReferenceEquals(unit.GetParent(), destination)
                )
                    continue;

                Planet originPlanet = unit.GetParentOfType<Planet>();
                Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(destination);
                if (ReferenceEquals(originPlanet, destinationPlanet))
                    continue;

                Point originPosition = unit.Movement?.CurrentPosition ?? originPlanet.GetPosition();
                groupTransitTicks = Math.Max(
                    groupTransitTicks,
                    _queries.CalculateTransitTicks(
                        unit,
                        originPosition,
                        originPlanet,
                        destinationPlanet
                    )
                );
            }

            return groupTransitTicks;
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
        /// Advances all active movement by one tick and resolves completed arrival groups.
        /// </summary>
        /// <param name="movables">The movable units present at the start of the movement phase.</param>
        /// <param name="results">The results generated this tick.</param>
        internal void UpdateMovements(IReadOnlyList<IMovable> movables, List<GameResult> results)
        {
            if (movables == null)
                throw new ArgumentNullException(nameof(movables));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            List<IMovable> arrivals = movables.Where(AdvanceMovement).ToList();
            ResolvePersonnelArrivalGroups(arrivals, results);
            foreach (IMovable movable in arrivals)
            {
                if (movable.Movement?.IsComplete() != true || movable.GetParent() == null)
                    continue;

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
                CheckArrival(movable, destination, destinationPlanet, results);
            }
        }

        /// <summary>Advances one active unit and reports whether it reached its destination.</summary>
        /// <param name="movable">The movable unit to advance.</param>
        /// <returns>True when the unit completed transit during this update.</returns>
        private bool AdvanceMovement(IMovable movable)
        {
            if (
                movable?.Movement == null
                || movable is IManufacturable m
                    && m.GetManufacturingStatus() == ManufacturingStatus.Building
            )
                return false;

            Planet destinationPlanet = movable.GetParentOfType<Planet>();
            if (destinationPlanet == null)
                throw new InvalidOperationException(
                    $"Unit {movable.GetDisplayName()} is in transit but has no parent planet."
                );

            movable.Movement.TicksElapsed++;
            movable.SetPosition(CalculateInterpolatedPosition(movable, destinationPlanet));

            GameLogger.Log(
                $"{movable.GetDisplayName()} in transit ({movable.Movement.TicksElapsed}/{movable.Movement.TransitTicks} ticks)"
            );

            return movable.Movement.IsComplete();
        }

        /// <summary>Resolves one arrival encounter for each completed personnel movement group.</summary>
        /// <param name="arrivals">The units that completed transit this tick.</param>
        /// <param name="results">The collection receiving encounter results.</param>
        private void ResolvePersonnelArrivalGroups(
            IReadOnlyList<IMovable> arrivals,
            ICollection<GameResult> results
        )
        {
            List<IMovable> personnel = arrivals
                .Where(movable => IsFreePersonnel(movable) && movable.GetParent() is not Mission)
                .ToList();
            foreach (
                IGrouping<string, IMovable> group in personnel
                    .Where(movable => !string.IsNullOrEmpty(movable.Movement?.MovementGroupID))
                    .GroupBy(movable => movable.Movement.MovementGroupID)
            )
            {
                Planet destinationPlanet = group.First().GetParentOfType<Planet>();
                ResolvePersonnelEncounters(group.ToList(), destinationPlanet, results);
            }

            foreach (
                IMovable movable in personnel.Where(movable =>
                    string.IsNullOrEmpty(movable.Movement?.MovementGroupID)
                )
            )
            {
                ResolvePersonnelEncounters(
                    new[] { movable },
                    movable.GetParentOfType<Planet>(),
                    results
                );
            }
        }

        /// <summary>Resolves hostile detection against personnel crossing one planetary system.</summary>
        /// <param name="movables">The personnel crossing together.</param>
        /// <param name="planet">The planetary system being crossed.</param>
        /// <param name="results">The collection receiving encounter results.</param>
        private void ResolvePersonnelEncounters(
            IReadOnlyList<IMovable> movables,
            Planet planet,
            ICollection<GameResult> results
        )
        {
            if (movables == null || planet == null || results == null)
                return;

            foreach (
                IGrouping<string, IMissionParticipant> group in movables
                    .OfType<IMissionParticipant>()
                    .Where(IsEligibleEncounterParticipant)
                    .GroupBy(participant => participant.GetOwnerInstanceID())
            )
                ResolvePersonnelEncounterGroup(group.ToList(), planet, results);
        }

        /// <summary>Returns whether personnel remain eligible for a movement encounter.</summary>
        /// <param name="participant">The participant to inspect.</param>
        /// <returns>True when the participant remains active and free.</returns>
        private static bool IsEligibleEncounterParticipant(IMissionParticipant participant)
        {
            return participant switch
            {
                Officer officer => !officer.IsKilled && !officer.IsCaptured,
                SpecialForces specialForces => specialForces.IsActive(),
                _ => false,
            };
        }

        /// <summary>Resolves detection and confrontation for one faction's personnel.</summary>
        /// <param name="participants">The personnel crossing together.</param>
        /// <param name="planet">The planetary system being crossed.</param>
        /// <param name="results">The collection receiving encounter results.</param>
        private void ResolvePersonnelEncounterGroup(
            IReadOnlyList<IMissionParticipant> participants,
            Planet planet,
            ICollection<GameResult> results
        )
        {
            PersonnelMovementEncounterOdds encounter = _queries.GetPersonnelEncounterOdds(
                participants,
                planet
            );
            if (
                encounter.Detectors.FirstOrDefault(detector =>
                    RollMovementPercent(detector.DetectionProbability)
                ) == null
            )
                return;

            foreach (IMissionParticipant participant in participants.ToList())
            {
                PersonnelMovementDetectorOdds detector = encounter.Detectors[
                    _movementRandom.NextInt(0, encounter.Detectors.Count)
                ];
                ResolveDetectedPersonnel(participant, detector, planet, results);
            }
        }

        /// <summary>Resolves one detected participant's confrontation.</summary>
        /// <param name="participant">The detected participant.</param>
        /// <param name="encounter">The hostile detector and calculated probabilities.</param>
        /// <param name="planet">The planetary system where the encounter occurs.</param>
        /// <param name="results">The collection receiving encounter results.</param>
        private void ResolveDetectedPersonnel(
            IMissionParticipant participant,
            PersonnelMovementDetectorOdds encounter,
            Planet planet,
            ICollection<GameResult> results
        )
        {
            bool evaded = RollMovementPercent(encounter.GetEvasionProbability(participant));
            if (participant is SpecialForces specialForces)
            {
                if (!evaded)
                    DestroyDetectedSpecialForces(specialForces, planet, results);
                return;
            }

            if (participant is not Officer officer)
                return;

            List<GameResult> injuryResults = new List<GameResult>();
            bool killed = Mission.ApplyEvasionInjury(
                officer,
                encounter.Commander ?? encounter.Detector as IGameEntity,
                planet,
                _game,
                _movementRandom,
                injuryResults
            );
            foreach (GameResult injuryResult in injuryResults)
                results.Add(injuryResult);

            if (killed)
            {
                _personnelCommands.KillOfficer(officer);
                return;
            }
            if (evaded)
                return;

            MovementState interruptedMovement = officer.Movement;
            officer.Movement = null;
            if (!officer.TryCapture(encounter.Detector.GetOwnerInstanceID()))
            {
                officer.Movement = interruptedMovement;
                return;
            }

            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    CaptorInstanceID = encounter.Detector.GetOwnerInstanceID(),
                    ParentAtCapture = officer.GetParent(),
                    CapturingUnit = encounter.Detector,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>Removes detected special forces that fail to evade.</summary>
        /// <param name="specialForces">The unit to remove.</param>
        /// <param name="planet">The planetary system where the unit was destroyed.</param>
        /// <param name="results">The collection receiving the destruction result.</param>
        private void DestroyDetectedSpecialForces(
            SpecialForces specialForces,
            Planet planet,
            ICollection<GameResult> results
        )
        {
            _game.DeleteNode(specialForces);
            results.Add(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = specialForces,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>Rolls against a movement encounter percentage.</summary>
        /// <param name="probability">The percentage chance of success.</param>
        /// <returns>True when the roll succeeds.</returns>
        private bool RollMovementPercent(double probability)
        {
            return probability > 0 && _movementRandom.NextDouble() * 100 < probability;
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
                CaptureArrivalSnapshot(movable, destinationPlanet);
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
        /// Returns whether a unit is free personnel eligible for a transit encounter.
        /// </summary>
        /// <param name="movable">The unit to inspect.</param>
        /// <returns>True for an active, uncaptured officer or special-forces unit.</returns>
        private static bool IsFreePersonnel(IMovable movable)
        {
            return movable switch
            {
                Officer officer => !officer.IsKilled && !officer.IsCaptured,
                SpecialForces specialForces => specialForces.IsActive(),
                _ => false,
            };
        }

        /// <summary>
        /// Returns whether a captive still has an eligible escort after a departure encounter.
        /// </summary>
        /// <param name="captive">The captured officer requiring an escort.</param>
        /// <param name="units">The complete movement group.</param>
        /// <returns>True when a surviving free escort remains in the group.</returns>
        private static bool HasAvailableEscort(Officer captive, IReadOnlyList<IMovable> units)
        {
            return !string.IsNullOrEmpty(captive.CaptorInstanceID)
                && units.Any(unit =>
                    !ReferenceEquals(unit, captive)
                    && unit.GetOwnerInstanceID() == captive.CaptorInstanceID
                    && IsFreePersonnel(unit)
                );
        }

        /// <summary>
        /// Returns whether personnel remain free and active after a transit encounter.
        /// </summary>
        /// <param name="movable">The personnel unit to inspect.</param>
        /// <returns>True when movement may continue.</returns>
        private static bool CanContinuePersonnelMovement(IMovable movable)
        {
            return movable switch
            {
                Officer officer => !officer.IsKilled && !officer.IsCaptured,
                SpecialForces specialForces => specialForces.IsActive()
                    && specialForces.GetParent() != null,
                _ => true,
            };
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
        /// Captures fog-of-war state when a fleet or one of its capital ships arrives visibly.
        /// </summary>
        /// <param name="movable">The arriving fleet or capital ship.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        private void CaptureArrivalSnapshot(IMovable movable, Planet destinationPlanet)
        {
            Fleet fleet = movable as Fleet ?? (movable as CapitalShip)?.GetParentOfType<Fleet>();
            if (fleet == null)
                return;

            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(f => f.InstanceID == fleet.OwnerInstanceID);
            if (faction == null || !_fogOfWarQueries.IsPlanetVisible(destinationPlanet, faction))
                return;

            PlanetSector sector = destinationPlanet.GetParentOfType<PlanetSector>();
            if (sector != null)
                _fogOfWar.ObservePlanet(faction, destinationPlanet);
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
        /// Moves a unit to the nearest planet owned by its faction that accepts it.
        /// </summary>
        /// <param name="unit">The unit to evacuate.</param>
        internal void EvacuateToNearestFriendlyPlanet(IMovable unit)
        {
            TryEvacuateToNearestFriendlyPlanet(unit, _pendingResults);
        }

        /// <summary>
        /// Attempts to move a unit to the nearest friendly destination.
        /// </summary>
        /// <param name="unit">The unit to evacuate.</param>
        /// <param name="results">The collection receiving movement facts.</param>
        /// <param name="force">Whether the relocation must leave its current planet.</param>
        /// <param name="opposingBlockadeAtDeparture">
        /// Whether the unit faced an opposing blockade before a preceding state transition, or
        /// null to inspect the current planet state.
        /// </param>
        /// <returns>True when a safe destination accepts the unit.</returns>
        internal bool TryEvacuateToNearestFriendlyPlanet(
            IMovable unit,
            ICollection<GameResult> results,
            bool force = false,
            bool? opposingBlockadeAtDeparture = null
        )
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            string ownerID = MovementQueries.GetMovementControlOwner(unit);
            if (string.IsNullOrEmpty(ownerID))
            {
                unit.Movement = null;
                GameLogger.Warning($"{unit.GetDisplayName()} has no owner — cannot evacuate.");
                return false;
            }

            Planet currentPlanet = unit.GetParentOfType<Planet>();
            if (unit is Fleet inactiveFleet && !inactiveFleet.HasOperationalCapitalShips())
            {
                Planet rebasePlanet = _queries
                    .FindSafeRelocationDestinations(
                        unit,
                        currentPlanet,
                        forceInterplanetaryTravel: true
                    )
                    .OfType<Planet>()
                    .FirstOrDefault();
                if (rebasePlanet != null)
                {
                    _game.MoveNode(inactiveFleet, rebasePlanet);
                    RetargetInTransitFleetJoiners(inactiveFleet, rebasePlanet);
                    GameLogger.Log(
                        $"{inactiveFleet.GetDisplayName()} rebased to {rebasePlanet.GetDisplayName()} because it has no operational capital ships."
                    );
                    return true;
                }
            }

            foreach (
                ContainerNode fallback in _queries.FindSafeRelocationDestinations(
                    unit,
                    currentPlanet,
                    forceInterplanetaryTravel: force
                )
            )
            {
                if (
                    ExecuteMove(
                        unit,
                        fallback,
                        results,
                        opposingBlockadeAtDeparture: opposingBlockadeAtDeparture
                    )
                )
                    return true;
            }

            unit.Movement = null;
            GameLogger.Warning($"{unit.GetDisplayName()} has no friendly planet to evacuate to.");
            return false;
        }

        /// <summary>
        /// Relocates units to a compatible ship in their current fleet or, when none can accept
        /// them, sends them toward the nearest eligible planet owned by their movement controller.
        /// </summary>
        /// <param name="units">The units to relocate from their current containers.</param>
        internal void RelocateUnits(IEnumerable<IMovable> units)
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

            ContainerNode fallback = _queries
                .FindSafeRelocationDestinations(movable, rejectedDestination)
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
        /// <param name="transitTicksOverride">The common group duration, when applicable.</param>
        /// <param name="opposingBlockadeAtDeparture">
        /// Whether the unit faced an opposing blockade before a preceding state transition, or
        /// null to inspect the current planet state.
        /// </param>
        /// <returns>True when the movement order was accepted; otherwise false.</returns>
        private bool ExecuteMove(
            IMovable unit,
            ContainerNode destination,
            ICollection<GameResult> results,
            string movementGroupID = null,
            string sourceEventInstanceID = null,
            int? transitTicksOverride = null,
            bool? opposingBlockadeAtDeparture = null
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

            Planet originPlanet = unit.GetParentOfType<Planet>();
            Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(
                resolvedDestination
            );
            if (
                originPlanet != null
                && destinationPlanet != originPlanet
                && resolvedDestination is not Mission
                && unit.GetParent() is not Mission
                && IsFreePersonnel(unit)
            )
            {
                ResolvePersonnelEncounters(new[] { unit }, originPlanet, results);
                if (!CanContinuePersonnelMovement(unit))
                    return true;
            }

            return ExecuteAcceptedMove(
                unit,
                resolvedDestination,
                results,
                movementGroupID,
                sourceEventInstanceID,
                transitTicksOverride,
                opposingBlockadeAtDeparture: opposingBlockadeAtDeparture
            );
        }

        /// <summary>Starts movement after another lifecycle has already resolved departure encounters.</summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="results">The collection receiving movement results.</param>
        /// <param name="movementGroupID">The shared movement group identifier.</param>
        /// <param name="transitTicks">The common movement-group duration.</param>
        /// <returns>True when the validated movement starts.</returns>
        private bool ExecuteMoveAfterDepartureEncounter(
            IMovable unit,
            ContainerNode destination,
            ICollection<GameResult> results,
            string movementGroupID,
            int transitTicks
        )
        {
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
                transitTicksOverride: transitTicks
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
        /// <param name="transitTicksOverride">The common group duration, when applicable.</param>
        /// <param name="opposingBlockadeAtDeparture">
        /// Whether the unit faced an opposing blockade before a preceding state transition, or
        /// null to inspect the current planet state.
        /// </param>
        /// <returns>True when the movement order was accepted; otherwise false.</returns>
        private bool ExecuteAcceptedMove(
            IMovable unit,
            ContainerNode destination,
            ICollection<GameResult> results,
            string movementGroupID,
            string sourceEventInstanceID = null,
            int? transitTicksOverride = null,
            bool? opposingBlockadeAtDeparture = null
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

            EvacuationLossesResult evacuationLoss = _evacuationLosses.Resolve(
                unit,
                originPlanet,
                opposingBlockadeAtDeparture
            );
            if (evacuationLoss != null)
            {
                results.Add(evacuationLoss);
                AddPlanetGarrisonChangedResults(results, unit, originPlanet);
                return true;
            }

            Point originPosition = unit.Movement?.CurrentPosition ?? originPlanet.GetPosition();
            int transitTicks =
                transitTicksOverride
                ?? _queries.CalculateTransitTicks(
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
