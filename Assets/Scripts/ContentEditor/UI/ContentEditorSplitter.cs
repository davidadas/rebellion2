using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Converts divider drags into workbench-relative pane widths.
/// </summary>
public sealed class ContentEditorSplitter
    : MonoBehaviour,
        IDragHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IBeginDragHandler,
        IEndDragHandler
{
    [SerializeField]
    private ContentEditorWorkbenchLayout layout;
    private Texture2D resizeCursor;
    private bool hovered;
    private bool dragging;

    /// <summary>
    /// Shows horizontal resize feedback while the pointer is over the divider.
    /// </summary>
    /// <param name="eventData">The entering pointer.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        ShowResizeCursor();
    }

    /// <summary>
    /// Restores the normal pointer after leaving a divider that is not being dragged.
    /// </summary>
    /// <param name="eventData">The leaving pointer.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        if (!dragging)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    /// <summary>
    /// Retains resize feedback for the duration of a left-button drag.
    /// </summary>
    /// <param name="eventData">The initiating pointer.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        dragging = true;
        ShowResizeCursor();
    }

    /// <summary>
    /// Releases resize feedback after a drag leaves the divider.
    /// </summary>
    /// <param name="eventData">The released pointer.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragging)
            layout.CommitSplit();
        dragging = false;
        if (!hovered)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    /// <summary>
    /// Restores the system pointer when the workbench is hidden.
    /// </summary>
    private void OnDisable()
    {
        hovered = dragging = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    /// <summary>
    /// Releases the cursor texture owned by this divider.
    /// </summary>
    private void OnDestroy()
    {
        if (resizeCursor == null)
            return;
        if (Application.isPlaying)
            Destroy(resizeCursor);
        else
            DestroyImmediate(resizeCursor);
    }

    /// <summary>
    /// Creates a high-contrast double arrow once and uses Unity's platform cursor support.
    /// </summary>
    private void ShowResizeCursor()
    {
        if (resizeCursor == null)
        {
            const int size = 32;
            bool[] shape = new bool[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                shape[y * size + x] =
                    x >= 5 && x <= 26 && Mathf.Abs(y - 15) <= 1
                    || x >= 2 && x <= 10 && Mathf.Abs(y - 15) <= x - 2
                    || x >= 21 && x <= 29 && Mathf.Abs(y - 15) <= 29 - x;
            Color32[] pixels = new Color32[shape.Length];
            for (int y = 1; y < size - 1; y++)
            for (int x = 1; x < size - 1; x++)
            {
                if (shape[y * size + x])
                    pixels[y * size + x] = Color.white;
                else
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (shape[(y + dy) * size + x + dx])
                            pixels[y * size + x] = Color.black;
            }
            resizeCursor = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Horizontal resize cursor",
                filterMode = FilterMode.Point,
            };
            resizeCursor.SetPixels32(pixels);
            resizeCursor.Apply();
        }
        Cursor.SetCursor(resizeCursor, new Vector2(15, 15), CursorMode.Auto);
    }

    /// <summary>
    /// Resizes the panes without changing the authoring selection or documents.
    /// </summary>
    /// <param name="eventData">The divider drag.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        RectTransform parent = (RectTransform)layout.transform;
        if (
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 point
            )
        )
            layout.SetSplit((point.x - parent.rect.xMin) / parent.rect.width);
    }
}
