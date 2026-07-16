using NUnit.Framework;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesTabCatalogTests
    {
        [TestCase(MessagesTab.Support, MessageType.PopularSupport)]
        [TestCase(MessagesTab.Fleet, MessageType.Fleet)]
        [TestCase(MessagesTab.Mission, MessageType.Mission)]
        [TestCase(MessagesTab.Resource, MessageType.Resource)]
        [TestCase(MessagesTab.Manufacturing, MessageType.Manufacturing)]
        [TestCase(MessagesTab.Defense, MessageType.Defense)]
        [TestCase(MessagesTab.Conflict, MessageType.Conflict)]
        [TestCase(MessagesTab.Chat, MessageType.Chat)]
        [TestCase(MessagesTab.Advice, MessageType.Advice)]
        public void GetMessageType_CategoryTab_ReturnsMessageType(
            MessagesTab tab,
            MessageType expected
        )
        {
            Assert.AreEqual(expected, MessagesTabCatalog.GetMessageType(tab));
        }

        [Test]
        public void GetMessageType_AllMessagesTab_ReturnsNull()
        {
            Assert.IsNull(MessagesTabCatalog.GetMessageType(MessagesTab.All));
        }

        [Test]
        public void OrderedTabs_ReturnsAuthoredTabOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    MessagesTab.All,
                    MessagesTab.Support,
                    MessagesTab.Fleet,
                    MessagesTab.Mission,
                    MessagesTab.Resource,
                    MessagesTab.Manufacturing,
                    MessagesTab.Defense,
                    MessagesTab.Conflict,
                    MessagesTab.Chat,
                    MessagesTab.Advice,
                },
                MessagesTabCatalog.OrderedTabs
            );
        }
    }
}
