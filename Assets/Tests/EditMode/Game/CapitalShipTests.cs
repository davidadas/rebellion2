using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

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
            HullStrength = 100,
            DamageControl = 10,
            MaxShieldStrength = 50,
            ShieldRechargeRate = 5,
            Hyperdrive = 2,
            SublightSpeed = 15,
            Maneuverability = 8,
            WeaponRecharge = 12,
            Bombardment = 20,
            TractorBeamPower = 7,
            TractorBeamnRange = 3,
            HasGravityWell = false,
            DetectionRating = 25,
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

    [Test]
    public void GetStarfighterCapacity_ReturnsCorrectCapacity()
    {
        int capacity = capitalShip.GetStarfighterCapacity();

        Assert.AreEqual(2, capacity);
    }

    [Test]
    public void GetCurrentStarfighterCount_NoStarfighters_ReturnsZero()
    {
        int count = capitalShip.GetCurrentStarfighterCount();

        Assert.AreEqual(0, count);
    }

    [Test]
    public void GetCurrentStarfighterCount_WithStarfighters_ReturnsCorrectCount()
    {
        capitalShip.AddStarfighter(new Starfighter());
        capitalShip.AddStarfighter(new Starfighter());

        int count = capitalShip.GetCurrentStarfighterCount();

        Assert.AreEqual(2, count);
    }

    [Test]
    public void GetRegimentCapacity_ReturnsCorrectCapacity()
    {
        int capacity = capitalShip.GetRegimentCapacity();

        Assert.AreEqual(3, capacity);
    }

    [Test]
    public void GetCurrentRegimentCount_NoRegiments_ReturnsZero()
    {
        int count = capitalShip.GetCurrentRegimentCount();

        Assert.AreEqual(0, count);
    }

    [Test]
    public void GetCurrentRegimentCount_WithRegiments_ReturnsCorrectCount()
    {
        capitalShip.AddRegiment(new Regiment());
        capitalShip.AddRegiment(new Regiment());

        int count = capitalShip.GetCurrentRegimentCount();

        Assert.AreEqual(2, count);
    }

    [Test]
    public void PrimaryWeapons_InitializedWithCorrectTypes()
    {
        Assert.IsTrue(capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.Turbolaser));
        Assert.IsTrue(capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.IonCannon));
        Assert.IsTrue(capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.LaserCannon));
    }

    [Test]
    public void PrimaryWeapons_InitializedWithCorrectArraySizes()
    {
        Assert.AreEqual(5, capitalShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser].Length);
        Assert.AreEqual(5, capitalShip.PrimaryWeapons[PrimaryWeaponType.IonCannon].Length);
        Assert.AreEqual(5, capitalShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon].Length);
    }

    [Test]
    public void WeaponRecharge_ReturnsCorrectValue()
    {
        Assert.AreEqual(12, capitalShip.WeaponRecharge);
    }

    [Test]
    public void Bombardment_ReturnsCorrectValue()
    {
        Assert.AreEqual(20, capitalShip.Bombardment);
    }

    [Test]
    public void HullStrength_ReturnsCorrectValue()
    {
        Assert.AreEqual(100, capitalShip.HullStrength);
    }

    [Test]
    public void DamageControl_ReturnsCorrectValue()
    {
        Assert.AreEqual(10, capitalShip.DamageControl);
    }

    [Test]
    public void MaxShieldStrength_ReturnsCorrectValue()
    {
        Assert.AreEqual(50, capitalShip.MaxShieldStrength);
    }

    [Test]
    public void ShieldRechargeRate_ReturnsCorrectValue()
    {
        Assert.AreEqual(5, capitalShip.ShieldRechargeRate);
    }

    [Test]
    public void Hyperdrive_ReturnsCorrectValue()
    {
        Assert.AreEqual(2, capitalShip.Hyperdrive);
    }

    [Test]
    public void SublightSpeed_ReturnsCorrectValue()
    {
        Assert.AreEqual(15, capitalShip.SublightSpeed);
    }

    [Test]
    public void Maneuverability_ReturnsCorrectValue()
    {
        Assert.AreEqual(8, capitalShip.Maneuverability);
    }

    [Test]
    public void TractorBeamPower_ReturnsCorrectValue()
    {
        Assert.AreEqual(7, capitalShip.TractorBeamPower);
    }

    [Test]
    public void TractorBeamRange_ReturnsCorrectValue()
    {
        Assert.AreEqual(3, capitalShip.TractorBeamnRange);
    }

    [Test]
    public void HasGravityWell_ReturnsCorrectValue()
    {
        Assert.IsFalse(capitalShip.HasGravityWell);
    }

    [Test]
    public void DetectionRating_ReturnsCorrectValue()
    {
        Assert.AreEqual(25, capitalShip.DetectionRating);
    }
}
