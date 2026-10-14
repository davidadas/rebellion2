using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Frees destination capacity when required and relocates a threatened mobile headquarters.
    /// </summary>
    public sealed class AIHeadquartersRelocationProposal : AIProposal
    {
        internal override AIProposalPriority Priority => AIProposalPriority.Critical;

        public Building Headquarters { get; }

        public Planet Origin { get; }

        public Planet Destination { get; }

        public Building BuildingToRemove { get; }

        public int MaintenanceHeadroomCost { get; }

        public IReadOnlyList<Fleet> DefensiveFleets { get; }

        public SpecialForces EspionageAgent { get; }

        public Mission EspionageMission { get; }

        public int ThreatStrength { get; }

        public int DefenseStrength { get; }

        /// <summary>
        /// Creates a headquarters-relocation proposal.
        /// </summary>
        /// <param name="headquarters">The mobile headquarters building.</param>
        /// <param name="origin">The current headquarters planet.</param>
        /// <param name="destination">The selected destination planet.</param>
        /// <param name="buildingToRemove">The destination building to remove, if capacity is full.</param>
        /// <param name="maintenanceHeadroomCost">The net maintenance headroom consumed by removing the building.</param>
        /// <param name="defensiveFleets">Ready fleets assigned to accompany the headquarters.</param>
        /// <param name="espionageAgent">The headquarters espionage unit to evacuate, if present.</param>
        /// <param name="espionageMission">The agent's active headquarters mission, if present.</param>
        /// <param name="threatStrength">The known incoming hostile fleet strength.</param>
        /// <param name="defenseStrength">The ready headquarters orbital defense strength.</param>
        public AIHeadquartersRelocationProposal(
            Building headquarters,
            Planet origin,
            Planet destination,
            Building buildingToRemove,
            int maintenanceHeadroomCost,
            IReadOnlyList<Fleet> defensiveFleets,
            SpecialForces espionageAgent,
            Mission espionageMission,
            int threatStrength,
            int defenseStrength
        )
        {
            Headquarters = headquarters;
            Origin = origin;
            Destination = destination;
            BuildingToRemove = buildingToRemove;
            MaintenanceHeadroomCost = Math.Max(0, maintenanceHeadroomCost);
            DefensiveFleets =
                defensiveFleets?.Where(fleet => fleet != null).ToList() ?? new List<Fleet>();
            EspionageAgent = espionageAgent;
            EspionageMission = espionageMission;
            ThreatStrength = threatStrength;
            DefenseStrength = defenseStrength;
        }

        /// <summary>
        /// Returns a stable key for this headquarters relocation.
        /// </summary>
        /// <returns>The relocation sort key.</returns>
        public override string GetSortKey()
        {
            return $"headquarters-relocation:{Origin?.InstanceID}:{Destination?.InstanceID}";
        }

        /// <summary>
        /// Returns whether the headquarters remains threatened and the destination remains usable.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the proposal can be selected.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether the headquarters remains threatened and the destination remains usable.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the proposal can execute.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Creates destination capacity when necessary and starts headquarters relocation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            if (!context.HeadquartersQueries.CanRelocate(Headquarters, Destination))
            {
                if (!TryRemoveBuilding(context))
                    return;

                context.CommitMaintenanceCost(MaintenanceHeadroomCost);
            }

            if (!context.HeadquartersQueries.CanRelocate(Headquarters, Destination))
                return;

            if (
                EspionageMission != null
                && !context.Missions.AbortMission(EspionageMission.InstanceID)
            )
                return;

            if (!CanEvacuateEspionageAgent(context))
                return;

            List<ISceneNode> accompanyingUnits = DefensiveFleets.Cast<ISceneNode>().ToList();
            if (
                EspionageAgent != null
                && !DefensiveFleets.Contains(EspionageAgent.GetParentOfType<Fleet>())
            )
                accompanyingUnits.Add(EspionageAgent);

            if (!context.Headquarters.TryRelocate(Headquarters, Destination, accompanyingUnits))
                return;

            foreach (Fleet fleet in DefensiveFleets)
            {
                fleet.Order = new FleetOrder
                {
                    OrderType = FleetOrderType.Defend,
                    Status = FleetOrderStatus.Readying,
                    TargetPlanetId = Destination.InstanceID,
                };
            }
        }

        /// <summary>
        /// Removes the selected destination building through the applicable application command.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when destination capacity was created.</returns>
        private bool TryRemoveBuilding(AITurnContext context)
        {
            if (BuildingToRemove.ManufacturingStatus == ManufacturingStatus.Building)
            {
                return context.Manufacturing.CancelManufacturing(
                    BuildingToRemove,
                    context.Faction.InstanceID
                );
            }

            return BuildingToRemove.ManufacturingStatus == ManufacturingStatus.Complete
                && context.Maintenance.TryScrap(
                    new List<IManufacturable> { BuildingToRemove },
                    context.Faction.InstanceID
                );
        }

        /// <summary>
        /// Returns whether the live game state still supports this relocation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the threat, headquarters, and destination remain valid.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (
                context?.Game == null
                || context.Faction?.Settings?.Headquarters?.IsMobile != true
                || context.Assessment == null
                || context.Headquarters == null
                || context.HeadquartersQueries == null
                || EspionageMission != null && context.Missions == null
                || context.Maintenance == null
                || context.Manufacturing == null
                || context.Game.GetSceneNodeByInstanceID<Building>(Headquarters?.InstanceID)
                    != Headquarters
                || context.Game.GetSceneNodeByInstanceID<Planet>(Origin?.InstanceID) != Origin
                || context.Game.GetSceneNodeByInstanceID<Planet>(Destination?.InstanceID)
                    != Destination
                || Headquarters.GetParent() != Origin
                || Origin.GetOwnerInstanceID() != context.Faction.InstanceID
                || Destination.GetOwnerInstanceID() != context.Faction.InstanceID
                || !Destination.IsColonized
                || Destination.IsDestroyed
                || MovementQueries.IsBlockedFromDestinationByBlockade(Headquarters, Destination)
                || context.Assessment.HasKnownHostileFleet(Destination)
                || context.Assessment.GetKnownIncomingHostileFleetStrength(Origin)
                    <= context.Assessment.GetHeadquartersOrbitalDefenseStrength(Origin)
                || DefensiveFleets.Any(fleet => !CanEvacuateDefensiveFleet(context, fleet))
                || !CanEvacuateEspionageAssignment(context)
            )
                return false;

            if (context.HeadquartersQueries.CanRelocate(Headquarters, Destination))
                return true;

            return BuildingToRemove != null
                && context.Game.GetSceneNodeByInstanceID<Building>(BuildingToRemove.InstanceID)
                    == BuildingToRemove
                && BuildingToRemove.GetParent() == Destination
                && BuildingToRemove.GetOwnerInstanceID() == context.Faction.InstanceID
                && BuildingToRemove.GetBuildingType() != BuildingType.Headquarters
                && BuildingToRemove.Movement == null
                && BuildingToRemove.ManufacturingStatus
                    is ManufacturingStatus.Building
                        or ManufacturingStatus.Complete
                && Destination.GetAvailableEnergy() == 0;
        }

        /// <summary>
        /// Returns whether a defensive fleet is still ready at the threatened headquarters.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The defensive fleet to inspect.</param>
        /// <returns>True when the fleet can accompany the relocation.</returns>
        private bool CanEvacuateDefensiveFleet(AITurnContext context, Fleet fleet)
        {
            return fleet != null
                && context.Game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID) == fleet
                && fleet.GetOwnerInstanceID() == context.Faction.InstanceID
                && fleet.GetParentOfType<Planet>() == Origin
                && fleet.RoleType == FleetRoleType.Battle
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips();
        }

        /// <summary>
        /// Returns whether the selected espionage unit and active mission still match the planned
        /// headquarters assignment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the assignment can be evacuated.</returns>
        private bool CanEvacuateEspionageAssignment(AITurnContext context)
        {
            if (EspionageAgent == null)
                return EspionageMission == null;

            if (
                context.Game.GetSceneNodeByInstanceID<SpecialForces>(EspionageAgent.InstanceID)
                    != EspionageAgent
                || EspionageAgent.GetOwnerInstanceID() != context.Faction.InstanceID
                || EspionageAgent.GetParentOfType<Planet>() != Origin
                || EspionageAgent.Movement != null
                || !EspionageAgent.CanPerformMission(MissionTypeIDs.Espionage)
            )
                return false;

            if (EspionageMission == null)
                return !EspionageAgent.IsOnMission();

            return context.Game.GetSceneNodeByInstanceID<Mission>(EspionageMission.InstanceID)
                    == EspionageMission
                && EspionageMission.ConfigKey == MissionTypeIDs.Espionage
                && EspionageMission.GetParentOfType<Planet>() == Origin
                && EspionageAgent.GetParent() == EspionageMission;
        }

        /// <summary>
        /// Returns whether an aborted or idle espionage unit is ready to depart from the old
        /// headquarters.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when no agent was selected or the selected agent can move.</returns>
        private bool CanEvacuateEspionageAgent(AITurnContext context)
        {
            return EspionageAgent == null
                || context.Game.GetSceneNodeByInstanceID<SpecialForces>(EspionageAgent.InstanceID)
                    == EspionageAgent
                    && EspionageAgent.GetOwnerInstanceID() == context.Faction.InstanceID
                    && EspionageAgent.GetParentOfType<Planet>() == Origin
                    && EspionageAgent.Movement == null
                    && !EspionageAgent.IsOnMission();
        }
    }
}
