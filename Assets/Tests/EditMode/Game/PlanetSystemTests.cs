using System.Collections.Generic;
using System.Drawing;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

[TestFixture]
public class PlanetSystemTests
{
    private PlanetSystem planetSystem;
    private Planet planet1;
    private Planet planet2;

    [SetUp]
    public void SetUp()
    {
        planetSystem = new PlanetSystem
        {
            InstanceID = "SYSTEM1",
            Visibility = GameSize.Medium,
            SystemType = PlanetSystemType.CoreSystem,
            Importance = PlanetSystemImportance.High,
        };

        planet1 = new Planet { InstanceID = "PLANET1", OwnerInstanceID = "FACTION1" };

        planet2 = new Planet { InstanceID = "PLANET2", OwnerInstanceID = "FACTION1" };
    }

    [Test]
    public void AddChild_WithPlanet_AddsPlanet()
    {
        planetSystem.AddChild(planet1);

        Assert.Contains(planet1, planetSystem.Planets);
    }

    [Test]
    public void RemoveChild_RemovesPlanet()
    {
        planetSystem.AddChild(planet1);

        planetSystem.RemoveChild(planet1);

        Assert.IsFalse(planetSystem.Planets.Contains(planet1));
    }

    [Test]
    public void GetChildren_ReturnsAllPlanets()
    {
        planetSystem.AddChild(planet1);
        planetSystem.AddChild(planet2);

        IEnumerable<ISceneNode> children = planetSystem.GetChildren();

        CollectionAssert.AreEquivalent(
            new ISceneNode[] { planet1, planet2 },
            children,
            "PlanetSystem should return correct children."
        );
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        planetSystem.AddChild(planet1);
        planetSystem.AddChild(planet2);

        string serialized = SerializationHelper.Serialize(planetSystem);
        PlanetSystem deserialized = SerializationHelper.Deserialize<PlanetSystem>(serialized);

        Assert.AreEqual(
            planetSystem.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            planetSystem.GetPosition().X,
            deserialized.GetPosition().X,
            "PositionX should be correctly deserialized."
        );
        Assert.AreEqual(
            planetSystem.GetPosition().Y,
            deserialized.GetPosition().Y,
            "PositionY should be correctly deserialized."
        );
        Assert.AreEqual(
            planetSystem.Visibility,
            deserialized.Visibility,
            "Visibility should be correctly deserialized."
        );
        Assert.AreEqual(
            planetSystem.SystemType,
            deserialized.SystemType,
            "SystemType should be correctly deserialized."
        );
        Assert.AreEqual(
            planetSystem.Importance,
            deserialized.Importance,
            "Importance should be correctly deserialized."
        );
        Assert.AreEqual(
            planetSystem.Planets.Count,
            deserialized.Planets.Count,
            "Planets count should be correctly deserialized."
        );
    }

    [Test]
    public void SystemType_SetToCoreSystem_ReturnsCoreSystem()
    {
        planetSystem.SystemType = PlanetSystemType.CoreSystem;

        Assert.AreEqual(PlanetSystemType.CoreSystem, planetSystem.SystemType);
    }

    [Test]
    public void SystemType_SetToOuterRim_ReturnsOuterRim()
    {
        planetSystem.SystemType = PlanetSystemType.OuterRim;

        Assert.AreEqual(PlanetSystemType.OuterRim, planetSystem.SystemType);
    }

    [Test]
    public void Visibility_SetToSmall_ReturnsSmall()
    {
        planetSystem.Visibility = GameSize.Small;

        Assert.AreEqual(GameSize.Small, planetSystem.Visibility);
    }

    [Test]
    public void Visibility_SetToMedium_ReturnsMedium()
    {
        planetSystem.Visibility = GameSize.Medium;

        Assert.AreEqual(GameSize.Medium, planetSystem.Visibility);
    }

    [Test]
    public void Visibility_SetToLarge_ReturnsLarge()
    {
        planetSystem.Visibility = GameSize.Large;

        Assert.AreEqual(GameSize.Large, planetSystem.Visibility);
    }

    [Test]
    public void Importance_SetToLow_ReturnsLow()
    {
        planetSystem.Importance = PlanetSystemImportance.Low;

        Assert.AreEqual(PlanetSystemImportance.Low, planetSystem.Importance);
    }

    [Test]
    public void Importance_SetToMedium_ReturnsMedium()
    {
        planetSystem.Importance = PlanetSystemImportance.Medium;

        Assert.AreEqual(PlanetSystemImportance.Medium, planetSystem.Importance);
    }

    [Test]
    public void Importance_SetToHigh_ReturnsHigh()
    {
        planetSystem.Importance = PlanetSystemImportance.High;

        Assert.AreEqual(PlanetSystemImportance.High, planetSystem.Importance);
    }

    [Test]
    public void AddChild_MultiplePlanets_AddsAllPlanets()
    {
        Planet planet3 = new Planet { InstanceID = "PLANET3", OwnerInstanceID = "FACTION2" };
        Planet planet4 = new Planet { InstanceID = "PLANET4", OwnerInstanceID = "FACTION2" };

        planetSystem.AddChild(planet1);
        planetSystem.AddChild(planet2);
        planetSystem.AddChild(planet3);
        planetSystem.AddChild(planet4);

        Assert.AreEqual(4, planetSystem.Planets.Count);
        Assert.Contains(planet1, planetSystem.Planets);
        Assert.Contains(planet2, planetSystem.Planets);
        Assert.Contains(planet3, planetSystem.Planets);
        Assert.Contains(planet4, planetSystem.Planets);
    }

    [Test]
    public void AddChild_SamePlanetTwice_AddsPlanetTwice()
    {
        planetSystem.AddChild(planet1);
        planetSystem.AddChild(planet1);

        Assert.AreEqual(2, planetSystem.Planets.Count);
    }

    [Test]
    public void RemoveChild_FromMultiplePlanets_RemovesOnlySpecifiedPlanet()
    {
        planetSystem.AddChild(planet1);
        planetSystem.AddChild(planet2);

        planetSystem.RemoveChild(planet1);

        Assert.AreEqual(1, planetSystem.Planets.Count);
        Assert.IsFalse(planetSystem.Planets.Contains(planet1));
        Assert.IsTrue(planetSystem.Planets.Contains(planet2));
    }

    [Test]
    public void GetPosition_WithZeroCoordinates_ReturnsZeroPoint()
    {
        Point position = planetSystem.GetPosition();

        Assert.AreEqual(0, position.X);
        Assert.AreEqual(0, position.Y);
    }
}
