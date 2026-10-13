using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a dense perspective star tunnel as one animated UI mesh.
/// </summary>
[DisallowMultipleComponent]
public sealed class HyperspaceStarfieldGraphic : MaskableGraphic
{
    private const int _starCount = 420;
    private const float _minimumDepth = 0.055f;
    private const float _trailDepth = 0.045f;

    private readonly System.Random random = new System.Random(19770425);
    private HyperspaceStar[] stars;

    /// <summary>
    /// Seeds the star tunnel whenever the graphic becomes active.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureStars();
        SetVerticesDirty();
    }

    /// <summary>
    /// Advances stars toward the viewer using unscaled time so loading cannot pause the effect.
    /// </summary>
    private void Update()
    {
        EnsureStars();
        if (stars == null)
            return;

        Rect bounds = rectTransform.rect;
        float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int index = 0; index < stars.Length; index++)
        {
            HyperspaceStar star = stars[index];
            star.Depth -= deltaTime * star.Speed;
            Vector2 position = Project(star);
            if (
                star.Depth <= _minimumDepth
                || Mathf.Abs(position.x) > bounds.width * 0.58f
                || Mathf.Abs(position.y) > bounds.height * 0.62f
            )
            {
                ResetStar(ref star, false, bounds);
            }
            stars[index] = star;
        }
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds tapered, depth-shaded streaks for the current star positions.
    /// </summary>
    /// <param name="vertexHelper">The UI mesh receiving the star geometry.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        EnsureStars();
        if (stars == null)
            return;

        for (int index = 0; index < stars.Length; index++)
            AddStar(vertexHelper, stars[index]);
    }

    /// <summary>
    /// Creates the deterministic star set on first use.
    /// </summary>
    private void EnsureStars()
    {
        if (stars != null)
            return;

        Rect bounds = rectTransform.rect;
        if (bounds.width <= 1f || bounds.height <= 1f)
            return;

        stars = new HyperspaceStar[_starCount];
        for (int index = 0; index < stars.Length; index++)
            ResetStar(ref stars[index], true, bounds);
    }

    /// <summary>
    /// Adds one fading perspective streak to the shared UI mesh.
    /// </summary>
    /// <param name="vertexHelper">The UI mesh receiving the quad.</param>
    /// <param name="star">The star to render.</param>
    private static void AddStar(VertexHelper vertexHelper, HyperspaceStar star)
    {
        Vector2 head = Project(star);
        Vector2 tail = star.PlanePosition / (star.Depth + _trailDepth);
        Vector2 travel = head - tail;
        if (travel.sqrMagnitude < 2.25f)
            travel = travel.normalized * 1.5f;

        float proximity = 1f - Mathf.Clamp01(star.Depth);
        float halfWidth = Mathf.Lerp(0.55f, 2.4f, proximity) * star.WidthScale;
        Vector2 perpendicular = new Vector2(-travel.y, travel.x).normalized * halfWidth;
        Color32 headColor = new Color(
            0.72f * star.BlueTint,
            0.88f * star.BlueTint,
            star.BlueTint,
            Mathf.Lerp(0.32f, 1f, proximity)
        );
        Color32 tailColor = new Color(headColor.r / 255f, headColor.g / 255f, 1f, 0.02f);
        int firstVertex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(tail - perpendicular, tailColor, Vector2.zero);
        vertexHelper.AddVert(tail + perpendicular, tailColor, Vector2.zero);
        vertexHelper.AddVert(head + perpendicular, headColor, Vector2.one);
        vertexHelper.AddVert(head - perpendicular, headColor, Vector2.one);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
    }

    /// <summary>
    /// Projects one star from the far plane into the loading-screen canvas.
    /// </summary>
    /// <param name="star">The star to project.</param>
    /// <returns>The projected canvas position.</returns>
    private static Vector2 Project(HyperspaceStar star)
    {
        return star.PlanePosition / Mathf.Max(star.Depth, _minimumDepth);
    }

    /// <summary>
    /// Places a star on the far plane or distributes it through the tunnel at startup.
    /// </summary>
    /// <param name="star">The star state to reset.</param>
    /// <param name="seedAcrossDepth">Whether to seed the star throughout the tunnel depth.</param>
    /// <param name="bounds">The current starfield bounds.</param>
    private void ResetStar(ref HyperspaceStar star, bool seedAcrossDepth, Rect bounds)
    {
        star.Depth = seedAcrossDepth ? Mathf.Lerp(0.16f, 1f, (float)random.NextDouble()) : 1f;
        Vector2 screenPosition = new Vector2(
            Mathf.Lerp(-bounds.width * 0.52f, bounds.width * 0.52f, (float)random.NextDouble()),
            Mathf.Lerp(-bounds.height * 0.54f, bounds.height * 0.54f, (float)random.NextDouble())
        );
        if (screenPosition.sqrMagnitude < 625f)
            screenPosition = screenPosition.normalized * 25f;

        star.PlanePosition = screenPosition * star.Depth;
        star.Speed = Mathf.Lerp(0.24f, 0.4f, (float)random.NextDouble());
        star.WidthScale = Mathf.Lerp(0.7f, 1.25f, (float)random.NextDouble());
        star.BlueTint = Mathf.Lerp(0.82f, 1f, (float)random.NextDouble());
    }

    /// <summary>
    /// Stores the perspective state for one reusable star streak.
    /// </summary>
    private struct HyperspaceStar
    {
        internal Vector2 PlanePosition;
        internal float Depth;
        internal float Speed;
        internal float WidthScale;
        internal float BlueTint;
    }
}
