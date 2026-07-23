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
using UnityEngine;
using UnityEngine.UI;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Combat
{
    [TestFixture]
    public class BattleAlertWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _opponentFactionId = "FNIMP1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private BattleAlertWindowController _controller;
        private int _dirtyCount;
        private GameRoot _game;
        private PendingCombatResult _pending;
        private SpaceCombatResult _resolveResult;
        private GameObject _rootObject;
        private int _stopMusicCount;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;
        private readonly List<string> _playedSfx = new List<string>();
        private readonly List<string> _playedTracks = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            _pending = null;
            _resolveResult = null;
            _stopMusicCount = 0;
            _playedSfx.Clear();
            _playedTracks.Clear();
            _game = CreateGame(
                out Planet planet,
                out GameFleet playerFleet,
                out GameFleet opponentFleet
            );
            _uiContext = new UIContext(
                _game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _pending = new PendingCombatResult
            {
                Planet = planet,
                AttackerFleet = playerFleet,
                DefenderFleet = opponentFleet,
                AttackerCanRetreat = true,
            };
            _resolveResult = new SpaceCombatResult
            {
                Planet = planet,
                AttackerFleet = playerFleet,
                DefenderFleet = opponentFleet,
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
                Winner = CombatSide.Defender,
                AttackerOutcome = SpaceCombatSideOutcome.Withdrawn,
                DefenderOutcome = SpaceCombatSideOutcome.Active,
            };
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
        public void Constructor_NullPendingCombatProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BattleAlertWindowController(
                    null,
                    () => _resolveResult,
                    () => _resolveResult,
                    () => _uiContext,
                    _ => { },
                    _ => { },
                    () => { },
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
        public void SyncPendingCombatWindow_BeforeInitialize_ThrowsInvalidOperationException()
        {
            BattleAlertWindowController controller = CreateController();

            Assert.Throws<InvalidOperationException>(() => controller.SyncPendingCombatWindow());
        }

        [Test]
        public void BindWindow_BeforeInitialize_ThrowsInvalidOperationException()
        {
            BattleAlertWindowController controller = CreateController();
            BattleAlertWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.BattleAlertWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view));
        }

        [Test]
        public void SyncPendingCombatWindow_NoPendingCombat_DoesNotCreateWindow()
        {
            _pending = null;

            bool changed = _controller.SyncPendingCombatWindow();

            Assert.IsFalse(changed);
            Assert.IsEmpty(_windowManager.Windows);
            Assert.AreEqual(0, _dirtyCount);
        }

        [Test]
        public void SyncPendingCombatWindow_NewCombat_CreatesNamedModalAndStartsMusic()
        {
            bool changed = _controller.SyncPendingCombatWindow();

            BattleAlertWindowView view = _controller.FindWindow();
            UIWindow window = _windowManager.Windows.Single();
            Assert.IsTrue(changed);
            Assert.AreEqual("BattleAlertWindow", view.name);
            Assert.AreEqual(new Vector2Int(141, 73), new Vector2Int(window.X, window.Y));
            Assert.IsTrue(window.Modal);
            Assert.AreEqual(1, _dirtyCount);
            Assert.AreEqual(1, _playedTracks.Count);
            Assert.IsNotEmpty(_playedTracks[0]);
        }

        [Test]
        public void SyncPendingCombatWindow_ExistingCombat_ReusesWindow()
        {
            _controller.SyncPendingCombatWindow();
            BattleAlertWindowView firstView = _controller.FindWindow();

            bool changed = _controller.SyncPendingCombatWindow();

            Assert.IsFalse(changed);
            Assert.AreSame(firstView, _controller.FindWindow());
            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreEqual(1, _playedTracks.Count);
        }

        [Test]
        public void SyncPendingCombatWindow_PendingCombatCleared_ClosesPendingWindow()
        {
            OpenWindow(out UIWindow window);
            _pending = null;

            bool changed = _controller.SyncPendingCombatWindow();

            Assert.IsTrue(changed);
            Assert.IsNull(_controller.FindWindow());
            Assert.IsEmpty(_windowManager.Windows);
            Assert.IsFalse(_windowManager.Windows.Contains(window));
        }

        [Test]
        public void RetreatButton_ResolvedCombat_PreservesResultAndRoutesRefresh()
        {
            BattleAlertWindowView view = OpenWindow(out UIWindow _);

            FindButton(view, "RetreatButtonImage").onClick.Invoke();

            Assert.IsTrue(_controller.HasCombatResult(view));
            Assert.AreEqual(1, _stopMusicCount);
            Assert.AreEqual(1, _actions.RebuildCount);
            Assert.AreEqual(2, _dirtyCount);
        }

        [Test]
        public void SyncPendingCombatWindow_ResultWithPendingCleared_PreservesResultWindow()
        {
            BattleAlertWindowView view = OpenWindow(out UIWindow _);
            FindButton(view, "RetreatButtonImage").onClick.Invoke();
            _pending = null;

            bool changed = _controller.SyncPendingCombatWindow();

            Assert.IsFalse(changed);
            Assert.AreSame(view, _controller.FindWindow());
            Assert.IsTrue(_controller.HasCombatResult(view));
        }

        [Test]
        public void OpenResult_Bombardment_OpensSharedResultWindowWithoutBattleMusic()
        {
            _pending = null;
            BombardmentResult result = new BombardmentResult
            {
                Planet = _resolveResult.Planet,
                AttackingFaction = _game.GetFactionByOwnerInstanceID(_playerFactionId),
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
            };

            _controller.OpenResult(result);

            BattleAlertWindowView view = _controller.FindWindow();
            BattleAlertWindowRenderData data = _controller.CreateRenderData(
                view,
                _playerFactionId,
                141,
                73
            );
            Assert.IsTrue(_controller.HasCombatResult(view));
            Assert.IsTrue(_windowManager.Windows.Single().Modal);
            Assert.AreEqual(BattleAlertWindowMode.Result, data.Mode);
            Assert.AreEqual(BattleResultCategory.CapitalShips, data.Result.Category);
            Assert.AreEqual("Orbital bombardment of Corellia", data.Result.Title);
            Assert.IsEmpty(_playedTracks);
        }

        [Test]
        public void OpenResult_PlanetaryAssault_DefaultsToTroopsWithoutBattleMusic()
        {
            _pending = null;
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = _resolveResult.Planet,
                AttackingFaction = _game.GetFactionByOwnerInstanceID(_playerFactionId),
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
            };

            _controller.OpenResult(result);

            BattleAlertWindowView view = _controller.FindWindow();
            BattleAlertWindowRenderData data = _controller.CreateRenderData(
                view,
                _playerFactionId,
                141,
                73
            );
            Assert.IsTrue(_controller.HasCombatResult(view));
            Assert.IsTrue(_windowManager.Windows.Single().Modal);
            Assert.AreEqual(BattleAlertWindowMode.Result, data.Mode);
            Assert.AreEqual(BattleResultCategory.Troops, data.Result.Category);
            Assert.AreEqual("Assault on Corellia", data.Result.Title);
            Assert.IsEmpty(_playedTracks);
            CollectionAssert.AreEqual(new[] { StrategyUISoundPaths.PlanetaryAssault }, _playedSfx);
        }

        [Test]
        public void ControlButton_InitializedView_PlaysSharedControlSound()
        {
            BattleAlertWindowView view = OpenWindow(out UIWindow _);

            FindButton(view, "SummaryButtonImage").onClick.Invoke();

            CollectionAssert.AreEqual(new[] { StrategyUISoundPaths.ControlPress }, _playedSfx);
        }

        [Test]
        public void ViewDestroyed_BoundSession_ReleasesControllerState()
        {
            BattleAlertWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.Throws<InvalidOperationException>(() =>
                _controller.CreateRenderData(view, _playerFactionId, 0, 0)
            );
        }

        private BattleAlertWindowController CreateController()
        {
            return new BattleAlertWindowController(
                () => _pending,
                () => _resolveResult,
                () => _resolveResult,
                () => _uiContext,
                path => _playedSfx.Add(path),
                path => _playedTracks.Add(path),
                () => _stopMusicCount++,
                _windowLayer,
                _windowManager,
                () => new Vector2Int(141, 73),
                window => _windowManager.DestroyWindow(window),
                () => _dirtyCount++
            );
        }

        private GameRoot CreateGame(
            out Planet planet,
            out GameFleet playerFleet,
            out GameFleet opponentFleet
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
            planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _opponentFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, system);
            playerFleet = CreateFleet("player-fleet", _playerFactionId);
            opponentFleet = CreateFleet("opponent-fleet", _opponentFactionId);
            return game;
        }

        private static GameFleet CreateFleet(string instanceId, string ownerId)
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{instanceId}-ship",
                OwnerInstanceID = ownerId,
            };
            return new GameFleet(ownerId, instanceId, new List<CapitalShip> { ship })
            {
                InstanceID = instanceId,
            };
        }

        private BattleAlertWindowView OpenWindow(out UIWindow window)
        {
            _controller.SyncPendingCombatWindow();
            BattleAlertWindowView view = _controller.FindWindow();
            window = _windowManager.Windows.Single();
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            return view;
        }

        private static Button FindButton(BattleAlertWindowView view, string objectName)
        {
            return view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == objectName);
        }

        private sealed class TestActions : IBattleAlertWindowActions
        {
            public int RebuildCount { get; private set; }

            public void OpenBattleResultFleet(Planet planet, int sourceX, int sourceY) { }

            public void OpenBattleResultSystem(
                GamePlanetSystem system,
                int sourceX,
                int sourceY
            ) { }

            public void RebuildBattleSnapshot()
            {
                RebuildCount++;
            }
        }
    }
}
