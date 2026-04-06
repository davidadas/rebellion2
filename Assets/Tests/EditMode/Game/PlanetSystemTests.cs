using System.Collections.Generic;
using System.Drawing;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

[TestFixture]
public class PlanetSystemTests
{
    private PlanetSystem _planetSystem;
    private Planet _planet1;
    private Planet _planet2;

    [SetUp]
    public void SetUp()
    {
        _planetSystem = new PlanetSystem
        {
            InstanceID = "SYSTEM1",
            Visibility = GameSize.Medium,
            SystemType = PlanetSystemType.CoreSystem,
            Importance = PlanetSystemImportance.High,
        };

        _planet1 = new Planet { InstanceID = "PLANET1", OwnerInstanceID = "FACTION1" };

        _planet2 = new Planet { InstanceID = "PLANET2", OwnerInstanceID = "FACTION1" };
    }

    [Test]
    public void AddChild_WithPlanet_AddsPlanet()
    {
        _planetSystem.AddChild(_planet1);

        Assert.Contains(_planet1, _planetSystem.Planets);
    }

    [Test]
    public void RemoveChild_RemovesPlanet()
    {
        _planetSystem.AddChild(_planet1);

        _planetSystem.RemoveChild(_planet1);

        Assert.IsFalse(_planetSystem.Planets.Contains(_planet1));
    }

    [Test]
    public void GetChildren_ReturnsAllPlanets()
    {
        _planetSystem.AddChild(_planet1);
        _planetSystem.AddChild(_planet2);

        IEnumerable<ISceneNode> children = _planetSystem.GetChildren();

        CollectionAssert.AreEquivalent(
            new ISceneNode[] { _planet1, _planet2 },
            children,
            "PlanetSystem should return correct children."
        );
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        _planetSystem.AddChild(_planet1);
        _planetSystem.AddChild(_planet2);

        string serialized = SerializationHelper.Serialize(_planetSystem);
        PlanetSystem deserialized = SerializationHelper.Deserialize<PlanetSystem>(serialized);

        Assert.AreEqual(
            _planetSystem.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            _planetSystem.GetPosition().X,
            deserialized.GetPosition().X,
            "PositionX should be correctly deserialized."
        );
        Assert.AreEqual(
            _planetSystem.GetPosition().Y,
            deserialized.GetPosition().Y,
            "PositionY should be correctly deserialized."
        );
        Assert.AreEqual(
            _planetSystem.Visibility,
            deserialized.Visibility,
            "Visibility should be correctly deserialized."
        );
        Assert.AreEqual(
            _planetSystem.SystemType,
            deserialized.SystemType,
            "SystemType should be correctly deserialized."
        );
        Assert.AreEqual(
            _planetSystem.Importance,
            deserialized.Importance,
            "Importance should be correctly deserialized."
        );
        Assert.AreEqual(
            _planetSystem.Planets.Count,
            deserialized.Planets.Count,
            "Planets count should be correctly deserialized."
        );
    }

    [Test]
    public void SystemType_SetToCoreSystem_ReturnsCoreSystem()
    {
        _planetSystem.SystemType = PlanetSystemType.CoreSystem;

        Assert.AreEqual(PlanetSystemType.CoreSystem, _planetSystem.SystemType);
    }

    [Test]
    public void SystemType_SetToOuterRim_ReturnsOuterRim()
    {
        _planetSystem.SystemType = PlanetSystemType.OuterRim;

        Assert.AreEqual(PlanetSystemType.OuterRim, _planetSystem.SystemType);
    }

    [Test]
    public void Visibility_SetToSmall_ReturnsSmall()
    {
        _planetSystem.Visibility = GameSize.Small;

        Assert.AreEqual(GameSize.Small, _planetSystem.Visibility);
    }

    [Test]
    public void Visibility_SetToMedium_ReturnsMedium()
    {
        _planetSystem.Visibility = GameSize.Medium;

        Assert.AreEqual(GameSize.Medium, _planetSystem.Visibility);
    }

    [Test]
    public void Visibility_SetToLarge_ReturnsLarge()
    {
        _planetSystem.Visibility = GameSize.Large;

        Assert.AreEqual(GameSize.Large, _planetSystem.Visibility);
    }

    [Test]
    public void Importance_SetToLow_ReturnsLow()
    {
        _planetSystem.Importance = PlanetSystemImportance.Low;

        Assert.AreEqual(PlanetSystemImportance.Low, _planetSystem.Importance);
    }

    [Test]
    public void Importance_SetToMedium_ReturnsMedium()
    {
        _planetSystem.Importance = PlanetSystemImportance.Medium;

        Assert.AreEqual(PlanetSystemImportance.Medium, _planetSystem.Importance);
    }

    [Test]
    public void Importance_SetToHigh_ReturnsHigh()
    {
        _planetSystem.Importance = PlanetSystemImportance.High;

        Assert.AreEqual(PlanetSystemImportance.High, _planetSystem.Importance);
    }

    [Test]
    public void AddChild_MultiplePlanets_AddsAllPlanets()
    {
        Planet planet3 = new Planet { InstanceID = "PLANET3", OwnerInstanceID = "FACTION2" };
        Planet planet4 = new Planet { InstanceID = "PLANET4", OwnerInstanceID = "FACTION2" };

        _planetSystem.AddChild(_planet1);
        _planetSystem.AddChild(_planet2);
        _planetSystem.AddChild(planet3);
        _planetSystem.AddChild(planet4);

        Assert.AreEqual(4, _planetSystem.Planets.Count);
        Assert.Contains(_planet1, _planetSystem.Planets);
        Assert.Contains(_planet2, _planetSystem.Planets);
        Assert.Contains(planet3, _planetSystem.Planets);
        Assert.Contains(planet4, _planetSystem.Planets);
    }

    [Test]
    public void AddChild_SamePlanetTwice_AddsPlanetTwice()
    {
        _planetSystem.AddChild(_planet1);
        _planetSystem.AddChild(_planet1);

        Assert.AreEqual(2, _planetSystem.Planets.Count);
    }

    [Test]
    public void RemoveChild_FromMultiplePlanets_RemovesOnlySpecifiedPlanet()
    {
        _planetSystem.AddChild(_planet1);
        _planetSystem.AddChild(_planet2);

        _planetSystem.RemoveChild(_planet1);

        Assert.AreEqual(1, _planetSystem.Planets.Count);
        Assert.IsFalse(_planetSystem.Planets.Contains(_planet1));
        Assert.IsTrue(_planetSystem.Planets.Contains(_planet2));
    }

    [Test]
    public void GetPosition_WithZeroCoordinates_ReturnsZeroPoint()
    {
        Point position = _planetSystem.GetPosition();

        Assert.AreEqual(0, position.X);
        Assert.AreEqual(0, position.Y);
    }
}
