using System;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using IEnumerableExtensions;
using ObjectExtensions;

/// <summary>
/// Responsible for generating and deploying buildings to the scene graph.
/// </summary>
public class BuildingGenerator : UnitGenerator<Building>
{
    /// <summary>
    /// Constructs a BuildingGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public BuildingGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    /// Creates a Dictionary from the frequency data stored in the new game config.
    /// Handles potential duplicate TypeIDs by keeping the entry with the highest frequency.
    /// </summary>
    /// <returns>A Dictionary with building TypeIDs as keys and their percentage frequency as values.</returns>
    private Dictionary<string, double> GetConfigMapping()
    {
        IConfig config = GetConfig();
        string[] typeIds = config.GetValue<string[]>("Buildings.InitialBuildings.TypeIDs");
        double[] frequencies = config.GetValue<double[]>("Buildings.InitialBuildings.Frequency");

        return typeIds
            .Zip(frequencies, (id, freq) => new { Id = id, Frequency = freq })
            .GroupBy(x => x.Id)
            .ToDictionary(group => group.Key, group => group.Max(x => x.Frequency));
    }

    /// <summary>
    /// Deploys buildings to a specific planet.
    /// </summary>
    /// <param name="planet">The planet to deploy buildings to.</param>
    /// <param name="availableBuildings">List of available buildings.</param>
    /// <param name="configMapping">Configuration mapping for building frequencies.</param>
    /// <param name="deployedBuildings">List to store deployed buildings.</param>
    private void DeployBuildingsToPlanet(
        Planet planet,
        List<Building> availableBuildings,
        Dictionary<string, double> configMapping,
        List<Building> deployedBuildings
    )
    {
        foreach (string buildingTypeId in configMapping.Keys.ToArray().Shuffle())
        {
            Building buildingTemplate = availableBuildings.Find(b => b.TypeID == buildingTypeId);
            int availableSlots = planet.GetAvailableSlots(buildingTemplate.GetBuildingSlot());

            if (availableSlots == 0)
                continue;

            List<Building> generatedBuildings = GenerateBuildings(
                buildingTemplate,
                availableSlots,
                planet.GetOwnerInstanceID()
            );
            List<Building> selectedBuildings = SelectBuildingsBasedOnFrequency(
                generatedBuildings,
                configMapping[buildingTypeId]
            );

            selectedBuildings.ForEach(planet.AddChild);
            deployedBuildings.AddRange(selectedBuildings);
        }
    }

    /// <summary>
    /// Generates a list of buildings based on a template and available slots.
    /// </summary>
    /// <param name="template">The building template to copy.</param>
    /// <param name="count">Number of buildings to generate.</param>
    /// <param name="ownerInstanceId">The owner's instance ID.</param>
    /// <returns>A list of generated buildings.</returns>
    private List<Building> GenerateBuildings(Building template, int count, string ownerInstanceId)
    {
        return Enumerable
            .Range(0, count)
            .Select(_ =>
            {
                Building copy = (Building)template.GetDeepCopy();
                copy.SetOwnerInstanceID(ownerInstanceId);
                copy.SetManufacturingStatus(ManufacturingStatus.Complete);
                copy.MovementStatus = MovementStatus.Idle;
                return copy;
            })
            .ToList();
    }

    /// <summary>
    /// Selects buildings based on their frequency.
    /// </summary>
    /// <param name="buildings">List of buildings to select from.</param>
    /// <param name="frequency">The frequency of selection.</param>
    /// <returns>A list of selected buildings.</returns>
    private List<Building> SelectBuildingsBasedOnFrequency(
        List<Building> buildings,
        double frequency
    )
    {
        return buildings.TakeWhile(_ => UnityEngine.Random.value < frequency).ToList();
    }

    /// <summary>
    /// Selects the buildings that can be created based on the starting research level.
    /// </summary>
    /// <param name="buildings">The available buildings.</param>
    /// <returns>An array of selected buildings.</returns>
    public override Building[] SelectUnits(Building[] buildings)
    {
        return buildings
            .Where(building =>
                building.RequiredResearchLevel <= GetGameSummary().StartingResearchLevel
            )
            .ToArray();
    }

    /// <summary>
    /// Decorates the buildings with additional properties or behavior.
    /// </summary>
    /// <param name="buildings">The buildings to decorate.</param>
    /// <returns>The decorated buildings.</returns>
    public override Building[] DecorateUnits(Building[] buildings)
    {
        // No additional decoration needed for buildings.
        return buildings;
    }

    /// <summary>
    /// Deploys the buildings to the specified planet systems.
    /// </summary>
    /// <param name="buildings">The buildings to deploy.</param>
    /// <param name="destinations">The planet systems to deploy the buildings to.</param>
    /// <returns>The deployed buildings.</returns>
    public override Building[] DeployUnits(Building[] buildings, PlanetSystem[] destinations)
    {
        Dictionary<string, double> configMapping = GetConfigMapping();
        List<Building> buildingList = new List<Building>(buildings);
        List<Building> deployedBuildings = new List<Building>();

        foreach (PlanetSystem planetSystem in destinations)
        {
            IEnumerable<Planet> colonizedPlanets = planetSystem.Planets.Where(planet =>
                planet.IsColonized
            );
            foreach (Planet planet in colonizedPlanets)
            {
                DeployBuildingsToPlanet(planet, buildingList, configMapping, deployedBuildings);
            }
        }

        return deployedBuildings.ToArray();
    }
}
