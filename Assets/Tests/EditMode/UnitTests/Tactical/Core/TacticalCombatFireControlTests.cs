using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatFireControlTests
    {
        [Test]
        public void AcquireTargets_FighterWithRearDesignatedTarget_UsesSharedBattery()
        {
            CombatUnit source = CreateFiringFighter();
            CombatUnit target = CreateFighter(1000001, new Vector3(0, 0, -10));
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.AreSame(target, shots.Single().Target);
        }

        [Test]
        public void AcquireTargets_FighterWithRearOpportunityTarget_UsesSharedBattery()
        {
            CombatUnit source = CreateFiringFighter();
            CombatUnit target = CreateFighter(1000001, new Vector3(0, 0, -10));
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.AreSame(target, shots.Single().Target);
        }

        [Test]
        public void GetStrongestArc_EqualStoredTotals_SelectsLaterLiveTotal()
        {
            CombatUnit source = CreateCapitalUnit(1, 1, 1);

            TacticalWeaponArc? arc = TacticalCombatFireControl.GetStrongestArc(source, false);

            Assert.AreEqual(TacticalWeaponArc.Port, arc);
        }

        [Test]
        public void AcquireTargets_EqualStoredBatteryTotals_SelectsLaterLiveTotal()
        {
            CombatUnit source = CreateCapitalUnit(2, 0, 1);
            CombatUnit forward = CreateFighter(1000001, new Vector3(0, 0, 10));
            CombatUnit starboard = CreateFighter(1000002, new Vector3(10, 0, 0));
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            TacticalCombatSpatialIndex index = new(new[] { source, forward, starboard });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.AreEqual(2, shots.Count);
            Assert.IsTrue(shots.All(shot => ReferenceEquals(shot.Target, starboard)));
        }

        [Test]
        public void AcquireTargets_PreparingSource_DoesNotFire()
        {
            CombatUnit source = CreateCapitalUnit(2, 0, 0);
            source.Status = CombatUnitStatus.PreparingHyperspace;
            CombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.IsEmpty(shots);
        }

        [Test]
        public void AcquireTargets_PreparingDesignatedTarget_StillFires()
        {
            CombatUnit source = CreateCapitalUnit(2, 0, 0);
            CombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
            target.Status = CombatUnitStatus.PreparingHyperspace;
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.AreSame(target, shots.Single().Target);
        }

        [Test]
        public void AcquireTargets_PreparingOpportunityTarget_DoesNotFire()
        {
            CombatUnit source = CreateCapitalUnit(2, 0, 0);
            CombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
            target.Status = CombatUnitStatus.PreparingHyperspace;
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.IsEmpty(shots);
        }

        [Test]
        public void AcquireTargets_OpportunityShot_ScalesStrengthBeforeHullDivision()
        {
            CapitalShip ship = new()
            {
                InstanceID = "source",
                MaxHullStrength = 1000,
                CurrentHullStrength = 631,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 137, 0, 0, 0, 100 };
            CombatUnit source = new(ship, CombatSide.Attacker, 1);
            CombatUnit target = new(
                new Starfighter
                {
                    InstanceID = "target",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                CombatSide.Defender,
                1000001
            )
            {
                Position = new Vector3(0, 0, 10),
            };
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.AreEqual(0x41b86b98, BitConverter.SingleToInt32Bits(shots.Single().Strength));
        }

        [Test]
        public void AcquireTargets_TorpedoDistanceJustAboveRange_DoesNotRoundIntoRange()
        {
            CombatUnit source = CreateTorpedoFighter(10);
            CombatUnit target = CreateCapitalUnit(0, 0, 0);
            target.Position = new Vector3(10, 0.001f, 0);
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.AreEqual(TacticalWeaponType.LaserCannon, shots.Single().Weapon);
        }

        [TestCase(0f, 0)]
        [TestCase(5f, 0)]
        [TestCase(float.NaN, 100)]
        public void AcquireTargets_ZeroOrUnorderedShieldRatio_AllowsAccompanyingTorpedo(
            float shields,
            int shieldLimit
        )
        {
            CombatUnit source = CreateTorpedoFighter(10);
            CombatUnit target = CreateCapitalUnit(0, 0, 0, shieldLimit);
            target.Position = new Vector3(0, 0, 10);
            target.RestorePools(1000, shields);
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            CollectionAssert.AreEqual(
                new[] { TacticalWeaponType.LaserCannon, TacticalWeaponType.Torpedo },
                shots.Select(shot => shot.Weapon).ToArray()
            );
        }

        [Test]
        public void AcquireTargets_FreshArmedCapitalBatteries_FiresWithoutRechargeSetup()
        {
            CapitalShip ship = new()
            {
                InstanceID = "source",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 3, 3, 3, 3, 100 };
            CombatUnit source = new(ship, CombatSide.Attacker, 1);
            CombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;
            FixedRandomProvider random = new(new[] { 0d });

            fireControl.AcquireTargets(source, group, index, random);

            Assert.AreSame(target, shots.Single().Target);
            Assert.AreEqual(TacticalWeaponType.LaserCannon, shots[0].Weapon);
            Assert.AreEqual(0, source.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(source.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AcquireTargets_DischargedBattery_FiresAgainOnlyAfterFullRecharge(bool isFighter)
        {
            CombatUnit source = isFighter ? CreateFiringFighter() : CreateCapitalUnit(3, 0, 0);
            CombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;
            FixedRandomProvider random = new(new[] { 0d });
            float capacity = source.GetBatteryCapacity(TacticalWeaponArc.Forward);
            float rechargeNeeded = isFighter ? capacity * 2 : capacity;
            fireControl.AcquireTargets(source, group, index, random);
            Assert.AreEqual(1, shots.Count);

            source.RechargeBatteries(1, rechargeNeeded / 2);
            fireControl.AcquireTargets(source, group, index, random);

            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(
                isFighter ? 0 : capacity / 2,
                source.GetBatteryCharge(TacticalWeaponArc.Forward)
            );

            source.RechargeBatteries(1, rechargeNeeded / 2);
            fireControl.AcquireTargets(source, group, index, random);

            Assert.AreEqual(2, shots.Count);
            Assert.AreEqual(0, source.GetBatteryCharge(TacticalWeaponArc.Forward));
        }

        [Test]
        public void AcquireTargets_DisabledCapitalBatteries_DoesNotConsumeFiringOpportunity()
        {
            CombatUnit source = CreateCapitalUnit(3, 0, 0, weaponDamage: 4);
            CombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;
            SystemRandomProvider random = new(42);

            fireControl.AcquireTargets(source, group, index, random);

            Assert.IsEmpty(shots);
            Assert.AreEqual(0, random.CallCount);
        }

        [Test]
        public void AcquireTargets_UnorderedDesignatedDistance_RetainsTargetForWeaponLanes()
        {
            CombatUnit source = CreateFiringFighter();
            CombatUnit target = CreateFighter(1000001, new Vector3(0, float.NaN, 10));
            TacticalCombatGroup group = new(10, new[] { source }, Vector3.UnitZ);
            group.SetTarget(target);
            TacticalCombatSpatialIndex index = new(new[] { source, target });
            TacticalCombatFireControl fireControl = new();
            List<TacticalCombatShot> shots = new();
            fireControl.ShotRequested += shots.Add;

            fireControl.AcquireTargets(source, group, index, new FixedRandomProvider(new[] { 0d }));

            Assert.AreSame(target, shots.Single().Target);
            Assert.AreEqual(TacticalWeaponType.LaserCannon, shots[0].Weapon);
        }

        /// <summary>
        /// Creates a squadron with laser cannons and a finite torpedo range.
        /// </summary>
        /// <param name="range">The torpedo range in tactical distance units.</param>
        /// <returns>The armed tactical squadron.</returns>
        private static CombatUnit CreateTorpedoFighter(int range) =>
            new(
                new Starfighter
                {
                    InstanceID = "source",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    LaserCannon = 3,
                    LaserRange = 100,
                    Torpedoes = 1,
                    TorpedoRange = range,
                },
                CombatSide.Attacker,
                2
            );

        /// <summary>
        /// Captures an active armed squadron without directional secondary batteries.
        /// </summary>
        /// <returns>The firing squadron.</returns>
        private static CombatUnit CreateFiringFighter() =>
            new(
                new Starfighter
                {
                    InstanceID = "source",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    LaserCannon = 3,
                    LaserRange = 100,
                },
                CombatSide.Attacker,
                1
            );

        /// <summary>
        /// Creates an active ship with the same authored weapon counts on all four batteries.
        /// </summary>
        /// <param name="lasers">The laser count per battery.</param>
        /// <param name="ions">The ion count per battery.</param>
        /// <param name="turbolasers">The turbolaser count per battery.</param>
        /// <param name="shieldLimit">The complete shield pool.</param>
        /// <param name="weaponDamage">The weapon-system damage carried into the encounter.</param>
        /// <returns>The campaign-backed tactical capital ship.</returns>
        private static CombatUnit CreateCapitalUnit(
            int lasers,
            int ions,
            int turbolasers,
            int shieldLimit = 0,
            int weaponDamage = 0
        )
        {
            CapitalShip ship = new()
            {
                InstanceID = "source",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                MaxShieldStrength = shieldLimit,
            };
            ship.SystemDamage.Weapons = weaponDamage;
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[]
            {
                lasers,
                lasers,
                lasers,
                lasers,
                100,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.IonCannon] = new[]
            {
                ions,
                ions,
                ions,
                ions,
                100,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
            {
                turbolasers,
                turbolasers,
                turbolasers,
                turbolasers,
                100,
            };
            return new(ship, CombatSide.Attacker, 1);
        }

        /// <summary>
        /// Creates an active opposing squadron at a specified battle position.
        /// </summary>
        /// <param name="index">The unique battle identifier.</param>
        /// <param name="position">The squadron's tactical position.</param>
        /// <returns>The campaign-backed tactical squadron.</returns>
        private static CombatUnit CreateFighter(int index, Vector3 position) =>
            new(
                new Starfighter
                {
                    InstanceID = "target-" + index,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                CombatSide.Defender,
                index
            )
            {
                Position = position,
            };
    }
}
