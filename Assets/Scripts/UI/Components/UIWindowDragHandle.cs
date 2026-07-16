using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Owns pointer-driven move preview and commit behavior for an authored window title handle.
/// </summary>
public sealed class UIWindowDragHandle
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    [SerializeField]
    private UIWindow window;

    private int offsetX;
    private int offsetY;
    private int previewX;
    private int previewY;
    private bool candidate;
    private bool dragging;

    /// <summary>
    /// Verifies the authored window reference before interaction begins.
    /// </summary>
    private void Awake()
    {
        if (window == null)
            window = GetComponentInParent<UIWindow>();

        if (window == null)
            throw new MissingReferenceException($"{name}/Window is missing.");
    }

    /// <summary>
    /// Captures a movable window and pointer offset for a potential drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        CaptureCandidate(eventData);
    }

    /// <summary>
    /// Commits an active drag when the pointer is released.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        FinishDrag(true);
    }

    /// <summary>
    /// Starts window move preview for a recognized drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        UpdateDragPreview(eventData);
    }

    /// <summary>
    /// Updates source-space move preview during a drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        UpdateDragPreview(eventData);
    }

    /// <summary>
    /// Commits an active drag at the end of Unity's drag lifecycle.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        FinishDrag(true);
    }

    /// <summary>
    /// Cancels an active preview when the authored handle becomes inactive.
    /// </summary>
    private void OnDisable()
    {
        FinishDrag(false);
    }

    /// <summary>
    /// Captures a focused movable window and the pointer's initial window-relative offset.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void CaptureCandidate(PointerEventData eventData)
    {
        candidate = false;
        dragging = false;
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        UIWindow target = window;
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

    /// <summary>
    /// Updates the clamped move-preview bounds for a valid drag candidate.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void UpdateDragPreview(PointerEventData eventData)
    {
        if (!candidate)
            CaptureCandidate(eventData);

        UIWindow target = window;
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

    /// <summary>
    /// Ends preview state and optionally commits the last preview position.
    /// </summary>
    /// <param name="commit">Whether a completed drag should move the window.</param>
    private void FinishDrag(bool commit)
    {
        UIWindow target = window;
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
}
