using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Integration.Campaign;

namespace Rebellion.Tests.Tactical.Integration.Campaign
{
    [TestFixture]
    public sealed class TacticalCombatUnitCaptureTests
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
            TacticalCombatUnitSetup setup = TacticalCombatUnitCapture.Capture(ship);

            ship.DisplayName = "Changed name";
            ship.CurrentHullStrength = 1;
            ship.MaxHullStrength = 200;
            ship.Hyperdrive = 0;
            ship.SystemDamage.Engines = 4;
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 999;
            TacticalCombatUnit unit = new(setup, CombatSide.Attacker, 1);

            Assert.AreEqual("Entry name", unit.DisplayName);
            Assert.AreEqual(80, unit.InitialHull);
            Assert.AreEqual(100, unit.Systems.MaximumHull);
            Assert.AreEqual(1, unit.Systems.GetDamage(TacticalShipSystem.Engines));
            Assert.IsTrue(unit.HasPrimaryHyperdrive);
            Assert.AreEqual(
                10,
                unit.Weapons.GetStrength(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon)
            );
            Assert.AreEqual(50, unit.Weapons.GetRange(TacticalWeaponType.LaserCannon));
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
            TacticalCombatUnitSetup setup = TacticalCombatUnitCapture.Capture(fighter);

            fighter.MaxSquadronSize = 4;
            fighter.CurrentSquadronSize = 0;
            fighter.LaserCannon = 0;
            fighter.ShieldStrength = 0;
            TacticalCombatUnit unit = new(setup, CombatSide.Attacker, 1);

            Assert.IsTrue(unit.IsFighter);
            Assert.AreEqual(7, unit.InitialSquadronSize);
            Assert.AreEqual(56, unit.InitialHull);
            Assert.AreEqual(96, unit.Systems.MaximumHull);
            Assert.AreEqual(24, unit.Systems.MaximumShields);
            Assert.AreEqual(
                36,
                unit.Weapons.GetStrength(TacticalWeaponArc.Forward, TacticalWeaponType.LaserCannon)
            );
        }
    }
}
