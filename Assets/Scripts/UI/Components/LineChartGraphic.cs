using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a normalized polyline over a quiet chart grid using ordinary UI geometry.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LineChartGraphic : MaskableGraphic
{
    private Vector2[] points = Array.Empty<Vector2>();

    /// <summary>
    /// Replaces the chart's normalized points without retaining a caller's mutable collection.
    /// </summary>
    /// <param name="values">The ordered points within the unit square.</param>
    public void Render(IReadOnlyList<Vector2> values)
    {
        points = values.ToArray();
        raycastTarget = false;
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds independent grid segments and the supplied line in the chart rectangle.
    /// </summary>
    /// <param name="helper">The target UI mesh.</param>
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect area = rectTransform.rect;
        Color grid = color;
        grid.a *= 0.22f;
        for (int i = 0; i <= 4; i++)
        {
            float fraction = i / 4f;
            AddLine(helper, area, new Vector2(0, fraction), new Vector2(1, fraction), grid, 1);
            AddLine(helper, area, new Vector2(fraction, 0), new Vector2(fraction, 1), grid, 1);
        }
        for (int i = 1; i < points.Length; i++)
            AddLine(helper, area, points[i - 1], points[i], color, 2);
    }

    /// <summary>
    /// Adds one line segment as a quad, safely skipping zero-length joins.
    /// </summary>
    /// <param name="helper">The target UI mesh.</param>
    /// <param name="area">The local plot area.</param>
    /// <param name="start">The normalized segment start.</param>
    /// <param name="end">The normalized segment end.</param>
    /// <param name="tint">The segment color.</param>
    /// <param name="width">The segment width in UI units.</param>
    private static void AddLine(
        VertexHelper helper,
        Rect area,
        Vector2 start,
        Vector2 end,
        Color tint,
        float width
    )
    {
        Vector2 a = area.min + Vector2.Scale(start, area.size);
        Vector2 b = area.min + Vector2.Scale(end, area.size);
        Vector2 direction = b - a;
        if (direction.sqrMagnitude < 0.0001f)
            return;
        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        int first = helper.currentVertCount;
        foreach (Vector2 point in new[] { a - normal, a + normal, b + normal, b - normal })
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = point;
            vertex.color = tint;
            helper.AddVert(vertex);
        }
        helper.AddTriangle(first, first + 1, first + 2);
        helper.AddTriangle(first, first + 2, first + 3);
    }
}
