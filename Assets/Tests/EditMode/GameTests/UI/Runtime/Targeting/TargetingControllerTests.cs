using System;
using NUnit.Framework;

namespace Rebellion.Tests.UI.Runtime.Targeting
{
    [TestFixture]
    public class TargetingControllerTests
    {
        [Test]
        public void Request_NullReceiver_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TargetingRequest("Prompt", null, null));
        }

        [Test]
        public void Request_NullPrompt_StoresNormalizedState()
        {
            object source = new object();
            RecordingReceiver receiver = new RecordingReceiver();

            TargetingRequest request = new TargetingRequest(null, source, receiver);

            Assert.AreEqual(string.Empty, request.Prompt);
            Assert.AreSame(source, request.Source);
            Assert.AreSame(receiver, request.Receiver);
        }

        [Test]
        public void Begin_NullRequest_ThrowsArgumentNullException()
        {
            TargetingController controller = new TargetingController();

            Assert.Throws<ArgumentNullException>(() => controller.Begin(null));
        }

        [Test]
        public void Begin_ExistingRequest_CancelsExistingAndActivatesReplacement()
        {
            RecordingReceiver firstReceiver = new RecordingReceiver();
            RecordingReceiver secondReceiver = new RecordingReceiver();
            TargetingRequest first = new TargetingRequest("First", null, firstReceiver);
            TargetingRequest second = new TargetingRequest("Second", null, secondReceiver);
            TargetingController controller = new TargetingController();
            controller.Begin(first);

            controller.Begin(second);

            Assert.AreEqual(1, firstReceiver.CanceledCount);
            Assert.AreSame(first, firstReceiver.LastRequest);
            Assert.AreEqual(0, secondReceiver.CanceledCount);
            Assert.IsTrue(controller.IsTargeting);
            Assert.AreSame(second, controller.ActiveRequest);
        }

        [Test]
        public void Begin_WithCursor_ShowsAndMovesCursorWhileActive()
        {
            RecordingCursor cursor = new RecordingCursor();
            TargetingController controller = new TargetingController(cursor);
            TargetingRequest request = new TargetingRequest(
                "Target",
                null,
                new RecordingReceiver()
            );

            controller.Begin(request, 12, 34);
            controller.MoveCursor(56, 78);

            Assert.AreEqual(1, cursor.ShowCount);
            Assert.AreEqual(12, cursor.LastX);
            Assert.AreEqual(34, cursor.LastY);
            Assert.AreEqual(1, cursor.MoveCount);
            Assert.AreEqual(56, cursor.MoveX);
            Assert.AreEqual(78, cursor.MoveY);
        }

        [Test]
        public void Begin_WithoutCursorVisibility_IgnoresMoveRequests()
        {
            RecordingCursor cursor = new RecordingCursor();
            TargetingController controller = new TargetingController(cursor);
            controller.Begin(new TargetingRequest("Target", null, new RecordingReceiver()));

            controller.MoveCursor(12, 34);

            Assert.AreEqual(0, cursor.MoveCount);
        }

        [Test]
        public void TrySelectTarget_NoActiveRequestOrTarget_ReturnsFalse()
        {
            TargetingController controller = new TargetingController();
            RecordingReceiver receiver = new RecordingReceiver();
            controller.Begin(new TargetingRequest("Target", null, receiver));

            bool nullTargetSelected = controller.TrySelectTarget(null);
            controller.Cancel();
            bool inactiveTargetSelected = controller.TrySelectTarget(
                new StubTargetable(new object())
            );

            Assert.IsFalse(nullTargetSelected);
            Assert.IsFalse(inactiveTargetSelected);
            Assert.AreEqual(0, receiver.SelectedCount);
        }

        [Test]
        public void TrySelectTarget_ActiveRequest_CompletesAndHidesCursor()
        {
            object target = new object();
            RecordingCursor cursor = new RecordingCursor();
            RecordingReceiver receiver = new RecordingReceiver();
            TargetingRequest request = new TargetingRequest("Target", null, receiver);
            TargetingController controller = new TargetingController(cursor);
            controller.Begin(request, 1, 2);

            bool selected = controller.TrySelectTarget(new StubTargetable(target));

            Assert.IsTrue(selected);
            Assert.IsFalse(controller.IsTargeting);
            Assert.IsNull(controller.ActiveRequest);
            Assert.AreEqual(1, cursor.HideCount);
            Assert.AreEqual(1, receiver.SelectedCount);
            Assert.AreSame(request, receiver.LastRequest);
            Assert.AreSame(target, receiver.LastTarget);
        }

        [Test]
        public void Cancel_ActiveRequest_CancelsAndHidesCursor()
        {
            RecordingCursor cursor = new RecordingCursor();
            RecordingReceiver receiver = new RecordingReceiver();
            TargetingRequest request = new TargetingRequest("Target", null, receiver);
            TargetingController controller = new TargetingController(cursor);
            controller.Begin(request, 1, 2);

            controller.Cancel();
            controller.Cancel();

            Assert.IsFalse(controller.IsTargeting);
            Assert.AreEqual(1, cursor.HideCount);
            Assert.AreEqual(1, receiver.CanceledCount);
            Assert.AreSame(request, receiver.LastRequest);
        }

        [Test]
        public void TryCancel_InactiveThenActive_ReturnsMatchingResult()
        {
            TargetingController controller = new TargetingController();

            bool inactiveResult = controller.TryCancel();
            controller.Begin(new TargetingRequest("Target", null, new RecordingReceiver()));
            bool activeResult = controller.TryCancel();

            Assert.IsFalse(inactiveResult);
            Assert.IsTrue(activeResult);
        }

        private sealed class StubTargetable : ITargetable
        {
            public StubTargetable(object target)
            {
                Target = target;
            }

            public object Target { get; }
        }

        private sealed class RecordingCursor : ITargetingCursor
        {
            public int HideCount { get; private set; }
            public int LastX { get; private set; }
            public int LastY { get; private set; }
            public int MoveCount { get; private set; }
            public int MoveX { get; private set; }
            public int MoveY { get; private set; }
            public int ShowCount { get; private set; }

            public void Show(int x, int y)
            {
                ShowCount++;
                LastX = x;
                LastY = y;
            }

            public void MoveTo(int x, int y)
            {
                MoveCount++;
                MoveX = x;
                MoveY = y;
            }

            public void Hide()
            {
                HideCount++;
            }
        }

        private sealed class RecordingReceiver : ITargetingReceiver
        {
            public int CanceledCount { get; private set; }
            public TargetingRequest LastRequest { get; private set; }
            public object LastTarget { get; private set; }
            public int SelectedCount { get; private set; }

            public void OnTargetSelected(TargetingRequest request, object target)
            {
                SelectedCount++;
                LastRequest = request;
                LastTarget = target;
            }

            public void OnTargetingCancelled(TargetingRequest request)
            {
                CanceledCount++;
                LastRequest = request;
            }
        }
    }
}
