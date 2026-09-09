using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Util.Common
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
    }
}
