using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class StrategyWindowCommandController
    : IFleetWindowActions,
        IStrategyWindowCommandActions,
        IStrategyContextMenuActions,
        IFinderWindowNavigationActions
{
    private readonly GameManager gameManager;
    private readonly List<UIWindow> windows = new List<UIWindow>();
    private readonly StrategyWindowLayerView windowLayer;
    private readonly StrategyWindowPlacementController windowPlacementController;
    private readonly BookmarkController bookmarkController;
    private readonly ConstructionWindowController constructionWindowController;
    private readonly MissionCreateWindowController missionCreateWindowController;
    private readonly StrategyConfirmActionController confirmActionController;
    private readonly FinderWindowNavigationController finderNavigationController;
    private readonly Func<IReadOnlyList<GalaxyMapSector>> getSectors;
    private readonly Func<string> getPlayerFactionId;
    private readonly Func<GalaxyMapSector, Vector2Int> getSystemSourcePosition;
    private readonly Action<UIWindow> clearClosingWindowState;
    private readonly Action rebuildSnapshot;
    private readonly Action markDirty;
    private int nextWindowId = 1;

    public StrategyWindowCommandController(
        GameManager gameManager,
        StrategyWindowLayerView windowLayer,
        StrategyWindowPlacementController windowPlacementController,
        BookmarkController bookmarkController,
        ConstructionWindowController constructionWindowController,
        MissionCreateWindowController missionCreateWindowController,
        StrategyConfirmActionController confirmActionController,
        Func<IReadOnlyList<GalaxyMapSector>> getSectors,
        Func<string> getPlayerFactionId,
        Func<GalaxyMapSector, Vector2Int> getSystemSourcePosition,
        Action<UIWindow> clearClosingWindowState,
        Action rebuildSnapshot,
        Action markDirty
    )
    {
        this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowPlacementController =
            windowPlacementController
            ?? throw new ArgumentNullException(nameof(windowPlacementController));
        this.bookmarkController =
            bookmarkController ?? throw new ArgumentNullException(nameof(bookmarkController));
        this.constructionWindowController =
            constructionWindowController
            ?? throw new ArgumentNullException(nameof(constructionWindowController));
        this.missionCreateWindowController =
            missionCreateWindowController
            ?? throw new ArgumentNullException(nameof(missionCreateWindowController));
        this.confirmActionController =
            confirmActionController
            ?? throw new ArgumentNullException(nameof(confirmActionController));
        this.getSectors = getSectors ?? throw new ArgumentNullException(nameof(getSectors));
        this.getPlayerFactionId =
            getPlayerFactionId ?? throw new ArgumentNullException(nameof(getPlayerFactionId));
        this.getSystemSourcePosition =
            getSystemSourcePosition
            ?? throw new ArgumentNullException(nameof(getSystemSourcePosition));
        this.clearClosingWindowState =
            clearClosingWindowState
            ?? throw new ArgumentNullException(nameof(clearClosingWindowState));
        this.rebuildSnapshot =
            rebuildSnapshot ?? throw new ArgumentNullException(nameof(rebuildSnapshot));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        finderNavigationController = new FinderWindowNavigationController(this);
    }

    public string PlayerFactionId => getPlayerFactionId() ?? string.Empty;
    public GameManager GameManager => gameManager;
    public IReadOnlyList<UIWindow> Windows => windows;

    public UIWindow GetWindow(PointerEventData eventData)
    {
        return windowLayer.GetWindow(eventData);
    }

    public UIWindow GetWindowById(int windowId)
    {
        return windows.FirstOrDefault(window => window != null && window.Id == windowId);
    }

    public void FocusWindow(UIWindow window)
    {
        if (window == null || !window.CanFocus || !windows.Contains(window))
            return;

        if (!windowLayer.FocusWindow(window))
            return;

        windows.Remove(window);
        windows.Add(window);
    }

    public bool HasModalWindow()
    {
        return windowLayer.HasModalWindow();
    }

    public bool TryGetWindowView<TView>(UIWindow window, out TView view)
        where TView : class
    {
        return windowLayer.TryGetWindowView(window, out view);
    }

    public void RenderWindows(StrategyWindowRenderContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        foreach (UIWindow window in windows)
            RenderWindow(context, window);
    }

    public void ReconcileWindows(IReadOnlyList<GalaxyMapSector> sectors)
    {
        foreach (UIWindow window in windows)
            ReconcileWindow(window, sectors);

        bookmarkController.ReconcilePlanets(sectors);
    }

    public bool OpenPlanetSystemWindow(PlanetSystem system, int x, int y)
    {
        if (system == null)
            return false;

        GalaxyMapSector sector = getSectors().FirstOrDefault(item => item.System == system);
        return OpenSectorWindow(sector, x, y);
    }

    public void OpenPlanetWindow(Planet planet, PlanetIcon icon, int x, int y)
    {
        if (planet == null || icon == PlanetIcon.None)
            return;

        GalaxyMapPlanet strategyPlanet = getSectors()
            .SelectMany(sector => sector.Planets)
            .FirstOrDefault(item => item.Planet?.GetInstanceID() == planet.GetInstanceID());
        if (strategyPlanet == null)
            return;

        OpenPlanetWindowAt(strategyPlanet, icon, x, y);
        markDirty();
    }

    private void RenderWindow(StrategyWindowRenderContext context, UIWindow window)
    {
        if (window == null)
            return;

        windowLayer.ShowWindow(window);
        bool active = IsWindowActive(window);
        if (window.TryGetContent(out IStrategyWindowContent content))
            content.RefreshWindow(context, window, active);
    }

    private void ReconcileWindow(UIWindow window, IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (window == null || sectors == null)
            return;

        if (TryGetWindowView(window, out IGalaxyMapSectorWindowView sectorView))
        {
            GalaxyMapSector sector = FindFreshSector(sectorView.Sector, sectors);
            if (sector != null)
                sectorView.ReconcileSector(sector);
            return;
        }

        if (!TryGetWindowView(window, out IGalaxyMapPlanetWindowView planetView))
            return;

        GalaxyMapPlanet planet = FindFreshPlanet(planetView.GalaxyMapPlanet, sectors);
        if (planet != null)
            planetView.ReconcilePlanet(planet);
    }

    private bool IsWindowActive(UIWindow window)
    {
        return window?.ActiveWindow == true;
    }

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

    public bool OpenSectorWindow(GalaxyMapSector sector, int x, int y)
    {
        if (sector == null)
            return false;

        UIWindow leftWindow = FindSectorWindow(SectorWindowPositions.Left);
        UIWindow rightWindow = FindSectorWindow(SectorWindowPositions.Right);
        PlanetSystemWindowView leftView = GetPlanetSystemWindowView(leftWindow);
        PlanetSystemWindowView rightView = GetPlanetSystemWindowView(rightWindow);

        if (leftView?.Sector == sector || rightView?.Sector == sector)
            return false;

        Vector2Int thresholds = windowPlacementController.GetSectorWindowOpenThresholds();
        int target = -1;

        if (x < thresholds.x)
            target = leftWindow == null ? SectorWindowPositions.Left : SectorWindowPositions.Right;
        else if (x > thresholds.y)
            target = rightWindow == null ? SectorWindowPositions.Right : SectorWindowPositions.Left;

        if (target < 0)
            return false;

        if (target == SectorWindowPositions.Left && leftWindow != null)
            CloseWindow(leftWindow);
        else if (target == SectorWindowPositions.Right && rightWindow != null)
            CloseWindow(rightWindow);

        Vector2Int position = windowPlacementController.GetSectorWindowPosition(target);
        UIWindow window = CreateWindow<PlanetSystemWindowView>(
            windowLayer.OpenPlanetSystemWindow,
            position.x,
            position.y,
            out PlanetSystemWindowView view
        );
        view.InitializeWindow(sector, target);
        AddStrategyWindow(window);
        return true;
    }

    public UIWindow OpenPlanetWindowAt(GalaxyMapPlanet planet, PlanetIcon icon, int x, int y)
    {
        if (icon == PlanetIcon.None)
            return null;

        BookmarkEntry bookmark = bookmarkController.Take(planet, icon);
        UIWindow existing = FindPlanetWindow(icon, planet);
        if (existing != null)
        {
            FocusWindow(existing);
            return existing;
        }

        Vector2Int position =
            bookmark != null
                ? windowPlacementController.ClampPlanetWindowPosition(icon, bookmark.X, bookmark.Y)
                : windowPlacementController.ClampPlanetWindowPosition(icon, x, y);

        UIWindow window = icon switch
        {
            PlanetIcon.Facility => OpenPlanetWindow<FacilityWindowView>(
                windowLayer.OpenFacilityWindow,
                planet,
                position
            ),
            PlanetIcon.Defense => OpenPlanetWindow<DefenseWindowView>(
                windowLayer.OpenDefenseWindow,
                planet,
                position
            ),
            PlanetIcon.Fleet => OpenPlanetWindow<FleetWindowView>(
                windowLayer.OpenFleetWindow,
                planet,
                position
            ),
            PlanetIcon.Mission => OpenPlanetWindow<MissionsWindowView>(
                windowLayer.OpenMissionsWindow,
                planet,
                position
            ),
            _ => null,
        };
        return window;
    }

    public void ExecuteWindowButton(UIWindow window, int action)
    {
        if (window == null || action == 0)
            return;

        switch (action)
        {
            case StrategyWindowButtonActions.CloseWindow:
            case StrategyDialogButtonActions.Close:
                CloseWindow(window);
                break;
            case StrategyWindowButtonActions.OpenSector:
                GalaxyMapPlanet planet = GetWindowPlanet(window);
                if (planet != null)
                    OpenSectorWindow(planet.Sector, window.X, window.Y);
                break;
            case StrategyWindowButtonActions.SwapWindow:
                SwapSectorWindow(window);
                break;
            case StrategyWindowButtonActions.MinimizeWindow:
                MinimizeWindow(window);
                break;
            case StrategyDialogButtonActions.Target:
                OpenSelectedFinderItem(window);
                break;
        }
    }

    public void ExecuteMainButton(int action)
    {
        switch (action)
        {
            case StrategyHudActions.Options:
                OpenSaveMenuWindow();
                break;
            case StrategyHudActions.Messages:
                OpenMessagesWindow(0);
                break;
            case StrategyHudActions.SystemFinder:
                OpenFinderWindow(FinderMode.Systems);
                break;
            case StrategyHudActions.FleetFinder:
                OpenFinderWindow(FinderMode.Fleets);
                break;
            case StrategyHudActions.TroopFinder:
                OpenFinderWindow(FinderMode.Troops);
                break;
            case StrategyHudActions.PersonnelFinder:
                OpenFinderWindow(FinderMode.Personnel);
                break;
            case StrategyHudActions.Encyclopedia:
                OpenEncyclopediaWindow();
                break;
        }
    }

    public void OpenSelectedFinderItem(int windowId)
    {
        OpenSelectedFinderItem(GetWindowById(windowId));
    }

    public void CloseWindow(int windowId)
    {
        CloseWindow(GetWindowById(windowId));
    }

    public void OpenConstructionInfo(int windowId)
    {
        UIWindow window = GetWindowById(windowId);
        if (!TryGetWindowView(window, out ConstructionWindowView view))
            return;

        OpenEncyclopediaWindow(view.GetSelectedBuildItem());
    }

    public void OpenStatusInfo(int windowId)
    {
        UIWindow window = GetWindowById(windowId);
        if (!TryGetWindowView(window, out StatusWindowView statusView) || statusView.InfoDisabled)
            return;

        OpenEncyclopediaWindow(statusView.StatusTarget);
    }

    public void StartConstruction(StrategyUIRequests.StartConstruction request)
    {
        UIWindow window = GetWindowById(request.WindowId);
        if (!TryGetWindowView(window, out ConstructionWindowView _))
            return;

        bool started = constructionWindowController.TryStartConstruction(
            window,
            request.BuildPanel,
            request.BuildSelection,
            request.BuildCount,
            PlayerFactionId
        );
        if (started)
        {
            rebuildSnapshot();
            CloseWindow(window);
        }

        markDirty();
    }

    public void StopManufacturingQueue(UIWindow window)
    {
        if (
            !TryGetWindowView(window, out FacilityWindowView view)
            || !view.TryGetContextManufacturingType(out ManufacturingType type)
        )
            return;

        Planet planet = GetAuthoritativeWindowPlanet(view.GalaxyMapPlanet);
        if (
            planet == null
            || !string.Equals(
                planet.GetOwnerInstanceID(),
                PlayerFactionId,
                StringComparison.Ordinal
            )
        )
            return;

        if (gameManager.StopManufacturing(planet, type))
            rebuildSnapshot();

        markDirty();
    }

    public void ExecuteConfirmDialogChoice(StrategyUIRequests.ConfirmDialogChoice request)
    {
        UIWindow window = GetWindowById(request.WindowId);
        if (!TryGetWindowView(window, out ConfirmDialogWindowView view))
            return;

        if (view.Kind == ConfirmDialogKind.Move)
            ExecuteMoveConfirmAction(window, view, request.Confirmed);
        else if (view.Kind == ConfirmDialogKind.Scrap)
            ExecuteScrapConfirmAction(window, view, request.Confirmed);
        else if (view.Kind == ConfirmDialogKind.Retire)
            ExecuteRetireConfirmAction(window, view, request.Confirmed);
        else
            return;

        markDirty();
    }

    public void ExecuteMissionCreateCommand(StrategyUIRequests.ExecuteMissionCreateCommand request)
    {
        UIWindow window = GetWindowById(request.WindowId);
        if (!TryGetWindowView(window, out MissionCreateWindowView view))
            return;

        MissionCreateCommandResult result = missionCreateWindowController.ExecuteCommand(
            view,
            request
        );
        if (
            request.Command == MissionCreateWindowCommand.Ok
            && result == MissionCreateCommandResult.CloseWindow
        )
            rebuildSnapshot();

        switch (result)
        {
            case MissionCreateCommandResult.OpenInfo:
                OpenEncyclopediaWindow();
                break;
            case MissionCreateCommandResult.CloseWindow:
                CloseWindow(window);
                break;
        }

        markDirty();
    }

    public bool TryRestoreBookmark(int index)
    {
        if (!bookmarkController.TryTake(index, out BookmarkEntry bookmark))
            return false;

        OpenPlanetWindowAt(bookmark.Planet, bookmark.Icon, bookmark.X, bookmark.Y);
        return true;
    }

    public Vector2Int GetSystemSourcePosition(GalaxyMapSector sector)
    {
        return getSystemSourcePosition(sector);
    }

    public void OpenConstructionWindow(UIWindow facilityWindow)
    {
        if (!TryGetWindowView(facilityWindow, out FacilityWindowView facilityView))
            return;

        int buildPanel = facilityView.GetContextManufacturingPanel();
        if (buildPanel == 0)
            return;

        if (
            !constructionWindowController.TryGetConstructionDestinationIds(
                facilityWindow,
                buildPanel,
                out string destinationPlanetId,
                out string destinationItemId
            )
        )
            return;

        UIWindow existing = windows.FirstOrDefault(window =>
            TryGetWindowView(window, out ConstructionWindowView view)
            && view.GalaxyMapPlanet == facilityView.GalaxyMapPlanet
        );
        if (existing != null)
        {
            if (existing.TryGetContent(out ConstructionWindowView existingView))
            {
                existingView.InitializeWindow(
                    facilityView.GalaxyMapPlanet,
                    facilityWindow,
                    buildPanel,
                    destinationPlanetId,
                    destinationItemId
                );
            }
            FocusWindow(existing);
            return;
        }

        Vector2Int position = windowPlacementController.GetConstructionWindowPosition(
            facilityWindow.X,
            facilityWindow.Y
        );
        UIWindow window = CreateWindow<ConstructionWindowView>(
            windowLayer.OpenConstructionWindow,
            position.x,
            position.y,
            out ConstructionWindowView constructionView
        );
        constructionView.InitializeWindow(
            facilityView.GalaxyMapPlanet,
            facilityWindow,
            buildPanel,
            destinationPlanetId,
            destinationItemId
        );
        AddStrategyWindow(window);
    }

    public void OpenStatusWindow(UIWindow sourceWindow)
    {
        OpenStatusWindow(sourceWindow, GetStatusTarget(sourceWindow));
    }

    public void OpenEncyclopediaWindow(UIWindow sourceWindow)
    {
        OpenEncyclopediaWindow(GetStatusTarget(sourceWindow));
    }

    public void OpenScrapConfirmWindow(UIWindow sourceWindow)
    {
        OpenScrapConfirmWindow(sourceWindow, GetScrapItems(sourceWindow));
    }

    public void OpenRetireConfirmWindow(UIWindow sourceWindow)
    {
        OpenRetireConfirmWindow(sourceWindow, GetContextItems(sourceWindow));
    }

    public void OpenMissionCreateWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        Vector2Int position = windowPlacementController.GetMissionCreateWindowPosition();
        UIWindow existing = FindWindow<MissionCreateWindowView>();
        if (existing != null)
            CloseWindow(existing);

        UIWindow window = CreateWindow<MissionCreateWindowView>(
            windowLayer.OpenMissionCreateWindow,
            position.x,
            position.y,
            out MissionCreateWindowView view
        );
        if (
            !missionCreateWindowController.TryInitializeWindow(
                view,
                sourceWindow,
                target,
                items,
                PlayerFactionId
            )
        )
        {
            windowLayer.CloseWindow(window);
            return;
        }

        AddStrategyWindow(window);
    }

    public bool TryExecuteMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        return TryExecuteMove(sourceWindow, target, ToSceneNodeList(items));
    }

    public void OpenMoveConfirmWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        Vector2Int position = windowPlacementController.GetConfirmWindowPosition();
        UIWindow window = CreateWindow<ConfirmDialogWindowView>(
            windowLayer.OpenConfirmDialogWindow,
            position.x,
            position.y,
            out ConfirmDialogWindowView view
        );
        if (
            !confirmActionController.TryInitializeMoveConfirmWindow(
                view,
                sourceWindow,
                target,
                items,
                PlayerFactionId
            )
        )
        {
            windowLayer.CloseWindow(window);
            return;
        }

        AddStrategyWindow(window);
    }

    void IFleetWindowActions.OpenFleetStatusWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = ToSceneNodeList(items);
        GalaxyMapPlanet planet = GetWindowPlanet(sourceWindow);
        if (planet == null || sourceItems.Count != 1)
            return;

        OpenStatusWindow(sourceWindow, new StrategyStatusTarget(planet, sourceItems[0]));
    }

    void IFleetWindowActions.OpenFleetScrapConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        OpenScrapConfirmWindow(sourceWindow, items);
    }

    void IFleetWindowActions.OpenFleetRetireConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        OpenRetireConfirmWindow(sourceWindow, items);
    }

    void IFleetWindowActions.OpenFleetMissionCreateWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        OpenMissionCreateWindow(sourceWindow, target, items);
    }

    bool IFleetWindowActions.TryExecuteFleetMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        return TryExecuteMove(sourceWindow, target, items);
    }

    void IFleetWindowActions.OpenFleetMoveConfirmWindow(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> items
    )
    {
        OpenMoveConfirmWindow(sourceWindow, target, items);
    }

    private void OpenStatusWindow(UIWindow sourceWindow, StrategyStatusTarget target)
    {
        if (target == null)
            return;

        UIWindow existing = FindWindow<StatusWindowView>();
        if (existing != null)
            CloseWindow(existing);

        Vector2Int position = windowPlacementController.GetStatusWindowPosition();
        UIWindow window = CreateWindow<StatusWindowView>(
            windowLayer.OpenStatusWindow,
            position.x,
            position.y,
            out StatusWindowView view
        );
        view.InitializeWindow(sourceWindow, target, false);
        AddStrategyWindow(window);
    }

    private StrategyStatusTarget GetStatusTarget(UIWindow window)
    {
        return TryGetWindowView(window, out IStrategyWindowStatusTargetView view)
            ? view.GetStatusTarget(GetWindowPlanet(window))
            : null;
    }

    private void OpenScrapConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems
    )
    {
        Vector2Int position = windowPlacementController.GetConfirmWindowPosition();
        UIWindow window = CreateWindow<ConfirmDialogWindowView>(
            windowLayer.OpenConfirmDialogWindow,
            position.x,
            position.y,
            out ConfirmDialogWindowView view
        );
        if (
            !confirmActionController.TryInitializeScrapConfirmWindow(
                view,
                sourceWindow,
                sourceItems
            )
        )
        {
            windowLayer.CloseWindow(window);
            return;
        }

        AddStrategyWindow(window);
    }

    private void OpenRetireConfirmWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems
    )
    {
        Vector2Int position = windowPlacementController.GetConfirmWindowPosition();
        UIWindow window = CreateWindow<ConfirmDialogWindowView>(
            windowLayer.OpenConfirmDialogWindow,
            position.x,
            position.y,
            out ConfirmDialogWindowView view
        );
        if (
            !confirmActionController.TryInitializeRetireConfirmWindow(
                view,
                sourceWindow,
                sourceItems,
                PlayerFactionId
            )
        )
        {
            windowLayer.CloseWindow(window);
            return;
        }

        AddStrategyWindow(window);
    }

    public void OpenMessagesWindow(int initialTab)
    {
        Vector2Int position = windowPlacementController.GetUtilityWindowPosition();
        UIWindow existing = FindWindow<MessagesWindowView>();
        if (existing != null)
        {
            if (existing.TryGetContent(out MessagesWindowView existingView))
                existingView.OpenTab(initialTab);
            FocusWindow(existing);
            markDirty();
            return;
        }

        UIWindow window = CreateWindow<MessagesWindowView>(
            windowLayer.OpenMessagesWindow,
            position.x,
            position.y,
            out MessagesWindowView view
        );
        view.OpenTab(initialTab);
        AddStrategyWindow(window);
    }

    public void OpenEncyclopediaWindow()
    {
        Vector2Int position = windowPlacementController.GetUtilityWindowPosition();
        UIWindow existing = FindWindow<EncyclopediaWindowView>();
        if (existing != null)
        {
            FocusWindow(existing);
            return;
        }

        UIWindow window = CreateWindow<EncyclopediaWindowView>(
            windowLayer.OpenEncyclopediaWindow,
            position.x,
            position.y,
            out EncyclopediaWindowView view
        );
        AddStrategyWindow(window);
    }

    private void OpenEncyclopediaWindow(StrategyStatusTarget target)
    {
        if (target?.ManufacturingType.HasValue == true)
        {
            OpenEncyclopediaWindow();
            return;
        }

        OpenEncyclopediaWindow(target?.Item ?? target?.Planet?.Planet);
    }

    private void OpenEncyclopediaWindow(ISceneNode target)
    {
        if (target == null)
        {
            OpenEncyclopediaWindow();
            return;
        }

        Vector2Int position = windowPlacementController.GetUtilityWindowPosition();
        UIWindow existing = FindWindow<EncyclopediaWindowView>();
        if (existing != null)
        {
            if (existing.TryGetContent(out EncyclopediaWindowView existingView))
                existingView.RequestEntry(target);
            FocusWindow(existing);
            markDirty();
            return;
        }

        UIWindow window = CreateWindow<EncyclopediaWindowView>(
            windowLayer.OpenEncyclopediaWindow,
            position.x,
            position.y,
            out EncyclopediaWindowView view
        );
        view.RequestEntry(target);
        AddStrategyWindow(window);
        markDirty();
    }

    private void OpenFinderWindow(FinderMode mode)
    {
        Vector2Int position = windowPlacementController.GetUtilityWindowPosition();
        UIWindow existing = FindFinderWindow(mode);
        if (existing != null)
        {
            FocusWindow(existing);
            return;
        }

        UIWindow window = CreateWindow<FinderWindowView>(
            windowLayer.OpenFinderWindow,
            position.x,
            position.y,
            out FinderWindowView view
        );
        view.InitializeWindow(mode);
        AddStrategyWindow(window);
    }

    private void OpenSaveMenuWindow()
    {
        SaveMenuLaunchContext.OpenFromStrategyView();
        SceneManager.LoadScene(SaveMenuLaunchContext.SaveMenuSceneName);
    }

    private void MinimizeWindow(UIWindow window)
    {
        GalaxyMapPlanet planet = GetWindowPlanet(window);
        PlanetIcon icon = GetPlanetWindowIcon(window);
        if (planet == null || icon == PlanetIcon.None)
            return;

        if (bookmarkController.TryAdd(icon, window.X, window.Y, planet))
            CloseWindow(window);
    }

    private void OpenSelectedFinderItem(UIWindow window)
    {
        if (window == null)
            return;

        if (TryGetWindowView(window, out FinderWindowView view))
            finderNavigationController.OpenSelectedFinderItem(window, view);

        markDirty();
    }

    private void CloseOpenWindows()
    {
        foreach (UIWindow window in Windows.ToList())
            CloseWindow(window);
    }

    private void AddStrategyWindow(UIWindow window)
    {
        if (window == null || windows.Contains(window))
            return;

        if (TryGetWindowView(window, out IGalaxyMapSectorWindowView _))
            windows.Insert(0, window);
        else
            windows.Add(window);

        markDirty();
    }

    public void CloseWindow(UIWindow window)
    {
        if (window == null)
            return;

        clearClosingWindowState(window);
        if (windows.Remove(window))
            windowLayer.CloseWindow(window);

        markDirty();
    }

    private void SwapSectorWindow(UIWindow window)
    {
        if (!TryGetWindowView(window, out PlanetSystemWindowView view))
            return;

        int target =
            view.SectorPosition == SectorWindowPositions.Left
                ? SectorWindowPositions.Right
                : SectorWindowPositions.Left;
        UIWindow existing = FindSectorWindow(target);
        if (existing != null)
            CloseWindow(existing);

        Vector2Int position = windowPlacementController.GetSectorWindowPosition(target);
        view.InitializeWindow(view.Sector, target);
        window.MoveTo(position.x, position.y);
        markDirty();
    }

    private void ExecuteScrapConfirmAction(
        UIWindow window,
        ConfirmDialogWindowView view,
        bool confirmed
    )
    {
        if (confirmed && confirmActionController.ExecuteScrap(view))
        {
            ClearWindowSelection(view.SourceWindow);
            rebuildSnapshot();
        }

        CloseWindow(window);
    }

    private void ExecuteRetireConfirmAction(
        UIWindow window,
        ConfirmDialogWindowView view,
        bool confirmed
    )
    {
        if (confirmed && confirmActionController.ExecuteRetire(view))
        {
            ClearWindowSelection(view.SourceWindow);
            rebuildSnapshot();
        }

        CloseWindow(window);
    }

    private void ExecuteMoveConfirmAction(
        UIWindow window,
        ConfirmDialogWindowView view,
        bool confirmed
    )
    {
        if (confirmed)
            TryExecuteMove(view.SourceWindow, view.MoveTarget, view.Items);

        CloseWindow(window);
    }

    private bool TryExecuteMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        List<ISceneNode> items
    )
    {
        if (!confirmActionController.TryExecuteMove(target, items, PlayerFactionId))
            return false;

        ClearWindowSelection(sourceWindow);
        rebuildSnapshot();
        markDirty();
        return true;
    }

    private void ClearWindowSelection(UIWindow window)
    {
        if (TryGetWindowView(window, out IStrategyWindowSelectionView view))
            view.ClearSelection();
    }

    private List<ISceneNode> GetScrapItems(UIWindow window)
    {
        if (window == null)
            return new List<ISceneNode>();

        if (TryGetWindowView(window, out IStrategyWindowScrapItemsView scrapItemsView))
            return scrapItemsView.GetScrapItems();

        return GetContextItems(window);
    }

    private List<ISceneNode> GetContextItems(UIWindow window)
    {
        return TryGetWindowView(window, out IStrategyWindowContextItemsView view)
            ? view.GetContextItems()
            : new List<ISceneNode>();
    }

    private UIWindow OpenPlanetWindow<TView>(
        Func<int, int, int, TView> openWindow,
        GalaxyMapPlanet planet,
        Vector2Int position
    )
        where TView : MonoBehaviour, IPlanetIconWindowView
    {
        UIWindow window = CreateWindow<TView>(openWindow, position.x, position.y, out TView view);
        view.InitializeWindow(planet);
        AddStrategyWindow(window);
        return window;
    }

    private UIWindow CreateWindow<TView>(
        Func<int, int, int, TView> openWindow,
        int x,
        int y,
        out TView view
    )
        where TView : MonoBehaviour
    {
        view = openWindow(nextWindowId++, x, y);
        InitializeWindowServices(view);
        return view.GetComponent<UIWindow>();
    }

    private void InitializeWindowServices(MonoBehaviour view)
    {
        if (view is IConstructionWindowControllerReceiver constructionReceiver)
            constructionReceiver.InitializeConstruction(constructionWindowController);
    }

    private UIWindow FindWindow<TView>()
        where TView : class
    {
        return windows.FirstOrDefault(window => TryGetWindowView(window, out TView _));
    }

    private UIWindow FindFinderWindow(FinderMode mode)
    {
        return windows.FirstOrDefault(window =>
            TryGetWindowView(window, out FinderWindowView view) && view.Mode == mode
        );
    }

    private UIWindow FindSectorWindow(int position)
    {
        return windows.FirstOrDefault(window =>
            TryGetWindowView(window, out PlanetSystemWindowView view)
            && view.SectorPosition == position
        );
    }

    private UIWindow FindPlanetWindow(PlanetIcon icon, GalaxyMapPlanet planet)
    {
        return windows.FirstOrDefault(window =>
            GetPlanetWindowIcon(window) == icon && GetWindowPlanet(window) == planet
        );
    }

    private PlanetIcon GetPlanetWindowIcon(UIWindow window)
    {
        return TryGetWindowView(window, out IPlanetIconWindowView view)
            ? view.PlanetIcon
            : PlanetIcon.None;
    }

    private GalaxyMapPlanet GetWindowPlanet(UIWindow window)
    {
        return TryGetWindowView(window, out IGalaxyMapPlanetWindowView view)
            ? view.GalaxyMapPlanet
            : null;
    }

    private Planet GetAuthoritativeWindowPlanet(GalaxyMapPlanet planet)
    {
        string planetId = planet?.Planet?.InstanceID;
        return string.IsNullOrEmpty(planetId)
            ? null
            : gameManager.GetGame()?.GetSceneNodeByInstanceID<Planet>(planetId);
    }

    private static PlanetSystemWindowView GetPlanetSystemWindowView(UIWindow window)
    {
        if (window == null)
            return null;

        return window.TryGetContent(out PlanetSystemWindowView view) ? view : null;
    }

    private static List<ISceneNode> ToSceneNodeList(IReadOnlyList<ISceneNode> items)
    {
        return items?.ToList() ?? new List<ISceneNode>();
    }
}
