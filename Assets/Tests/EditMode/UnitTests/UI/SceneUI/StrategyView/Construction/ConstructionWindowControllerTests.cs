using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using TMPro;
using UnityEngine;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private ConstructionWindowController _controller;
        private int _dirtyCount;
        private GameSession _gameSession;
        private GalaxyMapPlanet _planet;
        private GameObject _rootObject;
        private UIWindow _sourceWindow;
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
            GameRoot game = CreateGame();
            _gameSession = TestContent.CreateGameSession(game);
            _uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _planet = CreatePlanet(game);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _windowManager.WindowCloseRequested += _windowManager.DestroyWindow;
            _sourceWindow = CreateSourceWindow();
            _actions = new TestActions();
            _controller = CreateController();
            _controller.Initialize(_actions);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullGameProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConstructionWindowController(
                    null,
                    _gameSession,
                    () => _uiContext,
                    _windowLayer,
                    _windowManager,
                    (_, _) => Vector2Int.zero,
                    () => Vector2Int.zero,
                    _ => { },
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
            ConstructionWindowController controller = CreateController();
            ConstructionWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.ConstructionWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view));
        }

        [Test]
        public void OpenFromFacility_NullSourceWindow_DoesNotCreateWindow()
        {
            _controller.OpenFromFacility(
                _planet,
                null,
                FacilityWindowTab.Shipyards,
                _planet.Planet.InstanceID,
                null
            );

            Assert.IsEmpty(_windowManager.Windows);
            Assert.AreEqual(0, _dirtyCount);
        }

        [Test]
        public void OpenFromAdvisor_ValidPlanets_CreatesNamedSessionAtUtilityPosition()
        {
            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Shipyards);

            UIWindow window = _windowManager.Windows.Single();
            Assert.AreEqual(
                $"ConstructionWindow-{_planet.Planet.GetDisplayName()}",
                window.Content.name
            );
            Assert.AreEqual(new Vector2Int(141, 73), new Vector2Int(window.X, window.Y));
            Assert.IsTrue(window.Modal);
            Assert.AreEqual(1, _dirtyCount);
            _windowManager.TryGetWindowView(window, out ConstructionWindowView view);
            Assert.AreSame(_planet, _controller.GetPlanet(view));
            Assert.IsNotNull(_controller.GetStatusTarget(view));
        }

        [Test]
        public void OpenFromAdvisor_DifferentTab_ReplacesExistingWindow()
        {
            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Shipyards);
            UIWindow firstWindow = _windowManager.Windows.Single();

            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Training);

            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreNotSame(firstWindow, _windowManager.Windows.Single());
            Assert.AreEqual(2, _dirtyCount);
        }

        [Test]
        public void OpenFromAdvisor_SameRequest_TogglesExistingWindowClosed()
        {
            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Shipyards);

            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Shipyards);

            Assert.IsEmpty(_windowManager.Windows);
        }

        [Test]
        public void TryInitializeWindow_NonManufacturingTab_ReturnsFalse()
        {
            ConstructionWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.ConstructionWindowPrefab,
                _rootObject.transform
            );
            UIWindow window = view.GetComponent<UIWindow>();

            bool initialized = _controller.TryInitializeWindow(
                view,
                window,
                _planet,
                _sourceWindow,
                FacilityWindowTab.Manufacturing,
                _planet.Planet.InstanceID,
                null
            );

            Assert.IsFalse(initialized);
            Assert.IsNull(_controller.GetPlanet(view));
        }

        [Test]
        public void ReconcileWindow_InitializedSession_RebindsFreshPlanetProjection()
        {
            ConstructionWindowView view = OpenAdvisorWindow(out UIWindow _);
            GalaxyMapPlanet freshPlanet = new GalaxyMapPlanet(
                new GalaxyPlanetSector { InstanceID = "fresh-sector" },
                new Planet
                {
                    InstanceID = _planet.Planet.InstanceID,
                    DisplayName = "Fresh Producer",
                },
                _playerFactionId
            );

            _controller.ReconcileWindow(view, freshPlanet);

            Assert.AreSame(freshPlanet, _controller.GetPlanet(view));
        }

        [Test]
        public void TryCreateContextMenu_DefaultBuildSelection_ReturnsEnabledInformationCommands()
        {
            ConstructionWindowView view = OpenAdvisorWindow(out UIWindow window);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 4, 5, 6, 177),
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
                request.Commands.Cast<StrategyMenuCommand>().All(command => command.Enabled)
            );
            Assert.AreSame(view, window.Content);
        }

        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesPlanetAssociation()
        {
            ConstructionWindowView view = OpenAdvisorWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetPlanet(view));
        }

        [Test]
        public void BuildCountSubmitted_ValidInteger_UpdatesRenderedQuantity()
        {
            ConstructionWindowView view = OpenAdvisorWindow(out UIWindow window);
            TMP_InputField input = view.GetComponentInChildren<TMP_InputField>(true);

            view.RequestBuildCount("12");
            _controller.RenderWindow(view, window, true);

            Assert.AreEqual("12", input.text);
            Assert.AreEqual(2, _dirtyCount);
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
        private ConstructionWindowController CreateController()
        {
            return new ConstructionWindowController(
                () => _gameSession.GetGame(),
                _gameSession,
                () => _uiContext,
                _windowLayer,
                _windowManager,
                (x, y) => new Vector2Int(x + 17, y + 19),
                () => new Vector2Int(141, 73),
                _ => { },
                () => _dirtyCount++
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
            game.SetFactionController(_playerFactionId, "PLAYER1", PlayerControllerType.Human);
            return game;
        }

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <returns>The created planet.</returns>
        private GalaxyMapPlanet CreatePlanet(GameRoot game)
        {
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector
            {
                InstanceID = "sector",
                DisplayName = "Core Sector",
            };
            game.AttachNode(planetSector, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "producer",
                DisplayName = "Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, planetSector);
            return new GalaxyMapPlanet(planetSector, planet, _playerFactionId);
        }

        /// <summary>
        /// Creates source window.
        /// </summary>
        /// <returns>The created source window.</returns>
        private UIWindow CreateSourceWindow()
        {
            GameObject sourceObject = new GameObject(
                "FacilityWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            sourceObject.transform.SetParent(_rootObject.transform, false);
            UIWindow window = sourceObject.GetComponent<UIWindow>();
            window.Configure(500, 20, 30, 100, 80, false, true, false);
            return window;
        }

        /// <summary>
        /// Opens advisor window.
        /// </summary>
        /// <param name="window">Receives the window.</param>
        /// <returns>The result of open advisor window.</returns>
        private ConstructionWindowView OpenAdvisorWindow(out UIWindow window)
        {
            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Shipyards);
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConstructionWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            return view;
        }

        private sealed class TestActions : IConstructionWindowActions
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
    }
}
