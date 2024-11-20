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
                Planet nearestPlanet = faction.GetNearestPlanet(officer);
                if (
                    officer.IsMainCharacter()
                    && game.GetUnrecruitedOfficers(faction.InstanceID).Count > 0
                )
                {
                    missionManager.InitiateMission(MissionType.Recruitment, officer, nearestPlanet);
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

        List<Building> buildingOptions = faction.GetAvailableTechnologies<Building>();

        IManufacturable constructionYard = buildingOptions
            .FindAll(b => b.GetBuildingType() == BuildingType.ConstructionFacility)
            .LastOrDefault();

        foreach (Planet planet in idleConstructionFacilities)
        {
            int facilityCount = planet.GetBuildingTypeCount(BuildingType.ConstructionFacility);
            if (
                constructionYard is Building building
                && planet.GetAvailableSlots(building.GetBuildingSlot()) > 0
            )
            {
                planetManager.AddToManufacturingQueue(planet, planet, constructionYard, 1);
            }
        }
    }
}
