using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class MissionIconKeysTests
    {
        [TestCase(MissionTypeIDs.Diplomacy, MissionIconKeys.Diplomacy)]
        [TestCase(MissionTypeIDs.Rescue, MissionIconKeys.Rescue)]
        [TestCase(MissionTypeIDs.Sabotage, MissionIconKeys.Sabotage)]
        [TestCase(MissionTypeIDs.Espionage, MissionIconKeys.Espionage)]
        [TestCase(MissionTypeIDs.Reconnaissance, MissionIconKeys.Reconnaissance)]
        [TestCase(MissionTypeIDs.Recruitment, MissionIconKeys.Recruitment)]
        [TestCase(MissionTypeIDs.Abduction, MissionIconKeys.Abduction)]
        [TestCase(MissionTypeIDs.InciteUprising, MissionIconKeys.InciteUprising)]
        [TestCase(MissionTypeIDs.JediTraining, MissionIconKeys.JediTraining)]
        [TestCase(MissionTypeIDs.SubdueUprising, MissionIconKeys.SubdueUprising)]
        [TestCase(MissionTypeIDs.Assassination, MissionIconKeys.Assassination)]
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
            string iconKey = MissionIconKeys.GetMissionIconKey(MissionTypeIDs.Research, discipline);

            Assert.AreEqual(expected, iconKey);
        }

        [Test]
        public void GetMissionIconKey_MissingResearchDiscipline_ReturnsShipDesignIconKey()
        {
            string iconKey = MissionIconKeys.GetMissionIconKey(MissionTypeIDs.Research);

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
