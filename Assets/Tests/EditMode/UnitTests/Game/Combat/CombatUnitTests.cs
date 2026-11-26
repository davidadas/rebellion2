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
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 10, 20, 30, 40, 50 };
            CombatUnit combatant = CombatUnit.Create(ship);

            ship.DisplayName = "Changed name";
            ship.CurrentHullStrength = 1;
            ship.MaxHullStrength = 200;
            ship.Hyperdrive = 0;
            ship.SystemDamage.Engines = 4;
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 999;
            combatant.InitializeTacticalState(CombatSide.Attacker, 1);
            CombatUnit unit = combatant;

            Assert.AreEqual("Entry name", unit.DisplayName);
            Assert.AreEqual(80, unit.InitialHull);
            Assert.AreEqual(100, unit.MaximumHull);
            Assert.AreEqual(1, unit.GetDamage(TacticalShipSystem.Engines));
            Assert.IsTrue(unit.HasPrimaryHyperdrive);
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
            combatant.InitializeTacticalState(CombatSide.Attacker, 1);
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
