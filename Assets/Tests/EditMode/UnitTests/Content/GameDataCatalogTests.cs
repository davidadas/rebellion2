using System;
using System.IO;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class GameDataCatalogTests
    {
        [Test]
        public void ValidateBattleMapReferences_ExistingMapReference_DoesNotThrow()
        {
            BattleMap battleMap = new BattleMap { InstanceID = "SPACE_MAP" };
            Planet planet = new Planet { InstanceID = "PLANET" };
            planet.BattleMapInstanceIDs.Add(battleMap.InstanceID);
            PlanetSector sector = CreateSector(planet);

            Assert.DoesNotThrow(() =>
                GameDataCatalog.ValidateBattleMapReferences(new[] { battleMap }, new[] { sector })
            );
        }

        [Test]
        public void ValidateBattleMapReferences_MissingMapReference_ThrowsInvalidDataException()
        {
            Planet planet = new Planet { InstanceID = "PLANET" };
            planet.BattleMapInstanceIDs.Add("MISSING_MAP");
            PlanetSector sector = CreateSector(planet);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateBattleMapReferences(
                    System.Array.Empty<BattleMap>(),
                    new[] { sector }
                )
            );

            StringAssert.Contains("references missing battle map 'MISSING_MAP'", exception.Message);
        }

        [Test]
        public void ValidateBattleMapReferences_DuplicateMapInstanceID_ThrowsInvalidDataException()
        {
            BattleMap first = new BattleMap { InstanceID = "SPACE_MAP" };
            BattleMap second = new BattleMap { InstanceID = "SPACE_MAP" };

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateBattleMapReferences(
                    new[] { first, second },
                    System.Array.Empty<PlanetSector>()
                )
            );

            StringAssert.Contains("Duplicate battle map InstanceID 'SPACE_MAP'", exception.Message);
        }

        [Test]
        public void ValidateBattleMapReferences_MissingDefaultSpaceMap_ThrowsInvalidDataException()
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateBattleMapReferences(
                    Array.Empty<BattleMap>(),
                    Array.Empty<PlanetSector>(),
                    new[] { "MISSING_MAP" }
                )
            );

            StringAssert.Contains("reference missing map 'MISSING_MAP'", exception.Message);
        }

        [Test]
        public void ValidateBattleMapReferences_GroundDefaultSpaceMap_ThrowsInvalidDataException()
        {
            BattleMap groundMap = new BattleMap
            {
                InstanceID = "GROUND_MAP",
                Kind = BattleKind.Ground,
            };

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameDataCatalog.ValidateBattleMapReferences(
                    new[] { groundMap },
                    Array.Empty<PlanetSector>(),
                    new[] { groundMap.InstanceID }
                )
            );

            StringAssert.Contains("is not a space map", exception.Message);
        }

        [Test]
        public void GetBattleMapsForPlanet_NoSpaceOverride_ReturnsDefaultSpaceMaps()
        {
            BattleMap defaultMap = new BattleMap
            {
                InstanceID = "DEFAULT_SPACE",
                Kind = BattleKind.Space,
            };
            BattleMap groundMap = new BattleMap
            {
                InstanceID = "GROUND_MAP",
                Kind = BattleKind.Ground,
            };
            Planet planet = new Planet { InstanceID = "PLANET" };
            planet.BattleMapInstanceIDs.Add(groundMap.InstanceID);
            PlanetSector sector = CreateSector(planet);
            GameDataCatalog catalog = TestGameData.Create(
                planetSectors: new[] { sector },
                battleMaps: new[] { defaultMap, groundMap },
                defaultSpaceBattleMapInstanceIDs: new[] { defaultMap.InstanceID }
            );

            System.Collections.Generic.IReadOnlyList<BattleMap> maps =
                catalog.GetBattleMapsForPlanet(planet, BattleKind.Space);

            CollectionAssert.AreEqual(new[] { defaultMap }, maps);
        }

        [Test]
        public void GetBattleMapsForPlanet_SpaceOverride_ReturnsPlanetSpaceMaps()
        {
            BattleMap defaultMap = new BattleMap
            {
                InstanceID = "DEFAULT_SPACE",
                Kind = BattleKind.Space,
            };
            BattleMap planetMap = new BattleMap
            {
                InstanceID = "PLANET_SPACE",
                Kind = BattleKind.Space,
            };
            Planet planet = new Planet { InstanceID = "PLANET" };
            planet.BattleMapInstanceIDs.Add(planetMap.InstanceID);
            PlanetSector sector = CreateSector(planet);
            GameDataCatalog catalog = TestGameData.Create(
                planetSectors: new[] { sector },
                battleMaps: new[] { defaultMap, planetMap },
                defaultSpaceBattleMapInstanceIDs: new[] { defaultMap.InstanceID }
            );

            System.Collections.Generic.IReadOnlyList<BattleMap> maps =
                catalog.GetBattleMapsForPlanet(planet, BattleKind.Space);

            CollectionAssert.AreEqual(new[] { planetMap }, maps);
        }

        [Test]
        public void TryGetBattleMap_ExistingIdentifier_ReturnsAuthoredMap()
        {
            BattleMap map = new BattleMap { InstanceID = "SPACE_MAP", Kind = BattleKind.Space };
            GameDataCatalog catalog = TestGameData.Create(battleMaps: new[] { map });

            bool found = catalog.TryGetBattleMap(map.InstanceID, out BattleMap resolved);

            Assert.IsTrue(found);
            Assert.AreSame(map, resolved);
        }

        [Test]
        public void TryGetBattleMap_MissingIdentifier_ReturnsFalse()
        {
            GameDataCatalog catalog = TestGameData.Create();

            bool found = catalog.TryGetBattleMap("MISSING_MAP", out BattleMap resolved);

            Assert.IsFalse(found);
            Assert.IsNull(resolved);
        }

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
        /// Creates a planet sector containing one planet.
        /// </summary>
        /// <param name="planet">The planet to attach.</param>
        /// <returns>The containing sector.</returns>
        private static PlanetSector CreateSector(Planet planet)
        {
            PlanetSector sector = new PlanetSector();
            sector.AddChild(planet);
            return sector;
        }
    }
}
