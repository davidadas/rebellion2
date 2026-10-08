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
    /// Builds solid one-pixel segments in source-layout coordinates.
    /// </summary>
    /// <param name="vertexHelper">The target UI mesh.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!showBranch)
            return;

        if (hasHorizontalBranch)
        {
            for (int x = _stemX; x <= _branchEndX; x++)
                AddPixel(vertexHelper, x, _branchY);
        }

        if (continuesAbove)
        {
            for (int y = 0; y <= _branchY; y++)
                AddPixel(vertexHelper, _stemX, y);
        }

        if (continuesBelow)
        {
            for (int y = _branchY; y < Mathf.RoundToInt(rectTransform.rect.height); y++)
                AddPixel(vertexHelper, _stemX, y);
        }
    }

    /// <summary>
    /// Adds one one-pixel quad at a top-left source coordinate.
    /// </summary>
    /// <param name="vertexHelper">The target UI mesh.</param>
    /// <param name="sourceX">The source-layout horizontal coordinate.</param>
    /// <param name="sourceY">The source-layout vertical coordinate.</param>
    private void AddPixel(VertexHelper vertexHelper, int sourceX, int sourceY)
    {
        Rect rect = rectTransform.rect;
        float left = rect.xMin + sourceX;
        float top = rect.yMax - sourceY;
        int startIndex = vertexHelper.currentVertCount;
        Color32 vertexColor = color;
        vertexHelper.AddVert(new Vector3(left, top - 1f), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(left, top), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(left + 1f, top), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(left + 1f, top - 1f), vertexColor, Vector2.zero);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }
}
