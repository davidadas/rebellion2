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
        private const string _displayPrefabPath =
            "Assets/Prefabs/UI/StrategyView/GalacticInformationDisplay.prefab";

        private TestActions _actions;
        private GalacticInformationDisplayController _controller;
        private GalacticInformationDisplayView _displayView;
        private GalacticInformationLegendView _legendView;
        private UIRaycastArea _legendTrigger;
        private List<string> _playedSounds;
        private GameObject _rootObject;
        private UIContext _uiContext;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            game.SetFactionController(_playerFactionId, "PLAYER1", PlayerControllerType.Human);
            _uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _displayView = UIComponentTestHelper
                .InstantiatePrefab(_displayPrefabPath)
                .GetComponent<GalacticInformationDisplayView>();
            _displayView.transform.SetParent(_rootObject.transform, false);
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
            _controller.BindLegendView(_legendView);
            _controller.BindDisplayView(_displayView);
            _legendTrigger = _rootObject
                .GetComponentsInChildren<UIRaycastArea>(true)
                .Single(area => area.name == "GalacticInformationLegendTrigger");
            _controller.BindLegendTrigger(_legendTrigger);
            _actions.OpenLegend = _controller.ShowLegend;
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
        public void Constructor_NullDependencies_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GalacticInformationDisplayController(null, _ => { })
            );
            Assert.Throws<ArgumentNullException>(() =>
                new GalacticInformationDisplayController(() => null, null)
            );
        }

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

        [Test]
        public void TryCancel_ClosedSelector_ReturnsFalseWithoutSideEffects()
        {
            bool cancelled = _controller.TryCancel();

            Assert.IsFalse(cancelled);
            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(0, _actions.RenderRequestCount);
        }

        [Test]
        public void SelectFilter_ChangedVisibleFilter_RequestsRenderWithoutPointerAudio()
        {
            _controller.SelectFilter(GalacticInformationFilterMode.IdleShipyards);

            Assert.AreEqual(GalacticInformationFilterMode.IdleShipyards, _controller.FilterMode);
            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        [Test]
        public void SelectFilter_ChangedVisibleFilter_RaisesFilterChanged()
        {
            GalacticInformationFilterMode? changedFilter = null;
            _controller.FilterChanged += mode => changedFilter = mode;

            _controller.SelectFilter(GalacticInformationFilterMode.IdleConstructionYards);

            Assert.AreEqual(GalacticInformationFilterMode.IdleConstructionYards, changedFilter);
        }

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

        [Test]
        public void SelectFilterFromShortcut_ActiveFilter_DoesNotRepeatControlSound()
        {
            _controller.SelectFilter(GalacticInformationFilterMode.Troopers);
            _actions.RenderRequestCount = 0;

            _controller.SelectFilterFromShortcut(GalacticInformationFilterMode.Troopers);

            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

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

        [Test]
        public void DismissPointerDown_Default_PlaysControlSoundBeforeSelectorCloses()
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

        [Test]
        public void RestoreFilter_ActiveDisplay_ShowsOnlyTheCompactControl()
        {
            _controller.RestoreFilter(GalacticInformationFilterMode.Mines);

            Assert.IsTrue(_legendTrigger.gameObject.activeSelf);
            Assert.IsFalse(_legendView.Visible);
            Assert.IsTrue(_controller.CanShowLegend);
        }

        [Test]
        public void ShowLegend_DisplayOff_KeepsLegendAndControlHidden()
        {
            _controller.ShowLegend();

            Assert.IsFalse(_legendView.Visible);
            Assert.IsFalse(_legendTrigger.gameObject.activeSelf);
            Assert.IsFalse(_controller.CanShowLegend);
        }

        [Test]
        public void ShowLegend_ActiveDisplay_KeepsTheSelectorClosed()
        {
            _controller.RestoreFilter(GalacticInformationFilterMode.Mines);

            _controller.ShowLegend();

            Assert.IsTrue(_controller.LegendOpen);
            Assert.IsFalse(_controller.Open);
        }

        [Test]
        public void LegendControl_ActiveDisplay_OpensItsKeyAndHidesControl()
        {
            _controller.RestoreFilter(GalacticInformationFilterMode.Mines);
            PointerEventData pointer = new(null) { button = PointerEventData.InputButton.Left };

            _legendTrigger.OnPointerDown(pointer);
            _legendTrigger.OnPointerUp(pointer);
            _legendTrigger.OnPointerClick(pointer);

            Assert.IsTrue(_legendView.Visible);
            Assert.IsTrue(_controller.LegendOpen);
            Assert.IsFalse(_legendTrigger.gameObject.activeSelf);
            Assert.AreEqual(
                "Mines",
                _legendView
                    .GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
                    .Single(field => field.name == "TitleTextField")
                    .text
            );
        }

        [Test]
        public void CloseControl_OpenLegend_RestoresTheCompactControl()
        {
            _controller.RestoreFilter(GalacticInformationFilterMode.Mines);
            _controller.ShowLegend();
            UIRaycastArea close = _legendView
                .GetComponentsInChildren<UIRaycastArea>(true)
                .Single(area => area.name == "CloseHitArea");

            close.OnPointerClick(
                new PointerEventData(null) { button = PointerEventData.InputButton.Left }
            );

            Assert.IsFalse(_legendView.Visible);
            Assert.IsTrue(_legendTrigger.gameObject.activeSelf);
        }

        [Test]
        public void TryCancel_OpenLegend_ClosesLegendAndConsumesEscape()
        {
            _controller.RestoreFilter(GalacticInformationFilterMode.Mines);
            _controller.ShowLegend();

            bool cancelled = _controller.TryCancel();

            Assert.IsTrue(cancelled);
            Assert.IsFalse(_legendView.Visible);
            Assert.IsTrue(_legendTrigger.gameObject.activeSelf);
            Assert.IsFalse(_controller.TryCancel());
        }

        [Test]
        public void TryCancel_DestroyedMapControl_ClosesLegendSafely()
        {
            _controller.RestoreFilter(GalacticInformationFilterMode.Mines);
            _controller.ShowLegend();
            UnityEngine.Object.DestroyImmediate(_legendTrigger.gameObject);

            bool cancelled = _controller.TryCancel();

            Assert.IsTrue(cancelled);
            Assert.IsFalse(_controller.LegendOpen);
        }

        [Test]
        public void SelectFilter_DisplayOff_HidesLegendAndControl()
        {
            _controller.RestoreFilter(GalacticInformationFilterMode.Mines);
            _controller.ShowLegend();

            _controller.SelectFilter(GalacticInformationFilterMode.DisplayOff);

            Assert.IsFalse(_legendView.Visible);
            Assert.IsFalse(_legendTrigger.gameObject.activeSelf);
            Assert.IsFalse(_controller.CanShowLegend);
        }

        [Test]
        public void SelectFilter_DifferentDisplay_DismissesPreviousKeyAndOpensCurrentKeyOnRequest()
        {
            _controller.RestoreFilter(GalacticInformationFilterMode.Mines);
            _controller.ShowLegend();

            _controller.SelectFilter(GalacticInformationFilterMode.Uprisings);
            Assert.IsFalse(_legendView.Visible);
            _controller.ShowLegend();

            Assert.AreEqual(
                "Uprisings",
                _legendView
                    .GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
                    .Single(field => field.name == "TitleTextField")
                    .text
            );
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
            public Action OpenLegend { get; set; }

            /// <summary>
            /// Routes the legend request to the bound feature controller.
            /// </summary>
            public void OpenGalacticInformationLegend()
            {
                OpenLegend?.Invoke();
            }

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
