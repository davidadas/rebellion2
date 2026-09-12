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
    }
}
