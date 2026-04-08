using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;

[TestFixture]
public class OfficerTests
{
    [Test]
    public void IsMovable_OnActiveMission_ReturnsFalse()
    {
        Officer officer = new Officer { OwnerInstanceID = "rebels" };
        StubMission mission = new StubMission();
        mission.MaxProgress = 5;
        mission.CurrentProgress = 0; // IsComplete() == false
        officer.SetParent(mission);

        Assert.IsFalse(officer.IsMovable(), "Officer on an active mission should not be movable");
    }

    [Test]
    public void IsMovable_OnCompletedMission_ReturnsTrue()
    {
        Officer officer = new Officer { OwnerInstanceID = "rebels" };
        StubMission mission = new StubMission();
        mission.MaxProgress = 1;
        mission.CurrentProgress = 1; // IsComplete() == true
        officer.SetParent(mission);

        Assert.IsTrue(officer.IsMovable(), "Officer on a completed mission should be movable");
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
        StubMission mission = new StubMission();
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
        Officer officer = new Officer { Movement = null };
        bool isMovable = officer.IsMovable();
        Assert.IsTrue(isMovable);
    }

    [Test]
    public void IsMovable_WhenOnMission_ReturnsFalse()
    {
        StubMission mission = new StubMission();
        mission.MaxProgress = 5;
        mission.CurrentProgress = 0;
        Officer officer = new Officer { Movement = null };
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
            Movement = null,
            IsJedi = true,
            IsForceEligible = true,
            ForceValue = 75,
            ForceTrainingAdjustment = 10,
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
            originalOfficer.Movement,
            deserializedOfficer.Movement,
            "MovementStatus mismatch"
        );
        Assert.AreEqual(originalOfficer.IsJedi, deserializedOfficer.IsJedi, "IsJedi mismatch");
        Assert.AreEqual(
            originalOfficer.IsForceEligible,
            deserializedOfficer.IsForceEligible,
            "IsForceEligible mismatch"
        );
        Assert.AreEqual(
            originalOfficer.ForceValue,
            deserializedOfficer.ForceValue,
            "ForceValue mismatch"
        );
        Assert.AreEqual(
            originalOfficer.ForceTrainingAdjustment,
            deserializedOfficer.ForceTrainingAdjustment,
            "ForceTrainingAdjustment mismatch"
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

    [Test]
    public void ShipResearch_SetAndGet_ReturnsCorrectValue()
    {
        Officer officer = new Officer();
        officer.ShipResearch = 50;
        Assert.AreEqual(50, officer.ShipResearch);
    }

    [Test]
    public void TroopResearch_SetAndGet_ReturnsCorrectValue()
    {
        Officer officer = new Officer();
        officer.TroopResearch = 30;
        Assert.AreEqual(30, officer.TroopResearch);
    }

    [Test]
    public void FacilityResearch_SetAndGet_ReturnsCorrectValue()
    {
        Officer officer = new Officer();
        officer.FacilityResearch = 40;
        Assert.AreEqual(40, officer.FacilityResearch);
    }

    [Test]
    public void IsRecruitable_SetToTrue_ReturnsTrue()
    {
        Officer officer = new Officer();
        officer.IsRecruitable = true;
        Assert.IsTrue(officer.IsRecruitable);
    }

    [Test]
    public void IsRecruitable_SetToFalse_ReturnsFalse()
    {
        Officer officer = new Officer();
        officer.IsRecruitable = false;
        Assert.IsFalse(officer.IsRecruitable);
    }

    [Test]
    public void IsCaptured_SetToTrue_ReturnsTrue()
    {
        Officer officer = new Officer();
        officer.IsCaptured = true;
        Assert.IsTrue(officer.IsCaptured);
    }

    [Test]
    public void IsCaptured_SetToFalse_ReturnsFalse()
    {
        Officer officer = new Officer();
        officer.IsCaptured = false;
        Assert.IsFalse(officer.IsCaptured);
    }

    [Test]
    public void IsTraitor_SetToTrue_ReturnsTrue()
    {
        Officer officer = new Officer();
        officer.IsTraitor = true;
        Assert.IsTrue(officer.IsTraitor);
    }

    [Test]
    public void IsTraitor_SetToFalse_ReturnsFalse()
    {
        Officer officer = new Officer();
        officer.IsTraitor = false;
        Assert.IsFalse(officer.IsTraitor);
    }

    [Test]
    public void Loyalty_SetAndGet_ReturnsCorrectValue()
    {
        Officer officer = new Officer();
        officer.Loyalty = 75;
        Assert.AreEqual(75, officer.Loyalty);
    }

    [Test]
    public void GetDisplayName_WhenSet_ReturnsDisplayName()
    {
        Officer officer = new Officer();
        officer.DisplayName = "Admiral Ackbar";
        string displayName = officer.GetDisplayName();
        Assert.AreEqual("Admiral Ackbar", displayName);
    }

    [Test]
    public void GetDisplayName_WhenNotSet_ReturnsNull()
    {
        Officer officer = new Officer();
        string displayName = officer.GetDisplayName();
        Assert.IsNull(displayName);
    }

    [Test]
    public void GetOwnerInstanceID_WhenSet_ReturnsOwnerInstanceID()
    {
        Officer officer = new Officer();
        string expectedOwnerID = "owner123";
        officer.OwnerInstanceID = expectedOwnerID;
        string ownerInstanceID = officer.GetOwnerInstanceID();
        Assert.AreEqual(expectedOwnerID, ownerInstanceID);
    }

    [Test]
    public void GetOwnerInstanceID_WhenNotSet_ReturnsNull()
    {
        Officer officer = new Officer();
        string ownerInstanceID = officer.GetOwnerInstanceID();
        Assert.IsNull(ownerInstanceID);
    }
}
