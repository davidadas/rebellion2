using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Multiplies a UI graphic's vertex colors by a vertical two-color gradient.
/// </summary>
[RequireComponent(typeof(Graphic))]
public sealed class VerticalGradientEffect : BaseMeshEffect
{
    [SerializeField]
    private Color topColor = Color.white;

    [SerializeField]
    private Color bottomColor = Color.white;

    /// <summary>
    /// Replaces the gradient endpoints and refreshes the owning graphic.
    /// </summary>
    /// <param name="top">The color applied at the top edge.</param>
    /// <param name="bottom">The color applied at the bottom edge.</param>
    public void SetColors(Color top, Color bottom)
    {
        topColor = top;
        bottomColor = bottom;
        graphic.SetVerticesDirty();
    }

    /// <summary>
    /// Applies the configured gradient across the populated vertex range.
    /// </summary>
    /// <param name="vertexHelper">The generated UI mesh to modify.</param>
    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount == 0)
            return;

        UIVertex vertex = default;
        float minimumY = float.PositiveInfinity;
        float maximumY = float.NegativeInfinity;
        for (int index = 0; index < vertexHelper.currentVertCount; index++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, index);
            minimumY = Mathf.Min(minimumY, vertex.position.y);
            maximumY = Mathf.Max(maximumY, vertex.position.y);
        }

        float height = maximumY - minimumY;
        for (int index = 0; index < vertexHelper.currentVertCount; index++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, index);
            float position = height > 0f ? (vertex.position.y - minimumY) / height : 0f;
            Color sourceColor = vertex.color;
            vertex.color = sourceColor * Color.Lerp(bottomColor, topColor, position);
            vertexHelper.SetUIVertex(vertex, index);
        }
    }
}
