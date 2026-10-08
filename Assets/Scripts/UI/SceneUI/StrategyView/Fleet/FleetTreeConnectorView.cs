using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the dotted tree branch that links an expanded capital ship to its fleet row.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class FleetTreeConnectorView : MaskableGraphic
{
    private const int _branchEndX = 5;
    private const int _branchY = 6;
    private const int _dotSpacing = 2;
    private const int _stemX = 2;

    private bool continuesAbove;
    private bool continuesBelow;
    private bool showBranch;

    /// <summary>
    /// Applies one row's connector segments.
    /// </summary>
    /// <param name="visible">Whether the row draws a horizontal branch.</param>
    /// <param name="above">Whether the vertical stem reaches the top edge.</param>
    /// <param name="below">Whether the vertical stem reaches the bottom edge.</param>
    public void Render(bool visible, bool above, bool below)
    {
        showBranch = visible;
        continuesAbove = above;
        continuesBelow = below;
        raycastTarget = false;
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds one-pixel dots in source-layout coordinates.
    /// </summary>
    /// <param name="vertexHelper">The target UI mesh.</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!showBranch)
            return;

        for (int x = _stemX; x <= _branchEndX; x += _dotSpacing)
            AddDot(vertexHelper, x, _branchY);

        if (continuesAbove)
        {
            for (int y = 0; y <= _branchY; y += _dotSpacing)
                AddDot(vertexHelper, _stemX, y);
        }

        if (continuesBelow)
        {
            for (
                int y = _branchY;
                y < Mathf.RoundToInt(rectTransform.rect.height);
                y += _dotSpacing
            )
                AddDot(vertexHelper, _stemX, y);
        }
    }

    /// <summary>
    /// Adds one one-pixel quad at a top-left source coordinate.
    /// </summary>
    /// <param name="vertexHelper">The target UI mesh.</param>
    /// <param name="sourceX">The source-layout horizontal coordinate.</param>
    /// <param name="sourceY">The source-layout vertical coordinate.</param>
    private void AddDot(VertexHelper vertexHelper, int sourceX, int sourceY)
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
