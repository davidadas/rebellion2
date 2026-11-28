using System;
using NUnit.Framework;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public sealed class CapitalShipSystemDamageTests
    {
        [Test]
        public void CreateCopy_DamageState_ReturnsIndependentCopy()
        {
            CapitalShipSystemDamage source = new CapitalShipSystemDamage
            {
                Shields = 1,
                Engines = 2,
            };

            CapitalShipSystemDamage copy = source.CreateCopy();
            copy.Engines = 0;

            Assert.AreEqual(2, source.Engines);
        }

        [Test]
        public void CapitalShipCopy_SystemDamage_CopiesWithoutSharingIt()
        {
            CapitalShip source = new CapitalShip
            {
                SystemDamage = new CapitalShipSystemDamage { Weapons = 2 },
            };

            CapitalShip copy = (CapitalShip)source.CreateCopy();
            copy.SystemDamage.Weapons = 1;

            Assert.AreEqual(2, source.SystemDamage.Weapons);
            Assert.AreEqual(1, copy.SystemDamage.Weapons);
        }

        [Test]
        public void RepairComponent_SelectedDamageLevel_DecrementsComponent()
        {
            CapitalShipSystemDamage damage = new CapitalShipSystemDamage
            {
                Shields = 1,
                Weapons = 2,
                Engines = 1,
            };

            damage.RepairComponent(2);

            Assert.AreEqual(1, damage.Weapons);
            Assert.AreEqual(3, damage.Total);
        }

        [Test]
        public void RepairComponent_IndexOutsideDamage_ThrowsArgumentOutOfRangeException()
        {
            CapitalShipSystemDamage damage = new CapitalShipSystemDamage { Engines = 1 };

            Assert.Throws<ArgumentOutOfRangeException>(() => damage.RepairComponent(1));
        }
    }
}
