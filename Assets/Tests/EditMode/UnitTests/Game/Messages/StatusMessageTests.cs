using NUnit.Framework;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.Game.Messages
{
    [TestFixture]
    public class StatusMessageTests
    {
        [Test]
        public void Constructor_WithTypeAndBody_InitializesMessage()
        {
            StatusMessage message = new StatusMessage(
                MessageType.Conflict,
                "Test message",
                "Test message"
            );

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("Test message", message.Title);
            Assert.AreEqual("Test message", message.Body);
            Assert.IsFalse(message.Read);
            Assert.IsFalse(message.NotificationAcknowledged);
        }

        [Test]
        public void SerializeAndDeserialize_StatusMessage_MaintainsConcreteType()
        {
            StatusMessage message = new StatusMessage(
                MessageType.Advice,
                "Advice title",
                "Advice body"
            )
            {
                NotificationAcknowledged = true,
            };

            string serialized = SerializationHelper.Serialize(message);
            Message deserialized = SerializationHelper.Deserialize<Message>(serialized);

            Assert.IsInstanceOf<StatusMessage>(deserialized);
            Assert.AreEqual(message.Type, deserialized.Type);
            Assert.AreEqual(message.Title, deserialized.Title);
            Assert.AreEqual(message.Body, deserialized.Body);
            Assert.IsTrue(deserialized.NotificationAcknowledged);
        }

        [Test]
        public void NotificationAcknowledged_ReadMessage_ReturnsTrue()
        {
            StatusMessage message = new StatusMessage { Read = true };

            Assert.IsTrue(message.NotificationAcknowledged);
        }
    }
}
