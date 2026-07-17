using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Defense
{
    [TestFixture]
    public class DefenseWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private DefenseWindowController _controller;
        private int _dirtyCount;
        private Officer _officer;
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
            GameRoot game = CreateGame();
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _planet = CreatePlanet(game);
            _officer = new Officer { InstanceID = "officer", OwnerInstanceID = _playerFactionId };
            _planet.Planet.Officers.Add(_officer);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _targetingController = new TargetingController();
            _controller = CreateController();
            TestActions actions = new TestActions();
            _controller.Initialize(actions, actions, (_, _, _) => { }, _ => { }, _ => { });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullUIContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DefenseWindowController(
                    null,
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
                _controller.Initialize(null, actions, (_, _, _) => { }, _ => { }, _ => { })
            );
        }

        [Test]
        public void BindWindow_BeforeInitialize_ThrowsInvalidOperationException()
        {
            DefenseWindowController controller = CreateController();
            DefenseWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.DefenseWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view));
        }

        [Test]
        public void TryInitializeWindow_NullPlanet_ReturnsFalse()
        {
            DefenseWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.DefenseWindowPrefab,
                _rootObject.transform
            );

            bool initialized = _controller.TryInitializeWindow(view, null);

            Assert.IsFalse(initialized);
            Assert.IsNull(_controller.GetPlanet(view));
        }

        [Test]
        public void Open_ValidPlanet_CreatesNamedWindowAtResolvedPosition()
        {
            UIWindow window = _controller.Open(_planet, 20, 30, out bool created);

            Assert.IsTrue(created);
            Assert.AreEqual(
                $"DefenseWindow-{_planet.Planet.GetDisplayName()}",
                window.Content.name
            );
            Assert.AreEqual(new Vector2Int(37, 49), new Vector2Int(window.X, window.Y));
            Assert.IsFalse(window.Modal);
            Assert.AreEqual(1, _dirtyCount);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out DefenseWindowView view));
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
        public void SelectFinderTab_ValidTab_ChangesActiveTab()
        {
            DefenseWindowView view = OpenWindow(out UIWindow _);

            _controller.SelectFinderTab(view, DefenseWindowTab.Regiments);

            Assert.AreEqual(DefenseWindowTab.Regiments, _controller.GetActiveTab(view));
        }

        [Test]
        public void SelectTarget_MatchingItem_SelectsItemAndStatusTarget()
        {
            DefenseWindowView view = OpenWindow(out UIWindow window);

            bool selected = _controller.SelectTarget(view, _officer);

            Assert.IsTrue(selected);
            CollectionAssert.AreEqual(new[] { 0 }, _controller.GetSelectedItems(view));
            StrategyStatusTarget target = _controller.GetStatusTarget(window);
            Assert.AreSame(_planet, target.Planet);
            Assert.AreSame(_officer, target.Item);
        }

        [Test]
        public void ReconcileWindow_FreshProjection_RebindsPlanetAndSelectionByIdentity()
        {
            DefenseWindowView view = OpenWindow(out UIWindow _);
            _controller.SelectTarget(view, _officer);
            Officer freshOfficer = new Officer
            {
                InstanceID = _officer.InstanceID,
                OwnerInstanceID = _playerFactionId,
            };
            GalaxyMapPlanet freshPlanet = new GalaxyMapPlanet(
                new GamePlanetSystem { InstanceID = "fresh-system" },
                new Planet
                {
                    InstanceID = _planet.Planet.InstanceID,
                    DisplayName = "Fresh Planet",
                    Officers = { freshOfficer },
                },
                _playerFactionId
            );

            _controller.ReconcileWindow(view, freshPlanet);

            Assert.AreSame(freshPlanet, _controller.GetPlanet(view));
            CollectionAssert.AreEqual(new[] { 0 }, _controller.GetSelectedItems(view));
        }

        [Test]
        public void ClearSelection_SelectedItem_RemovesSelectionAndStatusTarget()
        {
            DefenseWindowView view = OpenWindow(out UIWindow window);
            _controller.SelectTarget(view, _officer);

            _controller.ClearSelection(window);

            Assert.IsEmpty(_controller.GetSelectedItems(view));
            Assert.IsNull(_controller.GetStatusTarget(window));
        }

        [Test]
        public void TryCreateContextMenu_NoContextItem_ReturnsDisabledInformationCommands()
        {
            OpenWindow(out UIWindow window);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 4, 177, 6, 7),
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
        public void ViewDestroyed_InitializedSession_ReleasesPlanetAssociation()
        {
            DefenseWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetPlanet(view));
        }

        private DefenseWindowController CreateController()
        {
            return new DefenseWindowController(
                () => _uiContext,
                _targetingController,
                _windowLayer,
                _windowManager,
                (x, y) => new Vector2Int(x + 17, y + 19),
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
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, system);
            return new GalaxyMapPlanet(system, planet, _playerFactionId);
        }

        private DefenseWindowView OpenWindow(out UIWindow window)
        {
            window = _controller.Open(_planet, 20, 30, out bool _);
            _windowManager.TryGetWindowView(window, out DefenseWindowView view);
            return view;
        }

        private sealed class TestActions : IDefenseWindowActions, IStrategyWindowCommandActions
        {
            public void OpenDefenseStatusWindow(StrategyStatusTarget target) { }

            public void OpenDefenseInfoWindow(StrategyStatusTarget target) { }

            public void OpenDefenseScrapConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            public void OpenDefenseStopConstructionConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

            public void OpenDefenseRetireConfirmWindow(
                UIWindow sourceWindow,
                IReadOnlyList<ISceneNode> items
            ) { }

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
