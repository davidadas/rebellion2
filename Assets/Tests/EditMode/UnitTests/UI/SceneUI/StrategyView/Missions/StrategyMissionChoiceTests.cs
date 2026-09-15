using System;
using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Missions
{
    [TestFixture]
    public class StrategyMissionChoiceTests
    {
        /// <summary>
        /// Verifies constructor null option throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullOption_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StrategyMissionChoice(null));
        }

        /// <summary>
        /// Verifies constructor research option preserves mission presentation.
        /// </summary>
        [Test]
        public void Constructor_ResearchOption_PreservesMissionPresentation()
        {
            MissionOption option = new MissionOption(
                MissionTypeIDs.Research,
                "Research Facilities",
                OfficerRating.FacilityResearch,
                MissionTargetKind.Planet,
                OfficerRating.None,
                ResearchDiscipline.FacilityDesign
            );

            StrategyMissionChoice choice = new StrategyMissionChoice(option);

            Assert.AreEqual(MissionTypeIDs.Research, choice.MissionTypeID);
            Assert.AreEqual(ResearchDiscipline.FacilityDesign, choice.Discipline);
            Assert.AreEqual("Research Facilities", choice.Name);
            Assert.AreEqual(MissionIconKeys.ResearchFacilityDesign, choice.IconKey);
            Assert.AreEqual(MissionTargetKind.Planet, choice.TargetKind);
        }
    }
}
