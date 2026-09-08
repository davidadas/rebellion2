using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.IdleBar
{
    [TestFixture]
    public class IdleBarControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private IdleBarController _controller;
        private Officer _officer;
        private GameObject _rootObject;
        private IdleBarView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<IdleBarView>(true);
            _officer = new Officer { InstanceID = "officer", DisplayName = "Officer" };
            _actions = new TestActions();
            _controller = new IdleBarController(
                () => null,
                () => null,
                () => true,
                instanceId => instanceId == _officer.InstanceID ? _officer : null
            );
            _controller.Initialize(_actions);
            _controller.BindView(_view);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void BindView_BeforeInitialize_Throws()
        {
            IdleBarController controller = new IdleBarController(
                () => null,
                () => null,
                () => false,
                _ => null
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindView(_view));
        }

        [Test]
        public void SelectEntry_ResolvesAndRoutesEntity()
        {
            RenderOfficerDirectly();

            _view
                .GetComponentInChildren<IdleBarSlotView>(false)
                .GetComponent<Button>()
                .onClick.Invoke();

            Assert.AreSame(_officer, _actions.OpenedTarget);
        }

        [Test]
        public void ToggleTracking_ChangesStateAndRequestsRender()
        {
            Assert.IsTrue(_controller.IsIdleBarTracked(_officer));

            _controller.ToggleIdleBarTracking(_officer);

            Assert.IsFalse(_controller.IsIdleBarTracked(_officer));
            Assert.AreEqual(1, _actions.RenderRequestCount);

            _controller.ToggleIdleBarTracking(_officer);

            Assert.IsTrue(_controller.IsIdleBarTracked(_officer));
            Assert.AreEqual(2, _actions.RenderRequestCount);
        }

        [Test]
        public void SecondaryClick_UntracksEntryAndRequestsRender()
        {
            RenderOfficerDirectly();
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);
            PointerEventData rightClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };

            slot.OnPointerClick(rightClick);

            Assert.IsFalse(_controller.IsIdleBarTracked(_officer));
            Assert.AreEqual(1, _actions.RenderRequestCount);
        }

        [Test]
        public void ResetSession_RestoresTracking()
        {
            _controller.ToggleIdleBarTracking(_officer);

            _controller.ResetSession();

            Assert.IsTrue(_controller.IsIdleBarTracked(_officer));
        }

        [Test]
        public void Render_DisabledFeature_HidesViewWithoutThemeData()
        {
            IdleBarController controller = new IdleBarController(
                () => null,
                () => null,
                () => false,
                _ => null
            );
            controller.Initialize(_actions);
            controller.BindView(_view);

            controller.Render();

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        private void RenderOfficerDirectly()
        {
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new List<IdleBarEntry> { new IdleBarEntry(_officer, null) },
                    new RectInt(0, 0, 700, 350)
                )
            );
        }

        private sealed class TestActions : IIdleBarActions
        {
            public int RenderRequestCount { get; private set; }

            public ISceneNode OpenedTarget { get; private set; }

            public void OpenIdleBarTarget(ISceneNode target)
            {
                OpenedTarget = target;
            }

            public void RequestIdleBarRender()
            {
                RenderRequestCount++;
            }
        }
    }
}
