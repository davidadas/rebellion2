using System;
using UnityEngine.EventSystems;

public sealed class StrategyScreenInputController : ICancelable
{
    private readonly StrategyContextMenuPresenter strategyContextMenu;
    private readonly GalaxyMapView galaxyMap;
    private readonly TargetingController targetingController;
    private readonly ContextMenuController contextMenuController;
    private readonly StrategyContextMenuRouter strategyContextMenuRouter;
    private readonly StrategyWindowCommandController windowCommandController;
    private readonly StrategyDragController strategyDragController;
    private readonly StrategyPointerPositionResolver tryGetSourcePosition;
    private readonly Func<PointerEventData, UIWindow> getWindow;
    private readonly Action markDirty;
    private readonly Action renderOverlay;
    private UIWindow pressedWindow;
    private bool suppressNextClick;

    public StrategyScreenInputController(
        StrategyContextMenuPresenter strategyContextMenu,
        GalaxyMapView galaxyMap,
        TargetingController targetingController,
        ContextMenuController contextMenuController,
        StrategyContextMenuRouter strategyContextMenuRouter,
        StrategyWindowCommandController windowCommandController,
        StrategyDragController strategyDragController,
        StrategyPointerPositionResolver tryGetSourcePosition,
        Func<PointerEventData, UIWindow> getWindow,
        Action markDirty,
        Action renderOverlay
    )
    {
        this.strategyContextMenu = strategyContextMenu;
        this.galaxyMap = galaxyMap;
        this.targetingController = targetingController;
        this.contextMenuController = contextMenuController;
        this.strategyContextMenuRouter =
            strategyContextMenuRouter
            ?? throw new ArgumentNullException(nameof(strategyContextMenuRouter));
        this.windowCommandController =
            windowCommandController
            ?? throw new ArgumentNullException(nameof(windowCommandController));
        this.strategyDragController =
            strategyDragController
            ?? throw new ArgumentNullException(nameof(strategyDragController));
        this.tryGetSourcePosition =
            tryGetSourcePosition ?? throw new ArgumentNullException(nameof(tryGetSourcePosition));
        this.getWindow = getWindow ?? throw new ArgumentNullException(nameof(getWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        this.renderOverlay =
            renderOverlay ?? throw new ArgumentNullException(nameof(renderOverlay));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!tryGetSourcePosition(eventData, eventData.position, out int x, out int y))
            return;

        suppressNextClick = false;

        if (targetingController?.IsTargeting == true)
        {
            targetingController.MoveCursor(x, y);
            suppressNextClick = true;
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            targetingController?.Cancel();
            strategyContextMenuRouter.OpenContextMenu(eventData, x, y);
            markDirty();
            return;
        }

        pressedWindow = getWindow(eventData);
        bool handledWindow = false;

        if (pressedWindow != null)
        {
            windowCommandController.FocusWindow(pressedWindow);
            handledWindow = true;
        }

        if (!handledWindow && !windowCommandController.HasModalWindow())
            pressedWindow = null;

        markDirty();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (ApplyDragEventResult(strategyDragController.TryHandleItemPointerUp(eventData)))
            return;

        if (eventData == null)
            return;

        if (!tryGetSourcePosition(eventData, eventData.position, out int x, out int y))
        {
            if (targetingController?.IsTargeting == true)
            {
                targetingController.Cancel();
                suppressNextClick = true;
                markDirty();
            }

            return;
        }

        if (targetingController?.IsTargeting == true)
        {
            targetingController.MoveCursor(x, y);
            if (eventData.button == PointerEventData.InputButton.Left)
            {
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

    public bool CancelTargeting()
    {
        if (targetingController?.TryCancel() != true)
            return false;

        suppressNextClick = true;
        markDirty();
        return true;
    }

    public bool TryCancel()
    {
        return CancelTargeting();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        HandlePointerMove(eventData, false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        HandlePointerMove(eventData, true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
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

        UIWindow clickedWindow = getWindow(eventData);
        if (clickedWindow != null)
            markDirty();
    }

    public void StartWindowItemDrag(StrategyUIRequests.StartWindowItemDrag request)
    {
        if (request == null)
            return;

        strategyDragController.StartItemCandidate(
            request.WindowId,
            request.SourceX,
            request.SourceY
        );
        markDirty();
    }

    public void MoveWindowItemDrag(StrategyUIRequests.WindowItemDragMove request)
    {
        PointerEventData eventData = request?.EventData;
        if (
            eventData == null
            || !tryGetSourcePosition(eventData, eventData.position, out int x, out int y)
        )
            return;

        ApplyDragEventResult(strategyDragController.TryHandleItemPointerMove(eventData, x, y));
    }

    public void EndWindowItemDrag(StrategyUIRequests.WindowItemDragEnd request)
    {
        PointerEventData eventData = request?.EventData;
        if (eventData == null)
            return;

        if (ApplyDragEventResult(strategyDragController.TryHandleItemPointerUp(eventData)))
            return;
    }

    public void ClearWindow(UIWindow window)
    {
        if (pressedWindow == window)
            pressedWindow = null;
    }

    public void SuppressNextClick()
    {
        suppressNextClick = true;
    }

    private void HandlePointerMove(PointerEventData eventData, bool allowItemDrag)
    {
        if (!tryGetSourcePosition(eventData, eventData.position, out int x, out int y))
            return;

        if (
            allowItemDrag
            && ApplyDragEventResult(
                strategyDragController.TryHandleItemPointerMove(eventData, x, y)
            )
        )
            return;

        if (targetingController?.IsTargeting == true)
        {
            targetingController.MoveCursor(x, y);
            return;
        }

        if (IsContextMenuOpen())
        {
            return;
        }

        UIWindow window = getWindow(eventData);
        if (
            window != null
            && windowCommandController.TryGetWindowView(window, out PlanetSystemWindowView _)
        )
        {
            galaxyMap?.ClearHover();
            return;
        }

        if (window == null && CanInteractWithGalaxy())
            return;

        if (galaxyMap != null && galaxyMap.ClearHover())
            markDirty();
    }

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

    private bool CanInteractWithGalaxy()
    {
        return !IsContextMenuOpen() && !windowCommandController.HasModalWindow();
    }

    private bool IsContextMenuOpen()
    {
        return contextMenuController?.IsOpen == true
            || strategyContextMenu != null && strategyContextMenu.Open;
    }
}
