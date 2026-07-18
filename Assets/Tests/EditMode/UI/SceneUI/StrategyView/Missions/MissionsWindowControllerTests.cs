using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionsWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private MissionsWindowController _controller;
        private Officer _decoy;
        private int _dirtyCount;
        private TestMission _firstMission;
        private GalaxyMapPlanet _planet;
        private GameObject _rootObject;
        private TestMission _secondMission;
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
            _firstMission = CreateMission("first-mission", "First Mission", out Officer _);
            _secondMission = CreateMission("second-mission", "Second Mission", out _decoy);
            _planet.Planet.Missions.Add(_firstMission);
            _planet.Planet.Missions.Add(_secondMission);
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _targetingController = new TargetingController();
            _controller = CreateController();
            _controller.Initialize(new TestActions());
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullTargetingController_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MissionsWindowController(
                    () => _uiContext,
                    _ => null,
                    null,
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
            MissionsWindowController controller = CreateController();
            MissionsWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.MissionsWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view));
        }

        [Test]
        public void TryInitializeWindow_NullPlanet_ReturnsFalse()
        {
            MissionsWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.MissionsWindowPrefab,
                _rootObject.transform
            );

            bool initialized = _controller.TryInitializeWindow(view, null);

            Assert.IsFalse(initialized);
            Assert.IsNull(_controller.GetPlanet(view));
        }

        [Test]
        public void Open_ValidPlanet_CreatesNamedWindowWithDefaultMissionSelection()
        {
            UIWindow window = _controller.Open(_planet, 20, 30, out bool created);

            Assert.IsTrue(created);
            Assert.AreEqual(
                $"MissionsWindow-{_planet.Planet.GetDisplayName()}",
                window.Content.name
            );
            Assert.AreEqual(new Vector2Int(37, 49), new Vector2Int(window.X, window.Y));
            Assert.IsFalse(window.Modal);
            Assert.AreEqual(1, _dirtyCount);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out MissionsWindowView view));
            Assert.AreSame(_planet, _controller.GetPlanet(view));
            Assert.AreEqual(0, _controller.GetSelectedMissionIndex(view));
            Assert.AreEqual(MissionParticipantRole.Agent, _controller.GetActiveRole(view));
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
        public void SelectTarget_DecoyParticipant_SelectsMissionAndDecoyRole()
        {
            MissionsWindowView view = OpenWindow(out UIWindow _);

            bool selected = _controller.SelectTarget(view, _decoy);

            Assert.IsTrue(selected);
            Assert.AreEqual(1, _controller.GetSelectedMissionIndex(view));
            Assert.AreEqual(MissionParticipantRole.Decoy, _controller.GetActiveRole(view));
            Assert.AreEqual(2, _dirtyCount);
            Assert.AreSame(_secondMission, _controller.GetStatusTarget(view).Item);
        }

        [Test]
        public void ReconcileWindow_FreshProjection_PreservesMissionSelectionByIdentity()
        {
            MissionsWindowView view = OpenWindow(out UIWindow _);
            _controller.SelectTarget(view, _secondMission);
            TestMission freshSecond = CreateMission(
                _secondMission.InstanceID,
                "Fresh Second",
                out Officer _
            );
            TestMission freshFirst = CreateMission(
                _firstMission.InstanceID,
                "Fresh First",
                out Officer _
            );
            GalaxyMapPlanet freshPlanet = new GalaxyMapPlanet(
                new GamePlanetSystem { InstanceID = "fresh-system" },
                new Planet
                {
                    InstanceID = _planet.Planet.InstanceID,
                    DisplayName = "Fresh Planet",
                    Missions = { freshSecond, freshFirst },
                },
                _playerFactionId
            );

            _controller.ReconcileWindow(view, freshPlanet);

            Assert.AreSame(freshPlanet, _controller.GetPlanet(view));
            Assert.AreEqual(0, _controller.GetSelectedMissionIndex(view));
            Assert.AreSame(freshSecond, _controller.GetStatusTarget(view).Item);
        }

        [Test]
        public void TryCreateContextMenu_SelectedMission_ReturnsAuthoredCommandsAndWidth()
        {
            OpenWindow(out UIWindow window);
            StrategyContextMenuProviderContext context = new StrategyContextMenuProviderContext(
                window,
                new StrategyContextMenuLayout(1, 2, 3, 4, 5, 177, 7),
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
            Assert.AreEqual(3, request.Commands.Count);
            Assert.IsTrue(
                request.Commands.Cast<StrategyMenuCommand>().Take(2).All(command => command.Enabled)
            );
        }

        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesPlanetAssociation()
        {
            MissionsWindowView view = OpenWindow(out UIWindow _);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.IsNull(_controller.GetPlanet(view));
        }

        private MissionsWindowController CreateController()
        {
            return new MissionsWindowController(
                () => _uiContext,
                _ => null,
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

        private static TestMission CreateMission(
            string instanceId,
            string displayName,
            out Officer decoy
        )
        {
            TestMission mission = new TestMission
            {
                InstanceID = instanceId,
                ConfigKey = MissionTypeIDs.Diplomacy,
                DisplayName = displayName,
            };
            mission.MainParticipants.Add(new Officer { InstanceID = $"{instanceId}-agent" });
            decoy = new Officer { InstanceID = $"{instanceId}-decoy" };
            mission.DecoyParticipants.Add(decoy);
            return mission;
        }

        private MissionsWindowView OpenWindow(out UIWindow window)
        {
            window = _controller.Open(_planet, 20, 30, out bool _);
            _windowManager.TryGetWindowView(window, out MissionsWindowView view);
            return view;
        }

        private sealed class TestActions : IMissionsWindowActions
        {
            public void OpenMissionsStatus(StrategyStatusTarget target) { }

            public void OpenMissionsInfo(StrategyStatusTarget target) { }
        }

        private sealed class TestMission : Mission
        {
            public override bool ShouldRepeatAfterCompletion(GameRoot game)
            {
                return false;
            }
        }
    }
}
