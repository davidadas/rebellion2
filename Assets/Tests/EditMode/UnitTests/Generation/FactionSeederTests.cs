using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Research;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class FactionSeederTests
    {
        /// <summary>
        /// Verifies seed non zero starting level applies to each research discipline.
        /// </summary>
        [Test]
        public void Seed_NonZeroStartingLevel_AppliesToEachResearchDiscipline()
        {
            Faction faction = new Faction { InstanceID = "FNEMP1" };

            new FactionSeeder().Seed(BuildContext(new[] { faction }, startingResearchLevel: 5));

            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign));
            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.TroopTraining));
        }

        /// <summary>
        /// Verifies seed multiple factions applies starting level to each faction.
        /// </summary>
        [Test]
        public void Seed_MultipleFactions_AppliesStartingLevelToEachFaction()
        {
            Faction empire = new Faction { InstanceID = "FNEMP1" };
            Faction alliance = new Faction { InstanceID = "FNALL1" };

            new FactionSeeder().Seed(
                BuildContext(new[] { empire, alliance }, startingResearchLevel: 3)
            );

            Assert.AreEqual(3, empire.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(3, alliance.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
        }

        /// <summary>
        /// Builds context.
        /// </summary>
        /// <param name="factions">The factions.</param>
        /// <param name="startingResearchLevel">The starting research level.</param>
        /// <returns>The constructed context.</returns>
        private static GenerationContext BuildContext(Faction[] factions, int startingResearchLevel)
        {
            GenerationContext ctx = GenerationContextFactory.CreateDefault();
            ctx.Factions = factions;
            ctx.Summary.StartingResearchLevel = startingResearchLevel;
            return ctx;
        }
    }
}
