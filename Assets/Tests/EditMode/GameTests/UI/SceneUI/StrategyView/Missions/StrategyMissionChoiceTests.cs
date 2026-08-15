using System;
using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;

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
                MissionTypeIDs.Research,
                "Research Facilities",
                OfficerRating.FacilityResearch,
                OfficerRating.None,
                ResearchDiscipline.FacilityDesign
            );

            StrategyMissionChoice choice = new StrategyMissionChoice(option);

            Assert.AreEqual(MissionTypeIDs.Research, choice.MissionTypeID);
            Assert.AreEqual(ResearchDiscipline.FacilityDesign, choice.Discipline);
            Assert.AreEqual("Research Facilities", choice.Name);
            Assert.AreEqual(MissionIconKeys.ResearchFacilityDesign, choice.IconKey);
        }
    }
}
