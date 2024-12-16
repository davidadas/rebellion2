using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Responsible for generating and deploying planet systems to the scene graph.
/// </summary>
public class PlanetSystemGenerator : UnitGenerator<PlanetSystem>
{
    /// <summary>
    /// Default constructor, constructs a PlanetSystemGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public PlanetSystemGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="parentSystem"></param>
    /// <param name="planet"></param>
    private void SetResources(PlanetSystem parentSystem, Planet planet)
    {
        string resourceAvailability = GetGameSummary().ResourceAvailability.ToString();
        IConfig planetConfig = GetConfig()
            .GetValue<IConfig>($"Planets.ResourceAvailability.{resourceAvailability}");
        string systemType = parentSystem.SystemType.ToString();

        var (groundSlotRange, orbitSlotRange, resourceRange) = (
            planetConfig.GetValue<int[]>($"{systemType}.GroundSlotRange"),
            planetConfig.GetValue<int[]>($"{systemType}.OrbitSlotRange"),
            planetConfig.GetValue<int[]>($"{systemType}.ResourceRange")
        );

        planet.OrbitSlots = Random.Range(orbitSlotRange[0], orbitSlotRange[1]);
        planet.GroundSlots = Random.Range(groundSlotRange[0], groundSlotRange[1]);
        planet.NumRawResourceNodes = Random.Range(resourceRange[0], resourceRange[1]);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="parentsystem"></param>
    /// <param name="planet"></param>
    private void SetColonizationStatus(PlanetSystem parentsystem, Planet planet)
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
    public override PlanetSystem[] SelectUnits(PlanetSystem[] units)
    {
        int galaxySize = (int)GetGameSummary().GalaxySize;
        List<PlanetSystem> galaxyMap = new List<PlanetSystem>();

        // Select planet systems with visibility greater than or equal to the galaxy size.
        foreach (PlanetSystem planetSystem in units)
        {
            int visibilityValue = (int)planetSystem.Visibility;

            // Add the planet system to the galaxy map if it is visible.
            if (visibilityValue <= galaxySize)
            {
                galaxyMap.Add(planetSystem);
            }
        }

        return galaxyMap.ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public override PlanetSystem[] DecorateUnits(PlanetSystem[] units)
    {
        IConfig planetConfig = GetConfig().GetValue<IConfig>("Planets.ResourceAvailability");

        foreach (PlanetSystem planetSystem in units)
        {
            foreach (Planet planet in planetSystem.Planets)
            {
                SetResources(planetSystem, planet);
                SetColonizationStatus(planetSystem, planet);
            }
        }

        return units;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public override PlanetSystem[] DeployUnits(PlanetSystem[] units, PlanetSystem[] destinations)
    {
        // No op.
        return units;
    }
}
