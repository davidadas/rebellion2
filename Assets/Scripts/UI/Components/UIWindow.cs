using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns the common interaction and source-space bounds of an authored UI window.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public sealed class UIWindow : MonoBehaviour, IPointerDownHandler
{
    [SerializeField]
    private CanvasGroup inputGroup;

    [SerializeField]
    private Button[] actionButtons = Array.Empty<Button>();

    [SerializeField]
    private int[] buttonActions = Array.Empty<int>();

    private readonly List<Button> boundActionButtons = new List<Button>();
    private readonly List<UnityAction> boundActionListeners = new List<UnityAction>();

    private UIWindowManager windowManager;
    private RectTransform rectTransform;
    private MonoBehaviour content;
    private bool activeWindow;

    /// <summary>
    /// Raised when an authored shell button requests a feature action.
    /// </summary>
    public event Action<UIWindow, int> ButtonRequested;

    /// <summary>
    /// Raised when the hosted feature requests a context menu at a desktop position.
    /// </summary>
    public event Action<UIWindow, PointerEventData, int, int> ContextRequested;

    /// <summary>
    /// Raised when a drag handle proposes new source-space bounds.
    /// </summary>
    public event Action<UIWindow, RectInt> MovePreviewChanged;

    /// <summary>
    /// Raised when the current move preview ends.
    /// </summary>
    public event Action<UIWindow> MovePreviewEnded;

    /// <summary>
    /// Raised after a drag handle commits a new window position.
    /// </summary>
    public event Action<UIWindow> Moved;

    public int Id { get; private set; }

    public bool Modal { get; private set; }

    public bool CanFocus { get; private set; }

    public bool CanMove { get; private set; }

    public bool ActiveWindow => activeWindow;

    public MonoBehaviour Content => content;

    /// <summary>
    /// Gets the source-space horizontal position.
    /// </summary>
    public int X => Mathf.RoundToInt(GetRectTransform().anchoredPosition.x);

    /// <summary>
    /// Gets the source-space vertical position.
    /// </summary>
    public int Y => Mathf.RoundToInt(-GetRectTransform().anchoredPosition.y);

    /// <summary>
    /// Gets the source-space width.
    /// </summary>
    public int Width => Mathf.RoundToInt(GetRectTransform().sizeDelta.x);

    /// <summary>
    /// Gets the source-space height.
    /// </summary>
    public int Height => Mathf.RoundToInt(GetRectTransform().sizeDelta.y);

    /// <summary>
    /// Gets the complete source-space window bounds.
    /// </summary>
    public RectInt Bounds => new RectInt(X, Y, Width, Height);

    /// <summary>
    /// Configures the window's runtime identity, capabilities, and source-space bounds.
    /// </summary>
    /// <param name="id">The runtime window identifier.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="modal">Whether this window blocks interaction with other windows.</param>
    /// <param name="canFocus">Whether the window can become active.</param>
    /// <param name="canMove">Whether the window can be dragged.</param>
    public void Configure(
        int id,
        int x,
        int y,
        int width,
        int height,
        bool modal,
        bool canFocus,
        bool canMove
    )
    {
        VerifyReferences();
        Id = id;
        Modal = modal;
        CanFocus = canFocus;
        CanMove = canMove;
        Resize(width, height);
        MoveTo(x, y);
    }

    /// <summary>
    /// Attaches the window to the manager that owns focus, modal, and movement policy.
    /// </summary>
    /// <param name="manager">The owning window manager.</param>
    public void Attach(UIWindowManager manager)
    {
        windowManager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <summary>
    /// Associates the window with its feature-specific view component.
    /// </summary>
    /// <param name="windowContent">The feature view hosted by this window.</param>
    public void SetContent(MonoBehaviour windowContent)
    {
        content = windowContent;
    }

    /// <summary>
    /// Attempts to read the hosted feature view through a requested contract.
    /// </summary>
    /// <typeparam name="TContent">The requested feature-view contract.</typeparam>
    /// <param name="typedContent">The hosted view when it implements the requested contract.</param>
    /// <returns>True when the hosted view implements the requested contract.</returns>
    public bool TryGetContent<TContent>(out TContent typedContent)
        where TContent : class
    {
        typedContent = content as TContent;
        return typedContent != null;
    }

    /// <summary>
    /// Moves the window to a source-space position without changing its size.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    public void MoveTo(int x, int y)
    {
        RectTransform rect = GetRectTransform();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// Resizes the window in source-space units.
    /// </summary>
    /// <param name="width">The non-negative source-space width.</param>
    /// <param name="height">The non-negative source-space height.</param>
    public void Resize(int width, int height)
    {
        GetRectTransform().sizeDelta = new Vector2(Mathf.Max(0, width), Mathf.Max(0, height));
    }

    /// <summary>
    /// Updates whether this is the manager's active window.
    /// </summary>
    /// <param name="active">Whether the window is active.</param>
    public void SetActiveWindow(bool active)
    {
        activeWindow = active;
    }

    /// <summary>
    /// Enables or blocks pointer interaction without changing visual opacity.
    /// </summary>
    /// <param name="blocked">Whether pointer interaction is blocked.</param>
    public void SetInputBlocked(bool blocked)
    {
        CanvasGroup group = GetInputGroup();
        group.blocksRaycasts = !blocked;
        group.interactable = !blocked;
    }

    /// <summary>
    /// Converts a screen position into the owning desktop's source-space coordinates.
    /// </summary>
    /// <param name="eventData">The pointer event providing the relevant camera.</param>
    /// <param name="screenPosition">The screen-space position.</param>
    /// <param name="x">The resolved source-space horizontal coordinate.</param>
    /// <param name="y">The resolved source-space vertical coordinate.</param>
    /// <returns>True when the position lies within the owning desktop.</returns>
    public bool TryGetDesktopPosition(
        PointerEventData eventData,
        Vector2 screenPosition,
        out int x,
        out int y
    )
    {
        x = 0;
        y = 0;
        return windowManager != null
            && windowManager.TryGetPosition(eventData, screenPosition, out x, out y);
    }

    /// <summary>
    /// Clamps a requested position to the owning manager's movement bounds.
    /// </summary>
    /// <param name="x">The requested source-space horizontal position.</param>
    /// <param name="y">The requested source-space vertical position.</param>
    /// <returns>The clamped source-space position.</returns>
    public Vector2Int ClampPosition(int x, int y)
    {
        return windowManager == null
            ? new Vector2Int(x, y)
            : windowManager.ClampPosition(x, y, new Vector2Int(Width, Height));
    }

    /// <summary>
    /// Requests focus through the owning manager.
    /// </summary>
    /// <returns>True when the window can receive interaction.</returns>
    public bool RequestFocus()
    {
        return CanFocus && windowManager != null && windowManager.Focus(this);
    }

    /// <summary>
    /// Emits a configured shell-button action when this window may interact.
    /// </summary>
    /// <param name="action">The feature-specific action identifier.</param>
    public void RequestButton(int action)
    {
        if (action != 0 && CanSendRequest())
            ButtonRequested?.Invoke(this, action);
    }

    /// <summary>
    /// Emits a context request with source-space pointer coordinates.
    /// </summary>
    /// <param name="eventData">The pointer event that opened the context menu.</param>
    public void RequestContext(PointerEventData eventData)
    {
        if (
            eventData == null
            || !TryGetDesktopPosition(eventData, eventData.position, out int x, out int y)
            || !CanSendRequest()
        )
        {
            return;
        }

        ContextRequested?.Invoke(this, eventData, x, y);
    }

    /// <summary>
    /// Gives hosted content the first cancellation opportunity, then requests window closure.
    /// </summary>
    /// <returns>True when cancellation was handled.</returns>
    public bool TryCancel()
    {
        if (!CanCancel())
            return false;
        if (content is ICancelable cancelable && cancelable.TryCancel())
            return true;
        return windowManager != null && windowManager.TryRequestClose(this);
    }

    /// <summary>
    /// Notifies listeners that a committed window move completed.
    /// </summary>
    public void NotifyMoved()
    {
        Moved?.Invoke(this);
    }

    /// <summary>
    /// Notifies listeners that the move-preview bounds changed.
    /// </summary>
    /// <param name="bounds">The proposed source-space window bounds.</param>
    public void NotifyMovePreviewChanged(RectInt bounds)
    {
        MovePreviewChanged?.Invoke(this, bounds);
    }

    /// <summary>
    /// Notifies listeners that the active move preview ended.
    /// </summary>
    public void NotifyMovePreviewEnded()
    {
        MovePreviewEnded?.Invoke(this);
    }

    /// <summary>
    /// Requests focus when the window body receives a pointer press.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        RequestFocus();
    }

    /// <summary>
    /// Caches authored component references and binds shell actions.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        BindActionButtons();
    }

    /// <summary>
    /// Removes shell-button listeners when the window is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        UnbindActionButtons();
    }

    /// <summary>
    /// Determines whether this window can emit an interaction request.
    /// </summary>
    /// <returns>True when modal and focus policy permit the request.</returns>
    private bool CanSendRequest()
    {
        if (windowManager != null && !windowManager.CanInteractWithWindow(this))
            return false;

        return !CanFocus || RequestFocus();
    }

    /// <summary>
    /// Determines whether this window currently owns cancellation handling.
    /// </summary>
    /// <returns>True when the active window may process cancellation.</returns>
    private bool CanCancel()
    {
        return activeWindow && (windowManager == null || windowManager.CanInteractWithWindow(this));
    }

    /// <summary>
    /// Resolves and caches the required rect transform.
    /// </summary>
    /// <returns>The window's rect transform.</returns>
    private RectTransform GetRectTransform()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        return rectTransform;
    }

    /// <summary>
    /// Resolves and caches the authored input group.
    /// </summary>
    /// <returns>The window's required canvas group.</returns>
    private CanvasGroup GetInputGroup()
    {
        if (inputGroup == null)
            throw new MissingReferenceException($"{name} is missing CanvasGroup.");

        return inputGroup;
    }

    /// <summary>
    /// Resolves required local components before interaction begins.
    /// </summary>
    private void ResolveReferences()
    {
        rectTransform = transform as RectTransform;
        if (inputGroup == null)
            inputGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Verifies the authored components required by the window shell.
    /// </summary>
    private void VerifyReferences()
    {
        ResolveReferences();
        if (rectTransform == null)
            throw new MissingReferenceException($"{name} is missing RectTransform.");
        if (inputGroup == null)
            throw new MissingReferenceException($"{name} is missing CanvasGroup.");
    }

    /// <summary>
    /// Binds configured shell buttons to their feature-specific action identifiers.
    /// </summary>
    private void BindActionButtons()
    {
        if (actionButtons == null || buttonActions == null)
            return;
        if (actionButtons.Length != buttonActions.Length)
            throw new InvalidOperationException($"{name} action-button bindings are incomplete.");

        for (int i = 0; i < actionButtons.Length; i++)
        {
            Button button = actionButtons[i];
            int action = buttonActions[i];
            if (button == null || action == 0)
                continue;

            UnityAction listener = () => RequestButton(action);
            button.onClick.AddListener(listener);
            boundActionButtons.Add(button);
            boundActionListeners.Add(listener);
        }
    }

    /// <summary>
    /// Removes every shell-button listener owned by this window.
    /// </summary>
    private void UnbindActionButtons()
    {
        for (int i = 0; i < boundActionButtons.Count; i++)
        {
            if (boundActionButtons[i] != null)
                boundActionButtons[i].onClick.RemoveListener(boundActionListeners[i]);
        }

        boundActionButtons.Clear();
        boundActionListeners.Clear();
    }
}
