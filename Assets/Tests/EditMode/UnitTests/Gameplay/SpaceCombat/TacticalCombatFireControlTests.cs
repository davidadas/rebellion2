using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatFireControlTests
    {
        [Test]
        public void AcquireTargets_FighterWithRearDesignatedTarget_UsesSharedBattery()
        {
            TacticalCombatUnit source = CreateFiringFighter();
            TacticalCombatUnit target = CreateFighter(1000001, new Vector3(0, 0, -10));
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
            TacticalCombatUnit source = CreateFiringFighter();
            TacticalCombatUnit target = CreateFighter(1000001, new Vector3(0, 0, -10));
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
            TacticalCombatUnit source = CreateCapitalUnit(1, 1, 1);

            TacticalWeaponArc? arc = TacticalCombatFireControl.GetStrongestArc(source, false);

            Assert.AreEqual(TacticalWeaponArc.Port, arc);
        }

        [Test]
        public void AcquireTargets_EqualStoredBatteryTotals_SelectsLaterLiveTotal()
        {
            TacticalCombatUnit source = CreateCapitalUnit(2, 0, 1);
            TacticalCombatUnit forward = CreateFighter(1000001, new Vector3(0, 0, 10));
            TacticalCombatUnit starboard = CreateFighter(1000002, new Vector3(10, 0, 0));
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
            TacticalCombatUnit source = CreateCapitalUnit(2, 0, 0);
            source.Status = TacticalCombatUnitStatus.PreparingHyperspace;
            TacticalCombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
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
            TacticalCombatUnit source = CreateCapitalUnit(2, 0, 0);
            TacticalCombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
            target.Status = TacticalCombatUnitStatus.PreparingHyperspace;
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
            TacticalCombatUnit source = CreateCapitalUnit(2, 0, 0);
            TacticalCombatUnit target = CreateFighter(1000001, new Vector3(0, 0, 10));
            target.Status = TacticalCombatUnitStatus.PreparingHyperspace;
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
            TacticalCombatUnit source = new(ship, CombatSide.Attacker, 1);
            TacticalCombatUnit target = new(
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

        /// <summary>
        /// Captures an active armed squadron without directional secondary batteries.
        /// </summary>
        /// <returns>The firing squadron.</returns>
        private static TacticalCombatUnit CreateFiringFighter() =>
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
        /// <returns>The campaign-backed tactical capital ship.</returns>
        private static TacticalCombatUnit CreateCapitalUnit(int lasers, int ions, int turbolasers)
        {
            CapitalShip ship = new()
            {
                InstanceID = "source",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
            };
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
            return new TacticalCombatUnit(ship, CombatSide.Attacker, 1);
        }

        /// <summary>
        /// Creates an active opposing squadron at a specified battle position.
        /// </summary>
        /// <param name="index">The unique battle identifier.</param>
        /// <param name="position">The squadron's tactical position.</param>
        /// <returns>The campaign-backed tactical squadron.</returns>
        private static TacticalCombatUnit CreateFighter(int index, Vector3 position) =>
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
