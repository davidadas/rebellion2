using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Performs game-level and shared-window actions requested by the Defense feature.
/// </summary>
public interface IDefenseWindowActions
{
    /// <summary>
    /// Opens status information for one Defense-window target.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    void OpenDefenseStatusWindow(StrategyStatusTarget target);

    /// <summary>
    /// Opens Encyclopedia information for one Defense-window target.
    /// </summary>
    /// <param name="target">The selected information target.</param>
    void OpenDefenseInfoWindow(StrategyStatusTarget target);

    /// <summary>
    /// Opens scrap confirmation for selected Defense items.
    /// </summary>
    /// <param name="sourceWindow">The requesting Defense window.</param>
    /// <param name="items">The selected Defense items.</param>
    void OpenDefenseScrapConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);

    /// <summary>
    /// Opens stop-construction confirmation for selected queued items.
    /// </summary>
    /// <param name="sourceWindow">The requesting Defense window.</param>
    /// <param name="items">The queued items selected for cancellation.</param>
    void OpenDefenseStopConstructionConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    );

    /// <summary>
    /// Opens retirement confirmation for selected Defense personnel.
    /// </summary>
    /// <param name="sourceWindow">The requesting Defense window.</param>
    /// <param name="items">The selected Defense personnel.</param>
    void OpenDefenseRetireConfirmWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);
}

/// <summary>
/// Owns Defense-window sessions, selection, targeting, command routing, and presentation requests.
/// </summary>
public sealed class DefenseWindowController
    : IStrategyContextMenuProvider,
        IContextMenuReceiver,
        ITargetingReceiver
{
    private readonly HashSet<DefenseWindowView> boundViews = new HashSet<DefenseWindowView>();
    private readonly Func<UIContext> getUIContext;
    private readonly Func<int, int, Vector2Int> getWindowPosition;
    private readonly Action markDirty;
    private readonly DefenseWindowProjector projector;
    private readonly Dictionary<DefenseWindowView, DefenseWindowSession> sessions =
        new Dictionary<DefenseWindowView, DefenseWindowSession>();
    private readonly TargetingController targetingController;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private Action<PointerEventData> endItemDrag;
    private IDefenseWindowActions actions;
    private IStrategyWindowCommandActions commandActions;
    private Action<PointerEventData> moveItemDrag;
    private Action<UIWindow, int, int> startItemDrag;

    /// <summary>
    /// Creates the Defense feature controller with its presentation and targeting dependencies.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="targetingController">Owns the active strategy targeting request.</param>
    /// <param name="windowLayer">Provides the authored Defense prefab and normal window layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getWindowPosition">Clamps a requested Defense-window placement.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public DefenseWindowController(
        Func<UIContext> getUIContext,
        TargetingController targetingController,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<int, int, Vector2Int> getWindowPosition,
        Action markDirty
    )
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        projector = new DefenseWindowProjector(getUIContext);
        this.targetingController =
            targetingController ?? throw new ArgumentNullException(nameof(targetingController));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Supplies Defense command and interaction actions after the strategy controller graph is constructed.
    /// </summary>
    /// <param name="windowActions">The feature-specific Defense actions.</param>
    /// <param name="windowCommandActions">The shared mission and movement actions.</param>
    /// <param name="beginItemDrag">Begins a strategy item-drag candidate.</param>
    /// <param name="continueItemDrag">Advances the active strategy item drag.</param>
    /// <param name="completeItemDrag">Completes the active strategy item drag.</param>
    public void Initialize(
        IDefenseWindowActions windowActions,
        IStrategyWindowCommandActions windowCommandActions,
        Action<UIWindow, int, int> beginItemDrag,
        Action<PointerEventData> continueItemDrag,
        Action<PointerEventData> completeItemDrag
    )
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
        commandActions =
            windowCommandActions ?? throw new ArgumentNullException(nameof(windowCommandActions));
        startItemDrag = beginItemDrag ?? throw new ArgumentNullException(nameof(beginItemDrag));
        moveItemDrag =
            continueItemDrag ?? throw new ArgumentNullException(nameof(continueItemDrag));
        endItemDrag = completeItemDrag ?? throw new ArgumentNullException(nameof(completeItemDrag));
    }

    /// <summary>
    /// Starts a Defense-window session for one authored view and planet projection.
    /// </summary>
    /// <param name="view">The destination Defense view.</param>
    /// <param name="planet">The represented strategy planet.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeWindow(DefenseWindowView view, GalaxyMapPlanet planet)
    {
        if (view == null || planet?.Planet == null)
            return false;

        BindWindow(view);
        sessions[view] = new DefenseWindowSession(planet, view.WindowShell);
        return true;
    }

    /// <summary>
    /// Opens or focuses the Defense window for a planet.
    /// </summary>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="sourceX">The requested source-space horizontal position.</param>
    /// <param name="sourceY">The requested source-space vertical position.</param>
    /// <param name="created">Receives whether a new window was created.</param>
    /// <returns>The opened or focused window shell.</returns>
    public UIWindow Open(GalaxyMapPlanet planet, int sourceX, int sourceY, out bool created)
    {
        created = false;
        if (planet?.Planet == null)
            return null;

        DefenseWindowView existing = FindWindow(planet.Planet.InstanceID);
        if (existing != null)
        {
            UIWindow existingWindow = existing.WindowShell;
            windowManager.Focus(existingWindow);
            return existingWindow;
        }

        Vector2Int position = getWindowPosition(sourceX, sourceY);
        UIWindow window = windowManager.CreateWindow(
            windowLayer.DefenseWindowPrefab,
            windowLayer.GetWindowParent(false),
            $"DefenseWindow-{planet.Planet.GetDisplayName()}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.DefenseWindowPrefab),
            false,
            true,
            true,
            false,
            out DefenseWindowView view
        );
        if (!TryInitializeWindow(view, planet))
        {
            windowManager.DestroyWindow(window);
            return null;
        }

        created = true;
        markDirty();
        return window;
    }

    /// <summary>
    /// Renders every registered Defense window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out DefenseWindowView view))
                RenderWindow(view, window, window.ActiveWindow);
        }
    }

    /// <summary>
    /// Rebinds Defense sessions to a refreshed galaxy snapshot.
    /// </summary>
    /// <param name="sectors">The refreshed visible sectors.</param>
    public void ReconcileWindows(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            return;

        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                !windowManager.TryGetWindowView(window, out DefenseWindowView view)
                || !sessions.TryGetValue(view, out DefenseWindowSession session)
            )
                continue;

            GalaxyMapPlanet planet = FindFreshPlanet(session.Planet, sectors);
            if (planet != null)
                session.RebindPlanet(planet);
        }
    }

    /// <summary>
    /// Subscribes to one authored Defense view exactly once.
    /// </summary>
    /// <param name="view">The Defense view to bind.</param>
    public void BindWindow(DefenseWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        EnsureInitialized();
        if (!boundViews.Add(view))
            return;

        view.Destroyed += HandleViewDestroyed;
        view.ItemDoubleClicked += HandleItemDoubleClicked;
        view.ItemDropped += HandleItemDropped;
        view.ItemPressed += HandleItemPressed;
        view.ItemReleased += HandleItemReleased;
        view.ScrollDragged += HandleScrollDragged;
        view.ScrollDragEnded += HandleScrollDragEnded;
        view.SurfaceClicked += HandleSurfaceClicked;
        view.TabRequested += HandleTabRequested;
    }

    /// <summary>
    /// Projects current domain state and renders one Defense window.
    /// </summary>
    /// <param name="view">The destination Defense view.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    public void RenderWindow(DefenseWindowView view, UIWindow window, bool active)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        DefenseWindowSession session = GetSession(view);
        view.Render(projector.Build(session, window, active));
    }

    /// <summary>
    /// Gets the planet represented by one Defense view.
    /// </summary>
    /// <param name="view">The Defense view.</param>
    /// <returns>The represented planet, or null.</returns>
    public GalaxyMapPlanet GetPlanet(DefenseWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out DefenseWindowSession session)
            ? session.Planet
            : null;
    }

    /// <summary>
    /// Replaces one Defense session's stale planet projection while preserving local UI state.
    /// </summary>
    /// <param name="view">The Defense view.</param>
    /// <param name="planet">The fresh strategy planet projection.</param>
    public void ReconcileWindow(DefenseWindowView view, GalaxyMapPlanet planet)
    {
        if (view == null || planet?.Planet == null)
            return;
        if (sessions.TryGetValue(view, out DefenseWindowSession session))
            session.RebindPlanet(planet);
    }

    /// <summary>
    /// Selects the Defense tab requested by Finder navigation.
    /// </summary>
    /// <param name="view">The destination Defense view.</param>
    /// <param name="tab">The requested Defense tab.</param>
    public void SelectFinderTab(DefenseWindowView view, DefenseWindowTab tab)
    {
        if (
            view == null
            || !DefenseWindowRenderData.OrderedTabs.Contains(tab)
            || !sessions.TryGetValue(view, out DefenseWindowSession session)
        )
            return;

        session.SelectTab(tab);
    }

    /// <summary>
    /// Gets the active tab owned by one Defense session.
    /// </summary>
    /// <param name="view">The Defense view.</param>
    /// <returns>The selected tab, or the personnel tab when no session exists.</returns>
    internal DefenseWindowTab GetActiveTab(DefenseWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out DefenseWindowSession session)
            ? session.ActiveTab
            : DefenseWindowTab.Personnel;
    }

    /// <summary>
    /// Gets the selected visual indices owned by one Defense session.
    /// </summary>
    /// <param name="view">The Defense view.</param>
    /// <returns>The selected indices, or an empty collection when no session exists.</returns>
    internal IReadOnlyCollection<int> GetSelectedItems(DefenseWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out DefenseWindowSession session)
            ? session.SelectedItemIndexes
            : Array.Empty<int>();
    }

    /// <summary>
    /// Selects the Defense tab and card containing one scene node.
    /// </summary>
    /// <param name="view">The destination Defense view.</param>
    /// <param name="target">The scene node to select.</param>
    /// <returns>True when the target belongs to this Defense session.</returns>
    public bool SelectTarget(DefenseWindowView view, ISceneNode target)
    {
        if (
            view == null
            || target == null
            || !sessions.TryGetValue(view, out DefenseWindowSession session)
        )
            return false;

        foreach (DefenseWindowTab tab in DefenseWindowRenderData.OrderedTabs)
        {
            IReadOnlyList<ISceneNode> tabItems = session.GetItems(tab);
            int itemIndex = FindItemIndex(tabItems, target);
            if (itemIndex < 0)
                continue;

            session.SelectSingleItem(tab, itemIndex);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the scene nodes targeted by the current context or drag selection.
    /// </summary>
    /// <param name="window">The source Defense window.</param>
    /// <returns>The selected scene nodes in visual order.</returns>
    public List<ISceneNode> GetContextItems(UIWindow window)
    {
        return TryGetSession(window, out _, out DefenseWindowSession session)
            ? GetContextItems(session, out _)
            : new List<ISceneNode>();
    }

    /// <summary>
    /// Resolves the current status target for one Defense window.
    /// </summary>
    /// <param name="window">The source Defense window.</param>
    /// <returns>The selected status target, or null.</returns>
    public StrategyStatusTarget GetStatusTarget(UIWindow window)
    {
        return TryGetSession(window, out _, out DefenseWindowSession session)
            ? GetStatusTarget(session)
            : null;
    }

    /// <summary>
    /// Clears selection and transient pointer state owned by one Defense window.
    /// </summary>
    /// <param name="window">The source Defense window.</param>
    public void ClearSelection(UIWindow window)
    {
        if (!TryGetSession(window, out _, out DefenseWindowSession session))
            return;

        session.ClearSelection();
    }

    /// <summary>
    /// Creates the current Defense drag preview from the controller-selected card.
    /// </summary>
    /// <param name="window">The source Defense window.</param>
    /// <param name="sourceX">The pointer's source-space horizontal coordinate.</param>
    /// <param name="sourceY">The pointer's source-space vertical coordinate.</param>
    /// <param name="preview">Receives the drag preview.</param>
    /// <returns>True when a drawable preview source is available.</returns>
    public bool TryGetDragPreview(
        UIWindow window,
        int sourceX,
        int sourceY,
        out DragPreview preview
    )
    {
        preview = null;
        return TryGetSession(window, out DefenseWindowView view, out DefenseWindowSession session)
            && view.TryCreateDragPreview(session.DragItemIndex, sourceX, sourceY, out preview);
    }

    /// <summary>
    /// Builds the Defense context menu for one shared provider request.
    /// </summary>
    /// <param name="context">The active context-menu invocation.</param>
    /// <param name="request">Receives the completed command request.</param>
    /// <param name="width">Receives the authored menu width.</param>
    /// <returns>True when the request belongs to a Defense window.</returns>
    public bool TryCreateContextMenu(
        StrategyContextMenuProviderContext context,
        out ContextMenuRequest request,
        out int width
    )
    {
        request = null;
        width = 0;
        if (
            context?.Window == null
            || !windowManager.TryGetWindowView(context.Window, out DefenseWindowView view)
            || !sessions.TryGetValue(view, out DefenseWindowSession session)
        )
            return false;

        CaptureContextTarget(view, session, context.EventData);
        List<ISceneNode> items = GetContextItems(session, out ISceneNode hitItem);
        string playerFactionId = getUIContext()?.GetPlayerFactionInstanceID();
        List<StrategyMenuCommand> commands = DefenseWindowContextMenuBuilder.Build(
            items,
            hitItem,
            StrategyContextMenuAvailability.CanMoveItems(items, playerFactionId),
            StrategyContextMenuAvailability.PlayerControlsItems(items, playerFactionId),
            StrategyContextMenuAvailability.CanCreateMission(items, playerFactionId),
            StrategyContextMenuAvailability.CanRetireFleet(items, playerFactionId)
        );
        if (commands.Count == 0)
            return false;

        DefenseContextMenuSource source = new DefenseContextMenuSource(
            context.Window,
            context.X,
            context.Y,
            items,
            GetStatusTarget(session)
        );
        request = new ContextMenuRequest(
            source,
            commands.Cast<IContextMenuCommand>().ToList(),
            this
        );
        width = context.Layout.DefenseMenuWidth;
        return true;
    }

    /// <summary>
    /// Routes one selected Defense context-menu command.
    /// </summary>
    /// <param name="request">The completed context-menu request.</param>
    /// <param name="command">The selected context-menu command.</param>
    public void OnContextMenuCommandSelected(
        ContextMenuRequest request,
        IContextMenuCommand command
    )
    {
        if (
            request?.Source is not DefenseContextMenuSource source
            || command is not StrategyMenuCommand strategyCommand
        )
            return;

        switch (strategyCommand.Action)
        {
            case StrategyContextMenuActions.Encyclopedia:
                actions.OpenDefenseInfoWindow(source.Target);
                break;
            case StrategyContextMenuActions.Status:
                actions.OpenDefenseStatusWindow(source.Target);
                break;
            case StrategyContextMenuActions.Scrap:
                actions.OpenDefenseScrapConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.Stop:
                actions.OpenDefenseStopConstructionConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.Retire:
                actions.OpenDefenseRetireConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.CreateMission:
            case StrategyContextMenuActions.Move:
            case StrategyContextMenuActions.MoveConfirm:
                BeginContextTargeting(source, strategyCommand.Action);
                break;
        }
    }

    /// <summary>
    /// Handles context-menu cancellation without changing Defense state.
    /// </summary>
    /// <param name="request">The canceled context-menu request.</param>
    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    /// <summary>
    /// Routes a completed Defense targeting request to its game-level action.
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

        switch (source.Action)
        {
            case StrategyContextMenuActions.CreateMission:
                commandActions.OpenMissionCreateWindow(missionTarget, source.Items);
                break;
            case StrategyContextMenuActions.Move:
                commandActions.TryExecuteMove(source.Window, missionTarget, source.Items);
                break;
            case StrategyContextMenuActions.MoveConfirm:
                commandActions.OpenMoveConfirmWindow(source.Window, missionTarget, source.Items);
                break;
        }
    }

    /// <summary>
    /// Handles cancellation of a Defense targeting request.
    /// </summary>
    /// <param name="request">The canceled targeting request.</param>
    public void OnTargetingCancelled(TargetingRequest request) { }

    /// <summary>
    /// Captures the unit card under a context-menu pointer.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="session">The controller-owned Defense session.</param>
    /// <param name="eventData">The context-menu pointer event.</param>
    private static void CaptureContextTarget(
        DefenseWindowView view,
        DefenseWindowSession session,
        PointerEventData eventData
    )
    {
        if (view.TryGetItemIndex(eventData, out int itemIndex))
        {
            session.CaptureContextItem(itemIndex);
            return;
        }

        session.ClearContextItem();
    }

    /// <summary>
    /// Resolves current context indices and selections to scene nodes in visual order.
    /// </summary>
    /// <param name="session">The controller-owned Defense session.</param>
    /// <param name="hitItem">Receives the item directly under the context pointer.</param>
    /// <returns>The selected scene nodes.</returns>
    private static List<ISceneNode> GetContextItems(
        DefenseWindowSession session,
        out ISceneNode hitItem
    )
    {
        hitItem = null;
        int itemIndex = session.ContextItemIndex;
        if (!session.TryGetItem(itemIndex, out hitItem))
            return new List<ISceneNode>();

        if (session.SelectedItemIndexes.Contains(itemIndex))
        {
            List<ISceneNode> selectedItems = session.GetSelectedItems();
            if (selectedItems.Count > 0)
                return selectedItems;
        }

        return new List<ISceneNode> { hitItem };
    }

    /// <summary>
    /// Resolves the current singular selection or direct context item as a status target.
    /// </summary>
    /// <param name="session">The controller-owned Defense session.</param>
    /// <returns>The current status target, or null.</returns>
    private static StrategyStatusTarget GetStatusTarget(DefenseWindowSession session)
    {
        List<ISceneNode> selectedItems = session.GetSelectedItems();
        if (selectedItems.Count == 1)
        {
            return new StrategyStatusTarget(session.Planet, selectedItems[0]);
        }

        session.TryGetItem(session.ContextItemIndex, out ISceneNode item);
        return item == null ? null : new StrategyStatusTarget(session.Planet, item);
    }

    /// <summary>
    /// Starts targeting with the current context-selected Defense items.
    /// </summary>
    /// <param name="source">The immutable Defense context selection.</param>
    /// <param name="action">The selected context-menu action.</param>
    private void BeginContextTargeting(DefenseContextMenuSource source, int action)
    {
        if (source?.Window == null)
            return;

        targetingController.Begin(
            new TargetingRequest(
                StrategyWindowTargetingSource.GetPrompt(action),
                new StrategyWindowTargetingSource(
                    source.Window,
                    action,
                    source.HotspotX,
                    source.HotspotY,
                    source.Items
                ),
                this
            ),
            source.HotspotX,
            source.HotspotY
        );
    }

    /// <summary>
    /// Handles a unit-card press and starts selection, context, or drag flow.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="itemIndex">The pressed unit-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleItemPressed(
        DefenseWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (!TryGetSession(view, out DefenseWindowSession session) || eventData == null)
            return;

        if (!session.TryGetItem(itemIndex, out _))
            return;
        if (eventData.button == PointerEventData.InputButton.Left)
            session.Window.RequestFocus();
        if (
            targetingController.IsTargeting
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        session.PrepareItemSelection(itemIndex);
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            session.SelectContextItem(itemIndex);
            markDirty();
            session.Window.RequestContext(eventData);
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        session.SelectItemForDrag(itemIndex, view.ItemColumnCount);
        if (
            session.CanDragSelectedItems()
            && view.ItemContainsDragSource(itemIndex, eventData)
            && view.TryGetDesktopPosition(eventData, out int x, out int y)
        )
        {
            session.BeginDrag(itemIndex);
            startItemDrag(session.Window, x, y);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Handles a unit-card release and resolves targeting or final selection.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="itemIndex">The released unit-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleItemReleased(
        DefenseWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (
            eventData?.button != PointerEventData.InputButton.Left
            || !TryGetSession(view, out DefenseWindowSession session)
        )
            return;

        if (!session.TryGetItem(itemIndex, out ISceneNode item))
            return;
        if (TrySelectTarget(session, item))
            return;
        if (SelectableListSelection.HasSelectionModifier())
            return;

        session.PrepareItemSelection(itemIndex);
        session.SelectItem(itemIndex, view.ItemColumnCount);
        markDirty();
    }

    /// <summary>
    /// Handles a drop over one unit card as a targeting selection.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="itemIndex">The drop-target unit-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleItemDropped(
        DefenseWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (!TryGetSession(view, out DefenseWindowSession session))
            return;

        if (session.TryGetItem(itemIndex, out ISceneNode item))
            TrySelectTarget(session, item);
    }

    /// <summary>
    /// Handles a unit-card double click as a status request.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="itemIndex">The double-clicked unit-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleItemDoubleClicked(
        DefenseWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (
            eventData?.button != PointerEventData.InputButton.Left
            || targetingController.IsTargeting
            || !TryGetSession(view, out DefenseWindowSession session)
        )
            return;

        StrategyStatusTarget target = GetStatusTarget(session);
        if (target != null)
            actions.OpenDefenseStatusWindow(target);
    }

    /// <summary>
    /// Handles a Defense-window surface click as targeting or selection clearing.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleSurfaceClicked(DefenseWindowView view, PointerEventData eventData)
    {
        if (!TryGetSession(view, out DefenseWindowSession session))
            return;
        if (TrySelectTarget(session, null) || view.IsSelectionItemClick(eventData))
            return;

        session.ClearSelection();
        markDirty();
    }

    /// <summary>
    /// Handles one authored tab request and clears item selection when the tab changes.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="tab">The requested Defense tab.</param>
    private void HandleTabRequested(DefenseWindowView view, DefenseWindowTab tab)
    {
        if (
            !DefenseWindowRenderData.OrderedTabs.Contains(tab)
            || !TryGetSession(view, out DefenseWindowSession session)
        )
            return;

        session.SelectTab(tab);
        markDirty();
    }

    /// <summary>
    /// Routes one scroll-drag update through the owning strategy input controller.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleScrollDragged(DefenseWindowView view, PointerEventData eventData)
    {
        if (eventData != null && sessions.ContainsKey(view))
            moveItemDrag(eventData);
    }

    /// <summary>
    /// Routes one scroll-drag completion through the owning strategy input controller.
    /// </summary>
    /// <param name="view">The source Defense view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleScrollDragEnded(DefenseWindowView view, PointerEventData eventData)
    {
        if (eventData != null && sessions.ContainsKey(view))
            endItemDrag(eventData);
    }

    /// <summary>
    /// Releases subscriptions and session state for a destroyed Defense view.
    /// </summary>
    /// <param name="view">The destroyed Defense view.</param>
    private void HandleViewDestroyed(DefenseWindowView view)
    {
        if (ReferenceEquals(view, null) || !boundViews.Remove(view))
            return;

        view.Destroyed -= HandleViewDestroyed;
        view.ItemDoubleClicked -= HandleItemDoubleClicked;
        view.ItemDropped -= HandleItemDropped;
        view.ItemPressed -= HandleItemPressed;
        view.ItemReleased -= HandleItemReleased;
        view.ScrollDragged -= HandleScrollDragged;
        view.ScrollDragEnded -= HandleScrollDragEnded;
        view.SurfaceClicked -= HandleSurfaceClicked;
        view.TabRequested -= HandleTabRequested;
        sessions.Remove(view);
    }

    /// <summary>
    /// Completes the current targeting request with a Defense-window destination.
    /// </summary>
    /// <param name="session">The controller-owned Defense session.</param>
    /// <param name="item">The selected item, or null for the represented planet.</param>
    /// <returns>True when an active targeting request was completed.</returns>
    private bool TrySelectTarget(DefenseWindowSession session, ISceneNode item)
    {
        if (!targetingController.IsTargeting || session?.Planet == null)
            return false;

        return targetingController.TrySelectTarget(new StrategyMissionTarget(session.Planet, item));
    }

    /// <summary>
    /// Finds one scene node in a current ordered Defense collection.
    /// </summary>
    /// <param name="items">The current ordered items.</param>
    /// <param name="target">The scene node to find.</param>
    /// <returns>The current visual index, or negative one.</returns>
    private static int FindItemIndex(IReadOnlyList<ISceneNode> items, ISceneNode target)
    {
        if (items == null || target == null)
            return -1;

        for (int index = 0; index < items.Count; index++)
        {
            if (
                ReferenceEquals(items[index], target)
                || !string.IsNullOrEmpty(target.InstanceID)
                    && string.Equals(
                        items[index]?.InstanceID,
                        target.InstanceID,
                        StringComparison.Ordinal
                    )
            )
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Finds the Defense window representing a planet.
    /// </summary>
    /// <param name="planetId">The represented planet identifier.</param>
    /// <returns>The matching Defense view, or null when none is open.</returns>
    private DefenseWindowView FindWindow(string planetId)
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                windowManager.TryGetWindowView(window, out DefenseWindowView view)
                && sessions.TryGetValue(view, out DefenseWindowSession session)
                && session.Planet?.Planet?.InstanceID == planetId
            )
                return view;
        }

        return null;
    }

    /// <summary>
    /// Resolves a projected planet against a refreshed sector collection.
    /// </summary>
    /// <param name="planet">The previous projected planet.</param>
    /// <param name="sectors">The refreshed visible sectors.</param>
    /// <returns>The refreshed planet, or null when it is no longer represented.</returns>
    private static GalaxyMapPlanet FindFreshPlanet(
        GalaxyMapPlanet planet,
        IReadOnlyList<GalaxyMapSector> sectors
    )
    {
        string planetId = planet?.Planet?.InstanceID;
        return planetId == null
            ? null
            : sectors
                .SelectMany(sector => sector.Planets)
                .FirstOrDefault(item => item.Planet?.InstanceID == planetId);
    }

    /// <summary>
    /// Resolves a Defense session from its authored view.
    /// </summary>
    /// <param name="view">The Defense view.</param>
    /// <param name="session">Receives the matching session.</param>
    /// <returns>True when a session exists.</returns>
    private bool TryGetSession(DefenseWindowView view, out DefenseWindowSession session)
    {
        if (view != null)
            return sessions.TryGetValue(view, out session);

        session = null;
        return false;
    }

    /// <summary>
    /// Returns the controller-owned session for an initialized Defense view.
    /// </summary>
    /// <param name="view">The initialized Defense view.</param>
    /// <returns>The session owned by the view.</returns>
    private DefenseWindowSession GetSession(DefenseWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out DefenseWindowSession session))
            return session;

        throw new InvalidOperationException($"{view.name} has not been initialized.");
    }

    /// <summary>
    /// Resolves a Defense view and session from its window shell.
    /// </summary>
    /// <param name="window">The Defense window shell.</param>
    /// <param name="view">Receives the matching Defense view.</param>
    /// <param name="session">Receives the matching session.</param>
    /// <returns>True when a session exists for the window.</returns>
    private bool TryGetSession(
        UIWindow window,
        out DefenseWindowView view,
        out DefenseWindowSession session
    )
    {
        if (window == null)
        {
            view = null;
            session = null;
            return false;
        }

        foreach (KeyValuePair<DefenseWindowView, DefenseWindowSession> entry in sessions)
        {
            if (ReferenceEquals(entry.Value.Window, window))
            {
                view = entry.Key;
                session = entry.Value;
                return true;
            }
        }

        view = null;
        session = null;
        return false;
    }

    /// <summary>
    /// Verifies that game-level Defense actions were supplied before binding views.
    /// </summary>
    private void EnsureInitialized()
    {
        if (
            actions == null
            || commandActions == null
            || startItemDrag == null
            || moveItemDrag == null
            || endItemDrag == null
        )
            throw new InvalidOperationException("DefenseWindowController is not initialized.");
    }

    /// <summary>
    /// Captures immutable command state for one open Defense context menu.
    /// </summary>
    private sealed class DefenseContextMenuSource : IStrategyContextMenuSource
    {
        /// <summary>
        /// Creates one Defense context-menu source snapshot.
        /// </summary>
        /// <param name="window">The source Defense window.</param>
        /// <param name="hotspotX">The menu hotspot horizontal coordinate.</param>
        /// <param name="hotspotY">The menu hotspot vertical coordinate.</param>
        /// <param name="items">The selected scene nodes.</param>
        /// <param name="target">The selected status target.</param>
        public DefenseContextMenuSource(
            UIWindow window,
            int hotspotX,
            int hotspotY,
            IReadOnlyList<ISceneNode> items,
            StrategyStatusTarget target
        )
        {
            Window = window;
            HotspotX = hotspotX;
            HotspotY = hotspotY;
            Items = new List<ISceneNode>(items ?? Array.Empty<ISceneNode>()).AsReadOnly();
            Target = target;
        }

        /// <summary>
        /// Gets the menu hotspot horizontal coordinate.
        /// </summary>
        public int HotspotX { get; }

        /// <summary>
        /// Gets the menu hotspot vertical coordinate.
        /// </summary>
        public int HotspotY { get; }

        /// <summary>
        /// Gets the selected scene nodes.
        /// </summary>
        public IReadOnlyList<ISceneNode> Items { get; }

        /// <summary>
        /// Gets the selected status target.
        /// </summary>
        public StrategyStatusTarget Target { get; }

        /// <summary>
        /// Gets the source Defense window.
        /// </summary>
        public UIWindow Window { get; }
    }
}
