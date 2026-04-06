using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

[TestFixture]
public class GalaxyMapTests
{
    private GalaxyMap _galaxyMap;
    private PlanetSystem _planetSystem1;
    private PlanetSystem _planetSystem2;

    [SetUp]
    public void SetUp()
    {
        _galaxyMap = new GalaxyMap { InstanceID = "GALAXY1" };

        _planetSystem1 = new PlanetSystem { InstanceID = "SYSTEM1" };

        _planetSystem2 = new PlanetSystem { InstanceID = "SYSTEM2" };
    }

    [Test]
    public void AddChild_WithPlanetSystem_AddsPlanetSystem()
    {
        _galaxyMap.AddChild(_planetSystem1);

        Assert.Contains(_planetSystem1, _galaxyMap.PlanetSystems);
    }

    [Test]
    public void RemoveChild_RemovesPlanetSystem()
    {
        _galaxyMap.AddChild(_planetSystem1);

        _galaxyMap.RemoveChild(_planetSystem1);

        Assert.IsFalse(_galaxyMap.PlanetSystems.Contains(_planetSystem1));
    }

    [Test]
    public void GetChildren_ReturnsAllPlanetSystems()
    {
        _galaxyMap.AddChild(_planetSystem1);
        _galaxyMap.AddChild(_planetSystem2);

        IEnumerable<ISceneNode> children = _galaxyMap.GetChildren();

        CollectionAssert.AreEquivalent(
            new ISceneNode[] { _planetSystem1, planetSystem2 },
            children,
            "GalaxyMap should return correct children."
        );
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        _galaxyMap.AddChild(_planetSystem1);
        _galaxyMap.AddChild(_planetSystem2);

        string serialized = SerializationHelper.Serialize(galaxyMap);
        GalaxyMap deserialized = SerializationHelper.Deserialize<GalaxyMap>(serialized);

        Assert.AreEqual(
            _galaxyMap.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            _galaxyMap.PlanetSystems.Count,
            deserialized.PlanetSystems.Count,
            "PlanetSystems count should be correctly deserialized."
        );
    }

    [Test]
    public void InstanceID_WhenSet_ReturnsCorrectValue()
    {
        GalaxyMap map = new GalaxyMap { InstanceID = "TEST_GALAXY" };

        Assert.AreEqual("TEST_GALAXY", map.InstanceID);
    }

    [Test]
    public void AddChild_WithMultiplePlanetSystems_AddsAllSystems()
    {
        PlanetSystem planetSystem3 = new PlanetSystem { InstanceID = "SYSTEM3" };

        _galaxyMap.AddChild(_planetSystem1);
        _galaxyMap.AddChild(_planetSystem2);
        _galaxyMap.AddChild(planetSystem3);

        Assert.AreEqual(3, _galaxyMap.PlanetSystems.Count);
        Assert.Contains(_planetSystem1, _galaxyMap.PlanetSystems);
        Assert.Contains(_planetSystem2, _galaxyMap.PlanetSystems);
        Assert.Contains(planetSystem3, _galaxyMap.PlanetSystems);
    }

    [Test]
    public void RemoveChild_WithMultiplePlanetSystems_RemovesCorrectSystems()
    {
        _galaxyMap.AddChild(_planetSystem1);
        _galaxyMap.AddChild(_planetSystem2);

        _galaxyMap.RemoveChild(_planetSystem1);

        Assert.AreEqual(1, _galaxyMap.PlanetSystems.Count);
        Assert.IsFalse(_galaxyMap.PlanetSystems.Contains(_planetSystem1));
        Assert.Contains(_planetSystem2, _galaxyMap.PlanetSystems);
    }

    [Test]
    public void RemoveChild_RemovingAllSystems_ResultsInEmptyList()
    {
        _galaxyMap.AddChild(_planetSystem1);
        _galaxyMap.AddChild(_planetSystem2);

        _galaxyMap.RemoveChild(_planetSystem1);
        _galaxyMap.RemoveChild(_planetSystem2);

        Assert.AreEqual(0, _galaxyMap.PlanetSystems.Count);
    }

    [Test]
    public void AddChild_WithNullPlanetSystem_DoesNotThrowException()
    {
        Assert.DoesNotThrow(() => _galaxyMap.AddChild(null));
    }

    [Test]
    public void RemoveChild_WithNullPlanetSystem_DoesNotThrowException()
    {
        Assert.DoesNotThrow(() => _galaxyMap.RemoveChild(null));
    }

    [Test]
    public void AddChild_WithNonPlanetSystemNode_DoesNotAddToList()
    {
        ISceneNode nonPlanetSystem = new GalaxyMap { InstanceID = "NOT_A_PLANET_SYSTEM" };

        _galaxyMap.AddChild(nonPlanetSystem);

        Assert.AreEqual(0, _galaxyMap.PlanetSystems.Count);
    }

    [Test]
    public void PlanetSystems_WhenInitialized_IsEmptyList()
    {
        GalaxyMap newMap = new GalaxyMap();

        Assert.IsNotNull(newMap.PlanetSystems);
        Assert.AreEqual(0, newMap.PlanetSystems.Count);
    }

    [Test]
    public void PlanetSystems_AfterAddingAndRemoving_MaintainsCorrectCount()
    {
        Assert.AreEqual(0, _galaxyMap.PlanetSystems.Count);

        _galaxyMap.AddChild(_planetSystem1);
        Assert.AreEqual(1, _galaxyMap.PlanetSystems.Count);

        _galaxyMap.AddChild(_planetSystem2);
        Assert.AreEqual(2, _galaxyMap.PlanetSystems.Count);

        _galaxyMap.RemoveChild(_planetSystem1);
        Assert.AreEqual(1, _galaxyMap.PlanetSystems.Count);

        _galaxyMap.RemoveChild(_planetSystem2);
        Assert.AreEqual(0, _galaxyMap.PlanetSystems.Count);
    }

    [Test]
    public void AddChild_WithSamePlanetSystemTwice_AddsItTwice()
    {
        _galaxyMap.AddChild(_planetSystem1);
        _galaxyMap.AddChild(_planetSystem1);

        Assert.AreEqual(2, _galaxyMap.PlanetSystems.Count);
    }

    [Test]
    public void RemoveChild_WithSystemNotInList_DoesNotChangeCount()
    {
        _galaxyMap.AddChild(_planetSystem1);

        _galaxyMap.RemoveChild(_planetSystem2);

        Assert.AreEqual(1, _galaxyMap.PlanetSystems.Count);
    }
}
