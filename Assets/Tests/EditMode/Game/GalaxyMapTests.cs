using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

[TestFixture]
public class GalaxyMapTests
{
    private GalaxyMap galaxyMap;
    private PlanetSystem planetSystem1;
    private PlanetSystem planetSystem2;

    [SetUp]
    public void SetUp()
    {
        galaxyMap = new GalaxyMap { InstanceID = "GALAXY1" };

        planetSystem1 = new PlanetSystem { InstanceID = "SYSTEM1" };

        planetSystem2 = new PlanetSystem { InstanceID = "SYSTEM2" };
    }

    [Test]
    public void AddChild_WithPlanetSystem_AddsPlanetSystem()
    {
        galaxyMap.AddChild(planetSystem1);

        Assert.Contains(planetSystem1, galaxyMap.PlanetSystems);
    }

    [Test]
    public void RemoveChild_RemovesPlanetSystem()
    {
        galaxyMap.AddChild(planetSystem1);

        galaxyMap.RemoveChild(planetSystem1);

        Assert.IsFalse(galaxyMap.PlanetSystems.Contains(planetSystem1));
    }

    [Test]
    public void GetChildren_ReturnsAllPlanetSystems()
    {
        galaxyMap.AddChild(planetSystem1);
        galaxyMap.AddChild(planetSystem2);

        IEnumerable<ISceneNode> children = galaxyMap.GetChildren();

        CollectionAssert.AreEquivalent(
            new ISceneNode[] { planetSystem1, planetSystem2 },
            children,
            "GalaxyMap should return correct children."
        );
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        galaxyMap.AddChild(planetSystem1);
        galaxyMap.AddChild(planetSystem2);

        string serialized = SerializationHelper.Serialize(galaxyMap);
        GalaxyMap deserialized = SerializationHelper.Deserialize<GalaxyMap>(serialized);

        Assert.AreEqual(
            galaxyMap.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            galaxyMap.PlanetSystems.Count,
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

        galaxyMap.AddChild(planetSystem1);
        galaxyMap.AddChild(planetSystem2);
        galaxyMap.AddChild(planetSystem3);

        Assert.AreEqual(3, galaxyMap.PlanetSystems.Count);
        Assert.Contains(planetSystem1, galaxyMap.PlanetSystems);
        Assert.Contains(planetSystem2, galaxyMap.PlanetSystems);
        Assert.Contains(planetSystem3, galaxyMap.PlanetSystems);
    }

    [Test]
    public void RemoveChild_WithMultiplePlanetSystems_RemovesCorrectSystems()
    {
        galaxyMap.AddChild(planetSystem1);
        galaxyMap.AddChild(planetSystem2);

        galaxyMap.RemoveChild(planetSystem1);

        Assert.AreEqual(1, galaxyMap.PlanetSystems.Count);
        Assert.IsFalse(galaxyMap.PlanetSystems.Contains(planetSystem1));
        Assert.Contains(planetSystem2, galaxyMap.PlanetSystems);
    }

    [Test]
    public void RemoveChild_RemovingAllSystems_ResultsInEmptyList()
    {
        galaxyMap.AddChild(planetSystem1);
        galaxyMap.AddChild(planetSystem2);

        galaxyMap.RemoveChild(planetSystem1);
        galaxyMap.RemoveChild(planetSystem2);

        Assert.AreEqual(0, galaxyMap.PlanetSystems.Count);
    }

    [Test]
    public void AddChild_WithNullPlanetSystem_DoesNotThrowException()
    {
        Assert.DoesNotThrow(() => galaxyMap.AddChild(null));
    }

    [Test]
    public void RemoveChild_WithNullPlanetSystem_DoesNotThrowException()
    {
        Assert.DoesNotThrow(() => galaxyMap.RemoveChild(null));
    }

    [Test]
    public void AddChild_WithNonPlanetSystemNode_DoesNotAddToList()
    {
        ISceneNode nonPlanetSystem = new GalaxyMap { InstanceID = "NOT_A_PLANET_SYSTEM" };

        galaxyMap.AddChild(nonPlanetSystem);

        Assert.AreEqual(0, galaxyMap.PlanetSystems.Count);
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
        Assert.AreEqual(0, galaxyMap.PlanetSystems.Count);

        galaxyMap.AddChild(planetSystem1);
        Assert.AreEqual(1, galaxyMap.PlanetSystems.Count);

        galaxyMap.AddChild(planetSystem2);
        Assert.AreEqual(2, galaxyMap.PlanetSystems.Count);

        galaxyMap.RemoveChild(planetSystem1);
        Assert.AreEqual(1, galaxyMap.PlanetSystems.Count);

        galaxyMap.RemoveChild(planetSystem2);
        Assert.AreEqual(0, galaxyMap.PlanetSystems.Count);
    }

    [Test]
    public void AddChild_WithSamePlanetSystemTwice_AddsItTwice()
    {
        galaxyMap.AddChild(planetSystem1);
        galaxyMap.AddChild(planetSystem1);

        Assert.AreEqual(2, galaxyMap.PlanetSystems.Count);
    }

    [Test]
    public void RemoveChild_WithSystemNotInList_DoesNotChangeCount()
    {
        galaxyMap.AddChild(planetSystem1);

        galaxyMap.RemoveChild(planetSystem2);

        Assert.AreEqual(1, galaxyMap.PlanetSystems.Count);
    }
}
