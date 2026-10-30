using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Owns the editor's pane widths and content zoom independently of its application menu.
/// </summary>
public sealed class ContentEditorWorkbenchLayout : UIBehaviour
{
    [SerializeField]
    private RectTransform explorer;

    [SerializeField]
    private RectTransform inspector;

    [SerializeField]
    private RectTransform divider;

    [SerializeField]
    private RectTransform navigationBackground;
    private float split = 0.205f;
    public float Zoom { get; private set; } = 1;
    public float Split => split;
    public event System.Action Changed;

    /// <summary>
    /// Changes the content scale while preserving the fixed application menu.
    /// </summary>
    /// <param name="scale">The requested content scale.</param>
    public void SetZoom(float scale)
    {
        float previous = Zoom;
        Zoom = Mathf.Clamp(scale, 0.65f, 1.75f);
        ApplyLayout();
        if (!Mathf.Approximately(previous, Zoom))
            Changed?.Invoke();
    }

    /// <summary>
    /// Moves the divider while keeping both panes usable.
    /// </summary>
    /// <param name="fraction">The desired horizontal fraction of the workbench.</param>
    public void SetSplit(float fraction)
    {
        split = Mathf.Clamp(fraction, 0.15f, 0.65f);
        ApplyLayout();
    }

    /// <summary>
    /// Publishes the final divider position after a drag has completed.
    /// </summary>
    public void CommitSplit() => Changed?.Invoke();

    /// <summary>
    /// Reflows the panes after their parent window changes size.
    /// </summary>
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        ApplyLayout();
    }

    /// <summary>
    /// Fits logical content dimensions within the unscaled workbench margins.
    /// </summary>
    private void ApplyLayout()
    {
        if (explorer == null || inspector == null || divider == null)
            return;
        Rect bounds = ((RectTransform)transform).rect;
        float minimum = Mathf.Min(bounds.width * 0.4f, 20 + 240 * Zoom);
        float maximum = Mathf.Max(minimum, bounds.width - 28 - 400 * Zoom);
        float x = Mathf.Clamp(bounds.width * split, minimum, maximum);
        if (navigationBackground != null)
            Place(navigationBackground, 0, 36, x + 8, bounds.height - 64, 1);
        Place(explorer, 20, 54, (x - 32) / Zoom, (bounds.height - 98) / Zoom, Zoom);
        Place(
            inspector,
            x + 28,
            56,
            (bounds.width - x - 56) / Zoom,
            (bounds.height - 100) / Zoom,
            Zoom
        );
        Place(divider, x - 4, 45, 9, bounds.height - 89, 1);
    }

    /// <summary>
    /// Places a pane in parent coordinates with independently scaled contents.
    /// </summary>
    /// <param name="rect">The pane.</param>
    /// <param name="x">The left margin.</param>
    /// <param name="y">The top margin.</param>
    /// <param name="width">The logical content width.</param>
    /// <param name="height">The logical content height.</param>
    /// <param name="scale">The content scale.</param>
    private static void Place(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height,
        float scale
    )
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(Mathf.Max(1, width), Mathf.Max(1, height));
        rect.localScale = Vector3.one * scale;
    }
}
