using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using ICollectionExtensions;
using IEnumerableExtensions;

/// <summary>
///
/// </summary>
public class BuildingGenerator : UnitGenerator<Building>
{
    /// <summary>
    /// Default constructor, constructs a BuildingGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public BuildingGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    /// Creates a Dictionary out of the frequency data stored in the new game config.
    /// </summary>
    /// <returns>A Dictionary with building GameIDs as keys as its percentage frequency.</returns>
    public Dictionary<string, double> getConfigMapping()
    {
        IConfig config = GetConfig();
        string[] gameIds = config.GetValue<string[]>("Buildings.InitialBuildings.GameIDs");
        double[] frequencies = config.GetValue<double[]>("Buildings.InitialBuildings.Frequency");
        Dictionary<string, double> configMapping = new Dictionary<string, double>();

        // Map each building's GameID with its percentage frequency, represented as a double.
        for (int i = 0; i < gameIds.Length; i++)
        {
            configMapping[gameIds[i]] = frequencies[i];
        }

        return configMapping;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="buildings"></param>
    /// <returns></returns>
    public override Building[] SelectUnits(Building[] buildings)
    {
        int startingResearchLevel = this.GetGameSummary().StartingResearchLevel;
        Building[] selectedBuildings = buildings
            .Where(
                (building) =>
                    building.RequiredResearchLevel <= this.GetGameSummary().StartingResearchLevel
            )
            .ToArray();
        return selectedBuildings;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="buildings"></param>
    /// <returns></returns>
    public override Building[] DecorateUnits(Building[] buildings)
    {
        // No op.
        return buildings;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="buildings"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public override Building[] DeployUnits(Building[] buildings, PlanetSystem[] destinations)
    {
        Dictionary<string, double> configMapping = getConfigMapping();
        List<Building> buildingList = buildings.ToList();
        List<Building> deployedBuildings = new List<Building>();

        foreach (PlanetSystem planetSystem in destinations)
        {
            // Only add buildings to populated planets.
            IEnumerable<Planet> colonizedPlanets = planetSystem.Planets.Where(
                planet => planet.IsColonized
            );

            // Generate the planet's initial buildings.
            foreach (Planet planet in colonizedPlanets)
            {
                // Shuffle the array to randomize the priority.
                foreach (string buildingGameId in configMapping.Keys.ToArray().Shuffle())
                {
                    Building building = buildingList.Find(
                        building => building.GameID == buildingGameId
                    );
                    int numAvailableSlots = planet.GetAvailableSlots(building.Slot);

                    if (numAvailableSlots == 0)
                        continue;

                    // Create an array of buildings and fill it with the current building type.
                    Building[] filledBuildings = new Building[numAvailableSlots];
                    Array.Fill(filledBuildings, building);

                    // Add this building each time its frequency exceeds a random value.
                    // Halt this process after the first failure, as frequency is calculated per system.
                    IEnumerable<Building> initialBuildings = filledBuildings.TakeWhile(
                        x => UnityEngine.Random.value < configMapping[buildingGameId]
                    );

                    // Add the generated buildings to the planet.
                    planet.AddBuildings(initialBuildings.ToArray());
                    deployedBuildings.AddAll(initialBuildings);
                }
            }
        }

        return deployedBuildings.ToArray();
    }
}
