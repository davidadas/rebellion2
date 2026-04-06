using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void Constructor_WithTypeAndText_InitializesCorrectly()
        {
            Message message = new Message(MessageType.Conflict, "Test message");

            Assert.AreEqual(MessageType.Conflict, message.Type, "Type should match");
            Assert.AreEqual("Test message", message.Text, "Text should match");
            Assert.IsFalse(message.Read, "Read should be false initially");
        }

        [Test]
        public void GetText_UnreadMessage_ReturnsText()
        {
            Message message = new Message(MessageType.Mission, "Mission update");

            string text = message.GetText();

            Assert.AreEqual("Mission update", text, "GetText should return correct text");
        }

        [Test]
        public void GetText_MessageWithReadTrue_SetsReadToFalse()
        {
            Message message = new Message(MessageType.Resource, "Resource alert");
            message.Read = true;

            message.GetText();

            Assert.IsFalse(message.Read, "GetText should set Read to false");
        }

        [Test]
        public void SerializeAndDeserialize_MessageWithText_MaintainsState()
        {
            Message message = new Message(MessageType.PopularSupport, "Support gained")
            {
                InstanceID = "MSG1",
                Read = true,
            };

            string serialized = SerializationHelper.Serialize(message);
            Message deserialized = SerializationHelper.Deserialize<Message>(serialized);

            Assert.AreEqual(
                message.InstanceID,
                deserialized.InstanceID,
                "InstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                message.Type,
                deserialized.Type,
                "Type should be correctly deserialized."
            );
            Assert.AreEqual(
                message.Text,
                deserialized.Text,
                "Text should be correctly deserialized."
            );
            Assert.AreEqual(
                message.Read,
                deserialized.Read,
                "Read should be correctly deserialized."
            );
        }
    }
} // namespace Rebellion.Tests.Game
