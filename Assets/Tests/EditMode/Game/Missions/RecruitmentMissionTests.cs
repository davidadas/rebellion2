using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class RecruitmentMissionTests
    {
        [Test]
        public void SerializeAndDeserialize_PopulatedMission_RetainsAllProperties()
        {
            RecruitmentMission mission = new RecruitmentMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Recruitment",
                DisplayName = "Recruitment",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Diplomacy,
                TargetOfficerInstanceID = "OFFICER4",
            };

            string xml = SerializationHelper.Serialize(mission);
            RecruitmentMission deserialized =
                SerializationHelper.Deserialize<RecruitmentMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Recruitment", deserialized.ConfigKey);
            Assert.AreEqual("OFFICER4", deserialized.TargetOfficerInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Diplomacy, deserialized.ParticipantSkill);
        }
    }
}
