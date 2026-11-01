using UnityEngine;

/// <summary>
/// Fits a shared scroll area to a resizable UI pane.
/// </summary>
public sealed class ScrollAreaLayout : MonoBehaviour
{
    [SerializeField]
    private ScrollAreaView scrollArea;

    [SerializeField, Min(1)]
    private float scrollbarWidth = 18;

    [SerializeField, Min(0)]
    private float scrollbarGap = 6;

    [SerializeField, Min(1)]
    private float scrollStep = 38;
    private float contentHeight;
    private float lastWidth;

    public event System.Action WidthChanged;

    public ScrollAreaView ScrollArea => scrollArea;

    /// <summary>
    /// Associates the responsive layout with an authored scroll control.
    /// </summary>
    /// <param name="view">The reusable scrolling control.</param>
    public void Configure(ScrollAreaView view)
    {
        scrollArea = view ?? throw new System.ArgumentNullException(nameof(view));
        OnRectTransformDimensionsChange();
    }

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
            new Vector2(
                Mathf.Max(1, rect.width - scrollbarWidth - scrollbarGap),
                Mathf.Max(1, rect.height)
            ),
            new Vector2(rect.width - scrollbarWidth, 0),
            new Vector2(scrollbarWidth, Mathf.Max(24, rect.height))
        );
        scrollArea.SetContentHeight(contentHeight, scrollStep, false);
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
        scrollArea.SetContentHeight(height, scrollStep, reset);
    }
}
