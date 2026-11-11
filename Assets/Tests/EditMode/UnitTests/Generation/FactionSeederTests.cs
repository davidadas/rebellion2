using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class FactionSeederTests
    {
        [Test]
        public void Seed_NonZeroStartingLevel_AppliesToEachResearchDiscipline()
        {
            Faction faction = new Faction { InstanceID = "FNEMP1" };

            new FactionSeeder().Seed(BuildContext(new[] { faction }, 5));

            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign));
            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(5, faction.GetHighestUnlockedOrder(ResearchDiscipline.TroopTraining));
        }

        [Test]
        public void Seed_MultipleFactions_AppliesStartingLevelToEachFaction()
        {
            Faction empire = new Faction { InstanceID = "FNEMP1" };
            Faction alliance = new Faction { InstanceID = "FNALL1" };

            new FactionSeeder().Seed(BuildContext(new[] { empire, alliance }, 3, 7));

            Assert.AreEqual(3, empire.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(7, alliance.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
        }

        [Test]
        public void Seed_CorePlanets_SetsResearchCostScaleForEveryFaction()
        {
            Faction empire = new Faction { InstanceID = "FNEMP1" };
            Faction alliance = new Faction { InstanceID = "FNALL1" };
            GenerationContext context = BuildContext(new[] { empire, alliance }, 0, 0);
            PlanetSector secondCoreSector = new PlanetSector
            {
                InstanceID = "core-2",
                SectorType = PlanetSectorType.Core,
            };
            secondCoreSector.AddChild(new Planet { InstanceID = "core-planet-2" });
            context.Sectors = new[] { context.Sectors[0], secondCoreSector };

            new FactionSeeder().Seed(context);

            Assert.AreEqual(2, empire.ResearchState.CostScalePercent);
            Assert.AreEqual(2, alliance.ResearchState.CostScalePercent);
        }

        /// <summary>
        /// Builds context.
        /// </summary>
        /// <param name="factions">The factions.</param>
        /// <param name="startingResearchLevels">The starting research level for each faction.</param>
        /// <returns>The constructed context.</returns>
        private static GenerationContext BuildContext(
            Faction[] factions,
            params int[] startingResearchLevels
        )
        {
            GenerationContext ctx = GenerationContextFactory.CreateDefault();
            ctx.Factions = factions;
            PlanetSector coreSector = new PlanetSector
            {
                InstanceID = "core-1",
                SectorType = PlanetSectorType.Core,
            };
            coreSector.AddChild(new Planet { InstanceID = "core-planet-1" });
            ctx.Sectors = new[] { coreSector };
            ctx.Config.GalaxyClassification.FactionSetups = new List<FactionSetup>();
            for (int index = 0; index < factions.Length; index++)
            {
                ctx.Config.GalaxyClassification.FactionSetups.Add(
                    new FactionSetup
                    {
                        FactionID = factions[index].InstanceID,
                        StartingResearchLevel = startingResearchLevels[index],
                    }
                );
            }
            return ctx;
        }
    }
}
