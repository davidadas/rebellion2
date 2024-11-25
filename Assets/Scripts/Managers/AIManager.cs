using System.Collections.Generic;
using System.Linq;

/// <summary>
/// @NOTE: This is a dummy class meant for testing. It does NOT represent the
/// actual AI that will be implemented in the game.
/// </summary>
public class AIManager
{
    private Game game;
    private MissionManager missionManager;
    private UnitManager unitManager;
    private PlanetManager planetManager;

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
    /// Updates the AI for all factions.
    /// </summary>
    public void Update()
    {
        // Update the AI for each faction.
        foreach (Faction faction in game.Factions)
        {
            if (faction.IsAIControlled())
            {
                UpdateFaction(faction);
            }
        }
    }

    /// <summary>
    /// Updates the AI for the specified faction.
    /// </summary>
    /// <param name="faction"></param>
    private void UpdateFaction(Faction faction)
    {
        UpdateOfficers(faction);
        UpdateManufacturing(faction);
        UpdateShipyards(faction);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="ownerInstanceId"></param>
    /// <returns></returns>
    private List<Officer> GetAvailableOfficers(string ownerInstanceId)
    {
        return game.GetSceneNodesByOwnerInstanceID<Officer>(ownerInstanceId)
            .FindAll(o => o.IsMovable());
    }

    /// <summary>
    ///
    /// </summary>
    private void UpdateOfficers(Faction faction)
    {
        List<Officer> officers = faction.GetAvailableOfficers(faction);

        foreach (Officer officer in officers)
        {
            if (officer.IsMovable())
            {
                if (officer.IsMain && game.GetUnrecruitedOfficers(faction.InstanceID).Count > 0)
                {
                    Planet nearestFriendlyPlanet = faction.GetNearestPlanetTo(officer);

                    GameLogger.Log(
                        $"Sending {officer.GetDisplayName()} on a recruitment mission to {nearestFriendlyPlanet.GetDisplayName()}."
                    );

                    missionManager.InitiateMission(
                        MissionType.Recruitment,
                        officer,
                        nearestFriendlyPlanet
                    );
                }
                else if (
                    officer.IsMain
                    || officer.GetSkillValue(MissionParticipantSkill.Diplomacy) > 60
                )
                {
                    Planet nearestUnownedPlanet =
                        officer
                            .GetParentOfType<PlanetSystem>()
                            .GetChildrenByOwnerInstanceID(null)
                            .FirstOrDefault() as Planet;

                    GameLogger.Log(
                        $"Sending {officer.GetDisplayName()} on a diplomacy mission to {nearestUnownedPlanet.GetDisplayName()}."
                    );

                    missionManager.InitiateMission(
                        MissionType.Diplomacy,
                        officer,
                        nearestUnownedPlanet
                    );
                }
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
    private void UpdateManufacturing(Faction faction)
    {
        List<Planet> idleConstructionFacilities = faction.GetIdleFacilities(
            ManufacturingType.Building
        );

        if (idleConstructionFacilities.Count == 0)
        {
            return;
        }

        List<Technology> buildingOptions = faction.GetResearchedTechnologies(
            ManufacturingType.Building
        );

        Technology constructionYardTech = buildingOptions
            .FindAll(technology =>
                (technology.GetReference() as Building).GetBuildingType()
                == BuildingType.ConstructionFacility
            )
            .LastOrDefault();

        foreach (Planet planet in idleConstructionFacilities)
        {
            int facilityCount = planet.GetBuildingTypeCount(BuildingType.ConstructionFacility);
            if (
                constructionYardTech.GetReference() is Building building
                && planet.GetAvailableSlots(building.GetBuildingSlot()) > 0
                && facilityCount < 5
            )
            {
                planetManager.AddToManufacturingQueue(planet, planet, constructionYardTech, 1);
            }
        }
    }

    private void UpdateShipyards(Faction faction)
    {
        List<Planet> idleShipyards = faction.GetIdleFacilities(ManufacturingType.Ship);

        if (idleShipyards.Count == 0)
        {
            return;
        }

        List<Technology> shipyardOptions = faction.GetResearchedTechnologies(
            ManufacturingType.Ship
        );

        Technology starfighterTech = shipyardOptions
            .FindAll(technology => (technology.GetReference().GetType() == typeof(Starfighter)))
            .LastOrDefault();

        foreach (Planet planet in idleShipyards)
        {
            int shipyardCount = planet.GetBuildingTypeCount(BuildingType.Shipyard);

            List<Fleet> fleets = faction.GetOwnedUnitsByType<Fleet>();
            List<Fleet> sortedFleets = fleets
                .OrderBy(fleet => fleet.GetExcessStarfighterCapacity())
                .ToList();

            GameLogger.Log($"Found {fleets.Count} fleets for {faction.GetDisplayName()}.");
            foreach (Fleet fleet in sortedFleets)
            {
                GameLogger.Log(
                    $"Adding {fleet.GetExcessStarfighterCapacity()} {starfighterTech.GetReference().GetDisplayName()} to the manufacturing queue of {planet.GetDisplayName()}."
                );
                planetManager.AddToManufacturingQueue(
                    planet,
                    fleet,
                    starfighterTech,
                    fleet.GetExcessStarfighterCapacity()
                );
            }
        }
    }
}
