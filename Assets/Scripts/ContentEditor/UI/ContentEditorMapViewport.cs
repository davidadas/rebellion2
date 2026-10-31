using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Zooms and pans a map while retaining its normalized coordinate frame.
/// </summary>
public sealed class ContentEditorMapViewport
    : MonoBehaviour,
        IScrollHandler,
        IBeginDragHandler,
        IDragHandler
{
    [SerializeField]
    private RectTransform mapRoot;
    private Vector2 lastPointer;
    private float zoom = 1;
    public bool IsFitted =>
        Mathf.Approximately(zoom, 1) && mapRoot.anchoredPosition.sqrMagnitude < 0.01f;
    public event System.Action ViewChanged;

    /// <summary>
    /// Restores the map to its original fitted bounds.
    /// </summary>
    public void ResetView()
    {
        zoom = 1;
        mapRoot.localScale = Vector3.one;
        mapRoot.anchoredPosition = Vector2.zero;
        ViewChanged?.Invoke();
    }

    /// <summary>
    /// Zooms around the pointer with the wheel, keeping the visible world position stable.
    /// </summary>
    /// <param name="eventData">The wheel event.</param>
    public void OnScroll(PointerEventData eventData)
    {
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 point
            )
        )
            return;
        float next = Mathf.Clamp(zoom * Mathf.Pow(1.15f, eventData.scrollDelta.y), 1, 6);
        mapRoot.anchoredPosition = point - (point - mapRoot.anchoredPosition) * next / zoom;
        zoom = next;
        mapRoot.localScale = Vector3.one * zoom;
        ClampPosition();
        ViewChanged?.Invoke();
        eventData.Use();
    }

    /// <summary>
    /// Starts a background pan without stealing left-button marker drags.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform,
            eventData.position,
            eventData.pressEventCamera,
            out lastPointer
        );
    }

    /// <summary>
    /// Pans the zoomed map using the middle or right mouse button.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            return;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 point
            )
        )
            return;
        mapRoot.anchoredPosition += point - lastPointer;
        lastPointer = point;
        ClampPosition();
        ViewChanged?.Invoke();
    }

    /// <summary>
    /// Keeps the map covering the viewport along each scrollable axis.
    /// </summary>
    private void ClampPosition()
    {
        Vector2 overflow = Vector2.Max(
            Vector2.zero,
            (mapRoot.rect.size * zoom - ((RectTransform)transform).rect.size) / 2
        );
        Vector2 position = mapRoot.anchoredPosition;
        mapRoot.anchoredPosition = new Vector2(
            Mathf.Clamp(position.x, -overflow.x, overflow.x),
            Mathf.Clamp(position.y, -overflow.y, overflow.y)
        );
    }
}
