using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalacticInformationDisplayControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private GalacticInformationDisplayController _controller;
        private GalacticInformationDisplayView _displayView;
        private GalacticInformationLegendView _legendView;
        private List<string> _playedSounds;
        private GameObject _rootObject;
        private UIContext _uiContext;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            game.SetFactionController(_playerFactionId, "PLAYER1", PlayerControllerType.Human);
            _uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _displayView = _rootObject.GetComponentInChildren<GalacticInformationDisplayView>(true);
            _legendView = _rootObject.GetComponentInChildren<GalacticInformationLegendView>(true);
            foreach (
                GalacticInformationFrameView frame in _displayView.GetComponentsInChildren<GalacticInformationFrameView>(
                    true
                )
            )
            {
                UIComponentTestHelper.InvokeLifecycle(frame, "Awake");
            }
            foreach (
                GalacticInformationSubmenuView submenu in _displayView.GetComponentsInChildren<GalacticInformationSubmenuView>(
                    true
                )
            )
            {
                UIComponentTestHelper.InvokeLifecycle(submenu, "Awake");
            }
            UIComponentTestHelper.InvokeLifecycle(_displayView, "Awake");
            UIComponentTestHelper.InvokeLifecycle(
                _legendView.GetComponentInChildren<GalacticInformationFrameView>(true),
                "Awake"
            );
            UIComponentTestHelper.InvokeLifecycle(_legendView, "Awake");
            _playedSounds = new List<string>();
            _actions = new TestActions();
            _controller = new GalacticInformationDisplayController(
                () => _uiContext,
                path => _playedSounds.Add(path)
            );
            _controller.Initialize(_actions);
            _controller.BindViews(_displayView, _legendView);
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
        /// Verifies constructor null dependencies throw argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullDependencies_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GalacticInformationDisplayController(null, _ => { })
            );
            Assert.Throws<ArgumentNullException>(() =>
                new GalacticInformationDisplayController(() => null, null)
            );
        }

        /// <summary>
        /// Verifies show closed selector opens authored display.
        /// </summary>
        [Test]
        public void Show_ClosedSelector_OpensAuthoredDisplay()
        {
            _controller.Show();

            Assert.IsTrue(_controller.Open);
            Assert.IsTrue(_displayView.gameObject.activeSelf);
            Assert.AreEqual(GalacticInformationFilterMode.DisplayOff, _controller.FilterMode);
            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(0, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Verifies try cancel open selector closes display plays control sound and requests render.
        /// </summary>
        [Test]
        public void TryCancel_OpenSelector_ClosesDisplayPlaysControlSoundAndRequestsRender()
        {
            _controller.Show();

            bool cancelled = _controller.TryCancel();

            Assert.IsTrue(cancelled);
            Assert.IsFalse(_controller.Open);
            Assert.IsFalse(_displayView.gameObject.activeSelf);
            CollectionAssert.AreEqual(
                new[] { StrategyUISoundPaths.GalacticInformationControl },
                _playedSounds
            );
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Verifies try cancel closed selector returns false without side effects.
        /// </summary>
        [Test]
        public void TryCancel_ClosedSelector_ReturnsFalseWithoutSideEffects()
        {
            bool cancelled = _controller.TryCancel();

            Assert.IsFalse(cancelled);
            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(0, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Verifies select filter changed visible filter requests render without pointer audio.
        /// </summary>
        [Test]
        public void SelectFilter_ChangedVisibleFilter_RequestsRenderWithoutPointerAudio()
        {
            _controller.SelectFilter(GalacticInformationFilterMode.IdleShipyards);

            Assert.AreEqual(GalacticInformationFilterMode.IdleShipyards, _controller.FilterMode);
            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Verifies selecting a different filter publishes the durable selection.
        /// </summary>
        [Test]
        public void SelectFilter_ChangedVisibleFilter_RaisesFilterChanged()
        {
            GalacticInformationFilterMode? changedFilter = null;
            _controller.FilterChanged += mode => changedFilter = mode;

            _controller.SelectFilter(GalacticInformationFilterMode.IdleConstructionYards);

            Assert.AreEqual(GalacticInformationFilterMode.IdleConstructionYards, changedFilter);
        }

        /// <summary>
        /// Verifies restoring a filter updates selection without publishing a user change.
        /// </summary>
        [Test]
        public void RestoreFilter_SavedFilter_RestoresWithoutRaisingFilterChanged()
        {
            int changeCount = 0;
            _controller.FilterChanged += _ => changeCount++;

            _controller.RestoreFilter(GalacticInformationFilterMode.IdleConstructionYards);

            Assert.AreEqual(
                GalacticInformationFilterMode.IdleConstructionYards,
                _controller.FilterMode
            );
            Assert.AreEqual(0, changeCount);
        }

        /// <summary>
        /// Verifies select filter active filter requests render without repeating audio.
        /// </summary>
        [Test]
        public void SelectFilter_ActiveFilter_RequestsRenderWithoutRepeatingAudio()
        {
            _controller.SelectFilter(GalacticInformationFilterMode.IdleShipyards);
            _playedSounds.Clear();
            _actions.RenderRequestCount = 0;

            _controller.SelectFilter(GalacticInformationFilterMode.IdleShipyards);

            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Verifies shortcut-driven filter changes play control audio and request rendering.
        /// </summary>
        [Test]
        public void SelectFilterFromShortcut_ChangedFilter_PlaysControlSoundAndRequestsRender()
        {
            _controller.SelectFilterFromShortcut(GalacticInformationFilterMode.Troopers);

            Assert.AreEqual(GalacticInformationFilterMode.Troopers, _controller.FilterMode);
            CollectionAssert.AreEqual(
                new[] { StrategyUISoundPaths.GalacticInformationControl },
                _playedSounds
            );
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Verifies selecting the active shortcut filter does not replay control audio.
        /// </summary>
        [Test]
        public void SelectFilterFromShortcut_ActiveFilter_DoesNotRepeatControlSound()
        {
            _controller.SelectFilter(GalacticInformationFilterMode.Troopers);
            _actions.RenderRequestCount = 0;

            _controller.SelectFilterFromShortcut(GalacticInformationFilterMode.Troopers);

            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Verifies selector controls filter selection route semantic controller action.
        /// </summary>
        [Test]
        public void SelectorControls_FilterSelection_RouteSemanticControllerAction()
        {
            _controller.Show();
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };
            FindRaycastArea("LoyaltyCategoryHitArea").OnPointerEnter(eventData);

            FindRaycastArea("PopularSupportFilterHitArea").OnPointerDown(eventData);

            CollectionAssert.AreEqual(
                new[] { StrategyUISoundPaths.GalacticInformationControl },
                _playedSounds
            );
            Assert.AreEqual(GalacticInformationFilterMode.DisplayOff, _controller.FilterMode);
            Assert.IsTrue(_controller.Open);
            FindRaycastArea("PopularSupportFilterHitArea").OnPointerClick(eventData);

            Assert.AreEqual(GalacticInformationFilterMode.PopularSupport, _controller.FilterMode);
            Assert.IsFalse(_controller.Open);
            CollectionAssert.AreEqual(
                new[] { StrategyUISoundPaths.GalacticInformationControl },
                _playedSounds
            );
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Verifies dismiss pointer down plays control sound before selector closes.
        /// </summary>
        [Test]
        public void DismissPointerDown_PlaysControlSoundBeforeSelectorCloses()
        {
            _controller.Show();
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            FindRaycastArea("DismissHitArea").OnPointerDown(eventData);

            CollectionAssert.AreEqual(
                new[] { StrategyUISoundPaths.GalacticInformationControl },
                _playedSounds
            );
            Assert.IsTrue(_controller.Open);
            Assert.AreEqual(0, _actions.RenderRequestCount);

            FindRaycastArea("DismissHitArea").OnPointerClick(eventData);

            Assert.IsFalse(_controller.Open);
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        /// <summary>
        /// Finds raycast area.
        /// </summary>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching raycast area.</returns>
        private UIRaycastArea FindRaycastArea(string objectName)
        {
            return _displayView
                .GetComponentsInChildren<UIRaycastArea>(true)
                .Single(area => area.name == objectName);
        }

        private sealed class TestActions : IGalacticInformationDisplayActions
        {
            public int RenderRequestCount { get; set; }

            /// <summary>
            /// Executes request galactic information render.
            /// </summary>
            public void RequestGalacticInformationRender()
            {
                RenderRequestCount++;
            }
        }
    }
}
