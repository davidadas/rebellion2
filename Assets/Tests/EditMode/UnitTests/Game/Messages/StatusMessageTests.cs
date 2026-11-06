using NUnit.Framework;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.Game.Messages
{
    [TestFixture]
    public class StatusMessageTests
    {
        [Test]
        public void CreateCopy_ExplicitEmptyIdentifier_PreservesStoredIdentity()
        {
            StatusMessage source = new StatusMessage { InstanceID = string.Empty };

            Message copy = source.CreateCopy();

            Assert.AreEqual(string.Empty, copy.InstanceID);
        }

        [Test]
        public void CreateCopy_ReadMessage_PreservesIndependentAcknowledgementState()
        {
            StatusMessage source = new StatusMessage
            {
                Read = true,
                NotificationAcknowledged = false,
            };
            Message copy = source.CreateCopy();

            copy.Read = false;

            Assert.IsFalse(copy.NotificationAcknowledged);
            Assert.IsTrue(source.Read);
        }

        [Test]
        public void CreateCopy_MessagePresentation_PreservesStoredValues()
        {
            StatusMessage source = new StatusMessage(MessageType.Advice, "Advice", "Details")
            {
                InstanceID = "message",
                TypeID = "type",
                DisplayName = "Display name",
                DisplayStatus = "Status",
                DisplayImagePath = "display",
                SmallDisplayImagePath = "small",
                InTransitImagePath = "transit",
                InTransitSmallImagePath = "transit-small",
                DamagedImagePath = "damaged",
                DamagedSmallImagePath = "damaged-small",
                CapturedOverlayImagePath = "captured",
                InjuredImagePath = "injured",
                Description = "Description",
                BackgroundImageKey = "advice",
                OverlayImagePath = "overlay",
                BackgroundAudioPath = "audio",
                OfficerVoicePath = "voice",
                MessageImagePath = "message-image",
                EventLocationInstanceID = "location",
                NavigationTargetInstanceID = "target",
                NavigationSecondaryTargetInstanceID = "secondary",
                MissionInstanceID = "mission",
                MissionContinuationPending = true,
                CreatedTick = 25,
                NotificationAcknowledged = true,
            };
            string before = SerializationHelper.Serialize(source);

            Message copy = source.CreateCopy();

            Assert.AreEqual(before, SerializationHelper.Serialize(copy));
        }

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
                MissionInstanceID = "mission-1",
                MissionContinuationPending = true,
                NotificationAcknowledged = true,
            };

            string serialized = SerializationHelper.Serialize(message);
            Message deserialized = SerializationHelper.Deserialize<Message>(serialized);

            Assert.IsInstanceOf<StatusMessage>(deserialized);
            Assert.AreEqual(message.Type, deserialized.Type);
            Assert.AreEqual(message.Title, deserialized.Title);
            Assert.AreEqual(message.Body, deserialized.Body);
            Assert.AreEqual(message.MissionInstanceID, deserialized.MissionInstanceID);
            Assert.IsTrue(deserialized.MissionContinuationPending);
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
