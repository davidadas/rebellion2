using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

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
    High
}

/// <summary>
/// Represents a system of planets, which is primarily a collection of planets.
/// </summary>
public class PlanetSystem : SceneNode
{
    // Settings.
    public GameSize Visibility;
    public PlanetSystemType SystemType;
    public PlanetSystemImportance Importance;

    // Child Nodes.
    public List<Planet> Planets = new List<Planet>();

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public PlanetSystem() { }

    /// <summary>
    /// Adds a planet to the planet system.
    /// </summary>
    /// <param name="child">The planet to add.</param>
    protected internal override void AddChild(SceneNode child)
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
    protected internal override void RemoveChild(SceneNode child)
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
    public override IEnumerable<SceneNode> GetChildren()
    {
        return Planets.ToArray();
    }
}
