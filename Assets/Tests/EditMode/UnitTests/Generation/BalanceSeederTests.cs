using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class BalanceSeederTests
    {
        [Test]
        public void Seed_HeadquartersPlanet_OwnerSupportPinnedToMax()
        {
            Planet planet = MakePlanet("CORUSCANT", "FNEMP1", isHq: true);
            planet.SetPopularSupport("FNEMP1", 40);
            PlanetSector sector = MakeSector(planet);
            Faction[] factions =
            {
                new Faction { InstanceID = "FNEMP1" },
                new Faction { InstanceID = "FNALL1" },
            };

            new BalanceSeeder().Seed(BuildContext(sector, factions));

            Assert.AreEqual(100, planet.GetPopularSupport("FNEMP1"));
        }

        [TestCase(2, 0, 0)]
        [TestCase(0, 1, 0)]
        [TestCase(0, 0, 2)]
        [TestCase(2, 1, 2)]
        public void Seed_OwnedPlanetWithMilitaryPresence_PreservesPopularSupport(
            int regiments,
            int fleets,
            int starfighters
        )
        {
            Planet planet = MakePlanet("p1", "FNALL1");
            planet.SetPopularSupport("FNALL1", 50);
            planet.SetPopularSupport("FNEMP1", 50);
            for (int index = 0; index < regiments; index++)
                planet.AddChild(
                    new Regiment { InstanceID = $"regiment-{index}", OwnerInstanceID = "FNALL1" }
                );
            for (int index = 0; index < fleets; index++)
                planet.AddChild(
                    new Fleet { InstanceID = $"fleet-{index}", OwnerInstanceID = "FNALL1" }
                );
            for (int index = 0; index < starfighters; index++)
                planet.AddChild(
                    new Starfighter { InstanceID = $"fighter-{index}", OwnerInstanceID = "FNALL1" }
                );
            PlanetSector sector = MakeSector(planet);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };

            new BalanceSeeder().Seed(BuildContext(sector, factions));

            Assert.AreEqual(50, planet.GetPopularSupport("FNALL1"));
            Assert.AreEqual(50, planet.GetPopularSupport("FNEMP1"));
        }

        [Test]
        public void Seed_PersistedSupportSettings_PreservesPopularSupport()
        {
            Planet planet = MakePlanet("p1", "FNALL1");
            planet.SetPopularSupport("FNALL1", 50);
            planet.AddChild(new Regiment { InstanceID = "r1", OwnerInstanceID = "FNALL1" });
            PlanetSector sector = MakeSector(planet);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };
            GenerationContext context = BuildContext(sector, factions);
            context.Config = SerializationHelper.Deserialize<GameGenerationConfig>(
                @"<GameGenerationConfig>
  <Balance>
    <SupportBoostPerUnit>2147483647</SupportBoostPerUnit>
    <MaxMilitaryPresenceBoost>2147483647</MaxMilitaryPresenceBoost>
  </Balance>
</GameGenerationConfig>"
            );

            new BalanceSeeder().Seed(context);

            Assert.AreEqual(50, planet.GetPopularSupport("FNALL1"));
        }

        [Test]
        public void Seed_UnownedPlanet_NoSupportChange()
        {
            Planet planet = MakePlanet("p1", null);
            planet.SetPopularSupport("FNALL1", 25);
            PlanetSector sector = MakeSector(planet);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };

            new BalanceSeeder().Seed(BuildContext(sector, factions));

            Assert.AreEqual(25, planet.GetPopularSupport("FNALL1"));
        }

        /// <summary>
        /// Builds context.
        /// </summary>
        /// <param name="sector">The sector.</param>
        /// <param name="factions">The factions.</param>
        /// <returns>The constructed context.</returns>
        private static GenerationContext BuildContext(PlanetSector sector, Faction[] factions)
        {
            GenerationContext ctx = GenerationContextFactory.CreateDefault();
            ctx.Sectors = new[] { sector };
            ctx.Factions = factions;
            return ctx;
        }

        /// <summary>
        /// Executes make planet.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="owner">The owner.</param>
        /// <param name="isHq">Whether is hq.</param>
        /// <returns>The result of make planet.</returns>
        private static Planet MakePlanet(string id, string owner, bool isHq = false)
        {
            return new Planet
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                IsColonized = true,
                IsHeadquarters = isHq,
            };
        }

        /// <summary>
        /// Executes make sector.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <returns>The result of make sector.</returns>
        private static PlanetSector MakeSector(Planet planet)
        {
            PlanetSector sector = new PlanetSector { InstanceID = $"sector_{planet.InstanceID}" };
            sector.AddChild(planet);
            return sector;
        }
    }
}
