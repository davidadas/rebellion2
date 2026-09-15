using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionWindowRenderDataTests
    {
        /// <summary>
        /// Verifies constructor mutable dropdown items copies input collection.
        /// </summary>
        [Test]
        public void Constructor_MutableDropdownItems_CopiesInputCollection()
        {
            List<StrategyDropdownItemRenderData> items = new List<StrategyDropdownItemRenderData>
            {
                new StrategyDropdownItemRenderData(null, "item", Color.white),
            };
            ConstructionWindowRenderData data = CreateRenderData(items);

            items.Clear();

            Assert.AreEqual(1, data.DropdownItems.Count);
            Assert.IsTrue(data.HasSelection);
        }

        /// <summary>
        /// Verifies constructor null text normalizes text to empty strings.
        /// </summary>
        [Test]
        public void Constructor_NullText_NormalizesTextToEmptyStrings()
        {
            ConstructionWindowRenderData data = new ConstructionWindowRenderData(
                1,
                2,
                null,
                null,
                null,
                3,
                null,
                null,
                null,
                null,
                false,
                false,
                Array.Empty<StrategyDropdownItemRenderData>()
            );

            Assert.AreEqual(1, data.X);
            Assert.AreEqual(2, data.Y);
            Assert.AreEqual(3, data.BuildCount);
            Assert.AreEqual(string.Empty, data.SelectedName);
            Assert.AreEqual(string.Empty, data.ConstructionCost);
            Assert.AreEqual(string.Empty, data.MaintenanceCost);
            Assert.AreEqual(string.Empty, data.CompletionEstimate);
            Assert.AreEqual(string.Empty, data.DeploymentEstimate);
            Assert.IsFalse(data.HasSelection);
        }

        /// <summary>
        /// Verifies constructor null dropdown items throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullDropdownItems_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CreateRenderData(null));
        }

        /// <summary>
        /// Creates render data.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <returns>The created render data.</returns>
        private static ConstructionWindowRenderData CreateRenderData(
            IReadOnlyList<StrategyDropdownItemRenderData> items
        )
        {
            return new ConstructionWindowRenderData(
                0,
                0,
                null,
                null,
                "selected",
                1,
                "2",
                "3",
                "4",
                "5",
                false,
                true,
                items
            );
        }
    }
}
