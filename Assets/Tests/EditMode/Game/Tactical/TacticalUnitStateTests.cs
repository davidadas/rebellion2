using System;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public class TacticalUnitStateTests
    {
        [Test]
        public void ApplyDamage_DamageWithinShieldStrength_DamagesOnlyShields()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 50);

            unit.ApplyDamage(30);

            Assert.AreEqual(20, unit.Shields);
            Assert.AreEqual(100, unit.Hull);
        }

        [Test]
        public void ApplyDamage_DamageExceedsShieldStrength_AppliesRemainderToHull()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 50);

            unit.ApplyDamage(70);

            Assert.AreEqual(0, unit.Shields);
            Assert.AreEqual(80, unit.Hull);
        }

        [Test]
        public void ApplyDamage_NegativeDamage_ThrowsArgumentOutOfRangeException()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 50);

            Assert.Throws<ArgumentOutOfRangeException>(() => unit.ApplyDamage(-1));
        }

        [Test]
        public void Advance_DamagedShields_RechargesContinuouslyUpToInitialStrength()
        {
            TacticalUnitState unit = CreateCapitalShipState(
                hull: 100,
                shields: 50,
                shieldRechargeRate: 3
            );
            unit.ApplyDamage(10);

            unit.Advance(0.5f);
            unit.Advance(0.5f);
            unit.Advance(10f);

            Assert.AreEqual(50, unit.Shields);
        }

        private static TacticalUnitState CreateCapitalShipState(
            int hull,
            int shields,
            int shieldRechargeRate = 0
        )
        {
            CapitalShip ship = new CapitalShip
            {
                CurrentHullStrength = hull,
                MaxHullStrength = hull,
                MaxShieldStrength = shields,
                ShieldRechargeRate = shieldRechargeRate,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            return TacticalUnitState.FromCapitalShip(ship, TacticalBattleSide.Attacker);
        }
    }
}
