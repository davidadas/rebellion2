using System;
using System.Drawing;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Extensions;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class BuildingTests
    {
        [Test]
        public void ConstructionInfo_SetValues_ReturnsCorrectValues()
        {
            Building building = new Building
            {
                ConstructionCost = 100,
                MaintenanceCost = 50,
                BaseBuildSpeed = 10,
                ResearchOrder = 3,
                ResearchDifficulty = 24,
            };

            Assert.AreEqual(100, building.ConstructionCost);
            Assert.AreEqual(50, building.MaintenanceCost);
            Assert.AreEqual(10, building.BaseBuildSpeed);
            Assert.AreEqual(3, building.ResearchOrder);
            Assert.AreEqual(24, building.ResearchDifficulty);
        }

        [Test]
        public void GetBuildingType_ValidBuildingType_ReturnsCorrectType()
        {
            Building building = new Building
            {
                ConstructionCost = 100,
                MaintenanceCost = 50,
                BaseBuildSpeed = 10,
                ResearchOrder = 3,
                ResearchDifficulty = 24,
                BuildingType = BuildingType.Mine,
                ProcessRate = 20,
                Bombardment = 5,
                WeaponStrength = 10,
                ShieldStrength = 15,
                ProducerOwnerID = "Faction1",
                ManufacturingProgress = 50,
                ManufacturingStatus = ManufacturingStatus.Building,
                ProductionType = ManufacturingType.Building,
                Movement = null,
            };

            Assert.AreEqual(BuildingType.Mine, building.GetBuildingType());
        }

        [Test]
        public void SerializeAndDeserialize_PopulatedBuilding_RetainsProperties()
        {
            Building building = new Building
            {
                ConstructionCost = 100,
                MaintenanceCost = 50,
                BaseBuildSpeed = 10,
                ResearchOrder = 3,
                ResearchDifficulty = 24,
                BuildingType = BuildingType.Mine,
                ProcessRate = 20,
                Bombardment = 5,
                WeaponStrength = 10,
                ShieldStrength = 15,
                ProducerOwnerID = "Faction1",
                ManufacturingProgress = 50,
                ManufacturingStatus = ManufacturingStatus.Building,
                ProductionType = ManufacturingType.Building,
                Movement = null,
            };

            string xml = SerializationHelper.Serialize(building);
            Building deserializedBuilding = SerializationHelper.Deserialize<Building>(xml);

            Assert.AreEqual(building.ConstructionCost, deserializedBuilding.ConstructionCost);
            Assert.AreEqual(building.MaintenanceCost, deserializedBuilding.MaintenanceCost);
            Assert.AreEqual(building.BaseBuildSpeed, deserializedBuilding.BaseBuildSpeed);
            Assert.AreEqual(building.ResearchOrder, deserializedBuilding.ResearchOrder);
            Assert.AreEqual(building.ResearchDifficulty, deserializedBuilding.ResearchDifficulty);
            Assert.AreEqual(building.BuildingType, deserializedBuilding.BuildingType);
            Assert.AreEqual(building.ProcessRate, deserializedBuilding.ProcessRate);
            Assert.AreEqual(building.Bombardment, deserializedBuilding.Bombardment);
            Assert.AreEqual(building.WeaponStrength, deserializedBuilding.WeaponStrength);
            Assert.AreEqual(building.ShieldStrength, deserializedBuilding.ShieldStrength);
            Assert.AreEqual(building.ProducerOwnerID, deserializedBuilding.ProducerOwnerID);
            Assert.AreEqual(
                building.ManufacturingProgress,
                deserializedBuilding.ManufacturingProgress
            );
            Assert.AreEqual(building.ManufacturingStatus, deserializedBuilding.ManufacturingStatus);
            Assert.AreEqual(building.ProductionType, deserializedBuilding.ProductionType);
            Assert.AreEqual(building.GetPosition().X, deserializedBuilding.GetPosition().X);
            Assert.AreEqual(building.GetPosition().Y, deserializedBuilding.GetPosition().Y);
            Assert.AreEqual(building.Movement, deserializedBuilding.Movement);
        }
    }
}
