using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Composes authored strategy views with feature controllers and owns screen-level lifecycle.
/// </summary>
public sealed class StrategyController
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerMoveHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler,
        IStrategyHudActions,
        IGalacticInformationDisplayActions,
        IGalaxyMapActions,
        IConstructionWindowActions,
        IFacilityWindowActions,
        IFleetWindowActions,
        IDefenseWindowActions,
        IPlanetSystemWindowActions,
        IMissionsWindowActions,
        IMissionCreateWindowActions,
        IMessagesWindowActions,
        IStatusWindowActions,
        IBattleAlertWindowActions
{
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
    private StrategyWindowLayerView strategyWindowLayerView;

    [SerializeField]
    private UIWindowManager strategyWindowManager;

    [SerializeField]
    private GalaxyMapView galaxyMap;

    [SerializeField]
    private GalacticInformationDisplayView galacticInformationDisplay;

    [SerializeField]
    private GalacticInformationLegendView galacticInformationLegend;

    [SerializeField]
    private BookmarkBarView bookmarkBar;

    /// <summary>
    /// Contains the resource paths sequenced by the strategy music playlist.
    /// </summary>
    private static readonly string[] _strategyMusicTracks =
    {
        "Audio/Music/battle_of_endor_medley_2",
        "Audio/Music/main_title_death_star_tatooine_emperor",
        "Audio/Music/emperor_arrives_death_of_yoda_obi_wan_revelation_stinger",
        "Audio/Music/emperor_arrives_death_of_yoda_obi_wan_revelation",
        "Audio/Music/imperial_march",
        "Audio/Music/battle_of_hoth_medley",
    };

    private CancelStack cancelStack;
    private GameManager gameManager;
    private MessageSystem messageSystem;
    private UIContext uiContext;

    private bool dirty = true;
    private int lastTick = -1;
    private bool initialized;
    private bool cancelHandlersRegistered;
    private RectInt windowMovePreviewBounds;
    private bool windowMovePreviewVisible;

    private StrategyHudController strategyHudController;
    private GalaxyMapController galaxyMapController;
    private GalacticInformationDisplayController galacticInformationDisplayController;
    private TargetingController targetingController;
    private ContextMenuController contextMenuController;
    private FleetWindowController fleetWindowController;
    private ConstructionWindowController constructionWindowController;
    private FacilityWindowController facilityWindowController;
    private DefenseWindowController defenseWindowController;
    private PlanetSystemWindowController planetSystemWindowController;
    private MissionsWindowController missionsWindowController;
    private MessagesWindowController messagesWindowController;
    private EncyclopediaWindowController encyclopediaWindowController;
    private FinderWindowController finderWindowController;
    private AdvisorReportWindowController advisorReportWindowController;
    private StatusWindowController statusWindowController;
    private BattleAlertWindowController battleAlertWindowController;
    private MissionCreateWindowController missionCreateWindowController;
    private ConfirmDialogWindowController confirmDialogWindowController;
    private AdvisorCommandController advisorCommandController;
    private StrategyContextMenuRouter strategyContextMenuRouter;
    private BookmarkController bookmarkController;
    private StrategyDragController strategyDragController;
    private StrategyWindowPlacementController windowPlacementController;
    private StrategyWindowCommandController windowCommandController;
    private StrategyScreenInputController inputController;

    /// <summary>
    /// Gets the sectors in the current visible galaxy snapshot.
    /// </summary>
    private IReadOnlyList<GalaxyMapSector> Sectors =>
        galaxyMapController?.Sectors ?? Array.Empty<GalaxyMapSector>();

    /// <summary>
    /// Gets the active player's faction identifier from the visible snapshot.
    /// </summary>
    private string PlayerFactionId => galaxyMapController?.PlayerFactionId ?? string.Empty;

    /// <summary>
    /// Initializes the strategy screen for the active game and UI context.
    /// </summary>
    /// <param name="manager">The active game manager.</param>
    /// <param name="context">The active strategy UI context.</param>
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

        ValidateAuthoredViews();
        gameManager = manager;
        uiContext = context;
        cancelStack = AppBootstrap.Instance?.GetCancelStack();
        strategyContextMenu.Initialize(uiContext);
        gameManager.GameSpeedChanged += MarkDirty;
        gameManager.GameReplaced += HandleGameReplaced;
        BindMessageSystem(gameManager.MessageSystem);

        InitializeScreenControllers();
        InitializeWindowControllers();
        InitializeInteractionControllers();
        BindWindowControllerActions();
        SubscribeViewEvents();
        initialized = true;
        RegisterCancelHandlers();
        OnGameReady();
    }

    /// <summary>
    /// Creates and binds the screen-level HUD and galaxy presentation controllers.
    /// </summary>
    private void InitializeScreenControllers()
    {
        strategyHudController = new StrategyHudController(
            () => gameManager?.GetPlayerFaction(),
            () => uiContext?.GetPlayerFactionTheme(),
            path => uiContext?.GetTexture(path),
            PlaySfx
        );
        strategyHudController.Initialize(this);
        strategyHudController.BindView(strategyHud);

        galaxyMapController = new GalaxyMapController(() => uiContext);
        galaxyMapController.Initialize(this);
        galaxyMapController.BindView(galaxyMap);
        galacticInformationDisplayController = new GalacticInformationDisplayController(
            () => uiContext,
            PlaySfx
        );
        galacticInformationDisplayController.Initialize(this);
        galacticInformationDisplayController.BindViews(
            galacticInformationDisplay,
            galacticInformationLegend
        );
    }

    /// <summary>
    /// Creates the shared window infrastructure and every feature-window controller.
    /// </summary>
    private void InitializeWindowControllers()
    {
        InitializeWindowInfrastructure();
        StrategyFleetCommandController fleetCommandController = new StrategyFleetCommandController(
            () => gameManager.GetGame(),
            () => gameManager.FleetSystem,
            (target, fleets) =>
                gameManager.ExecuteOrbitalBombardment(fleets, target, BombardmentType.General)
        );
        InitializeFeatureWindowControllers(fleetCommandController);
        InitializeSharedCommandControllers();
    }

    /// <summary>
    /// Creates the interaction, bookmark, placement, and authored presentation dependencies shared by windows.
    /// </summary>
    private void InitializeWindowInfrastructure()
    {
        targetingController = new TargetingController(strategyOverlay);
        contextMenuController = new ContextMenuController();
        bookmarkController = new BookmarkController(uiContext);
        windowPlacementController = new StrategyWindowPlacementController(
            uiContext,
            strategyWindowLayerView,
            strategyWindowManager
        );
        strategyWindowLayerView.RenderModalState(strategyWindowManager.HasModalWindow());
    }

    /// <summary>
    /// Creates each feature-window controller with its direct runtime dependencies.
    /// </summary>
    /// <param name="fleetCommandController">Executes fleet and bombardment game commands.</param>
    private void InitializeFeatureWindowControllers(
        StrategyFleetCommandController fleetCommandController
    )
    {
        fleetWindowController = new FleetWindowController(
            fleetCommandController,
            () => uiContext,
            targetingController,
            strategyWindowLayerView,
            strategyWindowManager,
            (x, y) => windowPlacementController.ClampPlanetWindowPosition(PlanetIcon.Fleet, x, y),
            MarkDirty
        );
        constructionWindowController = new ConstructionWindowController(
            () => gameManager.GetGame(),
            () => gameManager.ManufacturingSystem,
            () => gameManager.MovementSystem,
            () => uiContext,
            strategyWindowLayerView,
            strategyWindowManager,
            windowPlacementController.GetConstructionWindowPosition,
            windowPlacementController.GetUtilityWindowPosition,
            CloseWindow,
            MarkDirty
        );
        facilityWindowController = new FacilityWindowController(
            () => gameManager.GetGame(),
            constructionWindowController,
            () => uiContext,
            targetingController,
            strategyWindowLayerView,
            strategyWindowManager,
            (x, y) =>
                windowPlacementController.ClampPlanetWindowPosition(PlanetIcon.Facility, x, y),
            MarkDirty
        );
        defenseWindowController = new DefenseWindowController(
            () => uiContext,
            targetingController,
            strategyWindowLayerView,
            strategyWindowManager,
            (x, y) => windowPlacementController.ClampPlanetWindowPosition(PlanetIcon.Defense, x, y),
            MarkDirty
        );
        planetSystemWindowController = new PlanetSystemWindowController(
            fleetCommandController,
            () => uiContext,
            targetingController,
            strategyWindowLayerView,
            strategyWindowManager,
            () => Sectors,
            windowPlacementController.GetSectorWindowPosition,
            CloseWindow,
            MarkDirty
        );
        missionsWindowController = new MissionsWindowController(
            () => uiContext,
            galaxyMapController.FindVisibleNode,
            targetingController,
            strategyWindowLayerView,
            strategyWindowManager,
            (x, y) => windowPlacementController.ClampPlanetWindowPosition(PlanetIcon.Mission, x, y),
            MarkDirty
        );
        messagesWindowController = new MessagesWindowController(
            PlaySfx,
            () => uiContext,
            strategyWindowLayerView,
            strategyWindowManager,
            windowPlacementController.GetMessagesWindowPosition,
            CloseWindow,
            MarkDirty
        );
        encyclopediaWindowController = new EncyclopediaWindowController(
            () => uiContext,
            PlaySfx,
            strategyWindowLayerView,
            strategyWindowManager,
            windowPlacementController.GetEncyclopediaWindowPosition,
            MarkDirty
        );
        finderWindowController = new FinderWindowController(
            () => uiContext,
            PlaySfx,
            strategyWindowLayerView,
            strategyWindowManager,
            () => Sectors,
            windowPlacementController.GetFinderWindowPosition,
            OpenFinderTarget,
            CloseWindow,
            MarkDirty
        );
        advisorReportWindowController = new AdvisorReportWindowController(
            () => uiContext,
            strategyWindowLayerView,
            strategyWindowManager,
            windowPlacementController.GetAdvisorReportWindowPosition,
            CloseWindow,
            MarkDirty
        );
        statusWindowController = new StatusWindowController(
            () => uiContext,
            strategyWindowLayerView,
            strategyWindowManager,
            () => Sectors,
            galaxyMapController.FindVisibleNode,
            PlaySfx,
            windowPlacementController.GetStatusWindowPosition,
            CloseWindow,
            MarkDirty
        );
        battleAlertWindowController = new BattleAlertWindowController(
            () =>
                gameManager.SpaceCombatSystem.TryGetPendingCombat(out PendingCombatResult pending)
                    ? pending
                    : null,
            () => gameManager.ResolveCombatRetreat(PlayerFactionId),
            () => gameManager.ResolveCombat(true),
            () => uiContext,
            PlaySfx,
            PlayTrack,
            StopMusic,
            strategyWindowLayerView,
            strategyWindowManager,
            windowPlacementController.GetBattleAlertWindowPosition,
            CloseWindow,
            MarkDirty
        );
        missionCreateWindowController = new MissionCreateWindowController(
            () => gameManager.GetGame(),
            () => gameManager.MissionSystem,
            () => uiContext,
            PlaySfx,
            strategyWindowLayerView,
            strategyWindowManager,
            windowPlacementController.GetMissionCreateWindowPosition,
            CloseWindow,
            MarkDirty
        );
        confirmDialogWindowController = new ConfirmDialogWindowController(
            () => uiContext,
            PlaySfx,
            strategyWindowLayerView,
            strategyWindowManager,
            windowPlacementController.GetConfirmDialogWindowPosition,
            CloseWindow,
            MarkDirty
        );
    }

    /// <summary>
    /// Creates the advisor and shared strategy-window command handlers.
    /// </summary>
    private void InitializeSharedCommandControllers()
    {
        advisorCommandController = new AdvisorCommandController(
            gameManager,
            targetingController,
            () => Sectors,
            constructionWindowController.OpenFromAdvisor
        );
        windowCommandController = new StrategyWindowCommandController(
            missionCreateWindowController,
            confirmDialogWindowController,
            gameManager,
            PlaySfx,
            ClearWindowSelection,
            RebuildSnapshot,
            MarkDirty
        );
    }

    /// <summary>
    /// Connects each feature-window controller to its strategy action boundary.
    /// </summary>
    private void BindWindowControllerActions()
    {
        constructionWindowController.Initialize(this);
        facilityWindowController.Initialize(this, windowCommandController);
        fleetWindowController.Initialize(
            this,
            windowCommandController,
            windowCommandController,
            inputController.StartItemDrag,
            inputController.OnDrag,
            inputController.OnPointerUp
        );
        defenseWindowController.Initialize(
            this,
            windowCommandController,
            windowCommandController,
            inputController.StartItemDrag,
            inputController.OnDrag,
            inputController.OnPointerUp
        );
        planetSystemWindowController.Initialize(
            this,
            windowCommandController,
            windowCommandController,
            inputController.StartItemDrag
        );
        missionsWindowController.Initialize(this);
        missionCreateWindowController.Initialize(this);
        messagesWindowController.Initialize(this);
        statusWindowController.Initialize(this);
        battleAlertWindowController.Initialize(this);
    }

    /// <summary>
    /// Subscribes authored-view events used by the screen composition root.
    /// </summary>
    private void SubscribeViewEvents()
    {
        strategyWindowManager.WindowButtonRequested += HandleWindowShellButtonRequested;
        strategyWindowManager.WindowCloseRequested += HandleWindowCloseRequested;
        strategyWindowManager.WindowContextRequested += HandleWindowContextRequested;
        strategyWindowManager.WindowMovePreviewChanged += HandleWindowMovePreviewChanged;
        strategyWindowManager.WindowMovePreviewEnded += HandleWindowMovePreviewEnded;
        strategyWindowManager.WindowMoved += HandleWindowMoved;
        strategyWindowManager.FocusChanged += HandleWindowFocusChanged;
        strategyWindowManager.ModalOpened += HandleWindowModalOpened;
        strategyWindowManager.WindowClosed += HandleAnyWindowClosed;
        strategyContextMenu.CommandSelected += HandleContextMenuCommandSelected;
        strategyContextMenu.DismissRequested += HandleContextMenuDismissRequested;
        strategyOverlay.TargetingCancelRequested += HandleTargetingCancelRequested;
        bookmarkBar.BookmarkRequested += HandleBookmarkRequested;
    }

    /// <summary>
    /// Releases semantic events subscribed by the screen composition root.
    /// </summary>
    private void UnsubscribeViewEvents()
    {
        if (strategyWindowManager != null)
        {
            strategyWindowManager.WindowButtonRequested -= HandleWindowShellButtonRequested;
            strategyWindowManager.WindowCloseRequested -= HandleWindowCloseRequested;
            strategyWindowManager.WindowContextRequested -= HandleWindowContextRequested;
            strategyWindowManager.WindowMovePreviewChanged -= HandleWindowMovePreviewChanged;
            strategyWindowManager.WindowMovePreviewEnded -= HandleWindowMovePreviewEnded;
            strategyWindowManager.WindowMoved -= HandleWindowMoved;
            strategyWindowManager.FocusChanged -= HandleWindowFocusChanged;
            strategyWindowManager.ModalOpened -= HandleWindowModalOpened;
            strategyWindowManager.WindowClosed -= HandleAnyWindowClosed;
        }

        if (strategyContextMenu != null)
        {
            strategyContextMenu.CommandSelected -= HandleContextMenuCommandSelected;
            strategyContextMenu.DismissRequested -= HandleContextMenuDismissRequested;
        }

        if (strategyOverlay != null)
            strategyOverlay.TargetingCancelRequested -= HandleTargetingCancelRequested;
        if (bookmarkBar != null)
            bookmarkBar.BookmarkRequested -= HandleBookmarkRequested;
    }

    /// <summary>
    /// Creates shared drag, context-menu, and strategy-surface input routing.
    /// </summary>
    private void InitializeInteractionControllers()
    {
        strategyDragController = new StrategyDragController(
            targetingController,
            GetContextItems,
            TryGetDragPreview,
            TryGetSourcePosition,
            GetGalaxyMapDropTarget,
            () => PlayerFactionId,
            windowCommandController,
            strategyWindowLayerView.ItemDragStartDistance
        );
        strategyContextMenuRouter = new StrategyContextMenuRouter(
            strategyContextMenu,
            contextMenuController,
            strategyWindowManager,
            new IStrategyContextMenuProvider[]
            {
                constructionWindowController,
                facilityWindowController,
                defenseWindowController,
                fleetWindowController,
                missionsWindowController,
                planetSystemWindowController,
            }
        );
        inputController = new StrategyScreenInputController(
            galaxyMapController,
            targetingController,
            strategyContextMenuRouter,
            strategyWindowManager,
            TrySelectWindowPlanetTarget,
            TryOpenStatusWindow,
            strategyDragController,
            TryGetSourcePosition,
            MarkDirty,
            RenderOverlay
        );
    }

    /// <summary>
    /// Restores the shuffled strategy music playlist.
    /// </summary>
    private void ResumeStrategyMusic()
    {
        AudioManager.EnsureExists().PlayPlaylist(_strategyMusicTracks, true);
    }

    /// <summary>
    /// Plays a strategy sound effect through the shared audio manager.
    /// </summary>
    /// <param name="resourcePath">The Resources path of the sound effect.</param>
    private static void PlaySfx(string resourcePath)
    {
        AudioManager.EnsureExists().PlaySfx(resourcePath);
    }

    /// <summary>
    /// Plays one strategy music track through the shared audio manager.
    /// </summary>
    /// <param name="resourcePath">The Resources path of the music track.</param>
    private static void PlayTrack(string resourcePath)
    {
        AudioManager.EnsureExists().PlayTrack(resourcePath);
    }

    /// <summary>
    /// Stops strategy music through the shared audio manager.
    /// </summary>
    private static void StopMusic()
    {
        AudioManager.EnsureExists().StopMusic();
    }

    /// <summary>
    /// Starts strategy music and performs the initial render.
    /// </summary>
    private void OnGameReady()
    {
        ResumeStrategyMusic();
        RebuildSnapshot();
        lastTick = gameManager.GetCurrentTick();
        Render();
    }

    /// <summary>
    /// Restores cancellation routing when an initialized strategy screen is re-enabled.
    /// </summary>
    private void OnEnable()
    {
        if (initialized)
            RegisterCancelHandlers();
    }

    /// <summary>
    /// Cancels transient strategy interactions while the screen is disabled.
    /// </summary>
    private void OnDisable()
    {
        UnregisterCancelHandlers();
        targetingController?.Cancel();
        contextMenuController?.Cancel();
        strategyContextMenu?.Reset();
        galacticInformationDisplayController?.Hide();
        ClearWindowMovePreview();
    }

    /// <summary>
    /// Releases game and authored-view subscriptions owned by this screen.
    /// </summary>
    private void OnDestroy()
    {
        UnregisterCancelHandlers();
        UnsubscribeViewEvents();
        if (gameManager != null)
        {
            gameManager.GameSpeedChanged -= MarkDirty;
            gameManager.GameReplaced -= HandleGameReplaced;
        }
        BindMessageSystem(null);
    }

    /// <summary>
    /// Advances game time, synchronizes pending alerts, and renders invalidated UI state.
    /// </summary>
    private void Update()
    {
        if (gameManager == null)
            return;

        gameManager.Update();
        if (battleAlertWindowController.SyncPendingCombatWindow())
            dirty = true;

        int currentTick = gameManager.GetCurrentTick();
        Faction playerFaction = gameManager.GetPlayerFaction();
        strategyHudController.ProcessAdvisor(
            currentTick,
            messagesWindowController?.IsOpen != true && playerFaction?.TranslateCounterpart == true
        );
        if (currentTick != lastTick)
        {
            lastTick = currentTick;
            RebuildSnapshot();
            dirty = true;
        }

        if (dirty)
            Render();
    }

    /// <summary>
    /// Routes a strategy-surface pointer press to the input controller.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        inputController?.OnPointerDown(eventData);
    }

    /// <summary>
    /// Routes a strategy-surface pointer release to the input controller.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        inputController?.OnPointerUp(eventData);
    }

    /// <summary>
    /// Routes strategy-surface pointer movement to the input controller.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerMove(PointerEventData eventData)
    {
        inputController?.OnPointerMove(eventData);
    }

    /// <summary>
    /// Routes a strategy-surface drag to the input controller.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        inputController?.OnDrag(eventData);
    }

    /// <summary>
    /// Completes a strategy-surface drag through the pointer-release path.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        inputController?.OnPointerUp(eventData);
    }

    /// <summary>
    /// Routes a completed strategy-surface click to the input controller.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        inputController?.OnPointerClick(eventData);
    }

    /// <summary>
    /// Validates the authored strategy hierarchy before controller construction.
    /// </summary>
    private void ValidateAuthoredViews()
    {
        if (GetComponentInParent<Canvas>() == null)
            throw new InvalidOperationException("StrategyController must be under a Canvas.");

        if (transform is not RectTransform)
            throw new MissingReferenceException("StrategyController is missing RectTransform.");

        ValidateHudLayer();

        if (strategySurface == null)
            throw new MissingReferenceException("Viewport is missing RectTransform.");

        if (strategySurfaceImage == null)
            throw new MissingReferenceException("Viewport is missing RawImage.");

        ValidateWindowLayer();
        ValidateOverlayLayer();
        ValidateGalaxyMapLayer();
        ValidateGalacticInformationLayer();
        ValidateBookmarkLayer();
        ValidateContextMenuLayer();
    }

    /// <summary>
    /// Validates the authored HUD layer.
    /// </summary>
    private void ValidateHudLayer()
    {
        if (strategyHud == null)
            throw new MissingReferenceException("StrategyHud is missing StrategyHudView.");

        RequireRectTransform(strategyHud, "StrategyHud");
    }

    /// <summary>
    /// Validates the authored context-menu layer.
    /// </summary>
    private void ValidateContextMenuLayer()
    {
        if (strategyContextMenu == null)
            throw new MissingReferenceException(
                "ContextMenu is missing StrategyContextMenuPresenter."
            );

        RequireRectTransform(strategyContextMenu, "ContextMenu");
    }

    /// <summary>
    /// Validates the authored strategy-window layer.
    /// </summary>
    private void ValidateWindowLayer()
    {
        if (strategyWindowLayerView == null)
            throw new MissingReferenceException("Windows is missing StrategyWindowLayerView.");
        if (strategyWindowManager == null)
            throw new MissingReferenceException("Windows is missing UIWindowManager.");
        if (strategyWindowManager.transform != strategyWindowLayerView.transform)
            throw new MissingReferenceException(
                "StrategyWindowLayerView and UIWindowManager must share the Windows root."
            );

        RequireRectTransform(strategyWindowLayerView, "Windows");
    }

    /// <summary>
    /// Validates the authored overlay layer.
    /// </summary>
    private void ValidateOverlayLayer()
    {
        if (strategyOverlay == null)
            throw new MissingReferenceException("Overlay is missing StrategyOverlayView.");

        RequireRectTransform(strategyOverlay, "Overlay");
    }

    /// <summary>
    /// Validates the authored galaxy-map layer.
    /// </summary>
    private void ValidateGalaxyMapLayer()
    {
        if (galaxyMap == null)
            throw new MissingReferenceException("GalaxyMap is missing GalaxyMapView.");

        RequireRectTransform(galaxyMap, "GalaxyMap");
    }

    /// <summary>
    /// Validates the authored bookmark bar.
    /// </summary>
    private void ValidateBookmarkLayer()
    {
        if (bookmarkBar == null)
            throw new MissingReferenceException("Bookmarks is missing BookmarkBarView.");

        RequireRectTransform(bookmarkBar, "Bookmarks");
    }

    /// <summary>
    /// Validates the authored galactic-information selector and legend layers.
    /// </summary>
    private void ValidateGalacticInformationLayer()
    {
        if (galacticInformationDisplay == null)
            throw new MissingReferenceException(
                "GalacticInformationDisplay is missing GalacticInformationDisplayView."
            );
        if (galacticInformationLegend == null)
            throw new MissingReferenceException(
                "GalacticInformationLegend is missing GalacticInformationLegendView."
            );

        RequireRectTransform(galacticInformationDisplay, "GalacticInformationDisplay");
        RequireRectTransform(galacticInformationLegend, "GalacticInformationLegend");
    }

    /// <summary>
    /// Verifies that an authored strategy component is hosted by a rectangle transform.
    /// </summary>
    /// <param name="component">The component to validate.</param>
    /// <param name="label">The authored hierarchy label used in errors.</param>
    private static void RequireRectTransform(Component component, string label)
    {
        if (component != null && component.transform is RectTransform)
            return;

        throw new MissingReferenceException($"{label} is missing RectTransform.");
    }

    /// <summary>
    /// Rebuilds the visible galaxy snapshot and reconciles all open windows against it.
    /// </summary>
    private void RebuildSnapshot()
    {
        galaxyMapController.RebuildSnapshot(gameManager);
        bookmarkController.ReconcilePlanets(Sectors);
        statusWindowController.ReconcileWindows(Sectors);
        constructionWindowController.ReconcileWindows(Sectors);
        facilityWindowController.ReconcileWindows(Sectors);
        fleetWindowController.ReconcileWindows(Sectors);
        defenseWindowController.ReconcileWindows(Sectors);
        missionsWindowController.ReconcileWindows(Sectors);
        planetSystemWindowController.ReconcileWindows(Sectors);
        messagesWindowController.ReconcileWindows();
        finderWindowController.ReconcileWindows();
    }

    /// <summary>
    /// Renders the complete strategy presentation from the current controller state.
    /// </summary>
    private void Render()
    {
        RenderGalaxyMap();
        RenderBookmarks();
        strategyWindowLayerView.RenderModalState(strategyWindowManager.HasModalWindow());
        advisorReportWindowController.RenderWindows();
        statusWindowController.RenderWindows();
        encyclopediaWindowController.RenderWindows();
        finderWindowController.RenderWindows();
        messagesWindowController.RenderWindows();
        battleAlertWindowController.RenderWindows();
        confirmDialogWindowController.RenderWindows();
        missionCreateWindowController.RenderWindows();
        constructionWindowController.RenderWindows();
        facilityWindowController.RenderWindows();
        fleetWindowController.RenderWindows();
        defenseWindowController.RenderWindows();
        missionsWindowController.RenderWindows();
        planetSystemWindowController.RenderWindows();

        RenderHud();
        RenderContextMenu();
        RenderOverlay();
        dirty = false;
    }

    /// <summary>
    /// Projects current game resources, speed, and message state into the HUD.
    /// </summary>
    private void RenderHud()
    {
        if (strategyHud == null)
            return;

        Faction faction = gameManager.GetPlayerFaction();
        strategyHudController.Render(
            new StrategyHudRenderData(
                gameManager.GetCurrentTick().ToString(),
                faction?.RawMaterials.ToString() ?? "0",
                faction?.RefinedMaterials.ToString() ?? "0",
                faction?.MaintenanceHeadroom.ToString() ?? "0",
                gameManager.GetGameSpeed(),
                StrategyHudController.GetUnreadMessageTypes(faction)
            )
        );
    }

    /// <summary>
    /// Renders the current authored context-menu state.
    /// </summary>
    private void RenderContextMenu()
    {
        if (strategyContextMenu == null)
            return;

        strategyContextMenu.RenderCurrent();
    }

    /// <summary>
    /// Projects the visible snapshot and active information filter into the galaxy map.
    /// </summary>
    private void RenderGalaxyMap()
    {
        if (galaxyMapController == null)
            return;

        galaxyMapController.Render(
            Sectors,
            PlayerFactionId,
            galacticInformationDisplayController?.FilterMode
                ?? GalacticInformationFilterMode.DisplayOff
        );
    }

    /// <summary>
    /// Projects current bookmark state into the authored bookmark bar.
    /// </summary>
    private void RenderBookmarks()
    {
        if (bookmarkBar == null)
            return;

        bookmarkBar.Render(
            bookmarkController.BuildRenderData(),
            uiContext.GetPlayerFactionTheme()?.StrategyBookmarkLayout
        );
    }

    /// <summary>
    /// Renders drag, targeting, and window-move feedback above the strategy surface.
    /// </summary>
    private void RenderOverlay()
    {
        if (strategyOverlay == null)
            return;

        RectInt? dragFrameBounds = windowMovePreviewVisible ? windowMovePreviewBounds : null;
        Texture dragImageTexture = null;
        RectInt? dragImageBounds = null;
        if (
            strategyDragController != null
            && strategyDragController.TryGetOverlay(out Texture texture, out RectInt bounds)
        )
        {
            dragImageTexture = texture;
            dragImageBounds = bounds;
        }

        strategyOverlay.Render(
            new StrategyOverlayRenderData(dragFrameBounds, dragImageTexture, dragImageBounds)
        );
    }

    /// <summary>
    /// Restores a bookmarked planet window and invalidates presentation on success.
    /// </summary>
    /// <param name="index">The zero-based bookmark slot.</param>
    private void HandleBookmarkRequested(int index)
    {
        if (TryRestoreBookmark(index))
            dirty = true;
    }

    /// <summary>
    /// Executes a semantic button released by an authored window shell.
    /// </summary>
    /// <param name="window">The source window.</param>
    /// <param name="action">The semantic window action.</param>
    private void HandleWindowShellButtonRequested(UIWindow window, int action)
    {
        if (
            window == null
            || action == 0
            || strategyWindowManager.GetWindowById(window.Id) != window
        )
            return;

        inputController?.SuppressNextClick();
        ExecuteWindowButton(window, action);
        dirty = true;
    }

    /// <summary>
    /// Routes cancellation closure through the same semantic path as the authored close button.
    /// </summary>
    /// <param name="window">The window requesting closure.</param>
    private void HandleWindowCloseRequested(UIWindow window)
    {
        ExecuteWindowButton(window, StrategyWindowButtonActions.CloseWindow);
    }

    /// <summary>
    /// Invalidates presentation after the active window changes.
    /// </summary>
    /// <param name="window">The newly active window.</param>
    private void HandleWindowFocusChanged(UIWindow window)
    {
        if (window != null)
            dirty = true;
    }

    /// <summary>
    /// Clears move-preview state after an authored window has moved.
    /// </summary>
    /// <param name="window">The moved window.</param>
    private void HandleWindowMoved(UIWindow window)
    {
        if (window == null)
            return;

        ClearWindowMovePreview();
        dirty = true;
    }

    /// <summary>
    /// Displays immediate move-preview bounds for a dragged window shell.
    /// </summary>
    /// <param name="window">The moving window.</param>
    /// <param name="bounds">The current source-space preview bounds.</param>
    private void HandleWindowMovePreviewChanged(UIWindow window, RectInt bounds)
    {
        if (window == null)
            return;

        windowMovePreviewVisible = true;
        windowMovePreviewBounds = bounds;
        RenderOverlay();
    }

    /// <summary>
    /// Clears immediate move-preview feedback when a window drag ends.
    /// </summary>
    /// <param name="window">The moving window.</param>
    private void HandleWindowMovePreviewEnded(UIWindow window)
    {
        if (window == null)
            return;

        ClearWindowMovePreview();
        RenderOverlay();
    }

    /// <summary>
    /// Opens a feature context menu after clearing competing modal interactions.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <param name="eventData">The source pointer event.</param>
    /// <param name="sourceX">The source-space horizontal menu position.</param>
    /// <param name="sourceY">The source-space vertical menu position.</param>
    private void HandleWindowContextRequested(
        UIWindow window,
        PointerEventData eventData,
        int sourceX,
        int sourceY
    )
    {
        galacticInformationDisplayController?.Hide();
        targetingController?.Cancel();
        strategyContextMenuRouter.OpenContextMenu(window, eventData, sourceX, sourceY);
        dirty = true;
    }

    /// <summary>
    /// Routes a selected authored menu command to runtime or direct action handling.
    /// </summary>
    /// <param name="command">The selected semantic command.</param>
    private void HandleContextMenuCommandSelected(StrategyMenuCommand command)
    {
        inputController?.SuppressNextClick();
        strategyContextMenuRouter.SelectRuntimeContextMenu(command);

        dirty = true;
    }

    /// <summary>
    /// Dismisses the current context menu and optionally opens the menu beneath a right click.
    /// </summary>
    /// <param name="eventData">The pointer event that dismissed the menu.</param>
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

    /// <summary>
    /// Cancels competing interactions when a modal strategy window opens.
    /// </summary>
    /// <param name="window">The opened modal window.</param>
    private void HandleWindowModalOpened(UIWindow window)
    {
        if (window == null)
            return;

        strategyWindowLayerView.RenderModalState(strategyWindowManager.HasModalWindow());
        galacticInformationDisplayController?.Hide();
        targetingController?.Cancel();
        contextMenuController?.Cancel();
        strategyContextMenu?.Reset();
        dirty = true;
    }

    /// <summary>
    /// Clears transient targeting and move-preview state after any window closes.
    /// </summary>
    /// <param name="window">The closed window.</param>
    private void HandleAnyWindowClosed(UIWindow window)
    {
        if (window == null)
            return;

        strategyWindowLayerView.RenderModalState(strategyWindowManager.HasModalWindow());
        targetingController?.Cancel();
        ClearWindowMovePreview();
        dirty = true;
    }

    /// <summary>
    /// Offers a newly delivered player message to the advisor notification controller.
    /// </summary>
    /// <param name="faction">The message recipient.</param>
    /// <param name="message">The delivered message.</param>
    private void HandleMessageDelivered(Faction faction, Message message)
    {
        if (faction?.InstanceID != PlayerFactionId)
            return;

        strategyHudController.NotifyAdvisor(
            message,
            gameManager.GetCurrentTick(),
            faction.IsAdvisorMessageNotificationEnabled(message.Type)
        );
    }

    /// <summary>
    /// Rebinds strategy presentation and message delivery after a hot-loaded game replacement.
    /// </summary>
    /// <param name="game">The replacement active game.</param>
    private void HandleGameReplaced(GameRoot game)
    {
        uiContext.ReplaceGame(game);
        BindMessageSystem(gameManager.MessageSystem);
        lastTick = gameManager.GetCurrentTick();
        RefreshStrategyState();
    }

    /// <summary>
    /// Connects advisor notifications to the active message system.
    /// </summary>
    /// <param name="system">The message system to observe, or null while disposing.</param>
    private void BindMessageSystem(MessageSystem system)
    {
        if (messageSystem != null)
            messageSystem.MessageDelivered -= HandleMessageDelivered;

        messageSystem = system;
        if (messageSystem != null)
            messageSystem.MessageDelivered += HandleMessageDelivered;
    }

    /// <inheritdoc />
    void IStrategyHudActions.ReleaseHudButton(StrategyHudAction action, int sourceX, int sourceY)
    {
        if (action == StrategyHudAction.GalacticInformationDisplay)
            OpenGalacticInformationDisplay();
        else
            ExecuteHudAction(action);

        dirty = true;
    }

    /// <inheritdoc />
    void IStrategyHudActions.OpenMessagesTab(MessagesTab tab)
    {
        messagesWindowController.Open(tab);
        dirty = true;
    }

    /// <inheritdoc />
    void IStrategyHudActions.OpenSpeedContextMenu(
        ContextMenuRequest request,
        int sourceX,
        int sourceY
    )
    {
        targetingController?.Cancel();
        strategyContextMenuRouter.OpenRuntimeContextMenu(
            request,
            sourceX,
            sourceY,
            strategyContextMenu.SpeedMenuWidth
        );
        dirty = true;
    }

    /// <inheritdoc />
    void IStrategyHudActions.OpenAdvisorCommandContextMenu(
        ContextMenuRequest request,
        int sourceX,
        int sourceY
    )
    {
        targetingController?.Cancel();
        strategyContextMenuRouter.OpenRuntimeContextMenu(
            request,
            sourceX,
            sourceY,
            strategyContextMenu.Layout.FallbackMenuWidth
        );
        dirty = true;
    }

    /// <inheritdoc />
    void IStrategyHudActions.OpenAdvisorNotificationContextMenu(
        ContextMenuRequest request,
        int sourceX,
        int sourceY
    )
    {
        targetingController?.Cancel();
        strategyContextMenuRouter.OpenRuntimeContextMenu(
            request,
            sourceX,
            sourceY,
            strategyContextMenu.Layout.FallbackMenuWidth
        );
        dirty = true;
    }

    /// <inheritdoc />
    void IStrategyHudActions.BeginAdvisorConstruction(
        ManufacturingType manufacturingType,
        int sourceX,
        int sourceY
    )
    {
        advisorCommandController.BeginConstruction(manufacturingType, sourceX, sourceY);
        dirty = true;
    }

    /// <inheritdoc />
    void IStrategyHudActions.OpenAdvisorReport(AdvisorReportMode mode)
    {
        advisorReportWindowController.Open(mode);
        dirty = true;
    }

    /// <inheritdoc />
    void IStrategyHudActions.SetGameSpeed(TickSpeed speed)
    {
        gameManager.SetGameSpeed(speed);
        dirty = true;
    }

    /// <inheritdoc />
    void IStrategyHudActions.RequestHudRender()
    {
        dirty = true;
    }

    /// <inheritdoc />
    void IGalaxyMapActions.OpenPlanetSystemWindow(PlanetSystem system, int sourceX, int sourceY)
    {
        if (!CanInteractWithGalaxy())
            return;

        OpenPlanetSystemWindow(system, sourceX, sourceY);
        dirty = true;
    }

    /// <inheritdoc />
    void IGalaxyMapActions.RequestGalaxyMapRender()
    {
        dirty = true;
    }

    /// <inheritdoc />
    void IGalacticInformationDisplayActions.RequestGalacticInformationRender()
    {
        dirty = true;
    }

    /// <inheritdoc />
    void IConstructionWindowActions.OpenConstructionInfo(ISceneNode item)
    {
        OpenEncyclopediaWindow(item);
    }

    /// <inheritdoc />
    void IConstructionWindowActions.OpenConstructionStatus(StrategyStatusTarget target)
    {
        TryOpenStatusWindow(target);
    }

    /// <inheritdoc />
    void IConstructionWindowActions.RefreshAfterConstruction()
    {
        RefreshStrategyState();
    }

    /// <inheritdoc />
    void IFacilityWindowActions.OpenFacilityInfo(StrategyStatusTarget target)
    {
        OpenEncyclopediaWindow(target);
    }

    /// <inheritdoc />
    void IFacilityWindowActions.OpenFacilityStatus(StrategyStatusTarget target)
    {
        TryOpenStatusWindow(target);
    }

    /// <inheritdoc />
    void IFacilityWindowActions.RefreshFacilityState()
    {
        RefreshStrategyState();
    }

    /// <inheritdoc />
    void IFleetWindowActions.OpenFleetEncyclopediaWindow(IReadOnlyList<ISceneNode> items)
    {
        List<ISceneNode> sourceItems = CopySceneNodes(items);
        if (sourceItems.Count == 1)
            OpenEncyclopediaWindow(sourceItems[0]);
    }

    /// <inheritdoc />
    void IFleetWindowActions.OpenFleetStatusWindow(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> items
    )
    {
        List<ISceneNode> sourceItems = CopySceneNodes(items);
        GalaxyMapPlanet planet = GetWindowPlanet(sourceWindow);
        if (planet != null && sourceItems.Count == 1)
            TryOpenStatusWindow(new StrategyStatusTarget(planet, sourceItems[0]));
    }

    /// <inheritdoc />
    void IFleetWindowActions.RefreshFleetState()
    {
        RefreshStrategyState();
    }

    /// <inheritdoc />
    void IDefenseWindowActions.OpenDefenseInfoWindow(StrategyStatusTarget target)
    {
        OpenEncyclopediaWindow(target);
    }

    /// <inheritdoc />
    void IDefenseWindowActions.OpenDefenseStatusWindow(StrategyStatusTarget target)
    {
        TryOpenStatusWindow(target);
    }

    /// <inheritdoc />
    void IPlanetSystemWindowActions.RefreshPlanetSystemState()
    {
        RefreshStrategyState();
    }

    /// <inheritdoc />
    void IPlanetSystemWindowActions.OpenPlanetSystemPlanetWindow(
        GalaxyMapPlanet planet,
        PlanetIcon icon,
        int sourceX,
        int sourceY
    )
    {
        OpenPlanetWindowAt(planet, icon, sourceX, sourceY);
    }

    /// <inheritdoc />
    void IPlanetSystemWindowActions.OpenPlanetSystemInfo(StrategyStatusTarget target)
    {
        OpenEncyclopediaWindow(target);
    }

    /// <inheritdoc />
    void IPlanetSystemWindowActions.OpenPlanetSystemStatus(StrategyStatusTarget target)
    {
        TryOpenStatusWindow(target);
    }

    /// <inheritdoc />
    void IMissionsWindowActions.OpenMissionsInfo(StrategyStatusTarget target)
    {
        OpenEncyclopediaWindow(target);
    }

    /// <inheritdoc />
    void IMissionsWindowActions.OpenMissionsStatus(StrategyStatusTarget target)
    {
        TryOpenStatusWindow(target);
    }

    /// <inheritdoc />
    void IMissionCreateWindowActions.RefreshAfterMissionCreation()
    {
        RefreshStrategyState();
    }

    /// <inheritdoc />
    void IMissionCreateWindowActions.OpenMissionCreateInfo()
    {
        encyclopediaWindowController.Open();
    }

    /// <inheritdoc />
    bool IMessagesWindowActions.OpenMessageTarget(
        string targetInstanceId,
        string secondaryTargetInstanceId,
        string locationInstanceId
    )
    {
        return OpenMessageTarget(targetInstanceId, secondaryTargetInstanceId, locationInstanceId);
    }

    /// <inheritdoc />
    void IStatusWindowActions.OpenStatusInfo(StrategyStatusTarget target)
    {
        OpenEncyclopediaWindow(target);
    }

    /// <inheritdoc />
    void IBattleAlertWindowActions.OpenBattleResultFleet(Planet planet, int sourceX, int sourceY)
    {
        OpenPlanetWindow(planet, PlanetIcon.Fleet, sourceX, sourceY);
    }

    /// <inheritdoc />
    void IBattleAlertWindowActions.OpenBattleResultSystem(
        PlanetSystem system,
        int sourceX,
        int sourceY
    )
    {
        OpenPlanetSystemWindow(system, sourceX, sourceY);
    }

    /// <inheritdoc />
    void IBattleAlertWindowActions.RebuildBattleSnapshot()
    {
        RebuildSnapshot();
    }

    /// <summary>
    /// Routes one semantic HUD action to its destination feature or scene.
    /// </summary>
    /// <param name="action">The semantic HUD action.</param>
    private void ExecuteHudAction(StrategyHudAction action)
    {
        switch (action)
        {
            case StrategyHudAction.Options:
                SaveMenuLaunchContext.OpenFromStrategyView();
                SceneManager.LoadScene(SaveMenuLaunchContext.SaveMenuSceneName);
                break;
            case StrategyHudAction.SystemFinder:
                finderWindowController.Open(FinderMode.Systems);
                break;
            case StrategyHudAction.FleetFinder:
                finderWindowController.Open(FinderMode.Fleets);
                break;
            case StrategyHudAction.TroopFinder:
                finderWindowController.Open(FinderMode.Troops);
                break;
            case StrategyHudAction.PersonnelFinder:
                finderWindowController.Open(FinderMode.Personnel);
                break;
            case StrategyHudAction.Encyclopedia:
                encyclopediaWindowController.Open();
                break;
        }
    }

    /// <summary>
    /// Routes one semantic window-shell action to the owning screen lifecycle.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <param name="action">The semantic shell action.</param>
    private void ExecuteWindowButton(UIWindow window, int action)
    {
        if (window == null || action == 0)
            return;

        switch (action)
        {
            case StrategyWindowButtonActions.CloseWindow:
            case StrategyDialogButtonActions.Close:
                PlaySectorWindowCloseSound(window);
                CloseWindow(window);
                break;
            case StrategyWindowButtonActions.OpenSector:
                GalaxyMapPlanet planet = GetWindowPlanet(window);
                if (planet != null)
                    OpenPlanetSystemWindow(planet.Sector, window.X, window.Y);
                break;
            case StrategyWindowButtonActions.SwapWindow:
                planetSystemWindowController.Swap(window);
                break;
            case StrategyWindowButtonActions.MinimizeWindow:
                MinimizeWindow(window);
                break;
        }
    }

    /// <summary>
    /// Restores a minimized planet window from one bookmark slot.
    /// </summary>
    /// <param name="index">The zero-based bookmark slot.</param>
    /// <returns>True when a bookmarked window was restored.</returns>
    private bool TryRestoreBookmark(int index)
    {
        if (!bookmarkController.TryTake(index, out BookmarkEntry bookmark))
            return false;

        return OpenPlanetWindowAt(bookmark.Planet, bookmark.Icon, bookmark.X, bookmark.Y, bookmark)
            != null;
    }

    /// <summary>
    /// Replaces one planet feature window with a bookmark entry.
    /// </summary>
    /// <param name="window">The planet feature window to minimize.</param>
    private void MinimizeWindow(UIWindow window)
    {
        GalaxyMapPlanet planet = GetWindowPlanet(window);
        PlanetIcon icon = GetPlanetWindowIcon(window);
        if (planet == null || icon == PlanetIcon.None)
            return;

        if (bookmarkController.TryAdd(icon, window.X, window.Y, planet))
        {
            PlayStrategySfx(
                uiContext
                    ?.GetPlayerFactionTheme()
                    ?.StrategyWindowSounds?.PlanetWindowMinimizeSoundPath
            );
            CloseWindow(window, false);
        }
    }

    /// <summary>
    /// Opens or focuses a planet feature window at a source-space position.
    /// </summary>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="icon">The requested feature category.</param>
    /// <param name="sourceX">The source-space horizontal position.</param>
    /// <param name="sourceY">The source-space vertical position.</param>
    /// <returns>The opened or focused window, or null when unsupported.</returns>
    private UIWindow OpenPlanetWindowAt(
        GalaxyMapPlanet planet,
        PlanetIcon icon,
        int sourceX,
        int sourceY
    )
    {
        if (planet?.Planet == null || icon == PlanetIcon.None)
            return null;

        BookmarkEntry bookmark = bookmarkController.Take(planet, icon);
        return OpenPlanetWindowAt(planet, icon, sourceX, sourceY, bookmark);
    }

    /// <summary>
    /// Opens or focuses a planet feature window, optionally restoring a bookmark.
    /// </summary>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="icon">The requested feature category.</param>
    /// <param name="sourceX">The source-space horizontal position.</param>
    /// <param name="sourceY">The source-space vertical position.</param>
    /// <param name="bookmark">The restored bookmark, or null for a new request.</param>
    /// <returns>The opened or focused window, or null when unsupported.</returns>
    private UIWindow OpenPlanetWindowAt(
        GalaxyMapPlanet planet,
        PlanetIcon icon,
        int sourceX,
        int sourceY,
        BookmarkEntry bookmark
    )
    {
        int x = bookmark?.X ?? sourceX;
        int y = bookmark?.Y ?? sourceY;
        bool created;
        UIWindow window;
        switch (icon)
        {
            case PlanetIcon.Facility:
                window = facilityWindowController.Open(planet, x, y, out created);
                break;
            case PlanetIcon.Defense:
                window = defenseWindowController.Open(planet, x, y, out created);
                break;
            case PlanetIcon.Fleet:
                window = fleetWindowController.Open(planet, x, y, out created);
                break;
            case PlanetIcon.Mission:
                window = missionsWindowController.Open(planet, x, y, out created);
                break;
            default:
                return null;
        }

        if (created)
            PlayPlanetWindowOpenSound(icon, bookmark != null);

        return window;
    }

    /// <summary>
    /// Opens a category window for an authoritative planet entity.
    /// </summary>
    /// <param name="planet">The planet whose feature window should open.</param>
    /// <param name="icon">The requested feature category.</param>
    /// <param name="sourceX">The source-space horizontal position.</param>
    /// <param name="sourceY">The source-space vertical position.</param>
    private void OpenPlanetWindow(Planet planet, PlanetIcon icon, int sourceX, int sourceY)
    {
        if (planet == null || icon == PlanetIcon.None)
            return;

        GalaxyMapPlanet strategyPlanet = Sectors
            .SelectMany(sector => sector.Planets)
            .FirstOrDefault(item => item.Planet?.InstanceID == planet.InstanceID);
        if (strategyPlanet == null)
            return;

        OpenPlanetWindowAt(strategyPlanet, icon, sourceX, sourceY);
        MarkDirty();
    }

    /// <summary>
    /// Opens the most specific strategy location represented by message identifiers.
    /// </summary>
    /// <param name="targetInstanceId">The preferred target identifier.</param>
    /// <param name="secondaryTargetInstanceId">The fallback target identifier.</param>
    /// <param name="locationInstanceId">The event location identifier.</param>
    /// <returns>True when the target sector was resolved.</returns>
    private bool OpenMessageTarget(
        string targetInstanceId,
        string secondaryTargetInstanceId,
        string locationInstanceId
    )
    {
        ISceneNode target = ResolveMessageTarget(targetInstanceId, secondaryTargetInstanceId);
        GalaxyMapPlanet planet = FindMessageTargetPlanet(target, locationInstanceId);
        GalaxyMapSector sector =
            planet?.Sector
            ?? FindMessageLocationSector(target?.GetInstanceID())
            ?? FindMessageLocationSector(locationInstanceId);
        if (sector == null)
            return false;

        CloseWindow(strategyWindowManager.FindWindow<MessagesWindowView>());
        CloseWindow(strategyWindowManager.FindWindow<EncyclopediaWindowView>());

        Vector2Int source = GetSystemSourcePosition(sector);
        OpenPlanetSystemWindow(sector, source.x, source.y);

        PlanetIcon icon = GetMessageTargetIcon(target);
        if (planet != null && icon != PlanetIcon.None)
        {
            UIWindow targetWindow = OpenPlanetWindowAt(planet, icon, source.x, source.y);
            SelectMessageTarget(targetWindow, target);
        }

        MarkDirty();
        return true;
    }

    /// <summary>
    /// Resolves a message's preferred target from the live scene graph.
    /// </summary>
    /// <param name="targetInstanceId">The preferred target identifier.</param>
    /// <param name="secondaryTargetInstanceId">The fallback target identifier.</param>
    /// <returns>The resolved target, or null when neither identifier resolves.</returns>
    private ISceneNode ResolveMessageTarget(
        string targetInstanceId,
        string secondaryTargetInstanceId
    )
    {
        return gameManager.GetGame()?.GetSceneNodeByInstanceID<ISceneNode>(targetInstanceId)
            ?? gameManager
                .GetGame()
                ?.GetSceneNodeByInstanceID<ISceneNode>(secondaryTargetInstanceId);
    }

    /// <summary>
    /// Resolves the visible strategy planet associated with a message target or location.
    /// </summary>
    /// <param name="target">The resolved message target.</param>
    /// <param name="locationInstanceId">The message location identifier.</param>
    /// <returns>The matching visible planet, or null.</returns>
    private GalaxyMapPlanet FindMessageTargetPlanet(ISceneNode target, string locationInstanceId)
    {
        Planet targetPlanet = target as Planet ?? target?.GetParentOfType<Planet>();
        string planetId = targetPlanet?.InstanceID ?? locationInstanceId;
        if (string.IsNullOrEmpty(planetId))
            return null;

        return Sectors
            .SelectMany(sector => sector.Planets)
            .FirstOrDefault(planet => planet?.Planet?.InstanceID == planetId);
    }

    /// <summary>
    /// Maps a message target to the feature window that presents it.
    /// </summary>
    /// <param name="target">The resolved message target.</param>
    /// <returns>The matching feature category, or none.</returns>
    private static PlanetIcon GetMessageTargetIcon(ISceneNode target)
    {
        if (target == null)
            return PlanetIcon.None;
        if (target is Mission || target.GetParentOfType<Mission>() != null)
            return PlanetIcon.Mission;
        if (target is Fleet || target.GetParentOfType<Fleet>() != null)
            return PlanetIcon.Fleet;
        if (target is Building building)
        {
            return
                building.BuildingType is BuildingType.Defense or BuildingType.Weapon
                || building.DefenseFacilityClass != DefenseFacilityClass.None
                ? PlanetIcon.Defense
                : PlanetIcon.Facility;
        }

        return target is Officer or SpecialForces or Regiment or Starfighter
            ? PlanetIcon.Defense
            : PlanetIcon.None;
    }

    /// <summary>
    /// Selects a resolved message target inside its destination feature window.
    /// </summary>
    /// <param name="window">The destination feature window.</param>
    /// <param name="target">The target to select.</param>
    private void SelectMessageTarget(UIWindow window, ISceneNode target)
    {
        if (window == null || target == null)
            return;

        if (strategyWindowManager.TryGetWindowView(window, out MissionsWindowView missionsView))
            missionsWindowController.SelectTarget(missionsView, target);
        else if (strategyWindowManager.TryGetWindowView(window, out FleetWindowView fleetView))
            fleetWindowController.SelectTarget(fleetView, target);
        else if (strategyWindowManager.TryGetWindowView(window, out DefenseWindowView defenseView))
            defenseWindowController.SelectTarget(defenseView, target);
        else if (
            target is Building building
            && strategyWindowManager.TryGetWindowView(window, out FacilityWindowView facilityView)
        )
            facilityWindowController.SelectTarget(facilityView, building);
    }

    /// <summary>
    /// Resolves a message location identifier to a visible sector.
    /// </summary>
    /// <param name="locationInstanceId">A system or planet identifier.</param>
    /// <returns>The matching visible sector, or null.</returns>
    private GalaxyMapSector FindMessageLocationSector(string locationInstanceId)
    {
        if (string.IsNullOrEmpty(locationInstanceId))
            return null;

        return Sectors.FirstOrDefault(sector =>
            string.Equals(sector?.System?.InstanceID, locationInstanceId, StringComparison.Ordinal)
            || sector?.Planets?.Any(planet =>
                string.Equals(
                    planet?.Planet?.InstanceID,
                    locationInstanceId,
                    StringComparison.Ordinal
                )
            ) == true
        );
    }

    /// <summary>
    /// Opens status for the current semantic selection in a source window.
    /// </summary>
    /// <param name="window">The source feature window.</param>
    /// <returns>True when a valid status target was opened.</returns>
    private bool TryOpenStatusWindow(UIWindow window)
    {
        return TryOpenStatusWindow(GetStatusTarget(window));
    }

    /// <summary>
    /// Opens status for one resolved target.
    /// </summary>
    /// <param name="target">The target to present.</param>
    /// <returns>True when the target initialized a status window.</returns>
    private bool TryOpenStatusWindow(StrategyStatusTarget target)
    {
        return statusWindowController.Open(target);
    }

    /// <summary>
    /// Resolves the status target owned by a feature window's current selection.
    /// </summary>
    /// <param name="window">The source feature window.</param>
    /// <returns>The selected status target, or null.</returns>
    private StrategyStatusTarget GetStatusTarget(UIWindow window)
    {
        if (
            strategyWindowManager.TryGetWindowView(
                window,
                out ConstructionWindowView constructionView
            )
        )
            return constructionWindowController.GetStatusTarget(constructionView);
        if (strategyWindowManager.TryGetWindowView(window, out FacilityWindowView facilityView))
            return facilityWindowController.GetStatusTarget(facilityView);
        if (strategyWindowManager.TryGetWindowView(window, out FleetWindowView _))
            return fleetWindowController.GetStatusTarget(window);
        if (strategyWindowManager.TryGetWindowView(window, out DefenseWindowView _))
            return defenseWindowController.GetStatusTarget(window);
        if (strategyWindowManager.TryGetWindowView(window, out MissionsWindowView missionsView))
            return missionsWindowController.GetStatusTarget(missionsView);
        if (
            strategyWindowManager.TryGetWindowView(
                window,
                out PlanetSystemWindowView planetSystemView
            )
        )
            return planetSystemWindowController.GetStatusTarget(planetSystemView);

        return null;
    }

    /// <summary>
    /// Opens Encyclopedia information for a resolved status target.
    /// </summary>
    /// <param name="target">The selected status target.</param>
    private void OpenEncyclopediaWindow(StrategyStatusTarget target)
    {
        if (target?.ManufacturingType.HasValue == true)
        {
            encyclopediaWindowController.Open();
            return;
        }

        OpenEncyclopediaWindow(target?.Item ?? target?.Planet?.Planet);
    }

    /// <summary>
    /// Opens Encyclopedia information for one scene-graph entry.
    /// </summary>
    /// <param name="target">The entry to display.</param>
    private void OpenEncyclopediaWindow(ISceneNode target)
    {
        encyclopediaWindowController.Open(target);
    }

    /// <summary>
    /// Gets the current semantic selection owned by one strategy window.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <returns>The selected scene nodes in presentation order.</returns>
    private IReadOnlyList<ISceneNode> GetContextItems(UIWindow window)
    {
        if (strategyWindowManager.TryGetWindowView(window, out FleetWindowView _))
            return fleetWindowController.GetContextItems(window);
        if (strategyWindowManager.TryGetWindowView(window, out DefenseWindowView _))
            return defenseWindowController.GetContextItems(window);
        if (
            strategyWindowManager.TryGetWindowView(
                window,
                out PlanetSystemWindowView planetSystemView
            )
        )
            return planetSystemWindowController.GetContextItems(planetSystemView);

        return Array.Empty<ISceneNode>();
    }

    /// <summary>
    /// Creates the current drag preview owned by one strategy feature window.
    /// </summary>
    /// <param name="window">The source strategy window.</param>
    /// <param name="sourceX">The pointer's source-space horizontal coordinate.</param>
    /// <param name="sourceY">The pointer's source-space vertical coordinate.</param>
    /// <param name="preview">Receives the drag preview.</param>
    /// <returns>True when the feature supplied a drawable preview.</returns>
    private bool TryGetDragPreview(
        UIWindow window,
        int sourceX,
        int sourceY,
        out DragPreview preview
    )
    {
        preview = null;
        if (strategyWindowManager.TryGetWindowView(window, out FleetWindowView fleetView))
            return fleetView.TryGetDragPreview(sourceX, sourceY, out preview);
        if (strategyWindowManager.TryGetWindowView(window, out DefenseWindowView _))
            return defenseWindowController.TryGetDragPreview(window, sourceX, sourceY, out preview);
        if (
            strategyWindowManager.TryGetWindowView(
                window,
                out PlanetSystemWindowView planetSystemView
            )
        )
        {
            return planetSystemWindowController.TryGetDragPreview(
                planetSystemView,
                sourceX,
                sourceY,
                out preview
            );
        }

        return false;
    }

    /// <summary>
    /// Completes active targeting with the planet represented by a feature window.
    /// </summary>
    /// <param name="window">The candidate target window.</param>
    /// <returns>True when the represented planet completed targeting.</returns>
    private bool TrySelectWindowPlanetTarget(UIWindow window)
    {
        if (targetingController?.IsTargeting != true)
            return false;

        GalaxyMapPlanet planet = GetWindowPlanet(window);
        return planet?.Planet != null
            && targetingController.TrySelectTarget(
                new StrategyMissionTarget(planet, planet.Planet)
            );
    }

    /// <summary>
    /// Clears feature-owned selection state after a mutation.
    /// </summary>
    /// <param name="window">The source feature window.</param>
    private void ClearWindowSelection(UIWindow window)
    {
        if (strategyWindowManager.TryGetWindowView(window, out FacilityWindowView facilityView))
        {
            facilityWindowController.ClearSelection(facilityView);
            return;
        }
        if (strategyWindowManager.TryGetWindowView(window, out FleetWindowView _))
        {
            fleetWindowController.ClearSelection(window);
            return;
        }
        if (strategyWindowManager.TryGetWindowView(window, out DefenseWindowView _))
        {
            defenseWindowController.ClearSelection(window);
            return;
        }
        if (
            strategyWindowManager.TryGetWindowView(
                window,
                out PlanetSystemWindowView planetSystemView
            )
        )
            planetSystemWindowController.ClearSelection(planetSystemView);
    }

    /// <summary>
    /// Maps a registered planet feature window to its category icon.
    /// </summary>
    /// <param name="window">The feature window to inspect.</param>
    /// <returns>The matching icon category, or none.</returns>
    private PlanetIcon GetPlanetWindowIcon(UIWindow window)
    {
        if (strategyWindowManager.TryGetWindowView(window, out FacilityWindowView _))
            return PlanetIcon.Facility;
        if (strategyWindowManager.TryGetWindowView(window, out FleetWindowView _))
            return PlanetIcon.Fleet;
        if (strategyWindowManager.TryGetWindowView(window, out DefenseWindowView _))
            return PlanetIcon.Defense;
        if (strategyWindowManager.TryGetWindowView(window, out MissionsWindowView _))
            return PlanetIcon.Mission;

        return PlanetIcon.None;
    }

    /// <summary>
    /// Gets the strategy planet represented by a planet-backed feature window.
    /// </summary>
    /// <param name="window">The feature window to inspect.</param>
    /// <returns>The represented planet, or null.</returns>
    private GalaxyMapPlanet GetWindowPlanet(UIWindow window)
    {
        if (
            strategyWindowManager.TryGetWindowView(
                window,
                out ConstructionWindowView constructionView
            )
        )
            return constructionWindowController.GetPlanet(constructionView);
        if (strategyWindowManager.TryGetWindowView(window, out FacilityWindowView facilityView))
            return facilityWindowController.GetPlanet(facilityView);
        if (strategyWindowManager.TryGetWindowView(window, out FleetWindowView fleetView))
            return fleetWindowController.GetPlanet(fleetView);
        if (strategyWindowManager.TryGetWindowView(window, out DefenseWindowView defenseView))
            return defenseWindowController.GetPlanet(defenseView);
        if (strategyWindowManager.TryGetWindowView(window, out MissionsWindowView missionsView))
            return missionsWindowController.GetPlanet(missionsView);

        return null;
    }

    /// <summary>
    /// Rebuilds authoritative strategy state and invalidates presentation.
    /// </summary>
    private void RefreshStrategyState()
    {
        RebuildSnapshot();
        MarkDirty();
    }

    /// <summary>
    /// Copies a nullable scene-node collection while preserving source order.
    /// </summary>
    /// <param name="items">The source collection.</param>
    /// <returns>A non-null mutable list.</returns>
    private static List<ISceneNode> CopySceneNodes(IReadOnlyList<ISceneNode> items)
    {
        return items?.ToList() ?? new List<ISceneNode>();
    }

    /// <summary>
    /// Plays the configured close sound for a sector window.
    /// </summary>
    /// <param name="window">The closing window.</param>
    private void PlaySectorWindowCloseSound(UIWindow window)
    {
        if (strategyWindowManager.TryGetWindowView(window, out PlanetSystemWindowView _))
            PlayStrategySfx(StrategyUISoundPaths.SectorWindowClose);
    }

    /// <summary>
    /// Opens or focuses the sector window containing a planetary system.
    /// </summary>
    /// <param name="system">The planetary system to display.</param>
    /// <param name="sourceX">The source-space horizontal origin.</param>
    /// <param name="sourceY">The source-space vertical origin.</param>
    /// <returns>True when the sector was opened or focused.</returns>
    private bool OpenPlanetSystemWindow(PlanetSystem system, int sourceX, int sourceY)
    {
        GalaxyMapSector sector = Sectors.FirstOrDefault(candidate => candidate.System == system);
        return OpenPlanetSystemWindow(sector, sourceX, sourceY);
    }

    /// <summary>
    /// Opens or focuses one sector window and plays its authored creation sound.
    /// </summary>
    /// <param name="sector">The strategy sector to display.</param>
    /// <param name="sourceX">The source-space horizontal origin.</param>
    /// <param name="sourceY">The source-space vertical origin.</param>
    /// <returns>True when the sector was opened or focused.</returns>
    private bool OpenPlanetSystemWindow(GalaxyMapSector sector, int sourceX, int sourceY)
    {
        if (sector == null)
            return false;

        if (planetSystemWindowController.Open(sector, sourceX, sourceY))
        {
            PlayStrategySfx(StrategyUISoundPaths.SectorWindowOpen);
            return true;
        }

        UIWindow window = planetSystemWindowController.FindWindow(sector);
        if (window == null)
            return false;

        strategyWindowManager.Focus(window);
        return true;
    }

    /// <summary>
    /// Plays the configured open or bookmark-expansion sound for a planet feature window.
    /// </summary>
    /// <param name="icon">The opened feature category.</param>
    /// <param name="restored">Whether the window was restored from a bookmark.</param>
    private void PlayPlanetWindowOpenSound(PlanetIcon icon, bool restored)
    {
        StrategyWindowSoundTheme sounds = uiContext?.GetPlayerFactionTheme()?.StrategyWindowSounds;
        if (restored)
        {
            PlayStrategySfx(sounds?.PlanetWindowExpandSoundPath);
            return;
        }

        if (icon != PlanetIcon.Mission)
            PlayStrategySfx(sounds?.PlanetWindowOpenSoundPath);
    }

    /// <summary>
    /// Plays one non-empty strategy sound-effect path.
    /// </summary>
    /// <param name="path">The resource path to play.</param>
    private static void PlayStrategySfx(string path)
    {
        if (!string.IsNullOrEmpty(path))
            PlaySfx(path);
    }

    /// <summary>
    /// Opens the galactic-information selector after cancelling competing interactions.
    /// </summary>
    private void OpenGalacticInformationDisplay()
    {
        targetingController?.Cancel();
        contextMenuController?.Cancel();
        strategyContextMenu?.Reset();
        galacticInformationDisplayController?.Show();
    }

    /// <summary>
    /// Cancels active targeting requested by the strategy overlay.
    /// </summary>
    private void HandleTargetingCancelRequested()
    {
        inputController?.CancelTargeting();
    }

    /// <summary>
    /// Closes a registered strategy window and plays its configured collapse sound.
    /// </summary>
    /// <param name="window">The registered window to close.</param>
    private void CloseWindow(UIWindow window)
    {
        CloseWindow(window, true);
    }

    /// <summary>
    /// Closes a registered strategy window and releases its transient interaction state.
    /// </summary>
    /// <param name="window">The registered window to close.</param>
    /// <param name="playPlanetWindowSound">Whether to play the planet-window collapse sound.</param>
    private void CloseWindow(UIWindow window, bool playPlanetWindowSound)
    {
        if (window == null || strategyWindowManager.GetWindowById(window.Id) != window)
            return;

        bool resumeMusic = strategyWindowManager.TryGetWindowView(
            window,
            out BattleAlertWindowView _
        );
        ClearClosingWindowState(window);
        if (playPlanetWindowSound && IsPlanetWindow(window))
        {
            PlaySfx(
                uiContext
                    ?.GetPlayerFactionTheme()
                    ?.StrategyWindowSounds?.PlanetWindowCollapseSoundPath
            );
        }

        strategyWindowManager.DestroyWindow(window);
        if (resumeMusic)
            ResumeStrategyMusic();

        MarkDirty();
    }

    /// <summary>
    /// Reports whether a registered shell hosts a planet feature window.
    /// </summary>
    /// <param name="window">The registered window to inspect.</param>
    /// <returns>True when the shell hosts a planet feature view.</returns>
    private bool IsPlanetWindow(UIWindow window)
    {
        return strategyWindowManager.TryGetWindowView(window, out FacilityWindowView _)
            || strategyWindowManager.TryGetWindowView(window, out FleetWindowView _)
            || strategyWindowManager.TryGetWindowView(window, out DefenseWindowView _)
            || strategyWindowManager.TryGetWindowView(window, out MissionsWindowView _);
    }

    /// <summary>
    /// Clears transient input, drag, targeting, and menu state owned by a closing window.
    /// </summary>
    /// <param name="window">The closing strategy window.</param>
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

    /// <summary>
    /// Resolves the galaxy-map mission target beneath a drag pointer.
    /// </summary>
    /// <param name="eventData">The current drag pointer event.</param>
    /// <returns>The resolved mission target, or null when no planet is hit.</returns>
    private StrategyMissionTarget GetGalaxyMapDropTarget(PointerEventData eventData)
    {
        return
            galaxyMapController != null
            && galaxyMapController.TryGetMissionTarget(eventData, out StrategyMissionTarget target)
            ? target
            : null;
    }

    /// <summary>
    /// Opens the strategy destination represented by one Finder result.
    /// </summary>
    /// <param name="mode">The requesting Finder category.</param>
    /// <param name="row">The selected Finder result.</param>
    /// <returns>True when the Finder window may close.</returns>
    private bool OpenFinderTarget(FinderMode mode, FinderWindowRow row)
    {
        if (row?.Planet == null)
            return false;

        Vector2Int position = GetSystemSourcePosition(row.Planet.Sector);
        OpenPlanetSystemWindow(row.Planet.Sector, position.x, position.y);
        UIWindow window = OpenPlanetWindowAt(row.Planet, row.TargetIcon, position.x, position.y);
        switch (row.TargetIcon)
        {
            case PlanetIcon.Fleet:
                if (strategyWindowManager.TryGetWindowView(window, out FleetWindowView fleetView))
                    fleetWindowController.SelectFinderTarget(fleetView, row);
                return true;
            case PlanetIcon.Mission:
                if (
                    strategyWindowManager.TryGetWindowView(
                        window,
                        out MissionsWindowView missionsView
                    )
                )
                    missionsWindowController.SelectFinderTarget(missionsView, row);
                return true;
            case PlanetIcon.Defense:
                if (
                    !strategyWindowManager.TryGetWindowView(
                        window,
                        out DefenseWindowView defenseView
                    )
                )
                    return false;

                if (mode == FinderMode.Troops)
                    defenseWindowController.SelectFinderTab(
                        defenseView,
                        DefenseWindowTab.Regiments
                    );
                else if (mode == FinderMode.Personnel)
                    defenseWindowController.SelectFinderTab(
                        defenseView,
                        DefenseWindowTab.Personnel
                    );
                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// Resolves a visible sector's source-space map position.
    /// </summary>
    /// <param name="sector">The visible sector.</param>
    /// <returns>The sector's source-space position, or zero when unavailable.</returns>
    private Vector2Int GetSystemSourcePosition(GalaxyMapSector sector)
    {
        return galaxyMapController != null && sector?.System != null
            ? galaxyMapController.GetSystemSourcePosition(sector)
            : Vector2Int.zero;
    }

    /// <summary>
    /// Invalidates the strategy presentation for the next update.
    /// </summary>
    private void MarkDirty()
    {
        dirty = true;
    }

    /// <summary>
    /// Clears retained window move-preview state.
    /// </summary>
    private void ClearWindowMovePreview()
    {
        windowMovePreviewVisible = false;
        windowMovePreviewBounds = default;
    }

    /// <summary>
    /// Registers strategy interaction owners in front-to-back cancellation order.
    /// </summary>
    private void RegisterCancelHandlers()
    {
        if (!initialized || !isActiveAndEnabled || cancelHandlersRegistered || cancelStack == null)
            return;

        cancelStack.Register(strategyWindowManager);
        cancelStack.Register(inputController);
        cancelStack.Register(strategyContextMenuRouter);
        cancelStack.Register(galacticInformationDisplayController);
        cancelHandlersRegistered = true;
    }

    /// <summary>
    /// Removes strategy interaction owners from the cancellation stack.
    /// </summary>
    private void UnregisterCancelHandlers()
    {
        if (!cancelHandlersRegistered)
            return;

        cancelStack.Unregister(galacticInformationDisplayController);
        cancelStack.Unregister(strategyContextMenuRouter);
        cancelStack.Unregister(inputController);
        cancelStack.Unregister(strategyWindowManager);
        cancelHandlersRegistered = false;
    }

    /// <summary>
    /// Determines whether map interaction is unobstructed by menus or modal windows.
    /// </summary>
    /// <returns>True when the galaxy map may accept interaction.</returns>
    private bool CanInteractWithGalaxy()
    {
        return strategyContextMenuRouter?.IsOpen != true
            && galacticInformationDisplayController?.Open != true
            && !strategyWindowManager.HasModalWindow();
    }

    /// <summary>
    /// Converts a screen position into strategy source-space coordinates.
    /// </summary>
    /// <param name="eventData">The pointer event supplying the event camera.</param>
    /// <param name="screenPosition">The screen-space position to convert.</param>
    /// <param name="x">Receives the source-space horizontal coordinate.</param>
    /// <param name="y">Receives the source-space vertical coordinate.</param>
    /// <returns>True when the position lies within the strategy surface.</returns>
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

        if (strategySurface == null)
            return false;

        if (
            !UILayout.TryGetSourcePosition(
                strategySurface,
                screenPosition,
                eventData.pressEventCamera,
                out Vector2Int sourcePosition
            )
        )
            return false;

        x = sourcePosition.x;
        y = sourcePosition.y;
        return true;
    }
}
