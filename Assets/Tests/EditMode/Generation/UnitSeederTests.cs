using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
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
            Starfighter[] fighterTemplates = null,
            SpecialForces[] specialForcesTemplates = null
        )
        {
            GenerationContext ctx = GenerationContextFactory.CreateDefault();
            ctx.Systems = systems;
            ctx.Factions = factions;
            ctx.Config = config;
            ctx.Classification = classification;
            ctx.Summary.PlayerFactionID = "FNALL1";
            if (regimentTemplates != null)
                ctx.Regiments = regimentTemplates;
            if (shipTemplates != null)
                ctx.CapitalShips = shipTemplates;
            if (fighterTemplates != null)
                ctx.Starfighters = fighterTemplates;
            if (specialForcesTemplates != null)
                ctx.SpecialForces = specialForcesTemplates;
            return ctx;
        }

        private static Planet OwnedPlanet(string id, string owner, int ownerSupport)
        {
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                IsColonized = true,
            };
            planet.SetPopularSupport(owner, ownerSupport);
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
        public void Seed_FixedGarrisonWithUnknownUnitId_ThrowsInvalidOperationException()
        {
            Planet planet = OwnedPlanet("CORUSCANT", "FNEMP1", ownerSupport: 100);
            Faction[] factions = { new Faction { InstanceID = "FNEMP1" } };

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
                                new UnitEntry { TypeID = "UNKNOWN", Count = 1 },
                            },
                        },
                    },
                    FixedFleets = new List<FixedFleet>(),
                    FactionBudgets = new List<FactionBudget>(),
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                new UnitSeeder().Seed(
                    BuildContext(
                        new[] { WrapSystem(planet) },
                        factions,
                        config,
                        new GalaxyClassificationResult()
                    )
                )
            );

            Assert.That(exception.Message, Does.Contain("UNKNOWN"));
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

        [Test]
        public void Seed_BudgetDifficultyMapping_UsesMappedDifficulty()
        {
            Planet planet = OwnedPlanet("CORUSCANT", "FNEMP1", ownerSupport: 100);
            planet.EnergyCapacity = 8;
            planet.NumRawResourceNodes = 4;
            for (int i = 0; i < 4; i++)
            {
                planet.AddChild(CompleteBuilding($"mine{i}", BuildingType.Mine, "FNEMP1"));
                planet.AddChild(CompleteBuilding($"refinery{i}", BuildingType.Refinery, "FNEMP1"));
            }

            Faction empire = new Faction { InstanceID = "FNEMP1" };
            empire.Settings.RefinementMultiplier = 1;
            Faction[] factions = { empire };
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
                    FixedGarrisons = new List<FixedGarrison>(),
                    FixedFleets = new List<FixedFleet>(),
                    BudgetDifficultyMappings = new List<BudgetDifficultyMapping>
                    {
                        new BudgetDifficultyMapping { Difficulty = 2, BudgetDifficulty = 1 },
                    },
                    FactionBudgets = new List<FactionBudget>
                    {
                        new FactionBudget
                        {
                            FactionID = "FNEMP1",
                            BudgetLevels = new List<BudgetLevel>
                            {
                                new BudgetLevel
                                {
                                    GalaxySize = 0,
                                    Difficulty = 1,
                                    IsAI = true,
                                    Percentage = 100,
                                },
                                new BudgetLevel
                                {
                                    GalaxySize = 0,
                                    Difficulty = 2,
                                    IsAI = true,
                                    Percentage = 0,
                                },
                            },
                            UnitTable = new List<WeightedUnitEntry>
                            {
                                new WeightedUnitEntry
                                {
                                    CumulativeWeight = 100,
                                    Units = new List<UnitEntry>
                                    {
                                        new UnitEntry { TypeID = "REEM002", Count = 1 },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            GenerationContext context = BuildContext(
                new[] { WrapSystem(planet) },
                factions,
                config,
                new GalaxyClassificationResult(),
                regimentTemplates: regimentTemplates
            );
            context.Summary.GalaxySize = GameSize.Small;
            context.Summary.Difficulty = GameDifficulty.Hard;

            new UnitSeeder().Seed(context);

            Assert.AreEqual(4, planet.GetRegimentCount());
        }

        [Test]
        public void Seed_BudgetTableWithSpecialForces_DeploysSpecialForces()
        {
            Planet planet = OwnedPlanet("CORUSCANT", "FNEMP1", ownerSupport: 100);
            planet.EnergyCapacity = 8;
            planet.NumRawResourceNodes = 4;
            for (int i = 0; i < 4; i++)
            {
                planet.AddChild(CompleteBuilding($"mine{i}", BuildingType.Mine, "FNEMP1"));
                planet.AddChild(CompleteBuilding($"refinery{i}", BuildingType.Refinery, "FNEMP1"));
            }

            Faction empire = new Faction { InstanceID = "FNEMP1" };
            empire.Settings.RefinementMultiplier = 1;
            Faction[] factions = { empire };
            SpecialForces[] specialForcesTemplates =
            {
                new SpecialForces { TypeID = "SPAL004", MaintenanceCost = 1 },
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
                    FixedFleets = new List<FixedFleet>(),
                    FactionBudgets = new List<FactionBudget>
                    {
                        new FactionBudget
                        {
                            FactionID = "FNEMP1",
                            BudgetLevels = new List<BudgetLevel>
                            {
                                new BudgetLevel
                                {
                                    GalaxySize = 0,
                                    Difficulty = 0,
                                    IsAI = true,
                                    Percentage = 100,
                                },
                            },
                            UnitTable = new List<WeightedUnitEntry>
                            {
                                new WeightedUnitEntry
                                {
                                    CumulativeWeight = 100,
                                    Units = new List<UnitEntry>
                                    {
                                        new UnitEntry { TypeID = "SPAL004", Count = 1 },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            GenerationContext context = BuildContext(
                new[] { WrapSystem(planet) },
                factions,
                config,
                new GalaxyClassificationResult(),
                specialForcesTemplates: specialForcesTemplates
            );
            context.Summary.GalaxySize = GameSize.Small;
            context.Summary.Difficulty = GameDifficulty.Easy;

            new UnitSeeder().Seed(context);

            List<SpecialForces> specialForces = planet.GetAllSpecialForces();
            Assert.AreEqual(4, specialForces.Count);
            Assert.IsTrue(
                specialForces.All(unit =>
                    unit.TypeID == "SPAL004"
                    && unit.OwnerInstanceID == "FNEMP1"
                    && unit.ManufacturingStatus == ManufacturingStatus.Complete
                    && unit.Movement == null
                )
            );
        }

        private static Building CompleteBuilding(string id, BuildingType buildingType, string owner)
        {
            return new Building
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                BuildingType = buildingType,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
