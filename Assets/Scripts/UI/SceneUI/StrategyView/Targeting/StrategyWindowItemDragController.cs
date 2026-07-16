using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Resolves one strategy window's current item-drag preview.
/// </summary>
/// <param name="window">The source strategy window.</param>
/// <param name="sourceX">The pointer's source-space horizontal coordinate.</param>
/// <param name="sourceY">The pointer's source-space vertical coordinate.</param>
/// <param name="preview">Receives the drag preview.</param>
/// <returns>True when the source feature supplied a drawable preview.</returns>
public delegate bool StrategyWindowDragPreviewResolver(
    UIWindow window,
    int sourceX,
    int sourceY,
    out DragPreview preview
);

/// <summary>
/// Identifies the state transition produced while promoting an item-drag candidate.
/// </summary>
public enum StrategyWindowItemDragStartResult
{
    None,
    CandidateCleared,
    TargetingStarted,
    SourceDragStarted,
}

/// <summary>
/// Owns strategy-window item-drag candidates, source previews, and move targeting requests.
/// </summary>
public sealed class StrategyWindowItemDragController : ITargetingReceiver
{
    private readonly TargetingController targetingController;
    private readonly DragController dragController;
    private readonly Func<UIWindow, IReadOnlyList<ISceneNode>> getContextItems;
    private readonly StrategyWindowDragPreviewResolver tryGetDragPreview;
    private readonly Func<PointerEventData, StrategyMissionTarget> getGalaxyMapDropTarget;
    private readonly Func<string> getPlayerFactionId;
    private readonly IStrategyWindowCommandActions commands;
    private int candidateHotspotX;
    private int candidateHotspotY;
    private List<ISceneNode> candidateItems = new List<ISceneNode>();
    private DragPreview candidatePreview;
    private bool candidateHasPreview;

    /// <summary>
    /// Creates the strategy-window item-drag controller.
    /// </summary>
    /// <param name="targetingController">Owns the active semantic targeting request.</param>
    /// <param name="dragController">Owns drag threshold and preview geometry.</param>
    /// <param name="getContextItems">Gets the semantic selection for a source window.</param>
    /// <param name="tryGetDragPreview">Builds the visual preview for a source window.</param>
    /// <param name="getGalaxyMapDropTarget">Resolves a galaxy-map target beneath a pointer.</param>
    /// <param name="getPlayerFactionId">Returns the active player faction identifier.</param>
    /// <param name="commands">Executes move and mission commands.</param>
    public StrategyWindowItemDragController(
        TargetingController targetingController,
        DragController dragController,
        Func<UIWindow, IReadOnlyList<ISceneNode>> getContextItems,
        StrategyWindowDragPreviewResolver tryGetDragPreview,
        Func<PointerEventData, StrategyMissionTarget> getGalaxyMapDropTarget,
        Func<string> getPlayerFactionId,
        IStrategyWindowCommandActions commands
    )
    {
        this.targetingController =
            targetingController ?? throw new ArgumentNullException(nameof(targetingController));
        this.dragController =
            dragController ?? throw new ArgumentNullException(nameof(dragController));
        this.getContextItems =
            getContextItems ?? throw new ArgumentNullException(nameof(getContextItems));
        this.tryGetDragPreview =
            tryGetDragPreview ?? throw new ArgumentNullException(nameof(tryGetDragPreview));
        this.getGalaxyMapDropTarget =
            getGalaxyMapDropTarget
            ?? throw new ArgumentNullException(nameof(getGalaxyMapDropTarget));
        this.getPlayerFactionId =
            getPlayerFactionId ?? throw new ArgumentNullException(nameof(getPlayerFactionId));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    /// <summary>
    /// Gets whether a pointer press is waiting to cross the drag threshold.
    /// </summary>
    public bool HasCandidate => dragController.HasCandidate;

    /// <summary>
    /// Gets whether a source-image drag is active.
    /// </summary>
    public bool SourceDragActive => dragController.IsDragging;

    /// <summary>
    /// Captures one source-window selection as a possible item drag.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <param name="x">The horizontal source-space coordinate.</param>
    /// <param name="y">The vertical source-space coordinate.</param>
    public void StartCandidate(UIWindow window, int x, int y)
    {
        if (window == null)
            return;

        candidateHotspotX = x;
        candidateHotspotY = y;
        CaptureCandidate(window, x, y);
        dragController.StartCandidate(new DragRequest(window), x, y);
    }

    /// <summary>
    /// Promotes a threshold-crossing candidate into a source drag or targeting request.
    /// </summary>
    /// <param name="x">The current horizontal source-space coordinate.</param>
    /// <param name="y">The current vertical source-space coordinate.</param>
    /// <returns>The candidate transition result.</returns>
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

    /// <summary>
    /// Moves the active source-image drag.
    /// </summary>
    /// <param name="x">The current horizontal source-space coordinate.</param>
    /// <param name="y">The current vertical source-space coordinate.</param>
    /// <returns>True when an active source drag moved.</returns>
    public bool TryMoveSourceDrag(int x, int y)
    {
        return dragController.Move(x, y);
    }

    /// <summary>
    /// Completes an active source drag and offers its destination to targeting.
    /// </summary>
    /// <param name="eventData">The pointer-release event.</param>
    /// <param name="x">The final horizontal source-space coordinate.</param>
    /// <param name="y">The final vertical source-space coordinate.</param>
    /// <returns>True when an active source drag consumed the release.</returns>
    public bool TryHandleSourceDragPointerUp(PointerEventData eventData, int x, int y)
    {
        if (!dragController.End(x, y, out _))
            return false;

        if (!TrySelectGalaxyMapDropTarget(eventData) && targetingController.IsTargeting)
            targetingController.Cancel();

        return true;
    }

    /// <summary>
    /// Tries to get the active source-drag presentation.
    /// </summary>
    /// <param name="texture">Receives the active drag texture.</param>
    /// <param name="bounds">Receives the active drag bounds.</param>
    /// <returns>True when a drawable source-drag preview is active.</returns>
    public bool TryGetOverlay(out Texture texture, out RectInt bounds)
    {
        if (
            !dragController.TryGetPreview(
                out texture,
                out int x,
                out int y,
                out int width,
                out int height
            )
        )
        {
            bounds = default;
            return false;
        }

        bounds = new RectInt(x, y, width, height);
        return true;
    }

    /// <summary>
    /// Clears drag state owned by one closing source window.
    /// </summary>
    /// <param name="window">The closing source window.</param>
    public void ClearWindow(UIWindow window)
    {
        if (ReferenceEquals(dragController.CandidateRequest?.Source, window))
            ClearCapturedCandidate();

        dragController.ClearSource(window);
    }

    /// <summary>
    /// Clears only the pending drag candidate and its captured semantic selection.
    /// </summary>
    public void ClearCandidate()
    {
        dragController.ClearCandidate();
        candidateHotspotX = 0;
        candidateHotspotY = 0;
        ClearCapturedCandidate();
    }

    /// <summary>
    /// Clears all candidate, preview, and active source-drag state.
    /// </summary>
    public void Clear()
    {
        dragController.Clear();
        candidateHotspotX = 0;
        candidateHotspotY = 0;
        ClearCapturedCandidate();
    }

    /// <summary>
    /// Executes the command represented by a completed move-targeting request.
    /// </summary>
    /// <param name="request">The completed targeting request.</param>
    /// <param name="target">The selected target.</param>
    public void OnTargetSelected(TargetingRequest request, object target)
    {
        if (
            request?.Source is not StrategyWindowTargetingSource source
            || target is not StrategyMissionTarget missionTarget
        )
            return;

        if (source.Action == StrategyContextMenuActions.Move)
        {
            if (ShouldOpenMissionCreateWindow(source, missionTarget))
                commands.OpenMissionCreateWindow(missionTarget, source.Items);
            else
                commands.TryExecuteMove(source.Window, missionTarget, source.Items);
        }
    }

    /// <summary>
    /// Handles cancellation of a targeting request without additional item-drag state changes.
    /// </summary>
    /// <param name="request">The cancelled targeting request.</param>
    public void OnTargetingCancelled(TargetingRequest request) { }

    /// <summary>
    /// Begins a visual source drag from the captured candidate preview.
    /// </summary>
    /// <param name="window">The source window.</param>
    /// <param name="x">The current horizontal source-space coordinate.</param>
    /// <param name="y">The current vertical source-space coordinate.</param>
    /// <returns>True when a captured preview started dragging.</returns>
    private bool TryBeginSourceDrag(UIWindow window, int x, int y)
    {
        if (!candidateHasPreview || candidatePreview == null)
            return false;

        targetingController.Begin(CreateMoveTargetingRequest(window));
        dragController.BeginDrag(candidatePreview, x, y);
        ClearCapturedCandidate();
        return true;
    }

    /// <summary>
    /// Begins cursor-based move targeting from a candidate without a visual preview.
    /// </summary>
    /// <param name="window">The source window.</param>
    /// <param name="x">The current horizontal source-space coordinate.</param>
    /// <param name="y">The current vertical source-space coordinate.</param>
    private void StartMoveTargeting(UIWindow window, int x, int y)
    {
        targetingController.Begin(CreateMoveTargetingRequest(window), x, y);
        ClearCandidate();
    }

    /// <summary>
    /// Creates the semantic move-targeting request for the captured selection.
    /// </summary>
    /// <param name="window">The source window.</param>
    /// <returns>The complete targeting request.</returns>
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

    /// <summary>
    /// Offers the galaxy-map target beneath one pointer to the active targeting request.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>True when targeting accepted a resolved map target.</returns>
    private bool TrySelectGalaxyMapDropTarget(PointerEventData eventData)
    {
        if (!targetingController.IsTargeting)
            return false;

        StrategyMissionTarget target = getGalaxyMapDropTarget(eventData);
        return target != null && targetingController.TrySelectTarget(target);
    }

    /// <summary>
    /// Determines whether an enemy destination converts this move into mission creation.
    /// </summary>
    /// <param name="source">The source move-targeting state.</param>
    /// <param name="target">The selected destination.</param>
    /// <returns>True when the selected participants can create a mission at the destination.</returns>
    private bool ShouldOpenMissionCreateWindow(
        StrategyWindowTargetingSource source,
        StrategyMissionTarget target
    )
    {
        string playerFactionId = getPlayerFactionId();
        if (
            source == null
            || target?.Planet?.Planet == null
            || string.IsNullOrEmpty(playerFactionId)
            || string.Equals(
                target.Planet.Planet.GetOwnerInstanceID(),
                playerFactionId,
                StringComparison.Ordinal
            )
        )
            return false;

        return StrategyContextMenuAvailability.CanCreateMission(
            source.Items?.ToList(),
            playerFactionId
        );
    }

    /// <summary>
    /// Captures the semantic selection and optional visual preview for one pointer press.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <param name="x">The horizontal source-space coordinate.</param>
    /// <param name="y">The vertical source-space coordinate.</param>
    private void CaptureCandidate(UIWindow window, int x, int y)
    {
        ClearCapturedCandidate();
        IReadOnlyList<ISceneNode> selectedItems = getContextItems(window);
        candidateItems = selectedItems?.ToList() ?? new List<ISceneNode>();
        if (
            tryGetDragPreview(window, x, y, out DragPreview preview)
            && preview?.Texture != null
            && preview.Width > 0
            && preview.Height > 0
        )
        {
            candidatePreview = preview;
            candidateHasPreview = true;
        }
    }

    /// <summary>
    /// Releases the selection and preview captured for a pending candidate.
    /// </summary>
    private void ClearCapturedCandidate()
    {
        candidateItems = new List<ISceneNode>();
        candidatePreview = null;
        candidateHasPreview = false;
    }
}
