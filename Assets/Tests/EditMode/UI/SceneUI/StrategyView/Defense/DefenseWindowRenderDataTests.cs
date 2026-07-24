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

        [Test]
        public void StrategyUnitCardRenderData_NullName_NormalizesAndPreservesFlags()
        {
            StrategyUnitCardRenderData card = CreateCard();

            Assert.AreEqual(string.Empty, card.Name);
            Assert.AreEqual((Color32)Color.green, card.NameColor);
            Assert.IsTrue(card.ShowName);
            Assert.IsTrue(card.UseAlternateNameLayout);
            Assert.AreEqual(4, card.EntityFrameYOffset);
            Assert.IsTrue(card.CanDrag);
        }

        private static StrategyUnitCardRenderData CreateCard()
        {
            return new StrategyUnitCardRenderData(
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
        }
    }
}
