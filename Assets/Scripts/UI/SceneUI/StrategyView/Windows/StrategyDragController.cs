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

    /// <summary>
    /// Gets whether the item-drag controller consumed the pointer event.
    /// </summary>
    public bool Handled { get; }

    /// <summary>
    /// Gets whether the drag overlay must be rendered immediately.
    /// </summary>
    public bool RenderOverlay { get; }

    /// <summary>
    /// Gets whether the subsequent click must be suppressed.
    /// </summary>
    public bool SuppressClick { get; }

    /// <summary>
    /// Gets whether pressed-window gesture state must be cleared.
    /// </summary>
    public bool ClearPressedWindow { get; }

    /// <summary>
    /// Gets whether the complete strategy presentation must be invalidated.
    /// </summary>
    public bool Dirty { get; }

    /// <summary>
    /// Gets a result for an unhandled pointer event.
    /// </summary>
    public static StrategyDragEventResult None => new StrategyDragEventResult();

    /// <summary>
    /// Gets a result for a consumed event with no immediate presentation work.
    /// </summary>
    public static StrategyDragEventResult HandledOnly =>
        new StrategyDragEventResult(true, false, false, false, false);

    /// <summary>
    /// Gets a result for a continuing source drag whose overlay changed.
    /// </summary>
    public static StrategyDragEventResult SourceDragVisible =>
        new StrategyDragEventResult(true, true, true, false, false);

    /// <summary>
    /// Gets a result for a candidate that transitioned into targeting.
    /// </summary>
    public static StrategyDragEventResult TargetingStarted =>
        new StrategyDragEventResult(true, false, false, true, false);

    /// <summary>
    /// Gets a result for a candidate that transitioned into a source drag.
    /// </summary>
    public static StrategyDragEventResult SourceDragStarted =>
        new StrategyDragEventResult(true, true, true, true, false);

    /// <summary>
    /// Gets a result for a completed or canceled item drag.
    /// </summary>
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

    /// <summary>
    /// Gets whether an item drag may begin after additional pointer movement.
    /// </summary>
    private bool HasItemCandidate => itemDragController.HasCandidate;

    /// <summary>
    /// Gets whether an item candidate or source drag is active.
    /// </summary>
    private bool HasItemState =>
        itemDragController.HasCandidate || itemDragController.SourceDragActive;

    /// <summary>
    /// Begins tracking an item-drag candidate for a registered source window.
    /// </summary>
    /// <param name="window">The source window.</param>
    /// <param name="x">The source-space horizontal press coordinate.</param>
    /// <param name="y">The source-space vertical press coordinate.</param>
    public void StartItemCandidate(UIWindow window, int x, int y)
    {
        itemDragController.StartCandidate(window, x, y);
    }

    /// <summary>
    /// Processes item-drag movement at known strategy source coordinates.
    /// </summary>
    /// <param name="x">The source-space horizontal coordinate.</param>
    /// <param name="y">The source-space vertical coordinate.</param>
    /// <returns>The screen-level effects produced by the transition.</returns>
    public StrategyDragEventResult TryHandleItemPointerMove(int x, int y)
    {
        if (TryMoveItemDrag(x, y))
            return StrategyDragEventResult.SourceDragVisible;

        if (!HasItemCandidate)
            return StrategyDragEventResult.None;

        return TryStartItemDragFromCandidateForPointerMove(x, y);
    }

    /// <summary>
    /// Completes or cancels the active item-drag state for a pointer release.
    /// </summary>
    /// <param name="eventData">The pointer-release event.</param>
    /// <returns>The screen-level effects produced by the transition.</returns>
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
    /// Clears drag state owned by a closing source window.
    /// </summary>
    /// <param name="window">The closing source window.</param>
    public void ClearWindow(UIWindow window)
    {
        itemDragController.ClearWindow(window);
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
