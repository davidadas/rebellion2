using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Units;

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
        public void Count_DefaultCatalog_ReturnsManufacturingLaneCount()
        {
            Assert.AreEqual(3, FacilityManufacturingLaneCatalog.Count);
        }

        [TestCase(0, FacilityWindowTab.Shipyards)]
        [TestCase(1, FacilityWindowTab.Training)]
        [TestCase(2, FacilityWindowTab.Construction)]
        public void GetTab_ManufacturingCardIndex_ReturnsAuthoredTab(
            int cardIndex,
            FacilityWindowTab expected
        )
        {
            Assert.AreEqual(expected, FacilityManufacturingLaneCatalog.GetTab(cardIndex));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void GetTab_InvalidCardIndex_ReturnsNull(int cardIndex)
        {
            Assert.IsNull(FacilityManufacturingLaneCatalog.GetTab(cardIndex));
        }

        [TestCase(FacilityWindowTab.Shipyards, 0)]
        [TestCase(FacilityWindowTab.Training, 1)]
        [TestCase(FacilityWindowTab.Construction, 2)]
        public void GetCardIndex_ManufacturingTab_ReturnsAuthoredCardIndex(
            FacilityWindowTab tab,
            int expected
        )
        {
            Assert.AreEqual(expected, FacilityManufacturingLaneCatalog.GetCardIndex(tab));
        }

        [Test]
        public void GetCardIndex_NonManufacturingTab_ReturnsNull()
        {
            Assert.IsNull(FacilityManufacturingLaneCatalog.GetCardIndex(FacilityWindowTab.Mines));
        }

        [TestCase(FacilityWindowTab.Shipyards, ManufacturingType.Ship)]
        [TestCase(FacilityWindowTab.Training, ManufacturingType.Troop)]
        [TestCase(FacilityWindowTab.Construction, ManufacturingType.Building)]
        public void GetManufacturingType_ManufacturingTab_ReturnsMappedType(
            FacilityWindowTab tab,
            ManufacturingType expected
        )
        {
            Assert.AreEqual(expected, FacilityManufacturingLaneCatalog.GetManufacturingType(tab));
        }

        [Test]
        public void GetManufacturingType_NonManufacturingTab_ReturnsNull()
        {
            Assert.IsNull(
                FacilityManufacturingLaneCatalog.GetManufacturingType(FacilityWindowTab.Mines)
            );
        }

        [TestCase(ManufacturingType.Ship, FacilityWindowTab.Shipyards)]
        [TestCase(ManufacturingType.Troop, FacilityWindowTab.Training)]
        [TestCase(ManufacturingType.Building, FacilityWindowTab.Construction)]
        public void GetTab_ManufacturingType_ReturnsMappedTab(
            ManufacturingType type,
            FacilityWindowTab expected
        )
        {
            Assert.AreEqual(expected, FacilityManufacturingLaneCatalog.GetTab(type));
        }

        [Test]
        public void GetTab_UnsupportedManufacturingType_ReturnsNull()
        {
            Assert.IsNull(FacilityManufacturingLaneCatalog.GetTab(ManufacturingType.None));
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
