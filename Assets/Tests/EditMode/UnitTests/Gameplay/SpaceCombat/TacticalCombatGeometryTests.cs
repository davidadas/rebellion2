using System.Numerics;
using NUnit.Framework;
using Rebellion.Gameplay;

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

        [Test]
        public void GetHeadingAngle_UnorderedDotProduct_UsesLowerDotLimit()
        {
            float angle = TacticalCombatGeometry.GetHeadingAngle(
                Vector3.UnitX,
                new Vector3(float.NaN, 0, 0)
            );

            Assert.AreEqual((float)System.Math.PI, angle);
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
    }
}
