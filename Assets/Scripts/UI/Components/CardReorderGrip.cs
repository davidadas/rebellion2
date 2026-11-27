using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the six-dot reorder grip used by the game and the content editor.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CardReorderGrip : MaskableGraphic
{
    /// <summary>
    /// Draws two columns of three dots in the center of the hit area.
    /// </summary>
    /// <param name="vertices">The UI mesh that receives the dots.</param>
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        Vector2 center = GetPixelAdjustedRect().center;
        for (int column = -1; column <= 1; column += 2)
        for (int row = -1; row <= 1; row++)
        {
            Vector2 dot = center + new Vector2(column * 2.5f, row * 4.5f);
            int start = vertices.currentVertCount;
            vertices.AddVert(dot, color, Vector2.zero);
            for (int point = 0; point < 8; point++)
            {
                float angle = -point * Mathf.PI / 4f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.4f;
                vertices.AddVert(dot + offset, color, Vector2.zero);
                vertices.AddTriangle(start, start + 1 + point, start + 1 + (point + 1) % 8);
            }
        }
    }
}
