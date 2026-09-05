using Rebellion.AI.Planners.Demand;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Creates stable selection claims shared by AI proposals.
    /// </summary>
    internal static class AIClaimKeys
    {
        /// <summary>
        /// Creates a claim for a fleet's order.
        /// </summary>
        /// <param name="fleetId">The fleet instance ID.</param>
        /// <returns>The fleet-order claim.</returns>
        internal static string FleetOrder(string fleetId) => $"fleet:order:{fleetId}";

        /// <summary>
        /// Creates a claim for a fleet's movement.
        /// </summary>
        /// <param name="fleetId">The fleet instance ID.</param>
        /// <returns>The fleet-movement claim.</returns>
        internal static string FleetMovement(string fleetId) => $"fleet:movement:{fleetId}";

        /// <summary>
        /// Creates a claim for a fleet attack.
        /// </summary>
        /// <param name="fleetId">The fleet instance ID.</param>
        /// <returns>The fleet-attack claim.</returns>
        internal static string FleetAttack(string fleetId) => $"fleet:attack:{fleetId}";

        /// <summary>
        /// Creates a claim for a fleet attack target.
        /// </summary>
        /// <param name="planetId">The target planet instance ID.</param>
        /// <returns>The fleet attack-target claim.</returns>
        internal static string FleetAttackTarget(string planetId) =>
            $"fleet:attack-target:{planetId}";

        /// <summary>
        /// Creates a claim for a fleet colonization order.
        /// </summary>
        /// <param name="fleetId">The fleet instance ID.</param>
        /// <returns>The fleet-colonization claim.</returns>
        internal static string FleetColonization(string fleetId) => $"fleet:colonize:{fleetId}";

        /// <summary>
        /// Creates a claim for an orbital engagement target.
        /// </summary>
        /// <param name="planetId">The target planet instance ID.</param>
        /// <returns>The fleet engagement-target claim.</returns>
        internal static string FleetEngagementTarget(string planetId) =>
            $"fleet:engagement-target:{planetId}";

        /// <summary>
        /// Creates a claim for transfers into a fleet.
        /// </summary>
        /// <param name="fleetId">The destination fleet instance ID.</param>
        /// <returns>The fleet transfer-target claim.</returns>
        internal static string FleetTransferTarget(string fleetId) =>
            $"fleet:transfer-target:{fleetId}";

        /// <summary>
        /// Creates a claim for satisfying fleet reinforcement demand.
        /// </summary>
        /// <param name="demandKind">The production demand kind.</param>
        /// <param name="fleetId">The destination fleet instance ID.</param>
        /// <returns>The fleet-reinforcement claim.</returns>
        internal static string FleetReinforcement(AIDemandKind? demandKind, string fleetId) =>
            $"fleet:reinforcement:{demandKind}:{fleetId}";

        /// <summary>
        /// Creates a claim for capital-ship reinforcement of a fleet.
        /// </summary>
        /// <param name="fleetId">The destination fleet instance ID.</param>
        /// <returns>The capital-reinforcement claim.</returns>
        internal static string FleetCapitalReinforcement(string fleetId) =>
            $"fleet:capital-reinforcement:{fleetId}";

        /// <summary>
        /// Creates a claim for forming a new fleet.
        /// </summary>
        /// <param name="factionId">The owning faction instance ID.</param>
        /// <returns>The fleet-creation claim.</returns>
        internal static string FleetCreation(string factionId) => $"fleet:create:{factionId}";

        /// <summary>
        /// Creates a claim for attacking a planet.
        /// </summary>
        /// <param name="planetId">The target planet instance ID.</param>
        /// <returns>The planet-attack claim.</returns>
        internal static string PlanetAttack(string planetId) => $"planet:attack:{planetId}";

        /// <summary>
        /// Creates a claim for defending a planet.
        /// </summary>
        /// <param name="planetId">The target planet instance ID.</param>
        /// <returns>The planet-defense claim.</returns>
        internal static string PlanetDefense(string planetId) => $"planet:defense:{planetId}";

        /// <summary>
        /// Creates a claim for colonizing a planet.
        /// </summary>
        /// <param name="planetId">The target planet instance ID.</param>
        /// <returns>The planet-colonization claim.</returns>
        internal static string PlanetColonization(string planetId) => $"planet:colonize:{planetId}";

        /// <summary>
        /// Creates a claim for starting a faction offensive.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The new-offensive-order claim.</returns>
        internal static string NewOffensiveOrder(string factionId) =>
            $"faction:new-offensive-order:{factionId}";

        /// <summary>
        /// Creates a claim for starting a faction colonization order.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The new-colonization-order claim.</returns>
        internal static string NewColonizationOrder(string factionId) =>
            $"faction:new-colonization-order:{factionId}";

        /// <summary>
        /// Creates a claim for assigning a mission participant.
        /// </summary>
        /// <param name="participantId">The participant instance ID.</param>
        /// <returns>The mission-actor claim.</returns>
        internal static string MissionActor(string participantId) =>
            $"mission:actor:{participantId}";

        /// <summary>
        /// Creates a claim for an active mission.
        /// </summary>
        /// <param name="missionId">The mission instance ID.</param>
        /// <returns>The active-mission claim.</returns>
        internal static string Mission(string missionId) => $"mission:{missionId}";

        /// <summary>
        /// Creates a claim for a faction recruitment mission.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <returns>The mission-recruitment claim.</returns>
        internal static string MissionRecruitment(string factionId) =>
            $"mission:recruitment:{factionId}";

        /// <summary>
        /// Creates a claim for a faction research discipline.
        /// </summary>
        /// <param name="factionId">The faction instance ID.</param>
        /// <param name="discipline">The research discipline.</param>
        /// <returns>The mission-research claim.</returns>
        internal static string MissionResearch(string factionId, ResearchDiscipline discipline) =>
            $"mission:research:{factionId}:{discipline}";

        /// <summary>
        /// Creates a claim for a mission targeting an officer.
        /// </summary>
        /// <param name="officerId">The target officer instance ID.</param>
        /// <returns>The mission-officer claim.</returns>
        internal static string MissionOfficer(string officerId) => $"mission:officer:{officerId}";

        /// <summary>
        /// Creates a claim for a selected mission target.
        /// </summary>
        /// <param name="targetId">The selected target instance ID.</param>
        /// <returns>The mission-target claim.</returns>
        internal static string MissionTarget(string targetId) => $"mission:target:{targetId}";

        /// <summary>
        /// Creates a claim for a mission type at a planet.
        /// </summary>
        /// <param name="missionTypeId">The mission type ID.</param>
        /// <param name="planetId">The target planet instance ID.</param>
        /// <returns>The mission-at-planet claim.</returns>
        internal static string MissionAtPlanet(string missionTypeId, string planetId) =>
            $"mission:{missionTypeId}:planet:{planetId}";

        /// <summary>
        /// Creates a claim for satisfying production demand.
        /// </summary>
        /// <param name="demandId">The production demand ID.</param>
        /// <returns>The production-demand claim.</returns>
        internal static string ProductionDemand(string demandId) => $"production:demand:{demandId}";

        /// <summary>
        /// Creates a claim for constructing a building at a planet.
        /// </summary>
        /// <param name="planetId">The destination planet instance ID.</param>
        /// <returns>The building-destination claim.</returns>
        internal static string ProductionBuildingDestination(string planetId) =>
            $"production:building-destination:{planetId}";

        /// <summary>
        /// Creates a claim for replacing a building.
        /// </summary>
        /// <param name="buildingId">The replaced building instance ID.</param>
        /// <returns>The building-replacement claim.</returns>
        internal static string ProductionBuildingReplacement(string buildingId) =>
            $"production:building-replacement:{buildingId}";

        /// <summary>
        /// Creates a claim for producing a building type.
        /// </summary>
        /// <param name="buildingType">The building type.</param>
        /// <returns>The building-kind claim.</returns>
        internal static string ProductionBuildingKind(BuildingType buildingType) =>
            $"production:building-kind:{buildingType}";

        /// <summary>
        /// Creates a claim for a planet's building-production lane.
        /// </summary>
        /// <param name="planetId">The producer planet instance ID.</param>
        /// <returns>The building-production claim.</returns>
        internal static string BuildingManufacturingLane(string planetId) =>
            $"production:building:{planetId}";

        /// <summary>
        /// Creates a claim for a planet's manufacturing lane.
        /// </summary>
        /// <param name="manufacturingType">The manufacturing type.</param>
        /// <param name="planetId">The producer planet instance ID.</param>
        /// <returns>The production-lane claim.</returns>
        internal static string ManufacturingLane(
            ManufacturingType? manufacturingType,
            string planetId
        ) => $"production:{manufacturingType}:{planetId}";
    }
}
