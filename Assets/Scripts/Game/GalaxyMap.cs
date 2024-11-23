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
