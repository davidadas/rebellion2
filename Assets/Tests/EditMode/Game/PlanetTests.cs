using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class PlanetTests
    {
        private Planet _planet;

        [SetUp]
        public void Setup()
        {
            _planet = new Planet
            {
                IsColonized = true,
                EnergyCapacity = 5,
                OwnerInstanceID = "FNALL1",
            };
        }

        [Test]
        public void AddFleet_ValidFleet_AddsToPlanet()
        {
            Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(fleet);

            Assert.Contains(fleet, _planet.Fleets, "Fleet should be added to the _planet.");
        }

        [Test]
        public void AddBuilding_InvalidOwner_ThrowsException()
        {
            Building building = new Building { OwnerInstanceID = "INVALID" };

            Assert.Throws<SceneAccessException>(
                () => _planet.AddChild(building),
                "Adding a fleet with a mismatched OwnerInstanceID should throw a SceneAccessException."
            );
        }

        [Test]
        public void AddOfficer_ValidOfficer_AddsToPlanet()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(officer);

            Assert.Contains(officer, _planet.Officers, "Officer should be added to the _planet.");
        }

        [Test]
        public void AddOfficer_InvalidOwner_ThrowsException()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = false };

            Assert.Throws<SceneAccessException>(
                () => _planet.AddChild(officer),
                "Adding an officer with a mismatched OwnerInstanceID should throw a SceneAccessException."
            );
        }

        [Test]
        public void AddOfficer_CapturedEnemy_AddsToOfficers()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = true };

            _planet.AddChild(officer);

            Assert.Contains(
                officer,
                _planet.Officers,
                "Captured enemy officer should be accepted."
            );
        }

        [Test]
        public void AddBuilding_ValidBuilding_AddsToPlanet()
        {
            Building building = new Building
            {
                DisplayName = "Test Building",
                OwnerInstanceID = "FNALL1",
            };

            _planet.AddChild(building);

            List<Building> buildings = _planet.GetAllBuildings();
            Assert.Contains(building, buildings, "Building should be added to the _planet.");
        }

        [Test]
        public void AddBuilding_ExceedsCapacity_ThrowsException()
        {
            for (int i = 0; i < _planet.EnergyCapacity; i++)
            {
                _planet.AddChild(new Building { OwnerInstanceID = "FNALL1" });
            }

            Building extraBuilding = new Building { OwnerInstanceID = "FNALL1" };

            Assert.Throws<InvalidOperationException>(
                () => _planet.AddChild(extraBuilding),
                "Adding a building when slots are full should throw a InvalidOperationException."
            );
        }

        [Test]
        public void RemoveFleet_ValidFleet_RemovesFromPlanet()
        {
            Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(fleet);
            _planet.RemoveChild(fleet);

            Assert.IsFalse(
                _planet.Fleets.Contains(fleet),
                "Fleet should be removed from the _planet."
            );
        }

        [Test]
        public void RemoveOfficer_ValidOfficer_RemovesFromPlanet()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(officer);
            _planet.RemoveChild(officer);

            Assert.IsFalse(
                _planet.Officers.Contains(officer),
                "Officer should be removed from the _planet."
            );
        }

        [Test]
        public void RemoveBuilding_ValidBuilding_RemovesFromPlanet()
        {
            Building building = new Building
            {
                DisplayName = "Test Building",
                OwnerInstanceID = "FNALL1",
            };

            _planet.AddChild(building);
            _planet.RemoveChild(building);

            Assert.IsFalse(
                _planet.GetAllBuildings().Contains(building),
                "Building should be removed from the _planet."
            );
        }

        [Test]
        public void GetChildren_ValidChildren_ReturnsAllChildren()
        {
            Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            Building building = new Building { OwnerInstanceID = "FNALL1" };

            _planet.AddChild(fleet);
            _planet.AddChild(officer);
            _planet.AddChild(building);

            IEnumerable<ISceneNode> children = _planet.GetChildren();
            List<ISceneNode> expectedChildren = new List<ISceneNode> { fleet, officer, building };

            CollectionAssert.AreEquivalent(
                expectedChildren,
                children,
                "Planet should return all correct children."
            );
        }

        [Test]
        public void GetPopularSupport_ExistingFaction_ReturnsSupport()
        {
            _planet.SetPopularSupport("FNALL1", 50, 100);

            int support = _planet.GetPopularSupport("FNALL1");
            Assert.AreEqual(
                50,
                support,
                "Popular support for the faction should be correctly retrieved."
            );
        }

        [Test]
        public void GetPopularSupport_NonExistingFaction_ReturnsZero()
        {
            int support = _planet.GetPopularSupport("INVALID");
            Assert.AreEqual(
                0,
                support,
                "Popular support for a non-existing faction should return 0."
            );
        }

        [Test]
        public void SetPopularSupport_ValidFaction_SetsSupport()
        {
            _planet.SetPopularSupport("FNALL1", 75, 100);

            int support = _planet.GetPopularSupport("FNALL1");
            Assert.AreEqual(
                75,
                support,
                "Popular support should be correctly set for the faction."
            );
        }

        [Test]
        public void GetDistanceTo_ValidTargetPlanet_ReturnsCorrectTime()
        {
            Planet targetPlanet = new Planet
            {
                PositionX = _planet.GetPosition().X + 10,
                PositionY = _planet.GetPosition().Y + 10,
            };

            int travelTime = _planet.GetDistanceTo(targetPlanet, 5, 100);

            Assert.AreEqual(2, travelTime, "Travel time should be calculated correctly.");
        }

        [Test]
        public void AddToManufacturingQueue_UnitWithoutParent_ThrowsException()
        {
            IManufacturable unit = new Starfighter();

            Assert.Throws<InvalidOperationException>(
                () => _planet.AddToManufacturingQueue(unit),
                "Adding a manufacturable unit without a parent should throw a InvalidOperationException."
            );
        }

        [Test]
        public void SerializeAndDeserialize_Planet_RetainsProperties()
        {
            _planet.SetPopularSupport("FNALL1", 100, 100);
            _planet.IsDestroyed = true;
            _planet.AddChild(new Fleet { OwnerInstanceID = "FNALL1" });

            string serialized = SerializationHelper.Serialize(_planet);
            Planet deserialized = SerializationHelper.Deserialize<Planet>(serialized);

            Assert.AreEqual(
                _planet.IsDestroyed,
                deserialized.IsDestroyed,
                "Deserialized planet should retain IsDestroyed property."
            );
            Assert.AreEqual(
                _planet.GetPopularSupport("FNALL1"),
                deserialized.GetPopularSupport("FNALL1"),
                "Deserialized planet should retain popular support."
            );
            Assert.AreEqual(
                _planet.Fleets.Count,
                deserialized.Fleets.Count,
                "Deserialized planet should retain fleets."
            );
        }

        [Test]
        public void GetProductionRate_ValidManufacturingType_ReturnsCorrectRate()
        {
            Building building1 = new Building
            {
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 2,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building building2 = new Building
            {
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 3,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            _planet.AddChild(building1);
            _planet.AddChild(building2);

            int rate = _planet.GetProductionRate(ManufacturingType.Ship);

            Assert.AreEqual(
                1,
                rate,
                "Production rate should be calculated correctly based on building process rates."
            );
        }

        [Test]
        public void GetRawResourceNodes_ValidPlanet_ReturnsCorrectCount()
        {
            _planet.NumRawResourceNodes = 10;

            int resourceNodes = _planet.GetRawResourceNodes();

            Assert.AreEqual(
                10,
                resourceNodes,
                "Should return the total number of raw resource nodes."
            );
        }

        [Test]
        public void GetAvailableResourceNodes_NotBlockaded_ReturnsRawResourceNodes()
        {
            _planet.NumRawResourceNodes = 8;

            int availableNodes = _planet.GetAvailableResourceNodes();

            Assert.AreEqual(
                8,
                availableNodes,
                "Should return raw resource nodes when planet is not blockaded."
            );
        }

        [Test]
        public void GetAvailableResourceNodes_Blockaded_ReturnsZero()
        {
            _planet.NumRawResourceNodes = 8;
            Fleet enemyFleet = new Fleet { OwnerInstanceID = "ENEMY" };
            _planet.AddChild(enemyFleet);

            int availableNodes = _planet.GetAvailableResourceNodes();

            Assert.AreEqual(
                0,
                availableNodes,
                "Should return zero when planet is blockaded by enemy fleet."
            );
        }

        [Test]
        public void GetBuildingTypeCount_WithSpecificType_ReturnsCorrectCount()
        {
            Building mine1 = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building mine2 = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            _planet.AddChild(mine1);
            _planet.AddChild(mine2);
            _planet.AddChild(refinery);

            int mineCount = _planet.GetBuildingTypeCount(BuildingType.Mine);

            Assert.AreEqual(
                2,
                mineCount,
                "Should return the correct count of active mine buildings."
            );
        }

        [Test]
        public void GetBuildingTypeCount_ActiveFilter_ExcludesUnderConstruction()
        {
            Building completedMine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building underConstructionMine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Building,
            };

            _planet.AddChild(completedMine);
            _planet.AddChild(underConstructionMine);

            int mineCount = _planet.GetBuildingTypeCount(
                BuildingType.Mine,
                EntityStateFilter.Active
            );

            Assert.AreEqual(
                1,
                mineCount,
                "Active filter should exclude buildings under construction."
            );
        }

        [Test]
        public void GetBuildingTypeCount_AllFilter_IncludesUnderConstruction()
        {
            Building completedMine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building underConstructionMine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Building,
            };

            _planet.AddChild(completedMine);
            _planet.AddChild(underConstructionMine);

            int mineCount = _planet.GetBuildingTypeCount(BuildingType.Mine, EntityStateFilter.All);

            Assert.AreEqual(
                2,
                mineCount,
                "All filter should include buildings under construction."
            );
        }

        [Test]
        public void GetAllBuildings_MultipleSlots_ReturnsAllBuildings()
        {
            Building groundBuilding = new Building { OwnerInstanceID = "FNALL1" };
            Building orbitBuilding = new Building { OwnerInstanceID = "FNALL1" };

            _planet.AddChild(groundBuilding);
            _planet.AddChild(orbitBuilding);

            List<Building> allBuildings = _planet.GetAllBuildings();

            Assert.AreEqual(2, allBuildings.Count, "Should return all buildings from all slots.");
            Assert.Contains(groundBuilding, allBuildings, "Should include ground building.");
            Assert.Contains(orbitBuilding, allBuildings, "Should include orbit building.");
        }

        [Test]
        public void GetAllBuildings_ThreeBuildingsAdded_ReturnsAllThree()
        {
            Building building1 = new Building { OwnerInstanceID = "FNALL1" };
            Building building2 = new Building { OwnerInstanceID = "FNALL1" };
            Building building3 = new Building { OwnerInstanceID = "FNALL1" };

            _planet.AddChild(building1);
            _planet.AddChild(building2);
            _planet.AddChild(building3);

            List<Building> allBuildings = _planet.GetAllBuildings();

            Assert.AreEqual(3, allBuildings.Count, "Should return all buildings.");
            Assert.Contains(building1, allBuildings, "Should include first building.");
            Assert.Contains(building2, allBuildings, "Should include second building.");
            Assert.Contains(building3, allBuildings, "Should include third building.");
        }

        [Test]
        public void GetBuildings_ByManufacturingType_ReturnsCorrectBuildings()
        {
            Building shipyard1 = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FNALL1",
            };
            Building shipyard2 = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FNALL1",
            };
            Building troopFacility = new Building
            {
                ProductionType = ManufacturingType.Troop,
                OwnerInstanceID = "FNALL1",
            };

            _planet.AddChild(shipyard1);
            _planet.AddChild(shipyard2);
            _planet.AddChild(troopFacility);

            List<Building> shipBuildings = _planet.GetBuildings(ManufacturingType.Ship);

            Assert.AreEqual(
                2,
                shipBuildings.Count,
                "Should return only ship manufacturing buildings."
            );
            Assert.Contains(shipyard1, shipBuildings, "Should include first shipyard.");
            Assert.Contains(shipyard2, shipBuildings, "Should include second shipyard.");
        }

        [Test]
        public void GetAvailableEnergy_WithBuildings_ReturnsRemainingCapacity()
        {
            _planet.EnergyCapacity = 5;
            Building building1 = new Building { OwnerInstanceID = "FNALL1" };
            Building building2 = new Building { OwnerInstanceID = "FNALL1" };

            _planet.AddChild(building1);
            _planet.AddChild(building2);

            int available = _planet.GetAvailableEnergy();

            Assert.AreEqual(3, available, "Should return remaining energy capacity.");
        }

        [Test]
        public void GetAvailableEnergy_WithOneBuilding_ReturnsCorrectCount()
        {
            _planet.EnergyCapacity = 5;
            Building building = new Building { OwnerInstanceID = "FNALL1" };

            _planet.AddChild(building);

            int availableEnergy = _planet.GetAvailableEnergy();

            Assert.AreEqual(
                4,
                availableEnergy,
                "Should return correct amount of available energy."
            );
        }

        [Test]
        public void GetAvailableEnergy_NoBuildings_ReturnsFullCapacity()
        {
            int availableEnergy = _planet.GetAvailableEnergy();

            Assert.AreEqual(
                5,
                availableEnergy,
                "Should return full energy capacity when no buildings exist."
            );
        }

        [Test]
        public void GetManufacturingQueue_EmptyQueue_ReturnsEmptyDictionary()
        {
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _planet.GetManufacturingQueue();

            Assert.IsNotNull(queue, "Manufacturing queue should not be null.");
            Assert.AreEqual(0, queue.Count, "Manufacturing queue should be empty initially.");
        }

        [Test]
        public void GetManufacturingQueue_WithItems_ReturnsCorrectQueue()
        {
            Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(fleet);
            CapitalShip ship = new CapitalShip { OwnerInstanceID = "FNALL1" };
            ship.SetParent(_planet);

            _planet.AddToManufacturingQueue(ship);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _planet.GetManufacturingQueue();

            Assert.IsTrue(
                queue.ContainsKey(ManufacturingType.Ship),
                "Queue should contain ship manufacturing type."
            );
            Assert.AreEqual(
                1,
                queue[ManufacturingType.Ship].Count,
                "Queue should contain one ship."
            );
            Assert.Contains(
                ship,
                queue[ManufacturingType.Ship],
                "Queue should contain the added ship."
            );
        }

        [Test]
        public void GetIdleManufacturingFacilities_NoQueue_ReturnsAllFacilities()
        {
            Building shipyard1 = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building shipyard2 = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            _planet.AddChild(shipyard1);
            _planet.AddChild(shipyard2);

            int idleFacilities = _planet.GetIdleManufacturingFacilities(ManufacturingType.Ship);

            Assert.AreEqual(2, idleFacilities, "Should return all facilities when queue is empty.");
        }

        [Test]
        public void GetIdleManufacturingFacilities_WithQueue_ReturnsZero()
        {
            Building shipyard = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FNALL1",
            };
            _planet.AddChild(shipyard);

            Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(fleet);
            CapitalShip ship = new CapitalShip { OwnerInstanceID = "FNALL1" };
            ship.SetParent(_planet);
            _planet.AddToManufacturingQueue(ship);

            int idleFacilities = _planet.GetIdleManufacturingFacilities(ManufacturingType.Ship);

            Assert.AreEqual(
                0,
                idleFacilities,
                "Should return zero when manufacturing queue has items."
            );
        }

        [Test]
        public void AddStarfighter_ValidStarfighter_AddsToPlanet()
        {
            Starfighter starfighter = new Starfighter { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(starfighter);

            Assert.Contains(
                starfighter,
                _planet.Starfighters,
                "Starfighter should be added to the _planet."
            );
        }

        [Test]
        public void AddStarfighter_InvalidOwner_ThrowsException()
        {
            Starfighter starfighter = new Starfighter { OwnerInstanceID = "INVALID" };

            Assert.Throws<SceneAccessException>(
                () => _planet.AddChild(starfighter),
                "Adding a starfighter with a mismatched OwnerInstanceID should throw a SceneAccessException."
            );
        }

        [Test]
        public void RemoveStarfighter_ValidStarfighter_RemovesFromPlanet()
        {
            Starfighter starfighter = new Starfighter { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(starfighter);
            _planet.RemoveChild(starfighter);

            Assert.IsFalse(
                _planet.Starfighters.Contains(starfighter),
                "Starfighter should be removed from the _planet."
            );
        }

        [Test]
        public void GetStarfighterCount_AfterAdding_ReturnsCorrectCount()
        {
            _planet.AddChild(new Starfighter { OwnerInstanceID = "FNALL1" });
            _planet.AddChild(new Starfighter { OwnerInstanceID = "FNALL1" });

            Assert.AreEqual(
                2,
                _planet.GetStarfighterCount(),
                "Should return correct starfighter count."
            );
        }

        [Test]
        public void IsBlockaded_NoEnemyFleets_ReturnsFalse()
        {
            Fleet friendlyFleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(friendlyFleet);

            bool isBlockaded = _planet.IsBlockaded();

            Assert.IsFalse(
                isBlockaded,
                "Planet should not be blockaded with only friendly fleets."
            );
        }

        [Test]
        public void IsBlockaded_EnemyFleetPresent_ReturnsTrue()
        {
            Fleet enemyFleet = new Fleet { OwnerInstanceID = "ENEMY" };
            _planet.AddChild(enemyFleet);

            bool isBlockaded = _planet.IsBlockaded();

            Assert.IsTrue(isBlockaded, "Planet should be blockaded when enemy fleet is present.");
        }

        [Test]
        public void IsBlockaded_MixedFleets_ReturnsFalse()
        {
            Fleet friendlyFleet = new Fleet { OwnerInstanceID = "FNALL1" };
            Fleet enemyFleet = new Fleet { OwnerInstanceID = "ENEMY" };
            _planet.AddChild(friendlyFleet);
            _planet.AddChild(enemyFleet);

            bool isBlockaded = _planet.IsBlockaded();

            Assert.IsFalse(
                isBlockaded,
                "Planet should not be blockaded when defending fleets are present."
            );
        }

        [Test]
        public void IsContested_MixedFleets_ReturnsTrue()
        {
            Fleet friendlyFleet = new Fleet { OwnerInstanceID = "FNALL1" };
            Fleet enemyFleet = new Fleet { OwnerInstanceID = "ENEMY" };
            _planet.AddChild(friendlyFleet);
            _planet.AddChild(enemyFleet);

            bool isContested = _planet.IsContested();

            Assert.IsTrue(
                isContested,
                "Planet should be contested when any enemy fleet is present."
            );
        }

        [Test]
        public void IsContested_OnlyFriendly_ReturnsFalse()
        {
            Fleet friendlyFleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(friendlyFleet);

            bool isContested = _planet.IsContested();

            Assert.IsFalse(
                isContested,
                "Planet should not be contested with only friendly fleets."
            );
        }

        [Test]
        public void IsContested_OnlyEnemy_ReturnsTrue()
        {
            Fleet enemyFleet = new Fleet { OwnerInstanceID = "ENEMY" };
            _planet.AddChild(enemyFleet);

            bool isContested = _planet.IsContested();

            Assert.IsTrue(
                isContested,
                "Planet should be contested when only enemy fleet is present."
            );
        }

        [Test]
        public void IsContested_NoFleets_ReturnsFalse()
        {
            bool isContested = _planet.IsContested();

            Assert.IsFalse(isContested, "Planet should not be contested with no fleets.");
        }

        [Test]
        public void BeginUprising_NonUprisingPlanet_SetsIsInUprisingFlag()
        {
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "FNEMP1",
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };

            _planet.BeginUprising();

            Assert.IsTrue(_planet.IsInUprising);
        }

        [Test]
        public void EndUprising_UprisingPlanet_ClearsIsInUprisingFlag()
        {
            Planet planet = new Planet { InstanceID = "p1", IsInUprising = true };

            _planet.EndUprising();

            Assert.IsFalse(_planet.IsInUprising);
        }

        [Test]
        public void CalculateLoyalty_OwnerAt50_Returns0()
        {
            Planet planet = new Planet
            {
                OwnerInstanceID = "FNEMP1",
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };

            int loyalty = _planet.CalculateLoyalty();

            Assert.AreEqual(0, loyalty);
        }

        [Test]
        public void CalculateLoyalty_NoOwner_Returns0()
        {
            Planet planet = new Planet
            {
                OwnerInstanceID = null,
                PopularSupport = new Dictionary<string, int>(),
            };

            int loyalty = _planet.CalculateLoyalty();

            Assert.AreEqual(0, loyalty);
        }

        [Test]
        public void IsPopulated_NoSupport_ReturnsFalse()
        {
            Planet planet = new Planet { PopularSupport = new Dictionary<string, int>() };

            bool populated = _planet.IsPopulated();

            Assert.IsFalse(populated);
        }

        [Test]
        public void IsPopulated_WithSupport_ReturnsTrue()
        {
            Planet planet = new Planet
            {
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };

            bool populated = planet.IsPopulated();

            Assert.IsTrue(populated);
        }
    }
} // namespace Rebellion.Tests.Game
