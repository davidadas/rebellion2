using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

/// <summary>
/// Resolves the ordered facility inventory represented by a facility-window tab.
/// </summary>
internal static class FacilityWindowInventory
{
    /// <summary>
    /// Gets the facilities displayed by one inventory tab.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="tab">The inventory tab.</param>
    /// <returns>The matching facilities in display order.</returns>
    internal static List<Building> GetItems(Planet planet, FacilityWindowTab tab)
    {
        if (planet == null)
            return new List<Building>();

        BuildingType type = tab switch
        {
            FacilityWindowTab.Shipyards => BuildingType.Shipyard,
            FacilityWindowTab.Training => BuildingType.TrainingFacility,
            FacilityWindowTab.Construction => BuildingType.ConstructionFacility,
            FacilityWindowTab.Refineries => BuildingType.Refinery,
            FacilityWindowTab.Mines => BuildingType.Mine,
            _ => BuildingType.None,
        };
        return planet
            .Buildings.Where(building => building.GetBuildingType() == type)
            .OrderBy(building => building.GetDisplayName())
            .ToList();
    }
}
