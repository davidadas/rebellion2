using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class OfficerTests
{
    private class TestMission : Mission
    {
        public TestMission()
        {
            MainParticipants = new List<IMissionParticipant>();
            DecoyParticipants = new List<IMissionParticipant>();
        }

        protected override void OnSuccess(Game game) { }

        public override bool CanContinue(Game game)
        {
            return true;
        }
    }

    [Test]
    public void GetSkillValue_ValidSkill_ReturnsCorrectValue()
    {
        Officer officer = new Officer();
        officer.SetSkillValue(MissionParticipantSkill.Diplomacy, 10);
        int skillValue = officer.GetSkillValue(MissionParticipantSkill.Diplomacy);
        Assert.AreEqual(10, skillValue);
    }

    [Test]
    public void SetSkillValue_UpdatesValueCorrectly()
    {
        Officer officer = new Officer();
        int updatedValue = officer.SetSkillValue(MissionParticipantSkill.Combat, 15);
        Assert.AreEqual(15, updatedValue);
        Assert.AreEqual(15, officer.GetSkillValue(MissionParticipantSkill.Combat));
    }

    [Test]
    public void IsOnMission_WhenAssignedToMission_ReturnsTrue()
    {
        TestMission mission = new TestMission();
        Officer officer = new Officer();
        officer.SetParent(mission);
        bool isOnMission = officer.IsOnMission();
        Assert.IsTrue(isOnMission);
    }

    [Test]
    public void IsOnMission_WhenNotAssignedToMission_ReturnsFalse()
    {
        Officer officer = new Officer();
        bool isOnMission = officer.IsOnMission();
        Assert.IsFalse(isOnMission);
    }

    [Test]
    public void IsMovable_WhenIdleAndNotOnMission_ReturnsTrue()
    {
        Officer officer = new Officer { MovementStatus = MovementStatus.Idle };
        bool isMovable = officer.IsMovable();
        Assert.IsTrue(isMovable);
    }

    [Test]
    public void IsMovable_WhenOnMission_ReturnsFalse()
    {
        TestMission mission = new TestMission();
        Officer officer = new Officer { MovementStatus = MovementStatus.Idle };
        officer.SetParent(mission);
        bool isMovable = officer.IsMovable();
        Assert.IsFalse(isMovable);
    }

    [Test]
    public void SerializeDeserialize_Officer_PreservesAllData()
    {
        Officer originalOfficer = new Officer
        {
            IsMain = true,
            CurrentRank = OfficerRank.Admiral,
            Skills = new Dictionary<MissionParticipantSkill, int>
            {
                { MissionParticipantSkill.Espionage, 15 },
                { MissionParticipantSkill.Leadership, 25 },
            },
            MovementStatus = MovementStatus.Idle,
            IsJedi = true,
            JediLevel = 5,
            JediLevelVariance = 2,
            CanBetray = false,
        };

        string xml = SerializationHelper.Serialize(originalOfficer);
        Officer deserializedOfficer = SerializationHelper.Deserialize<Officer>(xml);

        Assert.AreEqual(originalOfficer.IsMain, deserializedOfficer.IsMain, "IsMain mismatch");
        Assert.AreEqual(
            originalOfficer.CurrentRank,
            deserializedOfficer.CurrentRank,
            "CurrentRank mismatch"
        );
        Assert.AreEqual(
            originalOfficer.MovementStatus,
            deserializedOfficer.MovementStatus,
            "MovementStatus mismatch"
        );
        Assert.AreEqual(originalOfficer.IsJedi, deserializedOfficer.IsJedi, "IsJedi mismatch");
        Assert.AreEqual(
            originalOfficer.JediLevel,
            deserializedOfficer.JediLevel,
            "JediLevel mismatch"
        );
        Assert.AreEqual(
            originalOfficer.JediLevelVariance,
            deserializedOfficer.JediLevelVariance,
            "JediLevelVariance mismatch"
        );
        Assert.AreEqual(
            originalOfficer.CanBetray,
            deserializedOfficer.CanBetray,
            "CanBetray mismatch"
        );
        Assert.AreEqual(
            originalOfficer.GetSkillValue(MissionParticipantSkill.Espionage),
            deserializedOfficer.GetSkillValue(MissionParticipantSkill.Espionage),
            "Espionage skill mismatch"
        );
        Assert.AreEqual(
            originalOfficer.GetSkillValue(MissionParticipantSkill.Leadership),
            deserializedOfficer.GetSkillValue(MissionParticipantSkill.Leadership),
            "Leadership skill mismatch"
        );
    }
}
