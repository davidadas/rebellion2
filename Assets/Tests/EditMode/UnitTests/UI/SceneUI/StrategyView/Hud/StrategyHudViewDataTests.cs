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
            Assert.AreSame(StrategyHudResourceBreakdown.Empty, data.ResourceBreakdown);
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

        [Test]
        public void RenderData_ResourceBreakdown_PreservesFacilityTotals()
        {
            StrategyHudResourceBreakdown breakdown = new StrategyHudResourceBreakdown(
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                0.25,
                0.375,
                0.5,
                0.125,
                0.1875,
                0.25,
                100,
                125,
                150,
                new StrategyHudMaintenanceBreakdown(5, 10, 15, 20, 25, 0, 50, 10, 15)
            );

            StrategyHudRenderData data = new StrategyHudRenderData(
                "",
                "",
                "",
                "",
                TickSpeed.Paused,
                null,
                breakdown
            );

            Assert.AreSame(breakdown, data.ResourceBreakdown);
            Assert.AreEqual(3, breakdown.DeployedMines);
            Assert.AreEqual(10, breakdown.TotalMines);
            Assert.AreEqual(11, breakdown.DeployedRefineries);
            Assert.AreEqual(26, breakdown.TotalRefineries);
            Assert.AreEqual(50, breakdown.MaintenanceHeadroom);
            Assert.AreEqual(65, breakdown.DeliveredMaintenanceHeadroom);
            Assert.AreEqual(75, breakdown.ProjectedMaintenanceHeadroom);
        }
    }
}
