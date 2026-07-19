using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Performs game-level window actions requested by the planet-system feature.
/// </summary>
public interface IPlanetSystemWindowActions
{
    /// <summary>
    /// Rebuilds shared strategy state after a planet-system command changes the game.
    /// </summary>
    void RefreshPlanetSystemState();

    /// <summary>
    /// Opens one planet icon window at a source-space position.
    /// </summary>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="icon">The requested planet icon.</param>
    /// <param name="sourceX">The source-space horizontal position.</param>
    /// <param name="sourceY">The source-space vertical position.</param>
    void OpenPlanetSystemPlanetWindow(
        GalaxyMapPlanet planet,
        PlanetIcon icon,
        int sourceX,
        int sourceY
    );

    /// <summary>
    /// Opens Encyclopedia information for the active planet-system target.
    /// </summary>
    /// <param name="target">The resolved information target.</param>
    void OpenPlanetSystemInfo(StrategyStatusTarget target);

    /// <summary>
    /// Opens status information for the active planet-system target.
    /// </summary>
    /// <param name="target">The resolved status target.</param>
    void OpenPlanetSystemStatus(StrategyStatusTarget target);
}

/// <summary>
/// Owns planet-system window sessions, selection, targeting, and semantic command routing.
/// </summary>
public sealed class PlanetSystemWindowController
    : IStrategyContextMenuProvider,
        IContextMenuReceiver,
        ITargetingReceiver
{
    private static readonly int[] _sectorWindowPositionOrder =
    {
        SectorWindowPositions.Left,
        SectorWindowPositions.Middle,
        SectorWindowPositions.Right,
    };

    private readonly HashSet<PlanetSystemWindowView> boundViews =
        new HashSet<PlanetSystemWindowView>();
    private readonly Action<UIWindow, bool> closeWindow;
    private readonly Func<IReadOnlyList<GalaxyMapSector>> getSectors;
    private readonly Func<UIContext> getUIContext;
    private readonly Func<int, Vector2Int> getWindowPosition;
    private readonly StrategyFleetCommandController fleetCommandController;
    private readonly Action markDirty;
    private readonly PlanetSystemWindowProjector projector;
    private readonly Dictionary<PlanetSystemWindowView, PlanetSystemWindowSession> sessions =
        new Dictionary<PlanetSystemWindowView, PlanetSystemWindowSession>();
    private readonly TargetingController targetingController;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;

    private IPlanetSystemWindowActions actions;
    private IStrategyWindowCommandActions commandActions;
    private IStrategyConfirmationActions confirmationActions;
    private Action<UIWindow, int, int> startItemDrag;

    /// <summary>
    /// Creates a planet-system feature controller.
    /// </summary>
    /// <param name="fleetCommandController">Executes shared fleet mutations.</param>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="targetingController">Owns the active strategy targeting request.</param>
    /// <param name="windowLayer">Provides the authored planet-system prefab and modeless window layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getSectors">Returns the current visible galaxy sectors.</param>
    /// <param name="getWindowPosition">Returns the authored position for a sector slot.</param>
    /// <param name="closeWindow">Closes a registered window through the screen lifecycle.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public PlanetSystemWindowController(
        StrategyFleetCommandController fleetCommandController,
        Func<UIContext> getUIContext,
        TargetingController targetingController,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<IReadOnlyList<GalaxyMapSector>> getSectors,
        Func<int, Vector2Int> getWindowPosition,
        Action<UIWindow, bool> closeWindow,
        Action markDirty
    )
    {
        this.fleetCommandController =
            fleetCommandController
            ?? throw new ArgumentNullException(nameof(fleetCommandController));
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.targetingController =
            targetingController ?? throw new ArgumentNullException(nameof(targetingController));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getSectors = getSectors ?? throw new ArgumentNullException(nameof(getSectors));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        projector = new PlanetSystemWindowProjector(getUIContext);
    }

    /// <summary>
    /// Supplies planet-system command and interaction actions.
    /// </summary>
    /// <param name="windowActions">The feature-specific planet-system actions.</param>
    /// <param name="windowCommandActions">The shared mission and movement actions.</param>
    /// <param name="windowConfirmationActions">The shared confirmation actions.</param>
    /// <param name="beginItemDrag">Begins a strategy item-drag candidate.</param>
    public void Initialize(
        IPlanetSystemWindowActions windowActions,
        IStrategyWindowCommandActions windowCommandActions,
        IStrategyConfirmationActions windowConfirmationActions,
        Action<UIWindow, int, int> beginItemDrag
    )
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
        commandActions =
            windowCommandActions ?? throw new ArgumentNullException(nameof(windowCommandActions));
        confirmationActions =
            windowConfirmationActions
            ?? throw new ArgumentNullException(nameof(windowConfirmationActions));
        startItemDrag = beginItemDrag ?? throw new ArgumentNullException(nameof(beginItemDrag));
    }

    /// <summary>
    /// Binds one planet-system view to a window session exactly once.
    /// </summary>
    /// <param name="view">The planet-system view to bind.</param>
    /// <param name="window">The owning window shell.</param>
    public void BindWindow(PlanetSystemWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        EnsureInitialized();
        if (sessions.TryGetValue(view, out PlanetSystemWindowSession existing))
        {
            if (!ReferenceEquals(existing.Window, window))
                throw new InvalidOperationException(
                    "A planet-system view cannot be rebound to a different window."
                );
            return;
        }

        boundViews.Add(view);
        sessions.Add(view, new PlanetSystemWindowSession(window));
        view.Clicked += HandlePlanetClicked;
        view.Destroyed += HandleViewDestroyed;
        view.HoverCleared += HandlePlanetHoverCleared;
        view.Hovered += HandlePlanetHovered;
        view.Pressed += HandlePlanetPressed;
        view.Released += HandlePlanetReleased;
    }

    /// <summary>
    /// Starts a controller-owned session for one planet-system window.
    /// </summary>
    /// <param name="view">The planet-system view.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="sector">The represented strategy sector.</param>
    /// <param name="sectorPosition">The authored sector-window position slot.</param>
    /// <returns>True when the session was initialized.</returns>
    public bool TryInitializeWindow(
        PlanetSystemWindowView view,
        UIWindow window,
        GalaxyMapSector sector,
        int sectorPosition
    )
    {
        if (view == null || window == null || sector == null)
            return false;

        BindWindow(view, window);
        sessions[view].Initialize(sector, sectorPosition);
        return true;
    }

    /// <summary>
    /// Opens the sector window containing a planetary system.
    /// </summary>
    /// <param name="system">The planetary system to display.</param>
    /// <returns>True when a new sector window was opened.</returns>
    public bool Open(PlanetSystem system)
    {
        if (system == null)
            return false;

        GalaxyMapSector sector = getSectors().FirstOrDefault(item => item.System == system);
        return Open(sector);
    }

    /// <summary>
    /// Opens a sector in the next authored planet-system window slot.
    /// </summary>
    /// <param name="sector">The sector to display.</param>
    /// <returns>True when a new sector window was opened.</returns>
    public bool Open(GalaxyMapSector sector)
    {
        if (sector == null || FindWindow(sector) != null)
            return false;

        OpenAt(sector, GetTargetPosition());
        return true;
    }

    /// <summary>
    /// Renders every registered planet-system window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out PlanetSystemWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Rebinds planet-system sessions to a refreshed galaxy snapshot.
    /// </summary>
    /// <param name="sectors">The refreshed visible sectors.</param>
    public void ReconcileWindows(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            return;

        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                !windowManager.TryGetWindowView(window, out PlanetSystemWindowView view)
                || !sessions.TryGetValue(view, out PlanetSystemWindowSession session)
            )
                continue;

            GalaxyMapSector sector = FindFreshSector(session.Sector, sectors);
            if (sector != null)
                session.ReconcileSector(sector);
        }
    }

    /// <summary>
    /// Moves a planet-system window to the next authored slot.
    /// </summary>
    /// <param name="window">The registered planet-system window to move.</param>
    public void Swap(UIWindow window)
    {
        if (
            !windowManager.TryGetWindowView(window, out PlanetSystemWindowView view)
            || !sessions.TryGetValue(view, out PlanetSystemWindowSession session)
        )
            return;

        int target = GetNextPosition(session.SectorPosition);
        UIWindow existing = FindWindow(target);
        if (existing != null)
            closeWindow(existing, true);

        Vector2Int position = getWindowPosition(target);
        session.SelectSectorPosition(target);
        window.MoveTo(position.x, position.y);
        markDirty();
    }

    /// <summary>
    /// Finds the registered planet-system window representing a sector.
    /// </summary>
    /// <param name="sector">The represented sector.</param>
    /// <returns>The matching registered window, or null.</returns>
    public UIWindow FindWindow(GalaxyMapSector sector)
    {
        string systemId = sector?.System?.InstanceID;
        if (string.IsNullOrEmpty(systemId))
            return null;

        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                windowManager.TryGetWindowView(window, out PlanetSystemWindowView view)
                && sessions.TryGetValue(view, out PlanetSystemWindowSession session)
                && string.Equals(
                    session.Sector?.System?.InstanceID,
                    systemId,
                    StringComparison.Ordinal
                )
            )
                return window;
        }

        return null;
    }

    /// <summary>
    /// Gets the strategy sector owned by one planet-system session.
    /// </summary>
    /// <param name="view">The planet-system view.</param>
    /// <returns>The represented sector, or null.</returns>
    public GalaxyMapSector GetSector(PlanetSystemWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out PlanetSystemWindowSession session)
            ? session.Sector
            : null;
    }

    /// <summary>
    /// Gets the authored position slot owned by one planet-system session.
    /// </summary>
    /// <param name="view">The planet-system view.</param>
    /// <returns>The position slot, or -1 when no session exists.</returns>
    public int GetSectorPosition(PlanetSystemWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out PlanetSystemWindowSession session)
            ? session.SectorPosition
            : -1;
    }

    /// <summary>
    /// Replaces the sector owned by one planet-system session.
    /// </summary>
    /// <param name="view">The planet-system view.</param>
    /// <param name="sector">The replacement strategy sector.</param>
    public void ReconcileWindow(PlanetSystemWindowView view, GalaxyMapSector sector)
    {
        if (
            view != null
            && sector != null
            && sessions.TryGetValue(view, out PlanetSystemWindowSession session)
        )
            session.ReconcileSector(sector);
    }

    /// <summary>
    /// Changes the authored position slot owned by one planet-system session.
    /// </summary>
    /// <param name="view">The planet-system view.</param>
    /// <param name="sectorPosition">The replacement position slot.</param>
    public void SetSectorPosition(PlanetSystemWindowView view, int sectorPosition)
    {
        if (view != null && sessions.TryGetValue(view, out PlanetSystemWindowSession session))
            session.SelectSectorPosition(sectorPosition);
    }

    /// <summary>
    /// Projects and renders one planet-system window.
    /// </summary>
    /// <param name="view">The destination planet-system view.</param>
    /// <param name="window">The owning window shell.</param>
    public void RenderWindow(PlanetSystemWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));
        PlanetSystemWindowSession session = GetSession(view);
        if (session.Sector == null)
            return;

        view.Render(
            projector.CreateRenderData(
                session.Sector,
                session.SelectedPlanetInstanceId,
                session.SelectedIcon,
                session.HoveredPlanetInstanceId,
                session.HoveredIcon
            )
        );
    }

    /// <summary>
    /// Builds the context commands available at a planet-system pointer target.
    /// </summary>
    /// <param name="context">The active context-menu invocation.</param>
    /// <param name="request">Receives the completed command request.</param>
    /// <param name="width">Receives the authored menu width.</param>
    /// <returns>True when a planet-system context menu was produced.</returns>
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
            || !windowManager.TryGetWindowView(context.Window, out PlanetSystemWindowView view)
        )
            return false;

        PlanetSystemWindowSession session = GetSession(view);
        CaptureContextTarget(view, session, context.EventData);
        PlanetSystemWindowHit hit = session.GetContextHit();
        List<ISceneNode> items = GetPlayerFleetItems(hit?.Planet);
        List<StrategyMenuCommand> commands = PlanetSystemWindowContextMenuBuilder.Create(
            hit,
            items,
            GetUIContext().GetPlayerFactionInstanceID()
        );
        if (commands.Count == 0)
            return false;

        PlanetSystemContextMenuSource source = new PlanetSystemContextMenuSource(
            context.Window,
            context.X,
            context.Y,
            items,
            GetStatusTarget(view)
        );
        request = new ContextMenuRequest(
            source,
            commands.Cast<IContextMenuCommand>().ToList(),
            this
        );
        width = context.Layout.PlanetSystemMenuWidth;
        return true;
    }

    /// <summary>
    /// Routes one selected planet-system context command.
    /// </summary>
    /// <param name="request">The completed context-menu request.</param>
    /// <param name="command">The selected context-menu command.</param>
    public void OnContextMenuCommandSelected(
        ContextMenuRequest request,
        IContextMenuCommand command
    )
    {
        if (
            request?.Source is not PlanetSystemContextMenuSource source
            || command is not StrategyMenuCommand strategyCommand
        )
            return;

        switch (strategyCommand.Action)
        {
            case StrategyContextMenuActions.Encyclopedia:
                actions.OpenPlanetSystemInfo(source.Target);
                break;
            case StrategyContextMenuActions.Status:
                actions.OpenPlanetSystemStatus(source.Target);
                break;
            case StrategyContextMenuActions.Scrap:
                confirmationActions.OpenScrapConfirmWindow(source.Window, source.Items);
                break;
            case StrategyContextMenuActions.PlanetaryBombardment:
                if (windowManager.TryGetWindowView(source.Window, out PlanetSystemWindowView view))
                    TryExecutePlanetaryBombardment(view);
                break;
            case StrategyContextMenuActions.CreateMission:
            case StrategyContextMenuActions.Move:
            case StrategyContextMenuActions.MoveConfirm:
                BeginContextTargeting(source, strategyCommand.Action);
                break;
        }
    }

    /// <summary>
    /// Handles context-menu cancellation without changing planet-system state.
    /// </summary>
    /// <param name="request">The canceled context-menu request.</param>
    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    /// <summary>
    /// Begins targeting for a planet-system context command.
    /// </summary>
    /// <param name="source">The immutable planet-system context selection.</param>
    /// <param name="action">The selected context-menu action.</param>
    private void BeginContextTargeting(PlanetSystemContextMenuSource source, int action)
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
    /// Routes a completed planet-system context target.
    /// </summary>
    /// <param name="request">The completed targeting request.</param>
    /// <param name="target">The selected strategy target.</param>
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
    /// Handles cancellation without changing planet-system state.
    /// </summary>
    /// <param name="request">The cancelled targeting request.</param>
    public void OnTargetingCancelled(TargetingRequest request) { }

    /// <summary>
    /// Gets the status target represented by a planet-system selection.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <returns>The selected status target, or null.</returns>
    public StrategyStatusTarget GetStatusTarget(PlanetSystemWindowView view)
    {
        if (!sessions.TryGetValue(view, out PlanetSystemWindowSession session))
            return null;

        PlanetSystemWindowHit hit = session.GetContextHit() ?? session.GetSelectedHit();
        if (hit?.Icon == PlanetIcon.Fleet)
        {
            List<ISceneNode> fleetItems = GetPlayerFleetItems(hit.Planet);
            return fleetItems.Count == 1
                ? new StrategyStatusTarget(hit.GalaxyMapPlanet, fleetItems[0])
                : null;
        }

        return hit?.GalaxyMapPlanet == null
            ? null
            : new StrategyStatusTarget(hit.GalaxyMapPlanet, hit.Planet);
    }

    /// <summary>
    /// Gets the player-controlled fleet items represented by a planet-system selection.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <returns>The selected fleet items.</returns>
    public List<ISceneNode> GetContextItems(PlanetSystemWindowView view)
    {
        if (!sessions.TryGetValue(view, out PlanetSystemWindowSession session))
            return new List<ISceneNode>();

        PlanetSystemWindowHit hit = session.GetContextHit() ?? session.GetSelectedHit();
        return hit?.Icon == PlanetIcon.Fleet
            ? GetPlayerFleetItems(hit.Planet)
            : new List<ISceneNode>();
    }

    /// <summary>
    /// Executes planetary bombardment for the selected fleet overlay.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <returns>True when bombardment was executed.</returns>
    public bool TryExecutePlanetaryBombardment(PlanetSystemWindowView view)
    {
        if (!sessions.TryGetValue(view, out PlanetSystemWindowSession session))
            return false;

        PlanetSystemWindowHit hit = session.GetContextHit() ?? session.GetSelectedHit();
        List<ISceneNode> items =
            hit?.Icon == PlanetIcon.Fleet
                ? GetPlayerFleetItems(hit.Planet)
                : new List<ISceneNode>();
        if (!fleetCommandController.TryExecutePlanetaryBombardment(items, hit?.Planet))
            return false;

        session.ClearSelection();
        actions.RefreshPlanetSystemState();
        return true;
    }

    /// <summary>
    /// Clears selection and context state for one planet-system window.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    public void ClearSelection(PlanetSystemWindowView view)
    {
        if (sessions.TryGetValue(view, out PlanetSystemWindowSession session))
            session.ClearSelection();
    }

    /// <summary>
    /// Tries to create a fleet drag preview for one planet-system window.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    /// <param name="preview">Receives the drag preview.</param>
    /// <returns>True when the current fleet selection produced a preview.</returns>
    public bool TryGetDragPreview(
        PlanetSystemWindowView view,
        int sourceX,
        int sourceY,
        out DragPreview preview
    )
    {
        preview = null;
        if (!sessions.TryGetValue(view, out PlanetSystemWindowSession session))
            return false;

        PlanetSystemWindowHit hit = session.GetContextHit() ?? session.GetSelectedHit();
        return hit?.Icon == PlanetIcon.Fleet
            && GetPlayerFleetItems(hit.Planet).Count > 0
            && view.TryGetFleetDragPreview(
                hit.Element,
                session.Window.X,
                session.Window.Y,
                sourceX,
                sourceY,
                out preview
            );
    }

    /// <summary>
    /// Creates a targeting result from one semantic planet-system hit.
    /// </summary>
    /// <param name="hit">The semantic planet-system hit.</param>
    /// <param name="request">The active targeting request.</param>
    /// <param name="fleetTarget">The player-controlled fleet target.</param>
    /// <returns>The strategy mission target, or null.</returns>
    internal static StrategyMissionTarget CreateTargetForHit(
        PlanetSystemWindowHit hit,
        TargetingRequest request,
        ISceneNode fleetTarget
    )
    {
        if (hit?.GalaxyMapPlanet == null)
            return null;
        if (hit.Icon == PlanetIcon.Fleet && IsMoveTargetingRequest(request))
            return new StrategyMissionTarget(hit.GalaxyMapPlanet, fleetTarget);
        if (hit.Icon != PlanetIcon.None || hit.PlanetImage)
            return new StrategyMissionTarget(hit.GalaxyMapPlanet, null);
        return null;
    }

    /// <summary>
    /// Captures the semantic planet target beneath a context pointer.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <param name="session">The active planet-system session.</param>
    /// <param name="eventData">The pointer event.</param>
    private static void CaptureContextTarget(
        PlanetSystemWindowView view,
        PlanetSystemWindowSession session,
        PointerEventData eventData
    )
    {
        if (
            view.TryCreateElement(eventData, out PlanetSystemWindowElement element)
            && session.ResolveHit(element) is PlanetSystemWindowHit hit
        )
        {
            session.StoreContextHit(hit);
            session.SelectHit(hit);
            return;
        }

        session.ClearContextHit();
    }

    /// <summary>
    /// Handles a semantic planet double click.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <param name="element">The semantic presentation element.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePlanetClicked(
        PlanetSystemWindowView view,
        PlanetSystemWindowElement element,
        PointerEventData eventData
    )
    {
        PlanetSystemWindowHit hit = ResolveHit(view, element);
        if (
            targetingController.IsTargeting
            || hit?.Icon == PlanetIcon.None
            || !TryGetDesktopPosition(view, eventData, out int x, out int y)
        )
            return;

        actions.OpenPlanetSystemPlanetWindow(hit.GalaxyMapPlanet, hit.Icon, x, y);
    }

    /// <summary>
    /// Clears planet-system hover state.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    private void HandlePlanetHoverCleared(PlanetSystemWindowView view)
    {
        if (
            sessions.TryGetValue(view, out PlanetSystemWindowSession session)
            && session.ClearHover()
        )
            markDirty();
    }

    /// <summary>
    /// Updates planet-system hover state.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <param name="element">The semantic presentation element.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePlanetHovered(
        PlanetSystemWindowView view,
        PlanetSystemWindowElement element,
        PointerEventData eventData
    )
    {
        PlanetSystemWindowHit hit = ResolveHit(view, element);
        if (
            sessions.TryGetValue(view, out PlanetSystemWindowSession session)
            && session.SetHover(hit)
        )
            markDirty();
    }

    /// <summary>
    /// Updates selection and begins a fleet drag candidate when appropriate.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <param name="element">The semantic presentation element.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePlanetPressed(
        PlanetSystemWindowView view,
        PlanetSystemWindowElement element,
        PointerEventData eventData
    )
    {
        PlanetSystemWindowHit hit = ResolveHit(view, element);
        if (
            !sessions.TryGetValue(view, out PlanetSystemWindowSession session)
            || hit == null
            || eventData == null
        )
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
            session.Window.RequestFocus();
        if (
            targetingController.IsTargeting
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        session.StoreContextHit(hit);
        bool selected = session.SelectHit(hit);
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            session.Window.RequestContext(eventData);
            markDirty();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (
            selected
            && hit.Icon == PlanetIcon.Fleet
            && TryGetDesktopPosition(view, eventData, out int x, out int y)
        )
            startItemDrag(session.Window, x, y);
        else
            markDirty();
    }

    /// <summary>
    /// Tries to complete the active targeting request from a planet release or drop.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <param name="element">The semantic presentation element.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePlanetReleased(
        PlanetSystemWindowView view,
        PlanetSystemWindowElement element,
        PointerEventData eventData
    )
    {
        PlanetSystemWindowHit hit = ResolveHit(view, element);
        if (!targetingController.IsTargeting || hit == null)
            return;

        StrategyMissionTarget target = CreateTargetForHit(
            hit,
            targetingController.ActiveRequest,
            GetPlayerFleetTarget(hit.Planet)
        );
        if (target != null)
            targetingController.TrySelectTarget(target);
    }

    /// <summary>
    /// Releases subscriptions and state for a destroyed planet-system view.
    /// </summary>
    /// <param name="view">The destroyed planet-system view.</param>
    private void HandleViewDestroyed(PlanetSystemWindowView view)
    {
        if (ReferenceEquals(view, null) || !boundViews.Remove(view))
            return;

        view.Clicked -= HandlePlanetClicked;
        view.Destroyed -= HandleViewDestroyed;
        view.HoverCleared -= HandlePlanetHoverCleared;
        view.Hovered -= HandlePlanetHovered;
        view.Pressed -= HandlePlanetPressed;
        view.Released -= HandlePlanetReleased;
        sessions.Remove(view);
    }

    /// <summary>
    /// Converts a pointer event into source-space desktop coordinates.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="x">Receives the horizontal desktop coordinate.</param>
    /// <param name="y">Receives the vertical desktop coordinate.</param>
    /// <returns>True when the source window resolved the pointer position.</returns>
    private bool TryGetDesktopPosition(
        PlanetSystemWindowView view,
        PointerEventData eventData,
        out int x,
        out int y
    )
    {
        x = 0;
        y = 0;
        return eventData != null
            && sessions.TryGetValue(view, out PlanetSystemWindowSession session)
            && session.Window.TryGetDesktopPosition(eventData, eventData.position, out x, out y);
    }

    /// <summary>
    /// Resolves a presentation element against one view's controller-owned session.
    /// </summary>
    /// <param name="view">The source planet-system view.</param>
    /// <param name="element">The selected presentation element.</param>
    /// <returns>The resolved strategy hit, or null.</returns>
    private PlanetSystemWindowHit ResolveHit(
        PlanetSystemWindowView view,
        PlanetSystemWindowElement element
    )
    {
        return view != null && sessions.TryGetValue(view, out PlanetSystemWindowSession session)
            ? session.ResolveHit(element)
            : null;
    }

    /// <summary>
    /// Gets the first player-controlled fleet at one planet.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <returns>The player-controlled fleet, or null.</returns>
    private Fleet GetPlayerFleetTarget(Planet planet)
    {
        string playerFactionId = GetUIContext().GetPlayerFactionInstanceID();
        return planet?.Fleets.FirstOrDefault(fleet =>
            StrategyContextMenuAvailability.PlayerControlsItem(fleet, playerFactionId)
        );
    }

    /// <summary>
    /// Gets all player-controlled fleets at one planet.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <returns>The player-controlled fleet nodes.</returns>
    private List<ISceneNode> GetPlayerFleetItems(Planet planet)
    {
        string playerFactionId = GetUIContext().GetPlayerFactionInstanceID();
        return planet
                ?.Fleets.Cast<ISceneNode>()
                .Where(fleet =>
                    StrategyContextMenuAvailability.PlayerControlsItem(fleet, playerFactionId)
                )
                .ToList()
            ?? new List<ISceneNode>();
    }

    /// <summary>
    /// Determines whether the current request needs a concrete fleet target.
    /// </summary>
    /// <param name="request">The active targeting request.</param>
    /// <returns>True for immediate and confirmed move requests.</returns>
    private static bool IsMoveTargetingRequest(TargetingRequest request)
    {
        return request?.Source is StrategyWindowTargetingSource source
            && source.Action
                is StrategyContextMenuActions.Move
                    or StrategyContextMenuActions.MoveConfirm;
    }

    /// <summary>
    /// Opens a sector window in a specific authored slot.
    /// </summary>
    /// <param name="sector">The sector to display.</param>
    /// <param name="target">The authored sector position.</param>
    private void OpenAt(GalaxyMapSector sector, int target)
    {
        UIWindow existing = FindWindow(target);
        if (existing != null)
            closeWindow(existing, true);

        Vector2Int position = getWindowPosition(target);
        UIWindow window = windowManager.CreateWindow(
            windowLayer.PlanetSystemWindowPrefab,
            windowLayer.GetWindowParent(false),
            $"PlanetSystemWindow-{sector.System.GetDisplayName()}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.PlanetSystemWindowPrefab),
            false,
            false,
            false,
            true,
            out PlanetSystemWindowView view
        );
        if (!TryInitializeWindow(view, window, sector, target))
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Finds the registered planet-system window occupying an authored slot.
    /// </summary>
    /// <param name="position">The authored sector position.</param>
    /// <returns>The matching registered window, or null.</returns>
    private UIWindow FindWindow(int position)
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                windowManager.TryGetWindowView(window, out PlanetSystemWindowView view)
                && sessions.TryGetValue(view, out PlanetSystemWindowSession session)
                && session.SectorPosition == position
            )
                return window;
        }

        return null;
    }

    /// <summary>
    /// Selects the first available authored sector slot, replacing the first slot when full.
    /// </summary>
    /// <returns>The selected authored sector position.</returns>
    private int GetTargetPosition()
    {
        foreach (int position in _sectorWindowPositionOrder)
        {
            if (FindWindow(position) == null)
                return position;
        }

        return _sectorWindowPositionOrder[0];
    }

    /// <summary>
    /// Gets the authored sector slot following a current position.
    /// </summary>
    /// <param name="position">The current authored sector position.</param>
    /// <returns>The next authored sector position.</returns>
    private static int GetNextPosition(int position)
    {
        int index = Array.IndexOf(_sectorWindowPositionOrder, position);
        if (index < 0)
            return _sectorWindowPositionOrder[0];

        return _sectorWindowPositionOrder[(index + 1) % _sectorWindowPositionOrder.Length];
    }

    /// <summary>
    /// Resolves a projected sector against a refreshed sector collection.
    /// </summary>
    /// <param name="sector">The previous projected sector.</param>
    /// <param name="sectors">The refreshed visible sectors.</param>
    /// <returns>The refreshed sector, or null when it is no longer represented.</returns>
    private static GalaxyMapSector FindFreshSector(
        GalaxyMapSector sector,
        IReadOnlyList<GalaxyMapSector> sectors
    )
    {
        string systemId = sector?.System?.InstanceID;
        return systemId == null
            ? null
            : sectors.FirstOrDefault(item => item.System?.InstanceID == systemId);
    }

    /// <summary>
    /// Gets the current strategy presentation context.
    /// </summary>
    /// <returns>The current presentation context.</returns>
    private UIContext GetUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("The strategy UI context is unavailable.");
    }

    /// <summary>
    /// Gets the controller-owned session for an initialized planet-system view.
    /// </summary>
    /// <param name="view">The initialized planet-system view.</param>
    /// <returns>The controller-owned session.</returns>
    private PlanetSystemWindowSession GetSession(PlanetSystemWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (!sessions.TryGetValue(view, out PlanetSystemWindowSession session))
        {
            throw new InvalidOperationException(
                "The planet-system view has not been initialized by this controller."
            );
        }

        return session;
    }

    /// <summary>
    /// Verifies that command and interaction routing is available.
    /// </summary>
    private void EnsureInitialized()
    {
        if (
            actions == null
            || commandActions == null
            || confirmationActions == null
            || startItemDrag == null
        )
        {
            throw new InvalidOperationException(
                $"{nameof(PlanetSystemWindowController)} must be initialized before use."
            );
        }
    }

    /// <summary>
    /// Captures immutable command state for one open planet-system context menu.
    /// </summary>
    private sealed class PlanetSystemContextMenuSource : IStrategyContextMenuSource
    {
        /// <summary>
        /// Creates one planet-system context-menu source snapshot.
        /// </summary>
        /// <param name="window">The source planet-system window.</param>
        /// <param name="hotspotX">The menu hotspot horizontal coordinate.</param>
        /// <param name="hotspotY">The menu hotspot vertical coordinate.</param>
        /// <param name="items">The selected fleet items.</param>
        /// <param name="target">The selected status target.</param>
        public PlanetSystemContextMenuSource(
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

        public int HotspotX { get; }

        public int HotspotY { get; }

        public IReadOnlyList<ISceneNode> Items { get; }

        public StrategyStatusTarget Target { get; }

        public UIWindow Window { get; }
    }

    /// <summary>
    /// Contains controller-owned state for one planet-system window.
    /// </summary>
    private sealed class PlanetSystemWindowSession
    {
        private PlanetIcon contextIcon;
        private bool contextPlanetImage;
        private string contextPlanetInstanceId;

        /// <summary>
        /// Creates a planet-system session for one window shell.
        /// </summary>
        /// <param name="window">The owning window shell.</param>
        public PlanetSystemWindowSession(UIWindow window)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public PlanetIcon HoveredIcon { get; private set; }

        public string HoveredPlanetInstanceId { get; private set; }

        public GalaxyMapSector Sector { get; private set; }

        public PlanetIcon SelectedIcon { get; private set; }

        public string SelectedPlanetInstanceId { get; private set; }

        public int SectorPosition { get; private set; }

        public UIWindow Window { get; }

        /// <summary>
        /// Initializes the strategy identity and authored placement owned by the session.
        /// </summary>
        /// <param name="sector">The represented strategy sector.</param>
        /// <param name="sectorPosition">The authored sector-window position slot.</param>
        public void Initialize(GalaxyMapSector sector, int sectorPosition)
        {
            Sector = sector ?? throw new ArgumentNullException(nameof(sector));
            SelectSectorPosition(sectorPosition);
            ReconcileSelection();
        }

        /// <summary>
        /// Replaces the represented sector and removes stale interaction state.
        /// </summary>
        /// <param name="sector">The replacement strategy sector.</param>
        public void ReconcileSector(GalaxyMapSector sector)
        {
            Sector = sector ?? throw new ArgumentNullException(nameof(sector));
            ReconcileSelection();
        }

        /// <summary>
        /// Selects the authored window position owned by this session.
        /// </summary>
        /// <param name="sectorPosition">The requested sector-window position slot.</param>
        public void SelectSectorPosition(int sectorPosition)
        {
            SectorPosition = sectorPosition;
        }

        /// <summary>
        /// Resolves the stored context identity against the current sector projection.
        /// </summary>
        /// <returns>The current context hit, or null.</returns>
        public PlanetSystemWindowHit GetContextHit()
        {
            return GetStoredHit(contextPlanetInstanceId, contextIcon, contextPlanetImage);
        }

        /// <summary>
        /// Resolves the stored selection identity against the current sector projection.
        /// </summary>
        /// <returns>The current selected hit, or null.</returns>
        public PlanetSystemWindowHit GetSelectedHit()
        {
            return GetStoredHit(SelectedPlanetInstanceId, SelectedIcon, false);
        }

        /// <summary>
        /// Resolves one presentation element against the current sector projection.
        /// </summary>
        /// <param name="element">The selected presentation element.</param>
        /// <returns>The resolved semantic hit, or null.</returns>
        public PlanetSystemWindowHit ResolveHit(PlanetSystemWindowElement element)
        {
            if (
                Sector?.Planets == null
                || element == null
                || element.PlanetIndex < 0
                || element.PlanetIndex >= Sector.Planets.Count
            )
                return null;

            GalaxyMapPlanet planet = Sector.Planets[element.PlanetIndex];
            return planet?.Planet == null
                ? null
                : new PlanetSystemWindowHit(
                    planet,
                    element.PlanetIndex,
                    element.Icon,
                    element.PlanetImage
                );
        }

        /// <summary>
        /// Stores the active context hit.
        /// </summary>
        /// <param name="hit">The semantic planet hit.</param>
        public void StoreContextHit(PlanetSystemWindowHit hit)
        {
            contextPlanetInstanceId = hit?.Planet?.InstanceID;
            contextIcon = hit?.Icon ?? PlanetIcon.None;
            contextPlanetImage = hit?.PlanetImage == true;
        }

        /// <summary>
        /// Selects a semantic planet icon.
        /// </summary>
        /// <param name="hit">The semantic planet hit.</param>
        /// <returns>True when a selectable icon was stored.</returns>
        public bool SelectHit(PlanetSystemWindowHit hit)
        {
            if (hit?.Planet == null || hit.Icon == PlanetIcon.None)
                return false;

            SelectedPlanetInstanceId = hit.Planet.InstanceID;
            SelectedIcon = hit.Icon;
            return true;
        }

        /// <summary>
        /// Updates the hovered semantic planet icon.
        /// </summary>
        /// <param name="hit">The semantic planet hit.</param>
        /// <returns>True when hover state changed.</returns>
        public bool SetHover(PlanetSystemWindowHit hit)
        {
            string planetInstanceId = hit?.Planet?.InstanceID;
            PlanetIcon icon = hit?.Icon ?? PlanetIcon.None;
            if (
                string.Equals(HoveredPlanetInstanceId, planetInstanceId, StringComparison.Ordinal)
                && HoveredIcon == icon
            )
                return false;

            HoveredPlanetInstanceId = planetInstanceId;
            HoveredIcon = icon;
            return true;
        }

        /// <summary>
        /// Clears hovered planet state.
        /// </summary>
        /// <returns>True when hover state changed.</returns>
        public bool ClearHover()
        {
            if (string.IsNullOrEmpty(HoveredPlanetInstanceId) && HoveredIcon == PlanetIcon.None)
                return false;

            HoveredPlanetInstanceId = null;
            HoveredIcon = PlanetIcon.None;
            return true;
        }

        /// <summary>
        /// Clears context hit state.
        /// </summary>
        public void ClearContextHit()
        {
            contextPlanetInstanceId = null;
            contextIcon = PlanetIcon.None;
            contextPlanetImage = false;
        }

        /// <summary>
        /// Clears selected and context hit state.
        /// </summary>
        public void ClearSelection()
        {
            SelectedPlanetInstanceId = null;
            SelectedIcon = PlanetIcon.None;
            ClearContextHit();
        }

        /// <summary>
        /// Removes state that no longer resolves in the represented sector.
        /// </summary>
        public void ReconcileSelection()
        {
            HashSet<string> planetIds = new HashSet<string>(
                Sector
                    ?.Planets?.Select(planet => planet?.Planet?.InstanceID)
                    .Where(instanceId => !string.IsNullOrEmpty(instanceId))
                    ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal
            );
            if (!planetIds.Contains(SelectedPlanetInstanceId))
            {
                SelectedPlanetInstanceId = null;
                SelectedIcon = PlanetIcon.None;
            }
            if (!planetIds.Contains(contextPlanetInstanceId))
                ClearContextHit();
            if (!planetIds.Contains(HoveredPlanetInstanceId))
                ClearHover();
        }

        /// <summary>
        /// Resolves stored hit identity against the current sector projection.
        /// </summary>
        /// <param name="planetInstanceId">The stored planet identifier.</param>
        /// <param name="icon">The stored planet icon.</param>
        /// <param name="planetImage">Whether the planet image was hit.</param>
        /// <returns>The resolved semantic hit, or null.</returns>
        private PlanetSystemWindowHit GetStoredHit(
            string planetInstanceId,
            PlanetIcon icon,
            bool planetImage
        )
        {
            int planetIndex = FindPlanetIndex(planetInstanceId);
            return planetIndex < 0
                ? null
                : new PlanetSystemWindowHit(
                    Sector.Planets[planetIndex],
                    planetIndex,
                    icon,
                    planetImage
                );
        }

        /// <summary>
        /// Finds a planet's stable presentation index in the current sector.
        /// </summary>
        /// <param name="planetInstanceId">The planet identifier to find.</param>
        /// <returns>The matching index, or -1.</returns>
        private int FindPlanetIndex(string planetInstanceId)
        {
            if (Sector?.Planets == null || string.IsNullOrEmpty(planetInstanceId))
                return -1;

            for (int index = 0; index < Sector.Planets.Count; index++)
            {
                if (
                    string.Equals(
                        Sector.Planets[index]?.Planet?.InstanceID,
                        planetInstanceId,
                        StringComparison.Ordinal
                    )
                )
                    return index;
            }

            return -1;
        }
    }
}

/// <summary>
/// Identifies a visible planet-system element selected by pointer geometry.
/// </summary>
public sealed class PlanetSystemWindowHit
{
    /// <summary>
    /// Creates a semantic planet-system hit.
    /// </summary>
    /// <param name="galaxyMapPlanet">The represented strategy planet.</param>
    /// <param name="icon">The represented planet icon.</param>
    /// <param name="planetImage">Whether the planet image was hit.</param>
    public PlanetSystemWindowHit(GalaxyMapPlanet galaxyMapPlanet, PlanetIcon icon, bool planetImage)
        : this(galaxyMapPlanet, -1, icon, planetImage) { }

    /// <summary>
    /// Creates a semantic planet-system hit resolved from a presentation element.
    /// </summary>
    /// <param name="galaxyMapPlanet">The represented strategy planet.</param>
    /// <param name="planetIndex">The planet's position in the represented sector.</param>
    /// <param name="icon">The represented planet icon.</param>
    /// <param name="planetImage">Whether the planet image was hit.</param>
    internal PlanetSystemWindowHit(
        GalaxyMapPlanet galaxyMapPlanet,
        int planetIndex,
        PlanetIcon icon,
        bool planetImage
    )
    {
        GalaxyMapPlanet = galaxyMapPlanet;
        PlanetIndex = planetIndex;
        Icon = icon;
        PlanetImage = planetImage;
    }

    public PlanetSystemWindowElement Element =>
        new PlanetSystemWindowElement(PlanetIndex, Icon, PlanetImage);

    public GalaxyMapPlanet GalaxyMapPlanet { get; }

    public PlanetIcon Icon { get; }

    public Planet Planet => GalaxyMapPlanet?.Planet;

    public int PlanetIndex { get; }

    public bool PlanetImage { get; }
}
