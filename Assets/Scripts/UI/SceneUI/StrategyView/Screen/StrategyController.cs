using System;
using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StrategyController
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerMoveHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler
{
    private const int _bookmarkSlotCount = 12;

    [SerializeField]
    private RectTransform strategySurface;

    [SerializeField]
    private RawImage strategySurfaceImage;

    [SerializeField]
    private StrategyHudView strategyHud;

    [SerializeField]
    private StrategyContextMenuPresenter strategyContextMenu;

    [SerializeField]
    private StrategyOverlayView strategyOverlay;

    [SerializeField]
    private RectTransform strategyWindowLayer;

    [SerializeField]
    private StrategyWindowLayerView strategyWindowLayerView;

    [SerializeField]
    private GalaxyMapView galaxyMap;

    [SerializeField]
    private BookmarkBarView bookmarkBar;

    private readonly string[] tracks =
    {
        "Audio/Music/battle_of_endor_medley_2",
        "Audio/Music/main_title_death_star_tatooine_emperor",
        "Audio/Music/emperor_arrives_death_of_yoda_obi_wan_revelation_stinger",
        "Audio/Music/emperor_arrives_death_of_yoda_obi_wan_revelation",
        "Audio/Music/imperial_march",
        "Audio/Music/battle_of_hoth_medley",
    };

    private readonly List<GalaxyMapSector> sectors = new List<GalaxyMapSector>();
    private GameManager gameManager;
    private RectTransform screenRect;
    private RectTransform galaxyMapLayer;
    private RectTransform windowLayer;
    private RectTransform hudLayer;
    private RectTransform bookmarkLayer;
    private RectTransform contextMenuLayer;
    private RectTransform overlayLayer;
    private int lastTick = -1;
    private bool dirty = true;
    private bool useUpperButtonLayout;
    private string playerFactionId;
    private GalaxyMap visibleGalaxyMap;
    private CancelStack cancelStack;
    private UIContext uiContext;
    private TargetingController targetingController;
    private ContextMenuController contextMenuController;
    private StrategyUIRuntime uiRuntime;
    private FleetWindowController fleetWindowController;
    private ConstructionWindowController constructionWindowController;
    private FacilityWindowController facilityWindowController;
    private DefenseWindowController defenseWindowController;
    private PlanetSystemWindowController planetSystemWindowController;
    private MissionsWindowController missionsWindowController;
    private MissionCreateWindowController missionCreateWindowController;
    private StrategyConfirmActionController confirmActionController;
    private StrategyContextMenuRouter strategyContextMenuRouter;
    private BookmarkController bookmarkController;
    private StrategyDragController strategyDragController;
    private StrategyWindowPlacementController windowPlacementController;
    private StrategyWindowCommandController windowCommandController;
    private StrategyScreenInputController inputController;
    private bool windowMovePreviewVisible;
    private RectInt windowMovePreviewBounds;
    private IReadOnlyList<UIWindow> windows =>
        windowCommandController?.Windows ?? Array.Empty<UIWindow>();

    public void Initialize(GameManager manager, UIContext context)
    {
        if (manager == null)
            throw new InvalidOperationException(
                "StrategyController.Initialize received null GameManager."
            );

        if (context == null)
            throw new InvalidOperationException(
                "StrategyController.Initialize received null UIContext."
            );

        if (gameManager != null)
            throw new InvalidOperationException("StrategyController.Initialize called twice.");

        gameManager = manager;
        uiContext = context;
        cancelStack = AppBootstrap.Instance?.GetCancelStack();

        targetingController = new TargetingController(strategyOverlay);
        contextMenuController = new ContextMenuController();
        uiRuntime = new StrategyUIRuntime(uiContext, targetingController, contextMenuController);
        bookmarkController = new BookmarkController(uiContext, _bookmarkSlotCount);
        windowPlacementController = new StrategyWindowPlacementController(
            uiContext,
            strategyWindowLayerView
        );
        strategyWindowLayerView.Initialize(uiRuntime);
        fleetWindowController = new FleetWindowController();
        constructionWindowController = new ConstructionWindowController(gameManager);
        facilityWindowController = new FacilityWindowController(
            constructionWindowController,
            () => windows
        );
        facilityWindowController.Initialize(uiRuntime);
        defenseWindowController = new DefenseWindowController();
        planetSystemWindowController = new PlanetSystemWindowController();
        missionsWindowController = new MissionsWindowController();
        missionCreateWindowController = new MissionCreateWindowController(gameManager);
        confirmActionController = new StrategyConfirmActionController(gameManager);
        windowCommandController = new StrategyWindowCommandController(
            gameManager,
            strategyWindowLayerView,
            windowPlacementController,
            bookmarkController,
            constructionWindowController,
            missionCreateWindowController,
            confirmActionController,
            () => sectors,
            () => playerFactionId,
            GetSystemSourcePosition,
            ClearClosingWindowState,
            RebuildSnapshot,
            MarkDirty
        );
        strategyWindowLayerView.WindowButtonRequested += HandleWindowShellButtonRequested;
        strategyWindowLayerView.WindowFocused += HandleWindowShellFocused;
        strategyWindowLayerView.WindowMoved += HandleWindowShellMoved;
        strategyWindowLayerView.WindowMovePreviewChanged += HandleWindowMovePreviewChanged;
        strategyWindowLayerView.WindowMovePreviewEnded += HandleWindowMovePreviewEnded;
        strategyWindowLayerView.WindowCloseRequested += HandleWindowCloseRequested;
        strategyWindowLayerView.WindowContextRequested += HandleWindowContextRequested;
        strategyWindowLayerView.ModalWindowOpened += HandleWindowModalOpened;
        strategyWindowLayerView.WindowClosed += HandleAnyWindowClosed;
        fleetWindowController.Initialize(uiRuntime, windowCommandController);
        defenseWindowController.Initialize(uiRuntime, windowCommandController);
        planetSystemWindowController.Initialize(uiRuntime, windowCommandController);
        strategyDragController = new StrategyDragController(
            targetingController,
            strategyWindowLayerView,
            windowCommandController.GetWindowById,
            TryGetSourcePosition,
            windowCommandController
        );
        strategyContextMenuRouter = new StrategyContextMenuRouter(
            strategyContextMenu,
            contextMenuController,
            new IStrategyContextMenuProvider[]
            {
                facilityWindowController,
                defenseWindowController,
                fleetWindowController,
                missionsWindowController,
                planetSystemWindowController,
            },
            windowCommandController
        );
        strategyContextMenu.CommandSelected += HandleContextMenuCommandSelected;
        strategyContextMenu.DismissRequested += HandleContextMenuDismissRequested;
        inputController = new StrategyScreenInputController(
            strategyContextMenu,
            galaxyMap,
            targetingController,
            contextMenuController,
            strategyContextMenuRouter,
            windowCommandController,
            strategyDragController,
            TryGetSourcePosition,
            strategyWindowLayerView.GetWindow,
            MarkDirty,
            RenderOverlay
        );
        RegisterCancelHandlers();
        RegisterUIRequestHandlers();
        OnGameReady();
    }

    public void RefreshGalaxyView()
    {
        if (gameManager == null)
            return;

        RebuildSnapshot();
        lastTick = gameManager.GetCurrentTick();
        Render();
    }

    private void OnGameReady()
    {
        AudioManager.Instance.PlayPlaylistPaths(tracks, true);

        EnsureStrategySurface();
        RebuildSnapshot();
        lastTick = gameManager.GetCurrentTick();
        Render();
    }

    private void OnDisable()
    {
        UnregisterCancelHandlers();
        targetingController?.Cancel();
        contextMenuController?.Cancel();
        strategyContextMenu?.Reset();
        ClearWindowMovePreview();
    }

    private void RegisterUIRequestHandlers()
    {
        uiContext.Dispatcher.Register<StrategyUIRequests.OpenPlanetSystemWindow>(
            HandleOpenPlanetSystemWindowRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.OpenPlanetWindow>(
            HandleOpenPlanetWindowRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.StartWindowItemDrag>(
            HandleStartWindowItemDragRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.WindowItemDragMove>(
            HandleWindowItemDragMoveRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.WindowItemDragEnd>(
            HandleWindowItemDragEndRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.OpenSelectedFinderItem>(
            HandleOpenSelectedFinderItemRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.ReleaseWindowButton>(
            HandleReleaseWindowButtonRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.ReleaseHudButton>(
            HandleReleaseHudButtonRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.OpenMessagesTab>(
            HandleOpenMessagesTabRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.OpenSpeedContextMenu>(
            HandleOpenSpeedContextMenuRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.RequestRender>(HandleRequestRender);
        uiContext.Dispatcher.Register<StrategyUIRequests.CloseWindow>(HandleCloseWindowRequest);
        uiContext.Dispatcher.Register<StrategyUIRequests.OpenConstructionInfo>(
            HandleOpenConstructionInfoRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.OpenStatusInfo>(
            HandleOpenStatusInfoRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.StartConstruction>(
            HandleStartConstructionRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.ConfirmDialogChoice>(
            HandleConfirmDialogChoiceRequest
        );
        uiContext.Dispatcher.Register<StrategyUIRequests.ExecuteMissionCreateCommand>(
            HandleExecuteMissionCreateCommandRequest
        );
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        gameManager.Update();
        int currentTick = gameManager.GetCurrentTick();
        if (currentTick != lastTick)
        {
            lastTick = currentTick;
            RebuildSnapshot();
            dirty = true;
        }

        if (dirty)
            Render();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        inputController?.OnPointerDown(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputController?.OnPointerUp(eventData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        inputController?.OnPointerMove(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        inputController?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        inputController?.OnPointerUp(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        inputController?.OnPointerClick(eventData);
    }

    private void EnsureStrategySurface()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            throw new InvalidOperationException("StrategyController must be under a Canvas.");

        if (transform is not RectTransform)
            throw new MissingReferenceException("StrategyController is missing RectTransform.");

        EnsureHudLayer();

        screenRect = strategySurface;
        if (screenRect == null)
            throw new MissingReferenceException("Viewport is missing RectTransform.");

        if (strategySurfaceImage == null)
            throw new MissingReferenceException("Viewport is missing RawImage.");

        EnsureWindowLayer();
        EnsureOverlayLayer();
        EnsureGalaxyMapLayer();
        EnsureBookmarkLayer();
        EnsureContextMenuLayer();

        FitSurfaceToCanvas();
    }

    private void EnsureHudLayer()
    {
        if (strategyHud == null)
            throw new MissingReferenceException("StrategyHud is missing StrategyHudView.");

        hudLayer = RequireRectTransform(strategyHud, "StrategyHud");
    }

    private void EnsureContextMenuLayer()
    {
        if (strategyContextMenu == null)
            throw new MissingReferenceException(
                "ContextMenu is missing StrategyContextMenuPresenter."
            );

        contextMenuLayer = RequireRectTransform(strategyContextMenu, "ContextMenu");
        if (uiContext != null)
            strategyContextMenu.Initialize(uiContext);
    }

    private void EnsureWindowLayer()
    {
        windowLayer = strategyWindowLayer;
        if (windowLayer == null)
            throw new MissingReferenceException("Windows is missing RectTransform.");

        if (strategyWindowLayerView == null)
            throw new MissingReferenceException("Windows is missing StrategyWindowLayerView.");

        if (uiRuntime != null)
            strategyWindowLayerView.Initialize(uiRuntime);
    }

    private void EnsureOverlayLayer()
    {
        if (strategyOverlay == null)
            throw new MissingReferenceException("Overlay is missing StrategyOverlayView.");

        overlayLayer = RequireRectTransform(strategyOverlay, "Overlay");
        strategyOverlay.TargetingCancelRequested -= HandleTargetingCancelRequested;
        strategyOverlay.TargetingCancelRequested += HandleTargetingCancelRequested;
    }

    private void EnsureGalaxyMapLayer()
    {
        if (galaxyMap == null)
            throw new MissingReferenceException("GalaxyMap is missing GalaxyMapView.");

        galaxyMapLayer = RequireRectTransform(galaxyMap, "GalaxyMap");

        if (uiContext != null)
            galaxyMap.Initialize(uiContext);
    }

    private void EnsureBookmarkLayer()
    {
        if (bookmarkBar == null)
            throw new MissingReferenceException("Bookmarks is missing BookmarkBarView.");

        bookmarkLayer = RequireRectTransform(bookmarkBar, "Bookmarks");
        bookmarkBar.BookmarkRequested -= HandleBookmarkRequested;
        bookmarkBar.BookmarkRequested += HandleBookmarkRequested;
    }

    private void HandleBookmarkRequested(int index)
    {
        if (windowCommandController.TryRestoreBookmark(index))
            dirty = true;
    }

    private static RectTransform RequireRectTransform(Component component, string label)
    {
        if (component != null && component.transform is RectTransform rectTransform)
            return rectTransform;

        throw new MissingReferenceException($"{label} is missing RectTransform.");
    }

    private void FitSurfaceToCanvas()
    {
        RectTransform parent = screenRect.parent as RectTransform;
        if (parent == null)
            return;

        Vector2Int surfaceSize = GetSurfaceSize();
        if (surfaceSize.x <= 0 || surfaceSize.y <= 0)
            return;

        float scale = Mathf.Min(
            parent.rect.width / surfaceSize.x,
            parent.rect.height / surfaceSize.y
        );
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0)
            scale = 1f;

        Vector3 scaleVector = new Vector3(scale, scale, 1f);
        SetLayerScale(galaxyMapLayer, scaleVector);
        SetLayerScale(screenRect, scaleVector);
        SetLayerScale(hudLayer, scaleVector);
        SetLayerScale(bookmarkLayer, scaleVector);
        SetLayerScale(windowLayer, scaleVector);
        SetLayerScale(overlayLayer, scaleVector);
        SetLayerScale(contextMenuLayer, scaleVector);
    }

    private Vector2Int GetSurfaceSize()
    {
        if (screenRect == null)
            return Vector2Int.zero;

        int width = Mathf.RoundToInt(screenRect.sizeDelta.x);
        int height = Mathf.RoundToInt(screenRect.sizeDelta.y);
        if (width <= 0)
            width = Mathf.RoundToInt(screenRect.rect.width);
        if (height <= 0)
            height = Mathf.RoundToInt(screenRect.rect.height);

        return new Vector2Int(width, height);
    }

    private static void SetLayerScale(RectTransform layer, Vector3 scale)
    {
        if (layer != null)
            layer.localScale = scale;
    }

    private void RebuildSnapshot()
    {
        sectors.Clear();

        Faction playerFaction = gameManager.GetPlayerFaction();
        playerFactionId = playerFaction?.InstanceID ?? string.Empty;
        useUpperButtonLayout = uiContext?.GetPlayerFactionTheme()?.UseUpperButtonLayout == true;
        visibleGalaxyMap = null;
        if (playerFaction == null)
            return;

        GalaxyMap galaxy = gameManager.GetFogOfWarSystem().BuildFactionView(playerFaction);
        visibleGalaxyMap = galaxy;

        if (galaxy?.PlanetSystems == null)
            return;

        foreach (PlanetSystem system in galaxy.PlanetSystems)
        {
            GalaxyMapSector sector = new GalaxyMapSector(system);

            foreach (Planet planet in system.Planets)
            {
                GalaxyMapPlanet strategyPlanet = new GalaxyMapPlanet(
                    system,
                    planet,
                    planet.GetPlanetIconPath()
                );
                strategyPlanet.Sector = sector;
                sector.Planets.Add(strategyPlanet);
            }

            sectors.Add(sector);
        }

        ReconcileOpenWindows();
    }

    private void ReconcileOpenWindows()
    {
        windowCommandController.ReconcileWindows(sectors);
    }

    private void Render()
    {
        FitSurfaceToCanvas();
        strategyWindowLayerView.BeginRender();
        try
        {
            RenderGalaxyMap();
            RenderBookmarks();

            windowCommandController.RenderWindows(
                new StrategyWindowRenderContext(
                    gameManager,
                    visibleGalaxyMap,
                    sectors,
                    gameManager?.GetPlayerFaction(),
                    playerFactionId,
                    useUpperButtonLayout
                )
            );
        }
        finally
        {
            strategyWindowLayerView.EndRender();
        }

        RenderHud();
        RenderContextMenu();
        RenderOverlay();
        dirty = false;
    }

    private void RenderHud()
    {
        if (strategyHud == null)
            return;

        Faction faction = gameManager.GetPlayerFaction();
        StrategyHudRenderData data = new StrategyHudRenderData
        {
            TickText = gameManager.GetCurrentTick().ToString(),
            RawMaterialsText = faction?.RawMaterials.ToString() ?? "0",
            RefinedMaterialsText = faction?.RefinedMaterials.ToString() ?? "0",
            MaintenanceText = faction?.MaintenanceHeadroom.ToString() ?? "0",
            Speed = gameManager.GetGameSpeed(),
            UnreadMessageTypes = GetUnreadMessageTypes(faction),
        };

        strategyHud.Initialize(uiContext);
        strategyHud.Render(data);
    }

    private static HashSet<MessageType> GetUnreadMessageTypes(Faction faction)
    {
        HashSet<MessageType> types = new HashSet<MessageType>();
        if (faction?.Messages == null)
            return types;

        foreach (KeyValuePair<MessageType, List<Message>> entry in faction.Messages)
        {
            foreach (Message message in entry.Value ?? new List<Message>())
            {
                if (message?.Read == false)
                {
                    types.Add(entry.Key);
                    break;
                }
            }
        }

        return types;
    }

    private void RenderContextMenu()
    {
        if (strategyContextMenu == null)
            return;

        strategyContextMenu.RenderCurrent();
    }

    private void RenderGalaxyMap()
    {
        if (galaxyMap == null)
            return;

        galaxyMap.Render(sectors, playerFactionId);
    }

    private void RenderBookmarks()
    {
        if (bookmarkBar == null)
            return;

        bookmarkBar.Render(
            bookmarkController.BuildRenderData(),
            uiContext.GetPlayerFactionTheme()?.StrategyBookmarkLayout
        );
    }

    private void RenderOverlay()
    {
        if (strategyOverlay == null)
            return;

        StrategyOverlayRenderData data = new StrategyOverlayRenderData();
        strategyDragController?.ApplyOverlay(data, IsContextMenuOpen());
        if (windowMovePreviewVisible)
        {
            data.DragFrameVisible = true;
            data.DragFrameX = windowMovePreviewBounds.x;
            data.DragFrameY = windowMovePreviewBounds.y;
            data.DragFrameWidth = windowMovePreviewBounds.width;
            data.DragFrameHeight = windowMovePreviewBounds.height;
        }

        strategyOverlay.Render(data);
    }

    private void RebuildUIContext()
    {
        uiContext = new UIContext(gameManager.GetGame(), new FactionThemeLibrary());
        targetingController?.Cancel();
        contextMenuController?.Cancel();
        strategyDragController?.Clear();
        ClearWindowMovePreview();
        uiRuntime = new StrategyUIRuntime(uiContext, targetingController, contextMenuController);
        windowPlacementController.SetContext(uiContext);
        fleetWindowController.Initialize(uiRuntime, windowCommandController);
        facilityWindowController.Initialize(uiRuntime);
        defenseWindowController.Initialize(uiRuntime, windowCommandController);
        planetSystemWindowController.Initialize(uiRuntime, windowCommandController);
        RegisterUIRequestHandlers();
        strategyWindowLayerView?.Initialize(uiRuntime);
        strategyContextMenu?.Initialize(uiContext);
        galaxyMap?.Initialize(uiContext);
    }

    private void HandleOpenPlanetSystemWindowRequest(
        StrategyUIRequests.OpenPlanetSystemWindow request
    )
    {
        if (request == null || !CanInteractWithGalaxy())
            return;

        if (
            windowCommandController.OpenPlanetSystemWindow(
                request.System,
                request.SourceX,
                request.SourceY
            )
        )
            dirty = true;
    }

    private void HandleOpenPlanetWindowRequest(StrategyUIRequests.OpenPlanetWindow request)
    {
        if (request == null)
            return;

        windowCommandController.OpenPlanetWindow(
            request.Planet,
            request.Icon,
            request.SourceX,
            request.SourceY
        );
        dirty = true;
    }

    private void HandleStartWindowItemDragRequest(StrategyUIRequests.StartWindowItemDrag request)
    {
        inputController.StartWindowItemDrag(request);
    }

    private void HandleWindowItemDragMoveRequest(StrategyUIRequests.WindowItemDragMove request)
    {
        inputController.MoveWindowItemDrag(request);
    }

    private void HandleWindowItemDragEndRequest(StrategyUIRequests.WindowItemDragEnd request)
    {
        inputController.EndWindowItemDrag(request);
    }

    private void HandleOpenSelectedFinderItemRequest(
        StrategyUIRequests.OpenSelectedFinderItem request
    )
    {
        windowCommandController.OpenSelectedFinderItem(request.WindowId);
        dirty = true;
    }

    private void HandleReleaseWindowButtonRequest(StrategyUIRequests.ReleaseWindowButton request)
    {
        UIWindow window = windowCommandController.GetWindowById(request.WindowId);
        if (window == null || request.Action == 0)
            return;

        inputController.SuppressNextClick();
        windowCommandController.ExecuteWindowButton(window, request.Action);
        dirty = true;
    }

    private void HandleWindowShellButtonRequested(int windowId, int action)
    {
        UIWindow window = windowCommandController.GetWindowById(windowId);
        if (window == null || action == 0)
            return;

        inputController?.SuppressNextClick();
        windowCommandController.ExecuteWindowButton(window, action);
        dirty = true;
    }

    private void HandleWindowCloseRequested(int windowId)
    {
        windowCommandController.CloseWindow(windowId);
        dirty = true;
    }

    private void HandleWindowShellFocused(int windowId)
    {
        UIWindow window = windowCommandController.GetWindowById(windowId);
        if (window == null)
            return;

        windowCommandController.FocusWindow(window);
        dirty = true;
    }

    private void HandleWindowShellMoved(int windowId)
    {
        ClearWindowMovePreview();
        dirty = true;
    }

    private void HandleWindowMovePreviewChanged(int windowId, RectInt bounds)
    {
        windowMovePreviewVisible = true;
        windowMovePreviewBounds = bounds;
        RenderOverlay();
    }

    private void HandleWindowMovePreviewEnded(int windowId)
    {
        ClearWindowMovePreview();
        RenderOverlay();
    }

    private void HandleWindowContextRequested(
        UIWindow window,
        PointerEventData eventData,
        int sourceX,
        int sourceY
    )
    {
        targetingController?.Cancel();
        strategyContextMenuRouter.OpenContextMenu(window, eventData, sourceX, sourceY);
        dirty = true;
    }

    private void HandleContextMenuCommandSelected(StrategyMenuCommand command)
    {
        inputController?.SuppressNextClick();
        if (contextMenuController?.IsOpen == true)
            strategyContextMenuRouter.SelectRuntimeContextMenu(command);
        else
            strategyContextMenuRouter.ExecuteContextAction(command.Action);

        dirty = true;
    }

    private void HandleContextMenuDismissRequested(PointerEventData eventData)
    {
        contextMenuController?.Cancel();
        strategyContextMenu?.Reset();
        inputController?.SuppressNextClick();

        if (
            eventData?.button == PointerEventData.InputButton.Right
            && TryGetSourcePosition(eventData, eventData.position, out int sourceX, out int sourceY)
        )
        {
            strategyContextMenuRouter.OpenContextMenu(eventData, sourceX, sourceY);
        }

        dirty = true;
    }

    private void HandleWindowModalOpened()
    {
        targetingController?.Cancel();
        contextMenuController?.Cancel();
        strategyContextMenu?.Reset();
        dirty = true;
    }

    private void HandleAnyWindowClosed()
    {
        targetingController?.Cancel();
        ClearWindowMovePreview();
        dirty = true;
    }

    private void HandleReleaseHudButtonRequest(StrategyUIRequests.ReleaseHudButton request)
    {
        if (request.Action != 0)
            windowCommandController.ExecuteMainButton(request.Action);

        dirty = true;
    }

    private void HandleOpenMessagesTabRequest(StrategyUIRequests.OpenMessagesTab request)
    {
        windowCommandController.OpenMessagesWindow(request.Tab);
        dirty = true;
    }

    private void HandleOpenSpeedContextMenuRequest(StrategyUIRequests.OpenSpeedContextMenu request)
    {
        targetingController?.Cancel();
        strategyContextMenuRouter.OpenSpeedContextMenu(request.SourceX, request.SourceY);
        dirty = true;
    }

    private void HandleRequestRender(StrategyUIRequests.RequestRender request)
    {
        dirty = true;
    }

    private void HandleCloseWindowRequest(StrategyUIRequests.CloseWindow request)
    {
        windowCommandController.CloseWindow(request.WindowId);
        dirty = true;
    }

    private void HandleOpenConstructionInfoRequest(StrategyUIRequests.OpenConstructionInfo request)
    {
        windowCommandController.OpenConstructionInfo(request.WindowId);
        dirty = true;
    }

    private void HandleOpenStatusInfoRequest(StrategyUIRequests.OpenStatusInfo request)
    {
        windowCommandController.OpenStatusInfo(request.WindowId);
        dirty = true;
    }

    private void HandleStartConstructionRequest(StrategyUIRequests.StartConstruction request)
    {
        windowCommandController.StartConstruction(request);
    }

    private void HandleConfirmDialogChoiceRequest(StrategyUIRequests.ConfirmDialogChoice request)
    {
        windowCommandController.ExecuteConfirmDialogChoice(request);
    }

    private void HandleExecuteMissionCreateCommandRequest(
        StrategyUIRequests.ExecuteMissionCreateCommand request
    )
    {
        windowCommandController.ExecuteMissionCreateCommand(request);
    }

    private void HandleTargetingCancelRequested()
    {
        inputController?.CancelTargeting();
    }

    private void ClearClosingWindowState(UIWindow window)
    {
        inputController?.ClearWindow(window);
        strategyDragController?.ClearWindow(window);

        if (targetingController?.IsTargeting == true)
            targetingController.Cancel();

        if (
            contextMenuController?.ActiveRequest?.Source is IStrategyContextMenuSource source
            && source.Window == window
        )
            contextMenuController.Cancel();

        if (strategyContextMenu != null && strategyContextMenu.Window == window)
            strategyContextMenu.Reset();
    }

    private Vector2Int GetSystemSourcePosition(GalaxyMapSector sector)
    {
        return galaxyMap != null && sector?.System != null
            ? galaxyMap.GetSystemSourcePosition(sector.System)
            : Vector2Int.zero;
    }

    private void UpdateCurrentTick()
    {
        lastTick = gameManager.GetCurrentTick();
    }

    private void MarkDirty()
    {
        dirty = true;
    }

    private void ClearWindowMovePreview()
    {
        windowMovePreviewVisible = false;
        windowMovePreviewBounds = default;
    }

    private void RegisterCancelHandlers()
    {
        cancelStack?.Register(strategyWindowLayerView);
        cancelStack?.Register(inputController);
        cancelStack?.Register(strategyContextMenuRouter);
    }

    private void UnregisterCancelHandlers()
    {
        cancelStack?.Unregister(strategyContextMenuRouter);
        cancelStack?.Unregister(inputController);
        cancelStack?.Unregister(strategyWindowLayerView);
    }

    private bool CanInteractWithGalaxy()
    {
        return !IsContextMenuOpen() && !windowCommandController.HasModalWindow();
    }

    private bool IsContextMenuOpen()
    {
        return contextMenuController?.IsOpen == true
            || strategyContextMenu != null && strategyContextMenu.Open;
    }

    private bool TryGetSourcePosition(PointerEventData eventData, out int x, out int y)
    {
        return TryGetSourcePosition(
            eventData,
            eventData == null ? Vector2.zero : eventData.position,
            out x,
            out y
        );
    }

    private bool TryGetSourcePosition(
        PointerEventData eventData,
        Vector2 screenPosition,
        out int x,
        out int y
    )
    {
        x = 0;
        y = 0;

        if (eventData == null)
            return false;

        if (screenRect == null)
            EnsureStrategySurface();

        if (screenRect == null)
            return false;

        Camera camera = eventData.pressEventCamera;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                screenRect,
                screenPosition,
                camera,
                out Vector2 local
            )
        )
        {
            return false;
        }

        Vector2Int surfaceSize = GetSurfaceSize();
        if (surfaceSize.x <= 0 || surfaceSize.y <= 0)
            return false;

        x = Mathf.RoundToInt(local.x + surfaceSize.x / 2f);
        y = Mathf.RoundToInt(surfaceSize.y / 2f - local.y);
        return x >= 0 && x < surfaceSize.x && y >= 0 && y < surfaceSize.y;
    }
}
