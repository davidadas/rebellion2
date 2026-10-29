using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a resolution-independent open or closed padlock using the current menu text colour.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MenuLockIcon : MaskableGraphic
{
    [SerializeField]
    private bool locked = true;

    /// <summary>
    /// Gets or sets whether the icon represents a password-protected room.
    /// </summary>
    public bool Locked
    {
        get => locked;
        set
        {
            if (locked == value)
                return;
            locked = value;
            SetVerticesDirty();
        }
    }

    /// <summary>
    /// Draws the shared lock outline without requiring a platform font glyph or bitmap.
    /// </summary>
    /// <param name="vertices">The native UI mesh to populate.</param>
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        Quad(vertices, new Rect(0.18f, 0.08f, 0.64f, 0.08f));
        Quad(vertices, new Rect(0.18f, 0.48f, 0.64f, 0.08f));
        Quad(vertices, new Rect(0.18f, 0.08f, 0.08f, 0.48f));
        Quad(vertices, new Rect(0.74f, 0.08f, 0.08f, 0.48f));
        Quad(vertices, new Rect(0.46f, 0.25f, 0.08f, 0.14f));
        float centerX = locked ? 0.5f : 0.58f;
        float centerY = locked ? 0.61f : 0.71f;
        for (int index = 0; index < 12; index++)
        {
            float first = index * Mathf.PI / 12f;
            float second = (index + 1) * Mathf.PI / 12f;
            PointQuad(
                vertices,
                new Vector2(centerX + Mathf.Cos(first) * 0.24f, centerY + Mathf.Sin(first) * 0.25f),
                new Vector2(
                    centerX + Mathf.Cos(second) * 0.24f,
                    centerY + Mathf.Sin(second) * 0.25f
                ),
                new Vector2(
                    centerX + Mathf.Cos(second) * 0.16f,
                    centerY + Mathf.Sin(second) * 0.17f
                ),
                new Vector2(centerX + Mathf.Cos(first) * 0.16f, centerY + Mathf.Sin(first) * 0.17f)
            );
        }
        Quad(vertices, new Rect(centerX + 0.16f, 0.48f, 0.08f, centerY - 0.48f));
        Quad(
            vertices,
            new Rect(
                centerX - 0.24f,
                locked ? 0.48f : 0.66f,
                0.08f,
                centerY - (locked ? 0.48f : 0.66f)
            )
        );
    }

    /// <summary>
    /// Appends an axis-aligned segment in normalized icon coordinates.
    /// </summary>
    /// <param name="vertices">The destination mesh.</param>
    /// <param name="bounds">The segment bounds.</param>
    private void Quad(VertexHelper vertices, Rect bounds)
    {
        PointQuad(
            vertices,
            new Vector2(bounds.xMin, bounds.yMin),
            new Vector2(bounds.xMin, bounds.yMax),
            new Vector2(bounds.xMax, bounds.yMax),
            new Vector2(bounds.xMax, bounds.yMin)
        );
    }

    /// <summary>
    /// Appends a coloured quadrilateral scaled to the current UI rectangle.
    /// </summary>
    /// <param name="vertices">The destination mesh.</param>
    /// <param name="first">The first normalized corner.</param>
    /// <param name="second">The second normalized corner.</param>
    /// <param name="third">The third normalized corner.</param>
    /// <param name="fourth">The fourth normalized corner.</param>
    private void PointQuad(
        VertexHelper vertices,
        Vector2 first,
        Vector2 second,
        Vector2 third,
        Vector2 fourth
    )
    {
        Rect bounds = GetPixelAdjustedRect();
        int start = vertices.currentVertCount;
        foreach (Vector2 point in new[] { first, second, third, fourth })
            vertices.AddVert(
                new Vector3(
                    bounds.xMin + point.x * bounds.width,
                    bounds.yMin + point.y * bounds.height
                ),
                color,
                Vector2.zero
            );
        vertices.AddTriangle(start, start + 1, start + 2);
        vertices.AddTriangle(start, start + 2, start + 3);
    }
}
