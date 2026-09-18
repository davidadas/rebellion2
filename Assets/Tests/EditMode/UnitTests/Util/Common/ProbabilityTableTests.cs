using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Util.Common
{
    [TestFixture]
    public class ProbabilityTableTests
    {
        /// <summary>
        /// Verifies lookup value between thresholds returns previous threshold value.
        /// </summary>
        [Test]
        public void Lookup_ValueBetweenThresholds_ReturnsPreviousThresholdValue()
        {
            ProbabilityTable table = new ProbabilityTable(
                new Dictionary<int, int>
                {
                    { 1, 94 },
                    { -9, 96 },
                    { -20, 99 },
                    { -19, 98 },
                }
            );

            Assert.AreEqual(98, table.Lookup(-10));
            Assert.AreEqual(96, table.Lookup(0));
        }

        /// <summary>
        /// Verifies lookup value below lowest threshold returns lowest threshold value.
        /// </summary>
        [Test]
        public void Lookup_ValueBelowLowestThreshold_ReturnsLowestThresholdValue()
        {
            ProbabilityTable table = new ProbabilityTable(
                new Dictionary<int, int> { { 10, 20 }, { 20, 40 } }
            );

            Assert.AreEqual(20, table.Lookup(0));
        }

        /// <summary>
        /// Verifies lookup empty table returns zero.
        /// </summary>
        [Test]
        public void Lookup_EmptyTable_ReturnsZero()
        {
            ProbabilityTable table = new ProbabilityTable(new Dictionary<int, int>());

            Assert.AreEqual(0, table.Lookup(50));
        }
    }
}
