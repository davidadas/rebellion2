using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Routes strategy-surface pointer gestures to targeting, dragging, windows, and the galaxy map.
/// </summary>
public sealed class StrategyScreenInputController : ICancelable
{
    private readonly GalaxyMapController galaxyMapController;
    private readonly TargetingController targetingController;
    private readonly StrategyContextMenuRouter strategyContextMenuRouter;
    private readonly UIWindowManager windowManager;
    private readonly Func<UIWindow, bool> trySelectWindowPlanetTarget;
    private readonly Func<UIWindow, bool> tryOpenStatusWindow;
    private readonly StrategyDragController strategyDragController;
    private readonly StrategyPointerPositionResolver tryGetSourcePosition;
    private readonly Action markDirty;
    private readonly Action renderOverlay;
    private UIWindow pressedWindow;
    private bool suppressNextClick;

    /// <summary>
    /// Creates the strategy-surface input router.
    /// </summary>
    /// <param name="galaxyMapController">The galaxy-map interaction owner.</param>
    /// <param name="targetingController">The active targeting owner.</param>
    /// <param name="strategyContextMenuRouter">Routes strategy context commands.</param>
    /// <param name="windowManager">Owns window lookup, focus, and modal policy.</param>
    /// <param name="trySelectWindowPlanetTarget">Attempts to complete targeting with a window's planet.</param>
    /// <param name="tryOpenStatusWindow">Attempts to open status for a window selection.</param>
    /// <param name="strategyDragController">Owns strategy drag gestures.</param>
    /// <param name="tryGetSourcePosition">Resolves source-space pointer coordinates.</param>
    /// <param name="markDirty">Invalidates the strategy presentation.</param>
    /// <param name="renderOverlay">Renders immediate overlay feedback.</param>
    public StrategyScreenInputController(
        GalaxyMapController galaxyMapController,
        TargetingController targetingController,
        StrategyContextMenuRouter strategyContextMenuRouter,
        UIWindowManager windowManager,
        Func<UIWindow, bool> trySelectWindowPlanetTarget,
        Func<UIWindow, bool> tryOpenStatusWindow,
        StrategyDragController strategyDragController,
        StrategyPointerPositionResolver tryGetSourcePosition,
        Action markDirty,
        Action renderOverlay
    )
    {
        this.galaxyMapController =
            galaxyMapController ?? throw new ArgumentNullException(nameof(galaxyMapController));
        this.targetingController =
            targetingController ?? throw new ArgumentNullException(nameof(targetingController));
        this.strategyContextMenuRouter =
            strategyContextMenuRouter
            ?? throw new ArgumentNullException(nameof(strategyContextMenuRouter));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.trySelectWindowPlanetTarget =
            trySelectWindowPlanetTarget
            ?? throw new ArgumentNullException(nameof(trySelectWindowPlanetTarget));
        this.tryOpenStatusWindow =
            tryOpenStatusWindow ?? throw new ArgumentNullException(nameof(tryOpenStatusWindow));
        this.strategyDragController =
            strategyDragController
            ?? throw new ArgumentNullException(nameof(strategyDragController));
        this.tryGetSourcePosition =
            tryGetSourcePosition ?? throw new ArgumentNullException(nameof(tryGetSourcePosition));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        this.renderOverlay =
            renderOverlay ?? throw new ArgumentNullException(nameof(renderOverlay));
    }

    /// <summary>
    /// Routes one pointer press to targeting, context menus, or window focus.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (!tryGetSourcePosition(eventData, eventData.position, out int x, out int y))
            return;

        suppressNextClick = false;

        if (targetingController.IsTargeting)
        {
            targetingController.MoveCursor(x, y);
            suppressNextClick = true;
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            targetingController.Cancel();
            strategyContextMenuRouter.OpenContextMenu(eventData, x, y);
            markDirty();
            return;
        }

        pressedWindow = windowManager.GetWindow(eventData);
        bool handledWindow = false;

        if (pressedWindow != null)
        {
            windowManager.Focus(pressedWindow);
            handledWindow = true;
        }

        if (!handledWindow && !windowManager.HasModalWindow())
            pressedWindow = null;

        markDirty();
    }

    /// <summary>
    /// Completes an item drag or active targeting gesture.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (ApplyDragEventResult(strategyDragController.TryHandleItemPointerUp(eventData)))
            return;

        if (eventData == null)
            return;

        if (!tryGetSourcePosition(eventData, eventData.position, out int x, out int y))
        {
            if (targetingController.IsTargeting)
            {
                targetingController.Cancel();
                suppressNextClick = true;
                markDirty();
            }

            return;
        }

        if (targetingController.IsTargeting)
        {
            targetingController.MoveCursor(x, y);
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                UIWindow targetWindow = windowManager.GetWindow(eventData);
                if (trySelectWindowPlanetTarget(targetWindow))
                {
                    suppressNextClick = true;
                    markDirty();
                    return;
                }

                if (
                    galaxyMapController.TryGetMissionTarget(
                        eventData,
                        out StrategyMissionTarget target
                    ) == true
                    && targetingController.TrySelectTarget(target)
                )
                {
                    suppressNextClick = true;
                    markDirty();
                    return;
                }

                if (targetingController.IsTargeting)
                    targetingController.Cancel();
                suppressNextClick = true;
            }

            markDirty();
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
            return;

        if (pressedWindow != null)
        {
            strategyDragController.ClearItemDrag();
            pressedWindow = null;
            markDirty();
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Cancels active targeting and suppresses its trailing click.
    /// </summary>
    /// <returns>True when targeting was cancelled.</returns>
    public bool CancelTargeting()
    {
        if (!targetingController.TryCancel())
            return false;

        suppressNextClick = true;
        markDirty();
        return true;
    }

    /// <summary>
    /// Tries to cancel active strategy-surface interaction.
    /// </summary>
    /// <returns>True when targeting was cancelled.</returns>
    public bool TryCancel()
    {
        return CancelTargeting();
    }

    /// <summary>
    /// Routes pointer movement without starting a new item drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerMove(PointerEventData eventData)
    {
        HandlePointerMove(eventData, false);
    }

    /// <summary>
    /// Routes pointer movement that may advance an item drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        HandlePointerMove(eventData, true);
    }

    /// <summary>
    /// Routes a completed click, including status-window double clicks.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (!tryGetSourcePosition(eventData, eventData.position, out int x, out int y))
            return;

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        if (eventData.clickCount < 2)
            return;

        UIWindow clickedWindow = windowManager.GetWindow(eventData);
        if (TryOpenStatusForDoubleClick(eventData, clickedWindow))
        {
            markDirty();
            return;
        }

        if (clickedWindow != null)
            markDirty();
    }

    /// <summary>
    /// Clears transient input state owned by a closing window.
    /// </summary>
    /// <param name="window">The closing window.</param>
    public void ClearWindow(UIWindow window)
    {
        if (pressedWindow == window)
            pressedWindow = null;
    }

    /// <summary>
    /// Suppresses the next completed click after another gesture consumes it.
    /// </summary>
    public void SuppressNextClick()
    {
        suppressNextClick = true;
    }

    /// <summary>
    /// Begins tracking an item drag from a feature window.
    /// </summary>
    /// <param name="sourceWindow">The source strategy window.</param>
    /// <param name="sourceX">The source-space horizontal press coordinate.</param>
    /// <param name="sourceY">The source-space vertical press coordinate.</param>
    public void StartItemDrag(UIWindow sourceWindow, int sourceX, int sourceY)
    {
        if (sourceWindow != null)
            strategyDragController.StartItemCandidate(sourceWindow, sourceX, sourceY);
    }

    /// <summary>
    /// Routes pointer movement across drag, targeting, window, and map states.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="allowItemDrag">Whether item dragging may advance.</param>
    private void HandlePointerMove(PointerEventData eventData, bool allowItemDrag)
    {
        if (eventData == null)
            return;

        if (!tryGetSourcePosition(eventData, eventData.position, out int x, out int y))
            return;

        if (
            allowItemDrag
            && ApplyDragEventResult(strategyDragController.TryHandleItemPointerMove(x, y))
        )
            return;

        if (targetingController.IsTargeting)
        {
            targetingController.MoveCursor(x, y);
            return;
        }

        if (IsContextMenuOpen())
        {
            return;
        }

        UIWindow window = windowManager.GetWindow(eventData);
        if (window != null && windowManager.TryGetWindowView(window, out PlanetSystemWindowView _))
        {
            galaxyMapController.ClearHover();
            return;
        }

        if (window == null && CanInteractWithGalaxy())
            return;

        if (galaxyMapController.ClearHover())
            markDirty();
    }

    /// <summary>
    /// Applies state changes returned by the strategy drag controller.
    /// </summary>
    /// <param name="result">The drag event result.</param>
    /// <returns>True when the drag controller handled the event.</returns>
    private bool ApplyDragEventResult(StrategyDragEventResult result)
    {
        if (!result.Handled)
            return false;

        if (result.ClearPressedWindow)
            pressedWindow = null;
        if (result.SuppressClick)
            suppressNextClick = true;
        if (result.Dirty)
            markDirty();
        if (result.RenderOverlay)
            renderOverlay();

        return true;
    }

    /// <summary>
    /// Determines whether map interaction is available.
    /// </summary>
    /// <returns>True when no context menu or modal window blocks the map.</returns>
    private bool CanInteractWithGalaxy()
    {
        return !IsContextMenuOpen() && !windowManager.HasModalWindow();
    }

    /// <summary>
    /// Determines whether either context-menu implementation is open.
    /// </summary>
    /// <returns>True when a context menu is open.</returns>
    private bool IsContextMenuOpen()
    {
        return strategyContextMenuRouter.IsOpen;
    }

    /// <summary>
    /// Opens status for a double-click target inside one strategy window.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="clickedWindow">The clicked strategy window.</param>
    /// <returns>True when a status window was requested.</returns>
    private bool TryOpenStatusForDoubleClick(PointerEventData eventData, UIWindow clickedWindow)
    {
        if (clickedWindow == null || !IsStatusDoubleClickTarget(eventData))
            return false;

        return tryOpenStatusWindow(clickedWindow);
    }

    /// <summary>
    /// Determines whether the raycast hierarchy supports status double click.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>True when a status target marker was hit.</returns>
    private static bool IsStatusDoubleClickTarget(PointerEventData eventData)
    {
        GameObject target = GetRaycastTarget(eventData);
        return target != null
            && target.GetComponentInParent<IStrategyStatusDoubleClickTarget>() != null;
    }

    /// <summary>
    /// Gets the current or pressed pointer raycast target.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>The resolved raycast target, or null.</returns>
    private static GameObject GetRaycastTarget(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        return eventData.pointerCurrentRaycast.gameObject
            ?? eventData.pointerPressRaycast.gameObject;
    }
}
