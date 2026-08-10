using System;
using System.Collections.Generic;
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

        [Test]
        public void FireArc_ChargedWeaponFamiliesInRange_ReturnsSeparateAttacksAndDischargesArc()
        {
            TacticalUnitState unit = CreateCapitalShipState(
                hull: 100,
                shields: 50,
                weaponRechargeRate: 10
            );
            CapitalShip ship = (CapitalShip)unit.Unit;
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 20, 0, 0, 0, 50 };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { 10, 0, 0, 0, 50 };
            unit = TacticalUnitState.FromCapitalShip(ship, TacticalBattleSide.Attacker);

            IReadOnlyList<TacticalAttack> attacks = unit.FireArc(TacticalWeaponArc.Fore, 40f);

            Assert.AreEqual(2, attacks.Count);
            Assert.AreEqual(TacticalWeaponType.Turbolaser, attacks[0].WeaponType);
            Assert.AreEqual(20, attacks[0].Strength);
            Assert.AreEqual(TacticalWeaponType.IonCannon, attacks[1].WeaponType);
            Assert.AreEqual(10, attacks[1].Strength);
            Assert.AreEqual(0, unit.GetAvailableAttackStrength(TacticalWeaponArc.Fore, 40f));
        }

        [Test]
        public void Advance_DischargedWeaponArcs_RechargesArcsInFiringOrder()
        {
            CapitalShip ship = CreateCapitalShip(hull: 100, shields: 50, weaponRechargeRate: 10);
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 20, 20, 0, 0, 50 };
            TacticalUnitState unit = TacticalUnitState.FromCapitalShip(
                ship,
                TacticalBattleSide.Attacker
            );
            unit.FireArc(TacticalWeaponArc.Fore, 40f);
            unit.FireArc(TacticalWeaponArc.Aft, 40f);

            unit.Advance(2f);

            Assert.AreEqual(20, unit.GetAvailableAttackStrength(TacticalWeaponArc.Fore, 40f));
            Assert.AreEqual(0, unit.GetAvailableAttackStrength(TacticalWeaponArc.Aft, 40f));
        }

        [Test]
        public void FromFighters_FighterWeapons_CreatesForwardFiringBatteries()
        {
            Starfighter fighters = new Starfighter
            {
                CurrentSquadronSize = 12,
                LaserCannon = 5,
                LaserRange = 12,
                IonCannon = 3,
                IonRange = 18,
                Torpedoes = 2,
                TorpedoRange = 10,
            };

            TacticalUnitState unit = TacticalUnitState.FromFighters(
                fighters,
                TacticalBattleSide.Attacker
            );

            Assert.AreEqual(10, unit.GetAvailableAttackStrength(TacticalWeaponArc.Fore, 10f));
            Assert.AreEqual(0, unit.GetAvailableAttackStrength(TacticalWeaponArc.Aft, 10f));
        }

        private static TacticalUnitState CreateCapitalShipState(
            int hull,
            int shields,
            int shieldRechargeRate = 0,
            int weaponRechargeRate = 0
        )
        {
            CapitalShip ship = CreateCapitalShip(
                hull,
                shields,
                shieldRechargeRate,
                weaponRechargeRate
            );
            return TacticalUnitState.FromCapitalShip(ship, TacticalBattleSide.Attacker);
        }

        private static CapitalShip CreateCapitalShip(
            int hull,
            int shields,
            int shieldRechargeRate = 0,
            int weaponRechargeRate = 0
        )
        {
            return new CapitalShip
            {
                CurrentHullStrength = hull,
                MaxHullStrength = hull,
                MaxShieldStrength = shields,
                ShieldRechargeRate = shieldRechargeRate,
                WeaponRecharge = weaponRechargeRate,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
