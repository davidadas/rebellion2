using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Owns window identity, creation, registration, stacking, focus, shell requests, and desktop policy.
/// </summary>
public sealed class UIWindowManager : MonoBehaviour, ICancelable
{
    private readonly List<RaycastResult> raycastResults = new();
    private readonly List<UIWindow> windows = new();
    private RectTransform rectTransform;
    private RectInt? movementBounds;
    private int nextWindowId = 1;

    /// <summary>
    /// Raised when a registered window emits an authored button action.
    /// </summary>
    public event Action<UIWindow, int> WindowButtonRequested;

    /// <summary>
    /// Raised when a registered window requests a context menu.
    /// </summary>
    public event Action<UIWindow, PointerEventData, int, int> WindowContextRequested;

    /// <summary>
    /// Raised when a registered window proposes new source-space bounds.
    /// </summary>
    public event Action<UIWindow, RectInt> WindowMovePreviewChanged;

    /// <summary>
    /// Raised when a registered window ends its move preview.
    /// </summary>
    public event Action<UIWindow> WindowMovePreviewEnded;

    /// <summary>
    /// Raised after a registered window commits a new position.
    /// </summary>
    public event Action<UIWindow> WindowMoved;

    /// <summary>
    /// Raised when cancellation requests that the active window close.
    /// </summary>
    public event Action<UIWindow> WindowCloseRequested;

    /// <summary>
    /// Raised after a registered window becomes active.
    /// </summary>
    public event Action<UIWindow> FocusChanged;

    /// <summary>
    /// Raised after a window leaves the registry.
    /// </summary>
    public event Action<UIWindow> WindowClosed;

    /// <summary>
    /// Raised when a modal window enters the registry.
    /// </summary>
    public event Action<UIWindow> ModalOpened;

    /// <summary>
    /// Gets registered windows in bottom-to-top stacking order.
    /// </summary>
    public IReadOnlyList<UIWindow> Windows => windows;

    /// <summary>
    /// Gets the registered window that currently owns focus.
    /// </summary>
    public UIWindow ActiveWindow { get; private set; }

    /// <summary>
    /// Restricts movable windows to a source-space desktop rectangle.
    /// </summary>
    /// <param name="bounds">The allowed source-space movement bounds.</param>
    public void SetMovementBounds(RectInt bounds)
    {
        movementBounds = bounds;
    }

    /// <summary>
    /// Instantiates, configures, and registers one authored window view.
    /// </summary>
    /// <typeparam name="TView">The authored feature-view type.</typeparam>
    /// <param name="prefab">The authored feature-view prefab.</param>
    /// <param name="parent">The authored parent for the window modality.</param>
    /// <param name="hierarchyName">The semantic hierarchy name for the window instance.</param>
    /// <param name="x">The requested source-space horizontal position.</param>
    /// <param name="y">The requested source-space vertical position.</param>
    /// <param name="size">The authored source-space window size.</param>
    /// <param name="modal">Whether the window blocks interaction with other windows.</param>
    /// <param name="canFocus">Whether the window may receive focus.</param>
    /// <param name="canMove">Whether the window may be moved.</param>
    /// <param name="behind">Whether the new window starts behind the stack.</param>
    /// <param name="view">Receives the instantiated feature view.</param>
    /// <returns>The configured and registered window shell.</returns>
    public UIWindow CreateWindow<TView>(
        TView prefab,
        Transform parent,
        string hierarchyName,
        int x,
        int y,
        Vector2Int size,
        bool modal,
        bool canFocus,
        bool canMove,
        bool behind,
        out TView view
    )
        where TView : MonoBehaviour
    {
        if (prefab == null)
            throw new MissingReferenceException($"{typeof(TView).Name} prefab is missing.");
        if (parent == null)
            throw new MissingReferenceException("Window parent is missing.");
        if (size.x <= 0 || size.y <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        int id = AllocateWindowId();
        view = Instantiate(prefab, parent);
        view.name = GetUniqueHierarchyName(parent, hierarchyName, prefab.name, id);
        view.gameObject.SetActive(false);

        UIWindow window = GetRequiredWindowShell(view);
        window.SetContent(view);
        if (ShouldClampInitialPosition(modal, canMove))
        {
            Vector2Int position = ClampPosition(x, y, size);
            x = position.x;
            y = position.y;
        }

        window.Configure(id, x, y, size.x, size.y, modal, canFocus, canMove);
        Register(window, behind);
        view.gameObject.SetActive(true);
        return window;
    }

    /// <summary>
    /// Removes a registered window and destroys its hosted authored view.
    /// </summary>
    /// <param name="window">The registered window to destroy.</param>
    public void DestroyWindow(UIWindow window)
    {
        if (window == null || GetWindowById(window.Id) != window)
            return;

        MonoBehaviour view = window.Content;
        Unregister(window);
        if (view != null)
            Destroy(view.gameObject);
    }

    /// <summary>
    /// Registers a configured window and applies its initial stack position.
    /// </summary>
    /// <param name="window">The configured window.</param>
    /// <param name="behind">Whether to place it behind existing windows.</param>
    public void Register(UIWindow window, bool behind)
    {
        if (window == null)
            return;

        UIWindow registeredWindow = GetWindowById(window.Id);
        if (registeredWindow != null && registeredWindow != window)
            throw new InvalidOperationException($"Window ID {window.Id} is already registered.");

        bool wasRegistered = windows.Remove(window);
        if (behind)
            windows.Insert(0, window);
        else
            windows.Add(window);

        window.Attach(this);
        if (!wasRegistered)
            BindWindow(window);

        if (behind)
            window.transform.SetAsFirstSibling();
        else
            window.transform.SetAsLastSibling();

        if (!wasRegistered && window.Modal)
            ModalOpened?.Invoke(window);

        if (!window.CanFocus || !Focus(window))
            ApplyActiveState();
    }

    /// <summary>
    /// Removes a registered window and promotes the next eligible active window.
    /// </summary>
    /// <param name="window">The window to remove.</param>
    public void Unregister(UIWindow window)
    {
        if (window == null || !windows.Remove(window))
            return;

        if (ActiveWindow == window)
            ActiveWindow = GetTopFocusableWindow();

        window.SetActiveWindow(false);
        ApplyActiveState();
        UnbindWindow(window);
        WindowClosed?.Invoke(window);
    }

    /// <summary>
    /// Promotes an interactable focusable window to the top of the stack.
    /// </summary>
    /// <param name="window">The window requesting focus.</param>
    /// <returns>True when the window received focus.</returns>
    public bool Focus(UIWindow window)
    {
        if (window == null || !window.CanFocus || !CanInteractWithWindow(window))
            return false;

        windows.Remove(window);
        windows.Add(window);
        window.transform.SetAsLastSibling();
        ActiveWindow = window;
        ApplyActiveState();
        FocusChanged?.Invoke(window);
        return true;
    }

    /// <summary>
    /// Reports whether a registered modal window currently owns interaction.
    /// </summary>
    /// <returns>True when a modal window is registered.</returns>
    public bool HasModalWindow()
    {
        return GetTopModalWindow() != null;
    }

    /// <summary>
    /// Determines whether a registered window may receive input under current modal policy.
    /// </summary>
    /// <param name="window">The window to inspect.</param>
    /// <returns>True when the window is registered and not blocked by another modal window.</returns>
    public bool CanInteractWithWindow(UIWindow window)
    {
        if (window == null || !windows.Contains(window))
            return false;

        UIWindow modalWindow = GetTopModalWindow();
        return modalWindow == null || modalWindow == window;
    }

    /// <summary>
    /// Finds a registered window by runtime identifier.
    /// </summary>
    /// <param name="windowId">The runtime window identifier.</param>
    /// <returns>The matching window, or null when it is not registered.</returns>
    public UIWindow GetWindowById(int windowId)
    {
        foreach (UIWindow window in windows)
        {
            if (window != null && window.Id == windowId)
                return window;
        }

        return null;
    }

    /// <summary>
    /// Resolves the authored content hosted by a registered window.
    /// </summary>
    /// <typeparam name="TView">The requested authored view type.</typeparam>
    /// <param name="window">The registered window to inspect.</param>
    /// <param name="view">Receives the hosted view when its type matches.</param>
    /// <returns>True when the registered window hosts the requested view type.</returns>
    public bool TryGetWindowView<TView>(UIWindow window, out TView view)
        where TView : class
    {
        view = null;
        return window != null
            && GetWindowById(window.Id) == window
            && window.TryGetContent(out view);
    }

    /// <summary>
    /// Gives the active interactable window the opportunity to cancel its current operation.
    /// </summary>
    /// <returns>True when the active window handled cancellation.</returns>
    public bool TryCancel()
    {
        return ActiveWindow != null
            && CanInteractWithWindow(ActiveWindow)
            && ActiveWindow.TryCancel();
    }

    /// <summary>
    /// Emits a close request for the active registered window when one can be handled.
    /// </summary>
    /// <param name="window">The window requesting closure.</param>
    /// <returns>True when a close-request listener accepted the request.</returns>
    internal bool TryRequestClose(UIWindow window)
    {
        if (window == null || WindowCloseRequested == null || !CanInteractWithWindow(window))
            return false;

        WindowCloseRequested.Invoke(window);
        return true;
    }

    /// <summary>
    /// Resolves the topmost interactable registered window under a pointer event.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>The interactable window under the pointer, or null.</returns>
    public UIWindow GetWindow(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        UIWindow window = GetInteractableRegisteredWindow(
            eventData.pointerCurrentRaycast.gameObject
        );
        if (window != null)
            return window;

        if (EventSystem.current == null)
            return null;

        raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        foreach (RaycastResult result in raycastResults)
        {
            window = GetInteractableRegisteredWindow(result.gameObject);
            if (window != null)
                break;
        }

        raycastResults.Clear();
        return window;
    }

    /// <summary>
    /// Clamps a top-left window position to the configured desktop movement bounds.
    /// </summary>
    /// <param name="x">The requested source-space horizontal position.</param>
    /// <param name="y">The requested source-space vertical position.</param>
    /// <param name="windowSize">The window's source-space size.</param>
    /// <returns>The clamped source-space position.</returns>
    public Vector2Int ClampPosition(int x, int y, Vector2Int windowSize)
    {
        RectInt bounds = GetMovementBounds();
        int maxX = bounds.xMax - windowSize.x;
        int maxY = bounds.yMax - windowSize.y;
        return new Vector2Int(
            ClampCoordinate(x, bounds.xMin, maxX),
            ClampCoordinate(y, bounds.yMin, maxY)
        );
    }

    /// <summary>
    /// Converts a screen position into top-left source-space desktop coordinates.
    /// </summary>
    /// <param name="eventData">The pointer event providing the relevant camera.</param>
    /// <param name="screenPosition">The screen-space position.</param>
    /// <param name="x">The resolved source-space horizontal coordinate.</param>
    /// <param name="y">The resolved source-space vertical coordinate.</param>
    /// <returns>True when the resolved point lies inside this desktop.</returns>
    public bool TryGetPosition(
        PointerEventData eventData,
        Vector2 screenPosition,
        out int x,
        out int y
    )
    {
        x = 0;
        y = 0;

        if (eventData == null)
            return false;

        RectTransform rect = GetRectTransform();
        if (rect == null)
            return false;

        Camera camera = eventData.pressEventCamera;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                screenPosition,
                camera,
                out Vector2 local
            )
        )
        {
            return false;
        }

        Vector2Int managerSize = GetSize();
        if (managerSize.x <= 0 || managerSize.y <= 0)
            return false;

        x = Mathf.RoundToInt(local.x + managerSize.x / 2f);
        y = Mathf.RoundToInt(managerSize.y / 2f - local.y);
        return x >= 0 && x < managerSize.x && y >= 0 && y < managerSize.y;
    }

    /// <summary>
    /// Synchronizes active-window and modal-blocking state across registered windows.
    /// </summary>
    private void ApplyActiveState()
    {
        UIWindow modalWindow = GetTopModalWindow();
        foreach (UIWindow window in windows)
        {
            if (window == null)
                continue;

            window.SetActiveWindow(window == ActiveWindow);
            window.SetInputBlocked(modalWindow != null && window != modalWindow);
        }
    }

    /// <summary>
    /// Resolves the manager's current source-space size.
    /// </summary>
    /// <returns>The manager size.</returns>
    private Vector2Int GetSize()
    {
        RectTransform rect = GetRectTransform();
        if (rect == null)
            return Vector2Int.zero;

        int width = Mathf.RoundToInt(rect.sizeDelta.x);
        int height = Mathf.RoundToInt(rect.sizeDelta.y);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.rect.width);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.rect.height);

        return new Vector2Int(width, height);
    }

    /// <summary>
    /// Resolves explicit movement bounds or falls back to the entire desktop.
    /// </summary>
    /// <returns>The active movement bounds.</returns>
    private RectInt GetMovementBounds()
    {
        if (movementBounds.HasValue)
            return movementBounds.Value;

        Vector2Int managerSize = GetSize();
        return new RectInt(0, 0, managerSize.x, managerSize.y);
    }

    /// <summary>
    /// Clamps one coordinate, including bounds smaller than the requested window.
    /// </summary>
    /// <param name="value">The requested coordinate.</param>
    /// <param name="min">The minimum coordinate.</param>
    /// <param name="max">The maximum coordinate.</param>
    /// <returns>The clamped coordinate.</returns>
    private static int ClampCoordinate(int value, int min, int max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;

        return value;
    }

    /// <summary>
    /// Allocates the next unused runtime window identifier.
    /// </summary>
    /// <returns>The allocated identifier.</returns>
    private int AllocateWindowId()
    {
        while (GetWindowById(nextWindowId) != null)
            nextWindowId++;

        return nextWindowId++;
    }

    /// <summary>
    /// Resolves a readable sibling-unique hierarchy name for a window instance.
    /// </summary>
    /// <param name="parent">The window's hierarchy parent.</param>
    /// <param name="requestedName">The requested semantic name.</param>
    /// <param name="fallbackName">The prefab name used when no semantic name was supplied.</param>
    /// <param name="id">The runtime identifier used only to disambiguate a collision.</param>
    /// <returns>The sanitized hierarchy name.</returns>
    private static string GetUniqueHierarchyName(
        Transform parent,
        string requestedName,
        string fallbackName,
        int id
    )
    {
        string name = string.IsNullOrWhiteSpace(requestedName)
            ? fallbackName
            : requestedName.Trim();
        name = name.Replace('/', '-').Replace('\\', '-');

        for (int index = 0; index < parent.childCount; index++)
        {
            if (string.Equals(parent.GetChild(index).name, name, StringComparison.Ordinal))
                return $"{name}-{id}";
        }

        return name;
    }

    /// <summary>
    /// Subscribes to generic interaction requests from a newly registered window.
    /// </summary>
    /// <param name="window">The registered window.</param>
    private void BindWindow(UIWindow window)
    {
        window.ButtonRequested += HandleWindowButtonRequested;
        window.ContextRequested += HandleWindowContextRequested;
        window.MovePreviewChanged += HandleWindowMovePreviewChanged;
        window.MovePreviewEnded += HandleWindowMovePreviewEnded;
        window.Moved += HandleWindowMoved;
    }

    /// <summary>
    /// Releases generic interaction subscriptions from a departing window.
    /// </summary>
    /// <param name="window">The departing window.</param>
    private void UnbindWindow(UIWindow window)
    {
        window.ButtonRequested -= HandleWindowButtonRequested;
        window.ContextRequested -= HandleWindowContextRequested;
        window.MovePreviewChanged -= HandleWindowMovePreviewChanged;
        window.MovePreviewEnded -= HandleWindowMovePreviewEnded;
        window.Moved -= HandleWindowMoved;
    }

    /// <summary>
    /// Forwards an authored button request from a registered window.
    /// </summary>
    /// <param name="window">The requesting window.</param>
    /// <param name="action">The authored action identifier.</param>
    private void HandleWindowButtonRequested(UIWindow window, int action)
    {
        WindowButtonRequested?.Invoke(window, action);
    }

    /// <summary>
    /// Forwards a context request from a registered window.
    /// </summary>
    /// <param name="window">The requesting window.</param>
    /// <param name="eventData">The source pointer event.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    private void HandleWindowContextRequested(
        UIWindow window,
        PointerEventData eventData,
        int x,
        int y
    )
    {
        WindowContextRequested?.Invoke(window, eventData, x, y);
    }

    /// <summary>
    /// Forwards proposed move-preview bounds from a registered window.
    /// </summary>
    /// <param name="window">The moving window.</param>
    /// <param name="bounds">The proposed source-space bounds.</param>
    private void HandleWindowMovePreviewChanged(UIWindow window, RectInt bounds)
    {
        WindowMovePreviewChanged?.Invoke(window, bounds);
    }

    /// <summary>
    /// Forwards the end of a registered window's move preview.
    /// </summary>
    /// <param name="window">The moving window.</param>
    private void HandleWindowMovePreviewEnded(UIWindow window)
    {
        WindowMovePreviewEnded?.Invoke(window);
    }

    /// <summary>
    /// Forwards a committed move from a registered window.
    /// </summary>
    /// <param name="window">The moved window.</param>
    private void HandleWindowMoved(UIWindow window)
    {
        WindowMoved?.Invoke(window);
    }

    /// <summary>
    /// Resolves the required authored shell hosted by a feature view.
    /// </summary>
    /// <param name="view">The instantiated feature view.</param>
    /// <returns>The local authored window shell.</returns>
    private static UIWindow GetRequiredWindowShell(MonoBehaviour view)
    {
        UIWindow window = view.GetComponent<UIWindow>();
        if (window == null)
            throw new MissingReferenceException($"{view.name} is missing UIWindow.");

        return window;
    }

    /// <summary>
    /// Determines whether initial placement must stay within configured movement bounds.
    /// </summary>
    /// <param name="modal">Whether the window is modal.</param>
    /// <param name="canMove">Whether the window may move after opening.</param>
    /// <returns>True when the initial position must be clamped.</returns>
    private static bool ShouldClampInitialPosition(bool modal, bool canMove)
    {
        return canMove || !modal;
    }

    /// <summary>
    /// Finds the registered interactable window containing a raycast target.
    /// </summary>
    /// <param name="target">The raycast target.</param>
    /// <returns>The containing window, or null.</returns>
    private UIWindow GetInteractableRegisteredWindow(GameObject target)
    {
        UIWindow window = target == null ? null : target.GetComponentInParent<UIWindow>();
        return CanInteractWithWindow(window) ? window : null;
    }

    /// <summary>
    /// Finds the topmost registered modal window.
    /// </summary>
    /// <returns>The topmost modal window, or null.</returns>
    private UIWindow GetTopModalWindow()
    {
        for (int index = windows.Count - 1; index >= 0; index--)
        {
            UIWindow window = windows[index];
            if (window != null && window.Modal)
                return window;
        }

        return null;
    }

    /// <summary>
    /// Finds the topmost focusable window permitted by modal policy.
    /// </summary>
    /// <returns>The next eligible active window, or null.</returns>
    private UIWindow GetTopFocusableWindow()
    {
        UIWindow modalWindow = GetTopModalWindow();
        if (modalWindow != null)
            return modalWindow.CanFocus ? modalWindow : null;

        for (int index = windows.Count - 1; index >= 0; index--)
        {
            UIWindow window = windows[index];
            if (window != null && window.CanFocus)
                return window;
        }

        return null;
    }

    /// <summary>
    /// Resolves and caches this desktop's RectTransform.
    /// </summary>
    /// <returns>The desktop RectTransform.</returns>
    private RectTransform GetRectTransform()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        return rectTransform;
    }
}
