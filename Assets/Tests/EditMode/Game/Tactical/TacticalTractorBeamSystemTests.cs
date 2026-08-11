using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public class TacticalTractorBeamSystemTests
    {
        [Test]
        public void TryGetAttackStrength_LaserFireAtUnshieldedFighters_CreatesTractorAttack()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState target = CreateFighters(TacticalBattleSide.Defender);
            target.Position = new Vector3(0f, 0f, 10f);

            bool created = TacticalTractorBeamSystem.TryGetAttackStrength(
                source,
                target,
                new TacticalAttack(TacticalWeaponType.LaserCannon, 10),
                out int attackStrength
            );

            Assert.IsTrue(created);
            Assert.AreEqual(6, attackStrength);
        }

        [Test]
        public void TryGetAttackStrength_TurbolaserFire_DoesNotCreateTractorAttack()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState target = CreateFighters(TacticalBattleSide.Defender);

            bool created = TacticalTractorBeamSystem.TryGetAttackStrength(
                source,
                target,
                new TacticalAttack(TacticalWeaponType.Turbolaser, 10),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryGetAttackStrength_ShieldedFighters_DoesNotCreateTractorAttack()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState target = CreateFighters(TacticalBattleSide.Defender, 1);

            bool created = TacticalTractorBeamSystem.TryGetAttackStrength(
                source,
                target,
                new TacticalAttack(TacticalWeaponType.LaserCannon, 10),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryGetAttackStrength_CapitalShipTarget_DoesNotCreateTractorAttack()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState target = CreateCapitalShip(TacticalBattleSide.Defender, 0, 0);

            bool created = TacticalTractorBeamSystem.TryGetAttackStrength(
                source,
                target,
                new TacticalAttack(TacticalWeaponType.LaserCannon, 10),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryGetAttackStrength_TargetOutsideRange_DoesNotCreateTractorAttack()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker, 6, 20);
            TacticalUnitState target = CreateFighters(TacticalBattleSide.Defender);
            target.Position = new Vector3(0f, 0f, 21f);

            bool created = TacticalTractorBeamSystem.TryGetAttackStrength(
                source,
                target,
                new TacticalAttack(TacticalWeaponType.LaserCannon, 10),
                out _
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryGetAttackStrength_DamagedTractorSystem_ScalesAttackStrength()
        {
            TacticalUnitState source = CreateCapitalShip(TacticalBattleSide.Attacker, 12, 20);
            TacticalUnitState target = CreateFighters(TacticalBattleSide.Defender);
            source.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.LaserCannon, 1),
                new FixedRandomProvider(new[] { 0.93d })
            );
            source.Hull = source.InitialHull;
            source.ApplyDamage(
                new TacticalAttack(TacticalWeaponType.LaserCannon, 1),
                new FixedRandomProvider(new[] { 0.93d })
            );
            source.Hull = source.InitialHull;

            bool created = TacticalTractorBeamSystem.TryGetAttackStrength(
                source,
                target,
                new TacticalAttack(TacticalWeaponType.LaserCannon, 10),
                out int attackStrength
            );

            Assert.IsTrue(created);
            Assert.AreEqual(6, attackStrength);
        }

        private static TacticalUnitState CreateCapitalShip(
            TacticalBattleSide side,
            int tractorPower,
            int tractorRange
        )
        {
            CapitalShip ship = new CapitalShip
            {
                CurrentHullStrength = 100,
                MaxHullStrength = 100,
                SublightSpeed = 10,
                Hyperdrive = 100,
                TractorBeamPower = tractorPower,
                TractorBeamnRange = tractorRange,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            return TacticalUnitState.FromCapitalShip(ship, side);
        }

        private static TacticalUnitState CreateFighters(
            TacticalBattleSide side,
            int shieldStrength = 0
        )
        {
            Starfighter fighters = new Starfighter
            {
                CurrentSquadronSize = 100,
                ShieldStrength = shieldStrength,
                SublightSpeed = 10,
                Hyperdrive = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            return TacticalUnitState.FromFighters(fighters, side);
        }
    }
}
