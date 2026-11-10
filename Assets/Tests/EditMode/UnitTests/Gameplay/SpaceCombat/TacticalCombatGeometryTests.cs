using System;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Gameplay;
using Rebellion.Util.Mathematics;

namespace Rebellion.Tests.Gameplay
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
        public void TurnTowards_RetainedPartialTurn_StoresCompleteNativeRotation()
        {
            Vector3 result = TacticalCombatGeometry.TurnTowards(
                new Vector3(0.7094057f, 0, 0.7048003f),
                new Vector3(-0.14254497f, 0, -0.98978835f),
                0.5235988f,
                625,
                out float angle
            );

            float expectedAngle =
                NativeVectorMath.SignificandBits == 53 ? -0.32724923f : -0.32724926f;
            Assert.AreEqual(expectedAngle, angle);
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

        [TestCase(0f, 0x3f800000)]
        [TestCase(9.313225746154785e-10f, 0x3f800001)]
        [TestCase(9.094947017729282e-13f, 0x3f800000)]
        public void GetLength_MidpointWithSmallThirdCoordinate_UsesSeparatelyRoundedExtendedSums(
            float z,
            int expectedBits
        )
        {
            float length = TacticalCombatGeometry.GetLength(
                new Vector3(0.9999999403953552f, 0.00048828125f, z)
            );

            if (NativeVectorMath.SignificandBits == 53)
                expectedBits = 0x3f800000;
            Assert.AreEqual(expectedBits, System.BitConverter.SingleToInt32Bits(length));
        }

        [TestCase(0x92bda158u, 0xfff134d7u, 0x7fbb4c4au, 0x7ffb4c4au)]
        [TestCase(0x8dda66cfu, 0x7fb3a5e2u, 0x7fb86bd9u, 0x7ff86bd9u)]
        [TestCase(0xcfa894fau, 0x7fc42011u, 0xffd45a3cu, 0xffd45a3cu)]
        [TestCase(0xac66cd49u, 0xff937614u, 0x7febea63u, 0x7febea63u)]
        [TestCase(0xffc38ab5u, 0xae4e1c90u, 0xffd7c6afu, 0xffd7c6afu)]
        [TestCase(0x865f4267u, 0x7fc95c9au, 0xff980331u, 0xffd80331u)]
        [TestCase(0x7fd9ba6du, 0xaf69d8e8u, 0x7fe36727u, 0x7fe36727u)]
        public void GetLength_DistinctNaNPayloads_PreservesGreatestQuietedPayload(
            uint x,
            uint y,
            uint z,
            uint expected
        )
        {
            float length = TacticalCombatGeometry.GetLength(FromBits(x, y, z));

            Assert.IsTrue(float.IsNaN(length));
            if (NativeVectorMath.SignificandBits == 64)
                Assert.AreEqual(
                    expected,
                    unchecked((uint)System.BitConverter.SingleToInt32Bits(length))
                );
        }

        [TestCase(0x7fc12345u, 0x7fc12345u)]
        [TestCase(0xffc12345u, 0xffc12345u)]
        [TestCase(0x7f812345u, 0x7fc12345u)]
        [TestCase(0xff812345u, 0xffc12345u)]
        public void GetLength_SingleNaNWithInfinity_PreservesSignAndQuietsPayload(
            uint input,
            uint expected
        )
        {
            float length = TacticalCombatGeometry.GetLength(
                FromBits(input, 0x7f800000, 0xbf800000)
            );

            Assert.IsTrue(float.IsNaN(length));
            if (NativeVectorMath.SignificandBits == 64)
                Assert.AreEqual(
                    expected,
                    unchecked((uint)System.BitConverter.SingleToInt32Bits(length))
                );
        }

        [TestCase(0x7f812345u, 0x7fc00001u, 0x7fc12345u)]
        [TestCase(0x7fc00002u, 0x7f800001u, 0x7fc00002u)]
        [TestCase(0x7fc00001u, 0xffbfffffu, 0xffffffffu)]
        public void GetLength_QuietAndSignalingNaNs_ChoosesAfterQuietingBothPayloads(
            uint x,
            uint y,
            uint expected
        )
        {
            float length = TacticalCombatGeometry.GetLength(FromBits(x, y, 0x7f800000));

            Assert.IsTrue(float.IsNaN(length));
            if (NativeVectorMath.SignificandBits == 64)
                Assert.AreEqual(
                    expected,
                    unchecked((uint)System.BitConverter.SingleToInt32Bits(length))
                );
        }

        [TestCase(0x7fc12345u, 0x7f812345u, 0x7fc12345u)]
        [TestCase(0xffc12345u, 0xff812345u, 0xffc12345u)]
        public void GetLength_EqualPayloadsWithSameSign_PreservesQuietEncoding(
            uint x,
            uint y,
            uint expected
        )
        {
            float length = TacticalCombatGeometry.GetLength(FromBits(x, y, 0x3f800000));

            Assert.IsTrue(float.IsNaN(length));
            if (NativeVectorMath.SignificandBits == 64)
                Assert.AreEqual(
                    expected,
                    unchecked((uint)System.BitConverter.SingleToInt32Bits(length))
                );
        }

        [Test]
        public void GetLength_GreaterPayloadAfterOppositeSignTie_UsesGreaterEncoding()
        {
            float length = TacticalCombatGeometry.GetLength(
                FromBits(0x7fc00001, 0xffc00001, 0x7fc00002)
            );

            Assert.IsTrue(float.IsNaN(length));
            if (NativeVectorMath.SignificandBits == 64)
                Assert.AreEqual(
                    0x7fc00002u,
                    unchecked((uint)System.BitConverter.SingleToInt32Bits(length))
                );
        }

        [TestCase(0x92bda158u, 0xfff134d7u, 0x7fbb4c4au)]
        [TestCase(0x7fc00001u, 0xffc00001u, 0x3f800000u)]
        [TestCase(0xff812345u, 0x7f800000u, 0xbf800000u)]
        public void GetLength_NonfiniteInputs_UsesTargetNativeEncoding(uint x, uint y, uint z)
        {
            Vector3 input = FromBits(x, y, z);
            float expected = NativeVectorMath.Measure(input.X, input.Y, input.Z);

            float result = TacticalCombatGeometry.GetLength(input);

            Assert.AreEqual(
                BitConverter.SingleToInt32Bits(expected),
                BitConverter.SingleToInt32Bits(result)
            );
        }

        [Test]
        public void GetLength_SubnormalCoordinates_RoundsTheFinalMagnitudeToFloat()
        {
            float length = TacticalCombatGeometry.GetLength(
                new Vector3(float.Epsilon, float.Epsilon, float.Epsilon)
            );

            Assert.AreEqual(2, System.BitConverter.SingleToInt32Bits(length));
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

            Assert.AreEqual(0x3f800001, System.BitConverter.SingleToInt32Bits(distance));
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

        /// <summary>
        /// Constructs a vector from exact input encodings without performing floating-point arithmetic.
        /// </summary>
        /// <param name="x">The first coordinate's bits.</param>
        /// <param name="y">The second coordinate's bits.</param>
        /// <param name="z">The third coordinate's bits.</param>
        /// <returns>The vector with the supplied component encodings.</returns>
        private static Vector3 FromBits(uint x, uint y, uint z) =>
            new(
                System.BitConverter.Int32BitsToSingle(unchecked((int)x)),
                System.BitConverter.Int32BitsToSingle(unchecked((int)y)),
                System.BitConverter.Int32BitsToSingle(unchecked((int)z))
            );
    }
}
