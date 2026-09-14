using System;
using System.Collections.Generic;
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
using UnityEngine.EventSystems;
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
        private readonly List<string> _playedSfx = new List<string>();
        private GameObject _rootObject;
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
            _playedSfx.Clear();
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions()
                .Add(new Faction { InstanceID = _playerFactionId, DisplayName = "Player" });
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
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
            _windowManager.WindowCloseRequested += _windowManager.DestroyWindow;
            _controller = CreateController();
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
        /// Verifies constructor null context provider throws argument null exception.
        /// </summary>
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

        /// <summary>
        /// Verifies open closed controller creates bound window at configured position.
        /// </summary>
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

        /// <summary>
        /// Verifies that repeating the Encyclopedia command closes its existing window.
        /// </summary>
        [Test]
        public void Open_ExistingWindow_TogglesWindowClosed()
        {
            _controller.Open();

            _controller.Open();

            Assert.IsEmpty(_windowManager.Windows);
            Assert.IsNull(_windowManager.ActiveWindow);
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies request entry fleet node opens matching catalog topic.
        /// </summary>
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

        /// <summary>
        /// Verifies get state unbound view throws invalid operation exception.
        /// </summary>
        [Test]
        public void GetState_UnboundView_ThrowsInvalidOperationException()
        {
            EncyclopediaWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.EncyclopediaWindowPrefab
            );

            Assert.Throws<InvalidOperationException>(() => _controller.GetState(view));

            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        /// <summary>
        /// Verifies dialog control pointer down plays shared control sound before click.
        /// </summary>
        [Test]
        public void DialogControl_PointerDown_PlaysSharedControlSoundBeforeClick()
        {
            _controller.Open();
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out EncyclopediaWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            _controller.RenderWindow(view, window);
            RawImagePressVisual closePressVisual =
                view.GetComponentsInChildren<RawImagePressVisual>(true)
                    .Single(visual => visual.name == "LowerLayoutCloseButtonImage");

            closePressVisual.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Left }
            );

            CollectionAssert.AreEqual(new[] { StrategyUISoundPaths.ControlPress }, _playedSfx);
            Assert.AreEqual(1, _windowManager.Windows.Count);
        }

        /// <summary>
        /// Verifies find entry index entries returns exact match or negative one.
        /// </summary>
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

        /// <summary>
        /// Verifies get entry type id known scene nodes returns catalog identity.
        /// </summary>
        [Test]
        public void GetEntryTypeID_KnownSceneNodes_ReturnsCatalogIdentity()
        {
            Planet planet = new Planet { TypeID = "PLANET" };

            Assert.AreEqual("FLEET", EncyclopediaWindowController.GetEntryTypeID(new GameFleet()));
            Assert.AreEqual("PLANET", EncyclopediaWindowController.GetEntryTypeID(planet));
            Assert.IsNull(EncyclopediaWindowController.GetEntryTypeID(null));
        }

        /// <summary>
        /// Verifies get entry type id research mission returns discipline identity.
        /// </summary>
        /// <param name="discipline">The discipline.</param>
        /// <param name="expected">The expected.</param>
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

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
        private EncyclopediaWindowController CreateController()
        {
            return new EncyclopediaWindowController(
                () => _uiContext,
                path => _playedSfx.Add(path),
                _windowLayer,
                _windowManager,
                () => new Vector2Int(123, 45),
                () => _dirtyCount++
            );
        }
    }
}
