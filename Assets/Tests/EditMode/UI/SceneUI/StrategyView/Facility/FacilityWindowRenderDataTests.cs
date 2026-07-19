using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Facility
{
    [TestFixture]
    public class FacilityWindowRenderDataTests
    {
        [Test]
        public void OrderedTabs_DefaultCatalog_ReturnsAuthoredTabOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    FacilityWindowTab.Manufacturing,
                    FacilityWindowTab.Shipyards,
                    FacilityWindowTab.Training,
                    FacilityWindowTab.Construction,
                    FacilityWindowTab.Refineries,
                    FacilityWindowTab.Mines,
                },
                FacilityWindowRenderData.OrderedTabs
            );
            Assert.AreEqual(6, FacilityWindowRenderData.TabCount);
        }

        [Test]
        public void Constructor_MutableCollections_CopiesInputCollections()
        {
            List<FacilityWindowTabRenderData> tabs = new List<FacilityWindowTabRenderData>
            {
                new FacilityWindowTabRenderData(
                    FacilityWindowTab.Manufacturing,
                    FacilityWindowTabState.Active
                ),
            };
            List<ManufacturingLaneCardRenderData> cards = new List<ManufacturingLaneCardRenderData>
            {
                new ManufacturingLaneCardRenderData(
                    null,
                    null,
                    1,
                    2,
                    "title",
                    "empty",
                    "current",
                    "count",
                    "destination",
                    "facilities"
                ),
            };
            List<FacilityInventoryItemRenderData> items = new List<FacilityInventoryItemRenderData>
            {
                new FacilityInventoryItemRenderData(null, true),
            };
            FacilityWindowRenderData data = new FacilityWindowRenderData(
                3,
                4,
                null,
                "caption",
                FacilityWindowTab.Manufacturing,
                tabs,
                null,
                null,
                cards,
                "inventory",
                items,
                null
            );

            tabs.Clear();
            cards.Clear();
            items.Clear();

            Assert.AreEqual(3, data.X);
            Assert.AreEqual(4, data.Y);
            Assert.AreEqual("caption", data.Caption);
            Assert.AreEqual(1, data.Tabs.Count);
            Assert.AreEqual(1, data.ManufacturingCards.Count);
            Assert.AreEqual(1, data.InventoryItems.Count);
            Assert.IsTrue(data.ShowManufacturing);
        }

        [Test]
        public void Constructor_NullText_NormalizesTextToEmptyStrings()
        {
            ManufacturingLaneCardRenderData card = new ManufacturingLaneCardRenderData(
                null,
                null,
                0,
                0,
                null,
                null,
                null,
                null,
                null,
                null
            );
            FacilityWindowRenderData data = new FacilityWindowRenderData(
                0,
                0,
                null,
                null,
                FacilityWindowTab.Mines,
                Array.Empty<FacilityWindowTabRenderData>(),
                null,
                null,
                Array.Empty<ManufacturingLaneCardRenderData>(),
                null,
                Array.Empty<FacilityInventoryItemRenderData>(),
                null
            );

            Assert.AreEqual(string.Empty, card.Title);
            Assert.AreEqual(string.Empty, card.EmptyText);
            Assert.AreEqual(string.Empty, card.CurrentName);
            Assert.AreEqual(string.Empty, card.CurrentCount);
            Assert.AreEqual(string.Empty, card.DestinationText);
            Assert.AreEqual(string.Empty, card.FacilityCount);
            Assert.AreEqual(string.Empty, data.Caption);
            Assert.AreEqual(string.Empty, data.InventoryTitle);
            Assert.IsFalse(data.ShowManufacturing);
        }

        [Test]
        public void Constructor_NullTabs_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FacilityWindowRenderData(
                    0,
                    0,
                    null,
                    null,
                    FacilityWindowTab.Manufacturing,
                    null,
                    null,
                    null,
                    Array.Empty<ManufacturingLaneCardRenderData>(),
                    null,
                    Array.Empty<FacilityInventoryItemRenderData>(),
                    null
                )
            );
        }

        [Test]
        public void Constructor_NullManufacturingCards_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FacilityWindowRenderData(
                    0,
                    0,
                    null,
                    null,
                    FacilityWindowTab.Manufacturing,
                    Array.Empty<FacilityWindowTabRenderData>(),
                    null,
                    null,
                    null,
                    null,
                    Array.Empty<FacilityInventoryItemRenderData>(),
                    null
                )
            );
        }

        [Test]
        public void Constructor_NullInventoryItems_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FacilityWindowRenderData(
                    0,
                    0,
                    null,
                    null,
                    FacilityWindowTab.Manufacturing,
                    Array.Empty<FacilityWindowTabRenderData>(),
                    null,
                    null,
                    Array.Empty<ManufacturingLaneCardRenderData>(),
                    null,
                    null,
                    null
                )
            );
        }
    }
}
