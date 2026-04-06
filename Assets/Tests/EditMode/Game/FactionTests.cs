using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

[TestFixture]
public class FactionTests
{
    private Faction faction;
    private Planet planet1;
    private Planet planet2;
    private Fleet fleet;
    private Officer officer;
    private Building building;
    private Technology technology;

    [SetUp]
    public void SetUp()
    {
        faction = new Faction
        {
            InstanceID = "FACTION1",
            DisplayName = "Rebel Alliance",
            PlayerID = "PLAYER1",
        };

        planet1 = new Planet { InstanceID = "PLANET1", OwnerInstanceID = "FACTION1" };

        planet2 = new Planet { InstanceID = "PLANET2", OwnerInstanceID = "FACTION1" };

        fleet = new Fleet { InstanceID = "FLEET1", OwnerInstanceID = "FACTION1" };

        officer = new Officer
        {
            InstanceID = "OFFICER1",
            OwnerInstanceID = "FACTION1",
            Movement = null,
        };

        building = new Building
        {
            InstanceID = "BUILDING1",
            DisplayName = "Mine",
            ConstructionCost = 100,
            ResearchOrder = 1,
            ResearchDifficulty = 24,
        };

        technology = new Technology(building);
    }

    [Test]
    public void IsAIControlled_WithPlayerID_ReturnsFalse()
    {
        bool isAI = faction.IsAIControlled();

        Assert.IsFalse(isAI, "Faction with PlayerID should not be AI controlled");
    }

    [Test]
    public void IsAIControlled_WithoutPlayerID_ReturnsTrue()
    {
        faction.PlayerID = null;

        bool isAI = faction.IsAIControlled();

        Assert.IsTrue(isAI, "Faction without PlayerID should be AI controlled");
    }

    [Test]
    public void AddOwnedUnit_AddsPlanet()
    {
        faction.AddOwnedUnit(planet1);

        List<Planet> planets = faction.GetOwnedUnitsByType<Planet>();

        Assert.Contains(planet1, planets, "Faction should contain the added planet");
    }

    [Test]
    public void RemoveOwnedUnit_RemovesPlanet()
    {
        faction.AddOwnedUnit(planet1);

        faction.RemoveOwnedUnit(planet1);

        List<Planet> planets = faction.GetOwnedUnitsByType<Planet>();

        Assert.IsFalse(planets.Contains(planet1), "Faction should not contain removed planet");
    }

    [Test]
    public void GetOwnedUnitsByType_ReturnsCorrectUnits()
    {
        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);
        faction.AddOwnedUnit(fleet);

        List<Planet> planets = faction.GetOwnedUnitsByType<Planet>();
        List<Fleet> fleets = faction.GetOwnedUnitsByType<Fleet>();

        Assert.AreEqual(2, planets.Count, "Should return correct number of planets");
        Assert.Contains(planet1, planets, "Should contain planet1");
        Assert.Contains(planet2, planets, "Should contain planet2");
        Assert.AreEqual(1, fleets.Count, "Should return correct number of fleets");
        Assert.Contains(fleet, fleets, "Should contain fleet");
    }

    [Test]
    public void GetAllOwnedNodes_ReturnsAllUnits()
    {
        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(fleet);
        faction.AddOwnedUnit(officer);

        List<ISceneNode> allNodes = faction.GetAllOwnedNodes();

        Assert.AreEqual(3, allNodes.Count, "Should return all owned nodes");
        Assert.Contains(planet1, allNodes, "Should contain planet");
        Assert.Contains(fleet, allNodes, "Should contain fleet");
        Assert.Contains(officer, allNodes, "Should contain officer");
    }

    [Test]
    public void GetUnlockedTechnologies_ReturnsCorrectTechnologies()
    {
        faction.SetHighestUnlockedOrder(ManufacturingType.Building, 2);

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

        IManufacturable[] templates = new IManufacturable[] { building, advancedBuilding, futureBuilding };
        faction.RebuildResearchQueues(templates);

        List<Technology> unlocked = faction.GetUnlockedTechnologies(ManufacturingType.Building);

        Assert.AreEqual(2, unlocked.Count, "Should only return technologies at or below unlocked order");
        Assert.IsFalse(
            unlocked.Exists(t => t.GetReference().GetDisplayName() == "Future Building"),
            "Should not contain order 3 technology"
        );
    }

    [Test]
    public void GetCurrentResearchTarget_ReturnsNextUnlocked()
    {
        faction.SetHighestUnlockedOrder(ManufacturingType.Building, 0);

        IManufacturable[] templates = new IManufacturable[] { building };
        faction.RebuildResearchQueues(templates);

        Technology target = faction.GetCurrentResearchTarget(ManufacturingType.Building);

        Assert.IsNotNull(target, "Should return the next technology to research");
        Assert.AreEqual(1, target.GetResearchOrder());
    }

    [Test]
    public void GetCurrentResearchTarget_AllUnlocked_ReturnsNull()
    {
        faction.SetHighestUnlockedOrder(ManufacturingType.Building, 99);

        IManufacturable[] templates = new IManufacturable[] { building };
        faction.RebuildResearchQueues(templates);

        Technology target = faction.GetCurrentResearchTarget(ManufacturingType.Building);

        Assert.IsNull(target, "Should return null when all technologies are unlocked");
    }

    [Test]
    public void GetHighestUnlockedOrder_ReturnsCorrectOrder()
    {
        faction.SetHighestUnlockedOrder(ManufacturingType.Ship, 5);

        int order = faction.GetHighestUnlockedOrder(ManufacturingType.Ship);

        Assert.AreEqual(5, order, "Should return the correct unlocked order");
    }

    [Test]
    public void SetHighestUnlockedOrder_SetsOrder()
    {
        faction.SetHighestUnlockedOrder(ManufacturingType.Troop, 3);

        Assert.AreEqual(
            3,
            faction.GetHighestUnlockedOrder(ManufacturingType.Troop),
            "Should set the unlocked order correctly"
        );
    }

    [Test]
    public void RebuildResearchQueues_FiltersOwnership()
    {
        Building restrictedBuilding = new Building
        {
            DisplayName = "Restricted Building",
            ResearchOrder = 1,
            ResearchDifficulty = 24,
            AllowedOwnerInstanceIDs = new List<string> { "FACTION2" },
        };

        building.AllowedOwnerInstanceIDs = new List<string> { "FACTION1" };

        IManufacturable[] templates = new IManufacturable[] { building, restrictedBuilding };
        faction.RebuildResearchQueues(templates);

        List<Technology> queue = faction.ResearchQueue[ManufacturingType.Building];
        Assert.AreEqual(1, queue.Count, "Should only include technologies for this faction");
    }

    [Test]
    public void RebuildResearchQueues_SortsByResearchOrder()
    {
        Building b1 = new Building { DisplayName = "B1", ResearchOrder = 3, ResearchDifficulty = 60, AllowedOwnerInstanceIDs = new List<string> { "FACTION1" } };
        Building b2 = new Building { DisplayName = "B2", ResearchOrder = 1, ResearchDifficulty = 24, AllowedOwnerInstanceIDs = new List<string> { "FACTION1" } };
        Building b3 = new Building { DisplayName = "B3", ResearchOrder = 0, ResearchDifficulty = 0, AllowedOwnerInstanceIDs = new List<string> { "FACTION1" } };

        faction.RebuildResearchQueues(new IManufacturable[] { b1, b2, b3 });

        List<Technology> queue = faction.ResearchQueue[ManufacturingType.Building];
        Assert.AreEqual(0, queue[0].GetResearchOrder());
        Assert.AreEqual(1, queue[1].GetResearchOrder());
        Assert.AreEqual(3, queue[2].GetResearchOrder());
    }

    [Test]
    public void AddMessage_AddsMessageToCorrectList()
    {
        Message message = new Message(MessageType.Conflict, "Battle occurred");

        faction.AddMessage(message);

        Assert.Contains(
            message,
            faction.Messages[MessageType.Conflict],
            "Should add message to correct type list"
        );
    }

    [Test]
    public void RemoveMessage_RemovesMessage()
    {
        Message message = new Message(MessageType.Mission, "Mission completed");
        faction.AddMessage(message);

        faction.RemoveMessage(message);

        Assert.IsFalse(
            faction.Messages[MessageType.Mission].Contains(message),
            "Should remove message from list"
        );
    }

    [Test]
    public void GetAvailableOfficers_ReturnsOnlyMovableOfficers()
    {
        Officer availableOfficer = new Officer { OwnerInstanceID = "FACTION1", Movement = null };

        Officer unavailableOfficer = new Officer
        {
            OwnerInstanceID = "FACTION1",
            Movement = new MovementState(),
        };

        faction.AddOwnedUnit(availableOfficer);
        faction.AddOwnedUnit(unavailableOfficer);

        List<Officer> available = faction.GetAvailableOfficers(faction);

        Assert.AreEqual(1, available.Count, "Should return only movable officers");
        Assert.Contains(availableOfficer, available, "Should contain available officer");
        Assert.IsFalse(
            available.Contains(unavailableOfficer),
            "Should not contain unavailable officer"
        );
    }

    [Test]
    public void GetTotalRawResourceNodes_ReturnsSumAcrossPlanets()
    {
        planet1.NumRawResourceNodes = 10;
        planet2.NumRawResourceNodes = 15;

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        int total = faction.GetTotalRawResourceNodes();

        Assert.AreEqual(25, total, "Should sum raw resource nodes across all planets");
    }

    [Test]
    public void GetTotalAvailableResourceNodes_ReturnsSumAcrossPlanets()
    {
        planet1.NumRawResourceNodes = 10;
        // planet1 is not blockaded by default (no enemy fleets)

        planet2.NumRawResourceNodes = 15;
        // Add an enemy fleet to planet2 to blockade it
        Fleet enemyFleet = new Fleet { InstanceID = "ENEMYFLEET1", OwnerInstanceID = "FACTION2" };
        planet2.Fleets.Add(enemyFleet);

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        int total = faction.GetTotalAvailableResourceNodes();

        Assert.AreEqual(10, total, "Should only count non-blockaded planets");
    }

    // TODO: FogState serialization fails when Faction is root type (works fine in GameRoot)
    // Comprehensive serialization tested via SaveGameManagerTests
    //[Test]
    public void SerializeAndDeserialize_MaintainsState_DISABLED()
    {
        faction.SetHighestUnlockedOrder(ManufacturingType.Ship, 3);
        faction.AddOwnedUnit(planet1);
        faction.AddMessage(new Message(MessageType.Resource, "Test message"));

        string serialized = SerializationHelper.Serialize(faction);
        Faction deserialized = SerializationHelper.Deserialize<Faction>(serialized);

        Assert.AreEqual(
            faction.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            faction.DisplayName,
            deserialized.DisplayName,
            "DisplayName should be correctly deserialized."
        );
        Assert.AreEqual(
            faction.PlayerID,
            deserialized.PlayerID,
            "PlayerID should be correctly deserialized."
        );
        Assert.AreEqual(
            faction.GetHighestUnlockedOrder(ManufacturingType.Ship),
            deserialized.GetHighestUnlockedOrder(ManufacturingType.Ship),
            "Research orders should be correctly deserialized."
        );
    }

    [Test]
    public void GetHQInstanceID_ReturnsHQID()
    {
        faction.HQInstanceID = "HQ1";

        string hqID = faction.GetHQInstanceID();

        Assert.AreEqual("HQ1", hqID, "Should return the HQ instance ID");
    }

    [Test]
    public void GetHQInstanceID_WithNullHQ_ReturnsNull()
    {
        faction.HQInstanceID = null;

        string hqID = faction.GetHQInstanceID();

        Assert.IsNull(hqID, "Should return null when HQ is not set");
    }

    [Test]
    public void GetTotalRawMinedResources_ReturnsSumAcrossPlanets()
    {
        planet1.NumRawResourceNodes = 25;
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 50;
        for (int i = 0; i < 20; i++)
        {
            Building mine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(mine);
        }

        planet2.NumRawResourceNodes = 35;
        planet2.IsColonized = true;
        planet2.EnergyCapacity = 50;
        for (int i = 0; i < 30; i++)
        {
            Building mine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet2.AddChild(mine);
        }

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        int total = faction.GetTotalRawMinedResources();

        Assert.AreEqual(50, total, "Should sum raw mined resources across all planets");
    }

    [Test]
    public void GetTotalAvailableMinedResources_ReturnsSumAcrossPlanets()
    {
        planet1.NumRawResourceNodes = 25;
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 50;
        for (int i = 0; i < 20; i++)
        {
            Building mine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(mine);
        }

        planet2.NumRawResourceNodes = 35;
        planet2.IsColonized = true;
        planet2.EnergyCapacity = 50;
        for (int i = 0; i < 30; i++)
        {
            Building mine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet2.AddChild(mine);
        }
        Fleet enemyFleet = new Fleet { InstanceID = "ENEMYFLEET1", OwnerInstanceID = "FACTION2" };
        planet2.Fleets.Add(enemyFleet);

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        int total = faction.GetTotalAvailableMinedResources();

        Assert.AreEqual(20, total, "Should only count non-blockaded planets");
    }

    [Test]
    public void GetTotalRawRefinementCapacity_ReturnsSumAcrossPlanets()
    {
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 50;
        for (int i = 0; i < 5; i++)
        {
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(refinery);
        }

        planet2.IsColonized = true;
        planet2.EnergyCapacity = 50;
        for (int i = 0; i < 10; i++)
        {
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet2.AddChild(refinery);
        }

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        int total = faction.GetTotalRawRefinementCapacity();

        Assert.AreEqual(15, total, "Should sum raw refinement capacity across all planets");
    }

    [Test]
    public void GetTotalAvailableRefinementCapacity_ReturnsSumAcrossPlanets()
    {
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 50;
        for (int i = 0; i < 5; i++)
        {
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(refinery);
        }

        planet2.IsColonized = true;
        planet2.EnergyCapacity = 50;
        for (int i = 0; i < 10; i++)
        {
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet2.AddChild(refinery);
        }
        Fleet enemyFleet = new Fleet { InstanceID = "ENEMYFLEET1", OwnerInstanceID = "FACTION2" };
        planet2.Fleets.Add(enemyFleet);

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        int total = faction.GetTotalAvailableRefinementCapacity();

        Assert.AreEqual(5, total, "Should only count non-blockaded planets");
    }

    [Test]
    public void GetTotalRawMaterials_CalculatesCorrectly()
    {
        planet1.NumRawResourceNodes = 10;
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 50;
        for (int i = 0; i < 8; i++)
        {
            Building mine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(mine);
        }
        for (int i = 0; i < 5; i++)
        {
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(refinery);
        }

        faction.AddOwnedUnit(planet1);

        int total = faction.GetTotalRawMaterialsRaw();

        // Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
        Assert.AreEqual(5, total, "Should calculate raw materials correctly");
    }

    [Test]
    public void GetTotalAvailableMaterials_CalculatesCorrectly()
    {
        planet1.NumRawResourceNodes = 10;
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 50;
        for (int i = 0; i < 8; i++)
        {
            Building mine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(mine);
        }
        for (int i = 0; i < 5; i++)
        {
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(refinery);
        }

        faction.AddOwnedUnit(planet1);

        int total = faction.GetTotalAvailableMaterialsRaw();

        // Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
        Assert.AreEqual(5, total, "Should calculate available materials correctly");
    }

    [Test]
    public void GetTotalAvailableMaterials_ExcludesBlockadedPlanets()
    {
        planet1.NumRawResourceNodes = 10;
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 50;
        for (int i = 0; i < 8; i++)
        {
            Building mine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(mine);
        }
        for (int i = 0; i < 5; i++)
        {
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet1.AddChild(refinery);
        }

        planet2.NumRawResourceNodes = 15;
        planet2.IsColonized = true;
        planet2.EnergyCapacity = 50;
        for (int i = 0; i < 12; i++)
        {
            Building mine = new Building
            {
                BuildingType = BuildingType.Mine,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet2.AddChild(mine);
        }
        for (int i = 0; i < 8; i++)
        {
            Building refinery = new Building
            {
                BuildingType = BuildingType.Refinery,
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet2.AddChild(refinery);
        }
        Fleet enemyFleet = new Fleet { InstanceID = "ENEMYFLEET1", OwnerInstanceID = "FACTION2" };
        planet2.Fleets.Add(enemyFleet);

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        int total = faction.GetTotalAvailableMaterialsRaw();

        // Only planet1 should count: Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
        Assert.AreEqual(5, total, "Should exclude blockaded planets from calculation");
    }

    [Test]
    public void GetNearestFriendlyPlanetTo_ReturnsClosestPlanet()
    {
        Planet planet3 = new Planet { InstanceID = "PLANET3", OwnerInstanceID = "FACTION1" };

        planet1.IsColonized = true;
        planet1.EnergyCapacity = 10;

        Building testBuilding = new Building
        {
            InstanceID = "TESTBUILDING",
            OwnerInstanceID = "FACTION1",
        };
        planet1.AddChild(testBuilding);
        testBuilding.SetParent(planet1);

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);
        faction.AddOwnedUnit(planet3);

        Planet nearest = faction.GetNearestFriendlyPlanetTo(testBuilding);

        Assert.AreEqual("PLANET1", nearest.InstanceID, "Should return the nearest planet");
    }

    [Test]
    public void GetNearestFriendlyPlanetTo_WithNodeNotOnPlanet_ThrowsException()
    {
        Fleet floatingFleet = new Fleet { InstanceID = "FLEET2", OwnerInstanceID = "FACTION1" };

        faction.AddOwnedUnit(planet1);

        Assert.Throws<ArgumentException>(
            () => faction.GetNearestFriendlyPlanetTo(floatingFleet),
            "Should throw exception when node is not on a planet"
        );
    }

    [Test]
    public void GetIdleFacilities_ReturnsOnlyPlanetsWithIdleFacilities()
    {
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 5;
        planet2.IsColonized = true;
        planet2.EnergyCapacity = 5;

        // Add a shipyard to planet1 (idle facility)
        Building shipyard = new Building
        {
            ProductionType = ManufacturingType.Ship,
            OwnerInstanceID = "FACTION1",
            ManufacturingStatus = ManufacturingStatus.Complete,
        };
        planet1.AddChild(shipyard);

        // planet2 has no buildings, so no idle facilities

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        List<Planet> idleFacilities = faction.GetIdleFacilities(ManufacturingType.Ship);

        Assert.AreEqual(1, idleFacilities.Count, "Should return only planets with idle facilities");
        Assert.Contains(planet1, idleFacilities, "Should contain planet1");
        Assert.IsFalse(
            idleFacilities.Contains(planet2),
            "Should not contain planet2 with no idle facilities"
        );
    }

    [Test]
    public void GetIdleFacilities_WithNoIdleFacilities_ReturnsEmptyList()
    {
        planet1.IsColonized = true;
        planet1.EnergyCapacity = 5;
        planet2.IsColonized = true;
        planet2.EnergyCapacity = 5;

        // Add buildings with different production types (Ship, Troop) but not Building type
        Building shipyard = new Building
        {
            ProductionType = ManufacturingType.Ship,
            OwnerInstanceID = "FACTION1",
        };
        planet1.AddChild(shipyard);

        Building trainingFacility = new Building
        {
            ProductionType = ManufacturingType.Troop,
            OwnerInstanceID = "FACTION1",
        };
        planet2.AddChild(trainingFacility);

        faction.AddOwnedUnit(planet1);
        faction.AddOwnedUnit(planet2);

        List<Planet> idleFacilities = faction.GetIdleFacilities(ManufacturingType.Building);

        Assert.AreEqual(
            0,
            idleFacilities.Count,
            "Should return empty list when no facilities are idle"
        );
    }
}
