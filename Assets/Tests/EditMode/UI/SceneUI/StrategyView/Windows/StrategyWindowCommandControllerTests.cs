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

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Windows
{
    [TestFixture]
    public class StrategyWindowCommandControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _opponentFactionId = "FNIMP1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private UIWindow _clearedWindow;
        private StrategyWindowCommandController _controller;
        private int _dirtyCount;
        private GalaxyMapPlanet _destination;
        private GameRoot _game;
        private GameManager _gameManager;
        private GalaxyMapPlanet _missionTarget;
        private MissionCreateWindowController _missionCreateController;
        private Officer _officer;
        private int _rebuildCount;
        private GameObject _rootObject;
        private SpecialForces _specialForces;
        private UIWindow _sourceWindow;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _clearedWindow = null;
            _dirtyCount = 0;
            _rebuildCount = 0;
            _game = CreateGame(
                out Planet origin,
                out GalaxyMapPlanet destination,
                out GalaxyMapPlanet missionTarget
            );
            _destination = destination;
            _missionTarget = missionTarget;
            _officer = new Officer { InstanceID = "officer", OwnerInstanceID = _playerFactionId };
            _specialForces = new SpecialForces
            {
                InstanceID = "recon-team",
                OwnerInstanceID = _playerFactionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Reconnaissance },
            };
            _game.AttachNode(_officer, origin);
            _game.AttachNode(_specialForces, origin);
            _gameManager = new GameManager(_game);
            _uiContext = new UIContext(
                _game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _sourceWindow = CreateSourceWindow();
            _missionCreateController = new MissionCreateWindowController(
                () => _game,
                () => _gameManager.MissionSystem,
                () => _uiContext,
                _ => { },
                _windowLayer,
                _windowManager,
                () => new Vector2Int(100, 80),
                window => _windowManager.DestroyWindow(window),
                () => { }
            );
            _missionCreateController.Initialize(new MissionCreateActions());
            ConfirmDialogWindowController confirmController = new ConfirmDialogWindowController(
                () => _uiContext,
                _ => { },
                _windowLayer,
                _windowManager,
                () => new Vector2Int(120, 90),
                window => _windowManager.DestroyWindow(window),
                () => { }
            );
            _controller = new StrategyWindowCommandController(
                _missionCreateController,
                confirmController,
                _gameManager,
                _ => { },
                window => _clearedWindow = window,
                () => _rebuildCount++,
                () => _dirtyCount++
            );
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullMissionCreateController_ThrowsArgumentNullException()
        {
            ConfirmDialogWindowController confirmController = new ConfirmDialogWindowController(
                () => _uiContext,
                _ => { },
                _windowLayer,
                _windowManager,
                () => Vector2Int.zero,
                _ => { },
                () => { }
            );

            Assert.Throws<ArgumentNullException>(() =>
                new StrategyWindowCommandController(
                    null,
                    confirmController,
                    _gameManager,
                    _ => { },
                    _ => { },
                    () => { },
                    () => { }
                )
            );
        }

        [Test]
        public void TryExecuteMove_InvalidSelection_ReturnsFalseWithoutCallbacks()
        {
            bool moved = _controller.TryExecuteMove(
                _sourceWindow,
                new StrategyMissionTarget(_destination, null),
                Array.Empty<ISceneNode>()
            );

            Assert.IsFalse(moved);
            Assert.IsNull(_clearedWindow);
            Assert.AreEqual(0, _rebuildCount);
            Assert.AreEqual(0, _dirtyCount);
        }

        [Test]
        public void TryExecuteMove_ValidSelection_ClearsSourceAndInvalidatesSnapshot()
        {
            bool moved = _controller.TryExecuteMove(
                _sourceWindow,
                new StrategyMissionTarget(_destination, null),
                new ISceneNode[] { _officer }
            );

            Assert.IsTrue(moved);
            Assert.AreSame(_sourceWindow, _clearedWindow);
            Assert.AreEqual(1, _rebuildCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void ExecuteTargetedCommand_MoveConfirm_OpensConfirmationWindow()
        {
            _controller.ExecuteTargetedCommand(
                new StrategyWindowTargetingSource(
                    _sourceWindow,
                    StrategyMenuAction.MoveConfirm,
                    0,
                    0,
                    new ISceneNode[] { _officer }
                ),
                new StrategyMissionTarget(_destination, null)
            );

            UIWindow window = _windowManager.Windows.Single();
            Assert.IsTrue(window.Modal);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out ConfirmDialogWindowView _));
        }

        [Test]
        public void OpenMoveConfirmWindow_ConfirmedMove_MovesUnitAndRefreshesSource()
        {
            _controller.OpenMoveConfirmWindow(
                _sourceWindow,
                new StrategyMissionTarget(_destination, null),
                new ISceneNode[] { _officer }
            );

            ConfirmOpenDialog();

            Assert.AreEqual("target", _officer.GetParentOfType<Planet>().InstanceID);
            Assert.IsNotNull(_officer.Movement);
            Assert.AreSame(_sourceWindow, _clearedWindow);
            Assert.AreEqual(1, _rebuildCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void ExecuteTargetedCommand_CreateMission_OpensMissionCreateWindow()
        {
            _controller.ExecuteTargetedCommand(
                new StrategyWindowTargetingSource(
                    _sourceWindow,
                    StrategyMenuAction.CreateMission,
                    0,
                    0,
                    new ISceneNode[] { _specialForces }
                ),
                new StrategyMissionTarget(_missionTarget, null)
            );

            UIWindow window = _windowManager.Windows.Single();
            Assert.IsTrue(window.Modal);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out MissionCreateWindowView _));
        }

        [Test]
        public void OpenScrapConfirmWindow_ValidSelection_OpensConfirmationWindow()
        {
            _controller.OpenScrapConfirmWindow(_sourceWindow, new ISceneNode[] { _specialForces });

            UIWindow window = _windowManager.Windows.Single();
            Assert.IsTrue(window.Modal);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out ConfirmDialogWindowView _));
        }

        [Test]
        public void OpenStopConstructionConfirmWindow_BuildingItem_OpensConfirmationWindow()
        {
            _specialForces.ManufacturingStatus = ManufacturingStatus.Building;

            _controller.OpenStopConstructionConfirmWindow(
                _sourceWindow,
                new ISceneNode[] { _specialForces }
            );

            UIWindow window = _windowManager.Windows.Single();
            Assert.IsTrue(window.Modal);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out ConfirmDialogWindowView _));
        }

        [Test]
        public void OpenRetireConfirmWindow_EligibleOfficer_OpensConfirmationWindow()
        {
            _controller.OpenRetireConfirmWindow(_sourceWindow, new ISceneNode[] { _officer });

            UIWindow window = _windowManager.Windows.Single();
            Assert.IsTrue(window.Modal);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out ConfirmDialogWindowView _));
        }

        [Test]
        public void OpenRetireConfirmWindow_ConfirmedRetirement_RemovesOfficerAndRefreshesSource()
        {
            _controller.OpenRetireConfirmWindow(_sourceWindow, new ISceneNode[] { _officer });

            ConfirmOpenDialog();

            Assert.IsNull(_game.GetSceneNodeByInstanceID<Officer>(_officer.InstanceID));
            Assert.AreSame(_sourceWindow, _clearedWindow);
            Assert.AreEqual(1, _rebuildCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        private GameRoot CreateGame(
            out Planet origin,
            out GalaxyMapPlanet destination,
            out GalaxyMapPlanet missionTarget
        )
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
            }.WithMapPosition(0, 0);
            Planet target = new Planet
            {
                InstanceID = "target",
                DisplayName = "Target",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            }.WithMapPosition(100, 0);
            Planet enemyTarget = new Planet
            {
                InstanceID = "enemy-target",
                DisplayName = "Enemy Target",
                OwnerInstanceID = _opponentFactionId,
                IsColonized = true,
            }.WithMapPosition(200, 0);
            game.AttachNode(origin, system);
            game.AttachNode(target, system);
            game.AttachNode(enemyTarget, system);
            destination = new GalaxyMapPlanet(system, target, _playerFactionId);
            missionTarget = new GalaxyMapPlanet(system, enemyTarget, _playerFactionId);
            return game;
        }

        private UIWindow CreateSourceWindow()
        {
            GameObject sourceObject = new GameObject(
                "SourceWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            sourceObject.transform.SetParent(_rootObject.transform, false);
            UIWindow window = sourceObject.GetComponent<UIWindow>();
            window.Configure(500, 20, 30, 100, 80, false, true, false);
            return window;
        }

        private void ConfirmOpenDialog()
        {
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ConfirmButtonImage")
                .onClick.Invoke();
        }

        private sealed class MissionCreateActions : IMissionCreateWindowActions
        {
            public void RefreshAfterMissionCreation() { }

            public void OpenMissionCreateInfo() { }
        }
    }
}
