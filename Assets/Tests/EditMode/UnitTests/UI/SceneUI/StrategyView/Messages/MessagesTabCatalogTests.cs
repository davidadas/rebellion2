using System;
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
        public void GetMessageType_UnsupportedTab_ReturnsNull()
        {
            MessagesTab tab = (MessagesTab)20;

            Assert.IsNull(MessagesTabCatalog.GetMessageType(tab));
        }

        [Test]
        public void OrderedTabs_DefaultCatalog_ReturnsAuthoredTabOrder()
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
            Assert.AreEqual(10, MessagesTabCatalog.Count);
        }

        [TestCase(-5, MessagesTab.All)]
        [TestCase(0, MessagesTab.All)]
        [TestCase(4, MessagesTab.Resource)]
        [TestCase(9, MessagesTab.Advice)]
        [TestCase(20, MessagesTab.Advice)]
        public void Clamp_ExternalIndex_ReturnsBoundedAuthoredTab(int index, MessagesTab expected)
        {
            MessagesTab tab = MessagesTabCatalog.Clamp(index);

            Assert.AreEqual(expected, tab);
        }

        [TestCase(0, MessagesTab.All)]
        [TestCase(1, MessagesTab.Support)]
        [TestCase(2, MessagesTab.Fleet)]
        [TestCase(3, MessagesTab.Mission)]
        [TestCase(4, MessagesTab.Resource)]
        [TestCase(5, MessagesTab.Manufacturing)]
        [TestCase(6, MessagesTab.Defense)]
        [TestCase(7, MessagesTab.Conflict)]
        [TestCase(8, MessagesTab.Chat)]
        [TestCase(9, MessagesTab.Advice)]
        public void GetAt_AuthoredIndex_ReturnsSemanticTab(int index, MessagesTab expected)
        {
            MessagesTab tab = MessagesTabCatalog.GetAt(index);

            Assert.AreEqual(expected, tab);
        }

        [TestCase(-1)]
        [TestCase(10)]
        public void GetAt_InvalidIndex_ThrowsArgumentOutOfRangeException(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MessagesTabCatalog.GetAt(index));
        }

        [TestCase(MessagesTab.All, "All Messages")]
        [TestCase(MessagesTab.Support, "Popular Support Messages")]
        [TestCase(MessagesTab.Fleet, "Fleet Messages")]
        [TestCase(MessagesTab.Mission, "Mission Messages")]
        [TestCase(MessagesTab.Resource, "Resource Messages")]
        [TestCase(MessagesTab.Manufacturing, "Manufacturing Messages")]
        [TestCase(MessagesTab.Defense, "Defense Messages")]
        [TestCase(MessagesTab.Conflict, "Conflict Messages")]
        [TestCase(MessagesTab.Chat, "Chat Messages")]
        [TestCase(MessagesTab.Advice, "Advice Messages")]
        public void GetTitle_SemanticTab_ReturnsDisplayedTitle(MessagesTab tab, string expected)
        {
            string title = MessagesTabCatalog.GetTitle(tab);

            Assert.AreEqual(expected, title);
        }

        [Test]
        public void GetTitle_UnsupportedTab_ReturnsEmptyString()
        {
            MessagesTab tab = (MessagesTab)20;

            Assert.AreEqual(string.Empty, MessagesTabCatalog.GetTitle(tab));
        }
    }
}
