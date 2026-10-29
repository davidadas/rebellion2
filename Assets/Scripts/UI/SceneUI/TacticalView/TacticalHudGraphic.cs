using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flat panels and resolution-independent symbols for the tactical HUD.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TacticalHudGraphic : MaskableGraphic
{
    public enum Artwork
    {
        Housing,
        Well,
        Key,
        Brackets,
        Move,
        Attack,
        Stop,
        Guard,
        Focus,
        Retreat,
        Shield,
        Hull,
        SelectionRectangle,
        Dock,
    }

    [SerializeField]
    private Artwork artwork;

    /// <summary>
    /// Changes the authored vector artwork.
    /// </summary>
    /// <param name="value">The artwork to display.</param>
    public void SetArtwork(Artwork value)
    {
        artwork = value;
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds a flat, thin-bordered panel or a command symbol.
    /// </summary>
    /// <param name="mesh">The canvas mesh to populate.</param>
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (artwork <= Artwork.Key)
        {
            Quad(mesh, r, new Color(0.17f, 0.20f, 0.24f, color.a));
            Quad(mesh, Inset(r, 1f), color);
            return;
        }
        if (artwork == Artwork.Brackets)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = new Vector2(i % 2 == 0 ? r.xMin : r.xMax, i / 2 == 0 ? r.yMin : r.yMax);
                Line(mesh, p, p + new Vector2(i % 2 == 0 ? 12f : -12f, 0f), 1.5f, color);
                Line(mesh, p, p + new Vector2(0f, i / 2 == 0 ? 12f : -12f), 1.5f, color);
            }
            return;
        }
        Vector2 c = r.center;
        if (artwork == Artwork.SelectionRectangle)
        {
            Rect outline = Inset(r, 0.75f);
            Vector2 bottomLeft = outline.min;
            Vector2 topRight = outline.max;
            Vector2 topLeft = new Vector2(outline.xMin, outline.yMax);
            Vector2 bottomRight = new Vector2(outline.xMax, outline.yMin);
            Line(mesh, bottomLeft, topLeft, 1.5f, color);
            Line(mesh, topLeft, topRight, 1.5f, color);
            Line(mesh, topRight, bottomRight, 1.5f, color);
            Line(mesh, bottomRight, bottomLeft, 1.5f, color);
            return;
        }
        float s = Mathf.Min(r.width, r.height) * 0.38f;
        switch (artwork)
        {
            case Artwork.Move:
                Line(mesh, c - Vector2.right * s, c + Vector2.right * s, 2f, color);
                Line(mesh, c - Vector2.up * s, c + Vector2.up * s, 2f, color);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 n = new Vector2(-d.y, d.x);
                    Triangle(
                        mesh,
                        c + d * s,
                        c + d * s * 0.45f + n * s * 0.24f,
                        c + d * s * 0.45f - n * s * 0.24f,
                        color
                    );
                }
                break;
            case Artwork.Attack:
            case Artwork.Focus:
                Ring(mesh, c, s * 0.72f, 1.8f, color);
                Line(mesh, c - Vector2.right * s, c + Vector2.right * s, 1.5f, color);
                Line(mesh, c - Vector2.up * s, c + Vector2.up * s, 1.5f, color);
                if (artwork == Artwork.Focus)
                    Ring(mesh, c, s * 0.24f, 1.5f, color);
                break;
            case Artwork.Stop:
                Polygon(
                    mesh,
                    new Rect(c - Vector2.one * s * 0.72f, Vector2.one * s * 1.44f),
                    s * 0.3f,
                    color
                );
                break;
            case Artwork.Guard:
            case Artwork.Shield:
                Triangle(
                    mesh,
                    c + new Vector2(-s * 0.8f, s * 0.7f),
                    c + new Vector2(s * 0.8f, s * 0.7f),
                    c - Vector2.up * s,
                    color
                );
                Quad(mesh, new Rect(c.x - s * 0.8f, c.y, s * 1.6f, s * 0.7f), color);
                break;
            case Artwork.Hull:
                Line(mesh, c - Vector2.right * s, c + Vector2.right * s, s * 0.5f, color);
                Line(mesh, c - Vector2.up * s, c + Vector2.up * s, s * 0.5f, color);
                break;
            case Artwork.Retreat:
                Line(mesh, c + Vector2.right * s, c - Vector2.right * s, 2.5f, color);
                Line(
                    mesh,
                    c - Vector2.right * s,
                    c + new Vector2(-s * 0.25f, s * 0.65f),
                    2.5f,
                    color
                );
                Line(
                    mesh,
                    c - Vector2.right * s,
                    c + new Vector2(-s * 0.25f, -s * 0.65f),
                    2.5f,
                    color
                );
                break;
            case Artwork.Dock:
                Line(
                    mesh,
                    c + new Vector2(-s * 0.78f, -s * 0.2f),
                    c + new Vector2(-s * 0.78f, -s * 0.88f),
                    2f,
                    color
                );
                Line(
                    mesh,
                    c + new Vector2(-s * 0.78f, -s * 0.88f),
                    c + new Vector2(s * 0.78f, -s * 0.88f),
                    2f,
                    color
                );
                Line(
                    mesh,
                    c + new Vector2(s * 0.78f, -s * 0.88f),
                    c + new Vector2(s * 0.78f, -s * 0.2f),
                    2f,
                    color
                );
                Line(mesh, c + Vector2.up * s * 0.9f, c - Vector2.up * s * 0.34f, 2.5f, color);
                Triangle(
                    mesh,
                    c - Vector2.up * s * 0.62f,
                    c + new Vector2(-s * 0.38f, -s * 0.2f),
                    c + new Vector2(s * 0.38f, -s * 0.2f),
                    color
                );
                break;
        }
    }

    /// <summary>
    /// Insets a rectangle without changing its center.
    /// </summary>
    /// <param name="r">The original rectangle.</param>
    /// <param name="amount">The inset on each edge.</param>
    /// <returns>The inset rectangle.</returns>
    private static Rect Inset(Rect r, float amount) =>
        new Rect(
            r.x + amount,
            r.y + amount,
            Mathf.Max(0f, r.width - amount * 2f),
            Mathf.Max(0f, r.height - amount * 2f)
        );

    /// <summary>
    /// Adds a chamfered, convex panel.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="r">The panel rectangle.</param>
    /// <param name="cut">The corner chamfer size.</param>
    /// <param name="tint">The face color.</param>
    private static void Polygon(VertexHelper mesh, Rect r, float cut, Color tint)
    {
        cut = Mathf.Min(cut, Mathf.Min(r.width, r.height) * 0.5f);
        int start = mesh.currentVertCount;
        mesh.AddVert(r.center, tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMin + cut, r.yMin), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMax - cut, r.yMin), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMax, r.yMin + cut), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMax, r.yMax - cut), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMax - cut, r.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMin + cut, r.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMin, r.yMax - cut), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMin, r.yMin + cut), tint, Vector2.zero);
        for (int i = 0; i < 8; i++)
            mesh.AddTriangle(start, start + i + 1, start + (i + 1) % 8 + 1);
    }

    /// <summary>
    /// Adds a filled rectangle.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="r">The rectangle.</param>
    /// <param name="tint">The color.</param>
    internal static void Quad(VertexHelper mesh, Rect r, Color tint)
    {
        int i = mesh.currentVertCount;
        mesh.AddVert(new Vector2(r.xMin, r.yMin), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMin, r.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMax, r.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector2(r.xMax, r.yMin), tint, Vector2.zero);
        mesh.AddTriangle(i, i + 1, i + 2);
        mesh.AddTriangle(i, i + 2, i + 3);
    }

    /// <summary>
    /// Adds a thick line segment.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="a">The start point.</param>
    /// <param name="b">The end point.</param>
    /// <param name="width">The line width.</param>
    /// <param name="tint">The line color.</param>
    internal static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * width * 0.5f;
        int i = mesh.currentVertCount;
        mesh.AddVert(a - n, tint, Vector2.zero);
        mesh.AddVert(a + n, tint, Vector2.zero);
        mesh.AddVert(b + n, tint, Vector2.zero);
        mesh.AddVert(b - n, tint, Vector2.zero);
        mesh.AddTriangle(i, i + 1, i + 2);
        mesh.AddTriangle(i, i + 2, i + 3);
    }

    /// <summary>
    /// Adds a filled contact or arrow triangle.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    /// <param name="tint">The color.</param>
    internal static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int i = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero);
        mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddTriangle(i, i + 1, i + 2);
    }

    /// <summary>
    /// Adds a circular instrument line.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="width">The line width.</param>
    /// <param name="tint">The color.</param>
    internal static void Ring(
        VertexHelper mesh,
        Vector2 center,
        float radius,
        float width,
        Color tint
    )
    {
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI / 32f;
            float b = (i + 1) * Mathf.PI / 32f;
            Line(
                mesh,
                center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius,
                width,
                tint
            );
        }
    }
}
