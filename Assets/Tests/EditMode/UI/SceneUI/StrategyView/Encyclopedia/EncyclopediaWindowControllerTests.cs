using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Encyclopedia
{
    [TestFixture]
    public class EncyclopediaWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private EncyclopediaWindowController _controller;
        private int _dirtyCount;
        private GameObject _rootObject;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Player" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(
                    new[]
                    {
                        new EncyclopediaEntry
                        {
                            TypeID = "FLEET",
                            DisplayName = "Fleet",
                            Category = EncyclopediaEntryCategory.Ship,
                        },
                        new EncyclopediaEntry
                        {
                            TypeID = "PLANET",
                            DisplayName = "Planet",
                            Category = EncyclopediaEntryCategory.System,
                        },
                    }
                )
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _controller = CreateController();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EncyclopediaWindowController(
                    null,
                    _ => { },
                    _windowLayer,
                    _windowManager,
                    () => Vector2Int.zero,
                    () => { }
                )
            );
        }

        [Test]
        public void Open_ClosedController_CreatesBoundWindowAtConfiguredPosition()
        {
            _controller.Open();

            Assert.AreEqual(1, _windowManager.Windows.Count);
            UIWindow window = _windowManager.Windows.Single();
            Assert.AreEqual("EncyclopediaWindow", window.Content.name);
            Assert.AreEqual(new Vector2Int(123, 45), new Vector2Int(window.X, window.Y));
            Assert.IsTrue(
                _windowManager.TryGetWindowView(window, out EncyclopediaWindowView encyclopediaView)
            );
            EncyclopediaWindowState state = _controller.GetState(encyclopediaView);
            Assert.AreEqual(EncyclopediaWindowTab.AllDatabases, state.ActiveTab);
            Assert.AreEqual(-1, state.SelectedIndex);
            Assert.IsFalse(state.Panel);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void Open_ExistingWindow_FocusesWithoutCreatingAnotherWindow()
        {
            _controller.Open();
            UIWindow window = _windowManager.Windows.Single();

            _controller.Open();

            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreSame(window, _windowManager.ActiveWindow);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void RequestEntry_FleetNode_OpensMatchingCatalogTopic()
        {
            _controller.Open();
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out EncyclopediaWindowView encyclopediaView);

            _controller.RequestEntry(encyclopediaView, new GameFleet());

            EncyclopediaWindowState state = _controller.GetState(encyclopediaView);
            Assert.IsTrue(state.Panel);
            Assert.AreEqual(0, state.SelectedIndex);
            Assert.AreEqual(2, _dirtyCount);
        }

        [Test]
        public void GetState_UnboundView_ThrowsInvalidOperationException()
        {
            EncyclopediaWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.EncyclopediaWindowPrefab
            );

            Assert.Throws<InvalidOperationException>(() => _controller.GetState(view));

            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void FindEntryIndex_Entries_ReturnsExactMatchOrNegativeOne()
        {
            EncyclopediaEntry[] entries =
            {
                new EncyclopediaEntry { TypeID = "first" },
                null,
                new EncyclopediaEntry { TypeID = "second" },
            };

            Assert.AreEqual(2, EncyclopediaWindowController.FindEntryIndex(entries, "second"));
            Assert.AreEqual(-1, EncyclopediaWindowController.FindEntryIndex(entries, "SECOND"));
            Assert.AreEqual(-1, EncyclopediaWindowController.FindEntryIndex(entries, null));
            Assert.AreEqual(-1, EncyclopediaWindowController.FindEntryIndex(null, "second"));
        }

        [Test]
        public void GetEntryTypeID_KnownSceneNodes_ReturnsCatalogIdentity()
        {
            Planet planet = new Planet { TypeID = "PLANET" };

            Assert.AreEqual("FLEET", EncyclopediaWindowController.GetEntryTypeID(new GameFleet()));
            Assert.AreEqual("PLANET", EncyclopediaWindowController.GetEntryTypeID(planet));
            Assert.IsNull(EncyclopediaWindowController.GetEntryTypeID(null));
        }

        [TestCase(ResearchDiscipline.ShipDesign, MissionIconKeys.ResearchShipDesign)]
        [TestCase(ResearchDiscipline.FacilityDesign, MissionIconKeys.ResearchFacilityDesign)]
        [TestCase(ResearchDiscipline.TroopTraining, MissionIconKeys.ResearchTroopTraining)]
        public void GetEntryTypeID_ResearchMission_ReturnsDisciplineIdentity(
            ResearchDiscipline discipline,
            string expected
        )
        {
            ResearchMission mission = new ResearchMission { Discipline = discipline };

            string typeId = EncyclopediaWindowController.GetEntryTypeID(mission);

            Assert.AreEqual(expected, typeId);
        }

        private EncyclopediaWindowController CreateController()
        {
            return new EncyclopediaWindowController(
                () => _uiContext,
                _ => { },
                _windowLayer,
                _windowManager,
                () => new Vector2Int(123, 45),
                () => _dirtyCount++
            );
        }
    }
}
