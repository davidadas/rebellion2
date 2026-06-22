using NUnit.Framework;
using Rebellion.Game.Factions;
using GameMessageType = Rebellion.Game.Factions.MessageType;

public sealed class MessagesWindowViewTests
{
    [Test]
    public void GetHeaderUsesMessageTitleBeforeBody()
    {
        Message message = new Message(
            GameMessageType.Mission,
            "Diplomacy Mission Report",
            "The diplomacy mission to Balmorra failed.\nMore text."
        );

        Assert.AreEqual("Diplomacy Mission Report", MessagesWindowView.GetHeader(message));
    }

    [Test]
    public void GetHeaderFallsBackToFirstBodyLineWhenTitleIsMissing()
    {
        Message message = new Message(
            GameMessageType.Mission,
            null,
            "First body line\nSecond line"
        );

        Assert.AreEqual("First body line", MessagesWindowView.GetHeader(message));
    }

    [TestCase(1, GameMessageType.PopularSupport)]
    [TestCase(2, GameMessageType.Fleet)]
    [TestCase(3, GameMessageType.Mission)]
    [TestCase(4, GameMessageType.Resource)]
    [TestCase(5, GameMessageType.Manufacturing)]
    [TestCase(6, GameMessageType.Defense)]
    [TestCase(7, GameMessageType.Conflict)]
    [TestCase(8, GameMessageType.Chat)]
    [TestCase(9, GameMessageType.Advice)]
    public void GetMessageTypeForTabMapsMessageTabs(int tab, GameMessageType expected)
    {
        Assert.AreEqual(expected, MessagesWindowView.GetMessageTypeForTab(tab));
    }

    [Test]
    public void GetMessageTypeForTabReturnsNullForAllMessagesTab()
    {
        Assert.IsNull(MessagesWindowView.GetMessageTypeForTab(0));
    }

    [Test]
    public void MessagesThemeReturnsConfiguredDetailImagePath()
    {
        MessagesWindowTheme theme = new MessagesWindowTheme();
        theme.DetailImages.Add(
            new MessageDetailImageTheme
            {
                Key = "mission_report",
                ImagePath = "Art/UI/Messages/ui_message_mission_report",
            }
        );

        Assert.AreEqual(
            "Art/UI/Messages/ui_message_mission_report",
            theme.GetDetailImagePath("mission_report")
        );
    }
}
