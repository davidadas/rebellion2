using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a galaxy map in the game world. A galaxy map is a collection of planet systems.
/// </summary>
public class GalaxyMap : SceneNode
{
    // Child Nodes
    public List<PlanetSystem> PlanetSystems = new List<PlanetSystem>();
    
    /// <summary>
    /// Default constructor.
    /// </summary>
    public GalaxyMap() {}

    /// <summary>
    /// Adds a child to the node.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    protected internal override void AddChild(SceneNode child)
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
    protected internal override void RemoveChild(SceneNode child)
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
    public override IEnumerable<SceneNode> GetChildren()
    {
        return PlanetSystems.ToArray();
    }
}
