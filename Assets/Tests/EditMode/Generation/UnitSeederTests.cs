using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class UnitSeederTests
    {
        private static GenerationContext BuildContext(
            PlanetSystem[] systems,
            Faction[] factions,
            GameGenerationConfig config,
            GalaxyClassificationResult classification,
            Regiment[] regimentTemplates = null,
            CapitalShip[] shipTemplates = null,
            Starfighter[] fighterTemplates = null
        )
        {
            return new GenerationContext
            {
                Systems = systems,
                Factions = factions,
                Regiments = regimentTemplates ?? new Regiment[0],
                CapitalShips = shipTemplates ?? new CapitalShip[0],
                Starfighters = fighterTemplates ?? new Starfighter[0],
                Config = config,
                Classification = classification,
                GameConfig = new GameConfig
                {
                    Production = new GameConfig.ProductionConfig { RefinementMultiplier = 1 },
                    Planet = new GameConfig.PlanetConfig { MaxPopularSupport = 100 },
                },
                Summary = new GameSummary
                {
                    GalaxySize = GameSize.Small,
                    Difficulty = GameDifficulty.Easy,
                    PlayerFactionID = "FNALL1",
                },
                Rng = new StubRNG(),
            };
        }

        private static Planet OwnedPlanet(string id, string owner, int ownerSupport)
        {
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                IsColonized = true,
            };
            planet.SetPopularSupport(owner, ownerSupport, 100);
            return planet;
        }

        private static PlanetSystem WrapSystem(Planet planet)
        {
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = $"sys_{planet.InstanceID}",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);
            return system;
        }

        [Test]
        public void Seed_UprisingThresholdNotMet_AddsGarrisonTroops()
        {
            Planet planet = OwnedPlanet("p1", "FNEMP1", ownerSupport: 30);
            Faction[] factions = { new Faction { InstanceID = "FNEMP1" } };
            Regiment[] regimentTemplates =
            {
                new Regiment { TypeID = "REEM002", MaintenanceCost = 1 },
            };

            GameGenerationConfig config = new GameGenerationConfig
            {
                GalaxyClassification = new GalaxyClassificationSection
                {
                    FactionSetups = new List<FactionSetup>
                    {
                        new FactionSetup { FactionID = "FNEMP1", GarrisonTroopTypeID = "REEM002" },
                    },
                },
                UnitDeployment = new UnitDeploymentSection
                {
                    UprisingPreventionThreshold = 60,
                    SupportDeficitPerGarrisonTroop = 10,
                    FixedGarrisons = new List<FixedGarrison>(),
                    FixedFleets = new List<FixedFleet>(),
                    FactionBudgets = new List<FactionBudget>(),
                },
            };

            new UnitSeeder().Seed(
                BuildContext(
                    new[] { WrapSystem(planet) },
                    factions,
                    config,
                    new GalaxyClassificationResult(),
                    regimentTemplates: regimentTemplates
                )
            );

            // Deficit = 60 - 30 = 30; per-troop divisor = 10 → 3 troops.
            Assert.AreEqual(3, planet.GetRegimentCount());
        }

        [Test]
        public void Seed_OwnerSupportAtThreshold_NoGarrisonTroops()
        {
            Planet planet = OwnedPlanet("p1", "FNEMP1", ownerSupport: 60);
            Faction[] factions = { new Faction { InstanceID = "FNEMP1" } };
            Regiment[] regimentTemplates =
            {
                new Regiment { TypeID = "REEM002", MaintenanceCost = 1 },
            };

            GameGenerationConfig config = new GameGenerationConfig
            {
                GalaxyClassification = new GalaxyClassificationSection
                {
                    FactionSetups = new List<FactionSetup>
                    {
                        new FactionSetup { FactionID = "FNEMP1", GarrisonTroopTypeID = "REEM002" },
                    },
                },
                UnitDeployment = new UnitDeploymentSection
                {
                    UprisingPreventionThreshold = 60,
                    SupportDeficitPerGarrisonTroop = 10,
                    FixedGarrisons = new List<FixedGarrison>(),
                    FixedFleets = new List<FixedFleet>(),
                    FactionBudgets = new List<FactionBudget>(),
                },
            };

            new UnitSeeder().Seed(
                BuildContext(
                    new[] { WrapSystem(planet) },
                    factions,
                    config,
                    new GalaxyClassificationResult(),
                    regimentTemplates: regimentTemplates
                )
            );

            Assert.AreEqual(0, planet.GetRegimentCount());
        }

        [Test]
        public void Seed_FixedGarrison_PlacesConfiguredTroopsOnNamedPlanet()
        {
            Planet planet = OwnedPlanet("CORUSCANT", "FNEMP1", ownerSupport: 100);
            Faction[] factions = { new Faction { InstanceID = "FNEMP1" } };
            Regiment[] regimentTemplates =
            {
                new Regiment { TypeID = "REEM002", MaintenanceCost = 1 },
            };

            GameGenerationConfig config = new GameGenerationConfig
            {
                GalaxyClassification = new GalaxyClassificationSection
                {
                    FactionSetups = new List<FactionSetup>(),
                },
                UnitDeployment = new UnitDeploymentSection
                {
                    UprisingPreventionThreshold = 0,
                    SupportDeficitPerGarrisonTroop = 10,
                    FixedGarrisons = new List<FixedGarrison>
                    {
                        new FixedGarrison
                        {
                            PlanetInstanceID = "CORUSCANT",
                            FactionID = "FNEMP1",
                            Units = new List<UnitEntry>
                            {
                                new UnitEntry { TypeID = "REEM002", Count = 4 },
                            },
                        },
                    },
                    FixedFleets = new List<FixedFleet>(),
                    FactionBudgets = new List<FactionBudget>(),
                },
            };

            new UnitSeeder().Seed(
                BuildContext(
                    new[] { WrapSystem(planet) },
                    factions,
                    config,
                    new GalaxyClassificationResult(),
                    regimentTemplates: regimentTemplates
                )
            );

            Assert.AreEqual(4, planet.GetRegimentCount());
        }

        [Test]
        public void Seed_FixedGarrisonWithFactionHqSentinel_ResolvesToFactionHq()
        {
            Planet hq = OwnedPlanet("CORUSCANT", "FNEMP1", ownerSupport: 100);
            Faction[] factions = { new Faction { InstanceID = "FNEMP1" } };
            Regiment[] regimentTemplates =
            {
                new Regiment { TypeID = "REEM002", MaintenanceCost = 1 },
            };

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.FactionHQs["FNEMP1"] = hq;

            GameGenerationConfig config = new GameGenerationConfig
            {
                GalaxyClassification = new GalaxyClassificationSection
                {
                    FactionSetups = new List<FactionSetup>(),
                },
                UnitDeployment = new UnitDeploymentSection
                {
                    UprisingPreventionThreshold = 0,
                    SupportDeficitPerGarrisonTroop = 10,
                    FixedGarrisons = new List<FixedGarrison>
                    {
                        new FixedGarrison
                        {
                            PlanetInstanceID = GameGenerationConfig.FactionHqSentinel,
                            FactionID = "FNEMP1",
                            Units = new List<UnitEntry>
                            {
                                new UnitEntry { TypeID = "REEM002", Count = 2 },
                            },
                        },
                    },
                    FixedFleets = new List<FixedFleet>(),
                    FactionBudgets = new List<FactionBudget>(),
                },
            };

            new UnitSeeder().Seed(
                BuildContext(
                    new[] { WrapSystem(hq) },
                    factions,
                    config,
                    classification,
                    regimentTemplates: regimentTemplates
                )
            );

            Assert.AreEqual(2, hq.GetRegimentCount());
        }

        [Test]
        public void Seed_FixedFleet_PlacesConfiguredShipsAsFleetOnPlanet()
        {
            Planet planet = OwnedPlanet("CORUSCANT", "FNEMP1", ownerSupport: 100);
            Faction[] factions = { new Faction { InstanceID = "FNEMP1" } };
            CapitalShip[] shipTemplates =
            {
                new CapitalShip { TypeID = "SDESDS3", MaintenanceCost = 1 },
            };

            GameGenerationConfig config = new GameGenerationConfig
            {
                GalaxyClassification = new GalaxyClassificationSection
                {
                    FactionSetups = new List<FactionSetup>(),
                },
                UnitDeployment = new UnitDeploymentSection
                {
                    UprisingPreventionThreshold = 0,
                    SupportDeficitPerGarrisonTroop = 10,
                    FixedGarrisons = new List<FixedGarrison>(),
                    FixedFleets = new List<FixedFleet>
                    {
                        new FixedFleet
                        {
                            PlanetInstanceID = "CORUSCANT",
                            FactionID = "FNEMP1",
                            SpawnChancePct = 100,
                            Ships = new List<UnitEntry>
                            {
                                new UnitEntry { TypeID = "SDESDS3", Count = 2 },
                            },
                            Cargo = new List<UnitEntry>(),
                        },
                    },
                    FactionBudgets = new List<FactionBudget>(),
                },
            };

            new UnitSeeder().Seed(
                BuildContext(
                    new[] { WrapSystem(planet) },
                    factions,
                    config,
                    new GalaxyClassificationResult(),
                    shipTemplates: shipTemplates
                )
            );

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(1, fleets.Count, "Expected one fleet on the named planet.");
            Assert.AreEqual(2, fleets[0].CapitalShips.Count);
        }
    }
}
