using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatDeploymentTests
    {
        [Test]
        public void Constructor_InstalledLaserWithZeroRange_ClassifiesArmedLaserDeployment()
        {
            TacticalCombatUnit unit = CreateUnit(1, 0);

            TacticalCombatDeployment deployment = new(unit);

            Assert.AreEqual(1, deployment.Category);
            Assert.AreEqual(-1.96f, deployment.Score);
        }

        [Test]
        public void Constructor_EngineDamageAfterCapture_RetainsEntryScore()
        {
            TacticalCombatUnit unit = CreateUnit(1, 12);
            TacticalCombatDeployment deployment = new(unit);
            for (int damage = 0; damage < 4; damage++)
                unit.Systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            Assert.AreEqual(0, unit.GetSpeed());
            Assert.AreEqual(-120001.96f, deployment.Score);
        }

        [Test]
        public void Compare_EqualScores_UsesUnsignedRegistrationOrder()
        {
            TacticalCombatDeployment last = new(CreateUnit(-1, 12));
            TacticalCombatDeployment first = new(CreateUnit(1, 12));

            Assert.Greater(TacticalCombatDeployment.Compare(last, first), 0);
        }

        /// <summary>
        /// Captures a complete capital ship with one installed laser battery.
        /// </summary>
        /// <param name="index">The battle registration identifier.</param>
        /// <param name="range">The laser range.</param>
        /// <returns>The deployed capital ship.</returns>
        private static TacticalCombatUnit CreateUnit(int index, int range)
        {
            CapitalShip ship = new()
            {
                InstanceID = "deployment-" + index,
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                SublightSpeed = 4,
                Hyperdrive = 1,
                WeaponRecharge = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 5, 0, 0, 0, range };
            return new TacticalCombatUnit(ship, CombatSide.Attacker, index);
        }
    }
}
