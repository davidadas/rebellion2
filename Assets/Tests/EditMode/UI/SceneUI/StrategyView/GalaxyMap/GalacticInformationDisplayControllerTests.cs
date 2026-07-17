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

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
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
        public void SelectFilter_ChangedVisibleFilter_PlaysControlSoundAndRequestsRender()
        {
            _controller.SelectFilter(GalacticInformationFilterMode.IdleShipyards);

            Assert.AreEqual(GalacticInformationFilterMode.IdleShipyards, _controller.FilterMode);
            CollectionAssert.AreEqual(
                new[] { StrategyUISoundPaths.GalacticInformationControl },
                _playedSounds
            );
            Assert.AreEqual(1, _actions.RenderRequestCount);
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
        public void SelectorControls_FilterSelection_RouteSemanticControllerAction()
        {
            _controller.Show();
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };
            FindRaycastArea("LoyaltyCategoryHitArea").OnPointerEnter(eventData);

            FindRaycastArea("PopularSupportFilterHitArea").OnPointerClick(eventData);

            Assert.AreEqual(GalacticInformationFilterMode.PopularSupport, _controller.FilterMode);
            Assert.IsFalse(_controller.Open);
            CollectionAssert.AreEqual(
                new[] { StrategyUISoundPaths.GalacticInformationControl },
                _playedSounds
            );
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        private UIRaycastArea FindRaycastArea(string objectName)
        {
            return _displayView
                .GetComponentsInChildren<UIRaycastArea>(true)
                .Single(area => area.name == objectName);
        }

        private sealed class TestActions : IGalacticInformationDisplayActions
        {
            public int RenderRequestCount { get; set; }

            public void RequestGalacticInformationRender()
            {
                RenderRequestCount++;
            }
        }
    }
}
