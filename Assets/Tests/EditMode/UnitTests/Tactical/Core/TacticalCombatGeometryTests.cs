using System;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Tactical;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatGeometryTests
    {
        [TestCase(0, 10, TacticalWeaponArc.Forward)]
        [TestCase(10, 0, TacticalWeaponArc.Starboard)]
        [TestCase(0, -10, TacticalWeaponArc.Aft)]
        [TestCase(-10, 0, TacticalWeaponArc.Port)]
        [TestCase(10, 10, TacticalWeaponArc.Forward)]
        [TestCase(-10, 10, TacticalWeaponArc.Forward)]
        [TestCase(10, -10, TacticalWeaponArc.Starboard)]
        [TestCase(-10, -10, TacticalWeaponArc.Port)]
        public void GetWeaponArc_ForwardHeading_SelectsFacingBattery(
            float x,
            float z,
            TacticalWeaponArc expected
        )
        {
            Assert.AreEqual(
                expected,
                TacticalCombatGeometry.GetWeaponArc(Vector3.UnitZ, new Vector3(x, 0, z))
            );
        }

        [Test]
        public void TurnTowards_RetainedPartialTurn_StoresRoundedRotation()
        {
            Vector3 result = TacticalCombatGeometry.TurnTowards(
                new Vector3(0.7094057f, 0, 0.7048003f),
                new Vector3(-0.14254497f, 0, -0.98978835f),
                0.5235988f,
                625,
                out float angle
            );

            Assert.AreEqual(-0.32724923f, angle);
            Assert.That(result.X, Is.EqualTo(0.89830834f).Within(0.0000001f));
            Assert.That(result.Z, Is.EqualTo(0.43936566f).Within(0.0000001f));
        }

        [Test]
        public void GetWeaponArc_RotatedShip_UsesShipHeading()
        {
            Assert.AreEqual(
                TacticalWeaponArc.Forward,
                TacticalCombatGeometry.GetWeaponArc(Vector3.UnitX, Vector3.UnitX)
            );
        }

        [Test]
        public void Integrate_HalfSecond_AdvancesEveryCoordinate()
        {
            Vector3 result = TacticalCombatGeometry.Integrate(
                new Vector3(10, 20, 30),
                new Vector3(2, -4, 6),
                500
            );

            Assert.AreEqual(new Vector3(11, 18, 33), result);
        }

        [Test]
        public void TurnTowards_SufficientTurn_ReachesTargetHeading()
        {
            Vector3 heading = TacticalCombatGeometry.TurnTowards(
                Vector3.UnitZ,
                Vector3.UnitX,
                2,
                1000,
                out float angle
            );

            Assert.AreEqual(Vector3.UnitX, heading);
            Assert.That(angle, Is.EqualTo(-1.5707964f).Within(0.000001f));
        }

        [Test]
        public void TurnTowards_PartialTurn_RespectsAngularSpeed()
        {
            Vector3 heading = TacticalCombatGeometry.TurnTowards(
                Vector3.UnitZ,
                Vector3.UnitX,
                1,
                500,
                out float angle
            );

            Assert.That(angle, Is.EqualTo(-0.5f).Within(0.000001f));
            Assert.That(heading.X, Is.EqualTo(0.47942555f).Within(0.000001f));
            Assert.That(heading.Z, Is.EqualTo(0.87758255f).Within(0.000001f));
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void TurnTowards_ZeroOrUnorderedElapsed_PreservesHeadingWithoutAppliedAngle(
            float elapsed
        )
        {
            Vector3 heading = TacticalCombatGeometry.TurnTowards(
                Vector3.UnitZ,
                Vector3.UnitX,
                1,
                elapsed,
                out float angle
            );

            Assert.AreEqual(Vector3.UnitZ, heading);
            Assert.AreEqual(0, angle);
        }

        [Test]
        public void GetHeadingAngle_UnorderedDotProduct_PreservesUnorderedAngle()
        {
            float angle = TacticalCombatGeometry.GetHeadingAngle(
                Vector3.UnitX,
                new Vector3(float.NaN, 0, 0)
            );

            Assert.IsTrue(float.IsNaN(angle));
        }

        [Test]
        public void GetHeadingAngle_StoredNormalizedComponents_PreservesTheirRoundedDotProduct()
        {
            Vector3 heading = new(0.57735026f, 0.57735026f, 0.57735026f);

            float angle = TacticalCombatGeometry.GetHeadingAngle(heading, heading);

            Assert.AreEqual(0.000345266977f, angle);
        }

        [Test]
        public void Normalize_ZeroVector_DoesNotChooseAHeading()
        {
            Assert.AreEqual(Vector3.Zero, TacticalCombatGeometry.Normalize(Vector3.Zero));
        }

        [TestCase(0f)]
        [TestCase(9.313225746154785e-10f)]
        [TestCase(9.094947017729282e-13f)]
        public void GetLength_MidpointWithSmallThirdCoordinate_RoundsToNearestFloat(float z)
        {
            float length = TacticalCombatGeometry.GetLength(
                new Vector3(0.9999999403953552f, 0.00048828125f, z)
            );

            Assert.AreEqual(1f, length);
        }

        [Test]
        public void GetLength_SubnormalCoordinates_RoundsTheFinalMagnitudeToFloat()
        {
            float length = TacticalCombatGeometry.GetLength(
                new Vector3(float.Epsilon, float.Epsilon, float.Epsilon)
            );

            Assert.AreEqual(2, BitConverter.SingleToInt32Bits(length));
        }

        [Test]
        public void GetLength_MagnitudeBeyondFloatRange_ReturnsPositiveInfinity()
        {
            float length = TacticalCombatGeometry.GetLength(
                new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)
            );

            Assert.AreEqual(float.PositiveInfinity, length);
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, float.NaN)]
        public void GetLength_UnorderedCoordinate_PreservesUnorderedMagnitude(float x, float y)
        {
            Assert.IsTrue(float.IsNaN(TacticalCombatGeometry.GetLength(new Vector3(x, y, 0))));
        }

        [Test]
        public void GetTractorDistance_SmallVerticalPositionDifference_RetainsItBeforeSquaring()
        {
            float distance = TacticalCombatGeometry.GetTractorDistance(
                new Vector3(0.9999999403953552f, 0.00048828125f, 0),
                new Vector3(0, -9.094947017729282e-13f, 0)
            );

            Assert.AreEqual(0x3f800001, BitConverter.SingleToInt32Bits(distance));
        }

        [Test]
        public void GetRecoveryDistance_SubnormalSeparation_StoresUnderflowedSquare()
        {
            float distance = TacticalCombatGeometry.GetRecoveryDistance(
                new Vector3(float.Epsilon, 0, 0),
                Vector3.Zero
            );

            Assert.AreEqual(0, distance);
        }

        [Test]
        public void GetRecoveryDistance_LargeFiniteSeparation_StoresOverflowedSquare()
        {
            float distance = TacticalCombatGeometry.GetRecoveryDistance(
                new Vector3(float.MaxValue, 0, 0),
                Vector3.Zero
            );

            Assert.AreEqual(float.PositiveInfinity, distance);
        }

        [Test]
        public void GetLength_InfiniteCoordinate_ReturnsPositiveInfinity()
        {
            Assert.AreEqual(
                float.PositiveInfinity,
                TacticalCombatGeometry.GetLength(new Vector3(0, float.NegativeInfinity, 1))
            );
        }
    }
}
