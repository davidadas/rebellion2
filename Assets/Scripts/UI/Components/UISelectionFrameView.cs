using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marks a persistent selection with an authored, non-interactive faction-colored frame.
/// </summary>
public sealed class UISelectionFrameView : MonoBehaviour
{
    [SerializeField]
    private Image[] edges;

    /// <summary>
    /// Shows the selection independently of hover and pressed-state feedback.
    /// </summary>
    /// <param name="selected">Whether the containing control is selected.</param>
    /// <param name="color">The selection owner's faction color.</param>
    public void Render(bool selected, Color color)
    {
        foreach (Image edge in edges)
            edge.color = color;
        gameObject.SetActive(selected);
    }
}
