using System;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public sealed class BattleResultReconcilerTests
    {
        [Test]
        public void Apply_CapitalShip_CopiesDamageAndPreservesCampaignRepairSchedule()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "ship",
                CurrentHullStrength = 100,
                NextRepairTick = 40,
                RepairIntervalTicks = 5,
            };
            CombatUnit combatant = CombatUnit.Create(source);
            combatant.ApplyCapitalShipState(35, new CapitalShipSystemDamage { Engines = 2 });

            BattleResultReconciler.Apply(combatant, source);

            Assert.AreEqual(35, source.CurrentHullStrength);
            Assert.AreEqual(2, source.SystemDamage.Engines);
            Assert.AreEqual(40, source.NextRepairTick);
            Assert.AreEqual(5, source.RepairIntervalTicks);
            Assert.AreNotSame(((CapitalShip)combatant.Unit).SystemDamage, source.SystemDamage);
        }

        [Test]
        public void Apply_Starfighter_CopiesSurvivingSquadronSize()
        {
            Starfighter source = new Starfighter
            {
                InstanceID = "fighter",
                CurrentSquadronSize = 12,
            };
            CombatUnit combatant = CombatUnit.Create(source);
            combatant.ApplyStarfighterState(4, "carrier");

            BattleResultReconciler.Apply(combatant, source);

            Assert.AreEqual(4, source.CurrentSquadronSize);
        }

        [Test]
        public void Apply_DifferentSourceIdentity_ThrowsArgumentException()
        {
            CombatUnit combatant = CombatUnit.Create(
                new CapitalShip { InstanceID = "battle-ship" }
            );

            Assert.Throws<ArgumentException>(() =>
                BattleResultReconciler.Apply(
                    combatant,
                    new CapitalShip { InstanceID = "different-ship" }
                )
            );
        }

        [Test]
        public void Apply_DifferentSourceType_ThrowsArgumentException()
        {
            CombatUnit combatant = CombatUnit.Create(new CapitalShip { InstanceID = "unit" });

            Assert.Throws<ArgumentException>(() =>
                BattleResultReconciler.Apply(combatant, new Starfighter { InstanceID = "unit" })
            );
        }
    }
}
