using System;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public sealed class CombatUnitTests
    {
        [Test]
        public void Create_CapitalShip_CreatesIndependentBattleCopy()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
                CurrentHullStrength = 100,
            };
            source.GetComponents().Add(new Engine { Health = 50 });

            CombatUnit combatUnit = CombatUnit.Create(source);
            CapitalShip copy = combatUnit.GetUnit() as CapitalShip;

            Assert.IsNotNull(copy);
            Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitInstanceID);
            Assert.AreNotSame(source, copy);
            Assert.AreEqual(source.InstanceID, copy.InstanceID);
            Assert.AreNotSame(source.GetComponents(), copy.GetComponents());
            Assert.AreNotSame(source.GetComponents()[0], copy.GetComponents()[0]);

            copy.CurrentHullStrength = 25;
            copy.GetComponents()[0].Health = 10;

            Assert.AreEqual(100, source.CurrentHullStrength);
            Assert.AreEqual(50, source.GetComponents()[0].Health);
        }

        [Test]
        public void Create_Starfighter_CreatesIndependentBattleCopy()
        {
            Starfighter source = new Starfighter
            {
                InstanceID = "STARFIGHTER_SQUADRON_1",
                OwnerInstanceID = "FACTION_1",
                CurrentSquadronSize = 12,
            };

            CombatUnit combatUnit = CombatUnit.Create(source);
            Starfighter copy = combatUnit.GetUnit() as Starfighter;

            Assert.IsNotNull(copy);
            Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitInstanceID);
            Assert.AreNotSame(source, copy);
            Assert.AreEqual(1, copy.MaxSquadronSize);
            Assert.AreEqual(1, copy.CurrentSquadronSize);

            copy.CurrentSquadronSize = 0;

            Assert.AreEqual(12, source.CurrentSquadronSize);
        }

        [Test]
        public void Create_CapitalShip_StartsShieldsAtConfiguredMaximum()
        {
            CombatUnit combatant = CombatUnit.Create(new CapitalShip { MaxShieldStrength = 200 });

            Assert.AreEqual(200, combatant.CurrentShieldStrength);
        }

        [TestCase(0)]
        [TestCase(5)]
        public void Create_Starfighter_StartsShieldsAtConfiguredStrength(int strength)
        {
            CombatUnit combatant = CombatUnit.Create(new Starfighter { ShieldStrength = strength });

            Assert.AreEqual(strength, combatant.CurrentShieldStrength);
        }

        [Test]
        public void Create_DamagedCapitalShip_PreservesHullCondition()
        {
            CapitalShip source = new CapitalShip
            {
                MaxHullStrength = 100,
                CurrentHullStrength = 35,
            };

            CapitalShip copy = (CapitalShip)CombatUnit.Create(source).GetUnit();

            Assert.AreEqual(100, copy.MaxHullStrength);
            Assert.AreEqual(35, copy.CurrentHullStrength);
        }

        [Test]
        public void Create_ConfiguredFighter_CopiesIndependentPerFighterHull()
        {
            Starfighter source = new Starfighter { MaxHullStrength = 20, CurrentHullStrength = 12 };

            Starfighter copy = (Starfighter)CombatUnit.Create(source).GetUnit();
            copy.CurrentHullStrength = 3;

            Assert.AreEqual(20, copy.MaxHullStrength);
            Assert.AreEqual(12, source.CurrentHullStrength);
        }

        [Test]
        public void Create_UnconfiguredFighter_DoesNotInventHullStrength()
        {
            Starfighter copy = (Starfighter)CombatUnit.Create(new Starfighter()).GetUnit();

            Assert.AreEqual(0, copy.MaxHullStrength);
            Assert.AreEqual(0, copy.CurrentHullStrength);
        }

        [TestCase(null)]
        [TestCase(0)]
        [TestCase(37)]
        public void Serialize_ShieldState_PreservesRecordedValue(int? remainingShields)
        {
            CombatUnit combatant = CombatUnit.Create(new CapitalShip { MaxShieldStrength = 100 });
            combatant.CurrentShieldStrength = remainingShields;

            CombatUnit restored = SerializationHelper.Deserialize<CombatUnit>(
                SerializationHelper.Serialize(combatant)
            );

            Assert.AreEqual(remainingShields, restored.CurrentShieldStrength);
        }

        [Test]
        public void Serialize_FighterHull_PreservesCurrentAndMaximum()
        {
            CombatUnit combatant = CombatUnit.Create(
                new Starfighter { MaxHullStrength = 20, CurrentHullStrength = 12 }
            );

            CombatUnit restored = SerializationHelper.Deserialize<CombatUnit>(
                SerializationHelper.Serialize(combatant)
            );
            Starfighter fighter = (Starfighter)restored.GetUnit();

            Assert.AreEqual(20, fighter.MaxHullStrength);
            Assert.AreEqual(12, fighter.CurrentHullStrength);
        }

        [Test]
        public void Deserialize_UnrecordedShields_DoesNotRefillBattleShields()
        {
            CombatUnit combatant = CombatUnit.Create(new CapitalShip { MaxShieldStrength = 100 });
            string xml = SerializationHelper
                .Serialize(combatant)
                .Replace("<CurrentShieldStrength>100</CurrentShieldStrength>", string.Empty);
            StringAssert.DoesNotContain("CurrentShieldStrength", xml);

            CombatUnit restored = SerializationHelper.Deserialize<CombatUnit>(xml);

            Assert.IsNull(restored.CurrentShieldStrength);
        }

        [Test]
        public void Create_NullUnit_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CombatUnit.Create(null));
        }

        [Test]
        public void Create_UnsupportedUnit_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => CombatUnit.Create(new Regiment()));
        }
    }
}
