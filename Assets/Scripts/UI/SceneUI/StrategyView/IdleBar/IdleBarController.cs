using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.UIState;
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
    /// <returns>The result of open idle bar context menu.</returns>
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
    private const string _entityItemTypeID = "Entity";
    private static readonly string[] _planetItemTypeIDs =
    {
        nameof(ManufacturingType.Ship),
        nameof(ManufacturingType.Troop),
        nameof(ManufacturingType.Building),
    };

    private readonly Func<Faction> getFaction;
    private List<IgnoredItem> ignoredItems;
    private readonly ContextMenuController contextMenuController;
    private readonly Func<UIContext> getUIContext;
    private readonly Func<bool> getVisibility;
    private readonly Func<bool> getAlwaysOpen;
    private readonly IdleBarProjector projector;
    private readonly Func<string, ISceneNode> resolveEntity;

    private IIdleBarActions actions;
    private ContextMenuRequest activeContextMenuRequest;
    private bool disposed;
    private string highlightedEntityId;
    private IdleBarView view;

    /// <summary>
    /// Gets whether the idle bar is currently enabled.
    /// </summary>
    public bool IsIdleBarEnabled => getVisibility();

    /// <summary>
    /// Creates an idle-bar controller backed by current strategy state.
    /// </summary>
    /// <param name="getFaction">Returns the faction represented by the idle bar.</param>
    /// <param name="ignoredItems">The durable idle-bar exclusions to read and update.</param>
    /// <param name="contextMenuController">Owns the shared context-menu lifecycle.</param>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    /// <param name="getVisibility">Returns whether the experimental feature is enabled.</param>
    /// <param name="resolveEntity">Resolves an entity by its stable instance identifier.</param>
    /// <param name="getAlwaysOpen">Returns whether the idle bar remains expanded.</param>
    public IdleBarController(
        Func<Faction> getFaction,
        List<IgnoredItem> ignoredItems,
        ContextMenuController contextMenuController,
        Func<UIContext> getUIContext,
        Func<bool> getVisibility,
        Func<string, ISceneNode> resolveEntity,
        Func<bool> getAlwaysOpen = null
    )
    {
        this.getFaction = getFaction ?? throw new ArgumentNullException(nameof(getFaction));
        this.ignoredItems = ignoredItems ?? throw new ArgumentNullException(nameof(ignoredItems));
        this.contextMenuController =
            contextMenuController ?? throw new ArgumentNullException(nameof(contextMenuController));
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.getVisibility =
            getVisibility ?? throw new ArgumentNullException(nameof(getVisibility));
        this.resolveEntity =
            resolveEntity ?? throw new ArgumentNullException(nameof(resolveEntity));
        this.getAlwaysOpen = getAlwaysOpen ?? (() => false);
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
        view.EntryIgnoreRequested += HandleEntryIgnoreRequested;
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
        IdleBarRenderData projected = projector.Project(getFaction(), desktopBounds);
        List<IdleBarEntry> entries = projected
            .Entries.Where(entry => IsIdleBarTracked(entry.Entity))
            .ToList();
        if (
            !string.IsNullOrEmpty(highlightedEntityId)
            && entries.All(entry => entry.Entity?.InstanceID != highlightedEntityId)
        )
            ClearLocationHighlight();
        requiredView.Render(
            new IdleBarRenderData(true, entries, projected.DesktopBounds, getAlwaysOpen())
        );
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
    /// Clears transient interaction state after the active game changes.
    /// </summary>
    /// <param name="nextIgnoredItems">The replacement persisted idle-bar exclusions.</param>
    public void ResetSession(List<IgnoredItem> nextIgnoredItems)
    {
        ignoredItems =
            nextIgnoredItems ?? throw new ArgumentNullException(nameof(nextIgnoredItems));
        actions.CancelIdleBarItemDrag();
        ClearLocationHighlight();
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

    /// <summary>
    /// Reports whether an entity appears in the idle bar.
    /// </summary>
    /// <param name="entity">The entity whose tracking state is requested.</param>
    /// <returns><see langword="true"/> when the entity is tracked.</returns>
    public bool IsIdleBarTracked(ISceneNode entity)
    {
        if (string.IsNullOrEmpty(entity?.InstanceID))
            return false;

        return GetItemTypeIDs(entity)
            .Any(type => !ContainsIgnoredItem(ignoredItems, entity.InstanceID, type));
    }

    /// <summary>
    /// Changes whether an entity appears in the idle bar.
    /// </summary>
    /// <param name="entity">The entity whose tracking state should change.</param>
    public void ToggleIdleBarTracking(ISceneNode entity)
    {
        if (string.IsNullOrEmpty(entity?.InstanceID))
            return;

        string[] itemTypeIDs = GetItemTypeIDs(entity).ToArray();
        bool untrack = itemTypeIDs.Any(type =>
            !ContainsIgnoredItem(ignoredItems, entity.InstanceID, type)
        );
        foreach (string itemTypeID in itemTypeIDs)
        {
            ignoredItems.RemoveAll(item => IsIgnoredItem(item, entity.InstanceID, itemTypeID));
            if (untrack)
            {
                ignoredItems.Add(
                    new IgnoredItem
                    {
                        TargetInstanceID = entity.InstanceID,
                        ItemTypeID = itemTypeID,
                    }
                );
            }
        }
        if (untrack && highlightedEntityId == entity.InstanceID)
            ClearLocationHighlight();
        actions.RequestIdleBarRender();
    }

    /// <summary>
    /// Gets the independently persisted idle-bar identities represented by an entity.
    /// </summary>
    /// <param name="entity">The entity whose item identities are requested.</param>
    /// <returns>The item identities represented by the entity.</returns>
    private static IEnumerable<string> GetItemTypeIDs(ISceneNode entity)
    {
        return entity is Planet ? _planetItemTypeIDs : new[] { _entityItemTypeID };
    }

    /// <summary>
    /// Reports whether a persisted exclusion matches one idle-bar identity.
    /// </summary>
    /// <param name="items">The persisted exclusions to search.</param>
    /// <param name="entityInstanceId">The entity identifier to match.</param>
    /// <param name="itemTypeID">The item identity to match.</param>
    /// <returns>True when a matching exclusion exists.</returns>
    private static bool ContainsIgnoredItem(
        IEnumerable<IgnoredItem> items,
        string entityInstanceId,
        string itemTypeID
    )
    {
        return items.Any(item => IsIgnoredItem(item, entityInstanceId, itemTypeID));
    }

    /// <summary>
    /// Reports whether one exclusion matches the requested entity and manufacturing lane.
    /// </summary>
    /// <param name="item">The persisted exclusion to inspect.</param>
    /// <param name="entityInstanceId">The entity identifier to match.</param>
    /// <param name="itemTypeID">The item identity to match.</param>
    /// <returns>True when the exclusion represents the requested identity.</returns>
    private static bool IsIgnoredItem(IgnoredItem item, string entityInstanceId, string itemTypeID)
    {
        return item != null
            && string.Equals(item.TargetInstanceID, entityInstanceId, StringComparison.Ordinal)
            && string.Equals(item.ItemTypeID, itemTypeID, StringComparison.Ordinal);
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
    /// Ignores the requested idle-bar entity and refreshes the shelf.
    /// </summary>
    /// <param name="instanceId">The ignored entity identifier.</param>
    private void HandleEntryIgnoreRequested(string instanceId)
    {
        ISceneNode target = string.IsNullOrEmpty(instanceId) ? null : resolveEntity(instanceId);
        if (target != null)
            ToggleIdleBarTracking(target);
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
        view.EntryIgnoreRequested -= HandleEntryIgnoreRequested;
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
