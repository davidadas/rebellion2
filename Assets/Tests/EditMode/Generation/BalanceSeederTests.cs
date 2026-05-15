using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class BalanceSeederTests
    {
        private const int MaxSupport = 100;

        private static GenerationContext BuildContext(PlanetSystem system, Faction[] factions)
        {
            return new GenerationContext
            {
                Systems = new[] { system },
                Factions = factions,
                Config = new GameGenerationConfig
                {
                    Balance = new BalanceSection
                    {
                        SupportBoostPerUnit = 2,
                        MaxMilitaryPresenceBoost = 10,
                    },
                },
                GameConfig = new GameConfig
                {
                    Planet = new GameConfig.PlanetConfig { MaxPopularSupport = MaxSupport },
                },
            };
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

        private static PlanetSystem MakeSystem(Planet planet)
        {
            PlanetSystem system = new PlanetSystem { InstanceID = $"sys_{planet.InstanceID}" };
            system.Planets.Add(planet);
            return system;
        }

        [Test]
        public void Seed_HeadquartersPlanet_OwnerSupportPinnedToMax()
        {
            Planet planet = MakePlanet("CORUSCANT", "FNEMP1", isHq: true);
            planet.SetPopularSupport("FNEMP1", 40, MaxSupport);
            PlanetSystem system = MakeSystem(planet);
            Faction[] factions =
            {
                new Faction { InstanceID = "FNEMP1" },
                new Faction { InstanceID = "FNALL1" },
            };

            new BalanceSeeder().Seed(BuildContext(system, factions));

            Assert.AreEqual(MaxSupport, planet.GetPopularSupport("FNEMP1"));
        }

        [Test]
        public void Seed_OwnedPlanetWithMilitaryPresence_BoostsOwnerSupport()
        {
            Planet planet = MakePlanet("p1", "FNALL1");
            planet.SetPopularSupport("FNALL1", 50, MaxSupport);
            planet.AddChild(new Regiment { InstanceID = "r1", OwnerInstanceID = "FNALL1" });
            planet.AddChild(new Regiment { InstanceID = "r2", OwnerInstanceID = "FNALL1" });
            PlanetSystem system = MakeSystem(planet);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };

            new BalanceSeeder().Seed(BuildContext(system, factions));

            Assert.AreEqual(54, planet.GetPopularSupport("FNALL1"));
        }

        [Test]
        public void Seed_HighMilitaryPresence_BoostCappedAtMaxBoost()
        {
            Planet planet = MakePlanet("p1", "FNALL1");
            planet.SetPopularSupport("FNALL1", 50, MaxSupport);
            for (int i = 0; i < 20; i++)
            {
                planet.AddChild(new Regiment { InstanceID = $"r{i}", OwnerInstanceID = "FNALL1" });
            }
            PlanetSystem system = MakeSystem(planet);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };

            new BalanceSeeder().Seed(BuildContext(system, factions));

            Assert.AreEqual(60, planet.GetPopularSupport("FNALL1"));
        }

        [Test]
        public void Seed_UnownedPlanet_NoSupportChange()
        {
            Planet planet = MakePlanet("p1", null);
            planet.SetPopularSupport("FNALL1", 25, MaxSupport);
            PlanetSystem system = MakeSystem(planet);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };

            new BalanceSeeder().Seed(BuildContext(system, factions));

            Assert.AreEqual(25, planet.GetPopularSupport("FNALL1"));
        }
    }
}
