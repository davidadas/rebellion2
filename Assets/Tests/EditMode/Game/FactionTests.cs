using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class FactionTests
    {
        private Faction _faction;
        private Planet _planet1;
        private Planet _planet2;
        private Fleet _fleet;
        private Officer _officer;
        private Building _building;
        private Technology _technology;

        [SetUp]
        public void SetUp()
        {
            _faction = new Faction
            {
                InstanceID = "FACTION1",
                DisplayName = "Rebel Alliance",
                PlayerID = "PLAYER1",
            };

            _planet1 = new Planet { InstanceID = "PLANET1", OwnerInstanceID = "FACTION1" };

            _planet2 = new Planet { InstanceID = "PLANET2", OwnerInstanceID = "FACTION1" };

            _fleet = new Fleet { InstanceID = "FLEET1", OwnerInstanceID = "FACTION1" };

            _officer = new Officer
            {
                InstanceID = "OFFICER1",
                OwnerInstanceID = "FACTION1",
                Movement = null,
            };

            _building = new Building
            {
                InstanceID = "BUILDING1",
                DisplayName = "Mine",
                ConstructionCost = 100,
                ResearchOrder = 1,
                ResearchDifficulty = 24,
            };

            _technology = new Technology(_building);
        }

        [Test]
        public void IsAIControlled_WithPlayerID_ReturnsFalse()
        {
            bool isAI = _faction.IsAIControlled();

            Assert.IsFalse(isAI, "Faction with PlayerID should not be AI controlled");
        }

        [Test]
        public void IsAIControlled_WithoutPlayerID_ReturnsTrue()
        {
            _faction.PlayerID = null;

            bool isAI = _faction.IsAIControlled();

            Assert.IsTrue(isAI, "Faction without PlayerID should be AI controlled");
        }

        [Test]
        public void AddOwnedUnit_ValidPlanet_AddsPlanetToOwnedNodes()
        {
            _faction.AddOwnedUnit(_planet1);

            List<Planet> planets = _faction.GetOwnedUnitsByType<Planet>();

            Assert.Contains(_planet1, planets, "Faction should contain the added planet");
        }

        [Test]
        public void RemoveOwnedUnit_OwnedPlanet_RemovesFromOwnedNodes()
        {
            _faction.AddOwnedUnit(_planet1);

            _faction.RemoveOwnedUnit(_planet1);

            List<Planet> planets = _faction.GetOwnedUnitsByType<Planet>();

            Assert.IsFalse(planets.Contains(_planet1), "Faction should not contain removed planet");
        }

        [Test]
        public void GetOwnedUnitsByType_FactionWithMixedUnits_ReturnsUnitsOfType()
        {
            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);
            _faction.AddOwnedUnit(_fleet);

            List<Planet> planets = _faction.GetOwnedUnitsByType<Planet>();
            List<Fleet> fleets = _faction.GetOwnedUnitsByType<Fleet>();

            Assert.AreEqual(2, planets.Count, "Should return correct number of planets");
            Assert.Contains(_planet1, planets, "Should contain planet1");
            Assert.Contains(_planet2, planets, "Should contain planet2");
            Assert.AreEqual(1, fleets.Count, "Should return correct number of fleets");
            Assert.Contains(_fleet, fleets, "Should contain fleet");
        }

        [Test]
        public void GetAllOwnedNodes_FactionWithMultipleUnits_ReturnsAllUnits()
        {
            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_fleet);
            _faction.AddOwnedUnit(_officer);

            List<ISceneNode> allNodes = _faction.GetAllOwnedNodes();

            Assert.AreEqual(3, allNodes.Count, "Should return all owned nodes");
            Assert.Contains(_planet1, allNodes, "Should contain planet");
            Assert.Contains(_fleet, allNodes, "Should contain fleet");
            Assert.Contains(_officer, allNodes, "Should contain officer");
        }

        [Test]
        public void GetUnlockedTechnologies_FactionBelowOrder_ReturnsOnlyUnlockedTechnologies()
        {
            _faction.SetHighestUnlockedOrder(ManufacturingType.Building, 2);

            Building advancedBuilding = new Building
            {
                DisplayName = "Advanced Mine",
                ResearchOrder = 2,
                ResearchDifficulty = 40,
            };

            Building futureBuilding = new Building
            {
                DisplayName = "Future Building",
                ResearchOrder = 3,
                ResearchDifficulty = 60,
            };

            IManufacturable[] templates = new IManufacturable[]
            {
                _building,
                advancedBuilding,
                futureBuilding,
            };
            _faction.RebuildResearchQueues(templates);

            List<Technology> unlocked = _faction.GetUnlockedTechnologies(
                ManufacturingType.Building
            );

            Assert.AreEqual(
                2,
                unlocked.Count,
                "Should only return technologies at or below unlocked order"
            );
            Assert.IsFalse(
                unlocked.Exists(t => t.GetReference().GetDisplayName() == "Future Building"),
                "Should not contain order 3 technology"
            );
        }

        [Test]
        public void GetCurrentResearchTarget_WithUnresearched_ReturnsNextUnlocked()
        {
            _faction.SetHighestUnlockedOrder(ManufacturingType.Building, 0);

            IManufacturable[] templates = new IManufacturable[] { _building };
            _faction.RebuildResearchQueues(templates);

            Technology target = _faction.GetCurrentResearchTarget(ManufacturingType.Building);

            Assert.IsNotNull(target, "Should return the next technology to research");
            Assert.AreEqual(1, target.GetResearchOrder());
        }

        [Test]
        public void GetCurrentResearchTarget_AllUnlocked_ReturnsNull()
        {
            _faction.SetHighestUnlockedOrder(ManufacturingType.Building, 99);

            IManufacturable[] templates = new IManufacturable[] { _building };
            _faction.RebuildResearchQueues(templates);

            Technology target = _faction.GetCurrentResearchTarget(ManufacturingType.Building);

            Assert.IsNull(target, "Should return null when all technologies are unlocked");
        }

        [Test]
        public void GetHighestUnlockedOrder_WithSetOrder_ReturnsCorrectOrder()
        {
            _faction.SetHighestUnlockedOrder(ManufacturingType.Ship, 5);

            int order = _faction.GetHighestUnlockedOrder(ManufacturingType.Ship);

            Assert.AreEqual(5, order, "Should return the correct unlocked order");
        }

        [Test]
        public void SetHighestUnlockedOrder_ValidOrder_SetsOrder()
        {
            _faction.SetHighestUnlockedOrder(ManufacturingType.Troop, 3);

            Assert.AreEqual(
                3,
                _faction.GetHighestUnlockedOrder(ManufacturingType.Troop),
                "Should set the unlocked order correctly"
            );
        }

        [Test]
        public void RebuildResearchQueues_WithRestrictedBuilding_FiltersOwnership()
        {
            Building restrictedBuilding = new Building
            {
                DisplayName = "Restricted Building",
                ResearchOrder = 1,
                ResearchDifficulty = 24,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION2" },
            };

            _building.AllowedOwnerInstanceIDs = new List<string> { "FACTION1" };

            IManufacturable[] templates = new IManufacturable[] { _building, restrictedBuilding };
            _faction.RebuildResearchQueues(templates);

            List<Technology> queue = _faction.ResearchQueue[ManufacturingType.Building];
            Assert.AreEqual(1, queue.Count, "Should only include technologies for this faction");
        }

        [Test]
        public void RebuildResearchQueues_WithMultipleBuildings_SortsByResearchOrder()
        {
            Building b1 = new Building
            {
                DisplayName = "B1",
                ResearchOrder = 3,
                ResearchDifficulty = 60,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };
            Building b2 = new Building
            {
                DisplayName = "B2",
                ResearchOrder = 1,
                ResearchDifficulty = 24,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };
            Building b3 = new Building
            {
                DisplayName = "B3",
                ResearchOrder = 0,
                ResearchDifficulty = 0,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };

            _faction.RebuildResearchQueues(new IManufacturable[] { b1, b2, b3 });

            List<Technology> queue = _faction.ResearchQueue[ManufacturingType.Building];
            Assert.AreEqual(0, queue[0].GetResearchOrder());
            Assert.AreEqual(1, queue[1].GetResearchOrder());
            Assert.AreEqual(3, queue[2].GetResearchOrder());
        }

        [Test]
        public void AddMessage_ValidMessage_AddsToCorrectList()
        {
            Message message = new Message(MessageType.Conflict, "Battle occurred");

            _faction.AddMessage(message);

            Assert.Contains(
                message,
                _faction.Messages[MessageType.Conflict],
                "Should add message to correct type list"
            );
        }

        [Test]
        public void RemoveMessage_ExistingMessage_RemovesFromList()
        {
            Message message = new Message(MessageType.Mission, "Mission completed");
            _faction.AddMessage(message);

            _faction.RemoveMessage(message);

            Assert.IsFalse(
                _faction.Messages[MessageType.Mission].Contains(message),
                "Should remove message from list"
            );
        }

        [Test]
        public void GetAvailableOfficers_MixedOfficerStates_ReturnsOnlyMovableOfficers()
        {
            Officer availableOfficer = new Officer
            {
                OwnerInstanceID = "FACTION1",
                Movement = null,
            };

            Officer unavailableOfficer = new Officer
            {
                OwnerInstanceID = "FACTION1",
                Movement = new MovementState(),
            };

            _faction.AddOwnedUnit(availableOfficer);
            _faction.AddOwnedUnit(unavailableOfficer);

            List<Officer> available = _faction.GetAvailableOfficers();

            Assert.AreEqual(1, available.Count, "Should return only movable officers");
            Assert.Contains(availableOfficer, available, "Should contain available officer");
            Assert.IsFalse(
                available.Contains(unavailableOfficer),
                "Should not contain unavailable officer"
            );
        }

        [Test]
        public void GetTotalRawResourceNodes_FactionWithMultiplePlanets_ReturnsSumAcrossPlanets()
        {
            _planet1.NumRawResourceNodes = 10;
            _planet2.NumRawResourceNodes = 15;

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalRawResourceNodes();

            Assert.AreEqual(25, total, "Should sum raw resource nodes across all planets");
        }

        [Test]
        public void GetTotalAvailableResourceNodes_FactionWithBlockadedPlanet_ReturnsSumAcrossPlanets()
        {
            _planet1.NumRawResourceNodes = 10;
            // planet1 is not blockaded by default (no enemy fleets)

            _planet2.NumRawResourceNodes = 15;
            // Add an enemy fleet to planet2 to blockade it
            Fleet enemyFleet = new Fleet
            {
                InstanceID = "ENEMYFLEET1",
                OwnerInstanceID = "FACTION2",
            };
            _planet2.Fleets.Add(enemyFleet);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalAvailableResourceNodes();

            Assert.AreEqual(10, total, "Should only count non-blockaded planets");
        }

        [Test]
        public void SerializeAndDeserialize_MaintainsState()
        {
            _faction.SetHighestUnlockedOrder(ManufacturingType.Ship, 3);
            _faction.AddOwnedUnit(_planet1);
            _faction.AddMessage(new Message(MessageType.Resource, "Test message"));

            string serialized = SerializationHelper.Serialize(_faction);
            Console.WriteLine("=== SERIALIZED XML ===");
            Console.WriteLine(serialized);
            Console.WriteLine("=== END ===");
            Faction deserialized = SerializationHelper.Deserialize<Faction>(serialized);

            Assert.AreEqual(
                _faction.InstanceID,
                deserialized.InstanceID,
                "InstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _faction.DisplayName,
                deserialized.DisplayName,
                "DisplayName should be correctly deserialized."
            );
            Assert.AreEqual(
                _faction.PlayerID,
                deserialized.PlayerID,
                "PlayerID should be correctly deserialized."
            );
            Assert.AreEqual(
                _faction.GetHighestUnlockedOrder(ManufacturingType.Ship),
                deserialized.GetHighestUnlockedOrder(ManufacturingType.Ship),
                "Research orders should be correctly deserialized."
            );
        }

        [Test]
        public void GetHQInstanceID_FactionWithHQ_ReturnsHQInstanceID()
        {
            _faction.HQInstanceID = "HQ1";

            string hqID = _faction.GetHQInstanceID();

            Assert.AreEqual("HQ1", hqID, "Should return the HQ instance ID");
        }

        [Test]
        public void GetHQInstanceID_WithNullHQ_ReturnsNull()
        {
            _faction.HQInstanceID = null;

            string hqID = _faction.GetHQInstanceID();

            Assert.IsNull(hqID, "Should return null when HQ is not set");
        }

        [Test]
        public void GetTotalRawMinedResources_FactionWithMultiplePlanets_ReturnsSumAcrossPlanets()
        {
            _planet1.NumRawResourceNodes = 25;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 20; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }

            _planet2.NumRawResourceNodes = 35;
            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 30; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(mine);
            }

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalRawMinedResources();

            Assert.AreEqual(50, total, "Should sum raw mined resources across all planets");
        }

        [Test]
        public void GetTotalAvailableMinedResources_FactionWithBlockadedPlanet_ReturnsSumAcrossPlanets()
        {
            _planet1.NumRawResourceNodes = 25;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 20; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }

            _planet2.NumRawResourceNodes = 35;
            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 30; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(mine);
            }
            Fleet enemyFleet = new Fleet
            {
                InstanceID = "ENEMYFLEET1",
                OwnerInstanceID = "FACTION2",
            };
            _planet2.Fleets.Add(enemyFleet);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalAvailableMinedResources();

            Assert.AreEqual(20, total, "Should only count non-blockaded planets");
        }

        [Test]
        public void GetTotalRawRefinementCapacity_FactionWithMultiplePlanets_ReturnsSumAcrossPlanets()
        {
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 10; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(refinery);
            }

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalRawRefinementCapacity();

            Assert.AreEqual(15, total, "Should sum raw refinement capacity across all planets");
        }

        [Test]
        public void GetTotalAvailableRefinementCapacity_FactionWithBlockadedPlanet_ReturnsSumAcrossPlanets()
        {
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 10; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(refinery);
            }
            Fleet enemyFleet = new Fleet
            {
                InstanceID = "ENEMYFLEET1",
                OwnerInstanceID = "FACTION2",
            };
            _planet2.Fleets.Add(enemyFleet);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalAvailableRefinementCapacity();

            Assert.AreEqual(5, total, "Should only count non-blockaded planets");
        }

        [Test]
        public void GetTotalRawMaterials_FactionWithMultiplePlanets_CalculatesTotal()
        {
            _planet1.NumRawResourceNodes = 10;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 8; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _faction.AddOwnedUnit(_planet1);

            int total = _faction.GetTotalRawMaterialsRaw();

            // Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
            Assert.AreEqual(5, total, "Should calculate raw materials correctly");
        }

        [Test]
        public void GetTotalAvailableMaterials_FactionWithMultiplePlanets_CalculatesAvailableTotal()
        {
            _planet1.NumRawResourceNodes = 10;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 8; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _faction.AddOwnedUnit(_planet1);

            int total = _faction.GetTotalAvailableMaterialsRaw();

            // Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
            Assert.AreEqual(5, total, "Should calculate available materials correctly");
        }

        [Test]
        public void GetTotalAvailableMaterials_FactionWithBlockadedPlanet_ExcludesBlockadedPlanets()
        {
            _planet1.NumRawResourceNodes = 10;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 8; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _planet2.NumRawResourceNodes = 15;
            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 12; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(mine);
            }
            for (int i = 0; i < 8; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(refinery);
            }
            Fleet enemyFleet = new Fleet
            {
                InstanceID = "ENEMYFLEET1",
                OwnerInstanceID = "FACTION2",
            };
            _planet2.Fleets.Add(enemyFleet);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalAvailableMaterialsRaw();

            // Only planet1 should count: Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
            Assert.AreEqual(5, total, "Should exclude blockaded planets from calculation");
        }

        [Test]
        public void GetNearestFriendlyPlanetTo_MultipleFriendlyPlanets_ReturnsClosestPlanet()
        {
            Planet planet3 = new Planet { InstanceID = "PLANET3", OwnerInstanceID = "FACTION1" };

            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 10;

            Building testBuilding = new Building
            {
                InstanceID = "TESTBUILDING",
                OwnerInstanceID = "FACTION1",
            };
            _planet1.AddChild(testBuilding);
            testBuilding.SetParent(_planet1);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);
            _faction.AddOwnedUnit(planet3);

            Planet nearest = _faction.GetNearestFriendlyPlanetTo(testBuilding);

            Assert.AreEqual("PLANET1", nearest.InstanceID, "Should return the nearest planet");
        }

        [Test]
        public void GetNearestFriendlyPlanetTo_WithNodeNotOnPlanet_ThrowsException()
        {
            Fleet floatingFleet = new Fleet { InstanceID = "FLEET2", OwnerInstanceID = "FACTION1" };

            _faction.AddOwnedUnit(_planet1);

            Assert.Throws<ArgumentException>(
                () => _faction.GetNearestFriendlyPlanetTo(floatingFleet),
                "Should throw exception when node is not on a planet"
            );
        }

        [Test]
        public void GetIdleFacilities_GameWithMixedFacilities_ReturnsOnlyIdlePlanets()
        {
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 5;
            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 5;

            // Add a shipyard to planet1 (idle facility)
            Building shipyard = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _planet1.AddChild(shipyard);

            // planet2 has no buildings, so no idle facilities

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            List<Planet> idleFacilities = _faction.GetIdleFacilities(ManufacturingType.Ship);

            Assert.AreEqual(
                1,
                idleFacilities.Count,
                "Should return only planets with idle facilities"
            );
            Assert.Contains(_planet1, idleFacilities, "Should contain planet1");
            Assert.IsFalse(
                idleFacilities.Contains(_planet2),
                "Should not contain planet2 with no idle facilities"
            );
        }

        [Test]
        public void GetIdleFacilities_WithNoIdleFacilities_ReturnsEmptyList()
        {
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 5;
            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 5;

            // Add buildings with different production types (Ship, Troop) but not Building type
            Building shipyard = new Building
            {
                ProductionType = ManufacturingType.Ship,
                OwnerInstanceID = "FACTION1",
            };
            _planet1.AddChild(shipyard);

            Building trainingFacility = new Building
            {
                ProductionType = ManufacturingType.Troop,
                OwnerInstanceID = "FACTION1",
            };
            _planet2.AddChild(trainingFacility);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            List<Planet> idleFacilities = _faction.GetIdleFacilities(ManufacturingType.Building);

            Assert.AreEqual(
                0,
                idleFacilities.Count,
                "Should return empty list when no facilities are idle"
            );
        }
    }
}
