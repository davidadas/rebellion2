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
        }

        [TearDown]
        public void TearDown()
        {
            if (_texture != null)
                UnityEngine.Object.DestroyImmediate(_texture);
            if (_windowObject != null)
                UnityEngine.Object.DestroyImmediate(_windowObject);
        }

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

        [Test]
        public void TryHandleItemPointerMove_NoCandidate_ReturnsNone()
        {
            StrategyDragController controller = CreateController();

            StrategyDragEventResult result = controller.TryHandleItemPointerMove(10, 20);

            Assert.IsFalse(result.Handled);
        }

        [Test]
        public void TryHandleItemPointerMove_BelowThreshold_ReturnsHandledOnly()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, 10, 20);

            StrategyDragEventResult result = controller.TryHandleItemPointerMove(12, 22);

            Assert.IsTrue(result.Handled);
            Assert.IsFalse(result.RenderOverlay);
            Assert.IsFalse(result.ClearPressedWindow);
        }

        [Test]
        public void TryHandleItemPointerMove_EmptyCandidateCrossesThreshold_ClearsCandidate()
        {
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, 10, 20);

            StrategyDragEventResult started = controller.TryHandleItemPointerMove(13, 24);
            StrategyDragEventResult next = controller.TryHandleItemPointerMove(20, 30);

            Assert.IsTrue(started.Handled);
            Assert.IsFalse(started.RenderOverlay);
            Assert.IsFalse(next.Handled);
        }

        [Test]
        public void TryHandleItemPointerMove_ItemsWithoutPreview_StartsTargeting()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, 10, 20);

            StrategyDragEventResult result = controller.TryHandleItemPointerMove(13, 24);

            Assert.IsTrue(result.Handled);
            Assert.IsTrue(result.ClearPressedWindow);
            Assert.IsFalse(result.RenderOverlay);
            Assert.IsTrue(_targetingController.IsTargeting);
        }

        [Test]
        public void TryHandleItemPointerMove_PreviewCandidate_StartsAndMovesSourceDrag()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasPreview = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, 10, 20);

            StrategyDragEventResult started = controller.TryHandleItemPointerMove(13, 24);
            StrategyDragEventResult moved = controller.TryHandleItemPointerMove(50, 60);
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

        [Test]
        public void TryHandleItemPointerUp_UnresolvedWithoutState_ReturnsNone()
        {
            StrategyDragController controller = CreateController();

            StrategyDragEventResult result = controller.TryHandleItemPointerUp(null);

            Assert.IsFalse(result.Handled);
        }

        [Test]
        public void TryHandleItemPointerUp_UnresolvedWithCandidate_ClearsAndFinishes()
        {
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, 10, 20);

            StrategyDragEventResult result = controller.TryHandleItemPointerUp(null);
            StrategyDragEventResult nextMove = controller.TryHandleItemPointerMove(20, 30);

            Assert.IsTrue(result.Handled);
            Assert.IsTrue(result.SuppressClick);
            Assert.IsTrue(result.Dirty);
            Assert.IsFalse(nextMove.Handled);
        }

        [Test]
        public void TryHandleItemPointerUp_ResolvedCandidateWithoutSourceDrag_ClearsAndReturnsNone()
        {
            _pointerResolved = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, 10, 20);

            StrategyDragEventResult result = controller.TryHandleItemPointerUp(null);
            StrategyDragEventResult nextMove = controller.TryHandleItemPointerMove(20, 30);

            Assert.IsFalse(result.Handled);
            Assert.IsFalse(nextMove.Handled);
        }

        [Test]
        public void TryHandleItemPointerUp_ResolvedSourceDrag_FinishesDrag()
        {
            _pointerResolved = true;
            _contextItems = new ISceneNode[] { new Officer() };
            _hasPreview = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, 10, 20);
            controller.TryHandleItemPointerMove(13, 24);

            StrategyDragEventResult result = controller.TryHandleItemPointerUp(null);

            Assert.IsTrue(result.Handled);
            Assert.IsTrue(result.SuppressClick);
            Assert.IsTrue(result.Dirty);
            Assert.IsFalse(controller.TryGetOverlay(out _, out _));
            Assert.IsFalse(_targetingController.IsTargeting);
        }

        [Test]
        public void ClearWindow_MatchingSource_ClearsDragPresentation()
        {
            _contextItems = new ISceneNode[] { new Officer() };
            _hasPreview = true;
            StrategyDragController controller = CreateController();
            controller.StartItemCandidate(_window, 10, 20);
            controller.TryHandleItemPointerMove(13, 24);

            controller.ClearWindow(_window);

            Assert.IsFalse(controller.TryGetOverlay(out _, out _));
        }

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

        private sealed class RecordingWindowCommands : IStrategyWindowCommandActions
        {
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
                return true;
            }

            public void OpenMoveConfirmWindow(
                UIWindow sourceWindow,
                StrategyMissionTarget target,
                IReadOnlyList<ISceneNode> items
            ) { }
        }
    }
}
