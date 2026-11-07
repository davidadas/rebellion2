using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws projected selection brackets, health bars, navigation points, and transient weapon trails.
/// </summary>
public sealed class TacticalCombatOverlayGraphic : MaskableGraphic
{
    private readonly List<Stroke> _strokes = new();
    private readonly List<Marker> _markers = new();
    public Rect? SelectionRectangle { get; set; }

    /// <summary>
    /// Begins a new projection frame while retaining allocated drawing buffers.
    /// </summary>
    public void BeginFrame()
    {
        _strokes.Clear();
        _markers.Clear();
    }

    /// <summary>
    /// Adds a viewport-normalized marker for a visible battle unit.
    /// </summary>
    /// <param name="point">The projected point.</param>
    /// <param name="tint">The ownership or selection color.</param>
    /// <param name="selected">Whether to draw the full brackets and condition bars.</param>
    /// <param name="hull">The surviving hull fraction.</param>
    /// <param name="shields">The surviving shield fraction.</param>
    public void AddMarker(Vector2 point, Color tint, bool selected, float hull, float shields)
    {
        _markers.Add(new Marker(point, tint, selected, hull, shields));
    }

    /// <summary>
    /// Adds a viewport-normalized line segment.
    /// </summary>
    /// <param name="start">The first endpoint.</param>
    /// <param name="end">The second endpoint.</param>
    /// <param name="tint">The line color.</param>
    public void AddStroke(Vector2 start, Vector2 end, Color tint) =>
        _strokes.Add(new Stroke(start, end, tint));

    /// <summary>
    /// Invalidates the mesh after the controller finishes a projection frame.
    /// </summary>
    public void EndFrame() => SetVerticesDirty();

    /// <summary>
    /// Builds the current overlay using one UI mesh and no per-unit scene objects.
    /// </summary>
    /// <param name="vertexHelper">The target UI mesh builder.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        foreach (Stroke stroke in _strokes)
            AddLine(vertexHelper, ToLocal(stroke.Start), ToLocal(stroke.End), stroke.Tint, 1.2f);
        foreach (Marker marker in _markers)
            DrawMarker(vertexHelper, marker);
        if (SelectionRectangle.HasValue)
        {
            Rect selection = SelectionRectangle.Value;
            AddRectangle(
                vertexHelper,
                Rect.MinMaxRect(
                    ToLocal(selection.min).x,
                    ToLocal(selection.min).y,
                    ToLocal(selection.max).x,
                    ToLocal(selection.max).y
                ),
                Color.white
            );
        }
    }

    /// <summary>
    /// Draws one marker and the selected unit's two condition bars.
    /// </summary>
    /// <param name="mesh">The mesh builder.</param>
    /// <param name="marker">The projected unit condition.</param>
    private void DrawMarker(VertexHelper mesh, Marker marker)
    {
        Vector2 point = ToLocal(marker.Point);
        float radius = marker.Selected ? 10 : 3;
        AddRectangle(
            mesh,
            new Rect(point.x - radius, point.y - radius, radius * 2, radius * 2),
            marker.Tint
        );
        if (!marker.Selected)
            return;
        AddLine(
            mesh,
            point + new Vector2(-10, -15),
            point + new Vector2(-10 + 20 * Mathf.Clamp01(marker.Hull), -15),
            Color.green,
            2
        );
        AddLine(
            mesh,
            point + new Vector2(-10, -19),
            point + new Vector2(-10 + 20 * Mathf.Clamp01(marker.Shields), -19),
            Color.cyan,
            2
        );
    }

    /// <summary>
    /// Converts normalized viewport coordinates into this graphic's local rectangle.
    /// </summary>
    /// <param name="point">The normalized viewport point.</param>
    /// <returns>The local UI position.</returns>
    private Vector2 ToLocal(Vector2 point) =>
        rectTransform.rect.min + Vector2.Scale(rectTransform.rect.size, point);

    /// <summary>
    /// Adds a four-sided outline.
    /// </summary>
    /// <param name="mesh">The mesh builder.</param>
    /// <param name="rect">The local rectangle.</param>
    /// <param name="tint">The outline color.</param>
    private static void AddRectangle(VertexHelper mesh, Rect rect, Color tint)
    {
        AddLine(mesh, rect.min, new Vector2(rect.xMin, rect.yMax), tint, 1);
        AddLine(mesh, new Vector2(rect.xMin, rect.yMax), rect.max, tint, 1);
        AddLine(mesh, rect.max, new Vector2(rect.xMax, rect.yMin), tint, 1);
        AddLine(mesh, new Vector2(rect.xMax, rect.yMin), rect.min, tint, 1);
    }

    /// <summary>
    /// Adds one screen-space line quad.
    /// </summary>
    /// <param name="mesh">The mesh builder.</param>
    /// <param name="start">The first endpoint.</param>
    /// <param name="end">The second endpoint.</param>
    /// <param name="tint">The line color.</param>
    /// <param name="width">The line width.</param>
    private static void AddLine(
        VertexHelper mesh,
        Vector2 start,
        Vector2 end,
        Color tint,
        float width
    )
    {
        Vector2 direction = (end - start).normalized;
        Vector2 offset = new Vector2(-direction.y, direction.x) * (width * 0.5f);
        int first = mesh.currentVertCount;
        mesh.AddVert(start - offset, tint, Vector2.zero);
        mesh.AddVert(start + offset, tint, Vector2.zero);
        mesh.AddVert(end + offset, tint, Vector2.zero);
        mesh.AddVert(end - offset, tint, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first, first + 2, first + 3);
    }

    private readonly struct Stroke
    {
        public Vector2 Start { get; }
        public Vector2 End { get; }
        public Color Tint { get; }

        /// <summary>
        /// Captures one projected line.
        /// </summary>
        /// <param name="start">The first endpoint.</param>
        /// <param name="end">The second endpoint.</param>
        /// <param name="tint">The line color.</param>
        public Stroke(Vector2 start, Vector2 end, Color tint)
        {
            Start = start;
            End = end;
            Tint = tint;
        }
    }

    private readonly struct Marker
    {
        public Vector2 Point { get; }
        public Color Tint { get; }
        public bool Selected { get; }
        public float Hull { get; }
        public float Shields { get; }

        /// <summary>
        /// Captures a unit's projected marker.
        /// </summary>
        /// <param name="point">The projected position.</param>
        /// <param name="tint">The marker color.</param>
        /// <param name="selected">Whether the unit is selected.</param>
        /// <param name="hull">The hull fraction.</param>
        /// <param name="shields">The shield fraction.</param>
        public Marker(Vector2 point, Color tint, bool selected, float hull, float shields)
        {
            Point = point;
            Tint = tint;
            Selected = selected;
            Hull = hull;
            Shields = shields;
        }
    }
}
