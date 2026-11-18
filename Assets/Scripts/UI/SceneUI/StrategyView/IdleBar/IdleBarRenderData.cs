using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Describes one available strategy entity shown in the idle bar.
/// </summary>
internal sealed class IdleBarEntry
{
    internal ISceneNode Entity { get; }

    internal string Name { get; }

    internal string HoverText { get; }

    internal string GroupKey { get; }

    internal Texture2D Texture { get; }

    /// <summary>
    /// Creates one idle-bar entry.
    /// </summary>
    /// <param name="entity">The represented strategy entity.</param>
    /// <param name="texture">The resolved portrait texture.</param>
    internal IdleBarEntry(ISceneNode entity, Texture2D texture)
    {
        Entity = entity;
        Name = entity?.GetDisplayName() ?? string.Empty;
        Planet planet = entity is Planet ? null : entity?.GetParentOfType<Planet>();
        string planetName = planet?.GetDisplayName();
        HoverText =
            string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(planetName)
                ? Name
                : $"{Name} ({planetName})";
        GroupKey = CreateGroupKey(entity, planet);
        Texture = texture;
    }

    /// <summary>
    /// Creates a stable presentation key for identical special-forces units at one planet.
    /// </summary>
    /// <param name="entity">The represented strategy entity.</param>
    /// <param name="planet">The entity's parent planet, when present.</param>
    /// <returns>The presentation key, or null when the entry cannot be grouped.</returns>
    private static string CreateGroupKey(ISceneNode entity, Planet planet)
    {
        if (
            entity is not SpecialForces
            || string.IsNullOrEmpty(entity.GetTypeID())
            || string.IsNullOrEmpty(planet?.InstanceID)
        )
            return null;

        return $"{planet.InstanceID}\u001f{entity.GetTypeID()}";
    }
}

/// <summary>
/// Contains the complete immutable idle-bar presentation.
/// </summary>
internal sealed class IdleBarRenderData
{
    internal bool Visible { get; }

    internal IReadOnlyList<IdleBarEntry> Entries { get; }

    internal RectInt DesktopBounds { get; }

    internal bool AlwaysOpen { get; }

    internal IdleBarLocation Location { get; }

    /// <summary>
    /// Creates one idle-bar presentation.
    /// </summary>
    /// <param name="visible">Whether the idle bar is visible.</param>
    /// <param name="entries">The ordered entries to display.</param>
    /// <param name="desktopBounds">The strategy desktop bounds.</param>
    /// <param name="alwaysOpen">Whether the idle bar remains expanded without pointer hover.</param>
    /// <param name="location">The strategy-desktop corner occupied by the idle bar.</param>
    internal IdleBarRenderData(
        bool visible,
        IReadOnlyList<IdleBarEntry> entries,
        RectInt desktopBounds,
        bool alwaysOpen = false,
        IdleBarLocation location = IdleBarLocation.TopRight
    )
    {
        Visible = visible;
        Entries = entries ?? Array.Empty<IdleBarEntry>();
        DesktopBounds = desktopBounds;
        AlwaysOpen = alwaysOpen;
        Location = location;
    }
}
