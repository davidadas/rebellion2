using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class RectangleLayoutTests
    {
        [Test]
        public void Arrange_ClearItems_PreservesEveryCoordinate()
        {
            RectangleLayoutItem[] items =
            {
                Item(10, 10, 10, 10),
                Item(50, 10, 10, 10),
                Item(10, 50, 10, 10),
            };
            Vector2Int[] result = RectangleLayout.Arrange(
                items,
                new Rect(0, 0, 100, 100),
                new Vector2(2, 2),
                Vector2.one
            );
            CollectionAssert.AreEqual(items.Select(item => item.Position), result);
        }

        [Test]
        public void Arrange_SingleOverlap_SharesTheSmallestRequiredDisplacement()
        {
            RectangleLayoutItem[] items =
            {
                Item(40, 40, 10, 30),
                Item(46, 40, 10, 30),
                Item(80, 80, 10, 10),
            };
            Vector2Int[] result = RectangleLayout.Arrange(
                items,
                new Rect(0, 0, 100, 100),
                Vector2.zero,
                Vector2.one
            );
            Assert.AreEqual(new Vector2Int(38, 40), result[0]);
            Assert.AreEqual(new Vector2Int(48, 40), result[1]);
            Assert.AreEqual(items[2].Position, result[2]);
        }

        [Test]
        public void Arrange_OverlapAtFrameEdge_MovesOnlyIntoTheAvailableSpace()
        {
            RectangleLayoutItem[] items = { Item(0, 5, 10, 20), Item(2, 5, 10, 20) };
            Rect bounds = new(0, 0, 30, 40);
            Vector2 gap = new(2, 2);
            Vector2Int[] result = RectangleLayout.Arrange(items, bounds, gap, Vector2.one);
            Assert.AreEqual(new Vector2Int(0, 5), result[0]);
            Assert.AreEqual(new Vector2Int(12, 5), result[1]);
            AssertClear(items, result, bounds, gap);
        }

        [Test]
        public void Arrange_ClippedItem_ClampsToTheNearestValidAnchor()
        {
            Vector2Int[] result = RectangleLayout.Arrange(
                new[] { Item(-20, 200, 10, 10) },
                new Rect(0, 0, 100, 100),
                Vector2.zero,
                Vector2.one
            );
            Assert.AreEqual(new Vector2Int(0, 90), result[0]);
        }

        [Test]
        public void Arrange_UnequalCoordinateScales_PrefersTheSmallerVisualMove()
        {
            RectangleLayoutItem[] items = { Item(0, 0, 4, 4), Item(2, 2, 4, 4) };
            Vector2Int[] result = RectangleLayout.Arrange(
                items,
                new Rect(-10, -10, 40, 40),
                Vector2.zero,
                new Vector2(3, 1)
            );
            Assert.AreEqual(new Vector2Int(0, -1), result[0]);
            Assert.AreEqual(new Vector2Int(2, 3), result[1]);
        }

        [Test]
        public void Arrange_InterlockingCompoundFootprints_PreservesTheOpenSpaceBetweenStars()
        {
            RectangleLayoutItem[] items =
            {
                new(new Vector2Int(20, 20), new[] { new Rect(0, 0, 4, 4), new Rect(0, 16, 4, 4) }),
                Item(20, 28, 4, 4),
            };
            Vector2Int[] result = RectangleLayout.Arrange(
                items,
                new Rect(0, 0, 100, 100),
                new Vector2(2, 2),
                Vector2.one
            );
            CollectionAssert.AreEqual(items.Select(item => item.Position), result);
        }

        [Test]
        public void Arrange_OverlappingLabels_ClearsAllShapesWithLimitedDisplacement()
        {
            Rect[] shapes = { new(0, 0, 10, 10), new(-10, 10, 30, 8) };
            RectangleLayoutItem[] items =
            {
                new(new Vector2Int(30, 30), shapes),
                new(new Vector2Int(50, 30), shapes),
                Item(85, 85, 10, 10),
            };
            Rect bounds = new(0, 0, 100, 100);
            Vector2 gap = new(2, 2);
            Vector2Int[] result = RectangleLayout.Arrange(items, bounds, gap, Vector2.one);
            AssertClear(items, result, bounds, gap);
            Assert.LessOrEqual(
                result.Select((point, i) => (point - items[i].Position).sqrMagnitude).Sum(),
                72
            );
            Assert.AreEqual(items[2].Position, result[2]);
        }

        [Test]
        public void Arrange_CoincidentTenItems_ReturnsACompleteDeterministicAndIdempotentLayout()
        {
            RectangleLayoutItem[] items = Enumerable
                .Range(0, 10)
                .Select(_ => Item(80, 120, 55, 60))
                .ToArray();
            Rect bounds = new(0, 0, 220, 330);
            Vector2 gap = new(2, 2);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            Vector2Int[] result = RectangleLayout.Arrange(
                items,
                bounds,
                gap,
                Vector2.one,
                timeout.Token
            );
            AssertClear(items, result, bounds, gap);
            CollectionAssert.AreEqual(
                result,
                RectangleLayout.Arrange(items, bounds, gap, Vector2.one, timeout.Token)
            );
            RectangleLayoutItem[] arranged = items
                .Select((item, i) => new RectangleLayoutItem(result[i], item.Footprints))
                .ToArray();
            CollectionAssert.AreEqual(
                result,
                RectangleLayout.Arrange(arranged, bounds, gap, Vector2.one, timeout.Token)
            );
        }

        [Test]
        public void Arrange_IntegerSeparation_RoundingNeverReintroducesAnOverlap()
        {
            RectangleLayoutItem[] items = Enumerable
                .Range(0, 7)
                .Select(i => Item(i + 20, 20, 5, 30))
                .ToArray();
            Rect bounds = new(0, 0, 100, 100);
            Vector2Int[] result = RectangleLayout.Arrange(
                items,
                bounds,
                new Vector2(2, 2),
                Vector2.one
            );
            AssertClear(items, result, bounds, new Vector2(2, 2));
        }

        [Test]
        public void Arrange_CompoundItemsAtClippedCorner_ClearsFractionalFootprintsOnTheSavedGrid()
        {
            Rect[] shapes =
            {
                new(-0.21f, -0.12f, 8.15f, 4.68f),
                new(-2.35f, -0.24f, 6f, 2.34f),
                new(3.86f, -0.24f, 6.22f, 2.34f),
                new(-2.35f, 2.21f, 6f, 2.46f),
                new(3.65f, 2.21f, 6.22f, 2.46f),
                new(-0.21f, 4.55f, 13.08f, 0.49f),
                new(-0.21f, 5.05f, 13.08f, 0.49f),
                new(-0.21f, 5.54f, 8.15f, 0.49f),
                new(-2.2f, 5.91f, 12.1f, 1.64f),
            };
            RectangleLayoutItem[] items = Enumerable
                .Range(0, 10)
                .Select(_ => new RectangleLayoutItem(new Vector2Int(100, 200), shapes))
                .ToArray();
            Rect bounds = new(100.4f, 203.38f, 47.6f, 39.49f);
            Vector2 gap = new(0.42f, 0.24f);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            Vector2Int[] result = RectangleLayout.Arrange(
                items,
                bounds,
                gap,
                new Vector2(4.6f, 8.1f),
                timeout.Token
            );
            AssertClear(items, result, bounds, gap);
        }

        [Test]
        public void Arrange_CompoundAreaExceedsTheFrame_RejectsAnImpossibleRequest()
        {
            RectangleLayoutItem[] items = Enumerable
                .Range(0, 20)
                .Select(_ => Item(0, 0, 10, 10))
                .ToArray();
            Assert.Throws<ArgumentException>(() =>
                RectangleLayout.Arrange(items, new Rect(0, 0, 30, 30), Vector2.zero, Vector2.one)
            );
        }

        [Test]
        public void Arrange_ImpossibleFrame_RejectsTheLayoutWithoutChangingInputs()
        {
            RectangleLayoutItem[] items = { Item(0, 0, 6, 6), Item(1, 1, 6, 6) };
            Assert.Throws<ArgumentException>(() =>
                RectangleLayout.Arrange(items, new Rect(0, 0, 10, 10), Vector2.zero, Vector2.one)
            );
            Assert.AreEqual(new Vector2Int(1, 1), items[1].Position);
        }

        [Test]
        public void Arrange_CancelledOperation_DoesNotReturnPartialCoordinates()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                RectangleLayout.Arrange(
                    new[] { Item(1, 1, 10, 10) },
                    new Rect(0, 0, 100, 100),
                    Vector2.zero,
                    Vector2.one,
                    cancellation.Token
                )
            );
        }

        [Test]
        public void Arrange_EmptyMap_ReturnsAnEmptyLayout()
        {
            Assert.IsEmpty(
                RectangleLayout.Arrange(
                    Array.Empty<RectangleLayoutItem>(),
                    new Rect(0, 0, 100, 100),
                    Vector2.zero,
                    Vector2.one
                )
            );
        }

        [TestCase(1729)]
        [TestCase(606)]
        [TestCase(42)]
        public void Arrange_CrowdedRandomMaps_PreservesBoundsAndClearance(int seed)
        {
            System.Random random = new(seed);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            for (int sample = 0; sample < 20; sample++)
            {
                RectangleLayoutItem[] items = Enumerable
                    .Range(0, 12)
                    .Select(_ =>
                        Item(
                            random.Next(25, 75),
                            random.Next(25, 75),
                            random.Next(5, 14),
                            random.Next(5, 14)
                        )
                    )
                    .ToArray();
                Rect bounds = new(0, 0, 100, 100);
                Vector2Int[] result = RectangleLayout.Arrange(
                    items,
                    bounds,
                    new Vector2(2, 2),
                    Vector2.one,
                    timeout.Token
                );
                AssertClear(items, result, bounds, new Vector2(2, 2));
            }
        }

        /// <summary>
        /// Creates one rectangular fixture footprint.
        /// </summary>
        /// <param name="x">The desired horizontal anchor.</param>
        /// <param name="y">The desired vertical anchor.</param>
        /// <param name="width">The footprint width.</param>
        /// <param name="height">The footprint height.</param>
        /// <returns>The captured fixture item.</returns>
        private static RectangleLayoutItem Item(int x, int y, int width, int height) =>
            new(new Vector2Int(x, y), new[] { new Rect(0, 0, width, height) });

        /// <summary>
        /// Checks the returned lattice positions against every occupied shape and the frame.
        /// </summary>
        /// <param name="items">The requested footprints.</param>
        /// <param name="positions">The returned anchors.</param>
        /// <param name="bounds">The usable frame.</param>
        /// <param name="gap">The required clearance.</param>
        private static void AssertClear(
            RectangleLayoutItem[] items,
            Vector2Int[] positions,
            Rect bounds,
            Vector2 gap
        )
        {
            for (int i = 0; i < items.Length; i++)
                foreach (Rect shape in items[i].Footprints)
                {
                    Rect first = shape;
                    first.position += (Vector2)positions[i];
                    Assert.GreaterOrEqual(first.xMin, bounds.xMin - 0.0001f);
                    Assert.GreaterOrEqual(first.yMin, bounds.yMin - 0.0001f);
                    Assert.LessOrEqual(first.xMax, bounds.xMax + 0.0001f);
                    Assert.LessOrEqual(first.yMax, bounds.yMax + 0.0001f);
                    for (int j = i + 1; j < items.Length; j++)
                        foreach (Rect other in items[j].Footprints)
                        {
                            Rect second = other;
                            second.position += (Vector2)positions[j];
                            Assert.IsTrue(
                                first.xMax + gap.x <= second.xMin + 0.0001f
                                    || second.xMax + gap.x <= first.xMin + 0.0001f
                                    || first.yMax + gap.y <= second.yMin + 0.0001f
                                    || second.yMax + gap.y <= first.yMin + 0.0001f,
                                $"Items {i} and {j} overlap: {first}, {second}."
                            );
                        }
                }
        }
    }
}
