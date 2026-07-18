using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using UnityEngine;
using UnityEngine.UI;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionCreateWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _opponentFactionId = "FNIMP1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private UIWindow _closedWindow;
        private MissionCreateWindowController _controller;
        private int _dirtyCount;
        private GameRoot _game;
        private GameManager _gameManager;
        private GameObject _rootObject;
        private SpecialForces _specialForces;
        private StrategyMissionTarget _target;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _closedWindow = null;
            _dirtyCount = 0;
            _game = CreateGame(out Planet origin, out GalaxyMapPlanet targetPlanet);
            _gameManager = new GameManager(_game);
            _uiContext = new UIContext(
                _game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _specialForces = new SpecialForces
            {
                InstanceID = "recon-team",
                DisplayName = "Recon Team",
                OwnerInstanceID = _playerFactionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Reconnaissance },
            };
            _game.AttachNode(_specialForces, origin);
            _target = new StrategyMissionTarget(targetPlanet, null);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _controller = CreateController();
            _actions = new TestActions();
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
                new MissionCreateWindowController(
                    null,
                    () => _gameManager.MissionSystem,
                    () => _uiContext,
                    _ => { },
                    _windowLayer,
                    _windowManager,
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
            MissionCreateWindowController controller = CreateController();
            MissionCreateWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.MissionCreateWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view));
        }

        [Test]
        public void Open_InvalidParticipantSelection_DestroysRejectedWindow()
        {
            _controller.Open(_target, Array.Empty<ISceneNode>());

            Assert.IsEmpty(_windowManager.Windows);
            Assert.AreEqual(0, _dirtyCount);
        }

        [Test]
        public void Open_ValidSelection_CreatesNamedModalWindowAtAuthoredPosition()
        {
            MissionCreateWindowView view = OpenWindow(out UIWindow window);

            Assert.AreEqual(
                $"MissionCreateWindow-{_target.Planet.Planet.GetDisplayName()}",
                view.name
            );
            Assert.AreEqual(new Vector2Int(141, 73), new Vector2Int(window.X, window.Y));
            Assert.IsTrue(window.Modal);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void Open_ExistingWindow_ClosesPreviousAndCreatesReplacement()
        {
            OpenWindow(out UIWindow firstWindow);

            MissionCreateWindowView secondView = OpenWindow(out UIWindow secondWindow);

            Assert.AreSame(firstWindow, _closedWindow);
            Assert.AreNotSame(firstWindow, secondWindow);
            Assert.AreSame(secondView, secondWindow.Content);
            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreEqual(2, _dirtyCount);
        }

        [Test]
        public void InfoButton_InitializedSession_RoutesSemanticAction()
        {
            MissionCreateWindowView view = OpenWindow(out UIWindow window);

            FindButton(view, "InfoButtonImage").onClick.Invoke();

            Assert.AreEqual(1, _actions.InfoCount);
            Assert.AreSame(window, _windowManager.ActiveWindow);
        }

        [Test]
        public void DropdownButton_InitializedSession_ChangesLocalStateAndInvalidates()
        {
            MissionCreateWindowView view = OpenWindow(out UIWindow window);

            FindButton(view, "DropdownButtonImage").onClick.Invoke();

            Assert.AreEqual(2, _dirtyCount);
            Assert.AreSame(window, _windowManager.ActiveWindow);
        }

        [Test]
        public void CancelButton_InitializedSession_ClosesOwningWindow()
        {
            MissionCreateWindowView view = OpenWindow(out UIWindow window);

            FindButton(view, "CancelButtonImage").onClick.Invoke();

            Assert.AreSame(window, _closedWindow);
            Assert.IsEmpty(_windowManager.Windows);
        }

        [Test]
        public void ViewDestroyed_InitializedSession_RejectsFurtherRendering()
        {
            MissionCreateWindowView view = OpenWindow(out UIWindow window);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.Throws<InvalidOperationException>(() => _controller.RenderWindow(view, window));
        }

        private MissionCreateWindowController CreateController()
        {
            return new MissionCreateWindowController(
                () => _game,
                () => _gameManager.MissionSystem,
                () => _uiContext,
                _ => { },
                _windowLayer,
                _windowManager,
                () => new Vector2Int(141, 73),
                CloseWindow,
                () => _dirtyCount++
            );
        }

        private GameRoot CreateGame(out Planet origin, out GalaxyMapPlanet targetPlanet)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            game.Factions.Add(new Faction { InstanceID = _opponentFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            GamePlanetSystem system = new GamePlanetSystem
            {
                InstanceID = "system",
                DisplayName = "Core System",
            };
            game.AttachNode(system, game.GetGalaxyMap());
            origin = new Planet
            {
                InstanceID = "origin",
                DisplayName = "Origin",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            Planet target = new Planet
            {
                InstanceID = "target",
                DisplayName = "Target",
                OwnerInstanceID = _opponentFactionId,
                IsColonized = true,
            };
            game.AttachNode(origin, system);
            game.AttachNode(target, system);
            targetPlanet = new GalaxyMapPlanet(system, target, _playerFactionId);
            return game;
        }

        private void CloseWindow(UIWindow window)
        {
            _closedWindow = window;
            _windowManager.DestroyWindow(window);
        }

        private MissionCreateWindowView OpenWindow(out UIWindow window)
        {
            _controller.Open(_target, new ISceneNode[] { _specialForces });
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out MissionCreateWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            return view;
        }

        private static Button FindButton(MissionCreateWindowView view, string objectName)
        {
            return view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == objectName);
        }

        private sealed class TestActions : IMissionCreateWindowActions
        {
            public int InfoCount { get; private set; }

            public void RefreshAfterMissionCreation() { }

            public void OpenMissionCreateInfo()
            {
                InfoCount++;
            }
        }
    }
}
