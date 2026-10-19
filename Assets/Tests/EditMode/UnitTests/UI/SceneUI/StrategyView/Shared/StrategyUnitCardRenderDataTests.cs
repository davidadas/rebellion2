using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public sealed class StrategyUnitCardRenderDataTests
    {
        [Test]
        public void Constructor_NullName_NormalizesName()
        {
            StrategyUnitCardRenderData card = new StrategyUnitCardRenderData(
                null,
                Color.green,
                true,
                true,
                null,
                null,
                null,
                null,
                null,
                null,
                4,
                null,
                null,
                null,
                true
            );

            Assert.AreEqual(string.Empty, card.Name);
        }

        [Test]
        public void Constructor_StatsOverlay_PreservesOverlay()
        {
            StrategyUnitCardStatsOverlayData overlay = new StrategyUnitCardStatsOverlayData(
                1,
                1,
                new[] { new Color32(1, 2, 3, 4) }
            );

            StrategyUnitCardRenderData card = new StrategyUnitCardRenderData(
                null,
                Color.green,
                true,
                true,
                null,
                null,
                null,
                null,
                null,
                null,
                4,
                null,
                null,
                null,
                true,
                overlay
            );

            Assert.AreSame(overlay, card.StatsOverlay);
        }

        [Test]
        public void GetStatsOverlayRenderRect_WideOverlay_FitsAndCentersInEntityFrame()
        {
            StrategyUnitCardStatsOverlayData overlay = new StrategyUnitCardStatsOverlayData(
                66,
                25,
                new Color32[66 * 25]
            );

            RectInt result = StrategyUnitCardView.GetStatsOverlayRenderRect(
                overlay,
                new RectInt(25, 5, 61, 25)
            );

            Assert.AreEqual(new RectInt(25, 6, 61, 23), result);
        }
    }
}
