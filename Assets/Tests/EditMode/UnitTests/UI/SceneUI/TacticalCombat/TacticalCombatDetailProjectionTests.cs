using System;
using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatDetailProjectionTests
    {
        [TestCase(0x00000000u, 0x3f800000u, 0x0000000000000000UL)]
        [TestCase(0x80000000u, 0x3f800000u, 0x8000000000000000UL)]
        [TestCase(0x3f800000u, 0x41200000u, 0x4015da2332f0191dUL)]
        [TestCase(0x3fc00000u, 0x41200000u, 0x4022a1c61caa9553UL)]
        [TestCase(0x00000001u, 0x7f7fffffu, 0x3e8fffffe0000000UL)]
        [TestCase(0x00800000u, 0x00800000u, 0x3020000000000000UL)]
        [TestCase(0x3f25a1cau, 0x41c80000u, 0x4020c38fd857f55eUL)]
        [TestCase(0x7fc00100u, 0x7fc00300u, 0x7ff8006000000000UL)]
        [TestCase(0xffc000ffu, 0x3f800000u, 0xfff8001fe0000000UL)]
        public void GetFighterMetric_RetainedAngleAndFinalRounding_PreservesMetricBits(
            uint fieldBits,
            uint distanceBits,
            ulong expectedBits
        )
        {
            float field = BitConverter.Int32BitsToSingle(unchecked((int)fieldBits));
            float distance = BitConverter.Int32BitsToSingle(unchecked((int)distanceBits));

            double metric = new TacticalCombatDetailProjection(field).GetFighterMetric(distance);

            Assert.AreEqual(expectedBits, unchecked((ulong)BitConverter.DoubleToInt64Bits(metric)));
        }

        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(1f, float.NaN)]
        public void GetFighterMetric_UnorderedInput_PreservesUnorderedResult(
            float field,
            float depth
        ) => Assert.IsNaN(new TacticalCombatDetailProjection(field).GetFighterMetric(depth));

        [Test]
        public void SelectFighter_LargeFiniteField_UsesPlatformAnglePath()
        {
            TacticalCombatDetailProjection projection = new(8f);

            Assert.AreEqual(2, projection.SelectFighter(100f, true, 0));
        }

        [Test]
        public void GetFighterMetric_ReusedProjection_PreservesIndependentDepthResults()
        {
            TacticalCombatDetailProjection projection = new(1f);
            double initial = projection.GetFighterMetric(10f);

            projection.GetFighterMetric(100f);

            Assert.AreEqual(initial, projection.GetFighterMetric(10f));
            Assert.AreEqual(1f, projection.Field);
        }
    }
}
