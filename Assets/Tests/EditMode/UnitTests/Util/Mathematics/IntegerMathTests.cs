using NUnit.Framework;
using Rebellion.Util.Mathematics;

namespace Rebellion.Tests.Util.Mathematics
{
    [TestFixture]
    public sealed class IntegerMathTests
    {
        [Test]
        public void ScaleByPercent_ResultExceedsIntegerMaximum_SaturatesAtMaximum()
        {
            Assert.AreEqual(int.MaxValue, IntegerMath.ScaleByPercent(int.MaxValue, int.MaxValue));
        }

        [Test]
        public void ScaleByPercentRoundedUp_ResultExceedsIntegerMaximum_SaturatesAtMaximum()
        {
            Assert.AreEqual(
                int.MaxValue,
                IntegerMath.ScaleByPercentRoundedUp(int.MaxValue, int.MaxValue)
            );
        }

        [Test]
        public void SaturatingAdd_ResultOutsideIntegerRange_SaturatesAtNearestBound()
        {
            Assert.AreEqual(int.MaxValue, IntegerMath.SaturatingAdd(int.MaxValue, 1));
            Assert.AreEqual(int.MinValue, IntegerMath.SaturatingAdd(int.MinValue, -1));
        }

        [Test]
        public void SaturatingMultiply_ResultOutsideIntegerRange_SaturatesAtNearestBound()
        {
            Assert.AreEqual(int.MaxValue, IntegerMath.SaturatingMultiply(int.MaxValue, 2));
            Assert.AreEqual(int.MinValue, IntegerMath.SaturatingMultiply(int.MinValue, 2));
        }

        [Test]
        public void SaturateToInt_LongOutsideIntegerRange_SaturatesAtNearestBound()
        {
            Assert.AreEqual(int.MaxValue, IntegerMath.SaturateToInt((long)int.MaxValue + 1));
            Assert.AreEqual(int.MinValue, IntegerMath.SaturateToInt((long)int.MinValue - 1));
        }
    }
}
