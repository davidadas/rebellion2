using System;
using System.Numerics;
using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatDeparturePoseTests
    {
        [TestCase(0, 1000f, unchecked((int)0xc18fffff))]
        [TestCase(0, 2000f, unchecked((int)0xc2b9ffff))]
        [TestCase(0, 4000f, unchecked((int)0xc3c47fff))]
        [TestCase(1, 1000f, unchecked((int)0xc2078e38))]
        [TestCase(1, 2000f, unchecked((int)0xc31c8e38))]
        [TestCase(1, 4000f, unchecked((int)0xc421ce38))]
        [TestCase(2, 1000f, unchecked((int)0xc20cc4fe))]
        [TestCase(2, 2000f, unchecked((int)0xc321c4fe))]
        [TestCase(2, 4000f, unchecked((int)0xc42704fe))]
        [TestCase(3, 1000f, unchecked((int)0xc1836101))]
        [TestCase(3, 2000f, unchecked((int)0xc2ad6101))]
        [TestCase(3, 4000f, unchecked((int)0xc3b7e101))]
        public void Evaluate_RegisteredStaggering_ProducesExpectedFloatPosition(
            int ordinal,
            float elapsed,
            int expectedBits
        )
        {
            TacticalCombatDeparturePose pose = TacticalCombatDeparturePose.Evaluate(
                0,
                ordinal,
                new Vector3(3, 5, 7),
                Vector3.UnitZ,
                elapsed
            );

            Assert.AreEqual(expectedBits, BitConverter.SingleToInt32Bits(pose.Position.Z));
            Assert.AreEqual(3, pose.Position.X);
            Assert.AreEqual(5, pose.Position.Y);
            Assert.AreEqual(-Vector3.UnitZ, pose.Heading);
        }

        [Test]
        public void Evaluate_RegistrationAndOrdinal_WrapsSelectorWithoutChangingOtherCoordinates()
        {
            Vector3 position = new(3, 5, 7);
            TacticalCombatDeparturePose expected = TacticalCombatDeparturePose.Evaluate(
                0,
                2,
                position,
                Vector3.UnitZ,
                1000
            );

            TacticalCombatDeparturePose actual = TacticalCombatDeparturePose.Evaluate(
                int.MaxValue,
                3,
                position,
                Vector3.UnitZ,
                1000
            );

            Assert.AreEqual(expected.Position, actual.Position);
        }

        [Test]
        public void Evaluate_OppositeLane_ReversesOnlyDepartureDirection()
        {
            Vector3 position = new(3, 5, 7);
            TacticalCombatDeparturePose first = TacticalCombatDeparturePose.Evaluate(
                0,
                0,
                position,
                Vector3.UnitZ,
                1000
            );

            TacticalCombatDeparturePose second = TacticalCombatDeparturePose.Evaluate(
                0,
                0,
                position,
                -Vector3.UnitZ,
                1000
            );

            Assert.AreEqual(position.Z - first.Position.Z, second.Position.Z - position.Z);
            Assert.AreEqual(Vector3.UnitZ, second.Heading);
        }

        [Test]
        public void Evaluate_ElapsedBeyondCompletion_UsesFourSecondPose()
        {
            TacticalCombatDeparturePose expected = TacticalCombatDeparturePose.Evaluate(
                0,
                3,
                Vector3.Zero,
                Vector3.UnitZ,
                4000
            );

            TacticalCombatDeparturePose actual = TacticalCombatDeparturePose.Evaluate(
                0,
                3,
                Vector3.Zero,
                Vector3.UnitZ,
                float.PositiveInfinity
            );

            Assert.AreEqual(expected.Position, actual.Position);
        }

        [Test]
        public void Evaluate_NegativeElapsed_RetainsQuadraticDisplacement()
        {
            TacticalCombatDeparturePose expected = TacticalCombatDeparturePose.Evaluate(
                0,
                0,
                Vector3.Zero,
                Vector3.UnitZ,
                1000
            );

            TacticalCombatDeparturePose actual = TacticalCombatDeparturePose.Evaluate(
                0,
                0,
                Vector3.Zero,
                Vector3.UnitZ,
                -1000
            );

            Assert.AreEqual(expected.Position, actual.Position);
        }

        [Test]
        public void Evaluate_UnorderedTimer_PreservesUnorderedZWithoutChangingHeading()
        {
            TacticalCombatDeparturePose actual = TacticalCombatDeparturePose.Evaluate(
                0,
                0,
                new Vector3(3, 5, 7),
                Vector3.UnitZ,
                float.NaN
            );

            Assert.IsTrue(float.IsNaN(actual.Position.Z));
            Assert.AreEqual(3, actual.Position.X);
            Assert.AreEqual(5, actual.Position.Y);
            Assert.AreEqual(-Vector3.UnitZ, actual.Heading);
        }
    }
}
