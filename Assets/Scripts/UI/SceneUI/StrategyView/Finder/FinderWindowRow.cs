using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Represents one domain-backed Finder result and its destination window.
/// </summary>
public sealed class FinderWindowRow
{
    /// <summary>
    /// Creates an immutable Finder result.
    /// </summary>
    /// <param name="name">The displayed result name.</param>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="targetIcon">The destination planet-window type.</param>
    /// <param name="node">The represented domain node.</param>
    /// <param name="fleet">The optional containing fleet.</param>
    /// <param name="mission">The optional containing mission.</param>
    /// <param name="counts">The optional aggregated unit counts.</param>
    public FinderWindowRow(
        string name,
        GalaxyMapPlanet planet,
        PlanetIcon targetIcon = PlanetIcon.None,
        ISceneNode node = null,
        Fleet fleet = null,
        Mission mission = null,
        IReadOnlyList<int> counts = null
    )
    {
        Name = name ?? string.Empty;
        Planet = planet;
        TargetIcon = targetIcon;
        Node = node;
        Fleet = fleet;
        Mission = mission;
        Counts = CopyCounts(counts);
    }

    /// <summary>
    /// Gets the stable domain identity represented by the row.
    /// </summary>
    public string Identity => Node?.InstanceID ?? Planet?.Planet?.InstanceID ?? string.Empty;

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the planet.
    /// </summary>
    public GalaxyMapPlanet Planet { get; }

    /// <summary>
    /// Gets the target icon.
    /// </summary>
    public PlanetIcon TargetIcon { get; }

    /// <summary>
    /// Gets the node.
    /// </summary>
    public ISceneNode Node { get; }

    /// <summary>
    /// Gets the fleet.
    /// </summary>
    public Fleet Fleet { get; }

    /// <summary>
    /// Gets the mission.
    /// </summary>
    public Mission Mission { get; }

    /// <summary>
    /// Gets the counts.
    /// </summary>
    public IReadOnlyList<int> Counts { get; }

    /// <summary>
    /// Gets the owner faction ID.
    /// </summary>
    public string OwnerFactionId => Node?.OwnerInstanceID ?? Planet?.Planet?.OwnerInstanceID;

    /// <summary>
    /// Copies optional count values into an isolated read-only collection.
    /// </summary>
    /// <param name="counts">The count values to copy.</param>
    /// <returns>An immutable count collection.</returns>
    private static IReadOnlyList<int> CopyCounts(IReadOnlyList<int> counts)
    {
        if (counts == null || counts.Count == 0)
            return Array.Empty<int>();

        int[] copy = new int[counts.Count];
        for (int i = 0; i < counts.Count; i++)
            copy[i] = counts[i];

        return new ReadOnlyCollection<int>(copy);
    }
}
