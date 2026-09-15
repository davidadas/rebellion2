using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Messages;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesWindowSessionTests
    {
        private MessagesWindowSession _session;
        private GameObject _windowObject;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "MessagesWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            UIWindow window = _windowObject.GetComponent<UIWindow>();
            window.Configure(1, 0, 0, 100, 100, modal: true, canFocus: true, canMove: false);
            _session = new MessagesWindowSession(window);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        /// <summary>
        /// Verifies select tab with selected detail clears selection and detail.
        /// </summary>
        [Test]
        public void SelectTab_WithSelectedDetail_ClearsSelectionAndDetail()
        {
            Message message = new StatusMessage(MessageType.Fleet, "Fleet")
            {
                InstanceID = "message",
            };
            _session.Reconcile(new[] { message });
            _session.SelectOnly(message);
            _session.ShowDetail();

            _session.SelectTab(MessagesTab.Mission);

            Assert.AreEqual(MessagesTab.Mission, _session.ActiveTab);
            Assert.IsFalse(_session.DetailVisible);
            Assert.IsNull(_session.SelectedMessageId);
            Assert.IsEmpty(_session.GetSelectedMessageIDs());
        }

        /// <summary>
        /// Verifies select tab active tab still resets transient state.
        /// </summary>
        [Test]
        public void SelectTab_ActiveTab_StillResetsTransientState()
        {
            Message message = CreateMessage("message", "Message");
            _session.Reconcile(new[] { message });
            _session.SelectOnly(message);
            _session.ShowDetail();

            _session.SelectTab(MessagesTab.All);

            Assert.AreEqual(MessagesTab.All, _session.ActiveTab);
            Assert.IsFalse(_session.DetailVisible);
            Assert.IsNull(_session.SelectedMessageId);
            Assert.IsEmpty(_session.GetSelectedMessageIDs());
        }

        /// <summary>
        /// Verifies reconcile replacement with same id preserves selection identity.
        /// </summary>
        [Test]
        public void Reconcile_ReplacementWithSameID_PreservesSelectionIdentity()
        {
            Message original = new StatusMessage(MessageType.Fleet, "Original")
            {
                InstanceID = "message",
            };
            Message replacement = new StatusMessage(MessageType.Fleet, "Replacement")
            {
                InstanceID = original.InstanceID,
            };
            _session.Reconcile(new[] { original });
            _session.SelectOnly(original);

            _session.Reconcile(new[] { replacement });

            Assert.AreEqual(replacement.InstanceID, _session.SelectedMessageId);
            Assert.AreSame(replacement, _session.GetSelectedMessage());
        }

        /// <summary>
        /// Verifies reconcile empty messages clears selection and detail.
        /// </summary>
        [Test]
        public void Reconcile_EmptyMessages_ClearsSelectionAndDetail()
        {
            Message message = new StatusMessage(MessageType.Fleet, "Fleet")
            {
                InstanceID = "message",
            };
            _session.Reconcile(new[] { message });
            _session.SelectOnly(message);
            _session.ShowDetail();

            _session.Reconcile(System.Array.Empty<Message>());

            Assert.IsNull(_session.SelectedMessageId);
            Assert.IsFalse(_session.DetailVisible);
            Assert.IsEmpty(_session.GetSelectedMessageIDs());
        }

        /// <summary>
        /// Verifies reconcile source changes preserves message snapshot.
        /// </summary>
        [Test]
        public void Reconcile_SourceChanges_PreservesMessageSnapshot()
        {
            Message first = CreateMessage("first", "First");
            Message second = CreateMessage("second", "Second");
            Message[] messages = { first, second };

            _session.Reconcile(messages);
            messages[0] = second;

            Assert.AreEqual(2, _session.Messages.Count);
            Assert.AreSame(first, _session.Messages[0]);
            Assert.AreSame(second, _session.Messages[1]);
        }

        /// <summary>
        /// Verifies reconcile selected message removed while detail visible selects first message.
        /// </summary>
        [Test]
        public void Reconcile_SelectedMessageRemovedWhileDetailVisible_SelectsFirstMessage()
        {
            Message first = CreateMessage("first", "First");
            Message second = CreateMessage("second", "Second");
            _session.Reconcile(new[] { first, second });
            _session.SelectOnly(second);
            _session.ShowDetail();

            _session.Reconcile(new Message[] { null, first });

            Assert.IsTrue(_session.DetailVisible);
            Assert.AreEqual(first.InstanceID, _session.SelectedMessageId);
            Assert.AreSame(first, _session.GetSelectedMessage());
        }

        /// <summary>
        /// Verifies constructor null window throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MessagesWindowSession(null));
        }

        /// <summary>
        /// Verifies constructor window returns initial all messages state.
        /// </summary>
        [Test]
        public void Constructor_Window_ReturnsInitialAllMessagesState()
        {
            Assert.AreEqual(MessagesTab.All, _session.ActiveTab);
            Assert.IsFalse(_session.DetailVisible);
            Assert.IsEmpty(_session.Messages);
            Assert.IsNull(_session.SelectedMessageId);
            Assert.IsNull(_session.GetSelectedMessage());
            Assert.IsEmpty(_session.GetSelectedMessageIDs());
        }

        /// <summary>
        /// Verifies select only message replaces selection and primary identity.
        /// </summary>
        [Test]
        public void SelectOnly_Message_ReplacesSelectionAndPrimaryIdentity()
        {
            Message first = CreateMessage("first", "First");
            Message second = CreateMessage("second", "Second");
            _session.Reconcile(new[] { first, second });
            _session.SelectAll();

            _session.SelectOnly(second);

            Assert.AreEqual(second.InstanceID, _session.SelectedMessageId);
            Assert.AreSame(second, _session.GetSelectedMessage());
            CollectionAssert.AreEquivalent(
                new[] { second.InstanceID },
                _session.GetSelectedMessageIDs()
            );
        }

        /// <summary>
        /// Verifies select only null message clears selection.
        /// </summary>
        [Test]
        public void SelectOnly_NullMessage_ClearsSelection()
        {
            Message message = CreateMessage("message", "Message");
            _session.Reconcile(new[] { message });
            _session.SelectOnly(message);

            _session.SelectOnly(null);

            Assert.IsNull(_session.SelectedMessageId);
            Assert.IsNull(_session.GetSelectedMessage());
            Assert.IsEmpty(_session.GetSelectedMessageIDs());
        }

        /// <summary>
        /// Verifies select all messages selects every stable identity and preserves primary.
        /// </summary>
        [Test]
        public void SelectAll_Messages_SelectsEveryStableIdentityAndPreservesPrimary()
        {
            Message first = CreateMessage("first", "First");
            Message second = CreateMessage("second", "Second");
            _session.Reconcile(new Message[] { first, null, second });
            _session.SelectOnly(first);

            _session.SelectAll();

            Assert.AreEqual(first.InstanceID, _session.SelectedMessageId);
            CollectionAssert.AreEquivalent(
                new[] { first.InstanceID, second.InstanceID },
                _session.GetSelectedMessageIDs()
            );
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)_session.GetSelectedMessageIDs())[0] = "changed"
            );
        }

        /// <summary>
        /// Verifies clear selection selected messages clears primary and multi selection.
        /// </summary>
        [Test]
        public void ClearSelection_SelectedMessages_ClearsPrimaryAndMultiSelection()
        {
            Message first = CreateMessage("first", "First");
            Message second = CreateMessage("second", "Second");
            _session.Reconcile(new[] { first, second });
            _session.SelectOnly(first);
            _session.SelectAll();

            _session.ClearSelection();

            Assert.IsNull(_session.SelectedMessageId);
            Assert.IsNull(_session.GetSelectedMessage());
            Assert.IsEmpty(_session.GetSelectedMessageIDs());
        }

        /// <summary>
        /// Verifies move selection messages moves within source bounds.
        /// </summary>
        [Test]
        public void MoveSelection_Messages_MovesWithinSourceBounds()
        {
            Message first = CreateMessage("first", "First");
            Message second = CreateMessage("second", "Second");
            Message third = CreateMessage("third", "Third");
            _session.Reconcile(new[] { first, second, third });

            bool entered = _session.MoveSelection(1);
            bool moved = _session.MoveSelection(1);
            bool bounded = _session.MoveSelection(5);

            Assert.IsTrue(entered);
            Assert.IsTrue(moved);
            Assert.IsTrue(bounded);
            Assert.AreEqual(third.InstanceID, _session.SelectedMessageId);
            Assert.IsFalse(_session.MoveSelection(1));
        }

        /// <summary>
        /// Verifies move selection empty messages returns false.
        /// </summary>
        [Test]
        public void MoveSelection_EmptyMessages_ReturnsFalse()
        {
            bool moved = _session.MoveSelection(1);

            Assert.IsFalse(moved);
            Assert.IsNull(_session.SelectedMessageId);
        }

        /// <summary>
        /// Verifies show and hide detail selection preserves selection and changes panel.
        /// </summary>
        [Test]
        public void ShowAndHideDetail_Selection_PreservesSelectionAndChangesPanel()
        {
            Message message = CreateMessage("message", "Message");
            _session.Reconcile(new[] { message });
            _session.SelectOnly(message);

            _session.ShowDetail();
            Assert.IsTrue(_session.DetailVisible);
            _session.HideDetail();

            Assert.IsFalse(_session.DetailVisible);
            Assert.AreEqual(message.InstanceID, _session.SelectedMessageId);
        }

        /// <summary>
        /// Creates message.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="title">The title.</param>
        /// <returns>The created message.</returns>
        private static Message CreateMessage(string instanceId, string title)
        {
            return new StatusMessage(MessageType.Fleet, title) { InstanceID = instanceId };
        }
    }
}
