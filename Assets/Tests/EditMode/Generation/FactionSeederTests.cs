using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class FactionSeederTests
    {
        private static GenerationContext BuildContext(Faction[] factions, int startingResearchLevel)
        {
            return new GenerationContext
            {
                Factions = factions,
                Summary = new GameSummary { StartingResearchLevel = startingResearchLevel },
                Buildings = Array.Empty<Building>(),
                CapitalShips = Array.Empty<CapitalShip>(),
                Starfighters = Array.Empty<Starfighter>(),
                Regiments = Array.Empty<Regiment>(),
                SpecialForces = Array.Empty<SpecialForces>(),
            };
        }

        [Test]
        public void Seed_NonZeroStartingLevel_AppliesToEachResearchDiscipline()
        {
            Faction faction = new Faction { InstanceID = "FNEMP1" };

            new FactionSeeder().Seed(BuildContext(new[] { faction }, startingResearchLevel: 5));

            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign));
            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.TroopTraining));
        }

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
    }
}
