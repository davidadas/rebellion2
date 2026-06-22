using System;
using UnityEngine;
using UnityEngine.EventSystems;

public delegate bool StrategyPointerPositionResolver(
    PointerEventData eventData,
    Vector2 screenPosition,
    out int x,
    out int y
);

public readonly struct StrategyDragEventResult
{
    private StrategyDragEventResult(
        bool handled,
        bool renderOverlay,
        bool suppressClick,
        bool clearPressedWindow,
        bool dirty
    )
    {
        Handled = handled;
        RenderOverlay = renderOverlay;
        SuppressClick = suppressClick;
        ClearPressedWindow = clearPressedWindow;
        Dirty = dirty;
    }

    public bool Handled { get; }
    public bool RenderOverlay { get; }
    public bool SuppressClick { get; }
    public bool ClearPressedWindow { get; }
    public bool Dirty { get; }

    public static StrategyDragEventResult None => new StrategyDragEventResult();
    public static StrategyDragEventResult HandledOnly =>
        new StrategyDragEventResult(true, false, false, false, false);
    public static StrategyDragEventResult SourceDragVisible =>
        new StrategyDragEventResult(true, true, true, false, false);
    public static StrategyDragEventResult TargetingStarted =>
        new StrategyDragEventResult(true, false, false, true, false);
    public static StrategyDragEventResult SourceDragStarted =>
        new StrategyDragEventResult(true, true, true, true, false);
    public static StrategyDragEventResult ItemDragFinished =>
        new StrategyDragEventResult(true, false, true, true, true);
}

public sealed class StrategyDragController
{
    private const int _itemDragStartDistance = 5;

    private readonly StrategyWindowItemDragController itemDragController;
    private readonly Func<int, UIWindow> getWindowById;
    private readonly StrategyPointerPositionResolver resolvePointerPosition;

    public StrategyDragController(
        TargetingController targetingController,
        StrategyWindowLayerView windowLayer,
        Func<int, UIWindow> getWindowById,
        StrategyPointerPositionResolver resolvePointerPosition,
        IStrategyWindowCommandActions commands
    )
    {
        this.getWindowById =
            getWindowById ?? throw new ArgumentNullException(nameof(getWindowById));
        this.resolvePointerPosition =
            resolvePointerPosition
            ?? throw new ArgumentNullException(nameof(resolvePointerPosition));
        itemDragController = new StrategyWindowItemDragController(
            targetingController,
            new DragController(_itemDragStartDistance),
            windowLayer,
            commands
        );
    }

    private bool HasItemCandidate => itemDragController.HasCandidate;
    private bool HasItemState =>
        itemDragController.HasCandidate || itemDragController.SourceDragActive;

    public void StartItemCandidate(int windowId, int x, int y)
    {
        StartItemCandidate(getWindowById(windowId), x, y);
    }

    private void StartItemCandidate(UIWindow window, int x, int y)
    {
        itemDragController.StartCandidate(window, x, y);
    }

    public StrategyDragEventResult TryHandleItemPointerMove(PointerEventData eventData)
    {
        if (
            !TryResolvePointerPosition(
                eventData,
                eventData == null ? Vector2.zero : eventData.position,
                out int x,
                out int y
            )
        )
            return StrategyDragEventResult.None;

        return TryHandleItemPointerMove(eventData, x, y);
    }

    public StrategyDragEventResult TryHandleItemPointerMove(
        PointerEventData eventData,
        int x,
        int y
    )
    {
        if (TryMoveItemDrag(x, y))
            return StrategyDragEventResult.SourceDragVisible;

        if (!HasItemCandidate)
            return StrategyDragEventResult.None;

        return TryStartItemDragFromCandidateForPointerMove(x, y);
    }

    public StrategyDragEventResult TryHandleItemPointerUp(PointerEventData eventData)
    {
        bool hadItemState = HasItemState;
        if (
            !TryResolvePointerPosition(
                eventData,
                eventData == null ? Vector2.zero : eventData.position,
                out int x,
                out int y
            )
        )
        {
            if (!hadItemState)
                return StrategyDragEventResult.None;

            ClearItemDrag();
            return StrategyDragEventResult.ItemDragFinished;
        }

        if (TryFinishItemDrag(eventData, x, y))
            return StrategyDragEventResult.ItemDragFinished;

        ClearItemDrag();
        return StrategyDragEventResult.None;
    }

    private StrategyWindowItemDragStartResult TryStartItemDragFromCandidate(int x, int y)
    {
        return itemDragController.TryStartMoveDragFromCandidate(x, y);
    }

    private bool TryMoveItemDrag(int x, int y)
    {
        return itemDragController.TryMoveSourceDrag(x, y);
    }

    private bool TryFinishItemDrag(PointerEventData eventData, int x, int y)
    {
        return itemDragController.TryHandleSourceDragPointerUp(eventData, x, y);
    }

    public void ClearItemDrag()
    {
        itemDragController.Clear();
    }

    public void ApplyOverlay(StrategyOverlayRenderData data, bool contextMenuOpen)
    {
        itemDragController.ApplyOverlay(data);
    }

    public void ClearWindow(UIWindow window)
    {
        itemDragController.ClearWindow(window);
    }

    public void Clear()
    {
        itemDragController.Clear();
    }

    private StrategyDragEventResult TryStartItemDragFromCandidateForPointerMove(int x, int y)
    {
        StrategyWindowItemDragStartResult result = TryStartItemDragFromCandidate(x, y);
        return result switch
        {
            StrategyWindowItemDragStartResult.TargetingStarted =>
                StrategyDragEventResult.TargetingStarted,
            StrategyWindowItemDragStartResult.SourceDragStarted =>
                StrategyDragEventResult.SourceDragStarted,
            StrategyWindowItemDragStartResult.CandidateCleared =>
                StrategyDragEventResult.HandledOnly,
            _ => HasItemCandidate
                ? StrategyDragEventResult.HandledOnly
                : StrategyDragEventResult.None,
        };
    }

    private bool TryResolvePointerPosition(
        PointerEventData eventData,
        Vector2 screenPosition,
        out int x,
        out int y
    )
    {
        return resolvePointerPosition(eventData, screenPosition, out x, out y);
    }
}
