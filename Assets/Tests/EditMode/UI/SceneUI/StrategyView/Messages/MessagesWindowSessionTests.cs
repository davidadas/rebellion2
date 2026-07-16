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

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void SelectTab_WithSelectedDetail_ClearsSelectionAndDetail()
        {
            Message message = new Message(MessageType.Fleet, "Fleet") { InstanceID = "message" };
            _session.Reconcile(new[] { message });
            _session.SelectOnly(message);
            _session.ShowDetail();

            _session.SelectTab(MessagesTab.Mission);

            Assert.AreEqual(MessagesTab.Mission, _session.ActiveTab);
            Assert.IsFalse(_session.DetailVisible);
            Assert.IsNull(_session.SelectedMessageId);
            Assert.IsEmpty(_session.GetSelectedMessageIds());
        }

        [Test]
        public void Reconcile_ReplacementWithSameId_PreservesSelectionIdentity()
        {
            Message original = new Message(MessageType.Fleet, "Original")
            {
                InstanceID = "message",
            };
            Message replacement = new Message(MessageType.Fleet, "Replacement")
            {
                InstanceID = original.InstanceID,
            };
            _session.Reconcile(new[] { original });
            _session.SelectOnly(original);

            _session.Reconcile(new[] { replacement });

            Assert.AreEqual(replacement.InstanceID, _session.SelectedMessageId);
            Assert.AreSame(replacement, _session.GetSelectedMessage());
        }

        [Test]
        public void Reconcile_EmptyMessages_ClearsSelectionAndDetail()
        {
            Message message = new Message(MessageType.Fleet, "Fleet") { InstanceID = "message" };
            _session.Reconcile(new[] { message });
            _session.SelectOnly(message);
            _session.ShowDetail();

            _session.Reconcile(System.Array.Empty<Message>());

            Assert.IsNull(_session.SelectedMessageId);
            Assert.IsFalse(_session.DetailVisible);
            Assert.IsEmpty(_session.GetSelectedMessageIds());
        }
    }
}
