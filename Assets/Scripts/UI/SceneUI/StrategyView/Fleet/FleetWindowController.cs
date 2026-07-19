using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Performs game-level and shared-window actions requested by the fleet feature.
/// </summary>
public interface IFleetWindowActions
{
    /// <summary>
    /// Opens Encyclopedia information for one selected fleet item.
    /// </summary>
    /// <param name="items">The selected items.</param>
    void OpenFleetEncyclopediaWindow(IReadOnlyList<ISceneNode> items);

    /// <summary>
    /// Opens status information for one selected fleet item.
    /// </summary>
    /// <param name="sourceWindow">The requesting fleet window.</param>
    /// <param name="items">The selected items.</param>
    void OpenFleetStatusWindow(UIWindow sourceWindow, IReadOnlyList<ISceneNode> items);

    /// <summary>
    /// Rebuilds shared strategy state after a fleet command changes the game.
    /// </summary>
    void RefreshFleetState();
}

/// <summary>
/// Owns fleet-window sessions, selection, targeting, and semantic command routing.
/// </summary>
public sealed class FleetWindowController
    : IStrategyContextMenuProvider,
        IContextMenuReceiver,
        ITargetingReceiver
{
    private readonly HashSet<FleetWindowView> boundViews = new HashSet<FleetWindowView>();
    private readonly StrategyFleetCommandController fleetCommandController;
    private readonly Func<UIContext> getUIContext;
    private readonly Func<int, int, Vector2Int> getWindowPosition;
    private readonly Action markDirty;
    private readonly FleetWindowProjector projector;
    private readonly Dictionary<FleetWindowView, FleetWindowSession> sessions =
        new Dictionary<FleetWindowView, FleetWindowSession>();
    private readonly TargetingController targetingController;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private Action<PointerEventData> endItemDrag;
    private IFleetWindowActions actions;
    private IStrategyWindowCommandActions commandActions;
    private IStrategyConfirmationActions confirmationActions;
    private Action<PointerEventData> moveItemDrag;
    private Action<UIWindow, int, int> startItemDrag;

    /// <summary>
    /// Creates the fleet feature controller with its shared interaction dependencies.
    /// </summary>
    /// <param name="fleetCommandController">Executes shared fleet mutations.</param>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="targetingController">Owns the active strategy targeting request.</param>
    /// <param name="windowLayer">Provides the authored fleet prefab and normal window layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getWindowPosition">Clamps a requested fleet-window placement.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public FleetWindowController(
        StrategyFleetCommandController fleetCommandController,
        Func<UIContext> getUIContext,
        TargetingController targetingController,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<int, int, Vector2Int> getWindowPosition,
        Action markDirty
    )
    {
        this.fleetCommandController =
            fleetCommandController
            ?? throw new ArgumentNullException(nameof(fleetCommandController));
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        projector = new FleetWindowProjector(getUIContext);
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
    /// Supplies fleet command and interaction actions after the strategy controller graph is constructed.
    /// </summary>
    /// <param name="windowActions">The feature-specific fleet actions.</param>
    /// <param name="windowCommandActions">The shared mission and movement actions.</param>
    /// <param name="windowConfirmationActions">The shared confirmation actions.</param>
    /// <param name="beginItemDrag">Begins a strategy item-drag candidate.</param>
    /// <param name="continueItemDrag">Advances the active strategy item drag.</param>
    /// <param name="completeItemDrag">Completes the active strategy item drag.</param>
    public void Initialize(
        IFleetWindowActions windowActions,
        IStrategyWindowCommandActions windowCommandActions,
        IStrategyConfirmationActions windowConfirmationActions,
        Action<UIWindow, int, int> beginItemDrag,
        Action<PointerEventData> continueItemDrag,
        Action<PointerEventData> completeItemDrag
    )
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
        commandActions =
            windowCommandActions ?? throw new ArgumentNullException(nameof(windowCommandActions));
        confirmationActions =
            windowConfirmationActions
            ?? throw new ArgumentNullException(nameof(windowConfirmationActions));
        startItemDrag = beginItemDrag ?? throw new ArgumentNullException(nameof(beginItemDrag));
        moveItemDrag =
            continueItemDrag ?? throw new ArgumentNullException(nameof(continueItemDrag));
        endItemDrag = completeItemDrag ?? throw new ArgumentNullException(nameof(completeItemDrag));
    }

    /// <summary>
    /// Starts a fleet-window session for one authored view and planet projection.
    /// </summary>
    /// <param name="view">The destination fleet view.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="planet">The represented strategy planet.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeWindow(FleetWindowView view, UIWindow window, GalaxyMapPlanet planet)
    {
        if (view == null || window == null || planet?.Planet == null)
            return false;

        BindWindow(view);
        sessions[view] = new FleetWindowSession(planet, window);
        return true;
    }

    /// <summary>
    /// Opens or focuses the fleet window for a planet.
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

        FleetWindowSession existing = FindWindow(planet.Planet.InstanceID);
        if (existing != null)
        {
            windowManager.Focus(existing.Window);
            return existing.Window;
        }

        Vector2Int position = getWindowPosition(sourceX, sourceY);
        UIWindow window = windowManager.CreateWindow(
            windowLayer.FleetWindowPrefab,
            windowLayer.GetWindowParent(false),
            $"FleetWindow-{planet.Planet.GetDisplayName()}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.FleetWindowPrefab),
            false,
            true,
            true,
            false,
            out FleetWindowView view
        );
        if (!TryInitializeWindow(view, window, planet))
        {
            windowManager.DestroyWindow(window);
            return null;
        }

        created = true;
        markDirty();
        return window;
    }

    /// <summary>
    /// Renders every registered fleet window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out FleetWindowView view))
                RenderWindow(view, window, window.ActiveWindow);
        }
    }

    /// <summary>
    /// Rebinds fleet sessions to a refreshed galaxy snapshot.
    /// </summary>
    /// <param name="sectors">The refreshed visible sectors.</param>
    public void ReconcileWindows(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            return;

        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                !windowManager.TryGetWindowView(window, out FleetWindowView view)
                || !sessions.TryGetValue(view, out FleetWindowSession session)
            )
                continue;

            GalaxyMapPlanet planet = FindFreshPlanet(session.Planet, sectors);
            if (planet == null)
                continue;

            session.RebindPlanet(planet);
        }
    }

    /// <summary>
    /// Projects current domain state and renders one fleet window.
    /// </summary>
    /// <param name="view">The destination fleet view.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    public void RenderWindow(FleetWindowView view, UIWindow window, bool active)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        FleetWindowSession session = GetSession(view);
        view.Render(projector.Build(session, window, active));
    }

    /// <summary>
    /// Gets the planet represented by one fleet view.
    /// </summary>
    /// <param name="view">The fleet view.</param>
    /// <returns>The represented planet, or null.</returns>
    public GalaxyMapPlanet GetPlanet(FleetWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out FleetWindowSession session)
            ? session.Planet
            : null;
    }

    /// <summary>
    /// Replaces one stale planet projection while preserving local UI state.
    /// </summary>
    /// <param name="view">The fleet view.</param>
    /// <param name="planet">The fresh strategy planet projection.</param>
    public void ReconcileWindow(FleetWindowView view, GalaxyMapPlanet planet)
    {
        if (
            view != null
            && planet?.Planet != null
            && sessions.TryGetValue(view, out FleetWindowSession session)
        )
        {
            session.RebindPlanet(planet);
        }
    }

    /// <summary>
    /// Selects the fleet and detail tab containing a Finder result.
    /// </summary>
    /// <param name="view">The destination fleet view.</param>
    /// <param name="row">The Finder row to select.</param>
    /// <returns>True when the result belongs to this fleet session.</returns>
    public bool SelectFinderTarget(FleetWindowView view, FinderWindowRow row)
    {
        return row != null && SelectTarget(view, row.Node ?? row.Fleet);
    }

    /// <summary>
    /// Selects the fleet and detail tab containing one scene node.
    /// </summary>
    /// <param name="view">The destination fleet view.</param>
    /// <param name="target">The target scene node.</param>
    /// <returns>True when the target belongs to this fleet session.</returns>
    public bool SelectTarget(FleetWindowView view, ISceneNode target)
    {
        if (
            view == null
            || target == null
            || !sessions.TryGetValue(view, out FleetWindowSession session)
        )
            return false;

        if (!session.SelectTarget(target, GetTargetTab(target)))
            return false;

        view.ClearDragSource();
        return true;
    }

    /// <summary>
    /// Gets the selected detail tab for a fleet view.
    /// </summary>
    /// <param name="view">The fleet view.</param>
    /// <returns>The selected tab, or the capital-ships tab when no session exists.</returns>
    internal FleetWindowTab GetActiveTab(FleetWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out FleetWindowSession session)
            ? session.ActiveTab
            : FleetWindowTab.CapitalShips;
    }

    /// <summary>
    /// Gets the selected fleet-row index for a fleet view.
    /// </summary>
    /// <param name="view">The fleet view.</param>
    /// <returns>The selected fleet-row index, or -1 when no session exists.</returns>
    internal int GetSelectedFleetIndex(FleetWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out FleetWindowSession session)
            ? session.SelectedFleetIndex
            : -1;
    }

    /// <summary>
    /// Gets the scene nodes targeted by the current context or drag selection.
    /// </summary>
    /// <param name="window">The source fleet window.</param>
    /// <returns>The selected scene nodes in visual order.</returns>
    public List<ISceneNode> GetContextItems(UIWindow window)
    {
        return TryGetSession(window, out _, out FleetWindowSession session)
            ? session.GetContextItems()
            : new List<ISceneNode>();
    }

    /// <summary>
    /// Resolves the single current status target for one fleet window.
    /// </summary>
    /// <param name="window">The source fleet window.</param>
    /// <returns>The status target, or null when selection is not singular.</returns>
    public StrategyStatusTarget GetStatusTarget(UIWindow window)
    {
        if (!TryGetSession(window, out _, out FleetWindowSession session))
            return null;

        List<ISceneNode> items = session.GetContextItems();
        return items.Count == 1 ? new StrategyStatusTarget(session.Planet, items[0]) : null;
    }

    /// <summary>
    /// Clears selection and transient pointer state owned by one fleet window.
    /// </summary>
    /// <param name="window">The source fleet window.</param>
    public void ClearSelection(UIWindow window)
    {
        if (!TryGetSession(window, out FleetWindowView view, out FleetWindowSession session))
            return;

        session.ClearItemSelection();
        view.ClearDragSource();
    }

    /// <summary>
    /// Builds and opens the fleet context menu for one provider request.
    /// </summary>
    /// <param name="context">The shared context-menu provider state.</param>
    /// <param name="request">Receives the command request.</param>
    /// <param name="width">Receives the authored menu width.</param>
    /// <returns>True when the request belongs to a fleet window.</returns>
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
            || !windowManager.TryGetWindowView(context.Window, out FleetWindowView view)
            || !sessions.TryGetValue(view, out FleetWindowSession session)
        )
            return false;

        CaptureContextTarget(view, session, context.EventData);
        List<ISceneNode> items = session.GetContextItems();
        string playerFactionId = getUIContext()?.GetPlayerFactionInstanceID();
        List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
            items,
            StrategyContextMenuAvailability.PlayerControlsItems(items, playerFactionId),
            StrategyContextMenuAvailability.CanMoveItems(items, playerFactionId),
            StrategyContextMenuAvailability.CanCreateMission(items, playerFactionId),
            confirmationActions.CanRetire(items)
        );
        FleetContextMenuSource source = new FleetContextMenuSource(
            context.Window,
            context.X,
            context.Y,
            items
        );
        request = new ContextMenuRequest(
            source,
            commands.Cast<IContextMenuCommand>().ToList(),
            this
        );
        bool hasBombardment = commands.Any(command =>
            command.Action == StrategyContextMenuActions.PlanetaryBombardment
        );
        width = hasBombardment
            ? context.Layout.FleetBombardmentMenuWidth
            : context.Layout.FleetMenuWidth;
        return true;
    }

    /// <summary>
    /// Routes one selected fleet context-menu command.
    /// </summary>
    /// <param name="request">The completed shared context-menu request.</param>
    /// <param name="command">The selected command.</param>
    public void OnContextMenuCommandSelected(
        ContextMenuRequest request,
        IContextMenuCommand command
    )
    {
        if (
            request?.Source is not FleetContextMenuSource source
            || command is not StrategyMenuCommand menuCommand
        )
            return;

        switch (menuCommand.Action)
        {
            case StrategyContextMenuActions.Encyclopedia:
                actions.OpenFleetEncyclopediaWindow(source.Items);
                break;
            case StrategyContextMenuActions.Status:
                actions.OpenFleetStatusWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.Rename:
                BeginRename(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.Scrap:
                confirmationActions.OpenScrapConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.Stop:
                confirmationActions.OpenStopConstructionConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.Retire:
                confirmationActions.OpenRetireConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.CreateFleet:
                TryCreateFleetFromCapitalShips(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.PlanetaryBombardment:
                TryExecutePlanetaryBombardment(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.CreateMission:
            case StrategyContextMenuActions.Move:
            case StrategyContextMenuActions.MoveConfirm:
                targetingController.Begin(
                    new TargetingRequest(
                        StrategyWindowTargetingSource.GetPrompt(menuCommand.Action),
                        new StrategyWindowTargetingSource(
                            source.Window,
                            menuCommand.Action,
                            source.HotspotX,
                            source.HotspotY,
                            source.Items
                        ),
                        this
                    ),
                    source.HotspotX,
                    source.HotspotY
                );
                break;
        }
    }

    /// <summary>
    /// Creates a new fleet from a controlled capital-ship selection.
    /// </summary>
    /// <param name="sourceWindow">The requesting fleet window.</param>
    /// <param name="items">The selected capital ships.</param>
    /// <returns>True when a fleet was created.</returns>
    private bool TryCreateFleetFromCapitalShips(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        if (!fleetCommandController.TryCreateFleetFromCapitalShips(items))
            return false;

        ClearSelection(sourceWindow);
        actions.RefreshFleetState();
        return true;
    }

    /// <summary>
    /// Executes planetary bombardment for the selected fleets.
    /// </summary>
    /// <param name="sourceWindow">The requesting fleet window.</param>
    /// <param name="items">The selected fleets.</param>
    /// <returns>True when bombardment was executed.</returns>
    private bool TryExecutePlanetaryBombardment(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<Fleet> fleets = items?.OfType<Fleet>().ToList() ?? new List<Fleet>();
        if (fleets.Count == 0)
            return false;

        Planet targetPlanet = GetBombardmentTargetPlanet(sourceWindow, fleets);
        if (targetPlanet == null)
            return false;

        if (!fleetCommandController.TryExecutePlanetaryBombardment(items, targetPlanet))
            return false;

        ClearSelection(sourceWindow);
        actions.RefreshFleetState();
        return true;
    }

    /// <summary>
    /// Resolves the authoritative planet targeted by fleet bombardment.
    /// </summary>
    /// <param name="sourceWindow">The requesting fleet window.</param>
    /// <param name="fleets">The selected fleets.</param>
    /// <returns>The authoritative target planet, or null.</returns>
    private Planet GetBombardmentTargetPlanet(UIWindow sourceWindow, IReadOnlyList<Fleet> fleets)
    {
        Planet fleetPlanet = fleets
            ?.Select(fleet => fleet?.GetParentOfType<Planet>())
            .FirstOrDefault(planet => planet != null);
        if (fleetPlanet != null)
            return fleetPlanet;

        if (!TryGetSession(sourceWindow, out _, out FleetWindowSession session))
            return null;

        return session.Planet?.Planet;
    }

    /// <summary>
    /// Handles context-menu cancellation without changing fleet selection.
    /// </summary>
    /// <param name="request">The canceled context-menu request.</param>
    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    /// <summary>
    /// Routes a completed fleet targeting request to its game-level action.
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
    /// Handles targeting cancellation without changing fleet selection.
    /// </summary>
    /// <param name="request">The canceled targeting request.</param>
    public void OnTargetingCancelled(TargetingRequest request) { }

    /// <summary>
    /// Subscribes to one authored fleet view exactly once.
    /// </summary>
    /// <param name="view">The fleet view to bind.</param>
    private void BindWindow(FleetWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        EnsureInitialized();
        if (!boundViews.Add(view))
            return;

        view.Destroyed += HandleViewDestroyed;
        view.DetailItemDoubleClicked += HandleDetailItemDoubleClicked;
        view.DetailItemDropped += HandleDetailItemDropped;
        view.DetailItemPressed += HandleDetailItemPressed;
        view.DetailItemReleased += HandleDetailItemReleased;
        view.FleetListDropped += HandleFleetListDropped;
        view.FleetRowDoubleClicked += HandleFleetRowDoubleClicked;
        view.FleetRowDropped += HandleFleetRowDropped;
        view.FleetRowPressed += HandleFleetRowPressed;
        view.FleetRowReleased += HandleFleetRowReleased;
        view.RenameCancelled += HandleRenameCancelled;
        view.RenameSubmitted += HandleRenameSubmitted;
        view.ScrollDragged += HandleScrollDragged;
        view.ScrollDragEnded += HandleScrollDragEnded;
        view.SurfaceClicked += HandleSurfaceClicked;
        view.TabRequested += HandleTabRequested;
    }

    /// <summary>
    /// Begins rename presentation for one selected fleet or capital ship.
    /// </summary>
    /// <param name="window">The source fleet window.</param>
    /// <param name="items">The selected items.</param>
    private void BeginRename(UIWindow window, IReadOnlyList<ISceneNode> items)
    {
        if (
            !TryGetSession(window, out _, out FleetWindowSession session)
            || items == null
            || items.Count != 1
            || items[0] is not Fleet and not CapitalShip
        )
            return;

        if (session.BeginRename(items[0]))
            markDirty();
    }

    /// <summary>
    /// Captures the exact row or detail card under a context-menu pointer event.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="session">The controller-owned fleet session.</param>
    /// <param name="eventData">The pointer event.</param>
    private static void CaptureContextTarget(
        FleetWindowView view,
        FleetWindowSession session,
        PointerEventData eventData
    )
    {
        if (view.TryGetFleetRowIndex(eventData, out int fleetIndex))
        {
            session.CaptureFleetContext(fleetIndex);
            return;
        }

        if (view.TryGetDetailItemIndex(eventData, out int detailItemIndex))
        {
            session.CaptureDetailContext(detailItemIndex);
            return;
        }

        session.ClearContext();
    }

    /// <summary>
    /// Handles a fleet-row press and starts selection, context, or drag flow.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="fleetIndex">The pressed fleet-row index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetRowPressed(
        FleetWindowView view,
        int fleetIndex,
        PointerEventData eventData
    )
    {
        if (!TryGetSession(view, out FleetWindowSession session) || eventData == null)
            return;

        if (!session.TryGetFleet(fleetIndex, out _))
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
            session.Window.RequestFocus();
        if (
            targetingController.IsTargeting
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        view.ClearDragSource();
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            session.CaptureFleetContext(fleetIndex);
            markDirty();
            session.Window.RequestContext(eventData);
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        bool canStartDrag = session.PrepareFleetDragSelection(fleetIndex);
        if (
            canStartDrag
            && view.FleetRowContainsDragSource(fleetIndex, eventData)
            && TryGetDesktopPosition(session, eventData, out int x, out int y)
        )
        {
            view.SetFleetRowDragSource(fleetIndex);
            startItemDrag(session.Window, x, y);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Handles a fleet-row release and resolves targeting or final selection.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="fleetIndex">The released fleet-row index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetRowReleased(
        FleetWindowView view,
        int fleetIndex,
        PointerEventData eventData
    )
    {
        if (
            eventData?.button != PointerEventData.InputButton.Left
            || !TryGetSession(view, out FleetWindowSession session)
        )
            return;

        if (!session.TryGetFleet(fleetIndex, out Fleet fleet))
            return;
        if (TrySelectTarget(session, fleet))
            return;
        if (SelectableListSelection.HasSelectionModifier())
            return;

        view.ClearDragSource();
        session.SelectFleet(fleetIndex);
        markDirty();
    }

    /// <summary>
    /// Handles a drop over one fleet row as a targeting selection.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="fleetIndex">The drop fleet-row index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetRowDropped(
        FleetWindowView view,
        int fleetIndex,
        PointerEventData eventData
    )
    {
        if (!TryGetSession(view, out FleetWindowSession session))
            return;

        if (session.TryGetFleet(fleetIndex, out Fleet fleet))
            TrySelectTarget(session, fleet);
    }

    /// <summary>
    /// Handles a fleet-row double click as a status request.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="fleetIndex">The double-clicked fleet-row index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetRowDoubleClicked(
        FleetWindowView view,
        int fleetIndex,
        PointerEventData eventData
    )
    {
        OpenStatusWindow(view, eventData);
    }

    /// <summary>
    /// Handles a detail-card press and starts selection, context, or drag flow.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="itemIndex">The pressed detail-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDetailItemPressed(
        FleetWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (!TryGetSession(view, out FleetWindowSession session) || eventData == null)
            return;

        if (!session.TryGetDetailItem(itemIndex, out _))
            return;
        if (eventData.button == PointerEventData.InputButton.Left)
            session.Window.RequestFocus();
        if (
            targetingController.IsTargeting
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        view.ClearDragSource();
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            session.CaptureDetailContext(itemIndex);
            markDirty();
            session.Window.RequestContext(eventData);
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        bool canStartDrag = session.PrepareDetailDragSelection(itemIndex);
        if (
            canStartDrag
            && view.DetailItemContainsDragSource(itemIndex, eventData)
            && TryGetDesktopPosition(session, eventData, out int x, out int y)
        )
        {
            view.SetDetailItemDragSource(itemIndex);
            startItemDrag(session.Window, x, y);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Handles a detail-card release and resolves targeting or final selection.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="itemIndex">The released detail-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDetailItemReleased(
        FleetWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (
            eventData?.button != PointerEventData.InputButton.Left
            || !TryGetSession(view, out FleetWindowSession session)
        )
            return;

        if (!session.TryGetDetailItem(itemIndex, out ISceneNode item))
            return;
        if (TrySelectTarget(session, item))
            return;
        if (SelectableListSelection.HasSelectionModifier())
            return;

        view.ClearDragSource();
        session.SelectDetailItem(itemIndex);
        markDirty();
    }

    /// <summary>
    /// Handles a drop over one detail card as a targeting selection.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="itemIndex">The drop detail-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDetailItemDropped(
        FleetWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (!TryGetSession(view, out FleetWindowSession session))
            return;

        if (session.TryGetDetailItem(itemIndex, out ISceneNode item))
            TrySelectTarget(session, item);
    }

    /// <summary>
    /// Handles a detail-card double click as a status request.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="itemIndex">The double-clicked detail-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDetailItemDoubleClicked(
        FleetWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        OpenStatusWindow(view, eventData);
    }

    /// <summary>
    /// Handles a fleet-list surface drop as a planet-level targeting selection.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetListDropped(FleetWindowView view, PointerEventData eventData)
    {
        if (TryGetSession(view, out FleetWindowSession session))
            TrySelectTarget(session, null);
    }

    /// <summary>
    /// Handles a fleet-window surface click as targeting or transient-state clearing.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleSurfaceClicked(FleetWindowView view, PointerEventData eventData)
    {
        if (!TryGetSession(view, out FleetWindowSession session))
            return;
        if (TrySelectTarget(session, null) || view.IsSelectionItemClick(eventData))
            return;

        session.ClearContext();
        view.ClearDragSource();
        markDirty();
    }

    /// <summary>
    /// Handles one authored tab request and clears detail selection.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="tab">The requested fleet tab.</param>
    private void HandleTabRequested(FleetWindowView view, FleetWindowTab tab)
    {
        if (!TryGetSession(view, out FleetWindowSession session) || !session.SelectTab(tab))
            return;

        view.ClearDragSource();
        markDirty();
    }

    /// <summary>
    /// Applies a submitted fleet or capital-ship name and invalidates presentation.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="value">The submitted name.</param>
    private void HandleRenameSubmitted(FleetWindowView view, string value)
    {
        if (!TryGetSession(view, out FleetWindowSession session))
            return;

        if (session.RenameTarget != null && !string.IsNullOrEmpty(value))
            session.RenameTarget.DisplayName = value;
        session.EndRename();
        markDirty();
    }

    /// <summary>
    /// Clears controller-owned rename state after local cancellation.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    private void HandleRenameCancelled(FleetWindowView view)
    {
        if (!TryGetSession(view, out FleetWindowSession session))
            return;

        session.EndRename();
        markDirty();
    }

    /// <summary>
    /// Routes a scroll-surface drag to the shared item-drag flow.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleScrollDragged(FleetWindowView view, PointerEventData eventData)
    {
        if (eventData != null)
            moveItemDrag(eventData);
    }

    /// <summary>
    /// Routes the end of a scroll-surface drag to the shared item-drag flow.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleScrollDragEnded(FleetWindowView view, PointerEventData eventData)
    {
        if (eventData != null)
            endItemDrag(eventData);
    }

    /// <summary>
    /// Opens status for the current singular fleet-window selection.
    /// </summary>
    /// <param name="view">The source fleet view.</param>
    /// <param name="eventData">The double-click pointer event.</param>
    private void OpenStatusWindow(FleetWindowView view, PointerEventData eventData)
    {
        if (
            eventData?.button != PointerEventData.InputButton.Left
            || targetingController.IsTargeting
            || !TryGetSession(view, out FleetWindowSession session)
        )
            return;

        actions.OpenFleetStatusWindow(session.Window, session.GetContextItems());
    }

    /// <summary>
    /// Completes the active targeting request with one fleet-window target.
    /// </summary>
    /// <param name="session">The controller-owned fleet session.</param>
    /// <param name="item">The optional item within the represented planet.</param>
    /// <returns>True when an active targeting request completed.</returns>
    private bool TrySelectTarget(FleetWindowSession session, ISceneNode item)
    {
        return targetingController.IsTargeting
            && session?.Planet?.Planet != null
            && targetingController.TrySelectTarget(new StrategyMissionTarget(session.Planet, item));
    }

    /// <summary>
    /// Resolves a pointer event to strategy source coordinates through the owning window.
    /// </summary>
    /// <param name="session">The controller-owned fleet session.</param>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="x">Receives the horizontal source coordinate.</param>
    /// <param name="y">Receives the vertical source coordinate.</param>
    /// <returns>True when the pointer could be resolved.</returns>
    private static bool TryGetDesktopPosition(
        FleetWindowSession session,
        PointerEventData eventData,
        out int x,
        out int y
    )
    {
        x = 0;
        y = 0;
        return session?.Window != null
            && eventData != null
            && session.Window.TryGetDesktopPosition(eventData, eventData.position, out x, out y);
    }

    /// <summary>
    /// Maps one target type to its fleet detail tab.
    /// </summary>
    /// <param name="target">The target node.</param>
    /// <returns>The matching detail tab.</returns>
    private static FleetWindowTab GetTargetTab(ISceneNode target)
    {
        return target switch
        {
            Starfighter => FleetWindowTab.Starfighters,
            Regiment => FleetWindowTab.Regiments,
            Officer or SpecialForces => FleetWindowTab.Personnel,
            _ => FleetWindowTab.CapitalShips,
        };
    }

    /// <summary>
    /// Finds the fleet window session representing a planet.
    /// </summary>
    /// <param name="planetId">The represented planet identifier.</param>
    /// <returns>The matching fleet session, or null when none is open.</returns>
    private FleetWindowSession FindWindow(string planetId)
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                windowManager.TryGetWindowView(window, out FleetWindowView view)
                && sessions.TryGetValue(view, out FleetWindowSession session)
                && session.Planet?.Planet?.InstanceID == planetId
            )
                return session;
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
    /// Finds a controller-owned fleet session by its authored view.
    /// </summary>
    /// <param name="view">The fleet view.</param>
    /// <param name="session">Receives the session.</param>
    /// <returns>True when the view has a session.</returns>
    private bool TryGetSession(FleetWindowView view, out FleetWindowSession session)
    {
        if (view != null)
            return sessions.TryGetValue(view, out session);

        session = null;
        return false;
    }

    /// <summary>
    /// Returns the controller-owned session for an initialized fleet view.
    /// </summary>
    /// <param name="view">The initialized fleet view.</param>
    /// <returns>The session owned by the view.</returns>
    private FleetWindowSession GetSession(FleetWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out FleetWindowSession session))
            return session;

        throw new InvalidOperationException($"{view.name} has not been initialized.");
    }

    /// <summary>
    /// Finds a controller-owned fleet session by its window shell.
    /// </summary>
    /// <param name="window">The fleet window shell.</param>
    /// <param name="view">Receives the authored fleet view.</param>
    /// <param name="session">Receives the session.</param>
    /// <returns>True when the window owns a fleet session.</returns>
    private bool TryGetSession(
        UIWindow window,
        out FleetWindowView view,
        out FleetWindowSession session
    )
    {
        if (window != null)
        {
            foreach (KeyValuePair<FleetWindowView, FleetWindowSession> entry in sessions)
            {
                if (ReferenceEquals(entry.Value.Window, window))
                {
                    view = entry.Key;
                    session = entry.Value;
                    return true;
                }
            }
        }

        view = null;
        session = null;
        return false;
    }

    /// <summary>
    /// Verifies game-level action routing before a fleet view is bound.
    /// </summary>
    private void EnsureInitialized()
    {
        if (
            actions == null
            || commandActions == null
            || confirmationActions == null
            || startItemDrag == null
            || moveItemDrag == null
            || endItemDrag == null
        )
            throw new InvalidOperationException(
                $"{nameof(FleetWindowController)} must be initialized before use."
            );
    }

    /// <summary>
    /// Releases subscriptions and state for a destroyed fleet view.
    /// </summary>
    /// <param name="view">The destroyed fleet view.</param>
    private void HandleViewDestroyed(FleetWindowView view)
    {
        if (ReferenceEquals(view, null) || !boundViews.Remove(view))
            return;

        view.Destroyed -= HandleViewDestroyed;
        view.DetailItemDoubleClicked -= HandleDetailItemDoubleClicked;
        view.DetailItemDropped -= HandleDetailItemDropped;
        view.DetailItemPressed -= HandleDetailItemPressed;
        view.DetailItemReleased -= HandleDetailItemReleased;
        view.FleetListDropped -= HandleFleetListDropped;
        view.FleetRowDoubleClicked -= HandleFleetRowDoubleClicked;
        view.FleetRowDropped -= HandleFleetRowDropped;
        view.FleetRowPressed -= HandleFleetRowPressed;
        view.FleetRowReleased -= HandleFleetRowReleased;
        view.RenameCancelled -= HandleRenameCancelled;
        view.RenameSubmitted -= HandleRenameSubmitted;
        view.ScrollDragged -= HandleScrollDragged;
        view.ScrollDragEnded -= HandleScrollDragEnded;
        view.SurfaceClicked -= HandleSurfaceClicked;
        view.TabRequested -= HandleTabRequested;
        sessions.Remove(view);
    }

    /// <summary>
    /// Captures immutable command state for one open fleet context menu.
    /// </summary>
    private sealed class FleetContextMenuSource : IStrategyContextMenuSource
    {
        /// <summary>
        /// Creates one fleet context-menu source snapshot.
        /// </summary>
        /// <param name="window">The source fleet window.</param>
        /// <param name="hotspotX">The menu hotspot horizontal coordinate.</param>
        /// <param name="hotspotY">The menu hotspot vertical coordinate.</param>
        /// <param name="items">The selected scene nodes.</param>
        public FleetContextMenuSource(
            UIWindow window,
            int hotspotX,
            int hotspotY,
            IReadOnlyList<ISceneNode> items
        )
        {
            Window = window;
            HotspotX = hotspotX;
            HotspotY = hotspotY;
            Items = new List<ISceneNode>(items ?? Array.Empty<ISceneNode>()).AsReadOnly();
        }

        public int HotspotX { get; }

        public int HotspotY { get; }

        public IReadOnlyList<ISceneNode> Items { get; }

        public UIWindow Window { get; }
    }
}
