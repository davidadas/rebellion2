using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

[TestFixture]
public class FleetTests
{
    private Fleet _fleet;
    private CapitalShip _capitalShip1;
    private CapitalShip _capitalShip2;

    [SetUp]
    public void SetUp()
    {
        _fleet = new Fleet
        {
            InstanceID = "FLEET1",
            OwnerInstanceID = "FACTION1",
            Movement = null,
        };

        _capitalShip1 = new CapitalShip
        {
            InstanceID = "SHIP1",
            OwnerInstanceID = "FACTION1",
            StarfighterCapacity = 5,
            RegimentCapacity = 3,
        };

        _capitalShip2 = new CapitalShip
        {
            InstanceID = "SHIP2",
            OwnerInstanceID = "FACTION1",
            StarfighterCapacity = 8,
            RegimentCapacity = 4,
        };
    }

    [Test]
    public void AddChild_WithCapitalShip_AddsCapitalShip()
    {
        _fleet.AddChild(_capitalShip1);

        Assert.Contains(_capitalShip1, _fleet.CapitalShips);
    }

    [Test]
    public void AddChild_WithInvalidOwner_ThrowsException()
    {
        CapitalShip invalidShip = new CapitalShip { OwnerInstanceID = "INVALID" };

        Assert.Throws<SceneAccessException>(() => _fleet.AddChild(invalidShip));
    }

    [Test]
    public void RemoveChild_RemovesCapitalShip()
    {
        _fleet.AddChild(_capitalShip1);

        _fleet.RemoveChild(_capitalShip1);

        Assert.IsFalse(_fleet.CapitalShips.Contains(_capitalShip1));
    }

    [Test]
    public void GetChildren_ReturnsAllCapitalShips()
    {
        _fleet.AddChild(_capitalShip1);
        _fleet.AddChild(_capitalShip2);

        IEnumerable<ISceneNode> children = _fleet.GetChildren();

        CollectionAssert.AreEquivalent(
            new ISceneNode[] { _capitalShip1, _capitalShip2 },
            children,
            "Fleet should return correct children."
        );
    }

    [Test]
    public void GetStarfighterCapacity_ReturnsSum()
    {
        _fleet.AddChild(_capitalShip1);
        _fleet.AddChild(_capitalShip2);

        int capacity = _fleet.GetStarfighterCapacity();

        Assert.AreEqual(
            13,
            capacity,
            "Should return sum of all capital ship starfighter capacities"
        );
    }

    [Test]
    public void GetRegimentCapacity_ReturnsSum()
    {
        _fleet.AddChild(_capitalShip1);
        _fleet.AddChild(_capitalShip2);

        int capacity = _fleet.GetRegimentCapacity();

        Assert.AreEqual(7, capacity, "Should return sum of all capital ship regiment capacities");
    }

    [Test]
    public void GetCurrentStarfighterCount_ReturnsSum()
    {
        Starfighter starfighter1 = new Starfighter();
        Starfighter starfighter2 = new Starfighter();

        _fleet.AddChild(_capitalShip1);
        _capitalShip1.AddStarfighter(starfighter1);
        _capitalShip1.AddStarfighter(starfighter2);

        int count = _fleet.GetCurrentStarfighterCount();

        Assert.AreEqual(2, count, "Should return total starfighters across all capital ships");
    }

    [Test]
    public void GetExcessStarfighterCapacity_ReturnsCorrectValue()
    {
        Starfighter starfighter = new Starfighter();

        _fleet.AddChild(_capitalShip1);
        _capitalShip1.AddStarfighter(starfighter);

        int excess = _fleet.GetExcessStarfighterCapacity();

        Assert.AreEqual(4, excess, "Should return excess capacity (5 - 1 = 4)");
    }

    [Test]
    public void GetCurrentRegimentCount_ReturnsSum()
    {
        Regiment regiment1 = new Regiment();
        Regiment regiment2 = new Regiment();

        _fleet.AddChild(_capitalShip1);
        _capitalShip1.AddRegiment(regiment1);
        _capitalShip1.AddRegiment(regiment2);

        int count = _fleet.GetCurrentRegimentCount();

        Assert.AreEqual(2, count, "Should return total regiments across all capital ships");
    }

    [Test]
    public void GetExcessRegimentCapacity_ReturnsCorrectValue()
    {
        Regiment regiment = new Regiment();

        _fleet.AddChild(_capitalShip1);
        _capitalShip1.AddRegiment(regiment);

        int excess = _fleet.GetExcessRegimentCapacity();

        Assert.AreEqual(2, excess, "Should return excess capacity (3 - 1 = 2)");
    }

    [Test]
    public void IsMovable_WhenIdle_ReturnsTrue()
    {
        _fleet.Movement = null;

        bool isMovable = _fleet.IsMovable();

        Assert.IsTrue(isMovable, "Fleet should be movable when idle");
    }

    [Test]
    public void IsMovable_WhenInTransit_ReturnsFalse()
    {
        _fleet.Movement = new MovementState();

        bool isMovable = _fleet.IsMovable();

        Assert.IsFalse(isMovable, "Fleet should not be movable when in transit");
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        _fleet.AddChild(_capitalShip1);
        string serialized = SerializationHelper.Serialize(_fleet);
        Fleet deserialized = SerializationHelper.Deserialize<Fleet>(serialized);

        Assert.AreEqual(
            _fleet.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            _fleet.OwnerInstanceID,
            deserialized.OwnerInstanceID,
            "OwnerInstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            _fleet.Movement,
            deserialized.Movement,
            "MovementStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            _fleet.GetPosition().X,
            deserialized.GetPosition().X,
            "PositionX should be correctly deserialized."
        );
        Assert.AreEqual(
            _fleet.GetPosition().Y,
            deserialized.GetPosition().Y,
            "PositionY should be correctly deserialized."
        );
        Assert.AreEqual(
            _fleet.CapitalShips.Count,
            deserialized.CapitalShips.Count,
            "CapitalShips count should be correctly deserialized."
        );
    }

    [Test]
    public void GetStarfighters_ReturnsAllStarfightersAcrossFleet()
    {
        Starfighter starfighter1 = new Starfighter();
        Starfighter starfighter2 = new Starfighter();
        Starfighter starfighter3 = new Starfighter();

        _fleet.AddChild(_capitalShip1);
        _fleet.AddChild(_capitalShip2);
        _capitalShip1.AddStarfighter(starfighter1);
        _capitalShip1.AddStarfighter(starfighter2);
        _capitalShip2.AddStarfighter(starfighter3);

        IEnumerable<Starfighter> starfighters = _fleet.GetStarfighters();

        CollectionAssert.AreEquivalent(
            new Starfighter[] { starfighter1, starfighter2, starfighter3 },
            starfighters,
            "Should return all starfighters from all capital ships"
        );
    }

    [Test]
    public void GetStarfighters_WhenNoStarfighters_ReturnsEmpty()
    {
        _fleet.AddChild(_capitalShip1);

        IEnumerable<Starfighter> starfighters = _fleet.GetStarfighters();

        Assert.IsEmpty(starfighters, "Should return empty collection when no starfighters");
    }

    [Test]
    public void GetRegiments_ReturnsAllRegimentsAcrossFleet()
    {
        Regiment regiment1 = new Regiment();
        Regiment regiment2 = new Regiment();
        Regiment regiment3 = new Regiment();

        _fleet.AddChild(_capitalShip1);
        _fleet.AddChild(_capitalShip2);
        _capitalShip1.AddRegiment(regiment1);
        _capitalShip1.AddRegiment(regiment2);
        _capitalShip2.AddRegiment(regiment3);

        IEnumerable<Regiment> regiments = _fleet.GetRegiments();

        CollectionAssert.AreEquivalent(
            new Regiment[] { regiment1, regiment2, regiment3 },
            regiments,
            "Should return all regiments from all capital ships"
        );
    }

    [Test]
    public void GetRegiments_WhenNoRegiments_ReturnsEmpty()
    {
        _fleet.AddChild(_capitalShip1);

        IEnumerable<Regiment> regiments = _fleet.GetRegiments();

        Assert.IsEmpty(regiments, "Should return empty collection when no regiments");
    }

    [Test]
    public void GetOfficers_ReturnsAllOfficersAcrossFleet()
    {
        Officer officer1 = new Officer { OwnerInstanceID = "FACTION1" };
        Officer officer2 = new Officer { OwnerInstanceID = "FACTION1" };
        Officer officer3 = new Officer { OwnerInstanceID = "FACTION1" };

        _fleet.AddChild(_capitalShip1);
        _fleet.AddChild(_capitalShip2);
        _capitalShip1.AddOfficer(officer1);
        _capitalShip1.AddOfficer(officer2);
        _capitalShip2.AddOfficer(officer3);

        IEnumerable<Officer> officers = _fleet.GetOfficers();

        CollectionAssert.AreEquivalent(
            new Officer[] { officer1, officer2, officer3 },
            officers,
            "Should return all officers from all capital ships"
        );
    }

    [Test]
    public void GetOfficers_WhenNoOfficers_ReturnsEmpty()
    {
        _fleet.AddChild(_capitalShip1);

        IEnumerable<Officer> officers = _fleet.GetOfficers();

        Assert.IsEmpty(officers, "Should return empty collection when no officers");
    }

    [Test]
    public void AddChild_WithOfficer_ThrowsSceneAccessException()
    {
        Officer officer = new Officer { OwnerInstanceID = "FACTION1" };

        _fleet.AddChild(_capitalShip1);

        Assert.Throws<SceneAccessException>(() => _fleet.AddChild(officer));
    }

    [Test]
    public void AddChild_WithStarfighter_ThrowsSceneAccessException()
    {
        Starfighter sf = new Starfighter { OwnerInstanceID = "FACTION1" };

        _fleet.AddChild(_capitalShip1);

        Assert.Throws<SceneAccessException>(() => _fleet.AddChild(sf));
    }

    [Test]
    public void AddChild_WithRegiment_ThrowsSceneAccessException()
    {
        Regiment reg = new Regiment { OwnerInstanceID = "FACTION1" };

        _fleet.AddChild(_capitalShip1);

        Assert.Throws<SceneAccessException>(() => _fleet.AddChild(reg));
    }
}
