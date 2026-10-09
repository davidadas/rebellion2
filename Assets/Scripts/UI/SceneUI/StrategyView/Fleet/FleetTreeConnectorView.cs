using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the solid tree branch that links an expanded capital ship to its fleet row.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class FleetTreeConnectorView : MaskableGraphic
{
    private const int _branchEndX = 5;
    private const int _branchY = 6;
    private const int _stemX = 2;

    private bool continuesAbove;
    private bool continuesBelow;
    private bool hasHorizontalBranch;
    private bool showBranch;

    /// <summary>
    /// Applies one row's connector segments.
    /// </summary>
    /// <param name="visible">Whether the row draws any connector segments.</param>
    /// <param name="horizontal">Whether the connector reaches horizontally into the row.</param>
    /// <param name="above">Whether the vertical stem reaches the top edge.</param>
    /// <param name="below">Whether the vertical stem reaches the bottom edge.</param>
    public void Render(bool visible, bool horizontal, bool above, bool below)
    {
        showBranch = visible;
        hasHorizontalBranch = horizontal;
        continuesAbove = above;
        continuesBelow = below;
        raycastTarget = false;
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds solid connector segments in source-layout coordinates.
    /// </summary>
    /// <param name="vertexHelper">The target UI mesh.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!showBranch)
            return;

        int height = Mathf.RoundToInt(rectTransform.rect.height);
        bool hasVerticalStem = continuesAbove || continuesBelow;
        if (hasVerticalStem)
        {
            int top = continuesAbove ? 0 : _branchY;
            int bottom = continuesBelow ? height : _branchY + 1;
            AddRectangle(vertexHelper, _stemX, top, 1, bottom - top);
        }

        if (hasHorizontalBranch)
        {
            int left = hasVerticalStem ? _stemX + 1 : _stemX;
            AddRectangle(vertexHelper, left, _branchY, _branchEndX - left + 1, 1);
        }
    }

    /// <summary>
    /// Adds one rectangular quad at a top-left source coordinate.
    /// </summary>
    /// <param name="vertexHelper">The target UI mesh.</param>
    /// <param name="sourceX">The source-layout left coordinate.</param>
    /// <param name="sourceY">The source-layout top coordinate.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    private void AddRectangle(
        VertexHelper vertexHelper,
        int sourceX,
        int sourceY,
        int width,
        int height
    )
    {
        if (width <= 0 || height <= 0)
            return;

        Rect rect = rectTransform.rect;
        float left = rect.xMin + sourceX;
        float top = rect.yMax - sourceY;
        float right = left + width;
        float bottom = top - height;
        int startIndex = vertexHelper.currentVertCount;
        Color32 vertexColor = color;
        vertexHelper.AddVert(new Vector3(left, bottom), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(left, top), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(right, top), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(right, bottom), vertexColor, Vector2.zero);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }
}
