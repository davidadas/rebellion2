using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Systems;
using TMPro;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

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
        private GameManager _gameManager;
        private GalaxyMapPlanet _planet;
        private GameObject _rootObject;
        private UIWindow _sourceWindow;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            GameRoot game = CreateGame();
            _gameManager = new GameManager(game);
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _planet = CreatePlanet(game);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _sourceWindow = CreateSourceWindow();
            _actions = new TestActions();
            _controller = CreateController();
            _controller.Initialize(_actions);
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
                new ConstructionWindowController(
                    null,
                    () => _gameManager.ManufacturingSystem,
                    () => _gameManager.MovementSystem,
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
        public void OpenFromAdvisor_ExistingPlanet_ReusesWindowWithoutAdditionalInvalidation()
        {
            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Shipyards);
            UIWindow firstWindow = _windowManager.Windows.Single();

            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Training);

            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreSame(firstWindow, _windowManager.Windows.Single());
            Assert.AreEqual(1, _dirtyCount);
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
                new GamePlanetSystem { InstanceID = "fresh-system" },
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

        private ConstructionWindowController CreateController()
        {
            return new ConstructionWindowController(
                () => _gameManager.GetGame(),
                () => _gameManager.ManufacturingSystem,
                () => _gameManager.MovementSystem,
                () => _uiContext,
                _windowLayer,
                _windowManager,
                (x, y) => new Vector2Int(x + 17, y + 19),
                () => new Vector2Int(141, 73),
                _ => { },
                () => _dirtyCount++
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
                InstanceID = "producer",
                DisplayName = "Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, system);
            return new GalaxyMapPlanet(system, planet, _playerFactionId);
        }

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

        private ConstructionWindowView OpenAdvisorWindow(out UIWindow window)
        {
            _controller.OpenFromAdvisor(_planet, _planet, FacilityWindowTab.Shipyards);
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConstructionWindowView view);
            return view;
        }

        private sealed class TestActions : IConstructionWindowActions
        {
            public void OpenConstructionInfo(Rebellion.SceneGraph.ISceneNode item) { }

            public void OpenConstructionStatus(StrategyStatusTarget target) { }

            public void RefreshAfterConstruction() { }
        }
    }
}
