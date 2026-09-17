using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Galaxy
{
    [TestFixture]
    public class PlanetTests
    {
        private Planet _planet;

        /// <summary>
        /// Sets up.
        /// </summary>
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

        /// <summary>
        /// Verifies add fleet valid fleet adds to planet.
        /// </summary>
        [Test]
        public void AddFleet_ValidFleet_AddsToPlanet()
        {
            Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(fleet);

            Assert.Contains(
                fleet,
                _planet.GetChildren<Fleet>().ToList(),
                "Fleet should be added to the _planet."
            );
        }

        /// <summary>
        /// Verifies add building invalid owner throws exception.
        /// </summary>
        [Test]
        public void AddBuilding_InvalidOwner_ThrowsException()
        {
            Building building = new Building { OwnerInstanceID = "INVALID" };

            Assert.Throws<SceneAccessException>(
                () => _planet.AddChild(building),
                "Adding a fleet with a mismatched OwnerInstanceID should throw a SceneAccessException."
            );
        }

        /// <summary>
        /// Verifies add building valid building adds to planet.
        /// </summary>
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

        /// <summary>
        /// Verifies add building completed building on uncolonized planet throws exception.
        /// </summary>
        [Test]
        public void AddBuilding_CompletedBuildingOnUncolonizedPlanet_ThrowsException()
        {
            _planet.IsColonized = false;
            Building building = new Building
            {
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            Assert.Throws<SceneAccessException>(() => _planet.AddChild(building));
        }

        /// <summary>
        /// Verifies add building under construction on owned uncolonized planet adds to planet.
        /// </summary>
        [Test]
        public void AddBuilding_UnderConstructionOnOwnedUncolonizedPlanet_AddsToPlanet()
        {
            _planet.IsColonized = false;
            Building building = new Building
            {
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Building,
            };

            _planet.AddChild(building);

            Assert.Contains(building, _planet.GetChildren<Building>().ToList());
        }

        /// <summary>
        /// Verifies add building exceeds capacity throws exception.
        /// </summary>
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

        /// <summary>
        /// Verifies add officer valid officer adds to planet.
        /// </summary>
        [Test]
        public void AddOfficer_ValidOfficer_AddsToPlanet()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(officer);

            Assert.Contains(
                officer,
                _planet.GetChildren<Officer>().ToList(),
                "Officer should be added to the _planet."
            );
        }

        /// <summary>
        /// Verifies add officer invalid owner throws exception.
        /// </summary>
        [Test]
        public void AddOfficer_InvalidOwner_ThrowsException()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = false };

            Assert.Throws<SceneAccessException>(
                () => _planet.AddChild(officer),
                "Adding an officer with a mismatched OwnerInstanceID should throw a SceneAccessException."
            );
        }

        /// <summary>
        /// Verifies add officer captured enemy adds to officers.
        /// </summary>
        [Test]
        public void AddOfficer_CapturedEnemy_AddsToOfficers()
        {
            Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = true };

            _planet.AddChild(officer);

            Assert.Contains(
                officer,
                _planet.GetChildren<Officer>().ToList(),
                "Captured enemy officer should be accepted."
            );
        }

        /// <summary>
        /// Verifies add officer uncolonized planet throws exception.
        /// </summary>
        [Test]
        public void AddOfficer_UncolonizedPlanet_ThrowsException()
        {
            _planet.IsColonized = false;
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };

            Assert.Throws<SceneAccessException>(() => _planet.AddChild(officer));
        }

        /// <summary>
        /// Verifies add regiment uncolonized neutral planet adds to planet.
        /// </summary>
        [Test]
        public void AddRegiment_UncolonizedNeutralPlanet_AddsToPlanet()
        {
            _planet.IsColonized = false;
            _planet.OwnerInstanceID = null;
            Regiment regiment = new Regiment { OwnerInstanceID = "FNALL1" };

            _planet.AddChild(regiment);

            Assert.Contains(regiment, _planet.GetChildren<Regiment>().ToList());
        }

        /// <summary>
        /// Verifies add regiment uncolonized owned planet with matching owner adds to planet.
        /// </summary>
        [Test]
        public void AddRegiment_UncolonizedOwnedPlanetWithMatchingOwner_AddsToPlanet()
        {
            _planet.IsColonized = false;
            Regiment regiment = new Regiment { OwnerInstanceID = "FNALL1" };

            _planet.AddChild(regiment);

            Assert.Contains(regiment, _planet.GetChildren<Regiment>().ToList());
        }

        /// <summary>
        /// Verifies add regiment uncolonized owned planet with different owner throws exception.
        /// </summary>
        [Test]
        public void AddRegiment_UncolonizedOwnedPlanetWithDifferentOwner_ThrowsException()
        {
            _planet.IsColonized = false;
            Regiment regiment = new Regiment { OwnerInstanceID = "FNEMP1" };

            Assert.Throws<SceneAccessException>(() => _planet.AddChild(regiment));
        }

        /// <summary>
        /// Verifies remove fleet valid fleet removes from planet.
        /// </summary>
        [Test]
        public void RemoveFleet_ValidFleet_RemovesFromPlanet()
        {
            Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(fleet);
            _planet.RemoveChild(fleet);

            Assert.IsFalse(
                _planet.GetChildren<Fleet>().Contains(fleet),
                "Fleet should be removed from the _planet."
            );
        }

        /// <summary>
        /// Verifies remove officer valid officer removes from planet.
        /// </summary>
        [Test]
        public void RemoveOfficer_ValidOfficer_RemovesFromPlanet()
        {
            Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(officer);
            _planet.RemoveChild(officer);

            Assert.IsFalse(
                _planet.GetChildren<Officer>().Contains(officer),
                "Officer should be removed from the _planet."
            );
        }

        /// <summary>
        /// Verifies remove building valid building removes from planet.
        /// </summary>
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

        /// <summary>
        /// Verifies get children valid children returns all children.
        /// </summary>
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

        /// <summary>
        /// Verifies get popular support existing faction returns support.
        /// </summary>
        [Test]
        public void GetPopularSupport_ExistingFaction_ReturnsSupport()
        {
            _planet.SetPopularSupport("FNALL1", 50);

            int support = _planet.GetPopularSupport("FNALL1");
            Assert.AreEqual(
                50,
                support,
                "Popular support for the faction should be correctly retrieved."
            );
        }

        /// <summary>
        /// Verifies get popular support non existing faction returns zero.
        /// </summary>
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

        /// <summary>
        /// Verifies set popular support valid faction sets support.
        /// </summary>
        [Test]
        public void SetPopularSupport_ValidFaction_SetsSupport()
        {
            _planet.SetPopularSupport("FNALL1", 75);

            int support = _planet.GetPopularSupport("FNALL1");
            Assert.AreEqual(
                75,
                support,
                "Popular support should be correctly set for the faction."
            );
        }

        /// <summary>
        /// Verifies set popular support increase exceeding total support reduces multiple other factions.
        /// </summary>
        [Test]
        public void SetPopularSupport_IncreaseExceedingTotalSupport_ReducesMultipleOtherFactions()
        {
            _planet.SetPopularSupport("FNEMP1", 40);
            _planet.SetPopularSupport("FNHUTT1", 60);

            _planet.SetPopularSupport("FNALL1", 80);

            Assert.AreEqual(80, _planet.GetPopularSupport("FNALL1"));
            Assert.AreEqual(20, _planet.GetPopularSupport("FNEMP1"));
            Assert.AreEqual(0, _planet.GetPopularSupport("FNHUTT1"));
        }

        /// <summary>
        /// Verifies set full popular support with existing support clears other factions.
        /// </summary>
        [Test]
        public void SetFullPopularSupport_WithExistingSupport_ClearsOtherFactions()
        {
            _planet.SetPopularSupport("FNEMP1", 40);
            _planet.SetPopularSupport("FNHUTT1", 60);

            _planet.SetFullPopularSupport("FNALL1");

            Assert.AreEqual(100, _planet.GetPopularSupport("FNALL1"));
            Assert.AreEqual(0, _planet.GetPopularSupport("FNEMP1"));
            Assert.AreEqual(0, _planet.GetPopularSupport("FNHUTT1"));
        }

        /// <summary>
        /// Verifies add to manufacturing queue unit without parent throws exception.
        /// </summary>
        [Test]
        public void AddToManufacturingQueue_UnitWithoutParent_ThrowsException()
        {
            IManufacturable unit = new Starfighter();

            Assert.Throws<InvalidOperationException>(
                () => _planet.AddToManufacturingQueue(unit),
                "Adding a manufacturable unit without a parent should throw a InvalidOperationException."
            );
        }

        /// <summary>
        /// Verifies add to manufacturing queue items in same lane assigns increasing sequences.
        /// </summary>
        [Test]
        public void AddToManufacturingQueue_ItemsInSameLane_AssignsIncreasingSequences()
        {
            Building first = new Building { OwnerInstanceID = _planet.OwnerInstanceID };
            Building second = new Building { OwnerInstanceID = _planet.OwnerInstanceID };
            _planet.AddChild(first);
            first.SetParent(_planet);
            _planet.AddChild(second);
            second.SetParent(_planet);

            _planet.AddToManufacturingQueue(first);
            _planet.AddToManufacturingQueue(second);

            Assert.AreEqual(1, first.ManufacturingQueueSequence);
            Assert.AreEqual(2, second.ManufacturingQueueSequence);
        }

        /// <summary>
        /// Verifies add to manufacturing queue items in different lanes assigns independent sequences.
        /// </summary>
        [Test]
        public void AddToManufacturingQueue_ItemsInDifferentLanes_AssignsIndependentSequences()
        {
            Building building = new Building { OwnerInstanceID = _planet.OwnerInstanceID };
            Regiment regiment = new Regiment { OwnerInstanceID = _planet.OwnerInstanceID };
            _planet.AddChild(building);
            building.SetParent(_planet);
            _planet.AddChild(regiment);
            regiment.SetParent(_planet);

            _planet.AddToManufacturingQueue(building);
            _planet.AddToManufacturingQueue(regiment);

            Assert.AreEqual(1, building.ManufacturingQueueSequence);
            Assert.AreEqual(1, regiment.ManufacturingQueueSequence);
        }

        /// <summary>
        /// Verifies serialize and deserialize planet retains properties.
        /// </summary>
        [Test]
        public void SerializeAndDeserialize_Planet_RetainsProperties()
        {
            _planet.SetPopularSupport("FNALL1", 100);
            _planet.IsDestroyed = true;
            _planet.SetManufacturingReserved(ManufacturingType.Ship, true);
            _planet.SetManufacturingReserved(ManufacturingType.Building, true);
            _planet.SetManufacturingReserved(ManufacturingType.Troop, true);
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
            Assert.IsTrue(deserialized.IsManufacturingReserved(ManufacturingType.Ship));
            Assert.IsTrue(deserialized.IsManufacturingReserved(ManufacturingType.Building));
            Assert.IsTrue(deserialized.IsManufacturingReserved(ManufacturingType.Troop));
            Assert.AreEqual(
                _planet.GetChildren<Fleet>().Count,
                deserialized.GetChildren<Fleet>().Count,
                "Deserialized planet should retain fleets."
            );
        }

        /// <summary>
        /// Verifies set manufacturing reserved reserved then released updates selected lane only.
        /// </summary>
        [Test]
        public void SetManufacturingReserved_ReservedThenReleased_UpdatesSelectedLaneOnly()
        {
            _planet.SetManufacturingReserved(ManufacturingType.Troop, true);

            Assert.IsTrue(_planet.IsManufacturingReserved(ManufacturingType.Troop));
            Assert.IsFalse(_planet.IsManufacturingReserved(ManufacturingType.Ship));
            Assert.IsFalse(_planet.IsManufacturingReserved(ManufacturingType.Building));

            _planet.SetManufacturingReserved(ManufacturingType.Troop, false);

            Assert.IsFalse(_planet.IsManufacturingReserved(ManufacturingType.Troop));
        }

        /// <summary>
        /// Verifies set manufacturing reserved none throws argument out of range exception.
        /// </summary>
        [Test]
        public void SetManufacturingReserved_None_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _planet.SetManufacturingReserved(ManufacturingType.None, true)
            );
        }

        /// <summary>
        /// Verifies get production rate valid manufacturing type returns correct rate.
        /// </summary>
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

            double rate = _planet.GetProductionRate(ManufacturingType.Ship);

            Assert.AreEqual(
                5.0 / 6.0,
                rate,
                0.0001,
                "Production rate should be calculated correctly based on building process rates."
            );
        }

        /// <summary>
        /// Verifies get raw resource nodes valid planet returns correct count.
        /// </summary>
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

        /// <summary>
        /// Verifies get available resource nodes not blockaded returns raw resource nodes.
        /// </summary>
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

        /// <summary>
        /// Verifies get available resource nodes blockaded returns zero.
        /// </summary>
        [Test]
        public void GetAvailableResourceNodes_Blockaded_ReturnsZero()
        {
            _planet.NumRawResourceNodes = 8;
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            _planet.AddChild(enemyFleet);

            int availableNodes = _planet.GetAvailableResourceNodes();

            Assert.AreEqual(
                0,
                availableNodes,
                "Should return zero when planet is blockaded by enemy fleet."
            );
        }

        /// <summary>
        /// Verifies get building type count with specific type returns correct count.
        /// </summary>
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

        /// <summary>
        /// Verifies get building type count under construction building excludes under construction.
        /// </summary>
        [Test]
        public void GetBuildingTypeCount_UnderConstructionBuilding_ExcludesUnderConstruction()
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

            int mineCount = _planet.GetBuildingTypeCount(BuildingType.Mine);

            Assert.AreEqual(
                1,
                mineCount,
                "Active filter should exclude buildings under construction."
            );
        }

        /// <summary>
        /// Verifies get total building type count under construction building includes under construction.
        /// </summary>
        [Test]
        public void GetTotalBuildingTypeCount_UnderConstructionBuilding_IncludesUnderConstruction()
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

            int mineCount = _planet.GetTotalBuildingTypeCount(BuildingType.Mine);

            Assert.AreEqual(
                2,
                mineCount,
                "All filter should include buildings under construction."
            );
        }

        /// <summary>
        /// Verifies get all buildings multiple slots returns all buildings.
        /// </summary>
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

        /// <summary>
        /// Verifies get all buildings three buildings added returns all three.
        /// </summary>
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

        /// <summary>
        /// Verifies get buildings by manufacturing type returns correct buildings.
        /// </summary>
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

        /// <summary>
        /// Verifies get available energy with buildings returns remaining capacity.
        /// </summary>
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

        /// <summary>
        /// Verifies get available energy with one building returns correct count.
        /// </summary>
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

        /// <summary>
        /// Verifies get available energy inactive building returns full capacity.
        /// </summary>
        [Test]
        public void GetAvailableEnergy_InactiveBuilding_ReturnsFullCapacity()
        {
            _planet.EnergyCapacity = 5;
            Building building = new Building { OwnerInstanceID = "FNALL1", IsEnabled = false };
            _planet.AddChild(building);

            int availableEnergy = _planet.GetAvailableEnergy();

            Assert.AreEqual(5, availableEnergy);
        }

        /// <summary>
        /// Verifies get available energy no buildings returns full capacity.
        /// </summary>
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

        /// <summary>
        /// Verifies get manufacturing queue empty queue returns empty dictionary.
        /// </summary>
        [Test]
        public void GetManufacturingQueue_EmptyQueue_ReturnsEmptyDictionary()
        {
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _planet.GetManufacturingQueue();

            Assert.IsNotNull(queue, "Manufacturing queue should not be null.");
            Assert.AreEqual(0, queue.Count, "Manufacturing queue should be empty initially.");
        }

        /// <summary>
        /// Verifies get manufacturing queue with items returns correct queue.
        /// </summary>
        [Test]
        public void GetManufacturingQueue_WithItems_ReturnsCorrectQueue()
        {
            Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(fleet);
            CapitalShip ship = new CapitalShip { OwnerInstanceID = "FNALL1" };
            fleet.AddChild(ship);
            ship.SetParent(fleet);

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

        /// <summary>
        /// Verifies get idle manufacturing facilities no queue returns all facilities.
        /// </summary>
        [Test]
        public void GetIdleManufacturingFacilities_NoQueue_ReturnsAllFacilities()
        {
            Building shipyard1 = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
                ProcessRate = 1,
            };
            Building shipyard2 = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Complete,
                ProcessRate = 1,
            };

            _planet.AddChild(shipyard1);
            _planet.AddChild(shipyard2);

            int idleFacilities = _planet.GetIdleManufacturingFacilities(ManufacturingType.Ship);

            Assert.AreEqual(2, idleFacilities, "Should return all facilities when queue is empty.");
        }

        /// <summary>
        /// Verifies get idle manufacturing facilities with queue returns zero.
        /// </summary>
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
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            _planet.AddToManufacturingQueue(ship);

            int idleFacilities = _planet.GetIdleManufacturingFacilities(ManufacturingType.Ship);

            Assert.AreEqual(
                0,
                idleFacilities,
                "Should return zero when manufacturing queue has items."
            );
        }

        /// <summary>
        /// Verifies add starfighter valid starfighter adds to planet.
        /// </summary>
        [Test]
        public void AddStarfighter_ValidStarfighter_AddsToPlanet()
        {
            Starfighter starfighter = new Starfighter { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(starfighter);

            Assert.Contains(
                starfighter,
                _planet.GetChildren<Starfighter>().ToList(),
                "Starfighter should be added to the _planet."
            );
        }

        /// <summary>
        /// Verifies add starfighter invalid owner throws exception.
        /// </summary>
        [Test]
        public void AddStarfighter_InvalidOwner_ThrowsException()
        {
            Starfighter starfighter = new Starfighter { OwnerInstanceID = "INVALID" };

            Assert.Throws<SceneAccessException>(
                () => _planet.AddChild(starfighter),
                "Adding a starfighter with a mismatched OwnerInstanceID should throw a SceneAccessException."
            );
        }

        /// <summary>
        /// Verifies add starfighter uncolonized planet throws exception.
        /// </summary>
        [Test]
        public void AddStarfighter_UncolonizedPlanet_ThrowsException()
        {
            _planet.IsColonized = false;
            Starfighter starfighter = new Starfighter { OwnerInstanceID = "FNALL1" };

            Assert.Throws<SceneAccessException>(() => _planet.AddChild(starfighter));
        }

        /// <summary>
        /// Verifies add special forces uncolonized planet throws exception.
        /// </summary>
        [Test]
        public void AddSpecialForces_UncolonizedPlanet_ThrowsException()
        {
            _planet.IsColonized = false;
            SpecialForces specialForces = new SpecialForces { OwnerInstanceID = "FNALL1" };

            Assert.Throws<SceneAccessException>(() => _planet.AddChild(specialForces));
        }

        /// <summary>
        /// Verifies remove starfighter valid starfighter removes from planet.
        /// </summary>
        [Test]
        public void RemoveStarfighter_ValidStarfighter_RemovesFromPlanet()
        {
            Starfighter starfighter = new Starfighter { OwnerInstanceID = "FNALL1" };
            _planet.AddChild(starfighter);
            _planet.RemoveChild(starfighter);

            Assert.IsFalse(
                _planet.GetChildren<Starfighter>().Contains(starfighter),
                "Starfighter should be removed from the _planet."
            );
        }

        /// <summary>
        /// Verifies get starfighter count after adding returns correct count.
        /// </summary>
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

        /// <summary>
        /// Verifies is blockaded no enemy fleets returns false.
        /// </summary>
        [Test]
        public void IsBlockaded_NoEnemyFleets_ReturnsFalse()
        {
            Fleet friendlyFleet = CreateOperationalFleet("FNALL1");
            _planet.AddChild(friendlyFleet);

            bool isBlockaded = _planet.IsBlockaded();

            Assert.IsFalse(
                isBlockaded,
                "Planet should not be blockaded with only friendly fleets."
            );
        }

        /// <summary>
        /// Verifies is blockaded enemy fleet without capital ships returns false.
        /// </summary>
        [Test]
        public void IsBlockaded_EnemyFleetWithoutCapitalShips_ReturnsFalse()
        {
            Fleet enemyFleet = new Fleet { OwnerInstanceID = "ENEMY" };
            _planet.AddChild(enemyFleet);

            Assert.IsFalse(_planet.IsBlockaded());
        }

        /// <summary>
        /// Verifies is blockaded enemy fleet with operational capital ship returns true.
        /// </summary>
        [Test]
        public void IsBlockaded_EnemyFleetWithOperationalCapitalShip_ReturnsTrue()
        {
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            _planet.AddChild(enemyFleet);

            bool isBlockaded = _planet.IsBlockaded();

            Assert.IsTrue(
                isBlockaded,
                "Planet should be blockaded when an enemy operational capital ship is present."
            );
        }

        /// <summary>
        /// Verifies is blockaded neutral planet with operational fleet returns true.
        /// </summary>
        [Test]
        public void IsBlockaded_NeutralPlanetWithOperationalFleet_ReturnsTrue()
        {
            _planet.OwnerInstanceID = null;
            _planet.AddChild(CreateOperationalFleet("FNALL1"));

            Assert.IsTrue(_planet.IsBlockaded());
        }

        /// <summary>
        /// Verifies is blockaded for neutral planet blockading faction returns false.
        /// </summary>
        [Test]
        public void IsBlockadedFor_NeutralPlanetBlockadingFaction_ReturnsFalse()
        {
            _planet.OwnerInstanceID = null;
            _planet.AddChild(CreateOperationalFleet("FNALL1"));

            Assert.IsFalse(_planet.IsBlockadedFor("FNALL1"));
        }

        /// <summary>
        /// Verifies is blockaded for neutral planet opposing faction returns true.
        /// </summary>
        [Test]
        public void IsBlockadedFor_NeutralPlanetOpposingFaction_ReturnsTrue()
        {
            _planet.OwnerInstanceID = null;
            _planet.AddChild(CreateOperationalFleet("FNALL1"));

            Assert.IsTrue(_planet.IsBlockadedFor("ENEMY"));
        }

        /// <summary>
        /// Verifies is blockaded inactive enemy fleet returns false.
        /// </summary>
        [Test]
        public void IsBlockaded_InactiveEnemyFleet_ReturnsFalse()
        {
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            enemyFleet.IsEnabled = false;
            _planet.AddChild(enemyFleet);

            Assert.IsFalse(_planet.IsBlockaded());
        }

        /// <summary>
        /// Verifies get blockade production modifier active ships and fighters reduces production.
        /// </summary>
        [Test]
        public void GetBlockadeProductionModifier_ActiveShipsAndFighters_ReducesProduction()
        {
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            CapitalShip activeShip = enemyFleet.GetChildren<CapitalShip>().Single();
            activeShip.AddTestChild(
                new Starfighter
                {
                    OwnerInstanceID = "ENEMY",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            activeShip.AddTestChild(
                new Starfighter
                {
                    OwnerInstanceID = "ENEMY",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            activeShip.AddTestChild(
                new Starfighter
                {
                    OwnerInstanceID = "ENEMY",
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );
            enemyFleet.AddChild(
                new CapitalShip
                {
                    OwnerInstanceID = "ENEMY",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    Movement = new MovementState { TransitTicks = 10 },
                }
            );
            _planet.AddChild(enemyFleet);
            _planet.AddChild(
                new Starfighter
                {
                    OwnerInstanceID = "FNALL1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            int modifier = _planet.GetBlockadeProductionModifier(5, 2);

            Assert.AreEqual(89, modifier);
        }

        /// <summary>
        /// Verifies get blockade production modifier operational kdy returns full production.
        /// </summary>
        [Test]
        public void GetBlockadeProductionModifier_OperationalKdy_ReturnsFullProduction()
        {
            _planet.AddChild(CreateOperationalFleet("ENEMY"));
            _planet.AddChild(
                new Building
                {
                    OwnerInstanceID = "FNALL1",
                    BuildingType = BuildingType.Weapon,
                    DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.AreEqual(100, _planet.GetBlockadeProductionModifier(5, 2));
        }

        /// <summary>
        /// Verifies get blockade production modifier heavy blockade does not return negative production.
        /// </summary>
        [Test]
        public void GetBlockadeProductionModifier_HeavyBlockade_DoesNotReturnNegativeProduction()
        {
            _planet.AddChild(CreateOperationalFleet("ENEMY"));

            Assert.AreEqual(0, _planet.GetBlockadeProductionModifier(100, 2));
        }

        /// <summary>
        /// Verifies is blockaded enemy fleet in transit returns false.
        /// </summary>
        [Test]
        public void IsBlockaded_EnemyFleetInTransit_ReturnsFalse()
        {
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            enemyFleet.Movement = new MovementState { TransitTicks = 10 };
            _planet.AddChild(enemyFleet);

            bool isBlockaded = _planet.IsBlockaded();

            Assert.IsFalse(isBlockaded);
        }

        /// <summary>
        /// Verifies is blockaded enemy capital ship in transit returns false.
        /// </summary>
        [Test]
        public void IsBlockaded_EnemyCapitalShipInTransit_ReturnsFalse()
        {
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            enemyFleet.GetChildren<CapitalShip>().Single().Movement = new MovementState
            {
                TransitTicks = 10,
            };
            _planet.AddChild(enemyFleet);

            Assert.IsFalse(_planet.IsBlockaded());
        }

        /// <summary>
        /// Verifies is blockaded defending fleet present returns false.
        /// </summary>
        [Test]
        public void IsBlockaded_DefendingFleetPresent_ReturnsFalse()
        {
            Fleet friendlyFleet = CreateOperationalFleet("FNALL1");
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            _planet.AddChild(friendlyFleet);
            _planet.AddChild(enemyFleet);

            bool isBlockaded = _planet.IsBlockaded();

            Assert.IsFalse(
                isBlockaded,
                "Planet should not be blockaded when defending fleets are present."
            );
        }

        /// <summary>
        /// Verifies is blockaded defending fleet in transit returns true.
        /// </summary>
        [Test]
        public void IsBlockaded_DefendingFleetInTransit_ReturnsTrue()
        {
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            Fleet friendlyFleet = CreateOperationalFleet("FNALL1");
            friendlyFleet.Movement = new MovementState { TransitTicks = 10 };
            _planet.AddChild(enemyFleet);
            _planet.AddChild(friendlyFleet);

            bool isBlockaded = _planet.IsBlockaded();

            Assert.IsTrue(isBlockaded);
        }

        /// <summary>
        /// Verifies is blockaded for opposing faction returns true.
        /// </summary>
        [Test]
        public void IsBlockadedFor_OpposingFaction_ReturnsTrue()
        {
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            _planet.AddChild(enemyFleet);

            bool isBlockaded = _planet.IsBlockadedFor("FNALL1");

            Assert.IsTrue(isBlockaded);
        }

        /// <summary>
        /// Verifies is blockaded for blockading faction returns false.
        /// </summary>
        [Test]
        public void IsBlockadedFor_BlockadingFaction_ReturnsFalse()
        {
            Fleet enemyFleet = CreateOperationalFleet("ENEMY");
            _planet.AddChild(enemyFleet);

            bool isBlockaded = _planet.IsBlockadedFor("ENEMY");

            Assert.IsFalse(isBlockaded);
        }

        /// <summary>
        /// Verifies begin uprising non uprising planet sets is in uprising flag.
        /// </summary>
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

        /// <summary>
        /// Verifies end uprising uprising planet clears is in uprising flag.
        /// </summary>
        [Test]
        public void EndUprising_UprisingPlanet_ClearsIsInUprisingFlag()
        {
            Planet planet = new Planet { InstanceID = "p1", IsInUprising = true };

            _planet.EndUprising();

            Assert.IsFalse(_planet.IsInUprising);
        }

        /// <summary>
        /// Verifies is populated no support returns false.
        /// </summary>
        [Test]
        public void IsPopulated_NoSupport_ReturnsFalse()
        {
            Planet planet = new Planet { PopularSupport = new Dictionary<string, int>() };

            bool populated = _planet.IsPopulated();

            Assert.IsFalse(populated);
        }

        /// <summary>
        /// Verifies is populated with support returns true.
        /// </summary>
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

        /// <summary>
        /// Verifies get active mined resources one complete one building mine returns one.
        /// </summary>
        [Test]
        public void GetActiveMinedResources_OneCompleteOneBuildingMine_ReturnsOne()
        {
            Planet planet = new Planet
            {
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 5,
                NumRawResourceNodes = 10,
            };
            Building completeMine = new Building
            {
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building buildingMine = new Building
            {
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            planet.AddChild(completeMine);
            planet.AddChild(buildingMine);

            Assert.AreEqual(1, planet.GetActiveMinedResources());
        }

        /// <summary>
        /// Verifies get active mined resources more mines than nodes capped by nodes.
        /// </summary>
        [Test]
        public void GetActiveMinedResources_MoreMinesThanNodes_CappedByNodes()
        {
            Planet planet = new Planet
            {
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 2,
            };
            for (int i = 0; i < 5; i++)
            {
                planet.AddChild(
                    new Building
                    {
                        OwnerInstanceID = "empire",
                        BuildingType = BuildingType.Mine,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    }
                );
            }

            Assert.AreEqual(2, planet.GetActiveMinedResources());
        }

        /// <summary>
        /// Verifies get active refinement capacity one complete one building refinery returns one.
        /// </summary>
        [Test]
        public void GetActiveRefinementCapacity_OneCompleteOneBuildingRefinery_ReturnsOne()
        {
            Planet planet = new Planet
            {
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 5,
            };
            Building completeRefinery = new Building
            {
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Refinery,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Building buildingRefinery = new Building
            {
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Refinery,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            planet.AddChild(completeRefinery);
            planet.AddChild(buildingRefinery);

            Assert.AreEqual(1, planet.GetActiveRefinementCapacity());
        }

        /// <summary>
        /// Verifies get raw distance to position returns euclidean distance.
        /// </summary>
        [Test]
        public void GetRawDistanceTo_Position_ReturnsEuclideanDistance()
        {
            _planet.PositionX = 3;
            _planet.PositionY = 4;

            double distance = _planet.GetRawDistanceTo(new Point(0, 0));

            Assert.AreEqual(5, distance);
        }

        /// <summary>
        /// Verifies get raw distance to planet returns euclidean distance.
        /// </summary>
        [Test]
        public void GetRawDistanceTo_Planet_ReturnsEuclideanDistance()
        {
            _planet.PositionX = 3;
            _planet.PositionY = 4;
            Planet targetPlanet = new Planet { PositionX = 0, PositionY = 0 };

            double distance = _planet.GetRawDistanceTo(targetPlanet);

            Assert.AreEqual(5, distance);
        }

        /// <summary>
        /// Creates operational fleet.
        /// </summary>
        /// <param name="ownerInstanceID">The owner instance id.</param>
        /// <returns>The created operational fleet.</returns>
        private static Fleet CreateOperationalFleet(string ownerInstanceID)
        {
            Fleet fleet = new Fleet { OwnerInstanceID = ownerInstanceID };
            fleet.AddChild(
                new CapitalShip
                {
                    OwnerInstanceID = ownerInstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            return fleet;
        }
    }
} // namespace Rebellion.Tests.Game.Galaxy
