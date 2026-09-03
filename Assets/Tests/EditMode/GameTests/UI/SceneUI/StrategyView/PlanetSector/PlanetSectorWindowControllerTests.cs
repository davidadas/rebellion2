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
            _controller.Initialize(_actions, _actions, _actions, (_, _) => { });
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

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

        [Test]
        public void Initialize_NullWindowActions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _controller.Initialize(null, _actions, _actions, (_, _) => { })
            );
        }

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

        [Test]
        public void TryInitializeWindow_NullInput_ReturnsFalse()
        {
            bool initialized = _controller.TryInitializeWindow(null, null, null, 0);

            Assert.IsFalse(initialized);
        }

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

        [Test]
        public void SetSectorPosition_InitializedWindow_UpdatesSessionSlot()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow _);

            _controller.SetSectorPosition(view, SectorWindowPositions.Right);

            Assert.AreEqual(SectorWindowPositions.Right, _controller.GetSectorPosition(view));
        }

        [Test]
        public void Swap_InitializedWindow_MovesToNextSlotAndMarksDirty()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);

            _controller.Swap(window);

            Vector2Int position = GetWindowPosition(SectorWindowPositions.Middle);
            Assert.AreEqual(SectorWindowPositions.Middle, _controller.GetSectorPosition(view));
            Assert.AreEqual(position, new Vector2Int(window.X, window.Y));
            Assert.AreEqual(2, _dirtyCount);
        }

        [Test]
        public void ReconcileWindows_MatchingSectorIdentity_ReplacesSectorSnapshot()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow window);
            GalaxyMapSector freshSector = CreateFreshSector();

            _controller.ReconcileWindows(new[] { freshSector });

            Assert.AreSame(freshSector, _controller.GetSector(view));
            Assert.AreSame(window, _controller.FindWindow(freshSector));
        }

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
            CollectionAssert.AreEqual(
                new ISceneNode[] { _fleet },
                _controller.GetContextItems(view)
            );
            Assert.AreSame(_planet, target.Planet);
            Assert.AreSame(_fleet, target.Item);
        }

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
            _controller.Initialize(_actions, _actions, _actions, (_, _) => { });
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

        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesSectorAssociation()
        {
            PlanetSectorWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetSector(view));
            Assert.AreEqual(-1, _controller.GetSectorPosition(view));
        }

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

        private StrategyFleetCommandController CreateFleetCommandController()
        {
            return new StrategyFleetCommandController(
                () => _gameManager.GetGame(),
                () => _gameManager.FleetSystem,
                () => _gameManager.BombardmentSystem,
                () => _gameManager.PlanetaryAssaultSystem
            );
        }

        private GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            game.GetFactions().Add(new Faction { InstanceID = _opposingFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return game;
        }

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

        private PlanetSectorWindowView OpenWindow(out UIWindow window)
        {
            _controller.Open(_sector);
            return GetOpenView(out window);
        }

        private PlanetSectorWindowView GetOpenView(out UIWindow window)
        {
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out PlanetSectorWindowView view);
            return view;
        }

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

        private static T GetField<T>(object owner, string fieldName)
        {
            return (T)
                owner
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(owner);
        }

        private static Vector2Int GetWindowPosition(int position)
        {
            return new Vector2Int(100 + position * 10, 200 + position * 20);
        }

        private void CloseWindow(UIWindow window, bool immediate)
        {
            _windowManager.DestroyWindow(window);
        }

        private void MarkDirty()
        {
            _dirtyCount++;
        }

        private static TargetingRequest CreateRequest(StrategyMenuAction action)
        {
            return CreateRequest(action, Array.Empty<ISceneNode>());
        }

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
            public void OnTargetSelected(TargetingRequest request, object target) { }

            public void OnTargetingCancelled(TargetingRequest request) { }
        }

        private sealed class TestActions
            : IPlanetSectorWindowActions,
                IStrategyWindowCommandActions,
                IStrategyConfirmationActions
        {
            public int RefreshCount { get; private set; }
            public int TargetedCommandCount { get; private set; }
            public GameResult LastBattleResult { get; private set; }
            public IReadOnlyList<ISceneNode> LastItems { get; private set; }
            public StrategyMissionTarget LastTarget { get; private set; }
            public StrategyWindowTargetingSource LastTargetingSource { get; private set; }

            public bool CanRetire(IReadOnlyList<ISceneNode> items) => false;

            public void ExecuteTargetedCommand(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            )
            {
                TargetedCommandCount++;
                LastTargetingSource = source;
                LastTarget = target;
            }

            public void OpenPlanetSectorBattleResult(GameResult result)
            {
                LastBattleResult = result;
            }

            public void RefreshPlanetSectorState()
            {
                RefreshCount++;
            }

            public void OpenPlanetSectorPlanetWindow(
                GalaxyMapPlanet planet,
                PlanetIcon icon,
                int sourceX,
                int sourceY
            ) { }

            public void OpenPlanetSectorInfo(StrategyStatusTarget target) { }

            public void OpenPlanetSectorStatus(StrategyStatusTarget target) { }

            public void OpenScrapConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            public void OpenStopConstructionConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            public void OpenRetireConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            public void OpenMissionCreateWindow(
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                LastItems = items;
            }

            public bool TryExecuteMove(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                LastItems = items;
                return true;
            }

            public void OpenMoveConfirmWindow(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                LastItems = items;
            }

            public bool TryAppendFleetWaypoint(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            ) => false;

            public bool TryCommitFleetWaypointPlan(StrategyWindowTargetingSource source) => false;

            public bool TryUndoFleetWaypointPlan(StrategyWindowTargetingSource source) => false;

            public bool ClearFleetWaypoints(IReadOnlyList<ISceneNode> items) => false;
        }
    }
}
