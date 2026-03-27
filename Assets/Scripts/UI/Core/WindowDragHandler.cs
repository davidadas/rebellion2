using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Allows a window to be dragged by its header.
/// The drag bounds and window root are configured at runtime.
/// </summary>
public sealed class WindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform windowRoot;
    private RectTransform dragBounds;
    private Vector2 pointerOffset;

    /// <summary>
    /// Sets the RectTransform that should be moved when dragging.
    /// </summary>
    public void SetWindowRoot(RectTransform root)
    {
        windowRoot = root;
    }

    /// <summary>
    /// Sets the RectTransform that constrains dragging bounds.
    /// </summary>
    public void SetDragBounds(RectTransform bounds)
    {
        dragBounds = bounds;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (windowRoot == null)
            return;

        RectTransform parentRect = windowRoot.parent as RectTransform;
        if (parentRect == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPointerPosition
        );

        pointerOffset = windowRoot.anchoredPosition - localPointerPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (windowRoot == null)
            return;

        RectTransform parentRect = windowRoot.parent as RectTransform;
        if (parentRect == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPointerPosition
        );

        Vector2 targetPosition = localPointerPosition + pointerOffset;

        windowRoot.anchoredPosition = ClampToBounds(targetPosition);
    }

    private Vector2 ClampToBounds(Vector2 targetPosition)
    {
        if (dragBounds == null)
            return targetPosition;

        Vector2 min = dragBounds.rect.min;
        Vector2 max = dragBounds.rect.max;

        float width = windowRoot.rect.width;
        float height = windowRoot.rect.height;

        float pivotX = windowRoot.pivot.x;
        float pivotY = windowRoot.pivot.y;

        float minX = min.x + width * pivotX;
        float maxX = max.x - width * (1f - pivotX);

        float minY = min.y + height * pivotY;
        float maxY = max.y - height * (1f - pivotY);

        float clampedX = Mathf.Clamp(targetPosition.x, minX, maxX);
        float clampedY = Mathf.Clamp(targetPosition.y, minY, maxY);

        return new Vector2(clampedX, clampedY);
    }
}
