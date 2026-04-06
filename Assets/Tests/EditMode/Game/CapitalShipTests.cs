using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

[TestFixture]
public class CapitalShipTests
{
    private CapitalShip _capitalShip;

    [SetUp]
    public void Setup()
    {
        _capitalShip = new CapitalShip
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

        _capitalShip.AddStarfighter(starfighter);

        Assert.Contains(starfighter, _capitalShip.Starfighters);
    }

    [Test]
    public void AddStarfighter_ExceedsCapacity_ThrowsException()
    {
        _capitalShip.AddStarfighter(new Starfighter());
        _capitalShip.AddStarfighter(new Starfighter());

        Assert.Throws<InvalidOperationException>(() =>
            _capitalShip.AddStarfighter(new Starfighter())
        );
    }

    [Test]
    public void AddRegiment_WithinCapacity_AddsRegiment()
    {
        Regiment regiment = new Regiment();

        _capitalShip.AddRegiment(regiment);

        Assert.Contains(regiment, _capitalShip.Regiments);
    }

    [Test]
    public void AddRegiment_ExceedsCapacity_ThrowsException()
    {
        _capitalShip.AddRegiment(new Regiment());
        _capitalShip.AddRegiment(new Regiment());
        _capitalShip.AddRegiment(new Regiment());

        Assert.Throws<InvalidOperationException>(() => _capitalShip.AddRegiment(new Regiment()));
    }

    [Test]
    public void AddOfficer_ValidOwner_AddsOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };

        _capitalShip.AddOfficer(officer);

        Assert.Contains(officer, _capitalShip.Officers);
    }

    [Test]
    public void AddOfficer_InvalidOwner_ThrowsException()
    {
        Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = false };

        Assert.Throws<SceneAccessException>(() => _capitalShip.AddOfficer(officer));
    }

    [Test]
    public void AddOfficer_CapturedEnemy_AddsOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = true };

        _capitalShip.AddOfficer(officer);

        Assert.Contains(officer, _capitalShip.Officers);
    }

    [Test]
    public void CanAcceptChild_CapturedEnemyOfficer_ReturnsTrue()
    {
        Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = true };

        Assert.IsTrue(_capitalShip.CanAcceptChild(officer));
    }

    [Test]
    public void CanAcceptChild_UncapturedEnemyOfficer_ReturnsFalse()
    {
        Officer officer = new Officer { OwnerInstanceID = "INVALID", IsCaptured = false };

        Assert.IsFalse(_capitalShip.CanAcceptChild(officer));
    }

    [Test]
    public void RemoveStarfighter_RemovesStarfighter()
    {
        Starfighter starfighter = new Starfighter();
        _capitalShip.AddStarfighter(starfighter);

        _capitalShip.RemoveChild(starfighter);

        Assert.IsFalse(_capitalShip.Starfighters.Contains(starfighter));
    }

    [Test]
    public void RemoveRegiment_RemovesRegiment()
    {
        Regiment regiment = new Regiment();
        _capitalShip.AddRegiment(regiment);

        _capitalShip.RemoveChild(regiment);

        Assert.IsFalse(_capitalShip.Regiments.Contains(regiment));
    }

    [Test]
    public void RemoveOfficer_RemovesOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        _capitalShip.AddOfficer(officer);

        _capitalShip.RemoveChild(officer);

        Assert.IsFalse(_capitalShip.Officers.Contains(officer));
    }

    [Test]
    public void GetChildren_ReturnsAllChildNodes()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        Starfighter starfighter = new Starfighter();
        Regiment regiment = new Regiment();

        _capitalShip.AddOfficer(officer);
        _capitalShip.AddStarfighter(starfighter);
        _capitalShip.AddRegiment(regiment);

        IEnumerable<ISceneNode> children = _capitalShip.GetChildren();

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

        _capitalShip.AddChild(starfighter);

        Assert.Contains(starfighter, _capitalShip.Starfighters);
    }

    [Test]
    public void AddChild_AddsRegiment()
    {
        Regiment regiment = new Regiment();

        _capitalShip.AddChild(regiment);

        Assert.Contains(regiment, _capitalShip.Regiments);
    }

    [Test]
    public void AddChild_AddsOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };

        _capitalShip.AddChild(officer);

        Assert.Contains(officer, _capitalShip.Officers);
    }

    [Test]
    public void AddChild_InvalidOwner_ThrowsException()
    {
        Officer officer = new Officer { OwnerInstanceID = "INVALID" };

        Assert.Throws<SceneAccessException>(() => _capitalShip.AddChild(officer));
    }

    [Test]
    public void RemoveChild_RemovesStarfighter()
    {
        Starfighter starfighter = new Starfighter();
        _capitalShip.AddChild(starfighter);

        _capitalShip.RemoveChild(starfighter);

        Assert.IsFalse(_capitalShip.Starfighters.Contains(starfighter));
    }

    [Test]
    public void RemoveChild_RemovesRegiment()
    {
        Regiment regiment = new Regiment();
        _capitalShip.AddChild(regiment);

        _capitalShip.RemoveChild(regiment);

        Assert.IsFalse(_capitalShip.Regiments.Contains(regiment));
    }

    [Test]
    public void RemoveChild_RemovesOfficer()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        _capitalShip.AddChild(officer);

        _capitalShip.RemoveChild(officer);

        Assert.IsFalse(_capitalShip.Officers.Contains(officer));
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        Starfighter starfighter = new Starfighter();
        Regiment regiment = new Regiment();

        _capitalShip.AddOfficer(officer);
        _capitalShip.AddStarfighter(starfighter);
        _capitalShip.AddRegiment(regiment);

        string serialized = SerializationHelper.Serialize(capitalShip);
        CapitalShip deserialized = SerializationHelper.Deserialize<CapitalShip>(serialized);

        Assert.AreEqual(
            _capitalShip.StarfighterCapacity,
            deserialized.StarfighterCapacity,
            "StarfighterCapacity should be correctly deserialized."
        );
        Assert.AreEqual(
            _capitalShip.RegimentCapacity,
            deserialized.RegimentCapacity,
            "RegimentCapacity should be correctly deserialized."
        );
        Assert.AreEqual(
            _capitalShip.OwnerInstanceID,
            deserialized.OwnerInstanceID,
            "OwnerInstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            _capitalShip.Officers.Count,
            deserialized.Officers.Count,
            "Officers should be correctly deserialized."
        );
        Assert.AreEqual(
            _capitalShip.Starfighters.Count,
            deserialized.Starfighters.Count,
            "Starfighters should be correctly deserialized."
        );
        Assert.AreEqual(
            _capitalShip.Regiments.Count,
            deserialized.Regiments.Count,
            "Regiments should be correctly deserialized."
        );
    }

    [Test]
    public void SetManufacturingStatus_BuildingToComplete_UpdatesSuccessfully()
    {
        _capitalShip.ManufacturingStatus = ManufacturingStatus.Building;

        ((IManufacturable)capitalShip).SetManufacturingStatus(ManufacturingStatus.Complete);

        Assert.AreEqual(ManufacturingStatus.Complete, _capitalShip.ManufacturingStatus);
    }

    [Test]
    public void SetManufacturingStatus_CompleteToBuilding_ThrowsException()
    {
        _capitalShip.ManufacturingStatus = ManufacturingStatus.Complete;

        Assert.Throws<InvalidOperationException>(() =>
            ((IManufacturable)capitalShip).SetManufacturingStatus(ManufacturingStatus.Building)
        );
    }

    [Test]
    public void GetStarfighterCapacity_ReturnsCorrectCapacity()
    {
        int capacity = _capitalShip.GetStarfighterCapacity();

        Assert.AreEqual(2, capacity);
    }

    [Test]
    public void GetCurrentStarfighterCount_NoStarfighters_ReturnsZero()
    {
        int count = _capitalShip.GetCurrentStarfighterCount();

        Assert.AreEqual(0, count);
    }

    [Test]
    public void GetCurrentStarfighterCount_WithStarfighters_ReturnsCorrectCount()
    {
        _capitalShip.AddStarfighter(new Starfighter());
        _capitalShip.AddStarfighter(new Starfighter());

        int count = _capitalShip.GetCurrentStarfighterCount();

        Assert.AreEqual(2, count);
    }

    [Test]
    public void GetRegimentCapacity_ReturnsCorrectCapacity()
    {
        int capacity = _capitalShip.GetRegimentCapacity();

        Assert.AreEqual(3, capacity);
    }

    [Test]
    public void GetCurrentRegimentCount_NoRegiments_ReturnsZero()
    {
        int count = _capitalShip.GetCurrentRegimentCount();

        Assert.AreEqual(0, count);
    }

    [Test]
    public void GetCurrentRegimentCount_WithRegiments_ReturnsCorrectCount()
    {
        _capitalShip.AddRegiment(new Regiment());
        _capitalShip.AddRegiment(new Regiment());

        int count = _capitalShip.GetCurrentRegimentCount();

        Assert.AreEqual(2, count);
    }

    [Test]
    public void PrimaryWeapons_InitializedWithCorrectTypes()
    {
        Assert.IsTrue(_capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.Turbolaser));
        Assert.IsTrue(_capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.IonCannon));
        Assert.IsTrue(_capitalShip.PrimaryWeapons.ContainsKey(PrimaryWeaponType.LaserCannon));
    }

    [Test]
    public void PrimaryWeapons_InitializedWithCorrectArraySizes()
    {
        Assert.AreEqual(5, _capitalShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser].Length);
        Assert.AreEqual(5, _capitalShip.PrimaryWeapons[PrimaryWeaponType.IonCannon].Length);
        Assert.AreEqual(5, _capitalShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon].Length);
    }

    [Test]
    public void WeaponRecharge_ReturnsCorrectValue()
    {
        Assert.AreEqual(12, _capitalShip.WeaponRecharge);
    }

    [Test]
    public void Bombardment_ReturnsCorrectValue()
    {
        Assert.AreEqual(20, _capitalShip.Bombardment);
    }

    [Test]
    public void HullStrength_ReturnsCorrectValue()
    {
        Assert.AreEqual(100, _capitalShip.HullStrength);
    }

    [Test]
    public void DamageControl_ReturnsCorrectValue()
    {
        Assert.AreEqual(10, _capitalShip.DamageControl);
    }

    [Test]
    public void MaxShieldStrength_ReturnsCorrectValue()
    {
        Assert.AreEqual(50, _capitalShip.MaxShieldStrength);
    }

    [Test]
    public void ShieldRechargeRate_ReturnsCorrectValue()
    {
        Assert.AreEqual(5, _capitalShip.ShieldRechargeRate);
    }

    [Test]
    public void Hyperdrive_ReturnsCorrectValue()
    {
        Assert.AreEqual(2, _capitalShip.Hyperdrive);
    }

    [Test]
    public void SublightSpeed_ReturnsCorrectValue()
    {
        Assert.AreEqual(15, _capitalShip.SublightSpeed);
    }

    [Test]
    public void Maneuverability_ReturnsCorrectValue()
    {
        Assert.AreEqual(8, _capitalShip.Maneuverability);
    }

    [Test]
    public void TractorBeamPower_ReturnsCorrectValue()
    {
        Assert.AreEqual(7, _capitalShip.TractorBeamPower);
    }

    [Test]
    public void TractorBeamRange_ReturnsCorrectValue()
    {
        Assert.AreEqual(3, _capitalShip.TractorBeamnRange);
    }

    [Test]
    public void HasGravityWell_ReturnsCorrectValue()
    {
        Assert.IsFalse(_capitalShip.HasGravityWell);
    }

    [Test]
    public void DetectionRating_ReturnsCorrectValue()
    {
        Assert.AreEqual(25, _capitalShip.DetectionRating);
    }
}
