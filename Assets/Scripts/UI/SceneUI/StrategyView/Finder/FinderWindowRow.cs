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

    public string Identity => Node?.InstanceID ?? Planet?.Planet?.InstanceID ?? string.Empty;

    public string Name { get; }

    public GalaxyMapPlanet Planet { get; }

    public PlanetIcon TargetIcon { get; }

    public ISceneNode Node { get; }

    public Fleet Fleet { get; }

    public Mission Mission { get; }

    public IReadOnlyList<int> Counts { get; }

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
