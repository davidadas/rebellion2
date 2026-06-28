using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class MissionDefinitionCatalogTests
    {
        [Test]
        public void Options_EachMissionType_ReturnsOption()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    MissionTypeIDs.Abduction,
                    MissionTypeIDs.Assassination,
                    MissionTypeIDs.Diplomacy,
                    MissionTypeIDs.Espionage,
                    MissionTypeIDs.InciteUprising,
                    MissionTypeIDs.JediTraining,
                    MissionTypeIDs.Reconnaissance,
                    MissionTypeIDs.Recruitment,
                    MissionTypeIDs.Research,
                    MissionTypeIDs.Rescue,
                    MissionTypeIDs.Sabotage,
                    MissionTypeIDs.SubdueUprising,
                },
                MissionDefinitionCatalog
                    .Options.Select(option => option.MissionTypeID)
                    .Distinct()
                    .ToArray()
            );
        }

        [Test]
        public void Options_NonResearchOptions_UseCatalogRatings()
        {
            foreach (
                MissionOption option in MissionDefinitionCatalog.Options.Where(option =>
                    option.MissionTypeID != MissionTypeIDs.Research
                )
            )
            {
                MissionDefinition definition = MissionDefinitionCatalog.Get(option.MissionTypeID);

                Assert.IsNotNull(definition);
                Assert.AreEqual(definition.ParticipantRating, option.ParticipantRating);
                Assert.AreEqual(definition.DecoyParticipantRating, option.DecoyParticipantRating);
            }
        }

        [Test]
        public void Options_ResearchMission_ReturnsDisciplineOptions()
        {
            MissionOption[] options = MissionDefinitionCatalog
                .Options.Where(option => option.MissionTypeID == MissionTypeIDs.Research)
                .ToArray();

            Assert.AreEqual(3, options.Length);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    ResearchDiscipline.ShipDesign,
                    ResearchDiscipline.FacilityDesign,
                    ResearchDiscipline.TroopTraining,
                },
                options.Select(option => option.Discipline).ToArray()
            );
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Ship Design Research",
                    "Facility Design Research",
                    "Troop Training Research",
                },
                options.Select(option => option.DisplayName).ToArray()
            );
            foreach (MissionOption option in options)
            {
                Assert.AreEqual(
                    Officer.GetRatingForResearchDiscipline(option.Discipline.Value),
                    option.ParticipantRating
                );
                Assert.AreEqual(OfficerRating.None, option.DecoyParticipantRating);
            }
        }
    }
}
