using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;

/// <summary>
/// The type of planet system (core system or outer rim).
/// </summary>
public enum PlanetSystemType
{
    CoreSystem,
    OuterRim,
}

/// <summary>
/// A carry-over from the original Rebellion.
/// Frankly, I have no idea what this does.
/// </summary>
public enum PlanetSystemImportance
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Represents a system of planets, which is primarily a collection of planets.
/// </summary>
[PersistableObject]
public class PlanetSystem : ContainerNode
{
    // Planet System Properties
    public GameSize Visibility { get; set; }
    public PlanetSystemType SystemType { get; set; }
    public PlanetSystemImportance Importance { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // Child Nodes
    public List<Planet> Planets { get; set; } = new List<Planet>();

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public PlanetSystem() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public Point GetPosition()
    {
        return new Point(PositionX, PositionY);
    }

    /// <summary>
    /// Adds a planet to the planet system.
    /// </summary>
    /// <param name="child">The planet to add.</param>
    public override void AddChild(ISceneNode child)
    {
        if (child is Planet planet)
        {
            Planets.Add(planet);
        }
    }

    /// <summary>
    /// Removes a planet from the planet system.
    /// </summary>
    /// <param name="child">The planet to remove.</param>
    public override void RemoveChild(ISceneNode child)
    {
        if (child is Planet planet)
        {
            Planets.Remove(planet);
        }
    }

    /// <summary>
    /// Returns the planets in the planet system.
    /// </summary>
    /// <returns>The planets in the planet system.</returns>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        return Planets.ToArray();
    }
}
