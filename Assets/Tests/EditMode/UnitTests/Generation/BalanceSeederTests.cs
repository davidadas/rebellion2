using NUnit.Framework;
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

        [Test]
        public void Seed_OwnedPlanetWithMilitaryPresence_BoostsOwnerSupport()
        {
            Planet planet = MakePlanet("p1", "FNALL1");
            planet.SetPopularSupport("FNALL1", 50);
            planet.AddChild(new Regiment { InstanceID = "r1", OwnerInstanceID = "FNALL1" });
            planet.AddChild(new Regiment { InstanceID = "r2", OwnerInstanceID = "FNALL1" });
            PlanetSector sector = MakeSector(planet);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };

            new BalanceSeeder().Seed(BuildContext(sector, factions));

            Assert.AreEqual(54, planet.GetPopularSupport("FNALL1"));
        }

        [Test]
        public void Seed_HighMilitaryPresence_BoostCappedAtMaxBoost()
        {
            Planet planet = MakePlanet("p1", "FNALL1");
            planet.SetPopularSupport("FNALL1", 50);
            for (int i = 0; i < 20; i++)
            {
                planet.AddChild(new Regiment { InstanceID = $"r{i}", OwnerInstanceID = "FNALL1" });
            }
            PlanetSector sector = MakeSector(planet);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };

            new BalanceSeeder().Seed(BuildContext(sector, factions));

            Assert.AreEqual(60, planet.GetPopularSupport("FNALL1"));
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

        private static GenerationContext BuildContext(PlanetSector sector, Faction[] factions)
        {
            GenerationContext ctx = GenerationContextFactory.CreateDefault();
            ctx.Sectors = new[] { sector };
            ctx.Factions = factions;
            return ctx;
        }

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

        private static PlanetSector MakeSector(Planet planet)
        {
            PlanetSector sector = new PlanetSector { InstanceID = $"sector_{planet.InstanceID}" };
            sector.AddChild(planet);
            return sector;
        }
    }
}
