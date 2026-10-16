using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a layered perspective hyperspace starfield as one animated UI mesh.
/// </summary>
[DisallowMultipleComponent]
public sealed class HyperspaceStarfieldGraphic : MaskableGraphic
{
    private const int _starCount = 560;
    private const int _speedRayCount = 24;
    private const float _minimumDepth = 0.048f;

    private readonly System.Random random = new System.Random(19770425);
    private HyperspaceStar[] stars;
    private float animationTime;
    private float viewYaw;

    /// <summary>
    /// Applies the normalized horizontal camera orbit to the tunnel's vanishing point.
    /// </summary>
    /// <param name="normalizedYaw">The camera yaw normalized to the supported viewing arc.</param>
    public void SetViewYaw(float normalizedYaw)
    {
        float clampedYaw = Mathf.Clamp(normalizedYaw, -1f, 1f);
        if (Mathf.Approximately(viewYaw, clampedYaw))
            return;

        viewYaw = clampedYaw;
        SetVerticesDirty();
    }

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

        float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        animationTime += deltaTime;
        Rect bounds = rectTransform.rect;
        Vector2 vanishingPoint = GetVanishingPoint(bounds);
        for (int index = 0; index < stars.Length; index++)
        {
            HyperspaceStar star = stars[index];
            star.Depth -= deltaTime * star.Speed;
            Vector2 position = Project(star, vanishingPoint);
            if (
                star.Depth <= _minimumDepth
                || Mathf.Abs(position.x) > bounds.width * 0.64f
                || Mathf.Abs(position.y) > bounds.height * 0.68f
            )
            {
                ResetStar(ref star, false, bounds);
            }
            stars[index] = star;
        }
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds the background gradient, speed rays, and depth-shaded stars.
    /// </summary>
    /// <param name="vertexHelper">The UI mesh receiving the star geometry.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        EnsureStars();
        if (stars == null)
            return;

        Rect bounds = rectTransform.rect;
        Vector2 vanishingPoint = GetVanishingPoint(bounds);
        AddBackdrop(vertexHelper, bounds);
        AddSpeedRays(vertexHelper, bounds, vanishingPoint);
        for (int index = 0; index < stars.Length; index++)
            AddStar(vertexHelper, stars[index], vanishingPoint);
        AddVignette(vertexHelper, bounds);
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
    /// Draws the black space backdrop and subtle central transit glow.
    /// </summary>
    /// <param name="vertexHelper">The mesh receiving geometry.</param>
    /// <param name="bounds">The presentation bounds.</param>
    private static void AddBackdrop(VertexHelper vertexHelper, Rect bounds)
    {
        AddQuad(vertexHelper, bounds, Color.black, Color.black);

        float glowWidth = bounds.width * 0.48f;
        float glowHeight = bounds.height * 0.62f;
        AddDiamond(
            vertexHelper,
            new Vector2(0f, bounds.height * 0.055f),
            glowWidth,
            glowHeight,
            new Color(0.18f, 0.22f, 0.28f, 0.1f),
            Color.clear
        );
    }

    /// <summary>
    /// Draws long low-opacity guide streaks around the outer tunnel.
    /// </summary>
    /// <param name="vertexHelper">The mesh receiving geometry.</param>
    /// <param name="bounds">The presentation bounds.</param>
    /// <param name="center">The tunnel vanishing point.</param>
    private void AddSpeedRays(VertexHelper vertexHelper, Rect bounds, Vector2 center)
    {
        float rotation = animationTime * 0.035f;
        for (int index = 0; index < _speedRayCount; index++)
        {
            float angle = index * Mathf.PI * 2f / _speedRayCount + rotation;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 scaledDirection = new Vector2(direction.x, direction.y * 0.72f);
            Vector2 start = center + scaledDirection * bounds.width * 0.19f;
            Vector2 end = center + scaledDirection * bounds.width * 0.76f;
            float pulse = (Mathf.Sin(animationTime * 1.6f + index * 1.7f) + 1f) * 0.5f;
            AddLine(
                vertexHelper,
                start,
                end,
                Mathf.Lerp(0.45f, 1.35f, pulse),
                new Color(0.18f, 0.5f, 1f, 0.015f),
                new Color(0.58f, 0.88f, 1f, 0.12f + pulse * 0.08f)
            );
        }
    }

    /// <summary>
    /// Adds one tapered, depth-shaded hyperspace streak.
    /// </summary>
    /// <param name="vertexHelper">The UI mesh receiving the quad.</param>
    /// <param name="star">The star to render.</param>
    /// <param name="vanishingPoint">The current tunnel center.</param>
    private static void AddStar(
        VertexHelper vertexHelper,
        HyperspaceStar star,
        Vector2 vanishingPoint
    )
    {
        Vector2 head = Project(star, vanishingPoint);
        float trailDepth = Mathf.Lerp(0.025f, 0.085f, star.TrailScale);
        Vector2 tail = vanishingPoint + star.PlanePosition / (star.Depth + trailDepth);
        Vector2 travel = head - tail;
        if (travel.sqrMagnitude < 1f)
            return;

        float proximity = 1f - Mathf.Clamp01(star.Depth);
        float halfWidth = Mathf.Lerp(0.35f, 2.8f, proximity) * star.WidthScale;
        Vector2 perpendicular = new Vector2(-travel.y, travel.x).normalized * halfWidth;
        float flicker = Mathf.Lerp(0.82f, 1f, star.Flicker);
        Color32 headColor = new Color(
            Mathf.Lerp(0.62f, 0.9f, star.Warmth) * flicker,
            Mathf.Lerp(0.82f, 0.95f, star.Warmth) * flicker,
            flicker,
            Mathf.Lerp(0.2f, 1f, proximity)
        );
        Color32 tailColor = new Color(0.18f, 0.42f, 1f, 0.01f);
        int firstVertex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(tail - perpendicular * 0.18f, tailColor, Vector2.zero);
        vertexHelper.AddVert(tail + perpendicular * 0.18f, tailColor, Vector2.up);
        vertexHelper.AddVert(head + perpendicular, headColor, Vector2.one);
        vertexHelper.AddVert(head - perpendicular, headColor, Vector2.right);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
    }

    /// <summary>
    /// Darkens the screen perimeter with layered translucent bands.
    /// </summary>
    /// <param name="vertexHelper">The mesh receiving geometry.</param>
    /// <param name="bounds">The presentation bounds.</param>
    private static void AddVignette(VertexHelper vertexHelper, Rect bounds)
    {
        float sideWidth = bounds.width * 0.13f;
        float verticalHeight = bounds.height * 0.12f;
        Color32 clear = new Color(0f, 0f, 0f, 0f);
        Color32 dark = new Color(0f, 0f, 0f, 0.72f);
        AddQuad(
            vertexHelper,
            new Rect(bounds.xMin, bounds.yMin, sideWidth, bounds.height),
            dark,
            dark,
            clear,
            clear
        );
        AddQuad(
            vertexHelper,
            new Rect(bounds.xMax - sideWidth, bounds.yMin, sideWidth, bounds.height),
            clear,
            clear,
            dark,
            dark
        );
        AddQuad(
            vertexHelper,
            new Rect(bounds.xMin, bounds.yMin, bounds.width, verticalHeight),
            dark,
            clear
        );
        AddQuad(
            vertexHelper,
            new Rect(bounds.xMin, bounds.yMax - verticalHeight, bounds.width, verticalHeight),
            clear,
            dark
        );
    }

    /// <summary>
    /// Returns the subtly drifting center of the hyperspace tunnel.
    /// </summary>
    /// <returns>The current local-space vanishing point.</returns>
    /// <param name="bounds">The presentation bounds used to scale the camera offset.</param>
    private Vector2 GetVanishingPoint(Rect bounds)
    {
        return new Vector2(
            Mathf.Sin(animationTime * 0.19f) * 11f - viewYaw * bounds.width * 0.22f,
            Mathf.Sin(animationTime * 0.13f) * 7f + 42f
        );
    }

    /// <summary>
    /// Projects one star from the far plane into the loading-screen canvas.
    /// </summary>
    /// <param name="star">The star to project.</param>
    /// <param name="vanishingPoint">The current tunnel center.</param>
    /// <returns>The projected canvas position.</returns>
    private static Vector2 Project(HyperspaceStar star, Vector2 vanishingPoint)
    {
        return vanishingPoint + star.PlanePosition / Mathf.Max(star.Depth, _minimumDepth);
    }

    /// <summary>
    /// Places a star on the far plane or distributes it through the tunnel at startup.
    /// </summary>
    /// <param name="star">The star state to reset.</param>
    /// <param name="seedAcrossDepth">Whether to seed the star throughout the tunnel depth.</param>
    /// <param name="bounds">The current starfield bounds.</param>
    private void ResetStar(ref HyperspaceStar star, bool seedAcrossDepth, Rect bounds)
    {
        star.Depth = seedAcrossDepth ? Mathf.Lerp(0.13f, 1f, (float)random.NextDouble()) : 1f;
        float angle = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());
        float radius = Mathf.Sqrt((float)random.NextDouble());
        float planeWidth = bounds.width * 0.52f;
        float planeHeight = bounds.height * 0.5f;
        star.PlanePosition =
            new Vector2(
                Mathf.Cos(angle) * planeWidth * radius,
                Mathf.Sin(angle) * planeHeight * radius
            ) * star.Depth;
        if (star.PlanePosition.sqrMagnitude < 900f)
            star.PlanePosition = star.PlanePosition.normalized * 30f;

        star.Speed = Mathf.Lerp(0.24f, 0.44f, (float)random.NextDouble());
        star.WidthScale = Mathf.Lerp(0.62f, 1.38f, (float)random.NextDouble());
        star.TrailScale = Mathf.Lerp(0.45f, 1.35f, (float)random.NextDouble());
        star.Warmth = (float)random.NextDouble();
        star.Flicker = Mathf.Lerp(0.72f, 1f, (float)random.NextDouble());
    }

    /// <summary>
    /// Adds a line segment as one quad.
    /// </summary>
    /// <param name="vertexHelper">The mesh receiving geometry.</param>
    /// <param name="start">The line start.</param>
    /// <param name="end">The line end.</param>
    /// <param name="width">The line width.</param>
    /// <param name="startColor">The start color.</param>
    /// <param name="endColor">The end color.</param>
    private static void AddLine(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float width,
        Color32 startColor,
        Color32 endColor
    )
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
        int firstVertex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(start - perpendicular, startColor, Vector2.zero);
        vertexHelper.AddVert(start + perpendicular, startColor, Vector2.up);
        vertexHelper.AddVert(end + perpendicular, endColor, Vector2.one);
        vertexHelper.AddVert(end - perpendicular, endColor, Vector2.right);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
    }

    /// <summary>
    /// Adds a four-sided central glow with a bright center and transparent perimeter.
    /// </summary>
    /// <param name="vertexHelper">The mesh receiving geometry.</param>
    /// <param name="center">The glow center.</param>
    /// <param name="width">The glow width.</param>
    /// <param name="height">The glow height.</param>
    /// <param name="centerColor">The center color.</param>
    /// <param name="edgeColor">The perimeter color.</param>
    private static void AddDiamond(
        VertexHelper vertexHelper,
        Vector2 center,
        float width,
        float height,
        Color32 centerColor,
        Color32 edgeColor
    )
    {
        int firstVertex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(center, centerColor, new Vector2(0.5f, 0.5f));
        vertexHelper.AddVert(center + Vector2.up * height, edgeColor, Vector2.up);
        vertexHelper.AddVert(center + Vector2.right * width, edgeColor, Vector2.right);
        vertexHelper.AddVert(center + Vector2.down * height, edgeColor, Vector2.down);
        vertexHelper.AddVert(center + Vector2.left * width, edgeColor, Vector2.left);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 3, firstVertex + 4);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 4, firstVertex + 1);
    }

    /// <summary>
    /// Adds one vertical-gradient quad.
    /// </summary>
    /// <param name="vertexHelper">The mesh receiving geometry.</param>
    /// <param name="rect">The quad bounds.</param>
    /// <param name="lowerColor">The lower color.</param>
    /// <param name="upperColor">The upper color.</param>
    private static void AddQuad(
        VertexHelper vertexHelper,
        Rect rect,
        Color32 lowerColor,
        Color32 upperColor
    )
    {
        AddQuad(vertexHelper, rect, lowerColor, upperColor, upperColor, lowerColor);
    }

    /// <summary>
    /// Adds one quad with independent corner colors.
    /// </summary>
    /// <param name="vertexHelper">The mesh receiving geometry.</param>
    /// <param name="rect">The quad bounds.</param>
    /// <param name="lowerLeft">The lower-left color.</param>
    /// <param name="upperLeft">The upper-left color.</param>
    /// <param name="upperRight">The upper-right color.</param>
    /// <param name="lowerRight">The lower-right color.</param>
    private static void AddQuad(
        VertexHelper vertexHelper,
        Rect rect,
        Color32 lowerLeft,
        Color32 upperLeft,
        Color32 upperRight,
        Color32 lowerRight
    )
    {
        int firstVertex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMin), lowerLeft, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMax), upperLeft, Vector2.up);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMax), upperRight, Vector2.one);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMin), lowerRight, Vector2.right);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
    }

    /// <summary>
    /// Stores the perspective and color state for one reusable star streak.
    /// </summary>
    private struct HyperspaceStar
    {
        internal Vector2 PlanePosition;
        internal float Depth;
        internal float Speed;
        internal float WidthScale;
        internal float TrailScale;
        internal float Warmth;
        internal float Flicker;
    }
}
