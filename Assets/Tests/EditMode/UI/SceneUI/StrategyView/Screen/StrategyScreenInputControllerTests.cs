using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public class StrategyScreenInputControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private UIWindowManager _windowManager;
        private StrategyContextMenuRouter _contextMenuRouter;
        private GalaxyMapController _galaxyMapController;
        private TargetingController _targetingController;
        private StrategyDragController _dragController;
        private StrategyScreenInputController _controller;
        private UIWindow _window;
        private UIWindow _otherWindow;
        private Texture2D _dragTexture;
        private RecordingTargetingCursor _cursor;
        private RecordingTargetingReceiver _receiver;
        private IReadOnlyList<ISceneNode> _contextItems;
        private bool _resolvePosition;
        private bool _hasDragPreview;
        private bool _selectWindowTarget;
        private bool _openStatus;
        private int _sourceX;
        private int _sourceY;
        private int _selectWindowTargetCount;
        private int _openStatusCount;
        private int _dirtyCount;
        private int _overlayCount;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            StrategyContextMenuPresenter presenter =
                _rootObject.GetComponentInChildren<StrategyContextMenuPresenter>(true);
            _contextMenuRouter = new StrategyContextMenuRouter(
                presenter,
                new ContextMenuController(),
                _windowManager,
                Array.Empty<IStrategyContextMenuProvider>()
            );
            _cursor = new RecordingTargetingCursor();
            _receiver = new RecordingTargetingReceiver();
            _targetingController = new TargetingController(_cursor);
            _galaxyMapController = new GalaxyMapController(() => null);
            _dragTexture = new Texture2D(1, 1);
            _contextItems = Array.Empty<ISceneNode>();
            _resolvePosition = true;
            _hasDragPreview = false;
            _selectWindowTarget = false;
            _openStatus = false;
            _sourceX = 40;
            _sourceY = 50;
            _selectWindowTargetCount = 0;
            _openStatusCount = 0;
            _dirtyCount = 0;
            _overlayCount = 0;
            _dragController = CreateDragController();
            _controller = CreateController();
            _window = CreateRegisteredWindow("Window", 1);
            _otherWindow = CreateRegisteredWindow("OtherWindow", 2);
        }

        [TearDown]
        public void TearDown()
        {
            if (_dragTexture != null)
                UnityEngine.Object.DestroyImmediate(_dragTexture);
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullDependency_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    null,
                    _targetingController,
                    _contextMenuRouter,
                    _windowManager,
                    TrySelectWindowTarget,
                    TryOpenStatus,
                    _dragController,
                    ResolvePosition,
                    MarkDirty,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    null,
                    _contextMenuRouter,
                    _windowManager,
                    TrySelectWindowTarget,
                    TryOpenStatus,
                    _dragController,
                    ResolvePosition,
                    MarkDirty,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    _targetingController,
                    null,
                    _windowManager,
                    TrySelectWindowTarget,
                    TryOpenStatus,
                    _dragController,
                    ResolvePosition,
                    MarkDirty,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    _targetingController,
                    _contextMenuRouter,
                    null,
                    TrySelectWindowTarget,
                    TryOpenStatus,
                    _dragController,
                    ResolvePosition,
                    MarkDirty,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    _targetingController,
                    _contextMenuRouter,
                    _windowManager,
                    null,
                    TryOpenStatus,
                    _dragController,
                    ResolvePosition,
                    MarkDirty,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    _targetingController,
                    _contextMenuRouter,
                    _windowManager,
                    TrySelectWindowTarget,
                    null,
                    _dragController,
                    ResolvePosition,
                    MarkDirty,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    _targetingController,
                    _contextMenuRouter,
                    _windowManager,
                    TrySelectWindowTarget,
                    TryOpenStatus,
                    null,
                    ResolvePosition,
                    MarkDirty,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    _targetingController,
                    _contextMenuRouter,
                    _windowManager,
                    TrySelectWindowTarget,
                    TryOpenStatus,
                    _dragController,
                    null,
                    MarkDirty,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    _targetingController,
                    _contextMenuRouter,
                    _windowManager,
                    TrySelectWindowTarget,
                    TryOpenStatus,
                    _dragController,
                    ResolvePosition,
                    null,
                    RenderOverlay
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyScreenInputController(
                    _galaxyMapController,
                    _targetingController,
                    _contextMenuRouter,
                    _windowManager,
                    TrySelectWindowTarget,
                    TryOpenStatus,
                    _dragController,
                    ResolvePosition,
                    MarkDirty,
                    null
                )
            );
        }

        [Test]
        public void PointerHandlers_NullOrUnresolvedEvent_DoNothing()
        {
            PointerEventData eventData = CreatePointerEvent(_window.gameObject);
            _resolvePosition = false;

            _controller.OnPointerDown(null);
            _controller.OnPointerUp(null);
            _controller.OnPointerMove(null);
            _controller.OnDrag(null);
            _controller.OnPointerClick(null);
            _controller.OnPointerDown(eventData);
            _controller.OnPointerMove(eventData);
            _controller.OnDrag(eventData);
            _controller.OnPointerClick(eventData);

            Assert.AreEqual(0, _dirtyCount);
            Assert.AreEqual(0, _overlayCount);
            Assert.AreSame(_otherWindow, _windowManager.ActiveWindow);
        }

        [Test]
        public void OnPointerDown_LeftWindow_FocusesWindowAndMarksDirty()
        {
            PointerEventData eventData = CreatePointerEvent(_window.gameObject);

            _controller.OnPointerDown(eventData);

            Assert.AreSame(_window, _windowManager.ActiveWindow);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void OnPointerDown_RightWindow_OpensContextMenuAndMarksDirty()
        {
            PointerEventData eventData = CreatePointerEvent(
                _window.gameObject,
                PointerEventData.InputButton.Right
            );

            _controller.OnPointerDown(eventData);

            Assert.IsTrue(_contextMenuRouter.IsOpen);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void OnPointerDown_ActiveTargeting_MovesCursorAndSuppressesNextClick()
        {
            BeginTargeting();
            PointerEventData eventData = CreateStatusDoubleClickEvent(_window);

            _controller.OnPointerDown(eventData);
            _controller.OnPointerClick(eventData);

            Assert.AreEqual(_sourceX, _cursor.LastX);
            Assert.AreEqual(_sourceY, _cursor.LastY);
            Assert.AreEqual(1, _cursor.MoveCount);
            Assert.AreEqual(0, _openStatusCount);
        }

        [Test]
        public void OnPointerUp_TargetingWindowAccepted_MarksDirtyAndSuppressesClick()
        {
            BeginTargeting();
            _selectWindowTarget = true;
            PointerEventData eventData = CreateStatusDoubleClickEvent(_window);

            _controller.OnPointerUp(eventData);
            _controller.OnPointerClick(eventData);

            Assert.AreEqual(1, _selectWindowTargetCount);
            Assert.AreEqual(1, _dirtyCount);
            Assert.AreEqual(0, _openStatusCount);
            Assert.AreEqual(0, _receiver.CancelledCount);
        }

        [Test]
        public void OnPointerUp_TargetingWithoutTarget_CancelsAndMarksDirty()
        {
            BeginTargeting();
            PointerEventData eventData = CreatePointerEvent(null);

            _controller.OnPointerUp(eventData);

            Assert.IsFalse(_targetingController.IsTargeting);
            Assert.AreEqual(1, _receiver.CancelledCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void OnPointerUp_TargetingRightButton_MovesCursorWithoutCancelling()
        {
            BeginTargeting();
            PointerEventData eventData = CreatePointerEvent(
                _window.gameObject,
                PointerEventData.InputButton.Right
            );

            _controller.OnPointerUp(eventData);

            Assert.IsTrue(_targetingController.IsTargeting);
            Assert.AreEqual(1, _cursor.MoveCount);
            Assert.AreEqual(0, _receiver.CancelledCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void OnPointerUp_RightButtonWithoutTargeting_DoesNotMarkDirty()
        {
            PointerEventData eventData = CreatePointerEvent(
                _window.gameObject,
                PointerEventData.InputButton.Right
            );

            _controller.OnPointerUp(eventData);

            Assert.AreEqual(0, _dirtyCount);
        }

        [Test]
        public void CancelTargeting_ActiveThenInactive_ReturnsMatchingState()
        {
            BeginTargeting();

            bool first = _controller.CancelTargeting();
            bool second = _controller.TryCancel();

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(1, _receiver.CancelledCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void OnPointerMove_ActiveTargeting_MovesCursor()
        {
            BeginTargeting();
            PointerEventData eventData = CreatePointerEvent(null);

            _controller.OnPointerMove(eventData);

            Assert.AreEqual(1, _cursor.MoveCount);
            Assert.AreEqual(_sourceX, _cursor.LastX);
            Assert.AreEqual(_sourceY, _cursor.LastY);
        }

        [Test]
        public void OnDrag_ItemCandidateStartsPreview_RendersOverlayAndSuppressesClick()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasDragPreview = true;
            _controller.StartItemDrag(_window, 10, 20);
            PointerEventData eventData = CreateStatusDoubleClickEvent(_window);

            _controller.OnDrag(eventData);
            _controller.OnPointerClick(eventData);

            Assert.AreEqual(1, _overlayCount);
            Assert.AreEqual(0, _openStatusCount);
        }

        [Test]
        public void OnPointerUp_UnresolvedItemCandidate_ClearsDragAndMarksDirty()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _controller.StartItemDrag(_window, 10, 20);
            _resolvePosition = false;

            _controller.OnPointerUp(CreatePointerEvent(_window.gameObject));

            Assert.AreEqual(1, _dirtyCount);
            Assert.IsFalse(_dragController.TryGetOverlay(out _, out _));
        }

        [Test]
        public void OnPointerClick_StatusDoubleClick_OpensStatusAndMarksDirty()
        {
            _openStatus = true;
            PointerEventData eventData = CreateStatusDoubleClickEvent(_window);

            _controller.OnPointerClick(eventData);

            Assert.AreEqual(1, _openStatusCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void OnPointerClick_NonactivatingClicks_DoNotOpenStatus()
        {
            _openStatus = true;

            _controller.OnPointerClick(
                CreatePointerEvent(_window.gameObject, PointerEventData.InputButton.Left, 1)
            );
            _controller.OnPointerClick(
                CreatePointerEvent(_window.gameObject, PointerEventData.InputButton.Right, 2)
            );

            Assert.AreEqual(0, _openStatusCount);
            Assert.AreEqual(0, _dirtyCount);
        }

        [Test]
        public void OnPointerClick_UnmarkedWindowDoubleClick_MarksWindowDirtyWithoutOpeningStatus()
        {
            _openStatus = true;
            PointerEventData eventData = CreatePointerEvent(
                _window.gameObject,
                PointerEventData.InputButton.Left,
                2
            );

            _controller.OnPointerClick(eventData);

            Assert.AreEqual(0, _openStatusCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void SuppressNextClick_FirstStatusDoubleClickIgnoredAndSecondHandled()
        {
            _openStatus = true;
            PointerEventData eventData = CreateStatusDoubleClickEvent(_window);
            _controller.SuppressNextClick();

            _controller.OnPointerClick(eventData);
            _controller.OnPointerClick(eventData);

            Assert.AreEqual(1, _openStatusCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void StartItemDrag_NullWindow_DoesNotCreateCandidate()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasDragPreview = true;

            _controller.StartItemDrag(null, 10, 20);
            _controller.OnDrag(CreatePointerEvent(null));

            Assert.AreEqual(0, _overlayCount);
            Assert.IsFalse(_dragController.TryGetOverlay(out _, out _));
        }

        private StrategyScreenInputController CreateController()
        {
            return new StrategyScreenInputController(
                _galaxyMapController,
                _targetingController,
                _contextMenuRouter,
                _windowManager,
                TrySelectWindowTarget,
                TryOpenStatus,
                _dragController,
                ResolvePosition,
                MarkDirty,
                RenderOverlay
            );
        }

        private StrategyDragController CreateDragController()
        {
            return new StrategyDragController(
                _targetingController,
                _ => _contextItems,
                ResolveDragPreview,
                ResolvePosition,
                _ => null,
                () => "player",
                new WindowCommandActions(),
                5
            );
        }

        private UIWindow CreateRegisteredWindow(string name, int id)
        {
            GameObject windowObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIWindow)
            );
            windowObject.transform.SetParent(_windowManager.transform, false);
            UIWindow window = windowObject.GetComponent<UIWindow>();
            window.Configure(id, 0, 0, 100, 100, false, true, true);
            _windowManager.Register(window, false);
            return window;
        }

        private PointerEventData CreateStatusDoubleClickEvent(UIWindow window)
        {
            GameObject target = new GameObject("StatusTarget", typeof(RectTransform));
            target.transform.SetParent(window.transform, false);
            target.AddComponent<StatusDoubleClickTarget>();
            return CreatePointerEvent(target, PointerEventData.InputButton.Left, 2);
        }

        private static PointerEventData CreatePointerEvent(
            GameObject target,
            PointerEventData.InputButton button = PointerEventData.InputButton.Left,
            int clickCount = 1
        )
        {
            return new PointerEventData(null)
            {
                button = button,
                clickCount = clickCount,
                pointerCurrentRaycast = new RaycastResult { gameObject = target },
            };
        }

        private void BeginTargeting()
        {
            _targetingController.Begin(new TargetingRequest("Target", this, _receiver), 1, 2);
        }

        private bool ResolvePosition(
            PointerEventData eventData,
            Vector2 screenPosition,
            out int x,
            out int y
        )
        {
            x = _sourceX;
            y = _sourceY;
            return _resolvePosition;
        }

        private bool ResolveDragPreview(
            UIWindow window,
            int sourceX,
            int sourceY,
            out DragPreview preview
        )
        {
            preview = new DragPreview(_dragTexture, 20, 30, 2, 3);
            return _hasDragPreview;
        }

        private bool TrySelectWindowTarget(UIWindow window)
        {
            _selectWindowTargetCount++;
            return _selectWindowTarget;
        }

        private bool TryOpenStatus(UIWindow window)
        {
            _openStatusCount++;
            return _openStatus;
        }

        private void MarkDirty()
        {
            _dirtyCount++;
        }

        private void RenderOverlay()
        {
            _overlayCount++;
        }

        private sealed class RecordingTargetingCursor : ITargetingCursor
        {
            public int MoveCount { get; private set; }
            public int LastX { get; private set; }
            public int LastY { get; private set; }

            public void Show(int x, int y) { }

            public void MoveTo(int x, int y)
            {
                MoveCount++;
                LastX = x;
                LastY = y;
            }

            public void Hide() { }
        }

        private sealed class RecordingTargetingReceiver : ITargetingReceiver
        {
            public int CancelledCount { get; private set; }

            public void OnTargetSelected(TargetingRequest request, object target) { }

            public void OnTargetingCancelled(TargetingRequest request)
            {
                CancelledCount++;
            }
        }

        private sealed class WindowCommandActions : IStrategyWindowCommandActions
        {
            public void ExecuteTargetedCommand(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            ) { }

            public void OpenMissionCreateWindow(
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            ) { }

            public bool TryExecuteMove(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                return false;
            }

            public void OpenMoveConfirmWindow(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            ) { }
        }

        private sealed class StatusDoubleClickTarget
            : MonoBehaviour,
                IStrategyStatusDoubleClickTarget { }
    }
}
