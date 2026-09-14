using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

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

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            _game = CreateGame();
            _gameManager = TestContent.CreateGameManager(_game);
            _uiContext = TestContent.CreateUIContext(
                _game,
                TestContent.CreateThemeLibrary(),
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
            _planet.Planet.AddTestChild(_building);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _targetingController = new TargetingController();
            _constructionController = CreateConstructionController();
            _constructionController.Initialize(new ConstructionActions());
            _controller = CreateController();
            FacilityActions actions = new FacilityActions();
            _controller.Initialize(actions, actions);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        /// <summary>
        /// Verifies constructor null game provider throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullGameProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FacilityWindowController(
                    null,
                    () => _gameManager.ManufacturingSystem,
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

        /// <summary>
        /// Verifies constructor null manufacturing system provider throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullManufacturingSystemProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FacilityWindowController(
                    () => _game,
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

        /// <summary>
        /// Verifies initialize null actions throws argument null exception.
        /// </summary>
        [Test]
        public void Initialize_NullActions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _controller.Initialize(null, new FacilityActions())
            );
        }

        /// <summary>
        /// Verifies bind window before initialize throws invalid operation exception.
        /// </summary>
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

        /// <summary>
        /// Verifies try initialize window null planet returns false.
        /// </summary>
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

        /// <summary>
        /// Verifies open valid planet creates named window at resolved position.
        /// </summary>
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

        /// <summary>
        /// Verifies open existing planet reuses window without additional invalidation.
        /// </summary>
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

        /// <summary>
        /// Verifies select target matching building selects inventory status and scrap target.
        /// </summary>
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

        /// <summary>
        /// Verifies try get construction destination i ds manufacturing tab returns planet fallback.
        /// </summary>
        [Test]
        public void TryGetConstructionDestinationIDs_ManufacturingTab_ReturnsPlanetFallback()
        {
            FacilityWindowView view = OpenWindow(out UIWindow _);

            bool found = _controller.TryGetConstructionDestinationIDs(
                view,
                FacilityWindowTab.Shipyards,
                out string destinationPlanetId,
                out string destinationItemId
            );

            Assert.IsTrue(found);
            Assert.AreEqual(_planet.Planet.InstanceID, destinationPlanetId);
            Assert.IsNull(destinationItemId);
        }

        /// <summary>
        /// Verifies reconcile window fresh projection rebinds planet and selection by identity.
        /// </summary>
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
            Planet freshPlanetNode = new Planet
            {
                InstanceID = _planet.Planet.InstanceID,
                DisplayName = "Fresh Planet",
            };
            freshPlanetNode.AddTestChild(freshBuilding);
            GalaxyMapPlanet freshPlanet = new GalaxyMapPlanet(
                new GalaxyPlanetSector { InstanceID = "fresh-sector" },
                freshPlanetNode,
                _playerFactionId
            );

            _controller.ReconcileWindow(view, freshPlanet);

            Assert.AreSame(freshPlanet, _controller.GetPlanet(view));
            CollectionAssert.AreEqual(
                new ISceneNode[] { freshBuilding },
                _controller.GetScrapItems(view)
            );
        }

        /// <summary>
        /// Verifies clear selection selected building removes status and scrap targets.
        /// </summary>
        [Test]
        public void ClearSelection_SelectedBuilding_RemovesStatusAndScrapTargets()
        {
            FacilityWindowView view = OpenWindow(out UIWindow _);
            _controller.SelectTarget(view, _building);

            _controller.ClearSelection(view);

            Assert.IsNull(_controller.GetStatusTarget(view));
            Assert.IsEmpty(_controller.GetScrapItems(view));
        }

        /// <summary>
        /// Verifies on context menu command selected manufacturing reservation toggles selected lane.
        /// </summary>
        [Test]
        public void OnContextMenuCommandSelected_ManufacturingReservation_TogglesSelectedLane()
        {
            FacilityWindowView view = OpenWindow(out UIWindow window);
            ManufacturingLaneCardView card =
                view.GetComponentsInChildren<ManufacturingLaneCardView>(true)
                    .Single(candidate => candidate.name == "ConstructionManufacturingLaneCard");
            UIComponentTestHelper.InvokeLifecycle(card, "Awake");
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            PointerEventData pointer = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };
            card.GetComponent<UIPointerGestureRelay>().OnPointerDown(pointer);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 4, 5, 6, 7),
                pointer,
                10,
                20
            );
            StrategyMenuCommand reserve = FacilityWindowContextMenuBuilder
                .Build(
                    _planet.Planet,
                    FacilityWindowTab.Manufacturing,
                    FacilityWindowTab.Construction,
                    null,
                    _playerFactionId
                )
                .Single(command => command.Action == StrategyMenuAction.Reserve);
            ContextMenuRequest request = new ContextMenuRequest(
                context,
                new IContextMenuCommand[] { reserve },
                _controller
            );

            _controller.OnContextMenuCommandSelected(request, reserve);

            Assert.IsTrue(_planet.Planet.IsManufacturingReserved(ManufacturingType.Building));
            Assert.IsFalse(_planet.Planet.IsManufacturingReserved(ManufacturingType.Ship));
            Assert.IsFalse(_planet.Planet.IsManufacturingReserved(ManufacturingType.Troop));
        }

        /// <summary>
        /// Verifies view destroyed initialized session releases planet association.
        /// </summary>
        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesPlanetAssociation()
        {
            FacilityWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetPlanet(view));
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
        private FacilityWindowController CreateController()
        {
            return new FacilityWindowController(
                () => _game,
                () => _gameManager.ManufacturingSystem,
                _constructionController,
                () => _uiContext,
                _targetingController,
                _windowLayer,
                _windowManager,
                (x, y) => new Vector2Int(x + 17, y + 19),
                () => _dirtyCount++
            );
        }

        /// <summary>
        /// Creates construction controller.
        /// </summary>
        /// <returns>The created construction controller.</returns>
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

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        private GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return game;
        }

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <returns>The created planet.</returns>
        private GalaxyMapPlanet CreatePlanet(GameRoot game)
        {
            GalaxyPlanetSector sector = new GalaxyPlanetSector
            {
                InstanceID = "sector",
                DisplayName = "Core Sector",
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, sector);
            return new GalaxyMapPlanet(sector, planet, _playerFactionId);
        }

        /// <summary>
        /// Opens window.
        /// </summary>
        /// <param name="window">Receives the window.</param>
        /// <returns>The result of open window.</returns>
        private FacilityWindowView OpenWindow(out UIWindow window)
        {
            window = _controller.Open(_planet, 20, 30, out bool _);
            _windowManager.TryGetWindowView(window, out FacilityWindowView view);
            return view;
        }

        private sealed class ConstructionActions : IConstructionWindowActions
        {
            /// <summary>
            /// Opens construction info.
            /// </summary>
            /// <param name="item">The item.</param>
            public void OpenConstructionInfo(ISceneNode item) { }

            /// <summary>
            /// Opens construction status.
            /// </summary>
            /// <param name="target">The target.</param>
            public void OpenConstructionStatus(StrategyStatusTarget target) { }

            /// <summary>
            /// Refreshes after construction.
            /// </summary>
            public void RefreshAfterConstruction() { }
        }

        private sealed class FacilityActions : IFacilityWindowActions, IStrategyConfirmationActions
        {
            /// <summary>
            /// Checks whether the retire condition is met.
            /// </summary>
            /// <param name="items">The items.</param>
            /// <returns>True when the retire condition is met; otherwise false.</returns>
            public bool CanRetire(IReadOnlyList<ISceneNode> items) => false;

            /// <summary>
            /// Opens facility status.
            /// </summary>
            /// <param name="target">The target.</param>
            public void OpenFacilityStatus(StrategyStatusTarget target) { }

            /// <summary>
            /// Opens facility info.
            /// </summary>
            /// <param name="target">The target.</param>
            public void OpenFacilityInfo(StrategyStatusTarget target) { }

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
            /// Refreshes facility state.
            /// </summary>
            public void RefreshFacilityState() { }
        }
    }
}
