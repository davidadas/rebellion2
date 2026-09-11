using NUnit.Framework;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public class MessagesWindowThemeTests
    {
        [Test]
        public void GetDetailImagePath_MatchingKey_ReturnsConfiguredPath()
        {
            MessagesWindowTheme theme = new MessagesWindowTheme();
            theme.DetailImages.Add(
                new MessageDetailImageTheme
                {
                    Key = "mission_report",
                    ImagePath = "Art/HD/UI/Messages/mission_report",
                }
            );

            string path = theme.GetDetailImagePath("mission_report");

            Assert.AreEqual("Art/HD/UI/Messages/mission_report", path);
        }

        [Test]
        public void GetIconImagePath_MatchingType_ReturnsConfiguredPath()
        {
            MessagesWindowTheme theme = new MessagesWindowTheme();
            theme.Icons.Add(
                new MessageWindowIconTheme
                {
                    MessageType = MessageType.Fleet,
                    ImagePath = "Art/HD/UI/StrategyView/messages_fleet_selected",
                }
            );

            string path = theme.GetIconImagePath(MessageType.Fleet);

            Assert.AreEqual("Art/HD/UI/StrategyView/messages_fleet_selected", path);
        }

        [Test]
        public void GetNormalIconImagePath_WithoutNormalPath_FallsBackToSelectedPath()
        {
            MessagesWindowTheme theme = new MessagesWindowTheme();
            theme.Icons.Add(
                new MessageWindowIconTheme
                {
                    MessageType = MessageType.Fleet,
                    ImagePath = "Art/HD/UI/StrategyView/messages_fleet_selected",
                }
            );

            string path = theme.GetNormalIconImagePath(MessageType.Fleet);

            Assert.AreEqual("Art/HD/UI/StrategyView/messages_fleet_selected", path);
        }
    }
}
