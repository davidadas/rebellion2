using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionIconKeysTests
    {
        [TestCase(DiplomacyMission.MissionTypeID, MissionIconKeys.Diplomacy)]
        [TestCase(RescueMission.MissionTypeID, MissionIconKeys.Rescue)]
        [TestCase(SabotageMission.MissionTypeID, MissionIconKeys.Sabotage)]
        [TestCase(EspionageMission.MissionTypeID, MissionIconKeys.Espionage)]
        [TestCase(ReconnaissanceMission.MissionTypeID, MissionIconKeys.Reconnaissance)]
        [TestCase(RecruitmentMission.MissionTypeID, MissionIconKeys.Recruitment)]
        [TestCase(AbductionMission.MissionTypeID, MissionIconKeys.Abduction)]
        [TestCase(InciteUprisingMission.MissionTypeID, MissionIconKeys.InciteUprising)]
        [TestCase(JediTrainingMission.MissionTypeID, MissionIconKeys.JediTraining)]
        [TestCase(SubdueUprisingMission.MissionTypeID, MissionIconKeys.SubdueUprising)]
        [TestCase(AssassinationMission.MissionTypeID, MissionIconKeys.Assassination)]
        public void GetMissionIconKey_ConfiguredMissionType_ReturnsMatchingIconKey(
            string missionTypeId,
            string expected
        )
        {
            string iconKey = MissionIconKeys.GetMissionIconKey(missionTypeId);

            Assert.AreEqual(expected, iconKey);
        }

        [TestCase(ResearchDiscipline.ShipDesign, MissionIconKeys.ResearchShipDesign)]
        [TestCase(ResearchDiscipline.FacilityDesign, MissionIconKeys.ResearchFacilityDesign)]
        [TestCase(ResearchDiscipline.TroopTraining, MissionIconKeys.ResearchTroopTraining)]
        public void GetMissionIconKey_ResearchDiscipline_ReturnsMatchingResearchIconKey(
            ResearchDiscipline discipline,
            string expected
        )
        {
            string iconKey = MissionIconKeys.GetMissionIconKey(
                ResearchMission.MissionTypeID,
                discipline
            );

            Assert.AreEqual(expected, iconKey);
        }

        [Test]
        public void GetMissionIconKey_MissingResearchDiscipline_ReturnsShipDesignIconKey()
        {
            string iconKey = MissionIconKeys.GetMissionIconKey(ResearchMission.MissionTypeID);

            Assert.AreEqual(MissionIconKeys.ResearchShipDesign, iconKey);
        }

        [Test]
        public void GetMissionIconKey_UnsupportedMissionType_ReturnsNull()
        {
            string iconKey = MissionIconKeys.GetMissionIconKey("unsupported");

            Assert.IsNull(iconKey);
        }
    }
}
