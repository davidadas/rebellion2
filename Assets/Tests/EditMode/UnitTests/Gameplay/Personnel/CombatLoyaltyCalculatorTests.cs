using System;
using NUnit.Framework;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class CombatLoyaltyCalculatorTests
    {
        [TestCase(40, 80, 80, 1)]
        [TestCase(80, 40, 80, 0)]
        [TestCase(81, 0, 80, -1)]
        [TestCase(0, 81, 80, 2)]
        [TestCase(0, -15, 10, -2)]
        [TestCase(-101, 99, 10, 29)]
        [TestCase(40, 80, -80, -1)]
        [TestCase(0, int.MinValue + 1, -1, -2)]
        [TestCase(int.MinValue, int.MinValue, int.MinValue, 1)]
        public void Calculate_FractionalLossWeights_TruncatesEachDivisionTowardZero(
            int favoredLoss,
            int opposingLoss,
            int divisor,
            int expected
        )
        {
            Assert.AreEqual(
                expected,
                CombatLoyaltyCalculator.Calculate(favoredLoss, opposingLoss, divisor)
            );
        }

        [TestCase(int.MinValue, 0, int.MinValue)]
        [TestCase(0, int.MaxValue, -2)]
        [TestCase(int.MinValue, int.MaxValue, int.MaxValue - 1)]
        [TestCase(int.MaxValue, int.MinValue, int.MinValue + 1)]
        public void Calculate_OverflowingLossWeights_WrapsDifferenceAndFinalSum(
            int favoredLoss,
            int opposingLoss,
            int expected
        )
        {
            Assert.AreEqual(
                expected,
                CombatLoyaltyCalculator.Calculate(favoredLoss, opposingLoss, 1)
            );
        }

        [Test]
        public void Calculate_ZeroDivisor_PropagatesDivisionFault()
        {
            Assert.Throws<DivideByZeroException>(() => CombatLoyaltyCalculator.Calculate(0, 0, 0));
        }

        [TestCase(int.MinValue, 0)]
        [TestCase(0, int.MinValue)]
        [TestCase(int.MinValue, int.MinValue)]
        public void Calculate_UnrepresentableQuotient_PropagatesDivisionFault(
            int favoredLoss,
            int opposingLoss
        )
        {
            Assert.Throws<OverflowException>(() =>
                CombatLoyaltyCalculator.Calculate(favoredLoss, opposingLoss, -1)
            );
        }
    }
}
