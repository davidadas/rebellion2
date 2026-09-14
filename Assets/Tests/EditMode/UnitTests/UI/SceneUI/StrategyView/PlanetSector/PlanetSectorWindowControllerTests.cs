using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSector
{
    [TestFixture]
    public class PlanetSectorWindowControllerTests
    {
        private const string _opposingFactionId = "FNEMP1";
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private PlanetSectorWindowController _controller;
        private int _dirtyCount;
        private GameFleet _fleet;
        private StrategyFleetCommandController _fleetCommandController;
        private GameRoot _game;
        private GameManager _gameManager;
        private GalaxyMapPlanet _planet;
        private GameObject _rootObject;
        private GalaxyMapSector _sector;
        private GalaxyPlanetSector _planetSector;
        private TargetingController _targetingController;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            _game = CreateGame();
            _uiContext = TestContent.CreateUIContext(
                _game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _sector = CreateSector();
            _gameManager = TestContent.CreateGameManager(_game);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _targetingController = new TargetingController();
            _actions = new TestActions();
            _fleetCommandController = CreateFleetCommandController();
            _controller = CreateController();
            _controller.Initialize(_actions, _actions, _actions, _actions, (_, _) => { });
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        /// <summary>
        /// Verifies constructor null dependency throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullDependency_ThrowsArgumentNullException()
        {
            StrategyFleetCommandController fleetCommands = CreateFleetCommandController();

            Assert.Throws<ArgumentNullException>(() =>
                new PlanetSectorWindowController(
                    fleetCommands,
                    null,
                    _targetingController,
                    _windowLayer,
                    _windowManager,
                    () => new[] { _sector },
                    GetWindowPosition,
                    CloseWindow,
                    MarkDirty
                )
            );
        }

        /// <summary>
        /// Verifies initialize null window actions throws argument null exception.
        /// </summary>
        [Test]
        public void Initialize_NullWindowActions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _controller.Initialize(null, _actions, _actions, _actions, (_, _) => { })
            );
        }

        /// <summary>
        /// Verifies bind window before initialize throws invalid operation exception.
        /// </summary>
        [Test]
        public void BindWindow_BeforeInitialize_ThrowsInvalidOperationException()
        {
            PlanetSectorWindowController controller = CreateController();
            PlanetSectorWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.PlanetSectorWindowPrefab,
                _rootObject.transform
            );
            UIWindow window = view.GetComponent<UIWindow>();

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view, window));
        }

        /// <summary>
        /// Verifies try initialize window null input returns false.
        /// </summary>
        [Test]
        public void TryInitializeWindow_NullInput_ReturnsFalse()
        {
            bool initialized = _controller.TryInitializeWindow(null, null, null, 0);

            Assert.IsFalse(initialized);
        }

        /// <summary>
        /// Verifies open valid sector creates named window in first slot.
        /// </summary>
        [Test]
        public void Open_ValidSector_CreatesNamedWindowInFirstSlot()
        {
            bool opened = _controller.Open(_sector);
            PlanetSectorWindowView view = GetOpenView(out UIWindow window);
            Vector2Int expectedPosition = GetWindowPosition(SectorWindowPositions.Left);

            Assert.IsTrue(opened);
            Assert.AreEqual($"PlanetSectorWindow-{_planetSector.GetDisplayName()}", view.name);
            Assert.AreEqual(expectedPosition, new Vector2Int(window.X, window.Y));
            Assert.AreSame(_sector, _controller.GetSector(view));
            Assert.AreEqual(SectorWindowPositions.Left, _controller.GetSectorPosition(view));
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies open existing sector returns false without additional window.
        /// </summary>
        [Test]
        public void Open_ExistingSector_ReturnsFalseWithoutAdditionalWindow()
        {
            bool first = _controller.Open(_planetSector);

            bool second = _controller.Open(_sector);

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies set sector position initialized window updates session slot.
        /// </summary>
        [Test]
        public void TryOpenInAvailableSlot_AvailableSlot_OpensWithoutReplacingWindow()
        {
            GalaxyMapSector secondSector = CreateSector("second", "Second Sector");
            _controller.Open(_sector);

            bool opened = _controller.TryOpenInAvailableSlot(secondSector);

            Assert.IsTrue(opened);
            Assert.AreEqual(2, _windowManager.Windows.Count);
            Assert.IsNotNull(_controller.FindWindow(_sector));
            Assert.IsNotNull(_controller.FindWindow(secondSector));
        }

        /// <summary>
        /// Verifies that opening with every slot occupied does not replace a window.
        /// </summary>
        [Test]
        public void TryOpenInAvailableSlot_AllSlotsOccupied_DoesNotReplaceWindow()
        {
            GalaxyMapSector secondSector = CreateSector("second", "Second Sector");
            GalaxyMapSector thirdSector = CreateSector("third", "Third Sector");
            GalaxyMapSector fourthSector = CreateSector("fourth", "Fourth Sector");
            _controller.Open(_sector);
            _controller.Open(secondSector);
            _controller.Open(thirdSector);

            bool opened = _controller.TryOpenInAvailableSlot(fourthSector);

            Assert.IsFalse(opened);
            Assert.AreEqual(3, _windowManager.Windows.Count);
            Assert.IsNotNull(_controller.FindWindow(_sector));
            Assert.IsNotNull(_controller.FindWindow(secondSector));
            Assert.IsNotNull(_controller.FindWindow(thirdSector));
            Assert.IsNull(_controller.FindWindow(fourthSector));
        }

        /// <summary>
        /// Verifies that moving an initialized sector window updates its session slot.
        /// </summary>
        [Test]
        public void TryOpenAtPosition_AvailableAuthoredSlot_RestoresRequestedSlot()
        {
            bool opened = _controller.TryOpenAtPosition(_sector, SectorWindowPositions.Right);
            PlanetSectorWindowView view = GetOpenView(out UIWindow window);

            Assert.IsTrue(opened);
            Assert.AreEqual(SectorWindowPositions.Right, _controller.GetSectorPosition(view));
            Assert.AreEqual(
                GetWindowPosition(SectorWindowPositions.Right),
                new Vector2Int(window.X, window.Y)
            );
        }

        [Test]
        public void TryOpenAtPosition_OccupiedAuthoredSlot_DoesNotReplaceWindow()
        {
            GalaxyMapSector secondSector = CreateSector("second", "Second Sector");
            _controller.TryOpenAtPosition(_sector, SectorWindowPositions.Middle);

            bool opened = _controller.TryOpenAtPosition(secondSector, SectorWindowPositions.Middle);

            Assert.IsFalse(opened);
            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.IsNotNull(_controller.FindWindow(_sector));
            Assert.IsNull(_controller.FindWindow(secondSector));
        }

        [Test]
        public void SetSectorPosition_InitializedWindow_UpdatesSessionSlot()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow _);

            _controller.SetSectorPosition(view, SectorWindowPositions.Right);

            Assert.AreEqual(SectorWindowPositions.Right, _controller.GetSectorPosition(view));
        }

        /// <summary>
        /// Verifies swap initialized window moves to next slot and marks dirty.
        /// </summary>
        [Test]
        public void Swap_InitializedWindow_MovesToNextSlotAndNotifiesChange()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            bool moved = false;
            window.Moved += _ => moved = true;

            _controller.Swap(window);

            Vector2Int position = GetWindowPosition(SectorWindowPositions.Middle);
            Assert.AreEqual(SectorWindowPositions.Middle, _controller.GetSectorPosition(view));
            Assert.AreEqual(position, new Vector2Int(window.X, window.Y));
            Assert.IsTrue(moved);
            Assert.AreEqual(2, _dirtyCount);
        }

        /// <summary>
        /// Verifies reconcile windows matching sector identity replaces sector snapshot.
        /// </summary>
        [Test]
        public void ReconcileWindows_MatchingSectorIdentity_ReplacesSectorSnapshot()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            GalaxyMapSector freshSector = CreateFreshSector();

            _controller.ReconcileWindows(new[] { freshSector });

            Assert.AreSame(freshSector, _controller.GetSector(view));
            Assert.AreSame(window, _controller.FindWindow(freshSector));
        }

        /// <summary>
        /// Verifies try create context menu no element returns disabled planet commands.
        /// </summary>
        [Test]
        public void TryCreateContextMenu_NoElement_ReturnsDisabledPlanetCommands()
        {
            OpenWindow(out UIWindow window);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 177, 4, 6, 7),
                null,
                10,
                20
            );

            bool created = _controller.TryCreateContextMenu(
                context,
                out ContextMenuRequest request,
                out int width
            );

            Assert.IsTrue(created);
            Assert.AreEqual(177, width);
            Assert.AreEqual(2, request.Commands.Count);
            Assert.IsFalse(((StrategyMenuCommand)request.Commands[0]).Enabled);
            Assert.IsFalse(((StrategyMenuCommand)request.Commands[1]).Enabled);
        }

        /// <summary>
        /// Verifies try create context menu fleet element selects fleet context and status.
        /// </summary>
        [Test]
        public void TryCreateContextMenu_FleetElement_SelectsFleetContextAndStatus()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window);
            PointerEventData eventData = CreateFleetPointerEvent(view);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 177, 4, 6, 7),
                eventData,
                10,
                20
            );

            bool created = _controller.TryCreateContextMenu(
                context,
                out ContextMenuRequest request,
                out int _
            );
            StrategyStatusTarget target = _controller.GetStatusTarget(view);

            Assert.IsTrue(created);
            Assert.AreEqual(8, request.Commands.Count);
            Assert.IsFalse(
                request
                    .Commands.Cast<StrategyMenuCommand>()
                    .Any(command => command.Action == StrategyMenuAction.ToggleIdleBarTracking)
            );
            CollectionAssert.AreEqual(
                new ISceneNode[] { _fleet },
                _controller.GetContextItems(view)
            );
            Assert.AreSame(_planet, target.Planet);
            Assert.AreSame(_fleet, target.Item);
        }

        /// <summary>
        /// Verifies try create context menu planet image offers planet tracking.
        /// </summary>
        [Test]
        public void TryCreateContextMenu_PlanetImage_OffersPlanetTracking()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 177, 4, 6, 7),
                CreatePlanetPointerEvent(view),
                10,
                20
            );

            bool created = _controller.TryCreateContextMenu(
                context,
                out ContextMenuRequest request,
                out int _
            );

            Assert.IsTrue(created);
            StrategyMenuCommand command = request
                .Commands.Cast<StrategyMenuCommand>()
                .Single(item => item.Action == StrategyMenuAction.ToggleIdleBarTracking);

            _controller.OnContextMenuCommandSelected(request, command);

            Assert.AreSame(_planet.Planet, _actions.LastTrackedEntity);
        }

        /// <summary>
        /// Verifies create planet context menu planet uses normal planet commands.
        /// </summary>
        [Test]
        public void CreatePlanetContextMenu_Planet_UsesNormalPlanetCommands()
        {
            ContextMenuRequest request = _controller.CreatePlanetContextMenu(_planet, 10, 20);

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyMenuAction.Encyclopedia,
                    StrategyMenuAction.Status,
                    StrategyMenuAction.ToggleIdleBarTracking,
                },
                request
                    .Commands.Cast<StrategyMenuCommand>()
                    .Select(command => command.Action)
                    .ToArray()
            );
            StrategyMenuCommand trackingCommand = request
                .Commands.Cast<StrategyMenuCommand>()
                .Single(command => command.Action == StrategyMenuAction.ToggleIdleBarTracking);

            _controller.OnContextMenuCommandSelected(request, trackingCommand);

            Assert.AreSame(_planet.Planet, _actions.LastTrackedEntity);
            Assert.AreSame(_controller, request.Receiver);
        }

        /// <summary>
        /// Verifies try create context menu facility image does not offer planet tracking.
        /// </summary>
        [Test]
        public void TryCreateContextMenu_FacilityImage_DoesNotOfferPlanetTracking()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 177, 4, 6, 7),
                CreateOverlayPointerEvent(view, "facilityImage"),
                10,
                20
            );

            bool created = _controller.TryCreateContextMenu(
                context,
                out ContextMenuRequest request,
                out int _
            );

            Assert.IsTrue(created);
            Assert.IsFalse(
                request
                    .Commands.Cast<StrategyMenuCommand>()
                    .Any(command => command.Action == StrategyMenuAction.ToggleIdleBarTracking)
            );
        }

        /// <summary>
        /// Verifies context menu planetary assault executes and routes battle result.
        /// </summary>
        [Test]
        public void ContextMenu_PlanetaryAssault_ExecutesAndRoutesBattleResult()
        {
            _planet.Planet.OwnerInstanceID = _opposingFactionId;
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "assault-ship",
                OwnerInstanceID = _playerFactionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                RegimentCapacity = 1,
            };
            _game.AttachNode(ship, _fleet);
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = "assault-regiment",
                    OwnerInstanceID = _playerFactionId,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ship
            );
            _fleetCommandController = CreateFleetCommandController();
            _controller = CreateController();
            _controller.Initialize(_actions, _actions, _actions, _actions, (_, _) => { });
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 177, 4, 6, 7),
                CreateFleetPointerEvent(view),
                10,
                20
            );
            _controller.TryCreateContextMenu(context, out ContextMenuRequest request, out _);
            StrategyMenuCommand command = request
                .Commands.Cast<StrategyMenuCommand>()
                .Single(item => item.Action == StrategyMenuAction.PlanetaryAssault);
            ContextMenuController contextMenuController = new ContextMenuController();
            contextMenuController.Open(request);

            bool selected = contextMenuController.TrySelectCommand(command);

            Assert.IsTrue(selected);
            Assert.IsInstanceOf<PlanetaryAssaultResult>(_actions.LastBattleResult);
            Assert.AreEqual(1, _actions.RefreshCount);
        }

        /// <summary>
        /// Verifies clear selection selected fleet clears context and status.
        /// </summary>
        [Test]
        public void ClearSelection_SelectedFleet_ClearsContextAndStatus()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window);
            CaptureFleetContext(view, window);

            _controller.ClearSelection(view);

            Assert.IsEmpty(_controller.GetContextItems(view));
            Assert.IsNull(_controller.GetStatusTarget(view));
        }

        /// <summary>
        /// Verifies planet pressed fleet icon marks dirty.
        /// </summary>
        [Test]
        public void PlanetPressed_FleetIcon_MarksDirty()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window);
            PlanetSectorPlanetView planetView =
                view.GetComponentsInChildren<PlanetSectorPlanetView>(true)
                    .Single(item => item.name == "Planet0");
            PointerEventData eventData = CreateFleetPointerEvent(
                view,
                PointerEventData.InputButton.Left
            );

            planetView.OnPointerDown(eventData);

            Assert.AreEqual(2, _dirtyCount);
        }

        /// <summary>
        /// Verifies on target selected known actions route shared commands.
        /// </summary>
        [Test]
        public void OnTargetSelected_KnownActions_RouteSharedCommands()
        {
            StrategyMissionTarget target = new StrategyMissionTarget(_planet, null);
            IReadOnlyList<ISceneNode> items = new ISceneNode[] { _fleet };

            _controller.OnTargetSelected(
                CreateRequest(StrategyMenuAction.CreateMission, items),
                target
            );
            _controller.OnTargetSelected(CreateRequest(StrategyMenuAction.Move, items), target);
            _controller.OnTargetSelected(
                CreateRequest(StrategyMenuAction.MoveConfirm, items),
                target
            );

            Assert.AreEqual(3, _actions.TargetedCommandCount);
            Assert.AreEqual(StrategyMenuAction.MoveConfirm, _actions.LastTargetingSource.Action);
            CollectionAssert.AreEqual(items, _actions.LastTargetingSource.Items);
            Assert.AreSame(target, _actions.LastTarget);
        }

        /// <summary>
        /// Verifies view destroyed initialized session releases sector association.
        /// </summary>
        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesSectorAssociation()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetSector(view));
            Assert.AreEqual(-1, _controller.GetSectorPosition(view));
        }

        /// <summary>
        /// Verifies create target for hit create mission on planet overlay icon targets planet.
        /// </summary>
        [Test]
        public void CreateTargetForHit_CreateMissionOnPlanetOverlayIcon_TargetsPlanet()
        {
            PlanetSectorWindowHit hit = CreateHit(PlanetIcon.Facility, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyMenuAction.CreateMission);

            StrategyMissionTarget target = PlanetSectorWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.AreSame(hit.GalaxyMapPlanet.Planet, target.Item);
        }

        /// <summary>
        /// Verifies create target for hit destination on fleet overlay icon targets planet.
        /// </summary>
        [Test]
        public void CreateTargetForHit_DestinationOnFleetOverlayIcon_TargetsPlanet()
        {
            PlanetSectorWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyMenuAction.Destination);

            StrategyMissionTarget target = PlanetSectorWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.AreSame(hit.GalaxyMapPlanet.Planet, target.Item);
        }

        /// <summary>
        /// Verifies create target for hit move on fleet overlay icon targets fleet.
        /// </summary>
        [Test]
        public void CreateTargetForHit_MoveOnFleetOverlayIcon_TargetsFleet()
        {
            PlanetSectorWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyMenuAction.Move);

            StrategyMissionTarget target = PlanetSectorWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.AreSame(fleet, target.Item);
        }

        /// <summary>
        /// Verifies create target for hit move confirm on fleet overlay icon targets fleet.
        /// </summary>
        [Test]
        public void CreateTargetForHit_MoveConfirmOnFleetOverlayIcon_TargetsFleet()
        {
            PlanetSectorWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyMenuAction.MoveConfirm);

            StrategyMissionTarget target = PlanetSectorWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.AreSame(fleet, target.Item);
        }

        /// <summary>
        /// Verifies create target for hit empty hit returns null.
        /// </summary>
        [Test]
        public void CreateTargetForHit_EmptyHit_ReturnsNull()
        {
            PlanetSectorWindowHit hit = CreateHit(PlanetIcon.None, false);

            StrategyMissionTarget missingHit = PlanetSectorWindowController.CreateTargetForHit(
                null,
                CreateRequest(StrategyMenuAction.Move),
                _fleet
            );
            StrategyMissionTarget emptyHit = PlanetSectorWindowController.CreateTargetForHit(
                hit,
                CreateRequest(StrategyMenuAction.Move),
                _fleet
            );

            Assert.IsNull(missingHit);
            Assert.IsNull(emptyHit);
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
        private PlanetSectorWindowController CreateController()
        {
            return new PlanetSectorWindowController(
                _fleetCommandController,
                () => _uiContext,
                _targetingController,
                _windowLayer,
                _windowManager,
                () => new[] { _sector },
                GetWindowPosition,
                CloseWindow,
                MarkDirty
            );
        }

        /// <summary>
        /// Creates fleet command controller.
        /// </summary>
        /// <returns>The created fleet command controller.</returns>
        private StrategyFleetCommandController CreateFleetCommandController()
        {
            return new StrategyFleetCommandController(
                () => _gameManager.GetGame(),
                () => _gameManager.FleetSystem,
                () => _gameManager.BombardmentSystem,
                () => _gameManager.PlanetaryAssaultSystem
            );
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        private GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            game.GetFactions().Add(new Faction { InstanceID = _opposingFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return game;
        }

        /// <summary>
        /// Creates sector.
        /// </summary>
        /// <returns>The created sector.</returns>
        private GalaxyMapSector CreateSector()
        {
            _planetSector = new GalaxyPlanetSector
            {
                InstanceID = "sector",
                DisplayName = "Core Sector",
            };
            _game.AttachNode(_planetSector, _game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            _game.AttachNode(planet, _planetSector);
            _fleet = new GameFleet
            {
                InstanceID = "fleet",
                DisplayName = "Fleet One",
                OwnerInstanceID = _playerFactionId,
            };
            _game.AttachNode(_fleet, planet);
            _planet = new GalaxyMapPlanet(_planetSector, planet, planet.GetPlanetIconPath());
            return new GalaxyMapSector(_planetSector, new[] { _planet });
        }

        /// <summary>
        /// Creates fresh sector.
        /// </summary>
        /// <returns>The created fresh sector.</returns>
        private GalaxyMapSector CreateFreshSector()
        {
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector
            {
                InstanceID = _planetSector.InstanceID,
                DisplayName = "Fresh Sector",
            };
            Planet planet = new Planet
            {
                InstanceID = _planet.Planet.InstanceID,
                DisplayName = "Fresh Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            GalaxyMapPlanet strategyPlanet = new GalaxyMapPlanet(
                planetSector,
                planet,
                planet.GetPlanetIconPath()
            );
            return new GalaxyMapSector(planetSector, new[] { strategyPlanet });
        }

        /// <summary>
        /// Creates an empty galaxy-map sector with the requested identity and label.
        /// </summary>
        /// <param name="instanceId">The sector instance identifier.</param>
        /// <param name="displayName">The displayed sector name.</param>
        /// <returns>The created galaxy-map sector.</returns>
        private static GalaxyMapSector CreateSector(string instanceId, string displayName)
        {
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector
            {
                InstanceID = instanceId,
                DisplayName = displayName,
            };
            return new GalaxyMapSector(planetSector, Array.Empty<GalaxyMapPlanet>());
        }

        /// <summary>
        /// Opens the configured planet-sector window.
        /// </summary>
        /// <param name="window">Receives the opened window container.</param>
        /// <returns>The opened planet-sector view.</returns>
        private PlanetSectorWindowView OpenWindow(out UIWindow window)
        {
            _controller.Open(_sector);
            return GetOpenView(out window);
        }

        /// <summary>
        /// Gets open view.
        /// </summary>
        /// <param name="window">Receives the window.</param>
        /// <returns>The requested open view.</returns>
        private PlanetSectorWindowView GetOpenView(out UIWindow window)
        {
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out PlanetSectorWindowView view);
            return view;
        }

        /// <summary>
        /// Captures fleet context.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="window">The window.</param>
        private void CaptureFleetContext(PlanetSectorWindowView view, UIWindow window)
        {
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 177, 4, 6, 7),
                CreateFleetPointerEvent(view),
                10,
                20
            );
            _controller.TryCreateContextMenu(context, out _, out _);
        }

        /// <summary>
        /// Creates fleet pointer event.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="button">The button.</param>
        /// <returns>The created fleet pointer event.</returns>
        private static PointerEventData CreateFleetPointerEvent(
            PlanetSectorWindowView view,
            PointerEventData.InputButton button = PointerEventData.InputButton.Right
        )
        {
            PlanetSectorPlanetView planetView =
                view.GetComponentsInChildren<PlanetSectorPlanetView>(true)
                    .Single(item => item.name == "Planet0");
            RawImage fleetImage = GetField<RawImage>(planetView, "fleetImage");
            return new PointerEventData(null)
            {
                button = button,
                pointerCurrentRaycast = new RaycastResult { gameObject = fleetImage.gameObject },
                pointerPressRaycast = new RaycastResult { gameObject = fleetImage.gameObject },
            };
        }

        /// <summary>
        /// Creates planet pointer event.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="button">The button.</param>
        /// <returns>The created planet pointer event.</returns>
        private static PointerEventData CreatePlanetPointerEvent(
            PlanetSectorWindowView view,
            PointerEventData.InputButton button = PointerEventData.InputButton.Right
        )
        {
            PlanetSectorPlanetView planetView =
                view.GetComponentsInChildren<PlanetSectorPlanetView>(true)
                    .Single(item => item.name == "Planet0");
            RawImage planetImage = GetField<RawImage>(planetView, "planetImage");
            return new PointerEventData(null)
            {
                button = button,
                pointerCurrentRaycast = new RaycastResult { gameObject = planetImage.gameObject },
                pointerPressRaycast = new RaycastResult { gameObject = planetImage.gameObject },
            };
        }

        /// <summary>
        /// Creates overlay pointer event.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="imageFieldName">The image field name.</param>
        /// <returns>The created overlay pointer event.</returns>
        private static PointerEventData CreateOverlayPointerEvent(
            PlanetSectorWindowView view,
            string imageFieldName
        )
        {
            PlanetSectorPlanetView planetView =
                view.GetComponentsInChildren<PlanetSectorPlanetView>(true)
                    .Single(item => item.name == "Planet0");
            RawImage overlayImage = GetField<RawImage>(planetView, imageFieldName);
            return new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
                pointerCurrentRaycast = new RaycastResult { gameObject = overlayImage.gameObject },
                pointerPressRaycast = new RaycastResult { gameObject = overlayImage.gameObject },
            };
        }

        /// <summary>
        /// Gets field.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested field.</returns>
        private static T GetField<T>(object owner, string fieldName)
        {
            return (T)
                owner
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(owner);
        }

        /// <summary>
        /// Gets window position.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The requested window position.</returns>
        private static Vector2Int GetWindowPosition(int position)
        {
            return new Vector2Int(100 + position * 10, 200 + position * 20);
        }

        /// <summary>
        /// Executes close window.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <param name="immediate">Whether immediate.</param>
        private void CloseWindow(UIWindow window, bool immediate)
        {
            _windowManager.DestroyWindow(window);
        }

        /// <summary>
        /// Executes mark dirty.
        /// </summary>
        private void MarkDirty()
        {
            _dirtyCount++;
        }

        /// <summary>
        /// Creates request.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>The created request.</returns>
        private static TargetingRequest CreateRequest(StrategyMenuAction action)
        {
            return CreateRequest(action, Array.Empty<ISceneNode>());
        }

        /// <summary>
        /// Creates request.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="items">The items.</param>
        /// <returns>The created request.</returns>
        private static TargetingRequest CreateRequest(
            StrategyMenuAction action,
            IReadOnlyList<ISceneNode> items
        )
        {
            return new TargetingRequest(
                StrategyWindowTargetingSource.GetPrompt(action),
                new StrategyWindowTargetingSource(null, action, 0, 0, items),
                new TestTargetingReceiver()
            );
        }

        /// <summary>
        /// Creates hit.
        /// </summary>
        /// <param name="icon">The icon.</param>
        /// <param name="planetImage">Whether planet image.</param>
        /// <returns>The created hit.</returns>
        private static PlanetSectorWindowHit CreateHit(PlanetIcon icon, bool planetImage)
        {
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector();
            Planet planet = new Planet();
            GalaxyMapPlanet galaxyMapPlanet = new GalaxyMapPlanet(
                planetSector,
                planet,
                string.Empty
            );
            return new PlanetSectorWindowHit(galaxyMapPlanet, icon, planetImage);
        }

        private sealed class TestTargetingReceiver : ITargetingReceiver
        {
            /// <summary>
            /// Executes on target selected.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="target">The target.</param>
            public void OnTargetSelected(TargetingRequest request, object target) { }

            /// <summary>
            /// Executes on targeting cancelled.
            /// </summary>
            /// <param name="request">The request.</param>
            public void OnTargetingCancelled(TargetingRequest request) { }
        }

        private sealed class TestActions
            : IPlanetSectorWindowActions,
                IIdleBarTrackingActions,
                IStrategyWindowCommandActions,
                IStrategyConfirmationActions
        {
            public int RefreshCount { get; private set; }
            public int TargetedCommandCount { get; private set; }
            public GameResult LastBattleResult { get; private set; }
            public IReadOnlyList<ISceneNode> LastItems { get; private set; }
            public ISceneNode LastTrackedEntity { get; private set; }
            public StrategyMissionTarget LastTarget { get; private set; }
            public StrategyWindowTargetingSource LastTargetingSource { get; private set; }

            public bool IsIdleBarEnabled => true;

            /// <summary>
            /// Checks whether the idle bar tracked condition is met.
            /// </summary>
            /// <param name="entity">The entity.</param>
            /// <returns>True when the idle bar tracked condition is met; otherwise false.</returns>
            public bool IsIdleBarTracked(ISceneNode entity) => true;

            /// <summary>
            /// Executes toggle idle bar tracking.
            /// </summary>
            /// <param name="entity">The entity.</param>
            public void ToggleIdleBarTracking(ISceneNode entity)
            {
                LastTrackedEntity = entity;
            }

            /// <summary>
            /// Checks whether the retire condition is met.
            /// </summary>
            /// <param name="items">The items.</param>
            /// <returns>True when the retire condition is met; otherwise false.</returns>
            public bool CanRetire(IReadOnlyList<ISceneNode> items) => false;

            /// <summary>
            /// Executes targeted command.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <param name="target">The target.</param>
            public void ExecuteTargetedCommand(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            )
            {
                TargetedCommandCount++;
                LastTargetingSource = source;
                LastTarget = target;
            }

            /// <summary>
            /// Opens planet sector battle result.
            /// </summary>
            /// <param name="result">The result.</param>
            public void OpenPlanetSectorBattleResult(GameResult result)
            {
                LastBattleResult = result;
            }

            /// <summary>
            /// Refreshes planet sector state.
            /// </summary>
            public void RefreshPlanetSectorState()
            {
                RefreshCount++;
            }

            /// <summary>
            /// Opens planet sector planet window.
            /// </summary>
            /// <param name="planet">The planet.</param>
            /// <param name="icon">The icon.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenPlanetSectorPlanetWindow(
                GalaxyMapPlanet planet,
                PlanetIcon icon,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Opens planet sector info.
            /// </summary>
            /// <param name="target">The target.</param>
            public void OpenPlanetSectorInfo(StrategyStatusTarget target) { }

            /// <summary>
            /// Opens planet sector status.
            /// </summary>
            /// <param name="target">The target.</param>
            public void OpenPlanetSectorStatus(StrategyStatusTarget target) { }

            /// <summary>
            /// Opens scrap confirm window.
            /// </summary>
            /// <param name="sourceWindow">The source window.</param>
            /// <param name="items">The items.</param>
            public void OpenScrapConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            /// <summary>
            /// Opens stop construction confirm window.
            /// </summary>
            /// <param name="sourceWindow">The source window.</param>
            /// <param name="items">The items.</param>
            public void OpenStopConstructionConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            /// <summary>
            /// Opens retire confirm window.
            /// </summary>
            /// <param name="sourceWindow">The source window.</param>
            /// <param name="items">The items.</param>
            public void OpenRetireConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            /// <summary>
            /// Opens mission create window.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="items">The items.</param>
            public void OpenMissionCreateWindow(
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                LastItems = items;
            }

            /// <summary>
            /// Attempts execute move.
            /// </summary>
            /// <param name="sourceWindow">The source window.</param>
            /// <param name="target">The target.</param>
            /// <param name="items">The items.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryExecuteMove(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                LastItems = items;
                return true;
            }

            /// <summary>
            /// Opens move confirm window.
            /// </summary>
            /// <param name="sourceWindow">The source window.</param>
            /// <param name="target">The target.</param>
            /// <param name="items">The items.</param>
            public void OpenMoveConfirmWindow(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                LastItems = items;
            }

            /// <summary>
            /// Attempts append fleet waypoint.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <param name="target">The target.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryAppendFleetWaypoint(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            ) => false;

            /// <summary>
            /// Attempts commit fleet waypoint plan.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryCommitFleetWaypointPlan(StrategyWindowTargetingSource source) => false;

            /// <summary>
            /// Attempts undo fleet waypoint plan.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryUndoFleetWaypointPlan(StrategyWindowTargetingSource source) => false;

            /// <summary>
            /// Executes clear fleet waypoints.
            /// </summary>
            /// <param name="items">The items.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool ClearFleetWaypoints(IReadOnlyList<ISceneNode> items) => false;
        }
    }
}
