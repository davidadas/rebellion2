using System;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public sealed class CombatUnitTests
    {
        [Test]
        public void Create_CapitalShip_CopiesMutableState()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "ship",
                DisplayName = "Entry name",
                CurrentHullStrength = 80,
                SystemDamage = new CapitalShipSystemDamage { Engines = 1 },
            };

            CombatUnit combatant = CombatUnit.Create(source);
            CapitalShip battleShip = (CapitalShip)combatant.Unit;
            battleShip.DisplayName = "Battle name";
            battleShip.CurrentHullStrength = 20;
            battleShip.SystemDamage.Engines = 3;

            Assert.AreEqual("ship", combatant.SourceUnitInstanceID);
            Assert.AreEqual("Entry name", source.DisplayName);
            Assert.AreEqual(80, source.CurrentHullStrength);
            Assert.AreEqual(1, source.SystemDamage.Engines);
        }

        [Test]
        public void Create_Starfighter_CapturesCampaignCarrierIdentity()
        {
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier",
                StarfighterCapacity = 1,
            };
            Starfighter fighter = new Starfighter { InstanceID = "fighter" };
            carrier.AddChild(fighter);
            fighter.SetParent(carrier);

            CombatUnit combatant = CombatUnit.Create(fighter);

            Assert.AreEqual("fighter", combatant.SourceUnitInstanceID);
            Assert.AreEqual("carrier", combatant.CarrierSourceUnitInstanceID);
            Assert.IsNull(combatant.Unit.ParentNode);
        }

        [Test]
        public void Create_UnsupportedUnit_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => CombatUnit.Create(new Regiment()));
        }

        [Test]
        public void ApplyCapitalShipState_Starfighter_ThrowsInvalidOperationException()
        {
            CombatUnit combatant = CombatUnit.Create(new Starfighter());

            Assert.Throws<InvalidOperationException>(() =>
                combatant.ApplyCapitalShipState(10, new CapitalShipSystemDamage())
            );
        }

        [Test]
        public void ApplyStarfighterState_CapitalShip_ThrowsInvalidOperationException()
        {
            CombatUnit combatant = CombatUnit.Create(new CapitalShip());

            Assert.Throws<InvalidOperationException>(() =>
                combatant.ApplyStarfighterState(1, "carrier")
            );
        }
    }
}
