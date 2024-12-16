using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class PlanetTests
{
    private Planet planet;

    [SetUp]
    public void Setup()
    {
        planet = new Planet
        {
            IsColonized = true,
            GroundSlots = 5,
            OrbitSlots = 3,
            OwnerInstanceID = "FNALL1",
        };
    }

    [Test]
    public void AddFleet_ValidFleet_AddsToPlanet()
    {
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
        planet.AddChild(fleet);

        Assert.Contains(fleet, planet.Fleets, "Fleet should be added to the planet.");
    }

    [Test]
    public void AddBuilding_InvalidOwner_ThrowsException()
    {
        Building building = new Building { OwnerInstanceID = "INVALID" };

        Assert.Throws<SceneAccessException>(
            () => planet.AddChild(building),
            "Adding a fleet with a mismatched OwnerInstanceID should throw a SceneAccessException."
        );
    }

    [Test]
    public void AddOfficer_ValidOfficer_AddsToPlanet()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        planet.AddChild(officer);

        Assert.Contains(officer, planet.Officers, "Officer should be added to the planet.");
    }

    [Test]
    public void AddOfficer_InvalidOwner_ThrowsException()
    {
        Officer officer = new Officer { OwnerInstanceID = "INVALID" };

        Assert.Throws<SceneAccessException>(
            () => planet.AddChild(officer),
            "Adding an officer with a mismatched OwnerInstanceID should throw a SceneAccessException."
        );
    }

    [Test]
    public void AddBuilding_ValidBuilding_AddsToPlanet()
    {
        Building building = new Building
        {
            BuildingSlot = BuildingSlot.Ground,
            DisplayName = "Test Building",
            OwnerInstanceID = "FNALL1",
        };

        planet.AddChild(building);

        List<Building> buildings = planet.GetBuildings(BuildingSlot.Ground);
        Assert.Contains(
            building,
            buildings,
            "Building should be added to the ground slots of the planet."
        );
    }

    [Test]
    public void AddBuilding_ExceedsCapacity_ThrowsException()
    {
        for (int i = 0; i < planet.GroundSlots; i++)
        {
            planet.AddChild(
                new Building { BuildingSlot = BuildingSlot.Ground, OwnerInstanceID = "FNALL1" }
            );
        }

        Building extraBuilding = new Building
        {
            BuildingSlot = BuildingSlot.Ground,
            OwnerInstanceID = "FNALL1",
        };

        Assert.Throws<GameStateException>(
            () => planet.AddChild(extraBuilding),
            "Adding a building when slots are full should throw a GameStateException."
        );
    }

    [Test]
    public void RemoveFleet_ValidFleet_RemovesFromPlanet()
    {
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
        planet.AddChild(fleet);
        planet.RemoveChild(fleet);

        Assert.IsFalse(planet.Fleets.Contains(fleet), "Fleet should be removed from the planet.");
    }

    [Test]
    public void RemoveOfficer_ValidOfficer_RemovesFromPlanet()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        planet.AddChild(officer);
        planet.RemoveChild(officer);

        Assert.IsFalse(
            planet.Officers.Contains(officer),
            "Officer should be removed from the planet."
        );
    }

    [Test]
    public void RemoveBuilding_ValidBuilding_RemovesFromPlanet()
    {
        Building building = new Building
        {
            BuildingSlot = BuildingSlot.Ground,
            DisplayName = "Test Building",
            OwnerInstanceID = "FNALL1",
        };

        planet.AddChild(building);
        planet.RemoveChild(building);

        Assert.IsFalse(
            planet.GetBuildings(BuildingSlot.Ground).Contains(building),
            "Building should be removed from the planet."
        );
    }

    [Test]
    public void GetChildren_ValidChildren_ReturnsAllChildren()
    {
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        Building building = new Building
        {
            BuildingSlot = BuildingSlot.Ground,
            OwnerInstanceID = "FNALL1",
        };

        planet.AddChild(fleet);
        planet.AddChild(officer);
        planet.AddChild(building);

        IEnumerable<ISceneNode> children = planet.GetChildren();
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
        planet.SetPopularSupport("FNALL1", 50);

        int support = planet.GetPopularSupport("FNALL1");
        Assert.AreEqual(
            50,
            support,
            "Popular support for the faction should be correctly retrieved."
        );
    }

    [Test]
    public void GetPopularSupport_NonExistingFaction_ReturnsZero()
    {
        int support = planet.GetPopularSupport("INVALID");
        Assert.AreEqual(0, support, "Popular support for a non-existing faction should return 0.");
    }

    [Test]
    public void SetPopularSupport_ValidFaction_SetsSupport()
    {
        planet.SetPopularSupport("FNALL1", 75);

        int support = planet.GetPopularSupport("FNALL1");
        Assert.AreEqual(75, support, "Popular support should be correctly set for the faction.");
    }

    [Test]
    public void GetDistanceTo_ValidTargetPlanet_ReturnsCorrectTime()
    {
        Planet targetPlanet = new Planet
        {
            PositionX = planet.PositionX + 10,
            PositionY = planet.PositionY + 10,
        };

        int travelTime = planet.GetDistanceTo(targetPlanet);

        Assert.AreEqual(2, travelTime, "Travel time should be calculated correctly.");
    }

    [Test]
    public void AddToManufacturingQueue_UnitWithoutParent_ThrowsException()
    {
        IManufacturable unit = new Starfighter();

        Assert.Throws<InvalidSceneOperationException>(
            () => planet.AddToManufacturingQueue(unit),
            "Adding a manufacturable unit without a parent should throw a GameStateException."
        );
    }

    [Test]
    public void SerializeAndDeserialize_Planet_RetainsProperties()
    {
        planet.SetPopularSupport("FNALL1", 100);
        planet.IsDestroyed = true;
        planet.AddChild(new Fleet { OwnerInstanceID = "FNALL1" });

        string serialized = SerializationHelper.Serialize(planet);
        Planet deserialized = SerializationHelper.Deserialize<Planet>(serialized);

        Assert.AreEqual(
            planet.IsDestroyed,
            deserialized.IsDestroyed,
            "Deserialized planet should retain IsDestroyed property."
        );
        Assert.AreEqual(
            planet.GetPopularSupport("FNALL1"),
            deserialized.GetPopularSupport("FNALL1"),
            "Deserialized planet should retain popular support."
        );
        Assert.AreEqual(
            planet.Fleets.Count,
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
        };
        Building building2 = new Building
        {
            ProductionType = ManufacturingType.Ship,
            ProcessRate = 3,
            OwnerInstanceID = "FNALL1",
        };

        planet.AddChild(building1);
        planet.AddChild(building2);

        int rate = planet.GetProductionRate(ManufacturingType.Ship);

        Assert.AreEqual(
            1,
            rate,
            "Production rate should be calculated correctly based on building process rates."
        );
    }
}
