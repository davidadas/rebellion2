using System;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatEngagementTests
    {
        [Test]
        public void GetAttackRate_CapitalAssessment_RetainsFinalDivisionUntilConsumerStore()
        {
            TacticalCombatUnit unit = CreateCapital(631, 1000, 11, 7, 13, 7);

            double attack = TacticalCombatEngagement.GetAttackRate(unit, null);

            Assert.AreEqual(0x4008e43044970b09L, BitConverter.DoubleToInt64Bits(attack));
        }

        [Test]
        public void GetAttackRate_PartialFighterHull_AddsLiveLaneContributionsBeforeStore()
        {
            TacticalCombatUnit unit = CreateFighter();

            double attack = TacticalCombatEngagement.GetAttackRate(unit, null);

            Assert.AreEqual(0x400dfbceba84b7f9L, BitConverter.DoubleToInt64Bits(attack));
        }

        [Test]
        public void GetAttackRate_UnshieldedCapital_RoundsTorpedoBudgetBeforeSecondHullScaling()
        {
            TacticalCombatUnit unit = CreateFighter();
            TacticalCombatUnit target = CreateCapital(100, 100, 0, 0, 0, 0);

            double attack = TacticalCombatEngagement.GetAttackRate(unit, target);

            Assert.AreEqual(0x40238f362c121be0L, BitConverter.DoubleToInt64Bits(attack));
        }

        [Test]
        public void GetAttackRate_CombinedMountCountAboveSignedRange_UsesUnsignedNormalization()
        {
            TacticalCombatUnit unit = CreateCapital(100, 100, int.MaxValue, int.MaxValue, 0, 1);

            double attack = TacticalCombatEngagement.GetAttackRate(unit, null);

            Assert.AreEqual(0x3fe2ac0837a978cfL, BitConverter.DoubleToInt64Bits(attack));
        }

        [Test]
        public void GetAttackRate_UnorderedFighterHull_ReturnsZero()
        {
            TacticalCombatUnit unit = CreateFighter();
            unit.Systems.RestorePools(float.NaN, 0);

            Assert.AreEqual(0, TacticalCombatEngagement.GetAttackRate(unit, null));
        }

        /// <summary>
        /// Captures a capital with a single authored forward battery.
        /// </summary>
        /// <param name="hull">The surviving hull pool.</param>
        /// <param name="maximumHull">The complete hull pool.</param>
        /// <param name="lasers">The forward laser mounts.</param>
        /// <param name="ions">The forward ion mounts.</param>
        /// <param name="turbolasers">The forward heavy mounts.</param>
        /// <param name="recharge">The authored recharge rate.</param>
        /// <returns>The captured capital ship.</returns>
        private static TacticalCombatUnit CreateCapital(
            int hull,
            int maximumHull,
            int lasers,
            int ions,
            int turbolasers,
            int recharge
        )
        {
            CapitalShip ship = new()
            {
                InstanceID = "capital",
                MaxHullStrength = maximumHull,
                CurrentHullStrength = hull,
                WeaponRecharge = recharge,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { lasers, 0, 0, 0, 10 };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[] { ions, 0, 0, 0, 10 };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { turbolasers, 0, 0, 0, 10 };
            return new TacticalCombatUnit(ship, CombatSide.Defender, 1000001);
        }

        /// <summary>
        /// Captures a twelve-craft squadron and applies a partial hull loss after entry.
        /// </summary>
        /// <returns>The squadron with active laser, ion, and torpedo payloads.</returns>
        private static TacticalCombatUnit CreateFighter()
        {
            TacticalCombatUnit unit = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    LaserCannon = 5,
                    IonCannon = 2,
                    Torpedoes = 3,
                    Hyperdrive = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Attacker,
                1
            );
            unit.Systems.ApplyHullLoss(23);
            return unit;
        }
    }
}
