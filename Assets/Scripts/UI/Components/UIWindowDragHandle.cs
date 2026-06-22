using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UIWindowDragHandle
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    private UIWindow window;
    private int offsetX;
    private int offsetY;
    private int previewX;
    private int previewY;
    private bool candidate;
    private bool dragging;

    public void OnPointerDown(PointerEventData eventData)
    {
        CaptureCandidate(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        FinishDrag(true);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        UpdateDragPreview(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateDragPreview(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        FinishDrag(true);
    }

    private void OnDisable()
    {
        FinishDrag(false);
    }

    private void CaptureCandidate(PointerEventData eventData)
    {
        candidate = false;
        dragging = false;
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        UIWindow target = GetWindow();
        if (target == null)
            return;

        if (!target.RequestFocus())
            return;

        if (!target.CanMove)
            return;

        if (!target.TryGetDesktopPosition(eventData, eventData.position, out int x, out int y))
            return;

        offsetX = x - target.X;
        offsetY = y - target.Y;
        previewX = target.X;
        previewY = target.Y;
        candidate = true;
    }

    private void UpdateDragPreview(PointerEventData eventData)
    {
        if (!candidate)
            CaptureCandidate(eventData);

        UIWindow target = GetWindow();
        if (!candidate || target == null)
            return;

        if (!target.TryGetDesktopPosition(eventData, eventData.position, out int x, out int y))
            return;

        Vector2Int position = target.ClampPosition(x - offsetX, y - offsetY);
        previewX = position.x;
        previewY = position.y;
        dragging = true;
        target.NotifyMovePreviewChanged(
            new RectInt(previewX, previewY, target.Width, target.Height)
        );
    }

    private void FinishDrag(bool commit)
    {
        UIWindow target = GetWindow();
        bool commitMove = commit && dragging && target != null;

        if (dragging && target != null)
            target.NotifyMovePreviewEnded();

        if (commitMove)
        {
            target.MoveTo(previewX, previewY);
            target.NotifyMoved();
        }

        candidate = false;
        dragging = false;
        offsetX = 0;
        offsetY = 0;
        previewX = 0;
        previewY = 0;
    }

    private UIWindow GetWindow()
    {
        if (window == null)
            window = GetComponentInParent<UIWindow>();

        return window;
    }
}
