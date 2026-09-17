using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Fleet
{
    [TestFixture]
    public class FleetWindowRenderDataTests
    {
        private Texture2D _firstTexture;
        private Texture2D _secondTexture;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _firstTexture = new Texture2D(1, 1);
            _secondTexture = new Texture2D(1, 1);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_firstTexture);
            UnityEngine.Object.DestroyImmediate(_secondTexture);
        }

        /// <summary>
        /// Verifies ordered tabs default catalog returns authored immutable order.
        /// </summary>
        [Test]
        public void OrderedTabs_DefaultCatalog_ReturnsAuthoredImmutableOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    FleetWindowTab.CapitalShips,
                    FleetWindowTab.Starfighters,
                    FleetWindowTab.Regiments,
                    FleetWindowTab.Personnel,
                },
                FleetWindowRenderData.OrderedTabs
            );
            Assert.AreEqual(4, FleetWindowRenderData.TabCount);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<FleetWindowTab>)FleetWindowRenderData.OrderedTabs)[0] =
                    FleetWindowTab.Personnel
            );
        }

        /// <summary>
        /// Verifies fleet list row render data null name normalizes to empty string.
        /// </summary>
        [Test]
        public void FleetListRowRenderData_NullName_NormalizesToEmptyString()
        {
            FleetListRowRenderData data = new FleetListRowRenderData(
                null,
                _firstTexture,
                _secondTexture,
                _firstTexture,
                _secondTexture,
                _firstTexture,
                _secondTexture,
                _firstTexture
            );

            Assert.AreEqual(string.Empty, data.Name);
        }

        /// <summary>
        /// Verifies constructor null required collection throws argument null exception.
        /// </summary>
        /// <param name="index">The index.</param>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Constructor_NullRequiredCollection_ThrowsArgumentNullException(int index)
        {
            IReadOnlyList<FleetListRowRenderData> rows =
                index == 0 ? null : new FleetListRowRenderData[0];
            IReadOnlyList<FleetWindowTabRenderData> tabs =
                index == 1 ? null : new FleetWindowTabRenderData[0];
            IReadOnlyList<StrategyUnitCardRenderData> items =
                index == 2 ? null : new StrategyUnitCardRenderData[0];

            Assert.Throws<ArgumentNullException>(() => CreateRenderData(rows, tabs, items));
        }

        /// <summary>
        /// Verifies constructor source collections change preserves complete normalized snapshot.
        /// </summary>
        [Test]
        public void Constructor_SourceCollectionsChange_PreservesCompleteNormalizedSnapshot()
        {
            FleetListRowRenderData row = new FleetListRowRenderData(
                "Fleet",
                _firstTexture,
                null,
                null,
                null,
                null,
                null,
                _secondTexture
            );
            FleetWindowTabRenderData tab = new FleetWindowTabRenderData(
                FleetWindowTab.CapitalShips,
                _firstTexture,
                _secondTexture
            );
            StrategyUnitCardRenderData item = CreateUnitCard();
            FleetListRowRenderData[] rows = { row };
            FleetWindowTabRenderData[] tabs = { tab };
            StrategyUnitCardRenderData[] items = { item };

            FleetWindowRenderData data = new FleetWindowRenderData(
                1,
                2,
                _firstTexture,
                null,
                _secondTexture,
                rows,
                FleetWindowTab.Personnel,
                3,
                true,
                _firstTexture,
                _secondTexture,
                _firstTexture,
                null,
                new Color32(1, 2, 3, 4),
                true,
                null,
                null,
                tabs,
                items,
                5,
                6,
                null
            );
            rows[0] = null;
            tabs[0] = null;
            items[0] = null;

            Assert.AreEqual(1, data.X);
            Assert.AreEqual(2, data.Y);
            Assert.AreSame(_firstTexture, data.TitleTexture);
            Assert.AreEqual(string.Empty, data.Caption);
            Assert.AreSame(_secondTexture, data.DetailBackgroundTexture);
            Assert.AreSame(row, data.FleetRows[0]);
            Assert.AreEqual(FleetWindowTab.Personnel, data.ActiveTab);
            Assert.AreEqual(3, data.SelectedFleetIndex);
            Assert.IsTrue(data.HasSelectedFleet);
            Assert.AreSame(_firstTexture, data.BannerTexture);
            Assert.AreSame(_secondTexture, data.BannerEnrouteOverlayTexture);
            Assert.AreSame(_firstTexture, data.BannerDamagedOverlayTexture);
            Assert.AreEqual(string.Empty, data.FleetName);
            Assert.AreEqual(new Color32(1, 2, 3, 4), data.FleetNameColor);
            Assert.IsTrue(data.ShowCapacity);
            Assert.AreEqual(string.Empty, data.CapacityLeft);
            Assert.AreEqual(string.Empty, data.CapacityRight);
            Assert.AreSame(tab, data.Tabs[0]);
            Assert.AreSame(item, data.DetailItems[0]);
            Assert.AreEqual(5, data.RenameFleetRowIndex);
            Assert.AreEqual(6, data.RenameDetailItemIndex);
            Assert.AreEqual(string.Empty, data.RenameText);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<FleetListRowRenderData>)data.FleetRows)[0] = null
            );
        }

        /// <summary>
        /// Creates render data.
        /// </summary>
        /// <param name="rows">The rows.</param>
        /// <param name="tabs">The tabs.</param>
        /// <param name="items">The items.</param>
        /// <returns>The created render data.</returns>
        private FleetWindowRenderData CreateRenderData(
            IReadOnlyList<FleetListRowRenderData> rows,
            IReadOnlyList<FleetWindowTabRenderData> tabs,
            IReadOnlyList<StrategyUnitCardRenderData> items
        )
        {
            return new FleetWindowRenderData(
                0,
                0,
                null,
                null,
                null,
                rows,
                FleetWindowTab.CapitalShips,
                -1,
                false,
                null,
                null,
                null,
                null,
                default,
                false,
                null,
                null,
                tabs,
                items,
                -1,
                -1,
                null
            );
        }

        /// <summary>
        /// Creates unit card.
        /// </summary>
        /// <returns>The created unit card.</returns>
        private StrategyUnitCardRenderData CreateUnitCard()
        {
            return new StrategyUnitCardRenderData(
                "Unit",
                Color.white,
                true,
                false,
                _firstTexture,
                null,
                null,
                _secondTexture,
                null,
                null,
                0,
                null,
                null,
                null,
                true
            );
        }
    }
}
