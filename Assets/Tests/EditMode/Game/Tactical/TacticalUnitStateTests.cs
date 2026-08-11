using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public class TacticalUnitStateTests
    {
        [Test]
        public void SetCollisionExtents_PositiveDimensions_StoresPhysicalBounds()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 50);

            unit.SetCollisionExtents(6f, 2f);

            Assert.AreEqual(6f, unit.HorizontalExtent);
            Assert.AreEqual(2f, unit.VerticalExtent);
        }

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(-1f, 1f)]
        [TestCase(1f, -1f)]
        public void SetCollisionExtents_NonpositiveDimension_ThrowsArgumentOutOfRangeException(
            float horizontalExtent,
            float verticalExtent
        )
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 50);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                unit.SetCollisionExtents(horizontalExtent, verticalExtent)
            );
        }

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
        public void ApplyDamage_IonDamageWithinShields_DamagesOnlyShields()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 50);

            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.IonCannon, 30),
                CreateRandom(0d)
            );

            Assert.AreEqual(20, unit.Shields);
            Assert.AreEqual(100, unit.Hull);
            Assert.AreEqual(0f, unit.ComponentDisruptionTime);
            Assert.AreEqual(0f, unit.MovementDisruptionTime);
        }

        [Test]
        public void ApplyDamage_IonDamageExceedsShields_DisruptsComponentsWithoutDamagingHull()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 1);

            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.IonCannon, 2),
                CreateRandom(0d, 0d)
            );

            Assert.AreEqual(0, unit.Shields);
            Assert.AreEqual(100, unit.Hull);
            Assert.AreEqual(30f, unit.ComponentDisruptionTime);
        }

        [Test]
        public void ApplyDamage_IonOverflowRollsMovementDisruption_DisablesMovementTemporarily()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 0);

            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.IonCannon, 1),
                CreateRandom(0.1d, 0d)
            );
            unit.Advance(29f, CreateRandom(1d));

            Assert.IsTrue(unit.IsMovementDisabled);
            Assert.AreEqual(1f, unit.MovementDisruptionTime);

            unit.Advance(1f, CreateRandom(1d));

            Assert.IsFalse(unit.IsMovementDisabled);
        }

        [Test]
        public void ApplyDamage_IonOverflowRollsArcDisruption_ClearsSelectedArcCharge()
        {
            CapitalShip ship = CreateCapitalShip(hull: 100, shields: 0, weaponRechargeRate: 10);
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 10, 0, 0, 0, 50 };
            TacticalUnitState unit = TacticalUnitState.FromCapitalShip(
                ship,
                TacticalBattleSide.Attacker
            );

            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.IonCannon, 1),
                CreateRandom(0.2d)
            );

            Assert.AreEqual(0, unit.GetAvailableAttackStrength(TacticalWeaponArc.Fore, 40f));

            unit.Advance(1f, CreateRandom(1d));

            Assert.AreEqual(10, unit.GetAvailableAttackStrength(TacticalWeaponArc.Fore, 40f));
        }

        [Test]
        public void ApplyDamage_IonOverflowAgainstFighters_DoesNotApplySystemDisruption()
        {
            Starfighter fighters = new Starfighter { CurrentSquadronSize = 12, ShieldStrength = 0 };
            TacticalUnitState unit = TacticalUnitState.FromFighters(
                fighters,
                TacticalBattleSide.Attacker
            );

            unit.ApplyDamage(new TacticalAttack(TacticalWeaponType.IonCannon, 5), CreateRandom(0d));

            Assert.AreEqual(12, unit.Hull);
            Assert.AreEqual(0f, unit.ComponentDisruptionTime);
            Assert.AreEqual(0f, unit.MovementDisruptionTime);
        }

        [Test]
        public void ApplyDamage_ConventionalShieldHit_CanDamageShieldGenerator()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 50);

            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.LaserCannon, 20),
                CreateRandom(0d)
            );

            Assert.AreEqual(1, unit.GetSystemDamage(TacticalDamageSystem.ShieldGenerator));
        }

        [TestCase(0.60d, TacticalDamageSystem.ShieldGenerator)]
        [TestCase(0.75d, TacticalDamageSystem.WeaponSystems)]
        [TestCase(0.85d, TacticalDamageSystem.TractorBeam)]
        [TestCase(0.93d, TacticalDamageSystem.SublightDrive)]
        [TestCase(0.99d, TacticalDamageSystem.Hyperdrive)]
        public void ApplyDamage_ConventionalHullCritical_DamagesSelectedSubsystem(
            double roll,
            TacticalDamageSystem expectedSystem
        )
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 0);

            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.LaserCannon, 1),
                CreateRandom(roll)
            );

            Assert.AreEqual(1, unit.GetSystemDamage(expectedSystem));
        }

        [Test]
        public void EffectiveSublightSpeed_MaximumDriveDamage_DisablesMovement()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 0);

            for (int hit = 0; hit < 4; hit++)
            {
                unit.ApplyDamage(
                    new TacticalAttack(TacticalWeaponType.LaserCannon, 1),
                    CreateRandom(0.93d)
                );
            }

            Assert.AreEqual(0f, unit.EffectiveSublightSpeed);
        }

        [Test]
        public void EffectiveTractorBeamPower_TractorAndHullDamage_ReducesAvailableStrength()
        {
            CapitalShip ship = CreateCapitalShip(hull: 100, shields: 0);
            ship.TractorBeamPower = 12;
            ship.TractorBeamnRange = 20;
            TacticalUnitState unit = TacticalUnitState.FromCapitalShip(
                ship,
                TacticalBattleSide.Attacker
            );

            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.LaserCannon, 1),
                CreateRandom(0.85d)
            );

            Assert.AreEqual(8.88f, unit.EffectiveTractorBeamPower, 0.001f);
            Assert.AreEqual(20, unit.TractorBeamRange);
        }

        [Test]
        public void EffectiveTractorBeamPower_HullDamage_ReducesAvailableStrengthProportionally()
        {
            CapitalShip ship = CreateCapitalShip(hull: 100, shields: 0);
            ship.TractorBeamPower = 12;
            TacticalUnitState unit = TacticalUnitState.FromCapitalShip(
                ship,
                TacticalBattleSide.Attacker
            );

            unit.ApplyDamage(50);

            Assert.AreEqual(6f, unit.EffectiveTractorBeamPower);
        }

        [Test]
        public void CanWithdraw_MaximumHyperdriveDamage_ReturnsFalse()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 0);

            for (int hit = 0; hit < 4; hit++)
            {
                unit.ApplyDamage(
                    new TacticalAttack(TacticalWeaponType.LaserCannon, 1),
                    CreateRandom(0.99d)
                );
            }

            Assert.IsFalse(unit.CanWithdraw);
        }

        [Test]
        public void CanWithdraw_MaximumSublightDriveDamage_ReturnsFalse()
        {
            TacticalUnitState unit = CreateCapitalShipState(hull: 100, shields: 0);

            for (int hit = 0; hit < 4; hit++)
            {
                unit.ApplyDamage(
                    new TacticalAttack(TacticalWeaponType.LaserCannon, 1),
                    CreateRandom(0.93d)
                );
            }

            Assert.IsFalse(unit.CanWithdraw);
        }

        [Test]
        public void CanWithdraw_FightersWithoutHyperdrive_ReturnsFalse()
        {
            Starfighter fighters = new Starfighter
            {
                CurrentSquadronSize = 12,
                SublightSpeed = 100,
            };
            TacticalUnitState unit = TacticalUnitState.FromFighters(
                fighters,
                TacticalBattleSide.Attacker
            );

            Assert.IsFalse(unit.CanWithdraw);
        }

        [Test]
        public void CanWithdraw_FightersWithHyperdrive_ReturnsTrue()
        {
            Starfighter fighters = new Starfighter
            {
                CurrentSquadronSize = 12,
                SublightSpeed = 100,
                Hyperdrive = 100,
            };
            TacticalUnitState unit = TacticalUnitState.FromFighters(
                fighters,
                TacticalBattleSide.Attacker
            );

            Assert.IsTrue(unit.CanWithdraw);
        }

        [Test]
        public void Advance_DamagedSubsystemAndSuccessfulDamageControl_RepairsOneLevel()
        {
            TacticalUnitState unit = CreateCapitalShipState(
                hull: 100,
                shields: 0,
                damageControl: 100
            );
            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.LaserCannon, 1),
                CreateRandom(0.93d)
            );

            unit.Advance(0f, CreateRandom(0d, 0d));

            Assert.AreEqual(0, unit.GetSystemDamage(TacticalDamageSystem.SublightDrive));
        }

        [Test]
        public void Advance_ShieldGeneratorDamage_ReducesShieldRecharge()
        {
            TacticalUnitState unit = CreateCapitalShipState(
                hull: 100,
                shields: 50,
                shieldRechargeRate: 4
            );
            unit.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.LaserCannon, 20),
                CreateRandom(0d)
            );

            unit.Advance(1f, CreateRandom(1d));

            Assert.AreEqual(33, unit.Shields);
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

            unit.Advance(0.5f, CreateRandom(1d));
            unit.Advance(0.5f, CreateRandom(1d));
            unit.Advance(10f, CreateRandom(1d));

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

            unit.Advance(2f, CreateRandom(1d));

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
            int weaponRechargeRate = 0,
            int damageControl = 0
        )
        {
            CapitalShip ship = CreateCapitalShip(
                hull,
                shields,
                shieldRechargeRate,
                weaponRechargeRate,
                damageControl
            );
            return TacticalUnitState.FromCapitalShip(ship, TacticalBattleSide.Attacker);
        }

        private static CapitalShip CreateCapitalShip(
            int hull,
            int shields,
            int shieldRechargeRate = 0,
            int weaponRechargeRate = 0,
            int damageControl = 0
        )
        {
            return new CapitalShip
            {
                CurrentHullStrength = hull,
                MaxHullStrength = hull,
                MaxShieldStrength = shields,
                ShieldRechargeRate = shieldRechargeRate,
                WeaponRecharge = weaponRechargeRate,
                DamageControl = damageControl,
                Hyperdrive = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static IRandomNumberProvider CreateRandom(params double[] values)
        {
            return new FixedRandomProvider(values);
        }
    }
}
