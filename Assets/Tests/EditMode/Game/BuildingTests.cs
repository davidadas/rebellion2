using System;
using System.Drawing;
using NUnit.Framework;

[TestFixture]
public class BuildingTests
{
    [Test]
    public void ConstructionInfo_SetValues_ReturnsCorrectValues()
    {
        var building = new Building
        {
            ConstructionCost = 100,
            MaintenanceCost = 50,
            BaseBuildSpeed = 10,
            RequiredResearchLevel = 3,
        };

        Assert.AreEqual(100, building.ConstructionCost);
        Assert.AreEqual(50, building.MaintenanceCost);
        Assert.AreEqual(10, building.BaseBuildSpeed);
        Assert.AreEqual(3, building.RequiredResearchLevel);
    }

    [Test]
    public void GetBuildingType_ValidBuildingType_ReturnsCorrectType()
    {
        var building = new Building { BuildingType = BuildingType.Mine };

        Assert.AreEqual(BuildingType.Mine, building.GetBuildingType());
    }

    [Test]
    public void GetBuildingSlot_ValidSlot_ReturnsCorrectSlot()
    {
        var building = new Building { BuildingSlot = BuildingSlot.Orbit };

        Assert.AreEqual(BuildingSlot.Orbit, building.GetBuildingSlot());
    }

    [Test]
    public void SetManufacturingStatus_ValidStatus_UpdatesSuccessfully()
    {
        var building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

        building.SetManufacturingStatus(ManufacturingStatus.Complete);

        Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
    }

    [Test]
    public void SetManufacturingStatus_InvalidTransition_ThrowsException()
    {
        var building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

        Assert.Throws<GameStateException>(
            () => building.SetManufacturingStatus(ManufacturingStatus.Building)
        );
    }

    [Test]
    public void IsMovable_IdleStatus_ReturnsTrue()
    {
        var building = new Building { MovementStatus = MovementStatus.Idle };

        Assert.IsTrue(building.IsMovable());
    }

    [Test]
    public void IsMovable_InTransitStatus_ReturnsFalse()
    {
        var building = new Building { MovementStatus = MovementStatus.InTransit };

        Assert.IsFalse(building.IsMovable());
    }

    [Test]
    public void SerializeDeserialize_BuildingObject_RetainsProperties()
    {
        var building = new Building
        {
            ConstructionCost = 100,
            MaintenanceCost = 50,
            BaseBuildSpeed = 10,
            RequiredResearchLevel = 3,
            BuildingType = BuildingType.Mine,
            BuildingSlot = BuildingSlot.Ground,
            ProcessRate = 20,
            Bombardment = 5,
            WeaponStrength = 10,
            ShieldStrength = 15,
            ProducerOwnerID = "Faction1",
            ManufacturingProgress = 50,
            ManufacturingStatus = ManufacturingStatus.Building,
            ProductionType = ManufacturingType.Building,
            PositionX = 10,
            PositionY = 20,
            MovementStatus = MovementStatus.Idle,
        };

        string xml = SerializationHelper.Serialize(building);
        var deserializedBuilding = SerializationHelper.Deserialize<Building>(xml);

        Assert.AreEqual(building.ConstructionCost, deserializedBuilding.ConstructionCost);
        Assert.AreEqual(building.MaintenanceCost, deserializedBuilding.MaintenanceCost);
        Assert.AreEqual(building.BaseBuildSpeed, deserializedBuilding.BaseBuildSpeed);
        Assert.AreEqual(building.RequiredResearchLevel, deserializedBuilding.RequiredResearchLevel);
        Assert.AreEqual(building.BuildingType, deserializedBuilding.BuildingType);
        Assert.AreEqual(building.BuildingSlot, deserializedBuilding.BuildingSlot);
        Assert.AreEqual(building.ProcessRate, deserializedBuilding.ProcessRate);
        Assert.AreEqual(building.Bombardment, deserializedBuilding.Bombardment);
        Assert.AreEqual(building.WeaponStrength, deserializedBuilding.WeaponStrength);
        Assert.AreEqual(building.ShieldStrength, deserializedBuilding.ShieldStrength);
        Assert.AreEqual(building.ProducerOwnerID, deserializedBuilding.ProducerOwnerID);
        Assert.AreEqual(building.ManufacturingProgress, deserializedBuilding.ManufacturingProgress);
        Assert.AreEqual(building.ManufacturingStatus, deserializedBuilding.ManufacturingStatus);
        Assert.AreEqual(building.ProductionType, deserializedBuilding.ProductionType);
        Assert.AreEqual(building.PositionX, deserializedBuilding.PositionX);
        Assert.AreEqual(building.PositionY, deserializedBuilding.PositionY);
        Assert.AreEqual(building.MovementStatus, deserializedBuilding.MovementStatus);
    }
}
