using System;
using System.Collections.Generic;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Describes one available strategy entity shown in the idle bar.
/// </summary>
internal sealed class IdleBarEntry
{
    /// <summary>Gets the represented strategy entity.</summary>
    internal ISceneNode Entity { get; }

    /// <summary>Gets the entry's display name.</summary>
    internal string Name { get; }

    /// <summary>Gets the resolved portrait texture.</summary>
    internal Texture2D Texture { get; }

    /// <summary>Gets whether the entry belongs to the current idle-bar selection.</summary>
    internal bool Selected { get; }

    /// <summary>
    /// Creates one idle-bar entry.
    /// </summary>
    /// <param name="entity">The represented strategy entity.</param>
    /// <param name="texture">The resolved portrait texture.</param>
    /// <param name="selected">Whether the entry belongs to the current selection.</param>
    internal IdleBarEntry(ISceneNode entity, Texture2D texture, bool selected = false)
    {
        Entity = entity;
        Name = entity?.GetDisplayName() ?? string.Empty;
        Texture = texture;
        Selected = selected;
    }
}

/// <summary>
/// Contains the complete immutable idle-bar presentation.
/// </summary>
internal sealed class IdleBarRenderData
{
    /// <summary>Gets whether the idle bar is visible.</summary>
    internal bool Visible { get; }

    /// <summary>Gets the ordered entries shown in the idle bar.</summary>
    internal IReadOnlyList<IdleBarEntry> Entries { get; }

    /// <summary>Gets the strategy desktop bounds.</summary>
    internal RectInt DesktopBounds { get; }

    /// <summary>
    /// Creates one idle-bar presentation.
    /// </summary>
    /// <param name="visible">Whether the idle bar is visible.</param>
    /// <param name="entries">The ordered entries to display.</param>
    /// <param name="desktopBounds">The strategy desktop bounds.</param>
    internal IdleBarRenderData(
        bool visible,
        IReadOnlyList<IdleBarEntry> entries,
        RectInt desktopBounds
    )
    {
        Visible = visible;
        Entries = entries ?? Array.Empty<IdleBarEntry>();
        DesktopBounds = desktopBounds;
    }
}
