using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Rebellion.Tests.UI.Components.ContextMenu
{
    [TestFixture]
    public class ContextMenuControllerTests
    {
        [Test]
        public void Request_NullReceiver_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ContextMenuRequest(null, Array.Empty<IContextMenuCommand>(), null)
            );
        }

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

        [Test]
        public void Open_NullRequest_ThrowsArgumentNullException()
        {
            ContextMenuController controller = new ContextMenuController();

            Assert.Throws<ArgumentNullException>(() => controller.Open(null));
        }

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

        [Test]
        public void TrySelectCommand_MissingRequest_ReturnsFalse()
        {
            ContextMenuController controller = new ContextMenuController();

            bool selected = controller.TrySelectCommand(new TestCommand("Command", true));

            Assert.IsFalse(selected);
        }

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

        private static ContextMenuRequest CreateRequest(
            TestReceiver receiver,
            params IContextMenuCommand[] commands
        )
        {
            return new ContextMenuRequest(new object(), commands, receiver);
        }

        private sealed class TestCommand : IContextMenuCommand
        {
            public TestCommand(string text, bool enabled)
            {
                Text = text;
                Enabled = enabled;
            }

            public string Text { get; }

            public bool Enabled { get; }
        }

        private sealed class TestParentCommand : IContextMenuParentCommand
        {
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

            public string Text { get; }

            public bool Enabled { get; }

            public IReadOnlyList<IContextMenuCommand> ChildCommands { get; }
        }

        private sealed class TestReceiver : IContextMenuReceiver
        {
            public int CancelledCount { get; private set; }

            public ContextMenuRequest CancelledRequest { get; private set; }

            public int SelectedCount { get; private set; }

            public IContextMenuCommand SelectedCommand { get; private set; }

            public ContextMenuRequest SelectedRequest { get; private set; }

            public void OnContextMenuCommandSelected(
                ContextMenuRequest request,
                IContextMenuCommand command
            )
            {
                SelectedCount++;
                SelectedRequest = request;
                SelectedCommand = command;
            }

            public void OnContextMenuCancelled(ContextMenuRequest request)
            {
                CancelledCount++;
                CancelledRequest = request;
            }
        }
    }
}
