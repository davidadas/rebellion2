using System;
using System.Collections.Generic;
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

    internal Texture2D Texture { get; }

    internal ManufacturingType? ManufacturingLane { get; }

    /// <summary>
    /// Creates one idle-bar entry.
    /// </summary>
    /// <param name="entity">The represented strategy entity.</param>
    /// <param name="texture">The resolved portrait texture.</param>
    /// <param name="manufacturingLane">The represented manufacturing lane, when applicable.</param>
    internal IdleBarEntry(
        ISceneNode entity,
        Texture2D texture,
        ManufacturingType? manufacturingLane = null
    )
    {
        Entity = entity;
        ManufacturingLane = manufacturingLane;
        string entityName = entity?.GetDisplayName() ?? string.Empty;
        Name = manufacturingLane.HasValue
            ? $"{entityName} ({GetManufacturingLaneName(manufacturingLane.Value)})"
            : entityName;
        Texture = texture;
    }

    /// <summary>
    /// Names the manufacturing capacity represented by an idle-bar entry.
    /// </summary>
    /// <param name="type">The represented manufacturing lane.</param>
    /// <returns>The singular lane name displayed beside its planet.</returns>
    private static string GetManufacturingLaneName(ManufacturingType type)
    {
        return type switch
        {
            ManufacturingType.Ship => "Shipyard",
            ManufacturingType.Troop => "Training Facility",
            ManufacturingType.Building => "Construction Yard",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
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
