using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Captures map records and their rendered geometry before a background layout operation.
/// </summary>
public sealed class ContentEditorMapLayout
{
    public IReadOnlyList<ContentPackEditorRecord> Records { get; }
    public IReadOnlyList<RectangleLayoutItem> Items { get; }
    public Rect Bounds { get; }
    public Vector2 Gap { get; }
    public Vector2 CoordinateScale { get; }
    public Vector2Int Origin { get; }

    /// <summary>
    /// Captures one coordinate frame and its complete immutable geometry snapshot.
    /// </summary>
    /// <param name="records">The records in the same order as the geometry.</param>
    /// <param name="items">The rigid footprints and desired integer coordinates.</param>
    /// <param name="bounds">The usable authored-coordinate frame.</param>
    /// <param name="gap">The required clearance in authored coordinates.</param>
    /// <param name="coordinateScale">The visual size of an authored coordinate step.</param>
    /// <param name="origin">The authored position corresponding to the layout frame's origin.</param>
    internal ContentEditorMapLayout(
        IEnumerable<ContentPackEditorRecord> records,
        IEnumerable<RectangleLayoutItem> items,
        Rect bounds,
        Vector2 gap,
        Vector2 coordinateScale,
        Vector2Int origin
    )
    {
        Records = Array.AsReadOnly(records.ToArray());
        Items = Array.AsReadOnly(items.ToArray());
        Bounds = bounds;
        Gap = gap;
        CoordinateScale = coordinateScale;
        Origin = origin;
    }
}
