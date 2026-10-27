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

        /// <summary>
        /// Uses the same mission identity and artwork for normal and repeat entries.
        /// </summary>
        /// <param name="isRepeating">Whether to request the repeat entry.</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Constructor_EspionageOption_PreservesIdentityAndSelectsRepeat(bool isRepeating)
        {
            MissionOption option = new MissionOption(
                EspionageMission.MissionTypeID,
                "Espionage",
                SkillRating.Espionage,
                MissionTargetKind.Planet,
                SkillRating.Espionage,
                canRepeat: true
            );

            StrategyMissionChoice choice = new StrategyMissionChoice(option, isRepeating);

            Assert.AreEqual(EspionageMission.MissionTypeID, choice.MissionTypeID);
            Assert.AreEqual(MissionIconKeys.Espionage, choice.IconKey);
            Assert.AreEqual(isRepeating ? "Espionage (Repeat)" : "Espionage", choice.Name);
            Assert.AreEqual(isRepeating, choice.IsRepeating);
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
