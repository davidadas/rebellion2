using System;
using System.Collections.Generic;
using System.Linq;
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
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Fleet
{
    [TestFixture]
    public class FleetWindowControllerTests
    {
        private const string _opposingFactionId = "FNALL2";
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private FleetWindowController _controller;
        private TestActions _actions;
        private int _dirtyCount;
        private GameRoot _game;
        private GameManager _gameManager;
        private GameFleet _fleet;
        private Officer _officer;
        private GalaxyMapPlanet _planet;
        private GameObject _rootObject;
        private StrategyFleetCommandController _fleetCommandController;
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
            _planet = CreatePlanet(_game);
            _fleet = CreateFleet("fleet", "First Fleet", out _officer);
            _planet.Planet.Fleets.Add(_fleet);
            AttachFleetGraph(_planet.Planet, _fleet);
            _gameManager = new GameManager(_game);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _targetingController = new TargetingController();
            _fleetCommandController = CreateFleetCommandController();
            _controller = CreateController();
            _actions = new TestActions();
            _controller.Initialize(
                _actions,
                _actions,
                _actions,
                (_, _, _) => { },
                _ => { },
                _ => { }
            );
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullFleetCommandController_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FleetWindowController(
                    null,
                    () => _uiContext,
                    _targetingController,
                    _windowLayer,
                    _windowManager,
                    (_, _) => Vector2Int.zero,
                    () => { }
                )
            );
        }

        [Test]
        public void Initialize_NullWindowActions_ThrowsArgumentNullException()
        {
            TestActions actions = new TestActions();

            Assert.Throws<ArgumentNullException>(() =>
                _controller.Initialize(null, actions, actions, (_, _, _) => { }, _ => { }, _ => { })
            );
        }

        [Test]
        public void TryInitializeWindow_BeforeInitialize_ThrowsInvalidOperationException()
        {
            FleetWindowController controller = CreateController();
            FleetWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.FleetWindowPrefab,
                _rootObject.transform
            );
            UIWindow window = view.GetComponent<UIWindow>();

            Assert.Throws<InvalidOperationException>(() =>
                controller.TryInitializeWindow(view, window, _planet)
            );
        }

        [Test]
        public void TryInitializeWindow_NullPlanet_ReturnsFalse()
        {
            FleetWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.FleetWindowPrefab,
                _rootObject.transform
            );
            UIWindow window = view.GetComponent<UIWindow>();

            bool initialized = _controller.TryInitializeWindow(view, window, null);

            Assert.IsFalse(initialized);
            Assert.IsNull(_controller.GetPlanet(view));
        }

        [Test]
        public void Open_ValidPlanet_CreatesNamedWindowAtResolvedPosition()
        {
            UIWindow window = _controller.Open(_planet, 20, 30, out bool created);

            Assert.IsTrue(created);
            Assert.AreEqual($"FleetWindow-{_planet.Planet.GetDisplayName()}", window.Content.name);
            Assert.AreEqual(new Vector2Int(37, 49), new Vector2Int(window.X, window.Y));
            Assert.IsFalse(window.Modal);
            Assert.AreEqual(1, _dirtyCount);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out FleetWindowView view));
            Assert.AreSame(_planet, _controller.GetPlanet(view));
            Assert.AreEqual(0, _controller.GetSelectedFleetIndex(view));
        }

        [Test]
        public void Open_ExistingPlanet_ReusesWindowWithoutAdditionalInvalidation()
        {
            UIWindow firstWindow = _controller.Open(_planet, 20, 30, out bool firstCreated);

            UIWindow secondWindow = _controller.Open(_planet, 40, 50, out bool secondCreated);

            Assert.IsTrue(firstCreated);
            Assert.IsFalse(secondCreated);
            Assert.AreSame(firstWindow, secondWindow);
            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void SelectTarget_ContainedOfficer_SelectsFleetAndPersonnelTab()
        {
            FleetWindowView view = OpenWindow(out UIWindow _);

            bool selected = _controller.SelectTarget(view, _officer);

            Assert.IsTrue(selected);
            Assert.AreEqual(FleetWindowTab.Personnel, _controller.GetActiveTab(view));
            Assert.AreEqual(0, _controller.GetSelectedFleetIndex(view));
        }

        [Test]
        public void ClearSelection_PopulatedFleet_PreservesRequiredFleetSelection()
        {
            FleetWindowView view = OpenWindow(out UIWindow window);
            _controller.SelectTarget(view, _officer);

            _controller.ClearSelection(window);

            Assert.AreEqual(0, _controller.GetSelectedFleetIndex(view));
            Assert.AreEqual(FleetWindowTab.Personnel, _controller.GetActiveTab(view));
        }

        [Test]
        public void DetailItemsDrop_ActiveTargeting_SelectsCurrentFleet()
        {
            FleetWindowView view = OpenWindow(out UIWindow window);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            _controller.RenderWindow(view, window, true);
            RecordingTargetingReceiver receiver = new RecordingTargetingReceiver();
            _targetingController.Begin(new TargetingRequest("Target", null, receiver));
            ScrollAreaView detailScrollArea = view.GetComponentsInChildren<StrategyUnitCardView>(
                    true
                )
                .Single(item => item.gameObject.activeInHierarchy)
                .GetComponentInParent<ScrollAreaView>();

            detailScrollArea.RelayDrop(new PointerEventData(null));

            Assert.IsFalse(_targetingController.IsTargeting);
            Assert.IsInstanceOf<StrategyMissionTarget>(receiver.Target);
            StrategyMissionTarget target = (StrategyMissionTarget)receiver.Target;
            Assert.AreSame(_planet, target.Planet);
            Assert.AreSame(_fleet, target.Item);
            Assert.AreSame(_fleet, target.GetMoveDestination());
        }

        [Test]
        public void ReconcileWindow_FreshProjection_RebindsPlanetAndTargetByIdentity()
        {
            FleetWindowView view = OpenWindow(out UIWindow _);
            _controller.SelectTarget(view, _officer);
            GameFleet freshFleet = CreateFleet(
                _fleet.InstanceID,
                "Fresh Fleet",
                out Officer freshOfficer
            );
            Planet freshPlanetNode = new Planet
            {
                InstanceID = _planet.Planet.InstanceID,
                DisplayName = "Fresh Planet",
                Fleets = { freshFleet },
            };
            AttachFleetGraph(freshPlanetNode, freshFleet);
            GalaxyMapPlanet freshPlanet = new GalaxyMapPlanet(
                new GamePlanetSystem { InstanceID = "fresh-system" },
                freshPlanetNode,
                _playerFactionId
            );

            _controller.ReconcileWindow(view, freshPlanet);

            Assert.AreSame(freshPlanet, _controller.GetPlanet(view));
            Assert.AreEqual(0, _controller.GetSelectedFleetIndex(view));
            Assert.IsTrue(_controller.SelectTarget(view, freshOfficer));
        }

        [Test]
        public void TryCreateContextMenu_NoContextItem_ReturnsDisabledInformationCommands()
        {
            OpenWindow(out UIWindow window);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 177, 188, 4, 5, 6, 7),
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
            Assert.IsTrue(
                request.Commands.Cast<StrategyMenuCommand>().All(command => !command.Enabled)
            );
        }

        [Test]
        public void ContextMenu_BombardmentLeaf_ExecutesAndRoutesBattleResult()
        {
            _planet.Planet.OwnerInstanceID = _opposingFactionId;
            _fleet.CapitalShips[0].Bombardment = 1;
            _planet.Planet.Fleets.Remove(_fleet);
            _fleet.SetParent(null);
            _game.AttachNode(_fleet, _planet.Planet);
            _fleetCommandController = CreateFleetCommandController();
            _controller = CreateController();
            _controller.Initialize(
                _actions,
                _actions,
                _actions,
                (_, _, _) => { },
                _ => { },
                _ => { }
            );
            FleetWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window, true);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 177, 188, 4, 5, 6, 7),
                CreateFleetPointerEvent(view),
                10,
                20
            );
            _controller.TryCreateContextMenu(context, out ContextMenuRequest request, out _);
            StrategyMenuCommand parent = request
                .Commands.Cast<StrategyMenuCommand>()
                .Single(command => command.Action == StrategyMenuAction.PlanetaryBombardment);
            StrategyMenuCommand command = parent.SubmenuCommands.Single(item =>
                item.Action == StrategyMenuAction.GeneralBombardment
            );
            ContextMenuController contextMenuController = new ContextMenuController();
            contextMenuController.Open(request);

            bool selected = contextMenuController.TrySelectCommand(command);

            Assert.IsTrue(selected);
            Assert.IsInstanceOf<BombardmentResult>(_actions.LastBattleResult);
            Assert.AreEqual(
                BombardmentType.General,
                ((BombardmentResult)_actions.LastBattleResult).Type
            );
            Assert.AreEqual(1, _actions.RefreshCount);
        }

        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesPlanetAssociation()
        {
            FleetWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetPlanet(view));
        }

        private FleetWindowController CreateController()
        {
            return new FleetWindowController(
                _fleetCommandController,
                () => _uiContext,
                _targetingController,
                _windowLayer,
                _windowManager,
                (x, y) => new Vector2Int(x + 17, y + 19),
                () => _dirtyCount++
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
            game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            game.Factions.Add(new Faction { InstanceID = _opposingFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return game;
        }

        private GalaxyMapPlanet CreatePlanet(GameRoot game)
        {
            GamePlanetSystem system = new GamePlanetSystem
            {
                InstanceID = "system",
                DisplayName = "Core System",
            };
            game.AttachNode(system, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, system);
            return new GalaxyMapPlanet(system, planet, _playerFactionId);
        }

        private GameFleet CreateFleet(string instanceId, string displayName, out Officer officer)
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{instanceId}-ship",
                DisplayName = "Capital Ship",
                OwnerInstanceID = _playerFactionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
            };
            officer = new Officer
            {
                InstanceID = $"{instanceId}-officer",
                DisplayName = "Fleet Officer",
                OwnerInstanceID = _playerFactionId,
            };
            ship.Officers.Add(officer);
            return new GameFleet(_playerFactionId, displayName, new List<CapitalShip> { ship })
            {
                InstanceID = instanceId,
            };
        }

        private static void AttachFleetGraph(Planet planet, GameFleet fleet)
        {
            fleet.SetParent(planet);
            foreach (CapitalShip ship in fleet.CapitalShips)
            {
                ship.SetParent(fleet);
                foreach (ISceneNode child in ship.GetChildren())
                    child.SetParent(ship);
            }
        }

        private FleetWindowView OpenWindow(out UIWindow window)
        {
            window = _controller.Open(_planet, 20, 30, out bool _);
            _windowManager.TryGetWindowView(window, out FleetWindowView view);
            return view;
        }

        private static PointerEventData CreateFleetPointerEvent(FleetWindowView view)
        {
            FleetListRowView row = view.GetComponentsInChildren<FleetListRowView>(true)
                .Single(item => item.gameObject.activeInHierarchy);
            return new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
                pointerCurrentRaycast = new RaycastResult
                {
                    gameObject = row.transform.GetChild(0).gameObject,
                },
                pointerPressRaycast = new RaycastResult
                {
                    gameObject = row.transform.GetChild(0).gameObject,
                },
            };
        }

        private sealed class RecordingTargetingReceiver : ITargetingReceiver
        {
            public object Target { get; private set; }

            public void OnTargetSelected(TargetingRequest request, object target)
            {
                Target = target;
            }

            public void OnTargetingCancelled(TargetingRequest request) { }
        }

        private sealed class TestActions
            : IFleetWindowActions,
                IStrategyWindowCommandActions,
                IStrategyConfirmationActions
        {
            public bool CanRetire(IReadOnlyList<ISceneNode> items) => false;

            public void ExecuteTargetedCommand(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            ) { }

            public GameResult LastBattleResult { get; private set; }

            public int RefreshCount { get; private set; }

            public void OpenFleetBattleResult(GameResult result)
            {
                LastBattleResult = result;
            }

            public void OpenFleetEncyclopediaWindow(IReadOnlyList<ISceneNode> items) { }

            public void OpenFleetStatusWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

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

            public void RefreshFleetState()
            {
                RefreshCount++;
            }

            public void OpenMissionCreateWindow(
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            ) { }

            public bool TryExecuteMove(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                return false;
            }

            public void OpenMoveConfirmWindow(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            ) { }
        }
    }
}
