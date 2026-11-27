using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tactical;
using CapitalShipRank = Rebellion.Tactical.TacticalCombatDeployment.CapitalShipRank;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatDeploymentTests
    {
        [Test]
        public void Constructor_InstalledLaserWithZeroRange_ClassifiesArmedLaserDeployment()
        {
            CombatUnit unit = CreateUnit(1, 0);

            CapitalShipRank deployment = new(unit);

            Assert.AreEqual(1, deployment.Category);
            Assert.AreEqual(-1.96f, deployment.Score);
        }

        [Test]
        public void Constructor_EngineDamageAfterCapture_RetainsEntryScore()
        {
            CombatUnit unit = CreateUnit(1, 12);
            CapitalShipRank deployment = new(unit);
            for (int damage = 0; damage < 4; damage++)
                unit.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            Assert.AreEqual(0, TacticalCombatMotion.GetSpeed(unit));
            Assert.AreEqual(-120001.96f, deployment.Score);
        }

        [Test]
        public void Compare_EqualScores_UsesUnsignedRegistrationOrder()
        {
            CapitalShipRank last = new(CreateUnit(-1, 12));
            CapitalShipRank first = new(CreateUnit(1, 12));

            Assert.Greater(CapitalShipRank.Compare(last, first), 0);
        }

        /// <summary>
        /// Captures a complete capital ship with one installed laser battery.
        /// </summary>
        /// <param name="index">The battle registration identifier.</param>
        /// <param name="range">The laser range.</param>
        /// <returns>The deployed capital ship.</returns>
        private static CombatUnit CreateUnit(int index, int range)
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
            ship.SetPrimaryWeapon(PrimaryWeaponType.LaserCannon, new[] { 5, 0, 0, 0, range });
            return new CombatUnit(ship, CombatSide.Attacker, index);
        }
    }
}
