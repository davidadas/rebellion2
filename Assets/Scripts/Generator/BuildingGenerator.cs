using IEnumerableExtensions;
using System;

/// <summary>
///
/// </summary>
public class BuildingGenerator : UnitGenerator, IUnitRandomizer<Building, PlanetSystem>
{
    /// <summary>
    /// Default constructor, constructs a BuildingGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="config">The Config containing new game configurations and settings.</param>
    public BuildingGenerator(GameSummary summary, IConfig config)
        : base(summary, config) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="buildings"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public PlanetSystem[] RandomizeUnits(Building[] buildings, PlanetSystem[] destinations)
    {
        foreach (PlanetSystem planetSystem in destinations)
        {
            string[] gameIds = GetConfig().GetValue<string[]>("Buildings.InitialBuildings.GameIDs");
            int[] values = GetConfig().GetValue<int[]>("Buildings.InitialBuildings.Frequency");

            string[] clonedGameIds = gameIds.Clone() as string[];

            foreach (Planet planet in planetSystem.Planets)
            {
                clonedGameIds.Shuffle();
                foreach (string buildingGameId in clonedGameIds) { }
            }
        }

        return destinations;
    }
}
