using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class StrategyHudViewDataTests
    {
        [Test]
        public void RenderData_NullTextAndMessages_NormalizesEmptyState()
        {
            StrategyHudRenderData data = new StrategyHudRenderData(
                null,
                null,
                null,
                null,
                TickSpeed.Paused,
                null
            );

            Assert.AreEqual(string.Empty, data.TickText);
            Assert.AreEqual(string.Empty, data.RawMaterialsText);
            Assert.AreEqual(string.Empty, data.RefinedMaterialsText);
            Assert.AreEqual(string.Empty, data.MaintenanceText);
            Assert.AreEqual(TickSpeed.Paused, data.Speed);
            Assert.IsFalse(data.HasUnreadMessageType(MessageType.Fleet));
        }

        [Test]
        public void RenderData_UnreadMessageTypes_CopiesAndDeduplicatesSource()
        {
            List<MessageType> unreadTypes = new List<MessageType>
            {
                MessageType.Fleet,
                MessageType.Fleet,
                MessageType.Mission,
            };

            StrategyHudRenderData data = new StrategyHudRenderData(
                "12",
                "34",
                "56",
                "78",
                TickSpeed.Fast,
                unreadTypes
            );
            unreadTypes.Clear();

            Assert.AreEqual("12", data.TickText);
            Assert.AreEqual("34", data.RawMaterialsText);
            Assert.AreEqual("56", data.RefinedMaterialsText);
            Assert.AreEqual("78", data.MaintenanceText);
            Assert.AreEqual(TickSpeed.Fast, data.Speed);
            Assert.IsTrue(data.HasUnreadMessageType(MessageType.Fleet));
            Assert.IsTrue(data.HasUnreadMessageType(MessageType.Mission));
            Assert.IsFalse(data.HasUnreadMessageType(MessageType.Resource));
        }
    }
}
