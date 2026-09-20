using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.UIState;
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
        private Faction _faction;
        private Officer _officer;
        private UIStateSection _uiState;
        private ISceneNode _resolvedEntity;
        private GameObject _rootObject;
        private IdleBarView _view;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<IdleBarView>(true);
            _faction = new Faction { InstanceID = "faction" };
            _uiState = new UIStateSection { SectionID = "Strategy" };
            _officer = new Officer { InstanceID = "officer", DisplayName = "Officer" };
            _resolvedEntity = _officer;
            _actions = new TestActions();
            _contextMenuController = new ContextMenuController();
            _controller = new IdleBarController(
                () => _faction,
                _uiState.IgnoredItems,
                _contextMenuController,
                () => null,
                () => true,
                instanceId => instanceId == _resolvedEntity?.InstanceID ? _resolvedEntity : null
            );
            _controller.Initialize(_actions);
            _controller.BindView(_view);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
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
                new List<IgnoredItem>(),
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
        public void IgnoreButton_UntracksEntryAndRequestsRender()
        {
            RenderOfficerDirectly();
            IdleBarSlotView slot = _view.GetComponentInChildren<IdleBarSlotView>(false);
            slot.OnPointerEnter(null);

            slot.transform.Find("IgnoreButton").GetComponent<Button>().onClick.Invoke();

            Assert.IsFalse(_controller.IsIdleBarTracked(_officer));
            Assert.AreEqual(1, _actions.RenderRequestCount);
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
        public void ResetSession_PreservesIgnoredState()
        {
            _controller.ToggleIdleBarTracking(_officer);

            _controller.ResetSession(_uiState.IgnoredItems);

            Assert.IsFalse(_controller.IsIdleBarTracked(_officer));
        }

        [Test]
        public void ResetSession_ReplacementState_UsesReplacementExclusions()
        {
            List<IgnoredItem> replacement = new List<IgnoredItem>
            {
                new IgnoredItem { TargetInstanceID = _officer.InstanceID, ItemTypeID = "Entity" },
            };

            _controller.ResetSession(replacement);

            Assert.IsFalse(_controller.IsIdleBarTracked(_officer));
        }

        [Test]
        public void RecreatedController_UsesPlayerUIState()
        {
            _controller.ToggleIdleBarTracking(_officer);
            IdleBarController recreated = new IdleBarController(
                () => _faction,
                _uiState.IgnoredItems,
                new ContextMenuController(),
                () => null,
                () => true,
                _ => null
            );

            Assert.IsFalse(recreated.IsIdleBarTracked(_officer));

            recreated.Dispose();
        }

        [Test]
        public void ToggleTracking_PlanetPersistsEachManufacturingLane()
        {
            Planet planet = new Planet { InstanceID = "planet" };

            _controller.ToggleIdleBarTracking(planet);

            CollectionAssert.AreEquivalent(
                new[] { "Ship", "Troop", "Building" },
                _uiState.IgnoredItems.ConvertAll(item => item.ItemTypeID)
            );
            Assert.IsTrue(
                _uiState.IgnoredItems.All(item => item.TargetInstanceID == planet.InstanceID)
            );
        }

        [Test]
        public void ToggleIdleBarTracking_ManufacturingLane_ChangesOnlySelectedLane()
        {
            Planet planet = new Planet { InstanceID = "planet" };

            _controller.ToggleIdleBarTracking(planet, ManufacturingType.Troop);

            Assert.IsTrue(_controller.IsIdleBarTracked(planet, ManufacturingType.Ship));
            Assert.IsFalse(_controller.IsIdleBarTracked(planet, ManufacturingType.Troop));
            Assert.IsTrue(_controller.IsIdleBarTracked(planet, ManufacturingType.Building));
            Assert.AreEqual(1, _actions.RenderRequestCount);

            _controller.ToggleIdleBarTracking(planet, ManufacturingType.Troop);

            Assert.IsTrue(_controller.IsIdleBarTracked(planet, ManufacturingType.Troop));
            Assert.AreEqual(2, _actions.RenderRequestCount);
        }

        [Test]
        public void Render_DisabledFeature_HidesViewWithoutThemeData()
        {
            IdleBarController controller = new IdleBarController(
                () => null,
                new List<IgnoredItem>(),
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

        /// <summary>
        /// Renders officer directly.
        /// </summary>
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

        /// <summary>
        /// Creates pointer event.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The created pointer event.</returns>
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

            /// <summary>
            /// Opens idle bar target.
            /// </summary>
            /// <param name="target">The target.</param>
            public void OpenIdleBarTarget(ISceneNode target)
            {
                OpenedTarget = target;
            }

            /// <summary>
            /// Opens idle bar context menu.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="eventData">The event data.</param>
            /// <returns>The result of open idle bar context menu.</returns>
            public ContextMenuRequest OpenIdleBarContextMenu(
                ISceneNode target,
                PointerEventData eventData
            )
            {
                ContextTarget = target;
                ContextEventData = eventData;
                return null;
            }

            /// <summary>
            /// Executes request idle bar render.
            /// </summary>
            public void RequestIdleBarRender()
            {
                RenderRequestCount++;
            }

            /// <summary>
            /// Sets idle bar location highlight.
            /// </summary>
            /// <param name="target">The target.</param>
            public void SetIdleBarLocationHighlight(ISceneNode target)
            {
                HighlightedTarget = target;
            }

            /// <summary>
            /// Attempts start idle bar item drag.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="preview">The preview.</param>
            /// <param name="eventData">The event data.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryStartIdleBarItemDrag(
                ISceneNode target,
                DragPreview preview,
                PointerEventData eventData
            )
            {
                DraggedTarget = target;
                return true;
            }

            /// <summary>
            /// Executes move idle bar item drag.
            /// </summary>
            /// <param name="eventData">The event data.</param>
            public void MoveIdleBarItemDrag(PointerEventData eventData)
            {
                DragMoveCount++;
            }

            /// <summary>
            /// Executes end idle bar item drag.
            /// </summary>
            /// <param name="eventData">The event data.</param>
            public void EndIdleBarItemDrag(PointerEventData eventData)
            {
                DragEndCount++;
            }

            /// <summary>
            /// Checks whether the cel idle bar item drag condition is met.
            /// </summary>
            public void CancelIdleBarItemDrag() { }
        }
    }
}
