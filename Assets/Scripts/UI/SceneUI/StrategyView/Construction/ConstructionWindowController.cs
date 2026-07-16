using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using UnityEngine;

/// <summary>
/// Performs window-level actions requested by the construction feature.
/// </summary>
public interface IConstructionWindowActions
{
    /// <summary>
    /// Opens Encyclopedia information for a manufacturable template.
    /// </summary>
    /// <param name="item">The selected manufacturable template.</param>
    void OpenConstructionInfo(ISceneNode item);

    /// <summary>
    /// Opens status information for the selected manufacturable template.
    /// </summary>
    /// <param name="target">The selected construction status target.</param>
    void OpenConstructionStatus(StrategyStatusTarget target);

    /// <summary>
    /// Rebuilds strategy state after a successful construction order.
    /// </summary>
    void RefreshAfterConstruction();
}

/// <summary>
/// Owns construction window sessions, domain orchestration, and semantic UI commands.
/// </summary>
public sealed class ConstructionWindowController
    : IStrategyContextMenuProvider,
        IContextMenuReceiver
{
    private readonly HashSet<ConstructionWindowView> boundViews =
        new HashSet<ConstructionWindowView>();
    private readonly Action<UIWindow> closeWindow;
    private readonly Func<Rebellion.Game.GameRoot> getGame;
    private readonly Func<int, int, Vector2Int> getConstructionWindowPosition;
    private readonly Func<Vector2Int> getUtilityWindowPosition;
    private readonly Action markDirty;
    private readonly ConstructionOrderController orderController;
    private readonly ConstructionWindowProjector projector;
    private readonly Dictionary<ConstructionWindowView, ConstructionWindowSession> sessions =
        new Dictionary<ConstructionWindowView, ConstructionWindowSession>();
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;

    private IConstructionWindowActions actions;

    /// <summary>
    /// Creates a construction feature controller.
    /// </summary>
    /// <param name="getGame">Returns the active game.</param>
    /// <param name="getManufacturingSystem">Returns the active manufacturing system.</param>
    /// <param name="getMovementSystem">Returns the active movement system.</param>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="windowLayer">Provides the authored construction prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getConstructionWindowPosition">Positions construction beside a facility window.</param>
    /// <param name="getUtilityWindowPosition">Returns the authored advisor utility placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public ConstructionWindowController(
        Func<Rebellion.Game.GameRoot> getGame,
        Func<ManufacturingSystem> getManufacturingSystem,
        Func<MovementSystem> getMovementSystem,
        Func<UIContext> getUIContext,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<int, int, Vector2Int> getConstructionWindowPosition,
        Func<Vector2Int> getUtilityWindowPosition,
        Action<UIWindow> closeWindow,
        Action markDirty
    )
    {
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getConstructionWindowPosition =
            getConstructionWindowPosition
            ?? throw new ArgumentNullException(nameof(getConstructionWindowPosition));
        this.getUtilityWindowPosition =
            getUtilityWindowPosition
            ?? throw new ArgumentNullException(nameof(getUtilityWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        orderController = new ConstructionOrderController(
            getGame,
            getManufacturingSystem,
            getMovementSystem
        );
        projector = new ConstructionWindowProjector(getUIContext);
    }

    /// <summary>
    /// Supplies owning window actions after the strategy controller graph is constructed.
    /// </summary>
    /// <param name="windowActions">The feature-specific construction actions.</param>
    public void Initialize(IConstructionWindowActions windowActions)
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
    }

    /// <summary>
    /// Starts or refreshes one construction session while preserving local state for the same panel.
    /// </summary>
    /// <param name="view">The destination construction view.</param>
    /// <param name="window">The owning construction window.</param>
    /// <param name="planet">The producing strategy planet.</param>
    /// <param name="sourceWindow">The facility window that opened this construction window.</param>
    /// <param name="initialManufacturingTab">The initial manufacturing facility tab.</param>
    /// <param name="destinationPlanetId">The selected destination planet identifier.</param>
    /// <param name="destinationItemId">The selected destination entity identifier.</param>
    /// <returns>True when a valid construction session was created.</returns>
    public bool TryInitializeWindow(
        ConstructionWindowView view,
        UIWindow window,
        GalaxyMapPlanet planet,
        UIWindow sourceWindow,
        FacilityWindowTab initialManufacturingTab,
        string destinationPlanetId,
        string destinationItemId
    )
    {
        if (
            view == null
            || window == null
            || planet?.Planet == null
            || !ConstructionOrderController.GetManufacturingType(initialManufacturingTab).HasValue
        )
            return false;

        BindWindow(view);
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
        {
            session = new ConstructionWindowSession(
                window,
                planet,
                sourceWindow,
                initialManufacturingTab,
                destinationPlanetId,
                destinationItemId
            );
            sessions.Add(view, session);
        }
        else
        {
            session.Reinitialize(
                planet,
                sourceWindow,
                initialManufacturingTab,
                destinationPlanetId,
                destinationItemId
            );
        }

        RefreshSessionItems(session);
        return true;
    }

    /// <summary>
    /// Opens construction beside the facility window that supplied its destination.
    /// </summary>
    /// <param name="producer">The manufacturing planet.</param>
    /// <param name="sourceWindow">The originating facility window.</param>
    /// <param name="manufacturingTab">The selected manufacturing category.</param>
    /// <param name="destinationPlanetId">The delivery planet identifier.</param>
    /// <param name="destinationItemId">The delivery entity identifier.</param>
    public void OpenFromFacility(
        GalaxyMapPlanet producer,
        UIWindow sourceWindow,
        FacilityWindowTab manufacturingTab,
        string destinationPlanetId,
        string destinationItemId
    )
    {
        if (sourceWindow == null)
            return;

        Open(
            producer,
            sourceWindow,
            manufacturingTab,
            destinationPlanetId,
            destinationItemId,
            getConstructionWindowPosition(sourceWindow.X, sourceWindow.Y)
        );
    }

    /// <summary>
    /// Opens advisor-directed construction for a resolved producer and destination.
    /// </summary>
    /// <param name="producer">The manufacturing planet.</param>
    /// <param name="destination">The delivery planet.</param>
    /// <param name="manufacturingTab">The selected manufacturing category.</param>
    public void OpenFromAdvisor(
        GalaxyMapPlanet producer,
        GalaxyMapPlanet destination,
        FacilityWindowTab manufacturingTab
    )
    {
        if (producer?.Planet == null || destination?.Planet == null)
            return;

        Open(
            producer,
            null,
            manufacturingTab,
            destination.Planet.InstanceID,
            null,
            getUtilityWindowPosition()
        );
    }

    /// <summary>
    /// Renders every registered construction window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out ConstructionWindowView view))
                RenderWindow(view, window, window.ActiveWindow);
        }
    }

    /// <summary>
    /// Rebinds construction sessions to a refreshed galaxy snapshot.
    /// </summary>
    /// <param name="sectors">The refreshed visible sectors.</param>
    public void ReconcileWindows(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            return;

        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                !windowManager.TryGetWindowView(window, out ConstructionWindowView view)
                || !sessions.TryGetValue(view, out ConstructionWindowSession session)
            )
                continue;

            GalaxyMapPlanet planet = FindFreshPlanet(session.Planet, sectors);
            if (planet != null)
                session.RebindPlanet(planet);
            RefreshSessionItems(session);
        }
    }

    /// <summary>
    /// Subscribes the controller to one construction view exactly once.
    /// </summary>
    /// <param name="view">The view to bind.</param>
    public void BindWindow(ConstructionWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        EnsureInitialized();
        if (!boundViews.Add(view))
            return;

        view.CancelRequested += HandleCancelRequested;
        view.DecrementRequested += HandleDecrementRequested;
        view.Destroyed += HandleViewDestroyed;
        view.DismissDropdownRequested += HandleDismissDropdownRequested;
        view.IncrementRequested += HandleIncrementRequested;
        view.InfoRequested += HandleInfoRequested;
        view.ItemSelected += HandleItemSelected;
        view.StartRequested += HandleStartRequested;
        view.ToggleDropdownRequested += HandleToggleDropdownRequested;
    }

    /// <summary>
    /// Builds the context commands available for the current construction selection.
    /// </summary>
    /// <param name="context">The active context-menu invocation.</param>
    /// <param name="request">Receives the completed command request.</param>
    /// <param name="width">Receives the authored menu width.</param>
    /// <returns>True when the construction view supplied a context menu.</returns>
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
            || !windowManager.TryGetWindowView(context.Window, out ConstructionWindowView view)
            || !sessions.TryGetValue(view, out ConstructionWindowSession session)
        )
            return false;

        bool hasSelection = session.SelectedItem != null;
        List<StrategyMenuCommand> commands = new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyContextMenuActions.Encyclopedia,
                "Encyclopedia",
                hasSelection
            ),
            new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", hasSelection),
        };
        request = new ContextMenuRequest(
            context,
            commands.Cast<IContextMenuCommand>().ToList(),
            this
        );
        width = context.Layout.FallbackMenuWidth;
        return true;
    }

    /// <summary>
    /// Routes one selected construction context command.
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
            || !windowManager.TryGetWindowView(context.Window, out ConstructionWindowView view)
            || !sessions.TryGetValue(view, out ConstructionWindowSession session)
        )
            return;

        ISceneNode item = session.SelectedItem as ISceneNode;
        switch (strategyCommand.Action)
        {
            case StrategyContextMenuActions.Encyclopedia when item != null:
                actions.OpenConstructionInfo(item);
                break;
            case StrategyContextMenuActions.Status when item != null:
                actions.OpenConstructionStatus(new StrategyStatusTarget(session.Planet, item));
                break;
        }
    }

    /// <summary>
    /// Handles context-menu cancellation without changing construction state.
    /// </summary>
    /// <param name="request">The canceled context-menu request.</param>
    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    /// <summary>
    /// Projects and renders the active session for one construction window.
    /// </summary>
    /// <param name="view">The destination construction view.</param>
    /// <param name="window">The window shell supplying source-space position.</param>
    /// <param name="active">Whether the window is currently active.</param>
    public void RenderWindow(ConstructionWindowView view, UIWindow window, bool active)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        ConstructionWindowSession session = GetSession(view);

        string playerFactionId = GetPlayerFactionId();
        IReadOnlyList<IManufacturable> items = session.Items;
        Planet producer = GetAuthoritativePlanet(session.Planet);
        ISceneNode destination = GetConstructionDestination(session);
        HashSet<int> canStartSelections = orderController.GetCanStartSelections(
            producer,
            destination,
            items,
            session.BuildCount,
            playerFactionId
        );
        IReadOnlyList<ConstructionBuildEstimate> estimates = orderController.GetBuildEstimates(
            producer,
            destination,
            items,
            session.BuildCount,
            canStartSelections
        );
        view.Render(
            projector.CreateRenderData(
                window.X,
                window.Y,
                session.Planet?.OwnerFactionId,
                active,
                items,
                session.SelectedItemIndex,
                session.BuildCount,
                canStartSelections,
                estimates,
                session.DropdownOpen
            )
        );
    }

    /// <summary>
    /// Replaces a construction session's strategy-planet projection without resetting local state.
    /// </summary>
    /// <param name="view">The construction view whose session changed.</param>
    /// <param name="planet">The refreshed strategy planet.</param>
    public void ReconcileWindow(ConstructionWindowView view, GalaxyMapPlanet planet)
    {
        if (
            view != null
            && planet != null
            && sessions.TryGetValue(view, out ConstructionWindowSession session)
        )
        {
            session.RebindPlanet(planet);
            RefreshSessionItems(session);
        }
    }

    /// <summary>
    /// Gets the strategy planet owned by one construction session.
    /// </summary>
    /// <param name="view">The construction view.</param>
    /// <returns>The session's strategy planet, or null.</returns>
    public GalaxyMapPlanet GetPlanet(ConstructionWindowView view)
    {
        return view != null && sessions.TryGetValue(view, out ConstructionWindowSession session)
            ? session.Planet
            : null;
    }

    /// <summary>
    /// Gets the selected build template as a status target.
    /// </summary>
    /// <param name="view">The construction view.</param>
    /// <returns>The selected status target, or null.</returns>
    public StrategyStatusTarget GetStatusTarget(ConstructionWindowView view)
    {
        if (view == null || !sessions.TryGetValue(view, out ConstructionWindowSession session))
            return null;

        IManufacturable item = session.SelectedItem;
        return item is ISceneNode node ? new StrategyStatusTarget(session.Planet, node) : null;
    }

    /// <summary>
    /// Routes a semantic close request for one construction session.
    /// </summary>
    /// <param name="view">The requesting construction view.</param>
    private void HandleCancelRequested(ConstructionWindowView view)
    {
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
            return;

        session.Window.RequestFocus();
        closeWindow(session.Window);
    }

    /// <summary>
    /// Decrements a construction session's build count within the supported byte range.
    /// </summary>
    /// <param name="view">The requesting construction view.</param>
    private void HandleDecrementRequested(ConstructionWindowView view)
    {
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
            return;

        session.Window.RequestFocus();
        session.DecrementBuildCount();
        markDirty();
    }

    /// <summary>
    /// Closes an open construction dropdown after a background click.
    /// </summary>
    /// <param name="view">The requesting construction view.</param>
    private void HandleDismissDropdownRequested(ConstructionWindowView view)
    {
        if (
            !sessions.TryGetValue(view, out ConstructionWindowSession session)
            || !session.DismissDropdown()
        )
            return;

        session.Window.RequestFocus();
        markDirty();
    }

    /// <summary>
    /// Increments a construction session's build count within the supported byte range.
    /// </summary>
    /// <param name="view">The requesting construction view.</param>
    private void HandleIncrementRequested(ConstructionWindowView view)
    {
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
            return;

        session.Window.RequestFocus();
        session.IncrementBuildCount();
        markDirty();
    }

    /// <summary>
    /// Opens Encyclopedia information for the selected construction template.
    /// </summary>
    /// <param name="view">The requesting construction view.</param>
    private void HandleInfoRequested(ConstructionWindowView view)
    {
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
            return;

        session.Window.RequestFocus();
        if (session.SelectedItem is ISceneNode item)
            actions.OpenConstructionInfo(item);
    }

    /// <summary>
    /// Selects a build template and closes the construction dropdown.
    /// </summary>
    /// <param name="view">The requesting construction view.</param>
    /// <param name="index">The selected dropdown index.</param>
    private void HandleItemSelected(ConstructionWindowView view, int index)
    {
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
            return;

        if (!session.SelectItem(index))
            return;

        session.Window.RequestFocus();
        markDirty();
    }

    /// <summary>
    /// Starts the selected construction order and routes successful completion.
    /// </summary>
    /// <param name="view">The requesting construction view.</param>
    private void HandleStartRequested(ConstructionWindowView view)
    {
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
            return;

        session.Window.RequestFocus();
        string playerFactionId = GetPlayerFactionId();
        IManufacturable selected = session.SelectedItem;
        bool started = orderController.TryStartConstruction(
            GetAuthoritativePlanet(session.Planet),
            GetConstructionDestination(session),
            selected,
            session.BuildCount,
            playerFactionId
        );
        if (started)
        {
            actions.RefreshAfterConstruction();
            closeWindow(session.Window);
        }
        else
            markDirty();
    }

    /// <summary>
    /// Toggles the construction dropdown for one session.
    /// </summary>
    /// <param name="view">The requesting construction view.</param>
    private void HandleToggleDropdownRequested(ConstructionWindowView view)
    {
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
            return;

        session.Window.RequestFocus();
        session.ToggleDropdown();
        markDirty();
    }

    /// <summary>
    /// Releases subscriptions and session state for a destroyed construction view.
    /// </summary>
    /// <param name="view">The destroyed construction view.</param>
    private void HandleViewDestroyed(ConstructionWindowView view)
    {
        if (ReferenceEquals(view, null) || !boundViews.Remove(view))
            return;

        view.CancelRequested -= HandleCancelRequested;
        view.DecrementRequested -= HandleDecrementRequested;
        view.Destroyed -= HandleViewDestroyed;
        view.DismissDropdownRequested -= HandleDismissDropdownRequested;
        view.IncrementRequested -= HandleIncrementRequested;
        view.InfoRequested -= HandleInfoRequested;
        view.ItemSelected -= HandleItemSelected;
        view.StartRequested -= HandleStartRequested;
        view.ToggleDropdownRequested -= HandleToggleDropdownRequested;
        sessions.Remove(view);
    }

    /// <summary>
    /// Gets the current player faction identifier.
    /// </summary>
    /// <returns>The player faction identifier, or null.</returns>
    private string GetPlayerFactionId()
    {
        return getGame()?.GetPlayerFaction()?.InstanceID;
    }

    /// <summary>
    /// Gets the controller-owned session for an initialized construction view.
    /// </summary>
    /// <param name="view">The initialized construction view.</param>
    /// <returns>The controller-owned session.</returns>
    private ConstructionWindowSession GetSession(ConstructionWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (!sessions.TryGetValue(view, out ConstructionWindowSession session))
        {
            throw new InvalidOperationException(
                "The construction view has not been initialized by this controller."
            );
        }

        return session;
    }

    /// <summary>
    /// Verifies that action routing is available before a view is bound.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                $"{nameof(ConstructionWindowController)} must be initialized before use."
            );
    }

    /// <summary>
    /// Updates open construction sessions that originated from a changed facility destination.
    /// </summary>
    /// <param name="facilityWindow">The facility window that owns the destination.</param>
    /// <param name="manufacturingType">The changed manufacturing category.</param>
    /// <param name="destinationPlanetId">The destination planet identifier.</param>
    /// <param name="destinationItemId">The destination entity identifier.</param>
    public void UpdateOpenConstructionDestination(
        UIWindow facilityWindow,
        ManufacturingType manufacturingType,
        string destinationPlanetId,
        string destinationItemId
    )
    {
        foreach (ConstructionWindowSession session in sessions.Values)
            session.TryUpdateDestination(
                facilityWindow,
                manufacturingType,
                destinationPlanetId,
                destinationItemId
            );
    }

    /// <summary>
    /// Opens or refreshes construction for one producer and delivery target.
    /// </summary>
    /// <param name="producer">The manufacturing planet.</param>
    /// <param name="sourceWindow">The originating facility window, or null for advisor requests.</param>
    /// <param name="manufacturingTab">The selected manufacturing category.</param>
    /// <param name="destinationPlanetId">The delivery planet identifier.</param>
    /// <param name="destinationItemId">The delivery entity identifier.</param>
    /// <param name="position">The requested source-space window position.</param>
    private void Open(
        GalaxyMapPlanet producer,
        UIWindow sourceWindow,
        FacilityWindowTab manufacturingTab,
        string destinationPlanetId,
        string destinationItemId,
        Vector2Int position
    )
    {
        if (producer?.Planet == null)
            return;

        ConstructionWindowView existing = FindWindow(producer.Planet.InstanceID);
        if (
            existing != null
            && sessions.TryGetValue(existing, out ConstructionWindowSession existingSession)
        )
        {
            TryInitializeWindow(
                existing,
                existingSession.Window,
                producer,
                sourceWindow,
                manufacturingTab,
                destinationPlanetId,
                destinationItemId
            );
            existingSession.Window.RequestFocus();
            return;
        }

        UIWindow window = windowManager.CreateWindow(
            windowLayer.ConstructionWindowPrefab,
            windowLayer.GetWindowParent(true),
            $"ConstructionWindow-{producer.Planet.GetDisplayName()}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.ConstructionWindowPrefab),
            true,
            true,
            true,
            false,
            out ConstructionWindowView view
        );
        if (
            !TryInitializeWindow(
                view,
                window,
                producer,
                sourceWindow,
                manufacturingTab,
                destinationPlanetId,
                destinationItemId
            )
        )
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Finds a construction window for a producing planet.
    /// </summary>
    /// <param name="planetId">The producing planet identifier.</param>
    /// <returns>The matching construction view, or null when none is open.</returns>
    private ConstructionWindowView FindWindow(string planetId)
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                windowManager.TryGetWindowView(window, out ConstructionWindowView view)
                && sessions.TryGetValue(view, out ConstructionWindowSession session)
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
    /// Resolves the destination planet for a construction session.
    /// </summary>
    /// <param name="session">The construction session.</param>
    /// <returns>The authoritative destination planet, or null.</returns>
    private Planet GetConstructionDestinationPlanet(ConstructionWindowSession session)
    {
        return GetAuthoritativePlanet(session?.GetDestinationPlanetId());
    }

    /// <summary>
    /// Resolves the destination node for a construction session.
    /// </summary>
    /// <param name="session">The construction session.</param>
    /// <returns>The configured entity or fallback destination planet.</returns>
    private ISceneNode GetConstructionDestination(ConstructionWindowSession session)
    {
        ISceneNode item = GetAuthoritativeNode(session?.DestinationItemId);
        return item ?? GetConstructionDestinationPlanet(session);
    }

    /// <summary>
    /// Resolves a strategy planet projection to its authoritative game node.
    /// </summary>
    /// <param name="planet">The strategy planet projection.</param>
    /// <returns>The authoritative planet, or null.</returns>
    private Planet GetAuthoritativePlanet(GalaxyMapPlanet planet)
    {
        return GetAuthoritativePlanet(planet?.Planet?.InstanceID);
    }

    /// <summary>
    /// Resolves a planet instance identifier to its authoritative game node.
    /// </summary>
    /// <param name="planetId">The planet instance identifier.</param>
    /// <returns>The authoritative planet, or null.</returns>
    private Planet GetAuthoritativePlanet(string planetId)
    {
        return getGame()?.GetSceneNodeByInstanceID<Planet>(planetId);
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
    /// Refreshes one session's ordered build templates from current research state.
    /// </summary>
    /// <param name="session">The construction session to refresh.</param>
    private void RefreshSessionItems(ConstructionWindowSession session)
    {
        if (session == null)
            return;

        session.SetItems(
            orderController.GetBuildSelection(session.ManufacturingTab, GetPlayerFactionId())
        );
    }
}
