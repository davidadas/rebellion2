using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class SpaceCombatSupportTests
    {
        private static readonly SpaceCombatSupportSideConfig _firstWeights = Weights(
            100,
            100,
            100,
            50,
            1000,
            17
        );
        private static readonly SpaceCombatSupportSideConfig _secondWeights = Weights(
            80,
            40,
            60,
            20,
            2000,
            83
        );

        [TestCase(200, 100, 1, 6u, 2, 83u)]
        [TestCase(200, 100, 2, 19u, 1, 17u)]
        [TestCase(100, 200, 1, 17u, 2, 83u)]
        [TestCase(100, 200, 2, 10u, 1, 17u)]
        public void TryCalculate_UnequalTotals_UsesEachSidesSelectedProfile(
            int firstTotal,
            int secondTotal,
            int survivingSide,
            uint expectedMagnitude,
            int expectedComparison,
            uint expectedThreshold
        )
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                firstTotal,
                secondTotal,
                20,
                30,
                survivingSide,
                _firstWeights,
                _secondWeights,
                2
            );

            Assert.AreEqual(survivingSide, adjustment.BeneficiarySide);
            Assert.AreEqual(expectedMagnitude, adjustment.Magnitude);
            Assert.AreEqual(expectedComparison, adjustment.ComparisonSide);
            Assert.AreEqual(expectedThreshold, adjustment.SupportThreshold);
        }

        [TestCase(1, 9u, 2, 83u)]
        [TestCase(2, 18u, 1, 17u)]
        public void TryCalculate_EqualTotals_SelectsSecondSideAsLeader(
            int survivingSide,
            uint expectedMagnitude,
            int expectedComparison,
            uint expectedThreshold
        )
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                100,
                100,
                20,
                30,
                survivingSide,
                _firstWeights,
                _secondWeights,
                2
            );

            Assert.AreEqual(survivingSide, adjustment.BeneficiarySide);
            Assert.AreEqual(expectedMagnitude, adjustment.Magnitude);
            Assert.AreEqual(expectedComparison, adjustment.ComparisonSide);
            Assert.AreEqual(expectedThreshold, adjustment.SupportThreshold);
        }

        [TestCase(200, 100, 60, 1, 1, 2, 11u, 1, 17u)]
        [TestCase(100, 200, 1, 60, 2, 1, 7u, 2, 83u)]
        public void TryCalculate_NegativeResult_UsesFlippedBeneficiaryCapAndCounterpartThreshold(
            int firstTotal,
            int secondTotal,
            int firstLoss,
            int secondLoss,
            int survivingSide,
            int expectedBeneficiary,
            uint expectedMagnitude,
            int expectedComparison,
            uint expectedThreshold
        )
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                firstTotal,
                secondTotal,
                firstLoss,
                secondLoss,
                survivingSide,
                FlatWeights(100, 7, 17),
                FlatWeights(100, 11, 83)
            );

            Assert.AreEqual(expectedBeneficiary, adjustment.BeneficiarySide);
            Assert.AreEqual(expectedMagnitude, adjustment.Magnitude);
            Assert.AreEqual(expectedComparison, adjustment.ComparisonSide);
            Assert.AreEqual(expectedThreshold, adjustment.SupportThreshold);
        }

        [TestCase(1, 0, 99, 7u)]
        [TestCase(2, 99, 0, 11u)]
        public void TryCalculate_PositiveResult_UsesSurvivingSidesCap(
            int survivingSide,
            int firstLoss,
            int secondLoss,
            uint expectedMagnitude
        )
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                survivingSide == 1 ? 200 : 100,
                survivingSide == 2 ? 200 : 100,
                firstLoss,
                secondLoss,
                survivingSide,
                FlatWeights(100, 7),
                FlatWeights(100, 11)
            );

            Assert.AreEqual(survivingSide, adjustment.BeneficiarySide);
            Assert.AreEqual(expectedMagnitude, adjustment.Magnitude);
        }

        [TestCase(0, 0)]
        [TestCase(0, -7)]
        [TestCase(-7, 0)]
        public void TryCalculate_ZeroLeadingTotal_LeavesBothWeightsZero(
            int firstTotal,
            int secondTotal
        )
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                firstTotal,
                secondTotal,
                int.MaxValue,
                int.MinValue,
                1,
                _firstWeights,
                _secondWeights
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(0u, adjustment.Magnitude);
            Assert.AreEqual(2, adjustment.ComparisonSide);
            Assert.AreEqual(83u, adjustment.SupportThreshold);
        }

        [Test]
        public void TryCalculate_FractionalGapWeight_TruncatesBeforeApplyingLosses()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                3,
                2,
                100,
                100,
                1,
                Weights(0, 50, 0, 0, int.MaxValue, 0),
                FlatWeights(100)
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(184u, adjustment.Magnitude);
        }

        [Test]
        public void TryCalculate_FractionalWeightedLosses_TruncatesEachSideBeforeCombining()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                2,
                1,
                9,
                9,
                1,
                FlatWeights(11),
                FlatWeights(13)
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(2u, adjustment.Magnitude);
        }

        [Test]
        public void TryCalculate_NegativeWeightedLoss_TruncatesTowardZero()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                2,
                1,
                3,
                0,
                1,
                FlatWeights(-50),
                FlatWeights(0)
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(1u, adjustment.Magnitude);
        }

        [TestCase(13, 4, 2, 2, 2u)]
        [TestCase(4, 13, 3, 1, 7u)]
        [TestCase(4, 13, -3, 2, 7u)]
        public void TryCalculate_FractionalFinalQuotient_TruncatesTowardZero(
            int firstLoss,
            int secondLoss,
            int divisor,
            int expectedBeneficiary,
            uint expectedMagnitude
        )
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                2,
                1,
                firstLoss,
                secondLoss,
                1,
                FlatWeights(100),
                FlatWeights(100),
                divisor
            );

            Assert.AreEqual(expectedBeneficiary, adjustment.BeneficiarySide);
            Assert.AreEqual(expectedMagnitude, adjustment.Magnitude);
        }

        [Test]
        public void TryCalculate_GapProductOverflow_UsesLow32ProductBits()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                int.MaxValue,
                0,
                100,
                100,
                1,
                Weights(100, 2, 0, 0, int.MaxValue, 0),
                FlatWeights(100)
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(100u, adjustment.Magnitude);
        }

        [Test]
        public void TryCalculate_TotalGapOverflow_UsesLow32DifferenceBits()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                int.MaxValue,
                -1,
                100,
                100,
                1,
                Weights(100, 1, 0, 0, int.MaxValue, 0),
                FlatWeights(100)
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(101u, adjustment.Magnitude);
        }

        [Test]
        public void TryCalculate_LeadingWeightOverflow_UsesLow32SumBits()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                2,
                0,
                1,
                0,
                1,
                Weights(int.MaxValue, 1, 0, 0, int.MaxValue, 0),
                FlatWeights(0)
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(21474836u, adjustment.Magnitude);
        }

        [Test]
        public void TryCalculate_TrailingWeightOverflow_UsesLow32SumBits()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                2,
                1,
                0,
                1,
                1,
                FlatWeights(0),
                Weights(0, 0, int.MaxValue, 1, int.MaxValue, 0)
            );

            Assert.AreEqual(2, adjustment.BeneficiarySide);
            Assert.AreEqual(42949672u, adjustment.Magnitude);
        }

        [Test]
        public void TryCalculate_WeightedLossProductOverflow_UsesLow32ProductBits()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                2,
                1,
                int.MaxValue,
                0,
                1,
                FlatWeights(100),
                FlatWeights(0)
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(1u, adjustment.Magnitude);
        }

        [TestCase(1, 2, uint.MaxValue)]
        [TestCase(2, 1, 0u)]
        public void TryCalculate_ZeroLosses_PreservesCounterpartThresholdBits(
            int survivingSide,
            int expectedComparison,
            uint expectedThreshold
        )
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                1,
                1,
                0,
                0,
                survivingSide,
                FlatWeights(100, int.MaxValue, 0),
                FlatWeights(100, int.MaxValue, uint.MaxValue)
            );

            Assert.AreEqual(survivingSide, adjustment.BeneficiarySide);
            Assert.AreEqual(0u, adjustment.Magnitude);
            Assert.AreEqual(expectedComparison, adjustment.ComparisonSide);
            Assert.AreEqual(expectedThreshold, adjustment.SupportThreshold);
        }

        [Test]
        public void TryCalculate_NegativeCap_PreservesSignedClampOrderAndUnsignedResultBits()
        {
            SpaceCombatSupport.Adjustment adjustment = Calculate(
                2,
                1,
                0,
                1,
                1,
                FlatWeights(100, -7),
                FlatWeights(100)
            );

            Assert.AreEqual(1, adjustment.BeneficiarySide);
            Assert.AreEqual(4294967289u, adjustment.Magnitude);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(3)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void TryCalculate_InvalidSurvivor_ReturnsNoRuleWithoutDividing(int survivingSide)
        {
            bool calculated = SpaceCombatSupport.TryCalculate(
                1,
                1,
                1,
                1,
                survivingSide,
                _firstWeights,
                _secondWeights,
                0,
                out SpaceCombatSupport.Adjustment adjustment
            );

            Assert.IsFalse(calculated);
            Assert.AreEqual(0, adjustment.BeneficiarySide);
            Assert.AreEqual(0u, adjustment.Magnitude);
            Assert.AreEqual(0, adjustment.ComparisonSide);
            Assert.AreEqual(0u, adjustment.SupportThreshold);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TryCalculate_ZeroDivisor_ThrowsWithoutFabricatingAMagnitude(int total)
        {
            Assert.Throws<DivideByZeroException>(() =>
                Calculate(total, total, 0, 0, 1, _firstWeights, _secondWeights, 0)
            );
        }

        [Test]
        public void TryCalculate_UnrepresentableGapQuotient_ThrowsWithoutFabricatingAMagnitude()
        {
            Assert.Throws<OverflowException>(() =>
                Calculate(
                    -1,
                    -2,
                    0,
                    0,
                    1,
                    Weights(0, int.MinValue, 0, 0, int.MaxValue, 0),
                    FlatWeights(0)
                )
            );
        }

        /// <summary>
        /// Calculates a support rule for a valid survivor and asserts that one was produced.
        /// </summary>
        /// <param name="firstTotal">The first configured side's total value.</param>
        /// <param name="secondTotal">The second configured side's total value.</param>
        /// <param name="firstLoss">The first configured side's loss value.</param>
        /// <param name="secondLoss">The second configured side's loss value.</param>
        /// <param name="survivingSide">The surviving configured side.</param>
        /// <param name="firstWeights">The first configured side's weights.</param>
        /// <param name="secondWeights">The second configured side's weights.</param>
        /// <param name="divisor">The final signed divisor.</param>
        /// <returns>The calculated support adjustment.</returns>
        private static SpaceCombatSupport.Adjustment Calculate(
            int firstTotal,
            int secondTotal,
            int firstLoss,
            int secondLoss,
            int survivingSide,
            SpaceCombatSupportSideConfig firstWeights,
            SpaceCombatSupportSideConfig secondWeights,
            int divisor = 1
        )
        {
            bool calculated = SpaceCombatSupport.TryCalculate(
                firstTotal,
                secondTotal,
                firstLoss,
                secondLoss,
                survivingSide,
                firstWeights,
                secondWeights,
                divisor,
                out SpaceCombatSupport.Adjustment adjustment
            );
            Assert.IsTrue(calculated);
            return adjustment;
        }

        /// <summary>
        /// Builds a complete authored support profile for one side.
        /// </summary>
        /// <param name="leadingFlat">The flat support weight used when this side leads.</param>
        /// <param name="leadingGapScale">The support weight applied to the advantage over the opposing side.</param>
        /// <param name="trailingFlat">The flat support weight used when this side trails.</param>
        /// <param name="trailingGapScale">The support weight applied to the gap when this side trails.</param>
        /// <param name="magnitudeCap">The maximum support adjustment for the side.</param>
        /// <param name="supportThreshold">The upper planetary support threshold for selecting recipients.</param>
        /// <returns>The requested force-order weights and bounds.</returns>
        private static SpaceCombatSupportSideConfig Weights(
            int leadingFlat,
            int leadingGapScale,
            int trailingFlat,
            int trailingGapScale,
            int magnitudeCap,
            uint supportThreshold
        ) =>
            new()
            {
                LeadingFlat = leadingFlat,
                LeadingGapScale = leadingGapScale,
                TrailingFlat = trailingFlat,
                TrailingGapScale = trailingGapScale,
                MagnitudeCap = magnitudeCap,
                SupportThreshold = supportThreshold,
            };

        /// <summary>
        /// Creates equal flat weights for both force orderings without gap scaling.
        /// </summary>
        /// <param name="weight">The flat weight for either ordering.</param>
        /// <param name="cap">The beneficiary's signed upper magnitude bound.</param>
        /// <param name="threshold">The comparison side's support threshold.</param>
        /// <returns>The side's fixed support weights.</returns>
        private static SpaceCombatSupportSideConfig FlatWeights(
            int weight,
            int cap = int.MaxValue,
            uint threshold = 0
        ) => Weights(weight, 0, weight, 0, cap, threshold);
    }
}
