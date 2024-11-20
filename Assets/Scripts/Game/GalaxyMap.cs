using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

/// <summary>
/// Represents a galaxy map in the game world. A galaxy map is a collection of planet systems.
/// </summary>
public class GalaxyMap : ContainerNode
{
    // Child Nodes
    public List<PlanetSystem> PlanetSystems { get; set; } = new List<PlanetSystem>();

    /// <summary>
    /// Default constructor.
    /// </summary>
    public GalaxyMap() { }

    /// <summary>
    /// Finds the closest friendly planet to a given unit, filtered by OwnerInstanceID.
    /// </summary>
    /// <param name="unit">The unit to find the closest friendly planet to.</param>
    /// <returns>The closest friendly planet with matching OwnerInstanceID, or null if no friendly planets are found.</returns>
    public Planet GetClosestFriendlyPlanet(ISceneNode unit)
    {
        // Find the planet that the unit is currently located on.
        Planet currentPlanet = unit.GetParentOfType<Planet>();

        if (currentPlanet == null)
        {
            return null;
        }

        // Filter planets by matching OwnerInstanceID (friendly planets).
        List<Planet> friendlyPlanets = PlanetSystems
            .SelectMany(system => system.Planets)
            .Where(planet => planet.OwnerInstanceID == unit.OwnerInstanceID)
            .ToList();

        // Initialize variables to track the closest planet.
        Planet closestPlanet = null;
        double closestDistance = double.MaxValue;

        // Iterate through friendly planets to find the closest one.
        foreach (var planet in friendlyPlanets)
        {
            double travelDistance = currentPlanet.GetTravelTime(planet);

            if (travelDistance < closestDistance)
            {
                closestDistance = travelDistance;
                closestPlanet = planet;
            }
        }

        return closestPlanet;
    }

    /// <summary>
    /// Adds a child to the node.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    public override void AddChild(ISceneNode child)
    {
        if (child is PlanetSystem planetSystem)
        {
            PlanetSystems.Add(planetSystem);
        }
    }

    /// <summary>
    /// Removes a child from the node.
    /// </summary>
    /// <param name="child">The child node to remove.</param>
    public override void RemoveChild(ISceneNode child)
    {
        if (child is PlanetSystem planetSystem)
        {
            PlanetSystems.Remove(planetSystem);
        }
    }

    /// <summary>
    /// Retrieves the children of the node.
    /// </summary>
    /// <returns>An array of child nodes.</returns>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        return PlanetSystems.ToArray();
    }
}
