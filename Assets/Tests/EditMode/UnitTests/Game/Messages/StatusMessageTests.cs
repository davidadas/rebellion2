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
            StatusMessage message = new StatusMessage(MessageType.Conflict, "Test message");

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("Test message", message.Title);
            Assert.AreEqual("Test message", message.Body);
            Assert.IsFalse(message.Read);
        }

        [Test]
        public void SerializeAndDeserialize_StatusMessage_MaintainsConcreteType()
        {
            StatusMessage message = new StatusMessage(
                MessageType.Advice,
                "Advice title",
                "Advice body"
            );

            string serialized = SerializationHelper.Serialize(message);
            Message deserialized = SerializationHelper.Deserialize<Message>(serialized);

            Assert.IsInstanceOf<StatusMessage>(deserialized);
            Assert.AreEqual(message.Type, deserialized.Type);
            Assert.AreEqual(message.Title, deserialized.Title);
            Assert.AreEqual(message.Body, deserialized.Body);
        }
    }
}
