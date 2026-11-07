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
    }
}
