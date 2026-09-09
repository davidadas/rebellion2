using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Defines strategy-screen actions requested by the idle-bar feature.
/// </summary>
public interface IIdleBarActions
{
    /// <summary>
    /// Opens the strategy location represented by an idle-bar entry.
    /// </summary>
    /// <param name="target">The selected strategy entity.</param>
    void OpenIdleBarTarget(ISceneNode target);

    /// <summary>
    /// Opens the normal strategy context menu for an idle-bar entry.
    /// </summary>
    /// <param name="target">The context-clicked strategy entity.</param>
    /// <param name="eventData">The source pointer event.</param>
    ContextMenuRequest OpenIdleBarContextMenu(ISceneNode target, PointerEventData eventData);

    /// <summary>
    /// Requests a strategy render after idle-bar state changes.
    /// </summary>
    void RequestIdleBarRender();

    /// <summary>
    /// Temporarily emphasizes an idle entity's planet on the galaxy map.
    /// </summary>
    /// <param name="target">The hovered entity, or null to restore the current display.</param>
    void SetIdleBarLocationHighlight(ISceneNode target);

    /// <summary>
    /// Begins a direct drag candidate for one idle entity.
    /// </summary>
    /// <param name="target">The pressed idle entity.</param>
    /// <param name="preview">The compact drag preview.</param>
    /// <param name="eventData">The source pointer event.</param>
    /// <returns>True when the candidate was accepted.</returns>
    bool TryStartIdleBarItemDrag(
        ISceneNode target,
        DragPreview preview,
        PointerEventData eventData
    );

    /// <summary>
    /// Advances an accepted direct idle-bar item drag.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    void MoveIdleBarItemDrag(PointerEventData eventData);

    /// <summary>
    /// Completes or clears an accepted direct idle-bar item drag.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    void EndIdleBarItemDrag(PointerEventData eventData);

    /// <summary>
    /// Cancels a direct item drag owned by the idle bar, if one remains active.
    /// </summary>
    void CancelIdleBarItemDrag();
}

/// <summary>
/// Reads and changes whether strategy entities appear in the idle bar.
/// </summary>
public interface IIdleBarTrackingActions
{
    bool IsIdleBarEnabled { get; }

    /// <summary>
    /// Reports whether an entity appears in the idle bar.
    /// </summary>
    /// <param name="entity">The entity whose tracking state is requested.</param>
    /// <returns><see langword="true"/> when the entity is tracked.</returns>
    bool IsIdleBarTracked(ISceneNode entity);

    /// <summary>
    /// Changes whether an entity appears in the idle bar.
    /// </summary>
    /// <param name="entity">The entity whose tracking state should change.</param>
    void ToggleIdleBarTracking(ISceneNode entity);
}

/// <summary>
/// Owns idle-bar projection, tracking state, and semantic action routing.
/// </summary>
public sealed class IdleBarController : IIdleBarTrackingActions, IDisposable
{
    private readonly Func<Faction> getPlayerFaction;
    private readonly ContextMenuController contextMenuController;
    private readonly Func<UIContext> getUIContext;
    private readonly Func<bool> getVisibility;
    private readonly HashSet<string> ignoredEntityIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly IdleBarProjector projector;
    private readonly Func<string, ISceneNode> resolveEntity;

    private IIdleBarActions actions;
    private ContextMenuRequest activeContextMenuRequest;
    private bool disposed;
    private string highlightedEntityId;
    private IdleBarView view;

    /// <inheritdoc />
    public bool IsIdleBarEnabled => getVisibility();

    /// <summary>
    /// Creates an idle-bar controller backed by current strategy state.
    /// </summary>
    /// <param name="getPlayerFaction">Returns the current player faction.</param>
    /// <param name="contextMenuController">Owns the shared context-menu lifecycle.</param>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    /// <param name="getVisibility">Returns whether the experimental feature is enabled.</param>
    /// <param name="resolveEntity">Resolves an entity by its stable instance identifier.</param>
    public IdleBarController(
        Func<Faction> getPlayerFaction,
        ContextMenuController contextMenuController,
        Func<UIContext> getUIContext,
        Func<bool> getVisibility,
        Func<string, ISceneNode> resolveEntity
    )
    {
        this.getPlayerFaction =
            getPlayerFaction ?? throw new ArgumentNullException(nameof(getPlayerFaction));
        this.contextMenuController =
            contextMenuController ?? throw new ArgumentNullException(nameof(contextMenuController));
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.getVisibility =
            getVisibility ?? throw new ArgumentNullException(nameof(getVisibility));
        this.resolveEntity =
            resolveEntity ?? throw new ArgumentNullException(nameof(resolveEntity));
        projector = new IdleBarProjector(getUIContext);
        contextMenuController.RequestClosed += HandleContextMenuClosed;
    }

    /// <summary>
    /// Connects the controller to strategy-screen actions.
    /// </summary>
    /// <param name="nextActions">The strategy-screen action boundary.</param>
    public void Initialize(IIdleBarActions nextActions)
    {
        actions = nextActions ?? throw new ArgumentNullException(nameof(nextActions));
    }

    /// <summary>
    /// Subscribes the controller to an authored idle-bar view exactly once.
    /// </summary>
    /// <param name="nextView">The authored idle-bar view.</param>
    public void BindView(IdleBarView nextView)
    {
        if (nextView == null)
            throw new ArgumentNullException(nameof(nextView));

        EnsureInitialized();
        if (ReferenceEquals(view, nextView))
            return;

        ReleaseView();
        view = nextView;
        view.SetContextMenuOpen(activeContextMenuRequest != null);
        view.Destroyed += HandleViewDestroyed;
        view.EntryHoverCleared += HandleEntryHoverCleared;
        view.EntryHovered += HandleEntryHovered;
        view.EntryDragCandidateRequested += HandleEntryDragCandidateRequested;
        view.EntryDragEnded += HandleEntryDragEnded;
        view.EntryDragMoved += HandleEntryDragMoved;
        view.EntryContextRequested += HandleEntryContextRequested;
        view.EntrySelected += HandleEntrySelected;
    }

    /// <summary>
    /// Projects and renders the current idle-bar presentation.
    /// </summary>
    public void Render()
    {
        IdleBarView requiredView = GetRequiredView();
        if (!IsIdleBarEnabled)
        {
            actions.CancelIdleBarItemDrag();
            ClearLocationHighlight();
            requiredView.Render(new IdleBarRenderData(false, null, new RectInt()));
            return;
        }

        RectInt desktopBounds = GetDesktopBounds();
        IdleBarRenderData projected = projector.Project(getPlayerFaction(), desktopBounds);
        List<IdleBarEntry> entries = projected
            .Entries.Where(entry => !ignoredEntityIds.Contains(entry.Entity?.InstanceID))
            .ToList();
        if (
            !string.IsNullOrEmpty(highlightedEntityId)
            && entries.All(entry => entry.Entity?.InstanceID != highlightedEntityId)
        )
            ClearLocationHighlight();
        requiredView.Render(new IdleBarRenderData(true, entries, projected.DesktopBounds));
    }

    /// <summary>
    /// Resolves the authored strategy desktop bounds used to place the idle bar.
    /// </summary>
    /// <returns>The source-space desktop bounds.</returns>
    private RectInt GetDesktopBounds()
    {
        SourceRectLayout bounds = getUIContext()
            ?.GetPlayerFactionTheme()
            ?.StrategyWindowPlacements?.WindowBounds;
        if (bounds == null)
        {
            throw new MissingReferenceException(
                "StrategyWindowPlacements/WindowBounds is missing."
            );
        }

        return new RectInt(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Clears per-game tracking choices after the active game changes.
    /// </summary>
    public void ResetSession()
    {
        actions.CancelIdleBarItemDrag();
        ClearLocationHighlight();
        ignoredEntityIds.Clear();
    }

    /// <summary>
    /// Releases context-menu and authored-view subscriptions owned by this controller.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        contextMenuController.RequestClosed -= HandleContextMenuClosed;
        activeContextMenuRequest = null;
        ReleaseView();
        actions = null;
    }

    /// <inheritdoc />
    public bool IsIdleBarTracked(ISceneNode entity)
    {
        return !string.IsNullOrEmpty(entity?.InstanceID)
            && !ignoredEntityIds.Contains(entity.InstanceID);
    }

    /// <inheritdoc />
    public void ToggleIdleBarTracking(ISceneNode entity)
    {
        if (string.IsNullOrEmpty(entity?.InstanceID))
            return;

        if (!ignoredEntityIds.Remove(entity.InstanceID))
        {
            ignoredEntityIds.Add(entity.InstanceID);
            if (highlightedEntityId == entity.InstanceID)
                ClearLocationHighlight();
        }
        actions.RequestIdleBarRender();
    }

    /// <summary>
    /// Resolves and opens the selected idle-bar entity.
    /// </summary>
    /// <param name="instanceId">The selected entity identifier.</param>
    private void HandleEntrySelected(string instanceId)
    {
        ISceneNode target = string.IsNullOrEmpty(instanceId) ? null : resolveEntity(instanceId);
        if (target != null)
            actions.OpenIdleBarTarget(target);
    }

    /// <summary>
    /// Opens the existing strategy context menu for the secondary-clicked entity.
    /// </summary>
    /// <param name="instanceId">The context-clicked entity identifier.</param>
    /// <param name="eventData">The source pointer event.</param>
    private void HandleEntryContextRequested(string instanceId, PointerEventData eventData)
    {
        ISceneNode target = string.IsNullOrEmpty(instanceId) ? null : resolveEntity(instanceId);
        if (target == null)
            return;

        activeContextMenuRequest = actions.OpenIdleBarContextMenu(target, eventData);
        view?.SetContextMenuOpen(activeContextMenuRequest != null);
    }

    /// <summary>
    /// Starts a shared strategy drag candidate for a movable idle-bar entity.
    /// </summary>
    /// <param name="instanceId">The pressed entity identifier.</param>
    /// <param name="preview">The compact entity drag preview.</param>
    /// <param name="eventData">The source pointer event.</param>
    private void HandleEntryDragCandidateRequested(
        string instanceId,
        DragPreview preview,
        PointerEventData eventData
    )
    {
        actions.CancelIdleBarItemDrag();
        ISceneNode target = string.IsNullOrEmpty(instanceId) ? null : resolveEntity(instanceId);
        if (target is Officer or SpecialForces)
            actions.TryStartIdleBarItemDrag(target, preview, eventData);
    }

    /// <summary>
    /// Advances the shared strategy drag owned by an idle-bar entity.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    private void HandleEntryDragMoved(PointerEventData eventData)
    {
        actions.MoveIdleBarItemDrag(eventData);
    }

    /// <summary>
    /// Completes or clears the shared strategy drag owned by an idle-bar entity.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    private void HandleEntryDragEnded(PointerEventData eventData)
    {
        actions.EndIdleBarItemDrag(eventData);
    }

    /// <summary>
    /// Releases the expanded shelf when its own context-menu request closes.
    /// </summary>
    /// <param name="request">The context-menu request that closed.</param>
    private void HandleContextMenuClosed(ContextMenuRequest request)
    {
        if (!ReferenceEquals(activeContextMenuRequest, request))
            return;

        activeContextMenuRequest = null;
        view?.SetContextMenuOpen(false);
    }

    /// <summary>
    /// Highlights the location represented by the hovered idle-bar entity.
    /// </summary>
    /// <param name="instanceId">The hovered entity identifier.</param>
    private void HandleEntryHovered(string instanceId)
    {
        ISceneNode target = string.IsNullOrEmpty(instanceId) ? null : resolveEntity(instanceId);
        if (target == null)
            return;

        highlightedEntityId = instanceId;
        actions.SetIdleBarLocationHighlight(target);
    }

    /// <summary>
    /// Restores the active galactic-information display when the current hover ends.
    /// </summary>
    /// <param name="instanceId">The entity identifier whose hover ended.</param>
    private void HandleEntryHoverCleared(string instanceId)
    {
        if (instanceId == highlightedEntityId)
            ClearLocationHighlight();
    }

    /// <summary>
    /// Clears any transient idle-bar location highlight.
    /// </summary>
    private void ClearLocationHighlight()
    {
        if (string.IsNullOrEmpty(highlightedEntityId))
            return;

        highlightedEntityId = null;
        actions?.SetIdleBarLocationHighlight(null);
    }

    /// <summary>
    /// Releases subscriptions when the bound authored view is destroyed.
    /// </summary>
    /// <param name="destroyedView">The destroyed idle-bar view.</param>
    private void HandleViewDestroyed(IdleBarView destroyedView)
    {
        if (ReferenceEquals(view, destroyedView))
            ReleaseView();
    }

    /// <summary>
    /// Releases subscriptions from the currently bound authored view.
    /// </summary>
    private void ReleaseView()
    {
        if (ReferenceEquals(view, null))
            return;

        view.Destroyed -= HandleViewDestroyed;
        view.EntryHoverCleared -= HandleEntryHoverCleared;
        view.EntryHovered -= HandleEntryHovered;
        view.EntryDragCandidateRequested -= HandleEntryDragCandidateRequested;
        view.EntryDragEnded -= HandleEntryDragEnded;
        view.EntryDragMoved -= HandleEntryDragMoved;
        view.EntryContextRequested -= HandleEntryContextRequested;
        view.EntrySelected -= HandleEntrySelected;
        view.SetContextMenuOpen(false);
        actions?.CancelIdleBarItemDrag();
        ClearLocationHighlight();
        view = null;
    }

    /// <summary>
    /// Verifies action routing is available before view binding or interaction.
    /// </summary>
    private void EnsureInitialized()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(IdleBarController));

        if (actions == null)
        {
            throw new InvalidOperationException(
                $"{nameof(IdleBarController)} must be initialized before binding a view."
            );
        }
    }

    /// <summary>
    /// Gets the bound authored view and rejects incomplete screen composition.
    /// </summary>
    /// <returns>The bound authored idle-bar view.</returns>
    private IdleBarView GetRequiredView()
    {
        EnsureInitialized();
        return view
            ?? throw new InvalidOperationException(
                $"{nameof(IdleBarController)} must bind a view before rendering."
            );
    }
}
