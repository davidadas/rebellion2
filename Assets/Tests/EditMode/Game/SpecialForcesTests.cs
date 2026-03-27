using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;

[TestFixture]
public class SpecialForcesTests
{
    private class TestMission : Mission
    {
        public TestMission()
        {
            MainParticipants = new List<IMissionParticipant>();
            DecoyParticipants = new List<IMissionParticipant>();
        }

        protected override void OnSuccess(GameRoot game, IRandomNumberProvider provider) { }

        public override bool CanContinue(GameRoot game)
        {
            return true;
        }
    }

    private SpecialForces specialForces;

    [SetUp]
    public void SetUp()
    {
        specialForces = new SpecialForces
        {
            InstanceID = "SF1",
            OwnerInstanceID = "FACTION1",
            ConstructionCost = 300,
            MaintenanceCost = 25,
            BaseBuildSpeed = 10,
            RequiredResearchLevel = 3,
            Movement = null,
            ManufacturingStatus = ManufacturingStatus.Building,
            ManufacturingProgress = 0,
        };

        specialForces.Skills[MissionParticipantSkill.Diplomacy] = 10;
        specialForces.Skills[MissionParticipantSkill.Espionage] = 20;
        specialForces.Skills[MissionParticipantSkill.Combat] = 30;
        specialForces.Skills[MissionParticipantSkill.Leadership] = 15;
    }

    [Test]
    public void GetManufacturingType_ReturnsTroop()
    {
        ManufacturingType type = specialForces.GetManufacturingType();

        Assert.AreEqual(ManufacturingType.Troop, type, "SpecialForces should be of type Troop");
    }

    [Test]
    public void SetMissionSkillValue_ThrowsException()
    {
        Assert.Throws<InvalidSceneOperationException>(
            () => specialForces.SetMissionSkillValue(MissionParticipantSkill.Combat, 50),
            "Special forces should not allow setting mission skills"
        );
    }

    [Test]
    public void IsOnMission_WhenAssignedToMission_ReturnsTrue()
    {
        TestMission mission = new TestMission();
        specialForces.SetParent(mission);

        bool isOnMission = specialForces.IsOnMission();

        Assert.IsTrue(isOnMission, "SpecialForces should be on mission when parent is Mission");
    }

    [Test]
    public void IsOnMission_WhenNotAssignedToMission_ReturnsFalse()
    {
        bool isOnMission = specialForces.IsOnMission();

        Assert.IsFalse(
            isOnMission,
            "SpecialForces should not be on mission when parent is not Mission"
        );
    }

    [Test]
    public void IsMovable_WhenInTransitAndNotOnMission_ReturnsTrue()
    {
        specialForces.Movement = new MovementState();

        bool isMovable = specialForces.IsMovable();

        Assert.IsTrue(
            isMovable,
            "SpecialForces should be movable when in transit and not on mission"
        );
    }

    [Test]
    public void IsMovable_WhenIdle_ReturnsFalse()
    {
        specialForces.Movement = null;

        bool isMovable = specialForces.IsMovable();

        Assert.IsFalse(isMovable, "SpecialForces should not be movable when idle");
    }

    [Test]
    public void IsMovable_WhenOnMission_ReturnsFalse()
    {
        TestMission mission = new TestMission();
        specialForces.Movement = new MovementState();
        specialForces.SetParent(mission);

        bool isMovable = specialForces.IsMovable();

        Assert.IsFalse(isMovable, "SpecialForces should not be movable when on mission");
    }

    [Test]
    public void GetSkillValue_Diplomacy_ReturnsCorrectValue()
    {
        int skillValue = specialForces.Skills[MissionParticipantSkill.Diplomacy];

        Assert.AreEqual(10, skillValue, "Diplomacy skill should return the correct value");
    }

    [Test]
    public void GetSkillValue_Espionage_ReturnsCorrectValue()
    {
        int skillValue = specialForces.Skills[MissionParticipantSkill.Espionage];

        Assert.AreEqual(20, skillValue, "Espionage skill should return the correct value");
    }

    [Test]
    public void GetSkillValue_Combat_ReturnsCorrectValue()
    {
        int skillValue = specialForces.Skills[MissionParticipantSkill.Combat];

        Assert.AreEqual(30, skillValue, "Combat skill should return the correct value");
    }

    [Test]
    public void GetSkillValue_Leadership_ReturnsCorrectValue()
    {
        int skillValue = specialForces.Skills[MissionParticipantSkill.Leadership];

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
        specialForces.Skills[MissionParticipantSkill.Combat] = 100;

        int skillValue = specialForces.Skills[MissionParticipantSkill.Combat];

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
    public void RequiredResearchLevel_WhenSet_ReturnsCorrectValue()
    {
        SpecialForces newSpecialForces = new SpecialForces { RequiredResearchLevel = 5 };

        Assert.AreEqual(5, newSpecialForces.RequiredResearchLevel);
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
        specialForces.ManufacturingProgress = 50;

        Assert.AreEqual(50, specialForces.ManufacturingProgress);
    }

    [Test]
    public void ManufacturingProgress_WhenCompleted_ReachesConstructionCost()
    {
        specialForces.ManufacturingProgress = specialForces.ConstructionCost;

        Assert.AreEqual(
            specialForces.ConstructionCost,
            specialForces.ManufacturingProgress,
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
        specialForces.ManufacturingStatus = ManufacturingStatus.Complete;

        Assert.AreEqual(ManufacturingStatus.Complete, specialForces.ManufacturingStatus);
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
        specialForces.OwnerInstanceID = "FACTION3";

        Assert.AreEqual("FACTION3", specialForces.OwnerInstanceID);
    }

    [Test]
    public void OwnerInstanceID_WhenNull_AllowsNullValue()
    {
        SpecialForces newSpecialForces = new SpecialForces { OwnerInstanceID = null };

        Assert.IsNull(newSpecialForces.OwnerInstanceID);
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        string serialized = SerializationHelper.Serialize(specialForces);
        SpecialForces deserialized = SerializationHelper.Deserialize<SpecialForces>(serialized);

        Assert.AreEqual(
            specialForces.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.OwnerInstanceID,
            deserialized.OwnerInstanceID,
            "OwnerInstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.ConstructionCost,
            deserialized.ConstructionCost,
            "ConstructionCost should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.MaintenanceCost,
            deserialized.MaintenanceCost,
            "MaintenanceCost should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.BaseBuildSpeed,
            deserialized.BaseBuildSpeed,
            "BaseBuildSpeed should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.RequiredResearchLevel,
            deserialized.RequiredResearchLevel,
            "RequiredResearchLevel should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.Movement,
            deserialized.Movement,
            "MovementStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.ManufacturingStatus,
            deserialized.ManufacturingStatus,
            "ManufacturingStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.ManufacturingProgress,
            deserialized.ManufacturingProgress,
            "ManufacturingProgress should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.Skills[MissionParticipantSkill.Diplomacy],
            deserialized.Skills[MissionParticipantSkill.Diplomacy],
            "Diplomacy skill should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.Skills[MissionParticipantSkill.Espionage],
            deserialized.Skills[MissionParticipantSkill.Espionage],
            "Espionage skill should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.Skills[MissionParticipantSkill.Combat],
            deserialized.Skills[MissionParticipantSkill.Combat],
            "Combat skill should be correctly deserialized."
        );
        Assert.AreEqual(
            specialForces.Skills[MissionParticipantSkill.Leadership],
            deserialized.Skills[MissionParticipantSkill.Leadership],
            "Leadership skill should be correctly deserialized."
        );
    }
}
