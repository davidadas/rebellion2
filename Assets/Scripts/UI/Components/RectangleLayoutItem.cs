using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Describes a movable integer-coordinate anchor and the rectangles attached to it.
/// </summary>
public sealed class RectangleLayoutItem
{
    public Vector2Int Position { get; }
    public IReadOnlyList<Rect> Footprints { get; }

    /// <summary>
    /// Captures a rigid compound footprint independently of its rendered controls.
    /// </summary>
    /// <param name="position">The desired integer-coordinate anchor.</param>
    /// <param name="footprints">The occupied rectangles relative to that anchor.</param>
    public RectangleLayoutItem(Vector2Int position, IEnumerable<Rect> footprints)
    {
        Position = position;
        Footprints = Array.AsReadOnly(
            (footprints ?? throw new ArgumentNullException(nameof(footprints))).ToArray()
        );
    }
}
