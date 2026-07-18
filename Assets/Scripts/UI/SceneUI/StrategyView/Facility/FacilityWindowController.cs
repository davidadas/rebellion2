using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Performs window-level actions requested by the facility feature.
/// </summary>
public interface IFacilityWindowActions
{
    /// <summary>
    /// Opens status information for the active facility selection.
    /// </summary>
    /// <param name="target">The resolved facility status target.</param>
    void OpenFacilityStatus(StrategyStatusTarget target);

    /// <summary>
    /// Opens Encyclopedia information for the active facility selection.
    /// </summary>
    /// <param name="target">The resolved facility information target.</param>
    void OpenFacilityInfo(StrategyStatusTarget target);

    /// <summary>
    /// Rebuilds shared strategy state after a facility command changes the game.
    /// </summary>
    void RefreshFacilityState();
}

/// <summary>
/// Owns facility window state, projection, context commands, and targeting interaction.
/// </summary>
public sealed class FacilityWindowController
    : IStrategyContextMenuProvider,
        IContextMenuReceiver,
        ITargetingReceiver
{
    private const int _manufacturingCardIndexLimit = (int)FacilityWindowTab.Construction + 1;
    private readonly HashSet<FacilityWindowView> boundViews = new HashSet<FacilityWindowView>();
    private readonly ConstructionWindowController constructionWindowController;
    private readonly Func<Rebellion.Game.GameRoot> getGame;
    private readonly Func<int, int, Vector2Int> getWindowPosition;
    private readonly Action markDirty;
    private readonly FacilityWindowProjector projector;
    private readonly Dictionary<FacilityWindowView, FacilityWindowSession> sessions =
        new Dictionary<FacilityWindowView, FacilityWindowSession>();
    private readonly TargetingController targetingController;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;

    private IFacilityWindowActions actions;
    private IStrategyConfirmationActions confirmationActions;

    /// <summary>
    /// Creates a facility feature controller.
    /// </summary>
    /// <param name="getGame">Returns the active game.</param>
    /// <param name="constructionWindowController">The construction sessions affected by destination changes.</param>
    /// <param name="getUIContext">Returns the active strategy presentation context.</param>
    /// <param name="targetingController">The strategy targeting controller.</param>
    /// <param name="windowLayer">Provides the authored facility prefab and normal window layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getWindowPosition">Clamps a requested facility-window placement.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public FacilityWindowController(
        Func<Rebellion.Game.GameRoot> getGame,
        ConstructionWindowController constructionWindowController,
        Func<UIContext> getUIContext,
        TargetingController targetingController,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<int, int, Vector2Int> getWindowPosition,
        Action markDirty
    )
    {
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.constructionWindowController =
            constructionWindowController
            ?? throw new ArgumentNullException(nameof(constructionWindowController));
        this.targetingController =
            targetingController ?? throw new ArgumentNullException(nameof(targetingController));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        projector = new FacilityWindowProjector(getUIContext);
    }

    /// <summary>
    /// Connects facility feature requests to strategy window actions.
    /// </summary>
    /// <param name="windowActions">The feature-specific facility actions.</param>
    /// <param name="windowConfirmationActions">The shared confirmation actions.</param>
    public void Initialize(
        IFacilityWindowActions windowActions,
        IStrategyConfirmationActions windowConfirmationActions
    )
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
        confirmationActions =
            windowConfirmationActions
            ?? throw new ArgumentNullException(nameof(windowConfirmationActions));
    }

    /// <summary>
    /// Subscribes the controller to one facility view exactly once.
    /// </summary>
    /// <param name="view">The facility view to bind.</param>
    public void BindWindow(FacilityWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        EnsureInitialized();
        if (!boundViews.Add(view))
            return;

        view.BackgroundClicked += HandleBackgroundClicked;
        view.Destroyed += HandleViewDestroyed;
        view.InventoryItemDoubleClicked += HandleInventoryItemDoubleClicked;
        view.InventoryItemPressed += HandleInventoryItemPressed;
        view.InventoryItemReleased += HandleInventoryItemReleased;
        view.ManufacturingCardPressed += HandleManufacturingCardPressed;
        view.ManufacturingCardReleased += HandleManufacturingCardReleased;
        view.TabSelected += HandleTabSelected;
    }

    /// <summary>
    /// Starts a facility-window session for one authored view and planet projection.
    /// </summary>
    /// <param name="view">The destination facility view.</param>
    /// <param name="window">The owning facility window.</param>
    /// <param name="planet">The represented strategy planet.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeWindow(
        FacilityWindowView view,
        UIWindow window,
        GalaxyMapPlanet planet
    )
    {
        if (view == null || window == null || planet?.Planet == null)
            return false;

        BindWindow(view);
        sessions[view] = new FacilityWindowSession(window, planet);
        return true;
    }

    /// <summary>
    /// Opens or focuses the facility window for a planet.
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

        FacilityWindowView existing = FindWindow(planet.Planet.InstanceID);
        if (existing != null && TryGetSession(existing, out FacilityWindowSession existingSession))
        {
            existingSession.Window.RequestFocus();
            return existingSession.Window;
        }

        Vector2Int position = getWindowPosition(sourceX, sourceY);
        UIWindow window = windowManager.CreateWindow(
            windowLayer.FacilityWindowPrefab,
            windowLayer.GetWindowParent(false),
            $"FacilityWindow-{planet.Planet.GetDisplayName()}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.FacilityWindowPrefab),
            false,
            true,
            true,
            false,
            out FacilityWindowView view
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
    /// Renders every registered facility window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out FacilityWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Rebinds facility sessions to a refreshed galaxy snapshot.
    /// </summary>
    /// <param name="sectors">The refreshed visible sectors.</param>
    public void ReconcileWindows(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            return;

        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                !windowManager.TryGetWindowView(window, out FacilityWindowView view)
                || !sessions.TryGetValue(view, out FacilityWindowSession session)
            )
                continue;

            GalaxyMapPlanet planet = FindFreshPlanet(session.Planet, sectors);
            if (planet != null)
                session.RebindPlanet(planet);
        }
    }

    /// <summary>
    /// Gets the planet represented by one facility view.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <returns>The represented planet, or null.</returns>
    public GalaxyMapPlanet GetPlanet(FacilityWindowView view)
    {
        return TryGetSession(view, out FacilityWindowSession session) ? session.Planet : null;
    }

    /// <summary>
    /// Replaces one facility session's stale planet projection while preserving local UI state.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <param name="planet">The fresh strategy planet projection.</param>
    public void ReconcileWindow(FacilityWindowView view, GalaxyMapPlanet planet)
    {
        if (planet?.Planet != null && TryGetSession(view, out FacilityWindowSession session))
            session.RebindPlanet(planet);
    }

    /// <summary>
    /// Projects and renders one facility window.
    /// </summary>
    /// <param name="view">The destination facility view.</param>
    /// <param name="window">The owning window shell.</param>
    public void RenderWindow(FacilityWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        FacilityWindowSession session = GetSession(view);

        view.Render(
            projector.CreateRenderData(window, session, GetManufacturingDestinationNames(session))
        );
    }

    /// <summary>
    /// Builds the context commands available for the facility control under the pointer.
    /// </summary>
    /// <param name="context">The active context-menu invocation.</param>
    /// <param name="request">Receives the completed command request.</param>
    /// <param name="width">Receives the authored menu width.</param>
    /// <returns>True when a facility command menu was produced.</returns>
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
            || !windowManager.TryGetWindowView(context.Window, out FacilityWindowView view)
        )
            return false;

        if (!TryGetSession(view, out FacilityWindowSession session))
            return false;

        CaptureContextTarget(view, session, context.EventData);
        List<StrategyMenuCommand> commands = CreateContextCommands(
            session.Planet?.Planet,
            session,
            getGame()?.GetPlayerFaction()?.InstanceID
        );
        if (commands.Count == 0)
            return false;

        request = new ContextMenuRequest(
            context,
            commands.Cast<IContextMenuCommand>().ToList(),
            this
        );
        width = context.Layout.FacilityMenuWidth;
        return true;
    }

    /// <summary>
    /// Routes one selected facility context command.
    /// </summary>
    /// <param name="request">The completed context-menu request.</param>
    /// <param name="command">The selected context-menu command.</param>
    public void OnContextMenuCommandSelected(
        ContextMenuRequest request,
        IContextMenuCommand command
    )
    {
        if (
            request?.Source is not StrategyContextMenuProviderContext context
            || command is not StrategyMenuCommand strategyCommand
            || !windowManager.TryGetWindowView(context.Window, out FacilityWindowView view)
        )
            return;

        switch (strategyCommand.Action)
        {
            case StrategyContextMenuActions.Build:
                OpenConstruction(context.Window);
                break;
            case StrategyContextMenuActions.Stop:
                confirmationActions.OpenStopConstructionConfirmWindow(
                    context.Window,
                    GetStopConstructionItems(view)
                );
                break;
            case StrategyContextMenuActions.Destination:
                BeginContextTargeting(context, strategyCommand.Action);
                break;
            case StrategyContextMenuActions.Encyclopedia:
                actions.OpenFacilityInfo(GetStatusTarget(view));
                break;
            case StrategyContextMenuActions.Status:
                actions.OpenFacilityStatus(GetStatusTarget(view));
                break;
            case StrategyContextMenuActions.Scrap:
                confirmationActions.OpenScrapConfirmWindow(context.Window, GetScrapItems(view));
                break;
        }
    }

    /// <summary>
    /// Handles context-menu cancellation without changing facility state.
    /// </summary>
    /// <param name="request">The canceled context-menu request.</param>
    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    /// <summary>
    /// Begins destination targeting for a facility manufacturing lane.
    /// </summary>
    /// <param name="context">The active context-menu invocation.</param>
    /// <param name="action">The selected context-menu action.</param>
    private void BeginContextTargeting(StrategyContextMenuProviderContext context, int action)
    {
        if (
            action != StrategyContextMenuActions.Destination
            || context?.Window == null
            || !windowManager.TryGetWindowView(context.Window, out FacilityWindowView view)
        )
            return;

        FacilityWindowTab? manufacturingTab = GetContextManufacturingTab(view);
        if (!manufacturingTab.HasValue)
            return;

        targetingController.Begin(
            new TargetingRequest(
                StrategyWindowTargetingSource.GetPrompt(action),
                new FacilityDestinationTargetingSource(
                    context.Window,
                    view,
                    manufacturingTab.Value
                ),
                this
            ),
            context.X,
            context.Y
        );
    }

    /// <summary>
    /// Applies a selected manufacturing destination.
    /// </summary>
    /// <param name="request">The completed targeting request.</param>
    /// <param name="target">The selected strategy target.</param>
    public void OnTargetSelected(TargetingRequest request, object target)
    {
        if (
            request?.Source is not FacilityDestinationTargetingSource source
            || target is not StrategyMissionTarget missionTarget
        )
            return;

        SetManufacturingDestination(source, missionTarget);
    }

    /// <summary>
    /// Handles cancellation without changing facility state.
    /// </summary>
    /// <param name="request">The cancelled targeting request.</param>
    public void OnTargetingCancelled(TargetingRequest request) { }

    /// <summary>
    /// Gets the selected manufacturing facility tab for a facility view.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <returns>The selected manufacturing facility tab, or null.</returns>
    public FacilityWindowTab? GetSelectedManufacturingTab(FacilityWindowView view)
    {
        return TryGetSession(view, out FacilityWindowSession session)
            ? session.GetSelectedManufacturingTab()
            : null;
    }

    /// <summary>
    /// Gets the context manufacturing facility tab for a facility view.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <returns>The context or selected manufacturing facility tab, or null.</returns>
    public FacilityWindowTab? GetContextManufacturingTab(FacilityWindowView view)
    {
        return TryGetSession(view, out FacilityWindowSession session)
            ? session.GetContextManufacturingTab()
            : null;
    }

    /// <summary>
    /// Gets the manufacturing category targeted by the active facility context.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <param name="type">Receives the manufacturing category.</param>
    /// <returns>True when a manufacturing lane is targeted.</returns>
    public bool TryGetContextManufacturingType(FacilityWindowView view, out ManufacturingType type)
    {
        FacilityWindowTab? manufacturingTab = GetContextManufacturingTab(view);
        ManufacturingType? selected = manufacturingTab.HasValue
            ? ConstructionOrderController.GetManufacturingType(manufacturingTab.Value)
            : null;
        type = selected ?? ManufacturingType.None;
        return selected.HasValue;
    }

    /// <summary>
    /// Opens construction for the manufacturing lane targeted by a facility window.
    /// </summary>
    /// <param name="window">The source facility window.</param>
    public void OpenConstruction(UIWindow window)
    {
        if (!windowManager.TryGetWindowView(window, out FacilityWindowView view))
            return;

        FacilityWindowTab? manufacturingTab = GetContextManufacturingTab(view);
        if (
            !manufacturingTab.HasValue
            || !TryGetConstructionDestinationIds(
                view,
                manufacturingTab.Value,
                out string destinationPlanetId,
                out string destinationItemId
            )
        )
        {
            return;
        }

        constructionWindowController.OpenFromFacility(
            GetPlanet(view),
            window,
            manufacturingTab.Value,
            destinationPlanetId,
            destinationItemId
        );
    }

    /// <summary>
    /// Gets construction destination identifiers for one manufacturing facility tab.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <param name="destinationPlanetId">Receives the destination planet identifier.</param>
    /// <param name="destinationItemId">Receives the destination entity identifier.</param>
    /// <returns>True when the panel maps to a manufacturing category.</returns>
    public bool TryGetConstructionDestinationIds(
        FacilityWindowView view,
        FacilityWindowTab manufacturingTab,
        out string destinationPlanetId,
        out string destinationItemId
    )
    {
        destinationPlanetId = null;
        destinationItemId = null;
        if (!TryGetSession(view, out FacilityWindowSession session))
            return false;

        ManufacturingType? type = ConstructionOrderController.GetManufacturingType(
            manufacturingTab
        );
        if (!type.HasValue)
            return false;

        session.GetDestination(type.Value, out destinationPlanetId, out destinationItemId);
        return true;
    }

    /// <summary>
    /// Gets the status target represented by the current facility selection.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <returns>The current status target, or null.</returns>
    public StrategyStatusTarget GetStatusTarget(FacilityWindowView view)
    {
        if (!TryGetSession(view, out FacilityWindowSession session))
            return null;

        GalaxyMapPlanet strategyPlanet = session.Planet;
        if (session.ActiveTab == FacilityWindowTab.Manufacturing)
        {
            FacilityWindowTab? manufacturingTab = session.GetContextManufacturingTab();
            ManufacturingType? type = manufacturingTab.HasValue
                ? ConstructionOrderController.GetManufacturingType(manufacturingTab.Value)
                : null;
            return type.HasValue ? new StrategyStatusTarget(strategyPlanet, null, type) : null;
        }

        Building building = session.GetStatusBuilding();
        return building == null ? null : new StrategyStatusTarget(strategyPlanet, building);
    }

    /// <summary>
    /// Gets facility items selected for scrapping.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <returns>The selected facility nodes in display order.</returns>
    public List<ISceneNode> GetScrapItems(FacilityWindowView view)
    {
        if (
            !TryGetSession(view, out FacilityWindowSession session)
            || session.ActiveTab == FacilityWindowTab.Manufacturing
        )
            return new List<ISceneNode>();

        return session.GetSelectedBuildings().Cast<ISceneNode>().ToList();
    }

    /// <summary>
    /// Gets queued or selected facility items whose construction can be stopped.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <returns>The construction items in their current display order.</returns>
    private List<ISceneNode> GetStopConstructionItems(FacilityWindowView view)
    {
        if (!TryGetSession(view, out FacilityWindowSession session))
            return new List<ISceneNode>();

        if (
            session.ActiveTab == FacilityWindowTab.Manufacturing
            && TryGetContextManufacturingType(view, out ManufacturingType type)
            && session
                .Planet?.Planet?.GetManufacturingQueue()
                .TryGetValue(type, out List<IManufacturable> queue) == true
        )
        {
            return queue.OfType<ISceneNode>().ToList();
        }

        return session
            .GetSelectedBuildings()
            .Where(building => building.GetManufacturingStatus() == ManufacturingStatus.Building)
            .Cast<ISceneNode>()
            .ToList();
    }

    /// <summary>
    /// Clears all selection and context state for a facility view.
    /// </summary>
    /// <param name="view">The facility view.</param>
    public void ClearSelection(FacilityWindowView view)
    {
        if (TryGetSession(view, out FacilityWindowSession session))
            session.ClearSelection();
    }

    /// <summary>
    /// Selects and displays a represented facility building.
    /// </summary>
    /// <param name="view">The destination facility view.</param>
    /// <param name="target">The represented building.</param>
    /// <returns>True when the building exists in a facility inventory tab.</returns>
    public bool SelectTarget(FacilityWindowView view, Building target)
    {
        if (target == null || !TryGetSession(view, out FacilityWindowSession session))
            return false;

        FacilityWindowTab? tab = GetFacilityTab(target.GetBuildingType());
        if (!tab.HasValue)
            return false;

        return session.SelectBuilding(tab.Value, target);
    }

    /// <summary>
    /// Creates facility context commands for the current tab and pointer target.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="session">The active facility session.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>The available context commands.</returns>
    private static List<StrategyMenuCommand> CreateContextCommands(
        Planet planet,
        FacilityWindowSession session,
        string playerFactionId
    )
    {
        return FacilityWindowContextMenuBuilder.Build(
            planet,
            session.ActiveTab,
            session.GetContextManufacturingTab(),
            session.GetContextBuilding(),
            playerFactionId
        );
    }

    /// <summary>
    /// Captures the card or inventory item under a context pointer event.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <param name="session">The active facility session.</param>
    /// <param name="eventData">The pointer event.</param>
    private static void CaptureContextTarget(
        FacilityWindowView view,
        FacilityWindowSession session,
        PointerEventData eventData
    )
    {
        if (
            view.TryGetManufacturingCardIndex(eventData, out int cardIndex)
            && GetManufacturingTab(cardIndex) is FacilityWindowTab manufacturingTab
        )
        {
            session.SelectManufacturingCardForContext(manufacturingTab, cardIndex);
            return;
        }

        if (view.TryGetInventoryItemIndex(eventData, out int itemIndex))
        {
            session.SelectBuildingForContext(itemIndex);
            return;
        }

        session.ClearContext();
    }

    /// <summary>
    /// Applies a successful targeted destination and updates related construction sessions.
    /// </summary>
    /// <param name="source">The destination targeting source.</param>
    /// <param name="target">The selected strategy target.</param>
    private void SetManufacturingDestination(
        FacilityDestinationTargetingSource source,
        StrategyMissionTarget target
    )
    {
        if (
            target?.Planet?.Planet == null
            || !TryGetSession(source.View, out FacilityWindowSession session)
        )
            return;

        ManufacturingType? type = ConstructionOrderController.GetManufacturingType(
            source.ManufacturingTab
        );
        string destinationPlanetId = target.Planet.Planet.InstanceID;
        if (!type.HasValue || string.IsNullOrEmpty(destinationPlanetId))
            return;

        string destinationItemId = target.Item?.GetInstanceID();
        session.SetDestination(type.Value, destinationPlanetId, destinationItemId);

        constructionWindowController.UpdateOpenConstructionDestination(
            source.Window,
            type.Value,
            destinationPlanetId,
            destinationItemId
        );
        markDirty();
    }

    /// <summary>
    /// Gets display names for the current manufacturing destinations.
    /// </summary>
    /// <param name="session">The facility session that owns the destinations.</param>
    /// <returns>The destination label for each configured manufacturing category.</returns>
    private Dictionary<ManufacturingType, string> GetManufacturingDestinationNames(
        FacilityWindowSession session
    )
    {
        Dictionary<ManufacturingType, string> names = new Dictionary<ManufacturingType, string>();
        if (session == null)
            return names;

        foreach (
            ManufacturingType type in new[]
            {
                ManufacturingType.Ship,
                ManufacturingType.Troop,
                ManufacturingType.Building,
            }
        )
        {
            session.GetDestination(type, out string planetId, out string itemId);
            if (!string.IsNullOrEmpty(itemId) && GetAuthoritativeNode(itemId) is ISceneNode item)
            {
                names[type] = item.GetDisplayName();
                continue;
            }

            Planet planet = GetAuthoritativePlanet(planetId);
            if (planet != null)
                names[type] = planet.GetDisplayName();
        }

        return names;
    }

    /// <summary>
    /// Handles a facility background click during strategy targeting.
    /// </summary>
    /// <param name="view">The clicked facility view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleBackgroundClicked(FacilityWindowView view, PointerEventData eventData)
    {
        TrySelectTarget(view, null);
    }

    /// <summary>
    /// Handles a manufacturing lane pointer press.
    /// </summary>
    /// <param name="view">The source facility view.</param>
    /// <param name="cardIndex">The manufacturing lane index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleManufacturingCardPressed(
        FacilityWindowView view,
        int cardIndex,
        PointerEventData eventData
    )
    {
        FacilityWindowTab? manufacturingTab = GetManufacturingTab(cardIndex);
        if (
            !manufacturingTab.HasValue
            || eventData == null
            || !TryGetSession(view, out FacilityWindowSession session)
            || session.ActiveTab != FacilityWindowTab.Manufacturing
            || targetingController.IsTargeting
                && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        session.CaptureManufacturingContext(manufacturingTab.Value);
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            session.SelectManufacturingCardForContext(manufacturingTab.Value, cardIndex);
            session.Window.RequestContext(eventData);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            session.Window.RequestFocus();
            session.SelectManufacturingCard(cardIndex, _manufacturingCardIndexLimit);
        }

        markDirty();
    }

    /// <summary>
    /// Handles a manufacturing lane release or targeting drop.
    /// </summary>
    /// <param name="view">The source facility view.</param>
    /// <param name="cardIndex">The manufacturing lane index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleManufacturingCardReleased(
        FacilityWindowView view,
        int cardIndex,
        PointerEventData eventData
    )
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            TrySelectTarget(view, null);
    }

    /// <summary>
    /// Handles an inventory item pointer press.
    /// </summary>
    /// <param name="view">The source facility view.</param>
    /// <param name="itemIndex">The inventory item index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleInventoryItemPressed(
        FacilityWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (
            !TryGetSession(view, out FacilityWindowSession session)
            || session.ActiveTab == FacilityWindowTab.Manufacturing
            || targetingController.IsTargeting
                && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        session.CaptureBuildingContext(itemIndex);
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            session.SelectBuildingForContext(itemIndex);
            session.Window.RequestContext(eventData);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            session.Window.RequestFocus();
            session.SelectBuilding(itemIndex, view.InventoryColumnCount);
        }

        markDirty();
    }

    /// <summary>
    /// Handles an inventory item release or targeting drop.
    /// </summary>
    /// <param name="view">The source facility view.</param>
    /// <param name="itemIndex">The inventory item index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleInventoryItemReleased(
        FacilityWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (
            eventData.button != PointerEventData.InputButton.Left
            || !TryGetSession(view, out FacilityWindowSession session)
        )
            return;

        TrySelectTarget(view, session.GetInventoryBuilding(itemIndex));
    }

    /// <summary>
    /// Opens status for a double-clicked inventory item.
    /// </summary>
    /// <param name="view">The source facility view.</param>
    /// <param name="itemIndex">The inventory item index.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleInventoryItemDoubleClicked(
        FacilityWindowView view,
        int itemIndex,
        PointerEventData eventData
    )
    {
        if (
            eventData.button == PointerEventData.InputButton.Left
            && !targetingController.IsTargeting
        )
            actions.OpenFacilityStatus(GetStatusTarget(view));
    }

    /// <summary>
    /// Changes the active facility tab and clears selection state.
    /// </summary>
    /// <param name="view">The source facility view.</param>
    /// <param name="tab">The selected facility tab.</param>
    private void HandleTabSelected(FacilityWindowView view, FacilityWindowTab tab)
    {
        if (
            !FacilityWindowRenderData.OrderedTabs.Contains(tab)
            || !TryGetSession(view, out FacilityWindowSession session)
        )
            return;

        session.SetActiveTab(tab);
        markDirty();
    }

    /// <summary>
    /// Releases subscriptions and state for a destroyed facility view.
    /// </summary>
    /// <param name="view">The destroyed facility view.</param>
    private void HandleViewDestroyed(FacilityWindowView view)
    {
        if (ReferenceEquals(view, null) || !boundViews.Remove(view))
            return;

        view.BackgroundClicked -= HandleBackgroundClicked;
        view.Destroyed -= HandleViewDestroyed;
        view.InventoryItemDoubleClicked -= HandleInventoryItemDoubleClicked;
        view.InventoryItemPressed -= HandleInventoryItemPressed;
        view.InventoryItemReleased -= HandleInventoryItemReleased;
        view.ManufacturingCardPressed -= HandleManufacturingCardPressed;
        view.ManufacturingCardReleased -= HandleManufacturingCardReleased;
        view.TabSelected -= HandleTabSelected;
        sessions.Remove(view);
    }

    /// <summary>
    /// Attempts to complete strategy targeting from a facility view.
    /// </summary>
    /// <param name="view">The source facility view.</param>
    /// <param name="item">The optional targeted inventory item.</param>
    /// <returns>True when an active targeting request accepted the target.</returns>
    private bool TrySelectTarget(FacilityWindowView view, ISceneNode item)
    {
        return targetingController.IsTargeting
            && TryGetSession(view, out FacilityWindowSession session)
            && session.Planet != null
            && targetingController.TrySelectTarget(new StrategyMissionTarget(session.Planet, item));
    }

    /// <summary>
    /// Maps a facility building type to its inventory tab.
    /// </summary>
    /// <param name="buildingType">The building type.</param>
    /// <returns>The matching inventory tab, or null.</returns>
    private static FacilityWindowTab? GetFacilityTab(BuildingType buildingType)
    {
        return buildingType switch
        {
            BuildingType.Shipyard => FacilityWindowTab.Shipyards,
            BuildingType.TrainingFacility => FacilityWindowTab.Training,
            BuildingType.ConstructionFacility => FacilityWindowTab.Construction,
            BuildingType.Refinery => FacilityWindowTab.Refineries,
            BuildingType.Mine => FacilityWindowTab.Mines,
            _ => null,
        };
    }

    /// <summary>
    /// Converts an authored manufacturing card index to its facility tab.
    /// </summary>
    /// <param name="cardIndex">The authored manufacturing card index.</param>
    /// <returns>The matching manufacturing facility tab, or null.</returns>
    private static FacilityWindowTab? GetManufacturingTab(int cardIndex)
    {
        FacilityWindowTab tab = (FacilityWindowTab)cardIndex;
        return ConstructionOrderController.GetManufacturingType(tab).HasValue ? tab : null;
    }

    /// <summary>
    /// Gets a bound facility session.
    /// </summary>
    /// <param name="view">The facility view.</param>
    /// <param name="session">Receives the bound session.</param>
    /// <returns>True when the view has a bound session.</returns>
    private bool TryGetSession(FacilityWindowView view, out FacilityWindowSession session)
    {
        if (view == null)
        {
            session = null;
            return false;
        }

        return sessions.TryGetValue(view, out session);
    }

    /// <summary>
    /// Returns the controller-owned session for an initialized facility view.
    /// </summary>
    /// <param name="view">The initialized facility view.</param>
    /// <returns>The session owned by the view.</returns>
    private FacilityWindowSession GetSession(FacilityWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out FacilityWindowSession session))
            return session;

        throw new InvalidOperationException($"{view.name} has not been initialized.");
    }

    /// <summary>
    /// Resolves an instance identifier to its authoritative game node.
    /// </summary>
    /// <param name="instanceId">The scene-node instance identifier.</param>
    /// <returns>The authoritative scene node, or null.</returns>
    private ISceneNode GetAuthoritativeNode(string instanceId)
    {
        return string.IsNullOrEmpty(instanceId)
            ? null
            : getGame()?.GetSceneNodeByInstanceID<ISceneNode>(instanceId);
    }

    /// <summary>
    /// Finds the facility window representing a planet.
    /// </summary>
    /// <param name="planetId">The represented planet identifier.</param>
    /// <returns>The matching facility view, or null when none is open.</returns>
    private FacilityWindowView FindWindow(string planetId)
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                windowManager.TryGetWindowView(window, out FacilityWindowView view)
                && sessions.TryGetValue(view, out FacilityWindowSession session)
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
    /// Resolves a planet identifier to its authoritative game node.
    /// </summary>
    /// <param name="planetId">The planet instance identifier.</param>
    /// <returns>The authoritative planet, or null.</returns>
    private Planet GetAuthoritativePlanet(string planetId)
    {
        return string.IsNullOrEmpty(planetId)
            ? null
            : getGame()?.GetSceneNodeByInstanceID<Planet>(planetId);
    }

    /// <summary>
    /// Verifies action routing before a facility view is bound.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null || confirmationActions == null)
            throw new InvalidOperationException(
                $"{nameof(FacilityWindowController)} must be initialized before use."
            );
    }

    /// <summary>
    /// Identifies the facility window and lane that began destination targeting.
    /// </summary>
    private sealed class FacilityDestinationTargetingSource
    {
        /// <summary>
        /// Creates a facility destination targeting source.
        /// </summary>
        /// <param name="window">The source facility window.</param>
        /// <param name="view">The source facility view.</param>
        /// <param name="manufacturingTab">The manufacturing facility tab.</param>
        public FacilityDestinationTargetingSource(
            UIWindow window,
            FacilityWindowView view,
            FacilityWindowTab manufacturingTab
        )
        {
            Window = window;
            View = view;
            ManufacturingTab = manufacturingTab;
        }

        /// <summary>
        /// Gets the window.
        /// </summary>
        public UIWindow Window { get; }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public FacilityWindowView View { get; }

        /// <summary>
        /// Gets the manufacturing facility tab.
        /// </summary>
        public FacilityWindowTab ManufacturingTab { get; }
    }
}
