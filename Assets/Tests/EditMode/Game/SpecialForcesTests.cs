using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class SpecialForcesTests
    {
        private SpecialForces _specialForces;

        [SetUp]
        public void SetUp()
        {
            _specialForces = new SpecialForces
            {
                InstanceID = "SF1",
                OwnerInstanceID = "FACTION1",
                ConstructionCost = 300,
                MaintenanceCost = 25,
                BaseBuildSpeed = 10,
                ResearchOrder = 3,
                ResearchDifficulty = 60,
                Movement = null,
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
            };

            _specialForces.Skills[MissionParticipantSkill.Diplomacy] = 10;
            _specialForces.Skills[MissionParticipantSkill.Espionage] = 20;
            _specialForces.Skills[MissionParticipantSkill.Combat] = 30;
            _specialForces.Skills[MissionParticipantSkill.Leadership] = 15;
        }

        [Test]
        public void GetManufacturingType_ForSpecialForces_ReturnsTroop()
        {
            ManufacturingType type = _specialForces.GetManufacturingType();

            Assert.AreEqual(ManufacturingType.Troop, type, "SpecialForces should be of type Troop");
        }

        [Test]
        public void SetMissionSkillValue_ValidSkill_SetsValue()
        {
            _specialForces.SetMissionSkillValue(MissionParticipantSkill.Combat, 99);

            Assert.AreEqual(99, _specialForces.Skills[MissionParticipantSkill.Combat]);
        }

        [Test]
        public void IsOnMission_WhenAssignedToMission_ReturnsTrue()
        {
            StubMission mission = new StubMission();
            _specialForces.SetParent(mission);

            bool isOnMission = _specialForces.IsOnMission();

            Assert.IsTrue(isOnMission, "SpecialForces should be on mission when parent is Mission");
        }

        [Test]
        public void IsOnMission_WhenNotAssignedToMission_ReturnsFalse()
        {
            bool isOnMission = _specialForces.IsOnMission();

            Assert.IsFalse(
                isOnMission,
                "SpecialForces should not be on mission when parent is not Mission"
            );
        }

        [Test]
        public void IsMovable_WhenInTransit_ReturnsFalse()
        {
            _specialForces.Movement = new MovementState();

            bool isMovable = _specialForces.IsMovable();

            Assert.IsFalse(isMovable, "SpecialForces should not be movable when in transit");
        }

        [Test]
        public void IsMovable_WhenIdle_ReturnsTrue()
        {
            _specialForces.Movement = null;

            bool isMovable = _specialForces.IsMovable();

            Assert.IsTrue(isMovable, "SpecialForces should be movable when idle");
        }

        [Test]
        public void IsMovable_WhenOnMission_ReturnsFalse()
        {
            StubMission mission = new StubMission();
            _specialForces.Movement = new MovementState();
            _specialForces.SetParent(mission);

            bool isMovable = _specialForces.IsMovable();

            Assert.IsFalse(isMovable, "SpecialForces should not be movable when on mission");
        }

        [Test]
        public void GetSkillValue_Diplomacy_ReturnsCorrectValue()
        {
            int skillValue = _specialForces.Skills[MissionParticipantSkill.Diplomacy];

            Assert.AreEqual(10, skillValue, "Diplomacy skill should return the correct value");
        }

        [Test]
        public void GetSkillValue_Espionage_ReturnsCorrectValue()
        {
            int skillValue = _specialForces.Skills[MissionParticipantSkill.Espionage];

            Assert.AreEqual(20, skillValue, "Espionage skill should return the correct value");
        }

        [Test]
        public void GetSkillValue_Combat_ReturnsCorrectValue()
        {
            int skillValue = _specialForces.Skills[MissionParticipantSkill.Combat];

            Assert.AreEqual(30, skillValue, "Combat skill should return the correct value");
        }

        [Test]
        public void GetSkillValue_Leadership_ReturnsCorrectValue()
        {
            int skillValue = _specialForces.Skills[MissionParticipantSkill.Leadership];

            Assert.AreEqual(15, skillValue, "Leadership skill should return the correct value");
        }

        [Test]
        public void Skills_SetBeforeMission_StoresCorrectValue()
        {
            SpecialForces newSpecialForces = new SpecialForces();
            newSpecialForces.Skills[MissionParticipantSkill.Diplomacy] = 50;
            newSpecialForces.Skills[MissionParticipantSkill.Espionage] = 60;

            Assert.AreEqual(50, newSpecialForces.Skills[MissionParticipantSkill.Diplomacy]);
            Assert.AreEqual(60, newSpecialForces.Skills[MissionParticipantSkill.Espionage]);
        }

        [Test]
        public void Skills_ModifiedBeforeMission_UpdatesCorrectly()
        {
            _specialForces.Skills[MissionParticipantSkill.Combat] = 100;

            int skillValue = _specialForces.Skills[MissionParticipantSkill.Combat];

            Assert.AreEqual(100, skillValue, "Combat skill should update correctly");
        }

        [Test]
        public void ConstructionCost_WhenSet_ReturnsCorrectValue()
        {
            SpecialForces newSpecialForces = new SpecialForces { ConstructionCost = 500 };

            Assert.AreEqual(500, newSpecialForces.ConstructionCost);
        }

        [Test]
        public void BaseBuildSpeed_WhenSet_ReturnsCorrectValue()
        {
            SpecialForces newSpecialForces = new SpecialForces { BaseBuildSpeed = 15 };

            Assert.AreEqual(15, newSpecialForces.BaseBuildSpeed);
        }

        [Test]
        public void MaintenanceCost_WhenSet_ReturnsCorrectValue()
        {
            SpecialForces newSpecialForces = new SpecialForces { MaintenanceCost = 35 };

            Assert.AreEqual(35, newSpecialForces.MaintenanceCost);
        }

        [Test]
        public void ResearchOrder_WhenSet_ReturnsCorrectValue()
        {
            SpecialForces newSpecialForces = new SpecialForces
            {
                ResearchOrder = 5,
                ResearchDifficulty = 50,
            };

            Assert.AreEqual(5, newSpecialForces.ResearchOrder);
            Assert.AreEqual(50, newSpecialForces.ResearchDifficulty);
        }

        [Test]
        public void ManufacturingProgress_InitialValue_IsZero()
        {
            SpecialForces newSpecialForces = new SpecialForces();

            Assert.AreEqual(0, newSpecialForces.ManufacturingProgress);
        }

        [Test]
        public void ManufacturingProgress_WhenIncremented_UpdatesCorrectly()
        {
            _specialForces.ManufacturingProgress = 50;

            Assert.AreEqual(50, _specialForces.ManufacturingProgress);
        }

        [Test]
        public void ManufacturingProgress_WhenCompleted_ReachesConstructionCost()
        {
            _specialForces.ManufacturingProgress = _specialForces.ConstructionCost;

            Assert.AreEqual(
                _specialForces.ConstructionCost,
                _specialForces.ManufacturingProgress,
                "ManufacturingProgress should equal ConstructionCost when complete"
            );
        }

        [Test]
        public void ManufacturingStatus_InitialValue_IsBuilding()
        {
            SpecialForces newSpecialForces = new SpecialForces();

            Assert.AreEqual(ManufacturingStatus.Building, newSpecialForces.ManufacturingStatus);
        }

        [Test]
        public void ManufacturingStatus_WhenChanged_UpdatesCorrectly()
        {
            _specialForces.ManufacturingStatus = ManufacturingStatus.Complete;

            Assert.AreEqual(ManufacturingStatus.Complete, _specialForces.ManufacturingStatus);
        }

        [Test]
        public void OwnerInstanceID_WhenSet_ReturnsCorrectValue()
        {
            SpecialForces newSpecialForces = new SpecialForces { OwnerInstanceID = "FACTION2" };

            Assert.AreEqual("FACTION2", newSpecialForces.OwnerInstanceID);
        }

        [Test]
        public void OwnerInstanceID_WhenChanged_UpdatesCorrectly()
        {
            _specialForces.OwnerInstanceID = "FACTION3";

            Assert.AreEqual("FACTION3", _specialForces.OwnerInstanceID);
        }

        [Test]
        public void OwnerInstanceID_WhenNull_AllowsNullValue()
        {
            SpecialForces newSpecialForces = new SpecialForces { OwnerInstanceID = null };

            Assert.IsNull(newSpecialForces.OwnerInstanceID);
        }

        [Test]
        public void CanImproveMissionSkill_Always_ReturnsFalse()
        {
            Assert.IsFalse(
                _specialForces.CanImproveMissionSkill,
                "SpecialForces should not gain skills from missions"
            );
        }

        [Test]
        public void CanPerformMission_AllowedType_ReturnsTrue()
        {
            _specialForces.AllowedMissionTypes.Add(MissionType.Espionage);

            Assert.IsTrue(_specialForces.CanPerformMission(MissionType.Espionage));
        }

        [Test]
        public void CanPerformMission_DisallowedType_ReturnsFalse()
        {
            _specialForces.AllowedMissionTypes.Add(MissionType.Espionage);

            Assert.IsFalse(_specialForces.CanPerformMission(MissionType.Sabotage));
        }

        [Test]
        public void SerializeAndDeserialize_WithPopulatedSpecialForces_MaintainsState()
        {
            string serialized = SerializationHelper.Serialize(_specialForces);
            SpecialForces deserialized = SerializationHelper.Deserialize<SpecialForces>(serialized);

            Assert.AreEqual(
                _specialForces.InstanceID,
                deserialized.InstanceID,
                "InstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.OwnerInstanceID,
                deserialized.OwnerInstanceID,
                "OwnerInstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.ConstructionCost,
                deserialized.ConstructionCost,
                "ConstructionCost should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.MaintenanceCost,
                deserialized.MaintenanceCost,
                "MaintenanceCost should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.BaseBuildSpeed,
                deserialized.BaseBuildSpeed,
                "BaseBuildSpeed should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.ResearchOrder,
                deserialized.ResearchOrder,
                "ResearchOrder should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.ResearchDifficulty,
                deserialized.ResearchDifficulty,
                "ResearchDifficulty should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.Movement,
                deserialized.Movement,
                "MovementStatus should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.ManufacturingStatus,
                deserialized.ManufacturingStatus,
                "ManufacturingStatus should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.ManufacturingProgress,
                deserialized.ManufacturingProgress,
                "ManufacturingProgress should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.Skills[MissionParticipantSkill.Diplomacy],
                deserialized.Skills[MissionParticipantSkill.Diplomacy],
                "Diplomacy skill should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.Skills[MissionParticipantSkill.Espionage],
                deserialized.Skills[MissionParticipantSkill.Espionage],
                "Espionage skill should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.Skills[MissionParticipantSkill.Combat],
                deserialized.Skills[MissionParticipantSkill.Combat],
                "Combat skill should be correctly deserialized."
            );
            Assert.AreEqual(
                _specialForces.Skills[MissionParticipantSkill.Leadership],
                deserialized.Skills[MissionParticipantSkill.Leadership],
                "Leadership skill should be correctly deserialized."
            );
        }
    }
}
