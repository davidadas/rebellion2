using System;
using System.Collections.Generic;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

public enum StrategyWindowItemDragStartResult
{
    None,
    CandidateCleared,
    TargetingStarted,
    SourceDragStarted,
}

public sealed class StrategyWindowItemDragController : ITargetingReceiver
{
    private readonly TargetingController targetingController;
    private readonly DragController dragController;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly IStrategyWindowCommandActions commands;
    private int candidateHotspotX;
    private int candidateHotspotY;
    private List<ISceneNode> candidateItems = new List<ISceneNode>();
    private DragPreview candidatePreview;
    private bool candidateSupportsPreview;
    private bool candidateHasPreview;

    public StrategyWindowItemDragController(
        TargetingController targetingController,
        DragController dragController,
        StrategyWindowLayerView windowLayer,
        IStrategyWindowCommandActions commands
    )
    {
        this.targetingController =
            targetingController ?? throw new ArgumentNullException(nameof(targetingController));
        this.dragController =
            dragController ?? throw new ArgumentNullException(nameof(dragController));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public bool HasCandidate => dragController.HasCandidate;
    public bool SourceDragActive => dragController.IsDragging;

    public void StartCandidate(UIWindow window, int x, int y)
    {
        if (window == null)
            return;

        candidateHotspotX = x;
        candidateHotspotY = y;
        CaptureCandidate(window, x, y);
        dragController.StartCandidate(new DragRequest(window), x, y);
    }

    public StrategyWindowItemDragStartResult TryStartMoveDragFromCandidate(int x, int y)
    {
        if (!dragController.HasCandidateDragStarted(x, y))
            return StrategyWindowItemDragStartResult.None;

        if (dragController.CandidateRequest?.Source is not UIWindow window)
        {
            ClearCandidate();
            return StrategyWindowItemDragStartResult.CandidateCleared;
        }

        if (candidateHasPreview)
        {
            if (TryBeginSourceDrag(window, x, y))
                return StrategyWindowItemDragStartResult.SourceDragStarted;

            ClearCandidate();
            return StrategyWindowItemDragStartResult.CandidateCleared;
        }

        if (candidateItems.Count > 0)
        {
            StartMoveTargeting(window, x, y);
            return StrategyWindowItemDragStartResult.TargetingStarted;
        }

        ClearCandidate();
        return StrategyWindowItemDragStartResult.CandidateCleared;
    }

    public bool TryMoveSourceDrag(int x, int y)
    {
        return dragController.Move(x, y);
    }

    public bool TryHandleSourceDragPointerUp(PointerEventData eventData, int x, int y)
    {
        if (!dragController.End(x, y, out _))
            return false;

        if (targetingController.IsTargeting)
            targetingController.Cancel();

        return true;
    }

    public void ApplyOverlay(StrategyOverlayRenderData data)
    {
        if (data == null)
            return;

        if (
            !dragController.TryGetPreview(
                out Texture texture,
                out int x,
                out int y,
                out int width,
                out int height
            )
        )
            return;

        data.DragImageVisible = true;
        data.DragImageTexture = texture;
        data.DragImageX = x;
        data.DragImageY = y;
        data.DragImageWidth = width;
        data.DragImageHeight = height;
    }

    public void ClearWindow(UIWindow window)
    {
        if (ReferenceEquals(dragController.CandidateRequest?.Source, window))
            ClearCapturedCandidate();

        dragController.ClearSource(window);
    }

    public void ClearCandidate()
    {
        dragController.ClearCandidate();
        candidateHotspotX = 0;
        candidateHotspotY = 0;
        ClearCapturedCandidate();
    }

    public void Clear()
    {
        dragController.Clear();
        candidateHotspotX = 0;
        candidateHotspotY = 0;
        ClearCapturedCandidate();
    }

    public void OnTargetSelected(TargetingRequest request, object target)
    {
        if (
            request?.Source is not StrategyWindowTargetingSource source
            || target is not StrategyMissionTarget missionTarget
        )
            return;

        if (source.Action == StrategyContextMenuActions.Move)
            commands.TryExecuteMove(source.Window, missionTarget, source.Items);
    }

    public void OnTargetingCancelled(TargetingRequest request) { }

    private bool TryBeginSourceDrag(UIWindow window, int x, int y)
    {
        if (!candidateHasPreview || candidatePreview == null)
            return false;

        targetingController.Begin(CreateMoveTargetingRequest(window));
        dragController.BeginDrag(candidatePreview, x, y);
        ClearCapturedCandidate();
        return true;
    }

    private void StartMoveTargeting(UIWindow window, int x, int y)
    {
        targetingController.Begin(CreateMoveTargetingRequest(window), x, y);
        ClearCandidate();
    }

    private TargetingRequest CreateMoveTargetingRequest(UIWindow window)
    {
        return new TargetingRequest(
            StrategyWindowTargetingSource.GetPrompt(StrategyContextMenuActions.Move),
            new StrategyWindowTargetingSource(
                window,
                StrategyContextMenuActions.Move,
                candidateHotspotX,
                candidateHotspotY,
                new List<ISceneNode>(candidateItems)
            ),
            this
        );
    }

    private void CaptureCandidate(UIWindow window, int x, int y)
    {
        ClearCapturedCandidate();
        candidateItems = GetContextItems(window);
        candidateSupportsPreview = TryGetWindowView(window, out IStrategyWindowDragImageView view);
        if (
            candidateSupportsPreview
            && view.TryGetDragPreview(x, y, out DragPreview preview)
            && preview?.Texture != null
            && preview.Width > 0
            && preview.Height > 0
        )
        {
            candidatePreview = preview;
            candidateHasPreview = true;
        }
    }

    private void ClearCapturedCandidate()
    {
        candidateItems = new List<ISceneNode>();
        candidatePreview = null;
        candidateSupportsPreview = false;
        candidateHasPreview = false;
    }

    private bool TryGetWindowView<TView>(UIWindow window, out TView view)
        where TView : class
    {
        return windowLayer.TryGetWindowView(window, out view);
    }

    private List<ISceneNode> GetContextItems(UIWindow window)
    {
        return TryGetWindowView(window, out IStrategyWindowContextItemsView view)
            ? view.GetContextItems()
            : new List<ISceneNode>();
    }
}
