using System;
using System.Collections.Generic;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Resolves a screen-space pointer position into strategy source coordinates.
/// </summary>
/// <param name="eventData">The pointer event supplying the relevant camera.</param>
/// <param name="screenPosition">The screen-space position to resolve.</param>
/// <param name="x">Receives the source-space horizontal coordinate.</param>
/// <param name="y">Receives the source-space vertical coordinate.</param>
/// <returns>True when the position lies inside the strategy surface.</returns>
public delegate bool StrategyPointerPositionResolver(
    PointerEventData eventData,
    Vector2 screenPosition,
    out int x,
    out int y
);

/// <summary>
/// Describes the screen-level effects produced by one item-drag pointer transition.
/// </summary>
public readonly struct StrategyDragEventResult
{
    /// <summary>
    /// Creates one immutable drag transition result.
    /// </summary>
    /// <param name="handled">Whether the item-drag controller consumed the pointer event.</param>
    /// <param name="renderOverlay">Whether the drag overlay must be rendered immediately.</param>
    /// <param name="suppressClick">Whether the subsequent click must be suppressed.</param>
    /// <param name="clearPressedWindow">Whether the pressed-window gesture state must be cleared.</param>
    /// <param name="dirty">Whether the complete strategy presentation must be invalidated.</param>
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

    /// <summary>
    /// Gets a result for an unhandled pointer event.
    /// </summary>
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

/// <summary>
/// Coordinates strategy-window item drag candidates, source drags, and targeting transitions.
/// </summary>
public sealed class StrategyDragController
{
    private readonly StrategyWindowItemDragController itemDragController;
    private readonly StrategyPointerPositionResolver resolvePointerPosition;
    private PointerEventData.InputButton itemPointerButton;
    private int itemPointerId;
    private Vector2 itemPointerPressPosition;
    private GameObject itemPointerPressTarget;
    private bool itemPointerTracked;

    /// <summary>
    /// Creates the strategy item-drag coordinator.
    /// </summary>
    /// <param name="targetingController">Owns the active semantic targeting request.</param>
    /// <param name="getContextItems">Gets the semantic selection for a source window.</param>
    /// <param name="tryGetDragPreview">Builds the drag preview for a source window.</param>
    /// <param name="resolvePointerPosition">Maps pointer positions into strategy coordinates.</param>
    /// <param name="getGalaxyMapDropTarget">Resolves a galaxy-map mission target under a pointer.</param>
    /// <param name="getPlayerFactionId">Returns the player faction identifier.</param>
    /// <param name="commands">Executes semantic move and mission commands.</param>
    /// <param name="itemDragStartDistance">The authored source-space activation distance.</param>
    public StrategyDragController(
        TargetingController targetingController,
        Func<UIWindow, IReadOnlyList<ISceneNode>> getContextItems,
        StrategyWindowDragPreviewResolver tryGetDragPreview,
        StrategyPointerPositionResolver resolvePointerPosition,
        Func<PointerEventData, StrategyMissionTarget> getGalaxyMapDropTarget,
        Func<string> getPlayerFactionId,
        IStrategyWindowCommandActions commands,
        int itemDragStartDistance
    )
    {
        this.resolvePointerPosition =
            resolvePointerPosition
            ?? throw new ArgumentNullException(nameof(resolvePointerPosition));
        itemDragController = new StrategyWindowItemDragController(
            targetingController,
            new DragController(itemDragStartDistance),
            getContextItems,
            tryGetDragPreview,
            getGalaxyMapDropTarget,
            getPlayerFactionId,
            commands
        );
    }

    private bool HasItemCandidate => itemDragController.HasCandidate;

    private bool HasItemState =>
        itemDragController.HasCandidate || itemDragController.SourceDragActive;

    /// <summary>
    /// Begins tracking an item-drag candidate for a registered source window.
    /// </summary>
    /// <param name="window">The source window.</param>
    /// <param name="eventData">The originating pointer press.</param>
    /// <param name="x">The source-space horizontal press coordinate.</param>
    /// <param name="y">The source-space vertical press coordinate.</param>
    public void StartItemCandidate(UIWindow window, PointerEventData eventData, int x, int y)
    {
        if (window == null || !TryTrackItemPointer(eventData))
        {
            ClearItemDrag();
            return;
        }

        itemDragController.StartCandidate(window, x, y);
    }

    /// <summary>
    /// Processes item-drag movement at known strategy source coordinates.
    /// </summary>
    /// <param name="eventData">The active pointer gesture.</param>
    /// <param name="x">The source-space horizontal coordinate.</param>
    /// <param name="y">The source-space vertical coordinate.</param>
    /// <returns>The screen-level effects produced by the transition.</returns>
    public StrategyDragEventResult TryHandleItemPointerMove(
        PointerEventData eventData,
        int x,
        int y
    )
    {
        if (HasItemState && !IsTrackedItemPointer(eventData))
        {
            ClearItemDrag();
            return StrategyDragEventResult.None;
        }

        if (TryMoveItemDrag(x, y))
            return StrategyDragEventResult.SourceDragVisible;

        if (!HasItemCandidate)
            return StrategyDragEventResult.None;

        StrategyDragEventResult result = TryStartItemDragFromCandidateForPointerMove(x, y);
        if (!HasItemState)
            ClearTrackedItemPointer();
        return result;
    }

    /// <summary>
    /// Completes or cancels the active item-drag state for a pointer release.
    /// </summary>
    /// <param name="eventData">The pointer-release event.</param>
    /// <returns>The screen-level effects produced by the transition.</returns>
    public StrategyDragEventResult TryHandleItemPointerUp(PointerEventData eventData)
    {
        bool hadItemState = HasItemState;
        if (hadItemState && !IsTrackedItemPointer(eventData))
        {
            ClearItemDrag();
            return StrategyDragEventResult.None;
        }

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
        {
            ClearTrackedItemPointer();
            return StrategyDragEventResult.ItemDragFinished;
        }

        ClearItemDrag();
        return StrategyDragEventResult.None;
    }

    /// <summary>
    /// Attempts to promote the current candidate into a source drag or targeting request.
    /// </summary>
    /// <param name="x">The source-space horizontal coordinate.</param>
    /// <param name="y">The source-space vertical coordinate.</param>
    /// <returns>The candidate transition result.</returns>
    private StrategyWindowItemDragStartResult TryStartItemDragFromCandidate(int x, int y)
    {
        return itemDragController.TryStartMoveDragFromCandidate(x, y);
    }

    /// <summary>
    /// Moves the active source drag.
    /// </summary>
    /// <param name="x">The source-space horizontal coordinate.</param>
    /// <param name="y">The source-space vertical coordinate.</param>
    /// <returns>True when a source drag was active and moved.</returns>
    private bool TryMoveItemDrag(int x, int y)
    {
        return itemDragController.TryMoveSourceDrag(x, y);
    }

    /// <summary>
    /// Completes the active source drag at a resolved pointer location.
    /// </summary>
    /// <param name="eventData">The pointer-release event.</param>
    /// <param name="x">The source-space horizontal coordinate.</param>
    /// <param name="y">The source-space vertical coordinate.</param>
    /// <returns>True when a source drag handled the release.</returns>
    private bool TryFinishItemDrag(PointerEventData eventData, int x, int y)
    {
        return itemDragController.TryHandleSourceDragPointerUp(eventData, x, y);
    }

    /// <summary>
    /// Clears all candidate and active item-drag state.
    /// </summary>
    public void ClearItemDrag()
    {
        itemDragController.Clear();
        ClearTrackedItemPointer();
    }

    /// <summary>
    /// Tries to get the active item-drag presentation.
    /// </summary>
    /// <param name="texture">Receives the active drag texture.</param>
    /// <param name="bounds">Receives the active drag bounds.</param>
    /// <returns>True when a drawable item-drag preview is active.</returns>
    public bool TryGetOverlay(out Texture texture, out RectInt bounds)
    {
        return itemDragController.TryGetOverlay(out texture, out bounds);
    }

    /// <summary>
    /// Tries to get the complete active item-drag presentation.
    /// </summary>
    /// <param name="preview">Receives the ordered drag-preview presentation.</param>
    /// <param name="pointerX">Receives the current source-space horizontal pointer coordinate.</param>
    /// <param name="pointerY">Receives the current source-space vertical pointer coordinate.</param>
    /// <returns>True when a drawable item-drag preview is active.</returns>
    public bool TryGetOverlay(out DragPreview preview, out int pointerX, out int pointerY)
    {
        return itemDragController.TryGetOverlay(out preview, out pointerX, out pointerY);
    }

    /// <summary>
    /// Clears drag state owned by a closing source window.
    /// </summary>
    /// <param name="window">The closing source window.</param>
    public void ClearWindow(UIWindow window)
    {
        itemDragController.ClearWindow(window);
        if (!HasItemState)
            ClearTrackedItemPointer();
    }

    /// <summary>
    /// Captures the primary pointer press that owns a new item-drag candidate.
    /// </summary>
    /// <param name="eventData">The originating pointer press.</param>
    /// <returns>True when the event identifies a valid primary-button press target.</returns>
    private bool TryTrackItemPointer(PointerEventData eventData)
    {
        GameObject pressTarget = eventData?.pointerPressRaycast.gameObject;
        if (
            eventData == null
            || eventData.button != PointerEventData.InputButton.Left
            || pressTarget == null
        )
            return false;

        itemPointerButton = eventData.button;
        itemPointerId = eventData.pointerId;
        itemPointerPressPosition = eventData.pressPosition;
        itemPointerPressTarget = pressTarget;
        itemPointerTracked = true;
        return true;
    }

    /// <summary>
    /// Reports whether one pointer event belongs to the press that started the candidate.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>True when the pointer identity and press target match.</returns>
    private bool IsTrackedItemPointer(PointerEventData eventData)
    {
        return itemPointerTracked
            && eventData != null
            && eventData.button == itemPointerButton
            && eventData.pointerId == itemPointerId
            && eventData.pressPosition == itemPointerPressPosition
            && ReferenceEquals(eventData.pointerPressRaycast.gameObject, itemPointerPressTarget);
    }

    /// <summary>
    /// Clears the pointer identity associated with item-drag state.
    /// </summary>
    private void ClearTrackedItemPointer()
    {
        itemPointerButton = default;
        itemPointerId = 0;
        itemPointerPressPosition = Vector2.zero;
        itemPointerPressTarget = null;
        itemPointerTracked = false;
    }

    /// <summary>
    /// Converts a candidate transition into screen-level pointer effects.
    /// </summary>
    /// <param name="x">The source-space horizontal coordinate.</param>
    /// <param name="y">The source-space vertical coordinate.</param>
    /// <returns>The screen-level effects produced by the transition.</returns>
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

    /// <summary>
    /// Resolves a screen position into strategy source coordinates.
    /// </summary>
    /// <param name="eventData">The pointer event supplying the relevant camera.</param>
    /// <param name="screenPosition">The screen-space position.</param>
    /// <param name="x">Receives the source-space horizontal coordinate.</param>
    /// <param name="y">Receives the source-space vertical coordinate.</param>
    /// <returns>True when the position lies inside the strategy surface.</returns>
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
