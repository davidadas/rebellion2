using System;
using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class StrategyMissionChoiceTests
    {
        [Test]
        public void Constructor_NullOption_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StrategyMissionChoice(null));
        }

        [Test]
        public void Constructor_ResearchOption_PreservesMissionPresentation()
        {
            MissionOption option = new MissionOption(
                ResearchMission.MissionTypeID,
                "Research Facilities",
                SkillRating.FacilityResearch,
                MissionTargetKind.Planet,
                SkillRating.None,
                ResearchDiscipline.FacilityDesign
            );

            StrategyMissionChoice choice = new StrategyMissionChoice(option);

            Assert.AreEqual(ResearchMission.MissionTypeID, choice.MissionTypeID);
            Assert.AreEqual(ResearchDiscipline.FacilityDesign, choice.Discipline);
            Assert.AreEqual("Research Facilities", choice.Name);
            Assert.AreEqual(MissionIconKeys.ResearchFacilityDesign, choice.IconKey);
            Assert.AreEqual(MissionTargetKind.Planet, choice.TargetKind);
        }
    }
}
