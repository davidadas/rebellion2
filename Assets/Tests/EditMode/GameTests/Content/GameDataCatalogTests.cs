using System.IO;
using NUnit.Framework;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class GameDataCatalogTests
    {
        [Test]
        public void ValidateBuildingUpgrades_ValidUpgradePath_DoesNotThrow()
        {
            Building basic = CreateBuilding("basic", "advanced");
            Building advanced = CreateBuilding("advanced");

            Assert.DoesNotThrow(() =>
                GameDataCatalog.ValidateBuildingUpgrades(new[] { basic, advanced })
            );
        }

        [Test]
        public void ValidateBuildingUpgrades_MissingUpgrade_ThrowsInvalidDataException()
        {
            Building building = CreateBuilding("basic", "missing");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateBuildingUpgrades(new[] { building })
            );

            StringAssert.Contains("references missing upgrade 'missing'", exception.Message);
        }

        [Test]
        public void ValidateBuildingUpgrades_DuplicateUpgrade_ThrowsInvalidDataException()
        {
            Building basic = CreateBuilding("basic", "advanced", "advanced");
            Building advanced = CreateBuilding("advanced");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateBuildingUpgrades(new[] { basic, advanced })
            );

            StringAssert.Contains("contains duplicate upgrade 'advanced'", exception.Message);
        }

        [Test]
        public void ValidateBuildingUpgrades_SelfUpgrade_ThrowsInvalidDataException()
        {
            Building building = CreateBuilding("basic", "basic");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateBuildingUpgrades(new[] { building })
            );

            StringAssert.Contains("cannot upgrade to itself", exception.Message);
        }

        [Test]
        public void ValidateBuildingUpgrades_IndirectCycle_ThrowsInvalidDataException()
        {
            Building basic = CreateBuilding("basic", "advanced");
            Building advanced = CreateBuilding("advanced", "basic");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateBuildingUpgrades(new[] { basic, advanced })
            );

            StringAssert.Contains("contain a cycle", exception.Message);
        }

        private static Building CreateBuilding(string typeID, params string[] upgrades)
        {
            Building building = new Building { TypeID = typeID };
            building.Upgrades.AddRange(upgrades);
            return building;
        }
    }
}
