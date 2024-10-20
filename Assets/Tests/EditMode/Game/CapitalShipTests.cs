using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class CapitalShipTests
{
    private CapitalShip capitalShip;

    [SetUp]
    public void Setup()
    {
        // Initialize a new instance of CapitalShip for each test with defined capacities and owner.
        capitalShip = new CapitalShip
        {
            StarfighterCapacity = 2,
            RegimentCapacity = 3,
            OwnerTypeID = "FNALL1"
        };
    }

    [Test]
    public void TestAddStarfighter()
    {
        // Create a starfighter and add it to the capital ship.
        Starfighter starfighter = new Starfighter();
        capitalShip.AddStarfighter(starfighter);

        // Ensure the starfighter is added to the capital ship's list.
        Assert.Contains(starfighter, capitalShip.Starfighters, "Starfighter should be added to the capital ship.");
    }

    [Test]
    public void TestAddStarfighterExceedsCapacity()
    {
        // Add two starfighters within capacity.
        Starfighter starfighter1 = new Starfighter();
        Starfighter starfighter2 = new Starfighter();
        capitalShip.AddStarfighter(starfighter1);
        capitalShip.AddStarfighter(starfighter2);

        // Attempt to add a third starfighter, which should exceed capacity and throw an exception.
        Starfighter starfighter3 = new Starfighter();
        Assert.Throws<GameException>(() => capitalShip.AddStarfighter(starfighter3), 
            "Adding starfighters beyond capacity should throw an exception.");
    }

    [Test]
    public void TestAddRegiment()
    {
        // Create a regiment and add it to the capital ship.
        Regiment regiment = new Regiment();
        capitalShip.AddRegiment(regiment);

        // Ensure the regiment is added to the capital ship's list.
        Assert.Contains(regiment, capitalShip.Regiments, "Regiment should be added to the capital ship.");
    }

    [Test]
    public void TestAddRegimentExceedsCapacity()
    {
        // Add regiments to reach the capacity limit.
        Regiment regiment1 = new Regiment();
        Regiment regiment2 = new Regiment();
        Regiment regiment3 = new Regiment();
        capitalShip.AddRegiment(regiment1);
        capitalShip.AddRegiment(regiment2);
        capitalShip.AddRegiment(regiment3);

        // Attempt to add another regiment, which should exceed the capacity and throw an exception.
        Regiment regiment4 = new Regiment();
        Assert.Throws<GameException>(() => capitalShip.AddRegiment(regiment4), 
            "Adding regiments beyond capacity should throw an exception.");
    }

    [Test]
    public void TestAddOfficer()
    {
        // Create an officer and add it to the capital ship.
        Officer officer = new Officer { OwnerTypeID = "FNALL1" };
        capitalShip.AddOfficer(officer);

        // Ensure the officer is added to the capital ship's list.
        Assert.Contains(officer, capitalShip.Officers, "Officer should be added to the capital ship.");
    }

    [Test]
    public void TestAddOfficerInvalidOwner()
    {
        // Attempt to add an officer with an invalid owner, which should throw an exception.
        Officer officer = new Officer { OwnerTypeID = "INVALID" };
        Assert.Throws<SceneAccessException>(() => capitalShip.AddOfficer(officer),
            "Adding an officer with an invalid owner should throw a SceneAccessException.");
    }

    [Test]
    public void TestRemoveStarfighter()
    {
        // Add and then remove a starfighter from the capital ship.
        Starfighter starfighter = new Starfighter();
        capitalShip.AddStarfighter(starfighter);
        capitalShip.RemoveChild(starfighter);

        // Ensure the starfighter is removed from the capital ship's list.
        Assert.IsFalse(capitalShip.Starfighters.Contains(starfighter), 
            "Starfighter should be removed from the capital ship.");
    }

    [Test]
    public void TestRemoveRegiment()
    {
        // Add and then remove a regiment from the capital ship.
        Regiment regiment = new Regiment();
        capitalShip.AddRegiment(regiment);
        capitalShip.RemoveChild(regiment);

        // Ensure the regiment is removed from the capital ship's list.
        Assert.IsFalse(capitalShip.Regiments.Contains(regiment), 
            "Regiment should be removed from the capital ship.");
    }

    [Test]
    public void TestRemoveOfficer()
    {
        // Add and then remove an officer from the capital ship.
        Officer officer = new Officer { OwnerTypeID = "FNALL1" };
        capitalShip.AddOfficer(officer);
        capitalShip.RemoveChild(officer);

        // Ensure the officer is removed from the capital ship's list.
        Assert.IsFalse(capitalShip.Officers.Contains(officer), 
            "Officer should be removed from the capital ship.");
    }

    [Test]
    public void TestGetChildren()
    {
        // Add various child nodes to the capital ship.
        Officer officer = new Officer { OwnerTypeID = "FNALL1" };
        Starfighter starfighter = new Starfighter();
        Regiment regiment = new Regiment();
        capitalShip.AddOfficer(officer);
        capitalShip.AddStarfighter(starfighter);
        capitalShip.AddRegiment(regiment);

        // Retrieve the children of the capital ship.
        IEnumerable<SceneNode> children = capitalShip.GetChildren();

        // Ensure all added child nodes are returned as children.
        List<SceneNode> expectedChildren = new List<SceneNode> { officer, starfighter, regiment };
        CollectionAssert.AreEquivalent(expectedChildren, children, "CapitalShip should return correct children.");
    }

    [Test]
    public void TestSerializeAndDeserialize()
    {
        // Add components to the CapitalShip.
        Officer officer = new Officer { OwnerTypeID = "FNALL1" };
        Starfighter starfighter = new Starfighter();
        Regiment regiment = new Regiment();
        capitalShip.AddOfficer(officer);
        capitalShip.AddStarfighter(starfighter);
        capitalShip.AddRegiment(regiment);

        // Serialize the CapitalShip object to a string.
        string serialized = SerializationHelper.Serialize(capitalShip);

        // Deserialize the string back to a CapitalShip object.
        CapitalShip deserialized = SerializationHelper.Deserialize<CapitalShip>(serialized);

        // Check that the deserialized object contains the same properties and children.
        Assert.AreEqual(capitalShip.StarfighterCapacity, deserialized.StarfighterCapacity, "StarfighterCapacity should be correctly deserialized.");
        Assert.AreEqual(capitalShip.RegimentCapacity, deserialized.RegimentCapacity, "RegimentCapacity should be correctly deserialized.");
        Assert.AreEqual(capitalShip.OwnerTypeID, deserialized.OwnerTypeID, "OwnerTypeID should be correctly deserialized.");
        Assert.AreEqual(capitalShip.Officers.Count, deserialized.Officers.Count, "Officers should be correctly deserialized.");
        Assert.AreEqual(capitalShip.Starfighters.Count, deserialized.Starfighters.Count, "Starfighters should be correctly deserialized.");
        Assert.AreEqual(capitalShip.Regiments.Count, deserialized.Regiments.Count, "Regiments should be correctly deserialized.");
    }
}
