using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

[TestFixture]
public class FleetTests
{
    private Fleet fleet;
    private CapitalShip capitalShip1;
    private CapitalShip capitalShip2;

    [SetUp]
    public void SetUp()
    {
        fleet = new Fleet
        {
            InstanceID = "FLEET1",
            OwnerInstanceID = "FACTION1",
            Movement = null,
        };

        capitalShip1 = new CapitalShip
        {
            InstanceID = "SHIP1",
            OwnerInstanceID = "FACTION1",
            StarfighterCapacity = 5,
            RegimentCapacity = 3,
        };

        capitalShip2 = new CapitalShip
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
        fleet.AddChild(capitalShip1);

        Assert.Contains(capitalShip1, fleet.CapitalShips);
    }

    [Test]
    public void AddChild_WithInvalidOwner_ThrowsException()
    {
        CapitalShip invalidShip = new CapitalShip { OwnerInstanceID = "INVALID" };

        Assert.Throws<SceneAccessException>(() => fleet.AddChild(invalidShip));
    }

    [Test]
    public void RemoveChild_RemovesCapitalShip()
    {
        fleet.AddChild(capitalShip1);

        fleet.RemoveChild(capitalShip1);

        Assert.IsFalse(fleet.CapitalShips.Contains(capitalShip1));
    }

    [Test]
    public void GetChildren_ReturnsAllCapitalShips()
    {
        fleet.AddChild(capitalShip1);
        fleet.AddChild(capitalShip2);

        IEnumerable<ISceneNode> children = fleet.GetChildren();

        CollectionAssert.AreEquivalent(
            new ISceneNode[] { capitalShip1, capitalShip2 },
            children,
            "Fleet should return correct children."
        );
    }

    [Test]
    public void GetStarfighterCapacity_ReturnsSum()
    {
        fleet.AddChild(capitalShip1);
        fleet.AddChild(capitalShip2);

        int capacity = fleet.GetStarfighterCapacity();

        Assert.AreEqual(
            13,
            capacity,
            "Should return sum of all capital ship starfighter capacities"
        );
    }

    [Test]
    public void GetRegimentCapacity_ReturnsSum()
    {
        fleet.AddChild(capitalShip1);
        fleet.AddChild(capitalShip2);

        int capacity = fleet.GetRegimentCapacity();

        Assert.AreEqual(7, capacity, "Should return sum of all capital ship regiment capacities");
    }

    [Test]
    public void GetCurrentStarfighterCount_ReturnsSum()
    {
        Starfighter starfighter1 = new Starfighter();
        Starfighter starfighter2 = new Starfighter();

        fleet.AddChild(capitalShip1);
        capitalShip1.AddStarfighter(starfighter1);
        capitalShip1.AddStarfighter(starfighter2);

        int count = fleet.GetCurrentStarfighterCount();

        Assert.AreEqual(2, count, "Should return total starfighters across all capital ships");
    }

    [Test]
    public void GetExcessStarfighterCapacity_ReturnsCorrectValue()
    {
        Starfighter starfighter = new Starfighter();

        fleet.AddChild(capitalShip1);
        capitalShip1.AddStarfighter(starfighter);

        int excess = fleet.GetExcessStarfighterCapacity();

        Assert.AreEqual(4, excess, "Should return excess capacity (5 - 1 = 4)");
    }

    [Test]
    public void GetCurrentRegimentCount_ReturnsSum()
    {
        Regiment regiment1 = new Regiment();
        Regiment regiment2 = new Regiment();

        fleet.AddChild(capitalShip1);
        capitalShip1.AddRegiment(regiment1);
        capitalShip1.AddRegiment(regiment2);

        int count = fleet.GetCurrentRegimentCount();

        Assert.AreEqual(2, count, "Should return total regiments across all capital ships");
    }

    [Test]
    public void GetExcessRegimentCapacity_ReturnsCorrectValue()
    {
        Regiment regiment = new Regiment();

        fleet.AddChild(capitalShip1);
        capitalShip1.AddRegiment(regiment);

        int excess = fleet.GetExcessRegimentCapacity();

        Assert.AreEqual(2, excess, "Should return excess capacity (3 - 1 = 2)");
    }

    [Test]
    public void IsMovable_WhenIdle_ReturnsTrue()
    {
        fleet.Movement = null;

        bool isMovable = fleet.IsMovable();

        Assert.IsTrue(isMovable, "Fleet should be movable when idle");
    }

    [Test]
    public void IsMovable_WhenInTransit_ReturnsFalse()
    {
        fleet.Movement = new MovementState();

        bool isMovable = fleet.IsMovable();

        Assert.IsFalse(isMovable, "Fleet should not be movable when in transit");
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        fleet.AddChild(capitalShip1);
        string serialized = SerializationHelper.Serialize(fleet);
        Fleet deserialized = SerializationHelper.Deserialize<Fleet>(serialized);

        Assert.AreEqual(
            fleet.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            fleet.OwnerInstanceID,
            deserialized.OwnerInstanceID,
            "OwnerInstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            fleet.Movement,
            deserialized.Movement,
            "MovementStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            fleet.GetPosition().X,
            deserialized.GetPosition().X,
            "PositionX should be correctly deserialized."
        );
        Assert.AreEqual(
            fleet.GetPosition().Y,
            deserialized.GetPosition().Y,
            "PositionY should be correctly deserialized."
        );
        Assert.AreEqual(
            fleet.CapitalShips.Count,
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

        fleet.AddChild(capitalShip1);
        fleet.AddChild(capitalShip2);
        capitalShip1.AddStarfighter(starfighter1);
        capitalShip1.AddStarfighter(starfighter2);
        capitalShip2.AddStarfighter(starfighter3);

        IEnumerable<Starfighter> starfighters = fleet.GetStarfighters();

        CollectionAssert.AreEquivalent(
            new Starfighter[] { starfighter1, starfighter2, starfighter3 },
            starfighters,
            "Should return all starfighters from all capital ships"
        );
    }

    [Test]
    public void GetStarfighters_WhenNoStarfighters_ReturnsEmpty()
    {
        fleet.AddChild(capitalShip1);

        IEnumerable<Starfighter> starfighters = fleet.GetStarfighters();

        Assert.IsEmpty(starfighters, "Should return empty collection when no starfighters");
    }

    [Test]
    public void GetRegiments_ReturnsAllRegimentsAcrossFleet()
    {
        Regiment regiment1 = new Regiment();
        Regiment regiment2 = new Regiment();
        Regiment regiment3 = new Regiment();

        fleet.AddChild(capitalShip1);
        fleet.AddChild(capitalShip2);
        capitalShip1.AddRegiment(regiment1);
        capitalShip1.AddRegiment(regiment2);
        capitalShip2.AddRegiment(regiment3);

        IEnumerable<Regiment> regiments = fleet.GetRegiments();

        CollectionAssert.AreEquivalent(
            new Regiment[] { regiment1, regiment2, regiment3 },
            regiments,
            "Should return all regiments from all capital ships"
        );
    }

    [Test]
    public void GetRegiments_WhenNoRegiments_ReturnsEmpty()
    {
        fleet.AddChild(capitalShip1);

        IEnumerable<Regiment> regiments = fleet.GetRegiments();

        Assert.IsEmpty(regiments, "Should return empty collection when no regiments");
    }

    [Test]
    public void GetOfficers_ReturnsAllOfficersAcrossFleet()
    {
        Officer officer1 = new Officer { OwnerInstanceID = "FACTION1" };
        Officer officer2 = new Officer { OwnerInstanceID = "FACTION1" };
        Officer officer3 = new Officer { OwnerInstanceID = "FACTION1" };

        fleet.AddChild(capitalShip1);
        fleet.AddChild(capitalShip2);
        capitalShip1.AddOfficer(officer1);
        capitalShip1.AddOfficer(officer2);
        capitalShip2.AddOfficer(officer3);

        IEnumerable<Officer> officers = fleet.GetOfficers();

        CollectionAssert.AreEquivalent(
            new Officer[] { officer1, officer2, officer3 },
            officers,
            "Should return all officers from all capital ships"
        );
    }

    [Test]
    public void GetOfficers_WhenNoOfficers_ReturnsEmpty()
    {
        fleet.AddChild(capitalShip1);

        IEnumerable<Officer> officers = fleet.GetOfficers();

        Assert.IsEmpty(officers, "Should return empty collection when no officers");
    }

    [Test]
    public void AddChild_WithOfficer_ThrowsSceneAccessException()
    {
        Officer officer = new Officer { OwnerInstanceID = "FACTION1" };

        fleet.AddChild(capitalShip1);

        Assert.Throws<SceneAccessException>(() => fleet.AddChild(officer));
    }

    [Test]
    public void AddChild_WithStarfighter_ThrowsSceneAccessException()
    {
        Starfighter sf = new Starfighter { OwnerInstanceID = "FACTION1" };

        fleet.AddChild(capitalShip1);

        Assert.Throws<SceneAccessException>(() => fleet.AddChild(sf));
    }

    [Test]
    public void AddChild_WithRegiment_ThrowsSceneAccessException()
    {
        Regiment reg = new Regiment { OwnerInstanceID = "FACTION1" };

        fleet.AddChild(capitalShip1);

        Assert.Throws<SceneAccessException>(() => fleet.AddChild(reg));
    }
}
