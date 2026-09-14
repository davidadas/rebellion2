using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Rebellion.Tests.UI.Components.ContextMenu
{
    [TestFixture]
    public class ContextMenuControllerTests
    {
        /// <summary>
        /// Verifies request null receiver throws argument null exception.
        /// </summary>
        [Test]
        public void Request_NullReceiver_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ContextMenuRequest(null, Array.Empty<IContextMenuCommand>(), null)
            );
        }

        /// <summary>
        /// Verifies request null commands uses empty command collection.
        /// </summary>
        [Test]
        public void Request_NullCommands_UsesEmptyCommandCollection()
        {
            TestReceiver receiver = new TestReceiver();
            object source = new object();

            ContextMenuRequest request = new ContextMenuRequest(source, null, receiver);

            Assert.AreSame(source, request.Source);
            Assert.AreSame(receiver, request.Receiver);
            Assert.IsEmpty(request.Commands);
        }

        /// <summary>
        /// Verifies request mutable commands copies input collection.
        /// </summary>
        [Test]
        public void Request_MutableCommands_CopiesInputCollection()
        {
            List<IContextMenuCommand> commands = new List<IContextMenuCommand>
            {
                new TestCommand("Command", true),
            };

            ContextMenuRequest request = new ContextMenuRequest(null, commands, new TestReceiver());
            commands.Clear();

            Assert.AreEqual(1, request.Commands.Count);
        }

        /// <summary>
        /// Verifies open null request throws argument null exception.
        /// </summary>
        [Test]
        public void Open_NullRequest_ThrowsArgumentNullException()
        {
            ContextMenuController controller = new ContextMenuController();

            Assert.Throws<ArgumentNullException>(() => controller.Open(null));
        }

        /// <summary>
        /// Verifies open existing request cancels previous request and activates replacement.
        /// </summary>
        [Test]
        public void Open_ExistingRequest_CancelsPreviousRequestAndActivatesReplacement()
        {
            ContextMenuController controller = new ContextMenuController();
            TestReceiver firstReceiver = new TestReceiver();
            ContextMenuRequest first = CreateRequest(firstReceiver, new TestCommand("First", true));
            ContextMenuRequest second = CreateRequest(
                new TestReceiver(),
                new TestCommand("Second", true)
            );
            controller.Open(first);

            controller.Open(second);

            Assert.AreEqual(1, firstReceiver.CancelledCount);
            Assert.IsTrue(controller.IsOpen);
            Assert.AreSame(second, controller.ActiveRequest);
        }

        /// <summary>
        /// Verifies try select command enabled root command completes request.
        /// </summary>
        [Test]
        public void TrySelectCommand_EnabledRootCommand_CompletesRequest()
        {
            ContextMenuController controller = new ContextMenuController();
            TestReceiver receiver = new TestReceiver();
            TestCommand command = new TestCommand("Command", true);
            ContextMenuRequest request = CreateRequest(receiver, command);
            controller.Open(request);

            bool selected = controller.TrySelectCommand(command);

            Assert.IsTrue(selected);
            Assert.IsFalse(controller.IsOpen);
            Assert.AreSame(request, receiver.SelectedRequest);
            Assert.AreSame(command, receiver.SelectedCommand);
            Assert.AreEqual(1, receiver.SelectedCount);
        }

        /// <summary>
        /// Verifies try select command enabled command emits closed request before receiver.
        /// </summary>
        [Test]
        public void TrySelectCommand_EnabledCommand_EmitsClosedRequestBeforeReceiver()
        {
            ContextMenuController controller = new ContextMenuController();
            TestReceiver receiver = new TestReceiver();
            TestCommand command = new TestCommand("Command", true);
            ContextMenuRequest request = CreateRequest(receiver, command);
            ContextMenuRequest closedRequest = null;
            controller.RequestClosed += closed =>
            {
                closedRequest = closed;
                Assert.AreEqual(0, receiver.SelectedCount);
            };
            controller.Open(request);

            controller.TrySelectCommand(command);

            Assert.AreSame(request, closedRequest);
        }

        /// <summary>
        /// Verifies try select command enabled nested command completes request.
        /// </summary>
        [Test]
        public void TrySelectCommand_EnabledNestedCommand_CompletesRequest()
        {
            ContextMenuController controller = new ContextMenuController();
            TestReceiver receiver = new TestReceiver();
            TestCommand child = new TestCommand("Child", true);
            TestParentCommand parent = new TestParentCommand(
                "Parent",
                true,
                new IContextMenuCommand[] { child }
            );
            controller.Open(CreateRequest(receiver, parent));

            bool selected = controller.TrySelectCommand(child);

            Assert.IsTrue(selected);
            Assert.AreSame(child, receiver.SelectedCommand);
        }

        /// <summary>
        /// Verifies try select command invalid command preserves active request.
        /// </summary>
        [Test]
        public void TrySelectCommand_InvalidCommand_PreservesActiveRequest()
        {
            ContextMenuController controller = new ContextMenuController();
            TestReceiver receiver = new TestReceiver();
            TestCommand included = new TestCommand("Included", true);
            TestCommand disabled = new TestCommand("Disabled", false);
            controller.Open(CreateRequest(receiver, included, disabled));

            bool missingSelected = controller.TrySelectCommand(new TestCommand("Missing", true));
            bool disabledSelected = controller.TrySelectCommand(disabled);
            bool nullSelected = controller.TrySelectCommand(null);

            Assert.IsFalse(missingSelected);
            Assert.IsFalse(disabledSelected);
            Assert.IsFalse(nullSelected);
            Assert.IsTrue(controller.IsOpen);
            Assert.AreEqual(0, receiver.SelectedCount);
        }

        /// <summary>
        /// Verifies try select command missing request returns false.
        /// </summary>
        [Test]
        public void TrySelectCommand_MissingRequest_ReturnsFalse()
        {
            ContextMenuController controller = new ContextMenuController();

            bool selected = controller.TrySelectCommand(new TestCommand("Command", true));

            Assert.IsFalse(selected);
        }

        /// <summary>
        /// Verifies cancel active request notifies receiver and clears state.
        /// </summary>
        [Test]
        public void Cancel_ActiveRequest_NotifiesReceiverAndClearsState()
        {
            ContextMenuController controller = new ContextMenuController();
            TestReceiver receiver = new TestReceiver();
            ContextMenuRequest request = CreateRequest(receiver, new TestCommand("Command", true));
            controller.Open(request);

            controller.Cancel();

            Assert.AreEqual(1, receiver.CancelledCount);
            Assert.AreSame(request, receiver.CancelledRequest);
            Assert.IsFalse(controller.IsOpen);
            Assert.IsNull(controller.ActiveRequest);
        }

        /// <summary>
        /// Verifies cancel active request emits closed request before receiver.
        /// </summary>
        [Test]
        public void Cancel_ActiveRequest_EmitsClosedRequestBeforeReceiver()
        {
            ContextMenuController controller = new ContextMenuController();
            TestReceiver receiver = new TestReceiver();
            ContextMenuRequest request = CreateRequest(receiver, new TestCommand("Command", true));
            ContextMenuRequest closedRequest = null;
            controller.RequestClosed += closed =>
            {
                closedRequest = closed;
                Assert.AreEqual(0, receiver.CancelledCount);
            };
            controller.Open(request);

            controller.Cancel();

            Assert.AreSame(request, closedRequest);
        }

        /// <summary>
        /// Verifies try cancel open then closed request reports state transition.
        /// </summary>
        [Test]
        public void TryCancel_OpenThenClosedRequest_ReportsStateTransition()
        {
            ContextMenuController controller = new ContextMenuController();
            TestReceiver receiver = new TestReceiver();
            controller.Open(CreateRequest(receiver, new TestCommand("Command", true)));

            bool firstCancelled = controller.TryCancel();
            bool secondCancelled = controller.TryCancel();

            Assert.IsTrue(firstCancelled);
            Assert.IsFalse(secondCancelled);
            Assert.AreEqual(1, receiver.CancelledCount);
        }

        /// <summary>
        /// Creates request.
        /// </summary>
        /// <param name="receiver">The receiver.</param>
        /// <param name="commands">The commands.</param>
        /// <returns>The created request.</returns>
        private static ContextMenuRequest CreateRequest(
            TestReceiver receiver,
            params IContextMenuCommand[] commands
        )
        {
            return new ContextMenuRequest(new object(), commands, receiver);
        }

        private sealed class TestCommand : IContextMenuCommand
        {
            public string Text { get; }

            public bool Enabled { get; }

            /// <summary>
            /// Initializes a new instance of the TestCommand class.
            /// </summary>
            /// <param name="text">The text.</param>
            /// <param name="enabled">Whether enabled.</param>
            public TestCommand(string text, bool enabled)
            {
                Text = text;
                Enabled = enabled;
            }
        }

        private sealed class TestParentCommand : IContextMenuParentCommand
        {
            public string Text { get; }

            public bool Enabled { get; }

            public IReadOnlyList<IContextMenuCommand> ChildCommands { get; }

            /// <summary>
            /// Initializes a new instance of the TestParentCommand class.
            /// </summary>
            /// <param name="text">The text.</param>
            /// <param name="enabled">Whether enabled.</param>
            /// <param name="childCommands">The child commands.</param>
            public TestParentCommand(
                string text,
                bool enabled,
                IReadOnlyList<IContextMenuCommand> childCommands
            )
            {
                Text = text;
                Enabled = enabled;
                ChildCommands = childCommands;
            }
        }

        private sealed class TestReceiver : IContextMenuReceiver
        {
            public int CancelledCount { get; private set; }

            public ContextMenuRequest CancelledRequest { get; private set; }

            public int SelectedCount { get; private set; }

            public IContextMenuCommand SelectedCommand { get; private set; }

            public ContextMenuRequest SelectedRequest { get; private set; }

            /// <summary>
            /// Executes on context menu command selected.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="command">The command.</param>
            public void OnContextMenuCommandSelected(
                ContextMenuRequest request,
                IContextMenuCommand command
            )
            {
                SelectedCount++;
                SelectedRequest = request;
                SelectedCommand = command;
            }

            /// <summary>
            /// Executes on context menu cancelled.
            /// </summary>
            /// <param name="request">The request.</param>
            public void OnContextMenuCancelled(ContextMenuRequest request)
            {
                CancelledCount++;
                CancelledRequest = request;
            }
        }
    }
}
