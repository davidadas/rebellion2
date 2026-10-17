using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Util.Random
{
    [TestFixture]
    public class ProbabilityTableTests
    {
        [Test]
        public void Lookup_ValueBetweenThresholds_ReturnsPreviousThresholdValue()
        {
            ProbabilityTable table = new ProbabilityTable(
                new Dictionary<int, int>
                {
                    { -20, 99 },
                    { -19, 98 },
                    { -9, 96 },
                    { 1, 94 },
                }
            );

            Assert.AreEqual(98, table.Lookup(-10));
            Assert.AreEqual(96, table.Lookup(0));
        }

        [Test]
        public void Lookup_ValueBelowLowestThreshold_ReturnsLowestThresholdValue()
        {
            ProbabilityTable table = new ProbabilityTable(
                new Dictionary<int, int> { { 10, 20 }, { 20, 40 } }
            );

            Assert.AreEqual(20, table.Lookup(0));
        }

        [Test]
        public void Lookup_EmptyTable_ReturnsZero()
        {
            ProbabilityTable table = new ProbabilityTable(new Dictionary<int, int>());

            Assert.AreEqual(0, table.Lookup(50));
        }

        [TestCase(int.MinValue, 17)]
        [TestCase(-101, 17)]
        [TestCase(-100, 29)]
        [TestCase(0, 29)]
        [TestCase(10, 41)]
        [TestCase(int.MaxValue, 53)]
        public void Lookup_UnsortedExtremeThresholds_ReturnsMatchingValue(int value, int expected)
        {
            ProbabilityTable table = new ProbabilityTable(
                new Dictionary<int, int>
                {
                    { 10, 41 },
                    { int.MaxValue, 53 },
                    { int.MinValue, 17 },
                    { -100, 29 },
                }
            );

            Assert.AreEqual(expected, table.Lookup(value));
        }

        [Test]
        public void Lookup_MaximumOnlyThreshold_ReturnsConfiguredValue()
        {
            ProbabilityTable table = new ProbabilityTable(
                new Dictionary<int, int> { { int.MaxValue, 53 } }
            );

            Assert.AreEqual(53, table.Lookup(int.MinValue));
        }

        [Test]
        public void Lookup_EditedEntries_ReflectsLatestThresholdsAndValues()
        {
            Dictionary<int, int> entries = new Dictionary<int, int> { { 10, 20 }, { 20, 40 } };
            ProbabilityTable table = new ProbabilityTable(entries);
            Assert.AreEqual(20, table.Lookup(15));

            entries.Remove(10);
            entries[20] = 90;
            entries[5] = 30;

            Assert.AreEqual(30, table.Lookup(15));
            Assert.AreEqual(90, table.Lookup(20));
        }
    }
}
