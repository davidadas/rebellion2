using System;
using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class CapitalShipTests
{
    private CapitalShip capitalShip;

    [SetUp]
    public void Setup()
    {
        capitalShip = new CapitalShip
        {
            StarfighterCapacity = 2,
            RegimentCapacity = 3,
            OwnerInstanceID = "FNALL1",
        };
    }

    [Test]
    public void AddStarfighter_WithinCapacity_AddsStarfighter()
    {
        Starfighter starfighter = new Starfighter();

        capitalShip.AddStarfighter(starfighter);

        Assert.Contains(starfighter, capitalShip.Starfighters);
    }

    [Test]
    public void AddStarfighter_ExceedsCapacity_ThrowsException()
    {
        capitalShip.AddStarfighter(new Starfighter());
        capitalShip.AddStarfighter(new Starfighter());

        Assert.Throws<GameException>(() => capitalShip.AddStarfighter(new Starfighter()));
    }

    [Test]
    public void AddRegiment_WithinCapacity_AddsRegiment()
    {
        Regiment regiment = new Regiment();

        capitalShip.AddRegiment(regiment);

        Assert.Contains(regiment, capitalShip.Regiments);
    }

    [Test]
    public void AddRegiment_ExceedsCapacity_ThrowsException()
    {
        capitalShip.AddRegiment(new Regiment());
        capitalShip.AddRegiment(new Regiment());
        capitalShip.AddRegiment(new Regiment());

        Assert.Throws<GameException>(() => capitalShip.AddRegiment(new Regiment()));
    }

    [Test]
    public void AddOfficer_ValidOwner_AddsOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };

        capitalShip.AddOfficer(officer);

        Assert.Contains(officer, capitalShip.Officers);
    }

    [Test]
    public void AddOfficer_InvalidOwner_ThrowsException()
    {
        Officer officer = new Officer { OwnerInstanceID = "INVALID" };

        Assert.Throws<SceneAccessException>(() => capitalShip.AddOfficer(officer));
    }

    [Test]
    public void RemoveStarfighter_RemovesStarfighter()
    {
        Starfighter starfighter = new Starfighter();
        capitalShip.AddStarfighter(starfighter);

        capitalShip.RemoveChild(starfighter);

        Assert.IsFalse(capitalShip.Starfighters.Contains(starfighter));
    }

    [Test]
    public void RemoveRegiment_RemovesRegiment()
    {
        Regiment regiment = new Regiment();
        capitalShip.AddRegiment(regiment);

        capitalShip.RemoveChild(regiment);

        Assert.IsFalse(capitalShip.Regiments.Contains(regiment));
    }

    [Test]
    public void RemoveOfficer_RemovesOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        capitalShip.AddOfficer(officer);

        capitalShip.RemoveChild(officer);

        Assert.IsFalse(capitalShip.Officers.Contains(officer));
    }

    [Test]
    public void GetChildren_ReturnsAllChildNodes()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        Starfighter starfighter = new Starfighter();
        Regiment regiment = new Regiment();

        capitalShip.AddOfficer(officer);
        capitalShip.AddStarfighter(starfighter);
        capitalShip.AddRegiment(regiment);

        IEnumerable<ISceneNode> children = capitalShip.GetChildren();

        CollectionAssert.AreEquivalent(
            new ISceneNode[] { officer, starfighter, regiment },
            children,
            "CapitalShip should return correct children."
        );
    }

    [Test]
    public void AddChild_AddsStarfighter()
    {
        Starfighter starfighter = new Starfighter();

        capitalShip.AddChild(starfighter);

        Assert.Contains(starfighter, capitalShip.Starfighters);
    }

    [Test]
    public void AddChild_AddsRegiment()
    {
        Regiment regiment = new Regiment();

        capitalShip.AddChild(regiment);

        Assert.Contains(regiment, capitalShip.Regiments);
    }

    [Test]
    public void AddChild_AddsOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };

        capitalShip.AddChild(officer);

        Assert.Contains(officer, capitalShip.Officers);
    }

    [Test]
    public void AddChild_InvalidOwner_ThrowsException()
    {
        Officer officer = new Officer { OwnerInstanceID = "INVALID" };

        Assert.Throws<SceneAccessException>(() => capitalShip.AddChild(officer));
    }

    [Test]
    public void RemoveChild_RemovesStarfighter()
    {
        Starfighter starfighter = new Starfighter();
        capitalShip.AddChild(starfighter);

        capitalShip.RemoveChild(starfighter);

        Assert.IsFalse(capitalShip.Starfighters.Contains(starfighter));
    }

    [Test]
    public void RemoveChild_RemovesRegiment()
    {
        Regiment regiment = new Regiment();
        capitalShip.AddChild(regiment);

        capitalShip.RemoveChild(regiment);

        Assert.IsFalse(capitalShip.Regiments.Contains(regiment));
    }

    [Test]
    public void RemoveChild_RemovesOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        capitalShip.AddChild(officer);

        capitalShip.RemoveChild(officer);

        Assert.IsFalse(capitalShip.Officers.Contains(officer));
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        Starfighter starfighter = new Starfighter();
        Regiment regiment = new Regiment();

        capitalShip.AddOfficer(officer);
        capitalShip.AddStarfighter(starfighter);
        capitalShip.AddRegiment(regiment);

        string serialized = SerializationHelper.Serialize(capitalShip);
        CapitalShip deserialized = SerializationHelper.Deserialize<CapitalShip>(serialized);

        Assert.AreEqual(
            capitalShip.StarfighterCapacity,
            deserialized.StarfighterCapacity,
            "StarfighterCapacity should be correctly deserialized."
        );
        Assert.AreEqual(
            capitalShip.RegimentCapacity,
            deserialized.RegimentCapacity,
            "RegimentCapacity should be correctly deserialized."
        );
        Assert.AreEqual(
            capitalShip.OwnerInstanceID,
            deserialized.OwnerInstanceID,
            "OwnerInstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            capitalShip.Officers.Count,
            deserialized.Officers.Count,
            "Officers should be correctly deserialized."
        );
        Assert.AreEqual(
            capitalShip.Starfighters.Count,
            deserialized.Starfighters.Count,
            "Starfighters should be correctly deserialized."
        );
        Assert.AreEqual(
            capitalShip.Regiments.Count,
            deserialized.Regiments.Count,
            "Regiments should be correctly deserialized."
        );
    }
}
