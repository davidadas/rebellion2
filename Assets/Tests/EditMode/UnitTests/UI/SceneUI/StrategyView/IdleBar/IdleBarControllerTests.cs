using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
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
        private ContextMenuController _contextMenuController;
        private IdleBarController _controller;
        private Officer _officer;
        private ISceneNode _resolvedEntity;
        private GameObject _rootObject;
        private IdleBarView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<IdleBarView>(true);
            _officer = new Officer { InstanceID = "officer", DisplayName = "Officer" };
            _resolvedEntity = _officer;
            _actions = new TestActions();
            _contextMenuController = new ContextMenuController();
            _controller = new IdleBarController(
                () => null,
                _contextMenuController,
                () => null,
                () => true,
                instanceId => instanceId == _resolvedEntity?.InstanceID ? _resolvedEntity : null
            );
            _controller.Initialize(_actions);
            _controller.BindView(_view);
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void BindView_BeforeInitialize_Throws()
        {
            IdleBarController controller = new IdleBarController(
                () => null,
                new ContextMenuController(),
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
        public void SecondaryClick_ResolvesAndRoutesContextMenuWithoutUntracking()
        {
            RenderOfficerDirectly();
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);
            PointerEventData rightClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };
            slot.OnPointerEnter(new PointerEventData(null));

            slot.OnPointerClick(rightClick);

            Assert.IsTrue(_controller.IsIdleBarTracked(_officer));
            Assert.AreSame(_officer, _actions.ContextTarget);
            Assert.AreSame(rightClick, _actions.ContextEventData);
            Assert.AreEqual(0, _actions.RenderRequestCount);
        }

        [Test]
        public void EntryHover_ActiveEntry_HighlightsLocationUntilPointerExits()
        {
            RenderOfficerDirectly();
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);

            slot.OnPointerEnter(new PointerEventData(null));

            Assert.AreSame(_officer, _actions.HighlightedTarget);

            slot.OnPointerExit(new PointerEventData(null));

            Assert.IsNull(_actions.HighlightedTarget);
        }

        [Test]
        public void EntryDrag_MovableEntity_RoutesCandidateMovementAndCompletion()
        {
            RenderOfficerDirectly();
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);
            ScrollAreaView scrollArea = _view.GetComponentInChildren<ScrollAreaView>(true);
            PointerEventData eventData = CreatePointerEvent(slot.gameObject);

            slot.OnPointerDown(eventData);
            scrollArea.RelayDrag(eventData);
            scrollArea.RelayDragEnd(eventData);

            Assert.AreSame(_officer, _actions.DraggedTarget);
            Assert.AreEqual(1, _actions.DragMoveCount);
            Assert.AreEqual(1, _actions.DragEndCount);
        }

        [Test]
        public void EntryDrag_Planet_DoesNotStartItemDrag()
        {
            Planet planet = new Planet { InstanceID = "planet", DisplayName = "Planet" };
            _resolvedEntity = planet;
            _view.Render(
                new IdleBarRenderData(
                    true,
                    new List<IdleBarEntry> { new IdleBarEntry(planet, null) },
                    new RectInt(0, 0, 700, 350)
                )
            );
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);

            slot.OnPointerDown(CreatePointerEvent(slot.gameObject));

            Assert.IsNull(_actions.DraggedTarget);
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
                new ContextMenuController(),
                () => null,
                () => false,
                _ => null
            );
            controller.Initialize(_actions);
            controller.BindView(_view);

            controller.Render();

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void Dispose_BoundView_ReleasesViewSubscriptions()
        {
            RenderOfficerDirectly();

            _controller.Dispose();
            _view
                .GetComponentInChildren<IdleBarSlotView>(false)
                .GetComponent<Button>()
                .onClick.Invoke();

            Assert.IsNull(_actions.OpenedTarget);
        }

        [Test]
        public void BindView_DisposedController_ThrowsObjectDisposedException()
        {
            _controller.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _controller.BindView(_view));
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

        private static PointerEventData CreatePointerEvent(GameObject target)
        {
            return new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -1,
                pressPosition = new Vector2(10, 20),
                pointerCurrentRaycast = new RaycastResult { gameObject = target },
                pointerPressRaycast = new RaycastResult { gameObject = target },
            };
        }

        private sealed class TestActions : IIdleBarActions
        {
            public int RenderRequestCount { get; private set; }

            public ISceneNode OpenedTarget { get; private set; }

            public ISceneNode ContextTarget { get; private set; }

            public PointerEventData ContextEventData { get; private set; }

            public ISceneNode HighlightedTarget { get; private set; }

            public ISceneNode DraggedTarget { get; private set; }

            public int DragMoveCount { get; private set; }

            public int DragEndCount { get; private set; }

            public void OpenIdleBarTarget(ISceneNode target)
            {
                OpenedTarget = target;
            }

            public ContextMenuRequest OpenIdleBarContextMenu(
                ISceneNode target,
                PointerEventData eventData
            )
            {
                ContextTarget = target;
                ContextEventData = eventData;
                return null;
            }

            public void RequestIdleBarRender()
            {
                RenderRequestCount++;
            }

            public void SetIdleBarLocationHighlight(ISceneNode target)
            {
                HighlightedTarget = target;
            }

            public bool TryStartIdleBarItemDrag(
                ISceneNode target,
                DragPreview preview,
                PointerEventData eventData
            )
            {
                DraggedTarget = target;
                return true;
            }

            public void MoveIdleBarItemDrag(PointerEventData eventData)
            {
                DragMoveCount++;
            }

            public void EndIdleBarItemDrag(PointerEventData eventData)
            {
                DragEndCount++;
            }

            public void CancelIdleBarItemDrag() { }
        }
    }
}
