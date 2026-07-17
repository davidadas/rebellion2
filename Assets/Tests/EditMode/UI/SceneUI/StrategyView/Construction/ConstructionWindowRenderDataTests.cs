using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionWindowRenderDataTests
    {
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
                false,
                null,
                false,
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

        [Test]
        public void Constructor_NullDropdownItems_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CreateRenderData(null));
        }

        [Test]
        public void ConstructionBuildEstimate_Constructor_StoresDurations()
        {
            ConstructionBuildEstimate estimate = new ConstructionBuildEstimate(12, 34);

            Assert.AreEqual(12, estimate.CompletionTicks);
            Assert.AreEqual(34, estimate.DeploymentTicks);
        }

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
                true,
                "5",
                true,
                false,
                true,
                items
            );
        }
    }
}
