using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

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
        private Planet _retreatPlanet;
        private Planet _visiblePlanet;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;
        private readonly List<string> _playedSfx = new List<string>();
        private readonly List<string> _playedTracks = new List<string>();

        /// <summary>
        /// Sets up.
        /// </summary>
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
            _uiContext = TestContent.CreateUIContext(
                _game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _pending = new PendingCombatResult
            {
                PlanetInstanceID = planet.InstanceID,
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
                AttackerCanRetreat = true,
            };
            _visiblePlanet = planet;
            _retreatPlanet = new Planet
            {
                InstanceID = "retreat-planet",
                DisplayName = "Bortras",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            _game.AttachNode(_retreatPlanet, planet.GetParent());
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
                AttackerRetreatPlanetInstanceID = _retreatPlanet.InstanceID,
            };
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _controller = CreateController();
            _actions = new TestActions();
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
        public void Constructor_NullPendingCombatProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BattleAlertWindowController(
                    null,
                    () => _resolveResult,
                    () => _resolveResult,
                    () => _uiContext,
                    _ => _visiblePlanet,
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
        public void CreateRenderData_PendingCombat_UsesVisiblePlanetProjection()
        {
            _resolveResult.Planet.EnergyCapacity = 10;
            Building hiddenBuilding = new Building
            {
                InstanceID = "hidden-building",
                DisplayName = "Hidden shipyard",
                OwnerInstanceID = _opponentFactionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(hiddenBuilding, _resolveResult.Planet);
            _visiblePlanet = new Planet
            {
                InstanceID = _resolveResult.Planet.InstanceID,
                DisplayName = _resolveResult.Planet.DisplayName,
                OwnerInstanceID = _opponentFactionId,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            Building visibleBuilding = new Building
            {
                InstanceID = "visible-building",
                DisplayName = "Visible mine",
                OwnerInstanceID = _opponentFactionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _visiblePlanet.AddChild(visibleBuilding);
            visibleBuilding.SetParent(_visiblePlanet);
            BattleAlertWindowView view = OpenWindow(out UIWindow _);
            view.RequestPrimaryPanel(BattleAlertPanel.SystemAssets);

            BattleAlertWindowRenderData data = _controller.CreateRenderData(
                view,
                _playerFactionId,
                141,
                73
            );

            CollectionAssert.AreEqual(
                new[] { "Visible mine" },
                data.Pending.Rows.Select(row => row.Text)
            );
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
        public void OpenResult_SpaceCombat_PlaysCompletedBattleMusic()
        {
            _pending = null;

            _controller.OpenResult(_resolveResult);

            Assert.AreEqual(1, _stopMusicCount);
            Assert.AreEqual(1, _playedTracks.Count);
            Assert.IsNotEmpty(_playedTracks[0]);
        }

        [Test]
        public void OpenReport_SpaceCombat_PreservesStrategyMusic()
        {
            CombatReport report = new CombatReport
            {
                CombatType = CombatReportType.SpaceBattle,
                PerspectiveOwnerInstanceID = _playerFactionId,
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
                Winner = CombatSide.Defender,
                Title = "Battle at Corellia",
                Body = "The defending fleet was victorious.",
            };

            _controller.OpenReport(report);

            Assert.AreEqual(0, _stopMusicCount);
            Assert.IsEmpty(_playedTracks);
        }

        [Test]
        public void OpenReport_PlanetaryAssault_PlaysReportSoundWithoutChangingMusic()
        {
            CombatReport report = new CombatReport
            {
                CombatType = CombatReportType.PlanetaryAssault,
                PerspectiveOwnerInstanceID = _playerFactionId,
                AttackerOwnerInstanceID = _opponentFactionId,
                DefenderOwnerInstanceID = _playerFactionId,
                Title = "Assault on Corellia",
                Body = "The defending troops repulsed the assault.",
            };

            _controller.OpenReport(report);

            Assert.AreEqual(0, _stopMusicCount);
            Assert.IsEmpty(_playedTracks);
            CollectionAssert.AreEqual(new[] { StrategyUISoundPaths.PlanetaryAssault }, _playedSfx);
        }

        [Test]
        public void OpenFleet_PlayerFleetWithdrawn_OpensRetreatPlanet()
        {
            _pending = null;
            _controller.OpenResult(_resolveResult);
            BattleAlertWindowView view = _controller.FindWindow();

            view.RequestOpenFleet();

            Assert.AreSame(_retreatPlanet, _actions.OpenedFleet);
        }

        [Test]
        public void OpenFleet_RetreatPlanetHidden_DoesNotOpenPlanet()
        {
            _pending = null;
            _retreatPlanet = null;
            _controller.OpenResult(_resolveResult);
            BattleAlertWindowView view = _controller.FindWindow();

            view.RequestOpenFleet();

            Assert.IsNull(_actions.OpenedFleet);
        }

        [Test]
        public void CloseRequested_ReportWithContinuation_ClosesWindowAndInvokesContinuation()
        {
            _pending = null;
            int continuationCount = 0;
            CombatReport report = new CombatReport
            {
                CombatType = CombatReportType.SpaceBattle,
                PerspectiveOwnerInstanceID = _playerFactionId,
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
                Title = "Battle at Corellia",
                Body = "The defending fleet was victorious.",
            };
            _controller.OpenReport(report, () => continuationCount++);
            BattleAlertWindowView view = _controller.FindWindow();

            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            view.RequestClose();

            Assert.IsNull(_controller.FindWindow());
            Assert.AreEqual(1, continuationCount);
        }

        [Test]
        public void TryCloseWindow_ReportWithContinuation_ClosesWindowAndInvokesContinuation()
        {
            _pending = null;
            int continuationCount = 0;
            CombatReport report = new CombatReport
            {
                CombatType = CombatReportType.SpaceBattle,
                PerspectiveOwnerInstanceID = _playerFactionId,
                AttackerOwnerInstanceID = _playerFactionId,
                DefenderOwnerInstanceID = _opponentFactionId,
                Title = "Battle at Corellia",
                Body = "The defending fleet was victorious.",
            };
            _controller.OpenReport(report, () => continuationCount++);
            UIWindow window = _windowManager.Windows.Single();

            bool handled = _controller.TryCloseWindow(window);

            Assert.IsTrue(handled);
            Assert.IsNull(_controller.FindWindow());
            Assert.AreEqual(1, continuationCount);
        }

        [Test]
        public void ControlButton_PointerDown_PlaysSharedControlSoundBeforeClick()
        {
            BattleAlertWindowView view = OpenWindow(out UIWindow window);
            _controller.RenderWindow(view, window);
            RawImagePressVisual pressVisual = view.GetComponentsInChildren<RawImagePressVisual>(
                    true
                )
                .Single(visual => visual.name == "SummaryButtonImage");

            pressVisual.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Left }
            );

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

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
        private BattleAlertWindowController CreateController()
        {
            return new BattleAlertWindowController(
                () => _pending,
                () => _resolveResult,
                () => _resolveResult,
                () => _uiContext,
                instanceId =>
                    _retreatPlanet?.InstanceID == instanceId ? _retreatPlanet
                    : _visiblePlanet?.InstanceID == instanceId ? _visiblePlanet
                    : null,
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

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="playerFleet">Receives the player fleet.</param>
        /// <param name="opponentFleet">Receives the opponent fleet.</param>
        /// <returns>The created game.</returns>
        private GameRoot CreateGame(
            out Planet planet,
            out GameFleet playerFleet,
            out GameFleet opponentFleet
        )
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            game.GetFactions().Add(new Faction { InstanceID = _opponentFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            game.SetFactionController(_playerFactionId, "PLAYER1", PlayerControllerType.Human);
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector
            {
                InstanceID = "sector",
                DisplayName = "Core Sector",
            };
            game.AttachNode(planetSector, game.GetGalaxyMap());
            planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _opponentFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, planetSector);
            playerFleet = CreateFleet("player-fleet", _playerFactionId);
            opponentFleet = CreateFleet("opponent-fleet", _opponentFactionId);
            return game;
        }

        /// <summary>
        /// Creates fleet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The created fleet.</returns>
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

        /// <summary>
        /// Opens window.
        /// </summary>
        /// <param name="window">Receives the window.</param>
        /// <returns>The result of open window.</returns>
        private BattleAlertWindowView OpenWindow(out UIWindow window)
        {
            _controller.SyncPendingCombatWindow();
            BattleAlertWindowView view = _controller.FindWindow();
            window = _windowManager.Windows.Single();
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            return view;
        }

        /// <summary>
        /// Finds button.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching button.</returns>
        private static Button FindButton(BattleAlertWindowView view, string objectName)
        {
            return view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == objectName);
        }

        private sealed class TestActions : IBattleAlertWindowActions
        {
            public Planet OpenedFleet { get; private set; }

            public int RebuildCount { get; private set; }

            /// <summary>
            /// Opens battle result fleet.
            /// </summary>
            /// <param name="planet">The planet.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenBattleResultFleet(Planet planet, int sourceX, int sourceY)
            {
                OpenedFleet = planet;
            }

            /// <summary>
            /// Opens battle result sector.
            /// </summary>
            /// <param name="planetSector">The planet sector.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenBattleResultSector(
                GalaxyPlanetSector planetSector,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Rebuilds battle snapshot.
            /// </summary>
            public void RebuildBattleSnapshot()
            {
                RebuildCount++;
            }
        }
    }
}
