using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Facility
{
    [TestFixture]
    public class FacilityWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private Building _building;
        private ConstructionWindowController _constructionController;
        private FacilityWindowController _controller;
        private int _dirtyCount;
        private GameRoot _game;
        private GameManager _gameManager;
        private GalaxyMapPlanet _planet;
        private GameObject _rootObject;
        private TargetingController _targetingController;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            _game = CreateGame();
            _gameManager = new GameManager(_game);
            _uiContext = new UIContext(
                _game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _planet = CreatePlanet(_game);
            _building = new Building
            {
                InstanceID = "shipyard",
                DisplayName = "Corellian Shipyard",
                OwnerInstanceID = _playerFactionId,
                BuildingType = BuildingType.Shipyard,
            };
            _planet.Planet.Buildings.Add(_building);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _targetingController = new TargetingController();
            _constructionController = CreateConstructionController();
            _constructionController.Initialize(new ConstructionActions());
            _controller = CreateController();
            _controller.Initialize(new FacilityActions());
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullGameProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FacilityWindowController(
                    null,
                    _constructionController,
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
        public void Initialize_NullActions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(null));
        }

        [Test]
        public void BindWindow_BeforeInitialize_ThrowsInvalidOperationException()
        {
            FacilityWindowController controller = CreateController();
            FacilityWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.FacilityWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view));
        }

        [Test]
        public void TryInitializeWindow_NullPlanet_ReturnsFalse()
        {
            FacilityWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.FacilityWindowPrefab,
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
            Assert.AreEqual(
                $"FacilityWindow-{_planet.Planet.GetDisplayName()}",
                window.Content.name
            );
            Assert.AreEqual(new Vector2Int(37, 49), new Vector2Int(window.X, window.Y));
            Assert.IsFalse(window.Modal);
            Assert.AreEqual(1, _dirtyCount);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out FacilityWindowView view));
            Assert.AreSame(_planet, _controller.GetPlanet(view));
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
        public void SelectTarget_MatchingBuilding_SelectsInventoryStatusAndScrapTarget()
        {
            FacilityWindowView view = OpenWindow(out UIWindow _);

            bool selected = _controller.SelectTarget(view, _building);

            Assert.IsTrue(selected);
            StrategyStatusTarget target = _controller.GetStatusTarget(view);
            Assert.AreSame(_planet, target.Planet);
            Assert.AreSame(_building, target.Item);
            CollectionAssert.AreEqual(
                new ISceneNode[] { _building },
                _controller.GetScrapItems(view)
            );
        }

        [Test]
        public void TryGetConstructionDestinationIds_ManufacturingTab_ReturnsPlanetFallback()
        {
            FacilityWindowView view = OpenWindow(out UIWindow _);

            bool found = _controller.TryGetConstructionDestinationIds(
                view,
                FacilityWindowTab.Shipyards,
                out string destinationPlanetId,
                out string destinationItemId
            );

            Assert.IsTrue(found);
            Assert.AreEqual(_planet.Planet.InstanceID, destinationPlanetId);
            Assert.IsNull(destinationItemId);
        }

        [Test]
        public void ReconcileWindow_FreshProjection_RebindsPlanetAndSelectionByIdentity()
        {
            FacilityWindowView view = OpenWindow(out UIWindow _);
            _controller.SelectTarget(view, _building);
            Building freshBuilding = new Building
            {
                InstanceID = _building.InstanceID,
                DisplayName = "Fresh Shipyard",
                OwnerInstanceID = _playerFactionId,
                BuildingType = BuildingType.Shipyard,
            };
            GalaxyMapPlanet freshPlanet = new GalaxyMapPlanet(
                new GamePlanetSystem { InstanceID = "fresh-system" },
                new Planet
                {
                    InstanceID = _planet.Planet.InstanceID,
                    DisplayName = "Fresh Planet",
                    Buildings = { freshBuilding },
                },
                _playerFactionId
            );

            _controller.ReconcileWindow(view, freshPlanet);

            Assert.AreSame(freshPlanet, _controller.GetPlanet(view));
            CollectionAssert.AreEqual(
                new ISceneNode[] { freshBuilding },
                _controller.GetScrapItems(view)
            );
        }

        [Test]
        public void ClearSelection_SelectedBuilding_RemovesStatusAndScrapTargets()
        {
            FacilityWindowView view = OpenWindow(out UIWindow _);
            _controller.SelectTarget(view, _building);

            _controller.ClearSelection(view);

            Assert.IsNull(_controller.GetStatusTarget(view));
            Assert.IsEmpty(_controller.GetScrapItems(view));
        }

        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesPlanetAssociation()
        {
            FacilityWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetPlanet(view));
        }

        private FacilityWindowController CreateController()
        {
            return new FacilityWindowController(
                () => _game,
                _constructionController,
                () => _uiContext,
                _targetingController,
                _windowLayer,
                _windowManager,
                (x, y) => new Vector2Int(x + 17, y + 19),
                () => _dirtyCount++
            );
        }

        private ConstructionWindowController CreateConstructionController()
        {
            return new ConstructionWindowController(
                () => _game,
                () => _gameManager.ManufacturingSystem,
                () => _gameManager.MovementSystem,
                () => _uiContext,
                _windowLayer,
                _windowManager,
                (_, _) => Vector2Int.zero,
                () => Vector2Int.zero,
                _ => { },
                () => { }
            );
        }

        private GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _playerFactionId });
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

        private FacilityWindowView OpenWindow(out UIWindow window)
        {
            window = _controller.Open(_planet, 20, 30, out bool _);
            _windowManager.TryGetWindowView(window, out FacilityWindowView view);
            return view;
        }

        private sealed class ConstructionActions : IConstructionWindowActions
        {
            public void OpenConstructionInfo(ISceneNode item) { }

            public void OpenConstructionStatus(StrategyStatusTarget target) { }

            public void RefreshAfterConstruction() { }
        }

        private sealed class FacilityActions : IFacilityWindowActions
        {
            public void OpenFacilityStatus(StrategyStatusTarget target) { }

            public void OpenFacilityInfo(StrategyStatusTarget target) { }

            public void OpenFacilityScrapConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            public void OpenFacilityStopConstructionConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            public void RefreshFacilityState() { }
        }
    }
}
