using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws an animated command-console accent beneath the loading status.
/// </summary>
[DisallowMultipleComponent]
public sealed class HyperspaceLoadingHudGraphic : MaskableGraphic
{
    private const int _segmentCount = 18;

    /// <summary>
    /// Advances the indeterminate transit sweep with unscaled time.
    /// </summary>
    private void Update()
    {
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds the segmented rail and moving energy sweep.
    /// </summary>
    /// <param name="vertexHelper">The UI mesh receiving the HUD geometry.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = rectTransform.rect;
        float centerY = rect.center.y;
        Color32 rail = new Color(0.2f, 0.7f, 1f, 0.32f);
        Color32 bright = new Color(0.55f, 0.92f, 1f, 0.95f);

        AddQuad(vertexHelper, new Rect(rect.xMin, centerY - 1f, rect.width, 2f), rail, rail);

        float segmentWidth = rect.width / _segmentCount;
        float sweep = Mathf.Repeat(Time.unscaledTime * 4.2f, _segmentCount + 5f) - 2.5f;
        for (int index = 0; index < _segmentCount; index++)
        {
            float distance = Mathf.Abs(index - sweep);
            float intensity = Mathf.Clamp01(1f - distance / 3.5f);
            Color32 segmentColor = Color.Lerp(rail, bright, intensity);
            float inset = Mathf.Max(2f, segmentWidth * 0.13f);
            AddQuad(
                vertexHelper,
                new Rect(
                    rect.xMin + index * segmentWidth + inset,
                    centerY - 5f - intensity * 2f,
                    segmentWidth - inset * 2f,
                    10f + intensity * 4f
                ),
                segmentColor,
                segmentColor
            );
        }
    }

    /// <summary>
    /// Adds one axis-aligned quad with independent lower and upper colors.
    /// </summary>
    /// <param name="vertexHelper">The mesh receiving geometry.</param>
    /// <param name="rect">The quad bounds.</param>
    /// <param name="lowerColor">The lower vertex color.</param>
    /// <param name="upperColor">The upper vertex color.</param>
    private static void AddQuad(
        VertexHelper vertexHelper,
        Rect rect,
        Color32 lowerColor,
        Color32 upperColor
    )
    {
        int firstVertex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMin), lowerColor, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMax), upperColor, Vector2.up);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMax), upperColor, Vector2.one);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMin), lowerColor, Vector2.right);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
    }
}
