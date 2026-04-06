using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

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
            RequiredResearchLevel = 1,
        };

        _technology = new Technology(building);
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
    public void AddOwnedUnit_AddsPlanet()
    {
        _faction.AddOwnedUnit(_planet1);

        List<Planet> planets = _faction.GetOwnedUnitsByType<Planet>();

        Assert.Contains(_planet1, planets, "Faction should contain the added planet");
    }

    [Test]
    public void RemoveOwnedUnit_RemovesPlanet()
    {
        _faction.AddOwnedUnit(_planet1);

        _faction.RemoveOwnedUnit(_planet1);

        List<Planet> planets = _faction.GetOwnedUnitsByType<Planet>();

        Assert.IsFalse(planets.Contains(_planet1), "Faction should not contain removed planet");
    }

    [Test]
    public void GetOwnedUnitsByType_ReturnsCorrectUnits()
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
    public void GetAllOwnedNodes_ReturnsAllUnits()
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
    public void AddTechnologyNode_AddsTechnology()
    {
        _faction.SetResearchLevel(ManufacturingType.Building, 1);
        _faction.AddTechnologyNode(1, _technology);

        List<Technology> technologies = _faction.GetResearchedTechnologies(
            ManufacturingType.Building
        );

        Assert.Contains(_technology, technologies, "Should contain the added technology");
    }

    [Test]
    public void AddTechnologyNode_WithInvalidOwner_ThrowsException()
    {
        Building restrictedBuilding = new Building
        {
            DisplayName = "Restricted Building",
            RequiredResearchLevel = 1,
            AllowedOwnerInstanceIDs = new List<string> { "FACTION2" },
        };
        Technology restrictedTech = new Technology(restrictedBuilding);

        Assert.Throws<InvalidOperationException>(
            () => _faction.AddTechnologyNode(1, restrictedTech),
            "Should throw exception when owner IDs do not match"
        );
    }

    [Test]
    public void GetResearchedTechnologies_ReturnsCorrectTechnologies()
    {
        _faction.SetResearchLevel(ManufacturingType.Building, 2);
        _faction.AddTechnologyNode(1, _technology);

        Building advancedBuilding = new Building
        {
            DisplayName = "Advanced Mine",
            RequiredResearchLevel = 2,
        };
        Technology advancedTech = new Technology(advancedBuilding);
        _faction.AddTechnologyNode(2, advancedTech);

        Building futureBuilding = new Building
        {
            DisplayName = "Future Building",
            RequiredResearchLevel = 3,
        };
        Technology futureTech = new Technology(futureBuilding);
        _faction.AddTechnologyNode(3, futureTech);

        List<Technology> researched = _faction.GetResearchedTechnologies(ManufacturingType.Building);

        Assert.AreEqual(
            2,
            researched.Count,
            "Should only return technologies at or below research level"
        );
        Assert.Contains(_technology, researched, "Should contain level 1 technology");
        Assert.Contains(advancedTech, researched, "Should contain level 2 technology");
        Assert.IsFalse(researched.Contains(futureTech), "Should not contain level 3 technology");
    }

    [Test]
    public void GetResearchLevel_ReturnsCorrectLevel()
    {
        _faction.SetResearchLevel(ManufacturingType.Ship, 5);

        int level = _faction.GetResearchLevel(ManufacturingType.Ship);

        Assert.AreEqual(5, level, "Should return the correct research level");
    }

    [Test]
    public void SetResearchLevel_SetsLevel()
    {
        _faction.SetResearchLevel(ManufacturingType.Troop, 3);

        Assert.AreEqual(
            3,
            _faction.GetResearchLevel(ManufacturingType.Troop),
            "Should set the research level correctly"
        );
    }

    [Test]
    public void GetResearchLevels_ReturnsAllLevels()
    {
        _faction.SetResearchLevel(ManufacturingType.Building, 2);
        _faction.SetResearchLevel(ManufacturingType.Ship, 3);
        _faction.SetResearchLevel(ManufacturingType.Troop, 1);

        Dictionary<ManufacturingType, int> levels = _faction.GetResearchLevels();

        Assert.AreEqual(2, levels[ManufacturingType.Building], "Building level should be 2");
        Assert.AreEqual(3, levels[ManufacturingType.Ship], "Ship level should be 3");
        Assert.AreEqual(1, levels[ManufacturingType.Troop], "Troop level should be 1");
    }

    [Test]
    public void AddMessage_AddsMessageToCorrectList()
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
    public void RemoveMessage_RemovesMessage()
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
    public void GetAvailableOfficers_ReturnsOnlyMovableOfficers()
    {
        Officer availableOfficer = new Officer { OwnerInstanceID = "FACTION1", Movement = null };

        Officer unavailableOfficer = new Officer
        {
            OwnerInstanceID = "FACTION1",
            Movement = new MovementState(),
        };

        _faction.AddOwnedUnit(availableOfficer);
        _faction.AddOwnedUnit(unavailableOfficer);

        List<Officer> available = _faction.GetAvailableOfficers(faction);

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
        _planet1.NumRawResourceNodes = 10;
        _planet2.NumRawResourceNodes = 15;

        _faction.AddOwnedUnit(_planet1);
        _faction.AddOwnedUnit(_planet2);

        int total = _faction.GetTotalRawResourceNodes();

        Assert.AreEqual(25, total, "Should sum raw resource nodes across all planets");
    }

    [Test]
    public void GetTotalAvailableResourceNodes_ReturnsSumAcrossPlanets()
    {
        _planet1.NumRawResourceNodes = 10;
        // planet1 is not blockaded by default (no enemy fleets)

        _planet2.NumRawResourceNodes = 15;
        // Add an enemy fleet to planet2 to blockade it
        Fleet enemyFleet = new Fleet { InstanceID = "ENEMYFLEET1", OwnerInstanceID = "FACTION2" };
        _planet2.Fleets.Add(enemyFleet);

        _faction.AddOwnedUnit(_planet1);
        _faction.AddOwnedUnit(_planet2);

        int total = _faction.GetTotalAvailableResourceNodes();

        Assert.AreEqual(10, total, "Should only count non-blockaded planets");
    }

    // TODO: FogState serialization fails when Faction is root type (works fine in GameRoot)
    // Comprehensive serialization tested via SaveGameManagerTests
    //[Test]
    public void SerializeAndDeserialize_MaintainsState_DISABLED()
    {
        _faction.SetResearchLevel(ManufacturingType.Ship, 3);
        _faction.AddOwnedUnit(_planet1);
        _faction.AddMessage(new Message(MessageType.Resource, "Test message"));

        string serialized = SerializationHelper.Serialize(faction);
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
            _faction.GetResearchLevel(ManufacturingType.Ship),
            deserialized.GetResearchLevel(ManufacturingType.Ship),
            "Research levels should be correctly deserialized."
        );
    }

    [Test]
    public void GetHQInstanceID_ReturnsHQID()
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
    public void GetTotalRawMinedResources_ReturnsSumAcrossPlanets()
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
    public void GetTotalAvailableMinedResources_ReturnsSumAcrossPlanets()
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
        Fleet enemyFleet = new Fleet { InstanceID = "ENEMYFLEET1", OwnerInstanceID = "FACTION2" };
        _planet2.Fleets.Add(enemyFleet);

        _faction.AddOwnedUnit(_planet1);
        _faction.AddOwnedUnit(_planet2);

        int total = _faction.GetTotalAvailableMinedResources();

        Assert.AreEqual(20, total, "Should only count non-blockaded planets");
    }

    [Test]
    public void GetTotalRawRefinementCapacity_ReturnsSumAcrossPlanets()
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
    public void GetTotalAvailableRefinementCapacity_ReturnsSumAcrossPlanets()
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
        Fleet enemyFleet = new Fleet { InstanceID = "ENEMYFLEET1", OwnerInstanceID = "FACTION2" };
        _planet2.Fleets.Add(enemyFleet);

        _faction.AddOwnedUnit(_planet1);
        _faction.AddOwnedUnit(_planet2);

        int total = _faction.GetTotalAvailableRefinementCapacity();

        Assert.AreEqual(5, total, "Should only count non-blockaded planets");
    }

    [Test]
    public void GetTotalRawMaterials_CalculatesCorrectly()
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
    public void GetTotalAvailableMaterials_CalculatesCorrectly()
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
    public void GetTotalAvailableMaterials_ExcludesBlockadedPlanets()
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
        Fleet enemyFleet = new Fleet { InstanceID = "ENEMYFLEET1", OwnerInstanceID = "FACTION2" };
        _planet2.Fleets.Add(enemyFleet);

        _faction.AddOwnedUnit(_planet1);
        _faction.AddOwnedUnit(_planet2);

        int total = _faction.GetTotalAvailableMaterialsRaw();

        // Only planet1 should count: Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
        Assert.AreEqual(5, total, "Should exclude blockaded planets from calculation");
    }

    [Test]
    public void GetNearestFriendlyPlanetTo_ReturnsClosestPlanet()
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
    public void GetIdleFacilities_ReturnsOnlyPlanetsWithIdleFacilities()
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

        Assert.AreEqual(1, idleFacilities.Count, "Should return only planets with idle facilities");
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
