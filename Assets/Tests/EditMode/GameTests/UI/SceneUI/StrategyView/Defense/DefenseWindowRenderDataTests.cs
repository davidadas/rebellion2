using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Defense
{
    [TestFixture]
    public class DefenseWindowRenderDataTests
    {
        [Test]
        public void OrderedTabs_DefaultCatalog_ReturnsAuthoredTabOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    DefenseWindowTab.Personnel,
                    DefenseWindowTab.Regiments,
                    DefenseWindowTab.Starfighters,
                    DefenseWindowTab.Shields,
                    DefenseWindowTab.Batteries,
                },
                DefenseWindowRenderData.OrderedTabs
            );
            Assert.AreEqual(5, DefenseWindowRenderData.TabCount);
        }

        [Test]
        public void Constructor_MutableCollections_CopiesAndNormalizesInputs()
        {
            List<DefenseWindowTabRenderData> tabs = new List<DefenseWindowTabRenderData>
            {
                new DefenseWindowTabRenderData(DefenseWindowTab.Personnel, null, null),
            };
            List<StrategyUnitCardRenderData> items = new List<StrategyUnitCardRenderData>
            {
                CreateCard(),
            };
            DefenseWindowRenderData data = new DefenseWindowRenderData(
                7,
                8,
                null,
                null,
                DefenseWindowTab.Personnel,
                null,
                null,
                tabs,
                items
            );

            tabs.Clear();
            items.Clear();

            Assert.AreEqual(7, data.X);
            Assert.AreEqual(8, data.Y);
            Assert.AreEqual(string.Empty, data.Caption);
            Assert.AreEqual(string.Empty, data.TabTitle);
            Assert.AreEqual(string.Empty, data.GarrisonRequirementText);
            Assert.AreEqual(1, data.Tabs.Count);
            Assert.AreEqual(1, data.Items.Count);
        }

        [Test]
        public void Constructor_NullTabs_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DefenseWindowRenderData(
                    0,
                    0,
                    null,
                    null,
                    DefenseWindowTab.Personnel,
                    null,
                    null,
                    null,
                    Array.Empty<StrategyUnitCardRenderData>()
                )
            );
        }

        [Test]
        public void Constructor_NullItems_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DefenseWindowRenderData(
                    0,
                    0,
                    null,
                    null,
                    DefenseWindowTab.Personnel,
                    null,
                    null,
                    Array.Empty<DefenseWindowTabRenderData>(),
                    null
                )
            );
        }

        private static StrategyUnitCardRenderData CreateCard()
        {
            return new StrategyUnitCardRenderData(
                string.Empty,
                Color.white,
                false,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                null,
                null,
                null,
                false
            );
        }
    }
}
