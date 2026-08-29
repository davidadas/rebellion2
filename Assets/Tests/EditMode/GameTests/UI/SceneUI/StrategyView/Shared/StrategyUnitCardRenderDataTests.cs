using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public sealed class StrategyUnitCardRenderDataTests
    {
        [Test]
        public void Constructor_NullName_NormalizesAndPreservesFlags()
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
                true,
                true
            );

            Assert.AreEqual(string.Empty, card.Name);
            Assert.AreEqual((Color32)Color.green, card.NameColor);
            Assert.IsTrue(card.ShowName);
            Assert.IsTrue(card.UseAlternateNameLayout);
            Assert.AreEqual(4, card.EntityFrameYOffset);
            Assert.IsTrue(card.HideBackgroundWhenSelected);
            Assert.IsTrue(card.CanDrag);
        }
    }
}
