using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesWindowProjectorTests
    {
        [Test]
        public void GetHeader_MessageWithTitle_ReturnsTitle()
        {
            Message message = new Message(
                MessageType.Mission,
                "Diplomacy Mission Report",
                "The diplomacy mission failed.\nMore text."
            );

            string header = MessagesWindowProjector.GetHeader(message);

            Assert.AreEqual("Diplomacy Mission Report", header);
        }

        [Test]
        public void GetHeader_MessageWithoutTitle_ReturnsFirstBodyLine()
        {
            Message message = new Message(
                MessageType.Mission,
                null,
                "First body line\nSecond line"
            );

            string header = MessagesWindowProjector.GetHeader(message);

            Assert.AreEqual("First body line", header);
        }

        [Test]
        public void CreateIndexRows_StoredMessages_ReturnsNewestFirstWithoutMutatingReadState()
        {
            Message first = new Message(MessageType.Fleet, "First")
            {
                InstanceID = "first",
                Read = true,
            };
            Message second = new Message(MessageType.Fleet, "Second")
            {
                InstanceID = "second",
                Read = false,
            };
            List<Message> messages = new List<Message> { first, second };

            List<MessageWindowRowRenderData> rows = MessagesWindowProjector.CreateIndexRows(
                messages,
                new[] { first.InstanceID }
            );

            CollectionAssert.AreEqual(
                new[] { "second", "first" },
                rows.Select(row => row.MessageId)
            );
            Assert.IsTrue(rows[0].Unread);
            Assert.IsFalse(rows[0].Selected);
            Assert.IsFalse(rows[1].Unread);
            Assert.IsTrue(rows[1].Selected);
            Assert.IsTrue(first.Read);
            Assert.IsFalse(second.Read);
        }
    }
}
