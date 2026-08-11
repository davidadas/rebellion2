using NUnit.Framework;

namespace Rebellion.Game.Tactical.Tests
{
    [TestFixture]
    public sealed class TacticalFlightCurveTests
    {
        [TestCase(0, 40f)]
        [TestCase(1, 57.5f)]
        [TestCase(2, 65f)]
        [TestCase(3, 32.5f)]
        public void GetArrivalDistance_AtStart_ReturnsLaneDistance(int lane, float expectedDistance)
        {
            float distance = TacticalFlightCurve.GetArrivalDistance(lane, 0f);

            Assert.AreEqual(expectedDistance, distance);
        }

        [TestCase(0, 1.5f)]
        [TestCase(1, 1.25f)]
        [TestCase(2, 1.425f)]
        [TestCase(3, 1.225f)]
        public void GetArrivalDistance_AtLaneCompletion_ReturnsZero(int lane, float elapsedTime)
        {
            float distance = TacticalFlightCurve.GetArrivalDistance(lane, elapsedTime);

            Assert.AreEqual(0f, distance, 0.0001f);
        }

        [TestCase(0, 40f)]
        [TestCase(1, 65.4222259f)]
        [TestCase(2, 67.5078125f)]
        [TestCase(3, 37.4757881f)]
        public void GetWithdrawalDistance_AtCompletion_ReturnsStaggeredLaneDistance(
            int lane,
            float expectedDistance
        )
        {
            float distance = TacticalFlightCurve.GetWithdrawalDistance(
                lane,
                TacticalFlightCurve.WithdrawalDuration
            );

            Assert.AreEqual(expectedDistance, distance, 0.0001f);
        }

        [Test]
        public void GetWithdrawalDistance_HalfElapsedTime_ReturnsQuarterLaneDistance()
        {
            float distance = TacticalFlightCurve.GetWithdrawalDistance(0, 2f);

            Assert.AreEqual(10f, distance);
        }
    }
}
