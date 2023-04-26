using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///
/// </summary>
public class PlanetSystemGenerator
    : UnitGenerator,
        IUnitSelector<PlanetSystem>,
        IUnitDecorator<PlanetSystem>
{
    /// <summary>
    /// Default constructor, constructs a PlanetSystemGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="config">The Config containing new game configurations and settings.</param>
    public PlanetSystemGenerator(GameSummary summary, Config config)
        : base(summary, config) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="parentSystem"></param>
    /// <param name="planet"></param>
    private void setResources(PlanetSystem parentSystem, Planet planet)
    {
        string resourceAvailability = GetGameSummary().ResourceAvailability.ToString();
        IConfig planetConfig = GetConfig().GetValue<IConfig>($"Planets.ResourceAvailability.{resourceAvailability}");
        string systemType = parentSystem.SystemType.ToString();

        var (groundSlotRange, orbitSlotRange, resourceRange) = (
            planetConfig.GetValue<int[]>($"{systemType}.GroundSlotRange"),
            planetConfig.GetValue<int[]>($"{systemType}.OrbitSlotRange"),
            planetConfig.GetValue<int[]>($"{systemType}.ResourceRange")
        );

        planet.OrbitSlots = Random.Range(orbitSlotRange[0], orbitSlotRange[1]);
        planet.GroundSlots = Random.Range(groundSlotRange[0], groundSlotRange[1]);
        planet.NumResourceNodes = Random.Range(resourceRange[0], resourceRange[1]);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="parentsystem"></param>
    /// <param name="planet"></param>
    private void setColonizationStatus(PlanetSystem parentsystem, Planet planet)
    {
        double colonizationRate = GetConfig().GetValue<double>("Planets.InitialColonizationRate");
        if (parentsystem.SystemType == PlanetSystemType.CoreSystem)
        {
            planet.IsColonized = true;
        }
        else
        {
            // Allow this to be overridden from planet data.
            // For example, OuterRim Bespin is always colonized.
            if (!planet.IsColonized)
            {
                if (Random.value < colonizationRate)
                {
                    planet.IsColonized = true;
                }
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public IUnitSelectionResult<PlanetSystem> SelectUnits(PlanetSystem[] units)
    {
        GameSize galaxySize = GetGameSummary().GalaxySize;
        List<PlanetSystem> galaxyMap = new List<PlanetSystem>();

        // Subsequent GameSizes include the previous values.
        // Therefore, we must include them in our calculations.
        IEnumerable<int> sizeRange = Enumerable.Range((int)GameSize.Small, (int)galaxySize);

        foreach (PlanetSystem planetSystem in units)
        {
            if (sizeRange.Contains((int)planetSystem.Visibility))
            {
                galaxyMap.Add(planetSystem);
            }
        }

        return new UnitSelectionResult<PlanetSystem>(galaxyMap.ToArray(), units.ToArray());
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public PlanetSystem[] DecorateUnits(PlanetSystem[] units)
    {
        IConfig planetConfig = GetConfig().GetValue<IConfig>("Planets.ResourceAvailability");

        foreach (PlanetSystem planetSystem in units)
        {
            foreach (Planet planet in planetSystem.Planets)
            {
                setResources(planetSystem, planet);
                setColonizationStatus(planetSystem, planet);
            }
        }

        return units;
    }
}
