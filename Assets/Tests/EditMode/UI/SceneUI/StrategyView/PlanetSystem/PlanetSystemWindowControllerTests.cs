using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSystem
{
    [TestFixture]
    public class PlanetSystemWindowControllerTests
    {
        private const string _opposingFactionId = "FNALL2";
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private PlanetSystemWindowController _controller;
        private int _dirtyCount;
        private GameFleet _fleet;
        private GameRoot _game;
        private GalaxyMapPlanet _planet;
        private GameObject _rootObject;
        private GalaxyMapSector _sector;
        private GamePlanetSystem _system;
        private TargetingController _targetingController;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            _game = CreateGame();
            _uiContext = new UIContext(
                _game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _sector = CreateSector();
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _targetingController = new TargetingController();
            _actions = new TestActions();
            _controller = CreateController();
            _controller.Initialize(_actions, _actions, (_, _, _) => { });
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
                new PlanetSystemWindowController(
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
                _controller.Initialize(null, _actions, (_, _, _) => { })
            );
        }

        [Test]
        public void BindWindow_BeforeInitialize_ThrowsInvalidOperationException()
        {
            PlanetSystemWindowController controller = CreateController();
            PlanetSystemWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.PlanetSystemWindowPrefab,
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
            bool opened = _controller.Open(_sector, 500, 600);
            PlanetSystemWindowView view = GetOpenView(out UIWindow window);
            Vector2Int requestedPosition = GetWindowPosition(SectorWindowPositions.Left);
            Vector2Int expectedPosition = _windowManager.ClampPosition(
                requestedPosition.x,
                requestedPosition.y,
                _windowLayer.GetWindowSize(_windowLayer.PlanetSystemWindowPrefab)
            );

            Assert.IsTrue(opened);
            Assert.AreEqual($"PlanetSystemWindow-{_system.GetDisplayName()}", view.name);
            Assert.AreEqual(expectedPosition, new Vector2Int(window.X, window.Y));
            Assert.AreSame(_sector, _controller.GetSector(view));
            Assert.AreEqual(SectorWindowPositions.Left, _controller.GetSectorPosition(view));
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void Open_ExistingSector_ReturnsFalseWithoutAdditionalWindow()
        {
            bool first = _controller.Open(_system, 10, 20);

            bool second = _controller.Open(_sector, 30, 40);

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void SetSectorPosition_InitializedWindow_UpdatesSessionSlot()
        {
            PlanetSystemWindowView view = OpenWindow(out UIWindow _);

            _controller.SetSectorPosition(view, SectorWindowPositions.Right);

            Assert.AreEqual(SectorWindowPositions.Right, _controller.GetSectorPosition(view));
        }

        [Test]
        public void Swap_InitializedWindow_MovesToNextSlotAndMarksDirty()
        {
            PlanetSystemWindowView view = OpenWindow(out UIWindow window);

            _controller.Swap(window);

            Vector2Int position = GetWindowPosition(SectorWindowPositions.Middle);
            Assert.AreEqual(SectorWindowPositions.Middle, _controller.GetSectorPosition(view));
            Assert.AreEqual(position, new Vector2Int(window.X, window.Y));
            Assert.AreEqual(2, _dirtyCount);
        }

        [Test]
        public void ReconcileWindows_MatchingSystemIdentity_ReplacesSectorSnapshot()
        {
            PlanetSystemWindowView view = OpenWindow(out UIWindow window);
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
            PlanetSystemWindowView view = OpenWindow(out UIWindow window);
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
            Assert.AreEqual(7, request.Commands.Count);
            CollectionAssert.AreEqual(
                new ISceneNode[] { _fleet },
                _controller.GetContextItems(view)
            );
            Assert.AreSame(_planet, target.Planet);
            Assert.AreSame(_fleet, target.Item);
        }

        [Test]
        public void ClearSelection_SelectedFleet_ClearsContextAndStatus()
        {
            PlanetSystemWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window);
            CaptureFleetContext(view, window);

            _controller.ClearSelection(view);

            Assert.IsEmpty(_controller.GetContextItems(view));
            Assert.IsNull(_controller.GetStatusTarget(view));
        }

        [Test]
        public void OnTargetSelected_KnownActions_RouteSharedCommands()
        {
            StrategyMissionTarget target = new StrategyMissionTarget(_planet, null);
            IReadOnlyList<ISceneNode> items = new ISceneNode[] { _fleet };

            _controller.OnTargetSelected(
                CreateRequest(StrategyContextMenuActions.CreateMission, items),
                target
            );
            _controller.OnTargetSelected(
                CreateRequest(StrategyContextMenuActions.Move, items),
                target
            );
            _controller.OnTargetSelected(
                CreateRequest(StrategyContextMenuActions.MoveConfirm, items),
                target
            );

            Assert.AreEqual(1, _actions.MissionCreateCount);
            Assert.AreEqual(1, _actions.MoveCount);
            Assert.AreEqual(1, _actions.MoveConfirmCount);
            CollectionAssert.AreEqual(items, _actions.LastItems);
        }

        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesSectorAssociation()
        {
            PlanetSystemWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetSector(view));
            Assert.AreEqual(-1, _controller.GetSectorPosition(view));
        }

        [Test]
        public void CreateTargetForHit_CreateMissionOnPlanetOverlayIcon_TargetsPlanet()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Facility, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.CreateMission);

            StrategyMissionTarget target = PlanetSystemWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.IsNull(target.Item);
        }

        [Test]
        public void CreateTargetForHit_DestinationOnFleetOverlayIcon_TargetsPlanet()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.Destination);

            StrategyMissionTarget target = PlanetSystemWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.IsNull(target.Item);
        }

        [Test]
        public void CreateTargetForHit_MoveOnFleetOverlayIcon_TargetsFleet()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.Move);

            StrategyMissionTarget target = PlanetSystemWindowController.CreateTargetForHit(
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
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.MoveConfirm);

            StrategyMissionTarget target = PlanetSystemWindowController.CreateTargetForHit(
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
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.None, false);

            StrategyMissionTarget missingHit = PlanetSystemWindowController.CreateTargetForHit(
                null,
                CreateRequest(StrategyContextMenuActions.Move),
                _fleet
            );
            StrategyMissionTarget emptyHit = PlanetSystemWindowController.CreateTargetForHit(
                hit,
                CreateRequest(StrategyContextMenuActions.Move),
                _fleet
            );

            Assert.IsNull(missingHit);
            Assert.IsNull(emptyHit);
        }

        private PlanetSystemWindowController CreateController()
        {
            return new PlanetSystemWindowController(
                CreateFleetCommandController(),
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
            return new StrategyFleetCommandController(() => _game, (_, _) => null);
        }

        private GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            game.Factions.Add(new Faction { InstanceID = _opposingFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return game;
        }

        private GalaxyMapSector CreateSector()
        {
            _system = new GamePlanetSystem { InstanceID = "system", DisplayName = "Core System" };
            _game.AttachNode(_system, _game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            _game.AttachNode(planet, _system);
            _fleet = new GameFleet
            {
                InstanceID = "fleet",
                DisplayName = "Fleet One",
                OwnerInstanceID = _playerFactionId,
            };
            _game.AttachNode(_fleet, planet);
            _planet = new GalaxyMapPlanet(_system, planet, planet.GetPlanetIconPath());
            return new GalaxyMapSector(_system, new[] { _planet });
        }

        private GalaxyMapSector CreateFreshSector()
        {
            GamePlanetSystem system = new GamePlanetSystem
            {
                InstanceID = _system.InstanceID,
                DisplayName = "Fresh System",
            };
            Planet planet = new Planet
            {
                InstanceID = _planet.Planet.InstanceID,
                DisplayName = "Fresh Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            GalaxyMapPlanet strategyPlanet = new GalaxyMapPlanet(
                system,
                planet,
                planet.GetPlanetIconPath()
            );
            return new GalaxyMapSector(system, new[] { strategyPlanet });
        }

        private PlanetSystemWindowView OpenWindow(out UIWindow window)
        {
            _controller.Open(_sector, 10, 20);
            return GetOpenView(out window);
        }

        private PlanetSystemWindowView GetOpenView(out UIWindow window)
        {
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out PlanetSystemWindowView view);
            return view;
        }

        private void CaptureFleetContext(PlanetSystemWindowView view, UIWindow window)
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

        private static PointerEventData CreateFleetPointerEvent(PlanetSystemWindowView view)
        {
            PlanetSystemPlanetView planetView =
                view.GetComponentsInChildren<PlanetSystemPlanetView>(true)
                    .Single(item => item.name == "Planet0");
            RawImage fleetImage = GetField<RawImage>(planetView, "fleetImage");
            return new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
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

        private static TargetingRequest CreateRequest(int action)
        {
            return CreateRequest(action, Array.Empty<ISceneNode>());
        }

        private static TargetingRequest CreateRequest(int action, IReadOnlyList<ISceneNode> items)
        {
            return new TargetingRequest(
                StrategyWindowTargetingSource.GetPrompt(action),
                new StrategyWindowTargetingSource(null, action, 0, 0, items),
                new TestTargetingReceiver()
            );
        }

        private static PlanetSystemWindowHit CreateHit(PlanetIcon icon, bool planetImage)
        {
            GamePlanetSystem system = new GamePlanetSystem();
            Planet planet = new Planet();
            GalaxyMapPlanet galaxyMapPlanet = new GalaxyMapPlanet(system, planet, string.Empty);
            return new PlanetSystemWindowHit(galaxyMapPlanet, icon, planetImage);
        }

        private sealed class TestTargetingReceiver : ITargetingReceiver
        {
            public void OnTargetSelected(TargetingRequest request, object target) { }

            public void OnTargetingCancelled(TargetingRequest request) { }
        }

        private sealed class TestActions : IPlanetSystemWindowActions, IStrategyWindowCommandActions
        {
            public int MissionCreateCount { get; private set; }
            public int MoveConfirmCount { get; private set; }
            public int MoveCount { get; private set; }
            public IReadOnlyList<ISceneNode> LastItems { get; private set; }

            public void RefreshPlanetSystemState() { }

            public void OpenPlanetSystemPlanetWindow(
                GalaxyMapPlanet planet,
                PlanetIcon icon,
                int sourceX,
                int sourceY
            ) { }

            public void OpenPlanetSystemInfo(StrategyStatusTarget target) { }

            public void OpenPlanetSystemStatus(StrategyStatusTarget target) { }

            public void OpenPlanetSystemScrapConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            public void OpenMissionCreateWindow(
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                MissionCreateCount++;
                LastItems = items;
            }

            public bool TryExecuteMove(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                MoveCount++;
                LastItems = items;
                return true;
            }

            public void OpenMoveConfirmWindow(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                MoveConfirmCount++;
                LastItems = items;
            }
        }
    }
}
