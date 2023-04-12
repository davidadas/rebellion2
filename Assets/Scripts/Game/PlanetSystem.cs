using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;

/// <summary>
/// Determines this PlanetSystem's appearance per "Game Size" setting.
/// </summary>
public enum PlanetSystemVisibility
{
    Small,
    Medium,
    Large,
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

public class PlanetSystem : GameNode
{
    // Settings.
    public bool IsColonized;
    public PlanetSystemVisibility Visibility;
    public PlanetSystemImportance Importance;

    // Child Nodes.
    public List<Planet> Planets = new List<Planet>();

    /// <summary>
    ///
    /// </summary>
    public PlanetSystem() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        return Planets.ToArray();
    }
}
