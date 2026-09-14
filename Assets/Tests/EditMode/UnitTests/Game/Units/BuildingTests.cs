using System;
using NUnit.Framework;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.Util.Extensions;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public class BuildingTests
    {
        /// <summary>
        /// Verifies construction info set values returns correct values.
        /// </summary>
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

        /// <summary>
        /// Verifies get building type valid building type returns correct type.
        /// </summary>
        [Test]
        public void GetBuildingType_ValidBuildingType_ReturnsCorrectType()
        {
            Building building = new Building { BuildingType = BuildingType.Mine };

            Assert.AreEqual(BuildingType.Mine, building.GetBuildingType());
        }

        /// <summary>
        /// Verifies set manufacturing status valid status updates successfully.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_ValidStatus_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            building.SetManufacturingStatus(ManufacturingStatus.Complete);

            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies set manufacturing status invalid transition throws exception.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_InvalidTransition_ThrowsException()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            Assert.Throws<InvalidOperationException>(() =>
                building.SetManufacturingStatus(ManufacturingStatus.Building)
            );
        }

        /// <summary>
        /// Verifies set manufacturing status building to complete updates successfully.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_BuildingToComplete_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            building.SetManufacturingStatus(ManufacturingStatus.Complete);

            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies set manufacturing status building to delivering updates successfully.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_BuildingToDelivering_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            building.SetManufacturingStatus(ManufacturingStatus.Delivering);

            Assert.AreEqual(ManufacturingStatus.Delivering, building.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies set manufacturing status delivering to complete updates successfully.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_DeliveringToComplete_UpdatesSuccessfully()
        {
            Building building = new Building
            {
                ManufacturingStatus = ManufacturingStatus.Delivering,
            };

            building.SetManufacturingStatus(ManufacturingStatus.Complete);

            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies set manufacturing status delivering to building throws exception.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_DeliveringToBuilding_ThrowsException()
        {
            Building building = new Building
            {
                ManufacturingStatus = ManufacturingStatus.Delivering,
            };

            Assert.Throws<InvalidOperationException>(() =>
                building.SetManufacturingStatus(ManufacturingStatus.Building)
            );
        }

        /// <summary>
        /// Verifies set manufacturing status complete to delivering throws exception.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_CompleteToDelivering_ThrowsException()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            Assert.Throws<InvalidOperationException>(() =>
                building.SetManufacturingStatus(ManufacturingStatus.Delivering)
            );
        }

        /// <summary>
        /// Verifies set manufacturing status complete to complete updates successfully.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_CompleteToComplete_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            building.SetManufacturingStatus(ManufacturingStatus.Complete);

            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies set manufacturing status building to building updates successfully.
        /// </summary>
        [Test]
        public void SetManufacturingStatus_BuildingToBuilding_UpdatesSuccessfully()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            building.SetManufacturingStatus(ManufacturingStatus.Building);

            Assert.AreEqual(ManufacturingStatus.Building, building.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies is movable idle status returns true.
        /// </summary>
        [Test]
        public void IsMovable_IdleStatus_ReturnsTrue()
        {
            Building building = new Building { Movement = null };

            Assert.IsTrue(building.IsMovable());
        }

        /// <summary>
        /// Verifies is movable in transit status returns false.
        /// </summary>
        [Test]
        public void IsMovable_InTransitStatus_ReturnsFalse()
        {
            Building building = new Building { Movement = new MovementState() };

            Assert.IsFalse(building.IsMovable());
        }

        /// <summary>
        /// Verifies get process rate valid process rate returns correct value.
        /// </summary>
        [Test]
        public void GetProcessRate_ValidProcessRate_ReturnsCorrectValue()
        {
            Building building = new Building { ProcessRate = 25 };

            Assert.AreEqual(25, building.GetProcessRate());
        }

        /// <summary>
        /// Verifies can upgrade to authored upgrade returns true.
        /// </summary>
        [Test]
        public void CanUpgradeTo_AuthoredUpgrade_ReturnsTrue()
        {
            Building building = new Building { Upgrades = { "advanced-shipyard" } };
            Building upgrade = new Building { TypeID = "advanced-shipyard" };

            Assert.IsTrue(building.CanUpgradeTo(upgrade));
        }

        /// <summary>
        /// Verifies can upgrade to unlisted building returns false.
        /// </summary>
        [Test]
        public void CanUpgradeTo_UnlistedBuilding_ReturnsFalse()
        {
            Building building = new Building { Upgrades = { "advanced-shipyard" } };
            Building upgrade = new Building { TypeID = "specialized-shipyard" };

            Assert.IsFalse(building.CanUpgradeTo(upgrade));
        }

        /// <summary>
        /// Verifies is defense facility defense building returns true.
        /// </summary>
        [Test]
        public void IsDefenseFacility_DefenseBuilding_ReturnsTrue()
        {
            Building building = new Building { BuildingType = BuildingType.Defense };

            Assert.IsTrue(building.IsDefenseFacility());
        }

        /// <summary>
        /// Verifies is defense facility non defense building returns false.
        /// </summary>
        [Test]
        public void IsDefenseFacility_NonDefenseBuilding_ReturnsFalse()
        {
            Building building = new Building { BuildingType = BuildingType.Mine };

            Assert.IsFalse(building.IsDefenseFacility());
        }

        /// <summary>
        /// Verifies is planetary shield generator positive shield strength returns true.
        /// </summary>
        [Test]
        public void IsPlanetaryShieldGenerator_PositiveShieldStrength_ReturnsTrue()
        {
            Building building = new Building
            {
                BuildingType = BuildingType.Defense,
                ShieldStrength = 1,
            };

            Assert.IsTrue(building.IsPlanetaryShieldGenerator());
        }

        /// <summary>
        /// Verifies is planetary shield generator weapon building returns false.
        /// </summary>
        [Test]
        public void IsPlanetaryShieldGenerator_WeaponBuilding_ReturnsFalse()
        {
            Building building = new Building
            {
                BuildingType = BuildingType.Weapon,
                ShieldStrength = 1,
            };

            Assert.IsFalse(building.IsPlanetaryShieldGenerator());
        }

        /// <summary>
        /// Verifies is unit shield generator protected unit type returns true.
        /// </summary>
        [Test]
        public void IsUnitShieldGenerator_ProtectedUnitType_ReturnsTrue()
        {
            Building building = new Building { ProtectedUnitTypeIDs = { "capital-ship" } };

            Assert.IsTrue(building.IsUnitShieldGenerator());
        }

        /// <summary>
        /// Verifies is shield generator no shield capability returns false.
        /// </summary>
        [Test]
        public void IsShieldGenerator_NoShieldCapability_ReturnsFalse()
        {
            Building building = new Building { BuildingType = BuildingType.Defense };

            Assert.IsFalse(building.IsShieldGenerator());
        }

        /// <summary>
        /// Verifies get production type valid production type returns correct type.
        /// </summary>
        [Test]
        public void GetProductionType_ValidProductionType_ReturnsCorrectType()
        {
            Building building = new Building { ProductionType = ManufacturingType.Building };

            Assert.AreEqual(ManufacturingType.Building, building.GetProductionType());
        }

        /// <summary>
        /// Verifies get manufacturing type always returns building.
        /// </summary>
        [Test]
        public void GetManufacturingType_Always_ReturnsBuilding()
        {
            Building building = new Building();

            Assert.AreEqual(ManufacturingType.Building, building.GetManufacturingType());
        }

        /// <summary>
        /// Verifies get manufacturing status valid status returns correct status.
        /// </summary>
        [Test]
        public void GetManufacturingStatus_ValidStatus_ReturnsCorrectStatus()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            Assert.AreEqual(ManufacturingStatus.Complete, building.GetManufacturingStatus());
        }

        /// <summary>
        /// Verifies bombardment set value returns correct value.
        /// </summary>
        [Test]
        public void Bombardment_SetValue_ReturnsCorrectValue()
        {
            Building building = new Building { Bombardment = 10 };

            Assert.AreEqual(10, building.Bombardment);
        }

        /// <summary>
        /// Verifies weapon strength set value returns correct value.
        /// </summary>
        [Test]
        public void WeaponStrength_SetValue_ReturnsCorrectValue()
        {
            Building building = new Building { WeaponStrength = 20 };

            Assert.AreEqual(20, building.WeaponStrength);
        }

        /// <summary>
        /// Verifies shield strength set value returns correct value.
        /// </summary>
        [Test]
        public void ShieldStrength_SetValue_ReturnsCorrectValue()
        {
            Building building = new Building { ShieldStrength = 30 };

            Assert.AreEqual(30, building.ShieldStrength);
        }

        /// <summary>
        /// Verifies manufacturing progress zero value returns zero.
        /// </summary>
        [Test]
        public void ManufacturingProgress_ZeroValue_ReturnsZero()
        {
            Building building = new Building { ManufacturingProgress = 0 };

            Assert.AreEqual(0, building.ManufacturingProgress);
        }

        /// <summary>
        /// Verifies manufacturing progress max value returns max value.
        /// </summary>
        [Test]
        public void ManufacturingProgress_MaxValue_ReturnsMaxValue()
        {
            Building building = new Building { ManufacturingProgress = int.MaxValue };

            Assert.AreEqual(int.MaxValue, building.ManufacturingProgress);
        }

        /// <summary>
        /// Verifies manufacturing progress negative value returns negative value.
        /// </summary>
        [Test]
        public void ManufacturingProgress_NegativeValue_ReturnsNegativeValue()
        {
            Building building = new Building { ManufacturingProgress = -10 };

            Assert.AreEqual(-10, building.ManufacturingProgress);
        }

        /// <summary>
        /// Verifies manufacturing progress partial progress returns correct value.
        /// </summary>
        [Test]
        public void ManufacturingProgress_PartialProgress_ReturnsCorrectValue()
        {
            Building building = new Building { ManufacturingProgress = 75 };

            Assert.AreEqual(75, building.ManufacturingProgress);
        }

        /// <summary>
        /// Verifies serialize and deserialize populated building retains properties.
        /// </summary>
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
                DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage,
                ProtectedUnitTypeIDs = { "capital-ship" },
                ProducerOwnerID = "Faction1",
                ProducerPlanetID = "Planet1",
                ManufacturingQueueSequence = 7,
                ManufacturingProgress = 50,
                ManufacturingStatus = ManufacturingStatus.Building,
                ProductionType = ManufacturingType.Building,
                ProductionCycleProgress = 1.5,
                ProductionCycleDuration = 4,
                ProductionPointReady = true,
                ProductionInputReserved = true,
                ResourceMaintenanceAllocation = 12,
                ResourceStartupCyclePending = false,
                IsDetectionBlocker = true,
                Upgrades = { "advanced-building" },
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
            Assert.AreEqual(building.DefenseWeaponEffect, deserializedBuilding.DefenseWeaponEffect);
            Assert.AreEqual(building.IsDetectionBlocker, deserializedBuilding.IsDetectionBlocker);
            CollectionAssert.AreEqual(
                building.ProtectedUnitTypeIDs,
                deserializedBuilding.ProtectedUnitTypeIDs
            );
            Assert.AreEqual(building.ProducerOwnerID, deserializedBuilding.ProducerOwnerID);
            Assert.AreEqual(building.ProducerPlanetID, deserializedBuilding.ProducerPlanetID);
            Assert.AreEqual(
                building.ManufacturingQueueSequence,
                deserializedBuilding.ManufacturingQueueSequence
            );
            Assert.AreEqual(
                building.ManufacturingProgress,
                deserializedBuilding.ManufacturingProgress
            );
            Assert.AreEqual(building.ManufacturingStatus, deserializedBuilding.ManufacturingStatus);
            Assert.AreEqual(building.ProductionType, deserializedBuilding.ProductionType);
            Assert.AreEqual(
                building.ProductionCycleProgress,
                deserializedBuilding.ProductionCycleProgress
            );
            Assert.AreEqual(
                building.ProductionCycleDuration,
                deserializedBuilding.ProductionCycleDuration
            );
            Assert.AreEqual(
                building.ProductionPointReady,
                deserializedBuilding.ProductionPointReady
            );
            Assert.AreEqual(
                building.ProductionInputReserved,
                deserializedBuilding.ProductionInputReserved
            );
            Assert.AreEqual(
                building.ResourceMaintenanceAllocation,
                deserializedBuilding.ResourceMaintenanceAllocation
            );
            Assert.AreEqual(
                building.ResourceStartupCyclePending,
                deserializedBuilding.ResourceStartupCyclePending
            );
            CollectionAssert.AreEqual(building.Upgrades, deserializedBuilding.Upgrades);
            Assert.AreEqual(building.GetPosition().X, deserializedBuilding.GetPosition().X);
            Assert.AreEqual(building.GetPosition().Y, deserializedBuilding.GetPosition().Y);
            Assert.AreEqual(building.Movement, deserializedBuilding.Movement);
        }
    }
}
