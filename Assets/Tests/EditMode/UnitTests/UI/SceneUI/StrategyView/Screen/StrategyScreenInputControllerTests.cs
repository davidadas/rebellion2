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

        /// <summary>
        /// Sets up.
        /// </summary>
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

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_dragTexture != null)
                UnityEngine.Object.DestroyImmediate(_dragTexture);
            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        /// <summary>
        /// Verifies constructor null dependency throws argument null exception.
        /// </summary>
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

        /// <summary>
        /// Verifies pointer handlers null or unresolved event do nothing.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer down left window focuses window and marks dirty.
        /// </summary>
        [Test]
        public void OnPointerDown_LeftWindow_FocusesWindowAndMarksDirty()
        {
            PointerEventData eventData = CreatePointerEvent(_window.gameObject);

            _controller.OnPointerDown(eventData);

            Assert.AreSame(_window, _windowManager.ActiveWindow);
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies on pointer down right window opens context menu and marks dirty.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer down active targeting moves cursor and suppresses next click.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer up targeting window accepted marks dirty and suppresses click.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer up targeting without target cancels and marks dirty.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer up targeting right button moves cursor without cancelling.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer up right button without targeting does not mark dirty.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer up unresolved item candidate clears drag and marks dirty.
        /// </summary>
        [Test]
        public void OnPointerUp_UnresolvedItemCandidate_ClearsDragAndMarksDirty()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            PointerEventData eventData = CreatePointerEvent(_window.gameObject);
            _controller.StartItemDrag(_window, eventData);
            _resolvePosition = false;

            _controller.OnPointerUp(eventData);

            Assert.AreEqual(1, _dirtyCount);
            Assert.IsFalse(_dragController.TryGetOverlay(out _, out _));
        }

        /// <summary>
        /// Verifies cancel targeting active then inactive returns matching state.
        /// </summary>
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

        /// <summary>
        /// Verifies try cancel active item drag clears targeting and overlay.
        /// </summary>
        [Test]
        public void TryCancel_ActiveItemDrag_ClearsTargetingAndOverlay()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasDragPreview = true;
            PointerEventData eventData = CreatePointerEvent(_window.gameObject);
            _sourceX = 10;
            _sourceY = 20;
            _controller.StartItemDrag(_window, eventData);
            _sourceX = 40;
            _sourceY = 50;
            _controller.OnDrag(eventData);

            bool cancelled = _controller.TryCancel();

            Assert.IsTrue(cancelled);
            Assert.IsFalse(_targetingController.IsTargeting);
            Assert.IsFalse(_dragController.TryGetOverlay(out _, out _));
            Assert.AreEqual(2, _overlayCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies on pointer move active targeting moves cursor.
        /// </summary>
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

        /// <summary>
        /// Verifies on drag item candidate starts preview renders overlay and suppresses click.
        /// </summary>
        [Test]
        public void OnDrag_ItemCandidateStartsPreview_RendersOverlayAndSuppressesClick()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasDragPreview = true;
            PointerEventData eventData = CreateStatusDoubleClickEvent(_window);
            _sourceX = 10;
            _sourceY = 20;
            _controller.StartItemDrag(_window, eventData);
            _sourceX = 40;
            _sourceY = 50;

            _controller.OnDrag(eventData);
            _controller.OnPointerClick(eventData);

            Assert.AreEqual(1, _overlayCount);
            Assert.AreEqual(0, _openStatusCount);
        }

        /// <summary>
        /// Verifies on pointer click status double click opens status and marks dirty.
        /// </summary>
        [Test]
        public void OnPointerClick_StatusDoubleClick_OpensStatusAndMarksDirty()
        {
            _openStatus = true;
            PointerEventData eventData = CreateStatusDoubleClickEvent(_window);

            _controller.OnPointerClick(eventData);

            Assert.AreEqual(1, _openStatusCount);
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies on pointer click nonactivating clicks do not open status.
        /// </summary>
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

        /// <summary>
        /// Verifies on pointer click unmarked window double click marks window dirty without opening status.
        /// </summary>
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

        /// <summary>
        /// Verifies suppress next click first status double click ignored and second handled.
        /// </summary>
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

        /// <summary>
        /// Verifies start item drag null window does not create candidate.
        /// </summary>
        [Test]
        public void StartItemDrag_NullWindow_DoesNotCreateCandidate()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasDragPreview = true;
            PointerEventData eventData = CreatePointerEvent(null);

            _controller.StartItemDrag(null, eventData);
            _controller.OnDrag(eventData);

            Assert.AreEqual(0, _overlayCount);
            Assert.IsFalse(_dragController.TryGetOverlay(out _, out _));
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
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

        /// <summary>
        /// Creates drag controller.
        /// </summary>
        /// <returns>The created drag controller.</returns>
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

        /// <summary>
        /// Creates registered window.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="id">The id.</param>
        /// <returns>The created registered window.</returns>
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

        /// <summary>
        /// Creates status double click event.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <returns>The created status double click event.</returns>
        private PointerEventData CreateStatusDoubleClickEvent(UIWindow window)
        {
            GameObject target = new GameObject("StatusTarget", typeof(RectTransform));
            target.transform.SetParent(window.transform, false);
            target.AddComponent<StatusDoubleClickTarget>();
            return CreatePointerEvent(target, PointerEventData.InputButton.Left, 2);
        }

        /// <summary>
        /// Creates pointer event.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="button">The button.</param>
        /// <param name="clickCount">The click count.</param>
        /// <returns>The created pointer event.</returns>
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
                pointerId = -1,
                pressPosition = new Vector2(10, 20),
                pointerCurrentRaycast = new RaycastResult { gameObject = target },
                pointerPressRaycast = new RaycastResult { gameObject = target },
            };
        }

        /// <summary>
        /// Executes begin targeting.
        /// </summary>
        private void BeginTargeting()
        {
            _targetingController.Begin(new TargetingRequest("Target", this, _receiver), 1, 2);
        }

        /// <summary>
        /// Resolves position.
        /// </summary>
        /// <param name="eventData">The event data.</param>
        /// <param name="screenPosition">The screen position.</param>
        /// <param name="x">Receives the x.</param>
        /// <param name="y">Receives the y.</param>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
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

        /// <summary>
        /// Resolves drag preview.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <param name="sourceX">The source x.</param>
        /// <param name="sourceY">The source y.</param>
        /// <param name="preview">Receives the preview.</param>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
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

        /// <summary>
        /// Attempts select window target.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
        private bool TrySelectWindowTarget(UIWindow window)
        {
            _selectWindowTargetCount++;
            return _selectWindowTarget;
        }

        /// <summary>
        /// Attempts open status.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
        private bool TryOpenStatus(UIWindow window)
        {
            _openStatusCount++;
            return _openStatus;
        }

        /// <summary>
        /// Executes mark dirty.
        /// </summary>
        private void MarkDirty()
        {
            _dirtyCount++;
        }

        /// <summary>
        /// Renders overlay.
        /// </summary>
        private void RenderOverlay()
        {
            _overlayCount++;
        }

        private sealed class RecordingTargetingCursor : ITargetingCursor
        {
            public int MoveCount { get; private set; }
            public int LastX { get; private set; }
            public int LastY { get; private set; }

            /// <summary>
            /// Shows the requested operation.
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            public void Show(int x, int y) { }

            /// <summary>
            /// Executes move to.
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            public void MoveTo(int x, int y)
            {
                MoveCount++;
                LastX = x;
                LastY = y;
            }

            /// <summary>
            /// Hides the requested operation.
            /// </summary>
            public void Hide() { }
        }

        private sealed class RecordingTargetingReceiver : ITargetingReceiver
        {
            public int CancelledCount { get; private set; }

            /// <summary>
            /// Executes on target selected.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="target">The target.</param>
            public void OnTargetSelected(TargetingRequest request, object target) { }

            /// <summary>
            /// Executes on targeting cancelled.
            /// </summary>
            /// <param name="request">The request.</param>
            public void OnTargetingCancelled(TargetingRequest request)
            {
                CancelledCount++;
            }
        }

        private sealed class WindowCommandActions : IStrategyWindowCommandActions
        {
            /// <summary>
            /// Executes targeted command.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <param name="target">The target.</param>
            public void ExecuteTargetedCommand(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            ) { }

            /// <summary>
            /// Opens mission create window.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="items">The items.</param>
            public void OpenMissionCreateWindow(
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            ) { }

            /// <summary>
            /// Attempts execute move.
            /// </summary>
            /// <param name="sourceWindow">The source window.</param>
            /// <param name="target">The target.</param>
            /// <param name="items">The items.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryExecuteMove(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            )
            {
                return false;
            }

            /// <summary>
            /// Opens move confirm window.
            /// </summary>
            /// <param name="sourceWindow">The source window.</param>
            /// <param name="target">The target.</param>
            /// <param name="items">The items.</param>
            public void OpenMoveConfirmWindow(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            ) { }

            /// <summary>
            /// Attempts append fleet waypoint.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <param name="target">The target.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryAppendFleetWaypoint(
                StrategyWindowTargetingSource source,
                StrategyMissionTarget target
            ) => false;

            /// <summary>
            /// Attempts commit fleet waypoint plan.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryCommitFleetWaypointPlan(StrategyWindowTargetingSource source) => false;

            /// <summary>
            /// Attempts undo fleet waypoint plan.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool TryUndoFleetWaypointPlan(StrategyWindowTargetingSource source) => false;

            /// <summary>
            /// Executes clear fleet waypoints.
            /// </summary>
            /// <param name="items">The items.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool ClearFleetWaypoints(IReadOnlyList<ISceneNode> items) => false;
        }

        private sealed class StatusDoubleClickTarget
            : MonoBehaviour,
                IStrategyStatusDoubleClickTarget { }
    }
}
