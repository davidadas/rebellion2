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
    public void TestAddFleet()
    {
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
        planet.AddChild(fleet);

        Assert.Contains(fleet, planet.Fleets, "Fleet should be added to the planet.");
    }

    [Test]
    public void TestAddOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        planet.AddChild(officer);

        Assert.Contains(officer, planet.Officers, "Officer should be added to the planet.");
    }

    [Test]
    public void TestAddBuilding()
    {
        Building building = new Building
        {
            BuildingSlot = BuildingSlot.Ground,
            DisplayName = "Test Building",
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
    public void TestRemoveFleet()
    {
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
        planet.AddChild(fleet);
        planet.RemoveChild(fleet);

        Assert.IsFalse(planet.Fleets.Contains(fleet), "Fleet should be removed from the planet.");
    }

    [Test]
    public void TestRemoveOfficer()
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
    public void TestRemoveBuilding()
    {
        Building building = new Building
        {
            BuildingSlot = BuildingSlot.Ground,
            DisplayName = "Test Building",
        };
        planet.AddChild(building);
        planet.RemoveChild(building);

        Assert.IsFalse(
            planet.Buildings[BuildingSlot.Ground].Contains(building),
            "Building should be removed from the planet."
        );
    }

    [Test]
    public void TestGetChildren()
    {
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        Building building = new Building
        {
            BuildingSlot = BuildingSlot.Ground,
            DisplayName = "Test Building",
        };

        planet.AddChild(fleet);
        planet.AddChild(officer);
        planet.AddChild(building);

        IEnumerable<SceneNode> children = planet.GetChildren();
        List<SceneNode> expectedChildren = new List<SceneNode> { fleet, officer, building };

        CollectionAssert.AreEquivalent(
            expectedChildren,
            children,
            "Planet should return correct children."
        );
    }
}
