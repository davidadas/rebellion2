using System.Collections.Generic;
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

        [Test]
        public void ValidateBuildingUpgrades_DiamondUpgradePath_DoesNotThrow()
        {
            Building basic = CreateBuilding("basic", "left", "right");
            Building left = CreateBuilding("left", "top");
            Building right = CreateBuilding("right", "top");
            Building top = CreateBuilding("top");

            Assert.DoesNotThrow(() =>
                GameDataCatalog.ValidateBuildingUpgrades(new[] { basic, left, right, top })
            );
        }

        [Test]
        public void ValidateOfficerTraits_ValidDefinitionAndReference_ReturnsIndexedDefinition()
        {
            OfficerTraitDefinition trait = CreateTrait("travel");
            Officer officer = new Officer { InstanceID = "officer" };
            officer.Traits.Add(trait.ID);

            IReadOnlyDictionary<string, OfficerTraitDefinition> result =
                GameDataCatalog.ValidateOfficerTraits(new[] { trait }, new[] { officer });

            Assert.AreSame(trait, result[trait.ID]);
        }

        [Test]
        public void ValidateOfficerTraits_DuplicateDefinition_ThrowsInvalidDataException()
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateOfficerTraits(
                    new[] { CreateTrait("duplicate"), CreateTrait("duplicate") },
                    new Officer[0]
                )
            );

            StringAssert.Contains("Duplicate officer trait ID 'duplicate'", exception.Message);
        }

        [Test]
        public void ValidateOfficerTraits_MissingOfficerReference_ThrowsInvalidDataException()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            officer.Traits.Add("missing");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateOfficerTraits(
                    new OfficerTraitDefinition[0],
                    new[] { officer }
                )
            );

            StringAssert.Contains("references missing trait 'missing'", exception.Message);
        }

        [Test]
        public void ValidateOfficerTraits_InvalidTravelPercentage_ThrowsInvalidDataException()
        {
            OfficerTraitDefinition trait = CreateTrait("travel");
            trait.PersonnelTravelTimePercent = 0;

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateOfficerTraits(new[] { trait }, new Officer[0])
            );

            StringAssert.Contains("travel-time percentage", exception.Message);
        }

        /// <summary>
        /// Creates building.
        /// </summary>
        /// <param name="typeID">The type id.</param>
        /// <param name="upgrades">The upgrades.</param>
        /// <returns>The created building.</returns>
        private static Building CreateBuilding(string typeID, params string[] upgrades)
        {
            Building building = new Building { TypeID = typeID };
            building.Upgrades.AddRange(upgrades);
            return building;
        }

        /// <summary>
        /// Creates an officer trait with neutral modifier defaults.
        /// </summary>
        /// <param name="id">The trait identifier.</param>
        /// <returns>The created trait.</returns>
        private static OfficerTraitDefinition CreateTrait(string id) =>
            new OfficerTraitDefinition { ID = id };
    }
}
