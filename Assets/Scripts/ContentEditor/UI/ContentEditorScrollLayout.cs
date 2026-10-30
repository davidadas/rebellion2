using UnityEngine;

/// <summary>
/// Fits a shared scroll area to a resizable authoring pane.
/// </summary>
public sealed class ContentEditorScrollLayout : MonoBehaviour
{
    [SerializeField]
    private ScrollAreaView scrollArea;
    private float contentHeight;
    private float lastWidth;

    public event System.Action WidthChanged;

    public ScrollAreaView ScrollArea => scrollArea;

    /// <summary>
    /// Fits the authored control after the window changes size.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (scrollArea == null || !gameObject.activeInHierarchy)
            return;
        Rect rect = ((RectTransform)transform).rect;
        scrollArea.SetLayout(
            Vector2.zero,
            new Vector2(Mathf.Max(1, rect.width - 24), Mathf.Max(1, rect.height)),
            new Vector2(rect.width - 18, 0),
            new Vector2(18, Mathf.Max(24, rect.height))
        );
        scrollArea.SetContentHeight(contentHeight, 38, false);
        if (!Mathf.Approximately(lastWidth, rect.width))
        {
            lastWidth = rect.width;
            WidthChanged?.Invoke();
        }
    }

    /// <summary>
    /// Sizes the scroll content after records or form fields change.
    /// </summary>
    /// <param name="height">The complete content height.</param>
    /// <param name="reset">Whether to return to the top.</param>
    public void SetContentHeight(float height, bool reset = false)
    {
        contentHeight = height;
        OnRectTransformDimensionsChange();
        scrollArea.SetContentHeight(height, 38, reset);
    }
}
