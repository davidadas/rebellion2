using System;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Tactical;

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
            Assert.AreEqual("carrier", combatant.CarrierID);
            Assert.IsNull(combatant.Unit.ParentNode);
        }

        [TestCase(null)]
        [TestCase("alternate-carrier")]
        public void Deserialize_LegacyCampaignCarrier_PreservesCurrentAssignment(string carrierID)
        {
            CombatUnit combatant = CombatUnit.Create(new Starfighter { InstanceID = "fighter" });
            combatant.CarrierID = carrierID;
            string xml = SerializationHelper
                .Serialize(combatant)
                .Replace(
                    "</CombatUnit>",
                    "<CarrierSourceUnitInstanceID>campaign-carrier</CarrierSourceUnitInstanceID></CombatUnit>"
                );

            CombatUnit loaded = SerializationHelper.Deserialize<CombatUnit>(xml);

            Assert.AreEqual(carrierID, loaded.CarrierID);
            StringAssert.DoesNotContain(
                "CarrierSourceUnitInstanceID",
                SerializationHelper.Serialize(loaded)
            );
        }

        [Test]
        public void Create_UnsupportedUnit_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => CombatUnit.Create(new Regiment()));
        }

        [Test]
        public void CaptureDamage_WithThreeDrives_RecordsDamageInEquipmentOrder()
        {
            CapitalShip source = new()
            {
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                Hyperdrives = new[]
                {
                    new Hyperdrive { Rating = 5 },
                    new Hyperdrive { Rating = 3 },
                    new Hyperdrive { Rating = 1 },
                },
            };
            CombatUnit combatant = TacticalCombatDeployment.CreateUnit(
                source,
                CombatSide.Attacker,
                1
            );

            TacticalCombatDamage.ApplyComponentEvent(
                combatant,
                TacticalShipSystem.Hyperdrive,
                isRepair: false
            );
            TacticalCombatDamage.ApplyComponentEvent(
                combatant,
                TacticalShipSystem.Hyperdrive,
                isRepair: false
            );

            Assert.IsTrue(combatant.HasHyperdrive);
            CollectionAssert.AreEqual(
                new[] { 1, 1, 0 },
                combatant.CaptureDamage().HyperdriveDamage
            );

            TacticalCombatDamage.ApplyComponentEvent(
                combatant,
                TacticalShipSystem.Hyperdrive,
                isRepair: false
            );

            Assert.IsFalse(combatant.HasHyperdrive);
            CollectionAssert.AreEqual(
                new[] { 1, 1, 1 },
                combatant.CaptureDamage().HyperdriveDamage
            );
        }

        [Test]
        public void ApplyComponentEvent_BackupOnlyShip_UpdatesTheBattleCopy()
        {
            CapitalShip source = new()
            {
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                Hyperdrives = new[]
                {
                    new Hyperdrive(),
                    new Hyperdrive { Rating = 4 },
                },
                SystemDamage = new CapitalShipSystemDamage { Engines = 1 },
            };
            CombatUnit combatant = TacticalCombatDeployment.CreateUnit(
                source,
                CombatSide.Attacker,
                1
            );
            CapitalShip battleShip = (CapitalShip)combatant.Unit;

            TacticalCombatDamage.ApplyComponentEvent(
                combatant,
                TacticalShipSystem.Hyperdrive,
                isRepair: false
            );
            TacticalCombatDamage.ApplyComponentEvent(
                combatant,
                TacticalShipSystem.Engines,
                isRepair: true
            );

            Assert.AreEqual(0, battleShip.SystemDamage.GetHyperdriveDamage(0));
            Assert.AreEqual(1, battleShip.SystemDamage.GetHyperdriveDamage(1));
            Assert.AreEqual(0, battleShip.SystemDamage.Engines);
            Assert.IsFalse(combatant.HasHyperdrive);
            Assert.AreEqual(1, source.SystemDamage.Engines);
            Assert.AreEqual(0, source.SystemDamage.GetHyperdriveDamage(1));
        }
    }

    [TestFixture]
    public sealed class CombatUnitCopyTests
    {
        [Test]
        public void Capture_CampaignShipChanges_RetainsEntryHardwareAndComponentValues()
        {
            CapitalShip ship = new()
            {
                InstanceID = "captured-ship",
                DisplayName = "Entry name",
                MaxHullStrength = 100,
                CurrentHullStrength = 80,
                MaxShieldStrength = 40,
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.SystemDamage.Engines = 1;
            ship.SetPrimaryWeapon(PrimaryWeaponType.LaserCannon, new[] { 10, 20, 30, 40, 50 });
            CombatUnit combatant = CombatUnit.Create(ship);

            ship.DisplayName = "Changed name";
            ship.CurrentHullStrength = 1;
            ship.MaxHullStrength = 200;
            ship.Hyperdrive = 0;
            ship.SystemDamage.Engines = 4;
            ship.SetPrimaryWeaponStrength(
                PrimaryWeaponType.LaserCannon,
                PrimaryWeaponArc.Fore,
                999
            );
            TacticalCombatDeployment.InitializeUnit(combatant, null, CombatSide.Attacker, 1);
            CombatUnit unit = combatant;

            Assert.AreEqual("Entry name", unit.DisplayName);
            Assert.AreEqual(80, unit.InitialHull);
            Assert.AreEqual(100, unit.MaximumHull);
            Assert.AreEqual(1, unit.GetDamage(TacticalShipSystem.Engines));
            Assert.IsTrue(unit.HasHyperdriveAt(0));
            Assert.AreEqual(
                10,
                unit.GetWeaponStrength(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon)
            );
            Assert.AreEqual(
                50,
                unit.GetWeaponRange(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon)
            );
        }

        [Test]
        public void Capture_FighterStrengthChanges_RetainsEntrySquadronAndWeaponPools()
        {
            Starfighter fighter = new()
            {
                InstanceID = "captured-fighter",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 7,
                LaserCannon = 3,
                LaserRange = 40,
                ShieldStrength = 2,
            };
            CombatUnit combatant = CombatUnit.Create(fighter);

            fighter.MaxSquadronSize = 4;
            fighter.CurrentSquadronSize = 0;
            fighter.LaserCannon = 0;
            fighter.ShieldStrength = 0;
            TacticalCombatDeployment.InitializeUnit(combatant, null, CombatSide.Attacker, 1);
            CombatUnit unit = combatant;

            Assert.IsTrue(unit.IsFighter);
            Assert.AreEqual(7, unit.InitialSquadronSize);
            Assert.AreEqual(56, unit.InitialHull);
            Assert.AreEqual(96, unit.MaximumHull);
            Assert.AreEqual(24, unit.MaximumShields);
            Assert.AreEqual(
                36,
                unit.GetWeaponStrength(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon)
            );
        }
    }
}
