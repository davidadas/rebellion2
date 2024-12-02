using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages AI behavior for factions in the game.
/// Note: This is a simplified implementation for testing purposes.
/// </summary>
public class AIManager
{
    private readonly Game game;
    private readonly MissionManager missionManager;
    private readonly UnitManager unitManager;
    private readonly PlanetManager planetManager;

    public AIManager(
        Game game,
        MissionManager missionManager,
        UnitManager unitManager,
        PlanetManager planetManager
    )
    {
        this.game = game;
        this.missionManager = missionManager;
        this.unitManager = unitManager;
        this.planetManager = planetManager;
    }

    /// <summary>
    /// Updates the AI for all AI-controlled factions.
    /// </summary>
    public void Update()
    {
        foreach (Faction faction in game.Factions.Where(f => f.IsAIControlled()))
        {
            UpdateFaction(faction);
        }
    }

    /// <summary>
    /// Updates various aspects of the AI for a specific faction.
    /// </summary>
    /// <param name="faction">The faction to update.</param>
    private void UpdateFaction(Faction faction)
    {
        UpdateOfficers(faction);
        UpdateBuildings(faction);
        UpdateShipyards(faction);
        UpdateTrainingFacilities(faction);
    }

    /// <summary>
    /// Manages officer assignments for missions.
    /// </summary>
    /// <param name="faction">The faction to update officer assignments for.</param>
    private void UpdateOfficers(Faction faction)
    {
        List<Officer> availableOfficers = faction.GetAvailableOfficers(faction);

        foreach (Officer officer in availableOfficers)
        {
            if (!officer.IsMovable())
                continue;

            if (officer.IsMain && game.GetUnrecruitedOfficers(faction.InstanceID).Any())
            {
                InitiateRecruitmentMission(officer, faction);
            }
            else if (
                officer.IsMain
                || officer.GetSkillValue(MissionParticipantSkill.Diplomacy) > 60
            )
            {
                InitiateDiplomacyMission(officer);
            }
        }
    }

    /// <summary>
    /// Initiates a recruitment mission for the given officer.
    /// </summary>
    /// <param name="officer">The officer to send on the recruitment mission.</param>
    /// <param name="faction">The faction the officer belongs to.</param>
    private void InitiateRecruitmentMission(Officer officer, Faction faction)
    {
        Planet nearestFriendlyPlanet = faction.GetNearestPlanetTo(officer);
        GameLogger.Log(
            $"Sending {officer.GetDisplayName()} on a recruitment mission to {nearestFriendlyPlanet.GetDisplayName()}."
        );
        missionManager.InitiateMission(MissionType.Recruitment, officer, nearestFriendlyPlanet);
    }

    /// <summary>
    /// Initiates a diplomacy mission for the given officer.
    /// </summary>
    /// <param name="officer">The officer to send on the diplomacy mission.</param>
    private void InitiateDiplomacyMission(Officer officer)
    {
        Planet nearestUnownedPlanet =
            officer
                .GetParentOfType<PlanetSystem>()
                .GetChildrenByOwnerInstanceID(null)
                .FirstOrDefault() as Planet;

        if (nearestUnownedPlanet != null)
        {
            GameLogger.Log(
                $"Sending {officer.GetDisplayName()} on a diplomacy mission to {nearestUnownedPlanet.GetDisplayName()}."
            );
            missionManager.InitiateMission(MissionType.Diplomacy, officer, nearestUnownedPlanet);
        }
    }

    /// <summary>
    /// Manages construction of buildings.
    /// </summary>
    /// <param name="faction">The faction to update building construction for.</param>
    private void UpdateBuildings(Faction faction)
    {
        List<Planet> idleConstructionFacilities = faction.GetIdleFacilities(
            ManufacturingType.Building
        );
        if (!idleConstructionFacilities.Any())
            return;

        Technology constructionYardTech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Building,
            BuildingType.ConstructionFacility
        );

        foreach (Planet planet in idleConstructionFacilities)
        {
            if (ShouldBuildConstructionFacility(planet, constructionYardTech))
            {
                planetManager.AddToManufacturingQueue(planet, planet, constructionYardTech, 1);
            }
        }
    }

    /// <summary>
    /// Determines if a construction facility should be built on the given planet.
    /// </summary>
    /// <param name="planet">The planet to check.</param>
    /// <param name="constructionYardTech">The construction yard technology to use.</param>
    /// <returns>True if a construction facility should be built, false otherwise.</returns>
    private bool ShouldBuildConstructionFacility(Planet planet, Technology constructionYardTech)
    {
        if (constructionYardTech?.GetReference() is not Building building)
            return false;

        return planet.GetAvailableSlots(building.GetBuildingSlot()) > 0
            && planet.GetBuildingTypeCount(BuildingType.ConstructionFacility) < 5;
    }

    /// <summary>
    /// Manages production of ships.
    /// </summary>
    /// <param name="faction">The faction to update ship production for.</param>
    private void UpdateShipyards(Faction faction)
    {
        List<Planet> idleShipyards = faction.GetIdleFacilities(ManufacturingType.Ship);
        if (!idleShipyards.Any())
            return;

        Technology starfighterTech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Ship,
            typeof(Starfighter)
        );

        foreach (Planet planet in idleShipyards)
        {
            AssignStarfightersToFleets(faction, planet, starfighterTech);
        }
    }

    /// <summary>
    /// Assigns starfighters to fleets with available capacity.
    /// </summary>
    /// <param name="faction">The faction to assign starfighters for.</param>
    /// <param name="planet">The planet with the idle shipyard.</param>
    /// <param name="starfighterTech">The starfighter technology to use.</param>
    private void AssignStarfightersToFleets(
        Faction faction,
        Planet planet,
        Technology starfighterTech
    )
    {
        List<Fleet> fleets = faction
            .GetOwnedUnitsByType<Fleet>()
            .OrderBy(fleet => fleet.GetExcessStarfighterCapacity())
            .Where(fleet => fleet.GetExcessStarfighterCapacity() > 0)
            .ToList();

        foreach (Fleet fleet in fleets)
        {
            planetManager.AddToManufacturingQueue(planet, fleet, starfighterTech, 1);
        }
    }

    /// <summary>
    /// Manages training of troops.
    /// </summary>
    /// <param name="faction">The faction to update troop training for.</param>
    private void UpdateTrainingFacilities(Faction faction)
    {
        List<Planet> idleTrainingFacilities = faction.GetIdleFacilities(ManufacturingType.Troop);
        if (!idleTrainingFacilities.Any())
            return;

        Technology regimentTech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Troop,
            typeof(Regiment)
        );

        foreach (Planet planet in idleTrainingFacilities)
        {
            AssignRegimentsToFleets(faction, planet, regimentTech);
        }
    }

    /// <summary>
    /// Assigns regiments to fleets with available capacity.
    /// </summary>
    /// <param name="faction">The faction to assign regiments for.</param>
    /// <param name="planet">The planet with the idle training facility.</param>
    /// <param name="regimentTech">The regiment technology to use.</param>
    private void AssignRegimentsToFleets(Faction faction, Planet planet, Technology regimentTech)
    {
        List<Fleet> fleets = faction
            .GetOwnedUnitsByType<Fleet>()
            .OrderBy(fleet => fleet.GetExcessRegimentCapacity())
            .Where(fleet => fleet.GetExcessRegimentCapacity() > 0)
            .ToList();

        foreach (Fleet fleet in fleets)
        {
            planetManager.AddToManufacturingQueue(planet, fleet, regimentTech, 1);
        }
    }

    /// <summary>
    /// Retrieves the highest-tier technology for the given manufacturing type and reference type.
    /// </summary>
    /// <param name="faction">The faction to get the technology for.</param>
    /// <param name="manufacturingType">The manufacturing type to filter by.</param>
    /// <param name="referenceType">The type of the technology reference to filter by.</param>
    /// <returns>The highest-tier technology matching the criteria, or null if not found.</returns>
    private Technology GetHighestTierTechnology(
        Faction faction,
        ManufacturingType manufacturingType,
        System.Type referenceType
    )
    {
        return faction
            .GetResearchedTechnologies(manufacturingType)
            .Where(tech => tech.GetReference().GetType() == referenceType)
            .LastOrDefault();
    }

    /// <summary>
    /// Retrieves the highest-tier technology for the given manufacturing type and building type.
    /// </summary>
    /// <param name="faction">The faction to get the technology for.</param>
    /// <param name="manufacturingType">The manufacturing type to filter by.</param>
    /// <param name="buildingType">The building type to filter by.</param>
    /// <returns>The highest-tier technology matching the criteria, or null if not found.</returns>
    private Technology GetHighestTierTechnology(
        Faction faction,
        ManufacturingType manufacturingType,
        BuildingType buildingType
    )
    {
        return faction
            .GetResearchedTechnologies(manufacturingType)
            .Where(tech => (tech.GetReference() as Building)?.GetBuildingType() == buildingType)
            .LastOrDefault();
    }
}
