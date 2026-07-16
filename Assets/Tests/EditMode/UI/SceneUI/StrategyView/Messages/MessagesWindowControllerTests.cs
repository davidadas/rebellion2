using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesWindowControllerTests
    {
        [Test]
        public void GetDetailAudioPaths_MessageAndOfficerPaths_ReturnsPlaybackOrder()
        {
            Message message = new Message(MessageType.Fleet, "Fleet Arrived")
            {
                MessageVoicePath = "Audio/SFX/StrategyView/Messages/fleet",
                OfficerVoicePath = "Audio/Voices/Officers/officer",
            };

            IReadOnlyList<string> paths = MessagesWindowController.GetDetailAudioPaths(message);

            CollectionAssert.AreEqual(
                new[] { "Audio/SFX/StrategyView/Messages/fleet", "Audio/Voices/Officers/officer" },
                paths
            );
        }

        [Test]
        public void ToggleMessageNotification_AllMessagesTab_TogglesGlobalSetting()
        {
            Faction faction = new Faction();
            faction.ToggleAdvisorMessageNotification(MessageType.Fleet);

            MessagesWindowController.ToggleMessageNotification(faction, MessagesTab.All);

            Assert.IsFalse(faction.AdvisorMessageNotificationsEnabled);
            Assert.IsFalse(
                MessagesWindowController.IsMessageNotificationEnabled(faction, MessagesTab.Fleet)
            );
            Assert.IsFalse(
                MessagesWindowController.IsMessageNotificationEnabled(faction, MessagesTab.Mission)
            );
        }

        [Test]
        public void ToggleMessageNotification_CategoryTab_TogglesOnlyCategorySetting()
        {
            Faction faction = new Faction();

            MessagesWindowController.ToggleMessageNotification(faction, MessagesTab.Fleet);

            Assert.IsTrue(faction.AdvisorMessageNotificationsEnabled);
            Assert.IsFalse(
                MessagesWindowController.IsMessageNotificationEnabled(faction, MessagesTab.Fleet)
            );
            Assert.IsTrue(
                MessagesWindowController.IsMessageNotificationEnabled(faction, MessagesTab.Mission)
            );
        }

        [Test]
        public void RemoveSelectedMessages_SelectedIds_RemovesMatchingMessagesAcrossBuckets()
        {
            Message fleet = new Message(MessageType.Fleet, "Fleet") { InstanceID = "fleet" };
            Message mission = new Message(MessageType.Mission, "Mission")
            {
                InstanceID = "mission",
            };
            Message retained = new Message(MessageType.Mission, "Retained")
            {
                InstanceID = "retained",
            };
            Faction faction = new Faction();
            faction.Messages[MessageType.Fleet] = new List<Message> { fleet };
            faction.Messages[MessageType.Mission] = new List<Message> { mission, retained };

            bool removed = MessagesWindowController.RemoveSelectedMessages(
                faction,
                new[] { fleet.InstanceID, mission.InstanceID }
            );

            Assert.IsTrue(removed);
            Assert.IsEmpty(faction.Messages[MessageType.Fleet]);
            CollectionAssert.AreEqual(new[] { retained }, faction.Messages[MessageType.Mission]);
        }
    }
}
