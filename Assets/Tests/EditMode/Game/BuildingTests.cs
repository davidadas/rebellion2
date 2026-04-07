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
            Building building = new Building { BuildingType = BuildingType.Mine };

            Assert.AreEqual(BuildingType.Mine, building.GetBuildingType());
        }

        [Test]
        public void SetManufacturingStatus_ValidStatus_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            building.SetManufacturingStatus(ManufacturingStatus.Complete);

            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
        }

        [Test]
        public void SetManufacturingStatus_InvalidTransition_ThrowsException()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            Assert.Throws<InvalidOperationException>(() =>
                building.SetManufacturingStatus(ManufacturingStatus.Building)
            );
        }

        [Test]
        public void IsMovable_IdleStatus_ReturnsTrue()
        {
            Building building = new Building { Movement = null };

            Assert.IsTrue(building.IsMovable());
        }

        [Test]
        public void IsMovable_InTransitStatus_ReturnsFalse()
        {
            Building building = new Building { Movement = new MovementState() };

            Assert.IsFalse(building.IsMovable());
        }

        [Test]
        public void GetProcessRate_ValidProcessRate_ReturnsCorrectValue()
        {
            Building building = new Building { ProcessRate = 25 };

            Assert.AreEqual(25, building.GetProcessRate());
        }

        [Test]
        public void GetProductionType_ValidProductionType_ReturnsCorrectType()
        {
            Building building = new Building { ProductionType = ManufacturingType.Building };

            Assert.AreEqual(ManufacturingType.Building, building.GetProductionType());
        }

        [Test]
        public void GetManufacturingType_Always_ReturnsBuilding()
        {
            Building building = new Building();

            Assert.AreEqual(ManufacturingType.Building, building.GetManufacturingType());
        }

        [Test]
        public void GetManufacturingStatus_ValidStatus_ReturnsCorrectStatus()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            Assert.AreEqual(ManufacturingStatus.Complete, building.GetManufacturingStatus());
        }

        [Test]
        public void SetManufacturingStatus_BuildingToComplete_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            building.SetManufacturingStatus(ManufacturingStatus.Complete);

            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
        }

        [Test]
        public void SetManufacturingStatus_CompleteToComplete_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            building.SetManufacturingStatus(ManufacturingStatus.Complete);

            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
        }

        [Test]
        public void SetManufacturingStatus_BuildingToBuilding_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            building.SetManufacturingStatus(ManufacturingStatus.Building);

            Assert.AreEqual(ManufacturingStatus.Building, building.ManufacturingStatus);
        }

        [Test]
        public void Bombardment_SetValue_ReturnsCorrectValue()
        {
            Building building = new Building { Bombardment = 10 };

            Assert.AreEqual(10, building.Bombardment);
        }

        [Test]
        public void WeaponStrength_SetValue_ReturnsCorrectValue()
        {
            Building building = new Building { WeaponStrength = 20 };

            Assert.AreEqual(20, building.WeaponStrength);
        }

        [Test]
        public void ShieldStrength_SetValue_ReturnsCorrectValue()
        {
            Building building = new Building { ShieldStrength = 30 };

            Assert.AreEqual(30, building.ShieldStrength);
        }

        [Test]
        public void ManufacturingProgress_ZeroValue_ReturnsZero()
        {
            Building building = new Building { ManufacturingProgress = 0 };

            Assert.AreEqual(0, building.ManufacturingProgress);
        }

        [Test]
        public void ManufacturingProgress_MaxValue_ReturnsMaxValue()
        {
            Building building = new Building { ManufacturingProgress = int.MaxValue };

            Assert.AreEqual(int.MaxValue, building.ManufacturingProgress);
        }

        [Test]
        public void ManufacturingProgress_NegativeValue_ReturnsNegativeValue()
        {
            Building building = new Building { ManufacturingProgress = -10 };

            Assert.AreEqual(-10, building.ManufacturingProgress);
        }

        [Test]
        public void ManufacturingProgress_PartialProgress_ReturnsCorrectValue()
        {
            Building building = new Building { ManufacturingProgress = 75 };

            Assert.AreEqual(75, building.ManufacturingProgress);
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
