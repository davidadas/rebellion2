using System;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>
/// Projects an animated UI sprite onto an authored surface with matching polygon hit testing.
/// </summary>
public sealed class PerspectiveImage : Image
{
    // Subdivision keeps the standard UI material and limits projective texture interpolation seams.
    private const int _subdivisions = 16;

    [SerializeField]
    private UIQuadrilateral corners = UIQuadrilateral.Unit;

    [SerializeField]
    private UIQuadrilateral hitRegion = UIQuadrilateral.Unit;

    public UIQuadrilateral Corners => corners;
    public UIQuadrilateral HitRegion => hitRegion;

    /// <summary>
    /// Sets the projected texture corners and the visible interactive surface.
    /// </summary>
    /// <param name="textureCorners">The texture corners in normalized image coordinates.</param>
    /// <param name="interactiveSurface">The visible surface in normalized image coordinates.</param>
    public void Configure(UIQuadrilateral textureCorners, UIQuadrilateral interactiveSurface)
    {
        if (!textureCorners.IsValid)
            throw new ArgumentException(
                "Texture corners must form a convex surface.",
                nameof(textureCorners)
            );
        if (!interactiveSurface.IsValid)
            throw new ArgumentException(
                "The interactive surface must be convex.",
                nameof(interactiveSurface)
            );
        corners = textureCorners;
        hitRegion = interactiveSurface;
        SetVerticesDirty();
    }

    /// <summary>
    /// Exports the same mesh used by the live control for native authoring previews.
    /// </summary>
    /// <param name="destination">The caller-owned mesh to overwrite.</param>
    public void WriteMesh(Mesh destination)
    {
        using (VertexHelper vertices = new VertexHelper())
        {
            OnPopulateMesh(vertices);
            vertices.FillMesh(destination);
        }
    }

    /// <summary>
    /// Rejects pointer positions outside the visible projected surface.
    /// </summary>
    /// <param name="screenPoint">The pointer position in screen coordinates.</param>
    /// <param name="eventCamera">The canvas event camera, or null for an overlay.</param>
    /// <returns>Whether the pointer lies inside the interactive polygon.</returns>
    public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPoint,
                eventCamera,
                out Vector2 local
            )
        )
            return false;
        Rect bounds = GetPixelAdjustedRect();
        if (bounds.width <= 0 || bounds.height <= 0)
            return false;
        return hitRegion.Contains(
            new Vector2((local.x - bounds.x) / bounds.width, (local.y - bounds.y) / bounds.height)
        );
    }

    /// <summary>
    /// Generates a subdivided projective mesh while preserving sprite-swap animation and tint.
    /// </summary>
    /// <param name="vertices">The native canvas mesh to populate.</param>
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        if (!corners.IsValid)
            return;
        Rect bounds = GetPixelAdjustedRect();
        Matrix4x4 projection = corners.ToMatrix();
        Sprite active = overrideSprite;
        Vector4 uv = active == null ? new Vector4(0, 0, 1, 1) : DataUtility.GetOuterUV(active);
        for (int row = 0; row <= _subdivisions; row++)
        {
            for (int column = 0; column <= _subdivisions; column++)
            {
                Vector2 sample = new Vector2(
                    (float)column / _subdivisions,
                    (float)row / _subdivisions
                );
                Vector2 position = projection.MultiplyPoint(sample);
                vertices.AddVert(
                    new Vector3(
                        bounds.x + position.x * bounds.width,
                        bounds.y + position.y * bounds.height
                    ),
                    color,
                    new Vector2(Mathf.Lerp(uv.x, uv.z, sample.x), Mathf.Lerp(uv.y, uv.w, sample.y))
                );
                if (row == _subdivisions || column == _subdivisions)
                    continue;
                int start = row * (_subdivisions + 1) + column;
                vertices.AddTriangle(start, start + _subdivisions + 1, start + 1);
                vertices.AddTriangle(
                    start + 1,
                    start + _subdivisions + 1,
                    start + _subdivisions + 2
                );
            }
        }
    }
}
