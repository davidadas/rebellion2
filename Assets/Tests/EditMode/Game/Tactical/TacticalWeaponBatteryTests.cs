using System;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public class TacticalWeaponBatteryTests
    {
        [Test]
        public void Create_WeaponDefinition_MapsArcsAndRange()
        {
            int[] values = { 10, 20, 30, 40, 50 };

            TacticalWeaponBattery battery = TacticalWeaponBattery.Create(
                PrimaryWeaponType.Turbolaser,
                values
            );

            Assert.AreEqual(PrimaryWeaponType.Turbolaser, battery.WeaponType);
            Assert.AreEqual(10, battery.GetCount(TacticalWeaponArc.Fore));
            Assert.AreEqual(20, battery.GetCount(TacticalWeaponArc.Aft));
            Assert.AreEqual(30, battery.GetCount(TacticalWeaponArc.Port));
            Assert.AreEqual(40, battery.GetCount(TacticalWeaponArc.Starboard));
            Assert.AreEqual(50, battery.Range);
        }

        [Test]
        public void Create_IncompleteWeaponDefinition_ThrowsArgumentException()
        {
            int[] values = { 10, 20, 30, 40 };

            Assert.Throws<ArgumentException>(() =>
                TacticalWeaponBattery.Create(PrimaryWeaponType.IonCannon, values)
            );
        }
    }
}
