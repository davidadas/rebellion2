using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatDetailLevelsTests
    {
        [TestCase(14.99f, 2, 0)]
        [TestCase(15f, 2, 2)]
        [TestCase(15.01f, 2, 1)]
        [TestCase(39.99f, 0, 1)]
        [TestCase(40f, 0, 0)]
        [TestCase(40.01f, 0, 2)]
        public void SelectCapital_HighDetailBoundaries_RetainsExactBoundaryLevel(
            float distance,
            int current,
            int expected
        ) =>
            Assert.AreEqual(
                expected,
                TacticalCombatDetailLevels.SelectCapital(distance, 1, true, current)
            );

        [TestCase(19.99f, 1)]
        [TestCase(20f, 1)]
        [TestCase(20.01f, 2)]
        public void SelectCapital_ReducedDetail_UsesOnlyMiddleAndFar(
            float distance,
            int expected
        ) =>
            Assert.AreEqual(
                expected,
                TacticalCombatDetailLevels.SelectCapital(distance, 1, false, 0)
            );

        [Test]
        public void SelectCapital_RoundedDivisionBoundary_RetainsCurrentLevel()
        {
            float field = 0.43f;
            float near = (float)(15.0 / field);

            Assert.AreEqual(2, TacticalCombatDetailLevels.SelectCapital(near, field, true, 2));
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void SelectCapital_UnorderedDistance_UsesAvailableNearLevel(
            bool highDetail,
            int expected
        ) =>
            Assert.AreEqual(
                expected,
                TacticalCombatDetailLevels.SelectCapital(float.NaN, 1, highDetail, 2)
            );

        [TestCase(40, true, 0)]
        [TestCase(60, true, 1)]
        [TestCase(110, true, 2)]
        [TestCase(40, false, 1)]
        [TestCase(60, false, 1)]
        [TestCase(110, false, 2)]
        public void SelectFighter_AvailableDetailLevels_UsesAngularMetric(
            float distance,
            bool highDetail,
            int expected
        ) =>
            Assert.AreEqual(
                expected,
                TacticalCombatDetailLevels.SelectFighter(distance, 0.2f, highDetail, 1)
            );

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void SelectFighter_UnorderedDistance_UsesAvailableNearLevel(
            bool highDetail,
            int expected
        ) =>
            Assert.AreEqual(
                expected,
                TacticalCombatDetailLevels.SelectFighter(float.NaN, 0.2f, highDetail, 2)
            );
    }
}
