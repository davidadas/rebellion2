using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Windows
{
    [TestFixture]
    public class StrategyDragControllerTests
    {
        private GameObject _windowObject;
        private UIWindow _window;
        private Texture2D _texture;
        private TargetingController _targetingController;
        private RecordingWindowCommands _commands;
        private IReadOnlyList<ISceneNode> _contextItems;
        private bool _hasPreview;
        private bool _pointerResolved;
        private int _pointerX;
        private int _pointerY;
        private PointerEventData _pointerEvent;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _texture = new Texture2D(1, 1);
            _targetingController = new TargetingController();
            _commands = new RecordingWindowCommands();
            _contextItems = Array.Empty<ISceneNode>();
            _hasPreview = false;
            _pointerResolved = false;
            _pointerX = 50;
            _pointerY = 60;
            _pointerEvent = CreatePointerEvent(_window.gameObject);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_texture != null)
                UnityEngine.Object.DestroyImmediate(_texture);
            if (_windowObject != null)
                UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        /// <summary>
        /// Verifies constructor null resolver or negative threshold throws.
        /// </summary>
        [Test]
        public void Constructor_NullResolverOrNegativeThreshold_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyDragController(
                    _targetingController,
                    _ => _contextItems,
                    ResolvePreview,
                    null,
                    _ => null,
                    () => "player",
                    _commands,
                    5
                )
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new StrategyDragController(
                    _targetingController,
                    _ => _contextItems,
                    ResolvePreview,
                    ResolvePointer,
                    _ => null,
                    () => "player",
                    _commands,
                    -1
                )
            );
        }

        /// <summary>
        /// Verifies event results static factories expose expected effects.
        /// </summary>
        [Test]
        public void EventResults_StaticFactories_ExposeExpectedEffects()
        {
            StrategyDragEventResult none = StrategyDragEventResult.None;
            StrategyDragEventResult handled = StrategyDragEventResult.HandledOnly;
            StrategyDragEventResult visible = StrategyDragEventResult.SourceDragVisible;
            StrategyDragEventResult targeting = StrategyDragEventResult.TargetingStarted;
            StrategyDragEventResult started = StrategyDragEventResult.SourceDragStarted;
            StrategyDragEventResult finished = StrategyDragEventResult.ItemDragFinished;

            Assert.IsFalse(none.Handled);
            Assert.IsTrue(handled.Handled);
            Assert.IsFalse(handled.RenderOverlay);
            Assert.IsTrue(visible.Handled);
            Assert.IsTrue(visible.RenderOverlay);
            Assert.IsTrue(visible.SuppressClick);
            Assert.IsTrue(targeting.Handled);
            Assert.IsTrue(targeting.ClearPressedWindow);
            Assert.IsFalse(targeting.SuppressClick);
            Assert.IsTrue(started.RenderOverlay);
            Assert.IsTrue(started.SuppressClick);
            Assert.IsTrue(started.ClearPressedWindow);
            Assert.IsTrue(finished.Handled);
            Assert.IsTrue(finished.SuppressClick);
            Assert.IsTrue(finished.ClearPressedWindow);
            Assert.IsTrue(finished.Dirty);
        }

        /// <summary>
        /// Verifies try handle item pointer move no candidate returns none.
        /// </summary>
        [Test]
        public void TryHandleItemPointerMove_NoCandidate_ReturnsNone()
        {
            StrategyDragController controller = CreateController();

            StrategyDragEventResult result = controller.TryHandleItemPointerMove(null, 10, 20);

            Assert.IsFalse(result.Handled);
        }

        /// <summary>
        /// Verifies try handle item pointer move below threshold returns handled only.
        /// </summary>
        [Test]
        public void TryHandleItemPointerMove_BelowThreshold_ReturnsHandledOnly()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);

            StrategyDragEventResult result = controller.TryHandleItemPointerMove(
                _pointerEvent,
                12,
                22
            );

            Assert.IsTrue(result.Handled);
            Assert.IsFalse(result.RenderOverlay);
            Assert.IsFalse(result.ClearPressedWindow);
        }

        /// <summary>
        /// Verifies try handle item pointer move empty candidate crosses threshold clears candidate.
        /// </summary>
        [Test]
        public void TryHandleItemPointerMove_EmptyCandidateCrossesThreshold_ClearsCandidate()
        {
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);

            StrategyDragEventResult started = controller.TryHandleItemPointerMove(
                _pointerEvent,
                13,
                24
            );
            StrategyDragEventResult next = controller.TryHandleItemPointerMove(
                _pointerEvent,
                20,
                30
            );

            Assert.IsTrue(started.Handled);
            Assert.IsFalse(started.RenderOverlay);
            Assert.IsFalse(next.Handled);
        }

        /// <summary>
        /// Verifies try handle item pointer move items without preview starts targeting.
        /// </summary>
        [Test]
        public void TryHandleItemPointerMove_ItemsWithoutPreview_StartsTargeting()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);

            StrategyDragEventResult result = controller.TryHandleItemPointerMove(
                _pointerEvent,
                13,
                24
            );

            Assert.IsTrue(result.Handled);
            Assert.IsTrue(result.ClearPressedWindow);
            Assert.IsFalse(result.RenderOverlay);
            Assert.IsTrue(_targetingController.IsTargeting);
        }

        /// <summary>
        /// Verifies try handle item pointer move preview candidate starts and moves source drag.
        /// </summary>
        [Test]
        public void TryHandleItemPointerMove_PreviewCandidate_StartsAndMovesSourceDrag()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasPreview = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);

            StrategyDragEventResult started = controller.TryHandleItemPointerMove(
                _pointerEvent,
                13,
                24
            );
            StrategyDragEventResult moved = controller.TryHandleItemPointerMove(
                _pointerEvent,
                50,
                60
            );
            bool hasOverlay = controller.TryGetOverlay(out Texture texture, out RectInt bounds);

            Assert.IsTrue(started.Handled);
            Assert.IsTrue(started.RenderOverlay);
            Assert.IsTrue(started.ClearPressedWindow);
            Assert.IsTrue(moved.Handled);
            Assert.IsTrue(moved.RenderOverlay);
            Assert.IsTrue(moved.SuppressClick);
            Assert.IsTrue(hasOverlay);
            Assert.AreSame(_texture, texture);
            Assert.AreEqual(new RectInt(48, 57, 20, 30), bounds);
        }

        /// <summary>
        /// Verifies try start item candidate direct entity uses shared drag flow.
        /// </summary>
        [Test]
        public void TryStartItemCandidate_DirectEntity_UsesSharedDragFlow()
        {
            Officer officer = new Officer();
            DragPreview preview = new DragPreview(_texture, 20, 30, 2, 3);
            StrategyDragController controller = CreateController();

            bool accepted = controller.TryStartItemCandidate(
                officer,
                preview,
                _pointerEvent,
                10,
                20
            );
            StrategyDragEventResult result = controller.TryHandleItemPointerMove(
                _pointerEvent,
                13,
                24
            );

            Assert.IsTrue(accepted);
            Assert.IsTrue(result.Handled);
            Assert.IsTrue(result.RenderOverlay);
            Assert.IsTrue(controller.TryGetOverlay(out Texture texture, out _));
            Assert.AreSame(_texture, texture);
        }

        /// <summary>
        /// Verifies try cancel direct item interaction direct candidate clears only direct state.
        /// </summary>
        [Test]
        public void TryCancelDirectItemInteraction_DirectCandidate_ClearsOnlyDirectState()
        {
            StrategyDragController controller = CreateController();
            controller.TryStartItemCandidate(
                new Officer(),
                new DragPreview(_texture, 20, 30, 2, 3),
                _pointerEvent,
                10,
                20
            );

            bool cancelled = controller.TryCancelDirectItemInteraction();
            StrategyDragEventResult nextMove = controller.TryHandleItemPointerMove(
                _pointerEvent,
                13,
                24
            );

            Assert.IsTrue(cancelled);
            Assert.IsFalse(controller.HasDirectItemInteraction);
            Assert.IsFalse(nextMove.Handled);
        }

        /// <summary>
        /// Verifies try cancel direct item interaction window candidate preserves window state.
        /// </summary>
        [Test]
        public void TryCancelDirectItemInteraction_WindowCandidate_PreservesWindowState()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);

            bool cancelled = controller.TryCancelDirectItemInteraction();
            StrategyDragEventResult nextMove = controller.TryHandleItemPointerMove(
                _pointerEvent,
                12,
                22
            );

            Assert.IsFalse(cancelled);
            Assert.IsFalse(controller.HasDirectItemInteraction);
            Assert.IsTrue(nextMove.Handled);
        }

        /// <summary>
        /// Verifies try cancel direct item interaction direct targeting cancels targeting.
        /// </summary>
        [Test]
        public void TryCancelDirectItemInteraction_DirectTargeting_CancelsTargeting()
        {
            StrategyDragController controller = CreateController();
            controller.TryStartItemCandidate(new Officer(), null, _pointerEvent, 10, 20);
            controller.TryHandleItemPointerMove(_pointerEvent, 13, 24);

            bool cancelled = controller.TryCancelDirectItemInteraction();

            Assert.IsTrue(cancelled);
            Assert.IsFalse(controller.HasDirectItemInteraction);
            Assert.IsFalse(_targetingController.IsTargeting);
        }

        /// <summary>
        /// Verifies try handle item pointer move different press clears candidate without dragging.
        /// </summary>
        [Test]
        public void TryHandleItemPointerMove_DifferentPress_ClearsCandidateWithoutDragging()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasPreview = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);
            GameObject otherTarget = new GameObject("OtherPress", typeof(RectTransform));
            otherTarget.transform.SetParent(_window.transform, false);
            PointerEventData otherPress = CreatePointerEvent(otherTarget);

            StrategyDragEventResult result = controller.TryHandleItemPointerMove(
                otherPress,
                50,
                60
            );
            StrategyDragEventResult stalePressRetry = controller.TryHandleItemPointerMove(
                _pointerEvent,
                50,
                60
            );

            Assert.IsFalse(result.Handled);
            Assert.IsFalse(stalePressRetry.Handled);
            Assert.IsFalse(controller.TryGetOverlay(out _, out _));
            Assert.IsFalse(_targetingController.IsTargeting);
        }

        /// <summary>
        /// Verifies try handle item pointer up unresolved without state returns none.
        /// </summary>
        [Test]
        public void TryHandleItemPointerUp_UnresolvedWithoutState_ReturnsNone()
        {
            StrategyDragController controller = CreateController();

            StrategyDragEventResult result = controller.TryHandleItemPointerUp(null);

            Assert.IsFalse(result.Handled);
        }

        /// <summary>
        /// Verifies try handle item pointer up unresolved with candidate clears and finishes.
        /// </summary>
        [Test]
        public void TryHandleItemPointerUp_UnresolvedWithCandidate_ClearsAndFinishes()
        {
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);

            StrategyDragEventResult result = controller.TryHandleItemPointerUp(_pointerEvent);
            StrategyDragEventResult nextMove = controller.TryHandleItemPointerMove(
                _pointerEvent,
                20,
                30
            );

            Assert.IsTrue(result.Handled);
            Assert.IsTrue(result.SuppressClick);
            Assert.IsTrue(result.Dirty);
            Assert.IsFalse(nextMove.Handled);
        }

        /// <summary>
        /// Verifies try handle item pointer up resolved candidate without source drag clears and returns none.
        /// </summary>
        [Test]
        public void TryHandleItemPointerUp_ResolvedCandidateWithoutSourceDrag_ClearsAndReturnsNone()
        {
            _pointerResolved = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);

            StrategyDragEventResult result = controller.TryHandleItemPointerUp(_pointerEvent);
            StrategyDragEventResult nextMove = controller.TryHandleItemPointerMove(
                _pointerEvent,
                20,
                30
            );

            Assert.IsFalse(result.Handled);
            Assert.IsFalse(nextMove.Handled);
        }

        /// <summary>
        /// Verifies try handle item pointer up resolved source drag finishes drag.
        /// </summary>
        [Test]
        public void TryHandleItemPointerUp_ResolvedSourceDrag_FinishesDrag()
        {
            _pointerResolved = true;
            _contextItems = new ISceneNode[] { new Officer() };
            _hasPreview = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);
            controller.TryHandleItemPointerMove(_pointerEvent, 13, 24);

            StrategyDragEventResult result = controller.TryHandleItemPointerUp(_pointerEvent);

            Assert.IsTrue(result.Handled);
            Assert.IsTrue(result.SuppressClick);
            Assert.IsTrue(result.Dirty);
            Assert.IsFalse(controller.TryGetOverlay(out _, out _));
            Assert.IsFalse(_targetingController.IsTargeting);
        }

        /// <summary>
        /// Verifies clear window matching source clears drag presentation.
        /// </summary>
        [Test]
        public void ClearWindow_MatchingSource_ClearsDragPresentation()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasPreview = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, _pointerEvent, 10, 20);
            controller.TryHandleItemPointerMove(_pointerEvent, 13, 24);

            controller.ClearWindow(_window);

            Assert.IsFalse(controller.TryGetOverlay(out _, out _));
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
        private StrategyDragController CreateController()
        {
            return new StrategyDragController(
                _targetingController,
                _ => _contextItems,
                ResolvePreview,
                ResolvePointer,
                _ => null,
                () => "player",
                _commands,
                5
            );
        }

        /// <summary>
        /// Resolves preview.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <param name="sourceX">The source x.</param>
        /// <param name="sourceY">The source y.</param>
        /// <param name="preview">Receives the preview.</param>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
        private bool ResolvePreview(
            UIWindow window,
            int sourceX,
            int sourceY,
            out DragPreview preview
        )
        {
            preview = new DragPreview(_texture, 20, 30, 2, 3);
            return _hasPreview;
        }

        /// <summary>
        /// Resolves pointer.
        /// </summary>
        /// <param name="eventData">The event data.</param>
        /// <param name="screenPosition">The screen position.</param>
        /// <param name="x">Receives the x.</param>
        /// <param name="y">Receives the y.</param>
        /// <returns>True when the operation succeeds; otherwise false.</returns>
        private bool ResolvePointer(
            PointerEventData eventData,
            Vector2 screenPosition,
            out int x,
            out int y
        )
        {
            x = _pointerX;
            y = _pointerY;
            return _pointerResolved;
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

        private sealed class RecordingWindowCommands : IStrategyWindowCommandActions
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
                return true;
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
    }
}
