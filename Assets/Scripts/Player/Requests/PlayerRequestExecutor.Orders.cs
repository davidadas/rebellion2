using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.PlayerRequests
{
    public sealed partial class PlayerRequestExecutor
    {
        /// <summary>
        /// Requests normal movement using the existing complete-selection ownership policy.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected unit identities and destination.</param>
        /// <returns>The completed movement outcome.</returns>
        private PlayerRequestOutcome Move(Faction actor, MoveRequest request)
        {
            if (!TryResolveSelection(request.ItemIDs, out List<ISceneNode> items))
                return Reject("The movement selection is unavailable.");
            if (
                items.Count == 1
                && items[0] is Building { BuildingType: BuildingType.Headquarters } headquarters
            )
            {
                return Complete(
                    OwnsSelection(actor, items)
                        && _session
                            .GetService<HeadquartersCommands>()
                            .TryRelocate(headquarters, FindNode<Planet>(request.DestinationID))
                );
            }
            return Complete(
                _session
                    .GetService<MovementCommands>()
                    .TryRequestMove(
                        items,
                        FindNode<ContainerNode>(request.DestinationID),
                        actor.InstanceID
                    )
            );
        }

        /// <summary>
        /// Relocates an owned headquarters through headquarters-specific movement rules.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The headquarters, destination, and accompanying selection.</param>
        /// <returns>The completed relocation outcome.</returns>
        private PlayerRequestOutcome RelocateHeadquarters(
            Faction actor,
            RelocateHeadquartersRequest request
        )
        {
            Building headquarters = FindNode<Building>(request.HeadquartersID);
            if (headquarters == null || !OwnsSelection(actor, new[] { headquarters }))
                return Reject("The headquarters is not controlled by this player.");

            List<ISceneNode> accompanyingUnits = new List<ISceneNode>();
            if (
                request.AccompanyingUnitIDs?.Length > 0
                && !TryResolveSelection(request.AccompanyingUnitIDs, out accompanyingUnits)
            )
                return Reject("The accompanying selection is unavailable.");

            return Complete(
                _session
                    .GetService<HeadquartersCommands>()
                    .TryRelocate(
                        headquarters,
                        FindNode<Planet>(request.DestinationPlanetID),
                        accompanyingUnits
                    )
            );
        }

        /// <summary>
        /// Commits a complete waypoint plan through the movement system's route validation.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The fleet selection and ordered route.</param>
        /// <returns>The completed route outcome.</returns>
        private PlayerRequestOutcome SetFleetRoute(Faction actor, SetFleetRouteRequest request)
        {
            return Complete(
                TryResolveSelection(request.ItemIDs, out List<ISceneNode> items)
                    && _session
                        .GetService<MovementCommands>()
                        .TrySetFleetWaypointRoute(
                            items,
                            request.WaypointPlanetIDs,
                            actor.InstanceID
                        )
            );
        }

        /// <summary>
        /// Clears controlled fleet continuation without cancelling current transit.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected fleet identities.</param>
        /// <returns>The completed route-clearing outcome.</returns>
        private PlayerRequestOutcome ClearFleetRoute(Faction actor, ClearFleetRouteRequest request)
        {
            return Complete(
                TryResolveSelection(request.ItemIDs, out List<ISceneNode> items)
                    && _session
                        .GetService<MovementCommands>()
                        .ClearFleetWaypoints(items, actor.InstanceID)
            );
        }

        /// <summary>
        /// Scraps a complete selection through live ownership and manufacturing validation.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected manufacturable identities.</param>
        /// <returns>The completed scrap outcome.</returns>
        private PlayerRequestOutcome Scrap(Faction actor, ScrapRequest request)
        {
            return Complete(
                TryResolveSelection(request.ItemIDs, out List<IManufacturable> items)
                    && _session.GetService<MaintenanceCommands>().TryScrap(items, actor.InstanceID)
            );
        }

        /// <summary>
        /// Cancels the selected production items that remain eligible, preserving partial acceptance.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected production identities.</param>
        /// <returns>The completed cancellation outcome.</returns>
        private PlayerRequestOutcome CancelManufacturing(
            Faction actor,
            CancelManufacturingRequest request
        )
        {
            List<IManufacturable> items = (request.ItemIDs ?? Array.Empty<string>())
                .Select(FindNode<IManufacturable>)
                .Where(item => item != null)
                .ToList();
            return Complete(
                _session
                    .GetService<ManufacturingCommands>()
                    .CancelManufacturing(items, actor.InstanceID)
            );
        }

        /// <summary>
        /// Retires a complete controlled selection through personnel eligibility validation.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected personnel identities.</param>
        /// <returns>The completed retirement outcome.</returns>
        private PlayerRequestOutcome RetirePersonnel(Faction actor, RetirePersonnelRequest request)
        {
            return Complete(
                TryResolveSelection(request.ItemIDs, out List<ISceneNode> items)
                    && _session.GetService<PersonnelCommands>().Retire(items, actor.InstanceID)
            );
        }

        /// <summary>
        /// Revalidates complete ownership and active work before interrupting selected missions.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected mission identities.</param>
        /// <returns>The completed abort outcome.</returns>
        private PlayerRequestOutcome AbortMissions(Faction actor, AbortMissionsRequest request)
        {
            return Complete(
                TryResolveSelection(request.ItemIDs, out List<Mission> missions)
                    && OwnsSelection(actor, missions)
                    && missions.All(mission =>
                        mission.HasInitiated && !mission.IsWaitingForParticipants()
                    )
                    && _session.GetService<MissionCommands>().AbortMissions(request.ItemIDs)
            );
        }

        /// <summary>
        /// Starts a mission while preserving observed target state and resolving owned participants.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The planned mission and observed target data.</param>
        /// <returns>The completed mission-start outcome.</returns>
        private PlayerRequestOutcome StartMission(Faction actor, StartMissionRequest request)
        {
            if (
                !TryResolveSelection(request.MainParticipantIDs, out List<IMissionParticipant> main)
                || !OwnsSelection(actor, main)
            )
                return Reject("The mission participants are unavailable.");

            List<IMissionParticipant> decoys = new List<IMissionParticipant>();
            if (
                request.DecoyParticipantIDs?.Length > 0
                && (
                    !TryResolveSelection(request.DecoyParticipantIDs, out decoys)
                    || !OwnsSelection(actor, decoys)
                )
            )
                return Reject("The mission decoys are unavailable.");

            MissionContext context = new MissionContext
            {
                MissionTypeID = request.MissionTypeID,
                Location = request.ObservedLocation,
                SelectedTarget = request.ObservedTarget,
                MainParticipants = main,
                DecoyParticipants = decoys,
                Discipline = request.Discipline,
                IsRepeating = request.IsRepeating,
            };
            return Complete(
                _session.GetService<MissionQueries>().CanCreateMission(context)
                    && _session.GetService<MissionCommands>().InitiateMission(context)
            );
        }

        /// <summary>
        /// Forms a fleet through the existing complete ship selection rules.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected capital ship identities.</param>
        /// <returns>The created fleet identity or an explicit rejection.</returns>
        private PlayerRequestOutcome CreateFleet(Faction actor, CreateFleetRequest request)
        {
            Fleet fleet = TryResolveSelection(request.ItemIDs, out List<CapitalShip> ships)
                ? _session.GetService<FleetCommands>().CreateFleet(ships, actor.InstanceID)
                : null;
            return fleet == null
                ? Reject("The selected ships cannot form a fleet.")
                : new PlayerRequestOutcome(true, createdEntityID: fleet.InstanceID);
        }

        /// <summary>
        /// Sets an owned production order using the faction's unlocked authored template.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The lane, product, delivery location, and unfinished quantity.</param>
        /// <returns>The completed production-order outcome.</returns>
        private PlayerRequestOutcome SetManufacturingOrder(
            Faction actor,
            SetManufacturingOrderRequest request
        )
        {
            if (!IsManufacturingType(request.ManufacturingType))
                return Reject("The manufacturing lane is unavailable.");
            IManufacturable template = _session
                .GetService<ManufacturingQueries>()
                .GetUnlockedManufacturables(request.ManufacturingType, actor.InstanceID)
                .FirstOrDefault(candidate => candidate.GetTypeID() == request.TemplateTypeID);
            return Complete(
                template != null
                    && _session
                        .GetService<ManufacturingCommands>()
                        .SetManufacturingOrder(
                            FindNode<Planet>(request.ProducerPlanetID),
                            template,
                            FindNode<ISceneNode>(request.DestinationID),
                            request.Quantity,
                            actor.InstanceID
                        )
            );
        }

        /// <summary>
        /// Retargets unfinished items through the production lane's existing movement policy.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The producer, lane, and requested destination.</param>
        /// <returns>The completed delivery-change outcome.</returns>
        private PlayerRequestOutcome RetargetManufacturing(
            Faction actor,
            RetargetManufacturingRequest request
        )
        {
            return Complete(
                IsManufacturingType(request.ManufacturingType)
                    && _session
                        .GetService<ManufacturingCommands>()
                        .RetargetManufacturingDestination(
                            FindNode<Planet>(request.ProducerPlanetID),
                            request.ManufacturingType,
                            FindNode<ContainerNode>(request.DestinationID),
                            actor.InstanceID
                        )
            );
        }

        /// <summary>
        /// Executes controlled fleet bombardment and captures the actor-visible report once.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected fleets, visible target, and bombardment profile.</param>
        /// <returns>The completed combat report or an explicit rejection.</returns>
        private PlayerRequestOutcome Bombard(Faction actor, BombardRequest request)
        {
            if (
                !Enum.IsDefined(typeof(BombardmentType), request.Type)
                || !TryResolveSelection(request.ItemIDs, out List<Fleet> fleets)
                || !OwnsSelection(actor, fleets)
            )
                return Reject("The selected fleets cannot execute this bombardment.");
            CombatReport report = _session
                .GetService<BombardmentCommands>()
                .TryExecuteReport(fleets, request.ObservedTarget, request.Type, actor.InstanceID);
            return report == null
                ? Reject("The bombardment is unavailable.")
                : new PlayerRequestOutcome(true, combatReport: report);
        }

        /// <summary>
        /// Executes controlled fleet assault and captures the actor-visible report once.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The selected fleets and observed target state.</param>
        /// <returns>The completed combat report or an explicit rejection.</returns>
        private PlayerRequestOutcome Assault(Faction actor, AssaultRequest request)
        {
            if (
                !TryResolveSelection(request.ItemIDs, out List<Fleet> fleets)
                || !OwnsSelection(actor, fleets)
            )
                return Reject("The selected fleets cannot execute this assault.");
            CombatReport report = _session
                .GetService<PlanetaryAssaultCommands>()
                .TryExecuteReport(fleets, request.ObservedTarget, actor.InstanceID);
            return report == null
                ? Reject("The assault is unavailable.")
                : new PlayerRequestOutcome(true, combatReport: report);
        }
    }
}
