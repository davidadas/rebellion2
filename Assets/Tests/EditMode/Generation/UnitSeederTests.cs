using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.Util.Common;

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
            SpecialForces[] specialForcesTemplates = null,
            IRandomNumberProvider rng = null
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
            if (rng != null)
                ctx.Rng = rng;
            return ctx;
        }

        private static Planet OwnedPlanet(
            string id,
            string owner,
            int ownerSupport,
            string typeId = null
        )
        {
            Planet planet = new Planet
            {
                InstanceID = id,
                TypeID = typeId ?? id,
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
        public void Seed_FixedGarrison_PlacesConfiguredTroopsOnConfiguredPlanetType()
        {
            Planet planet = OwnedPlanet(
                "CORUSCANT",
                "FNEMP1",
                ownerSupport: 100,
                typeId: "PLSEW05"
            );
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
                            PlanetTypeID = "PLSEW05",
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
                            PlanetTypeID = GameGenerationConfig.FactionHqSentinel,
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
            Planet planet = OwnedPlanet(
                "CORUSCANT",
                "FNEMP1",
                ownerSupport: 100,
                typeId: "PLSEW05"
            );
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
                            PlanetTypeID = "PLSEW05",
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
        public void Seed_FixedFleet_PlacesConfiguredShipsOnConfiguredPlanetType()
        {
            Planet planet = OwnedPlanet(
                "CORUSCANT",
                "FNEMP1",
                ownerSupport: 100,
                typeId: "PLSEW05"
            );
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
                            PlanetTypeID = "PLSEW05",
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
            Assert.AreEqual(1, fleets.Count, "Expected one fleet on the configured planet type.");
            Assert.AreEqual(2, fleets[0].CapitalShips.Count);
        }

        [Test]
        public void Seed_FixedFleetWithTargetPlanets_SelectsOneTargetByTypeId()
        {
            Planet yavin = OwnedPlanet("YAVIN", "FNALL1", ownerSupport: 100, typeId: "PLSUM06");
            Planet hq = OwnedPlanet("ALLIANCE_HQ", "FNALL1", ownerSupport: 100);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };
            CapitalShip[] shipTemplates =
            {
                new CapitalShip { TypeID = "ALCS006", MaintenanceCost = 1 },
                new CapitalShip
                {
                    TypeID = "ALCS003",
                    MaintenanceCost = 1,
                    RegimentCapacity = 2,
                },
            };
            Regiment[] regimentTemplates =
            {
                new Regiment { TypeID = "REAL001", MaintenanceCost = 1 },
            };

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.FactionHQs["FNALL1"] = hq;

            GameGenerationConfig config = CreateFixedFleetTargetConfig();

            new UnitSeeder().Seed(
                BuildContext(
                    new[] { WrapSystem(yavin), WrapSystem(hq) },
                    factions,
                    config,
                    classification,
                    regimentTemplates: regimentTemplates,
                    shipTemplates: shipTemplates,
                    rng: new SequenceRNG(intValues: new[] { 1 })
                )
            );

            Assert.AreEqual(0, yavin.GetFleets().Count);
            Assert.AreEqual(1, hq.GetFleets().Count);
            Assert.AreEqual(2, hq.GetFleets()[0].CapitalShips.Count);
        }

        [Test]
        public void Seed_FixedFleetWithShipEntries_LoadsCargoOntoConfiguredShip()
        {
            Planet yavin = OwnedPlanet("YAVIN", "FNALL1", ownerSupport: 100, typeId: "PLSUM06");
            Planet hq = OwnedPlanet("ALLIANCE_HQ", "FNALL1", ownerSupport: 100);
            Faction[] factions = { new Faction { InstanceID = "FNALL1" } };
            CapitalShip[] shipTemplates =
            {
                new CapitalShip { TypeID = "ALCS006", MaintenanceCost = 1 },
                new CapitalShip
                {
                    TypeID = "ALCS003",
                    MaintenanceCost = 1,
                    RegimentCapacity = 2,
                },
            };
            Regiment[] regimentTemplates =
            {
                new Regiment { TypeID = "REAL001", MaintenanceCost = 1 },
            };

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.FactionHQs["FNALL1"] = hq;

            GameGenerationConfig config = CreateFixedFleetTargetConfig();

            new UnitSeeder().Seed(
                BuildContext(
                    new[] { WrapSystem(yavin), WrapSystem(hq) },
                    factions,
                    config,
                    classification,
                    regimentTemplates: regimentTemplates,
                    shipTemplates: shipTemplates,
                    rng: new SequenceRNG(intValues: new[] { 0 })
                )
            );

            Fleet fleet = yavin.GetFleets()[0];
            CapitalShip corvette = fleet.CapitalShips.First(s => s.TypeID == "ALCS006");
            CapitalShip transport = fleet.CapitalShips.First(s => s.TypeID == "ALCS003");
            Assert.AreEqual(0, corvette.Regiments.Count);
            Assert.AreEqual(2, transport.Regiments.Count);
            Assert.IsTrue(transport.Regiments.All(r => r.TypeID == "REAL001"));
        }

        [Test]
        public void Seed_BudgetUnitTable_UsesPreviousThresholdRow()
        {
            Planet planet = OwnedPlanet("CORUSCANT", "FNEMP1", ownerSupport: 100);
            planet.EnergyCapacity = 2;
            planet.NumRawResourceNodes = 1;
            planet.AddChild(CompleteBuilding("mine0", BuildingType.Mine, "FNEMP1"));
            planet.AddChild(CompleteBuilding("refinery0", BuildingType.Refinery, "FNEMP1"));

            Faction empire = new Faction { InstanceID = "FNEMP1" };
            empire.Settings.RefinementMultiplier = 1;
            empire.Settings.ResourceProcessingPointsPerFacility = 1;
            Faction[] factions = { empire };
            Regiment[] regimentTemplates =
            {
                new Regiment { TypeID = "FIRST", MaintenanceCost = 1 },
                new Regiment { TypeID = "SECOND", MaintenanceCost = 1 },
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
                                    CumulativeWeight = 1,
                                    Units = new List<UnitEntry>
                                    {
                                        new UnitEntry { TypeID = "FIRST", Count = 1 },
                                    },
                                },
                                new WeightedUnitEntry
                                {
                                    CumulativeWeight = 9,
                                    Units = new List<UnitEntry>
                                    {
                                        new UnitEntry { TypeID = "SECOND", Count = 1 },
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
                regimentTemplates: regimentTemplates,
                rng: new SequenceRNG(intValues: new[] { 1 })
            );
            context.Summary.GalaxySize = GameSize.Small;
            context.Summary.Difficulty = GameDifficulty.Easy;

            new UnitSeeder().Seed(context);

            Assert.AreEqual(1, planet.Regiments.Count(r => r.TypeID == "FIRST"));
            Assert.AreEqual(0, planet.Regiments.Count(r => r.TypeID == "SECOND"));
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
            empire.Settings.ResourceProcessingPointsPerFacility = 1;
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
            empire.Settings.ResourceProcessingPointsPerFacility = 1;
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

            List<SpecialForces> specialForces = planet.SpecialForces;
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

        private static GameGenerationConfig CreateFixedFleetTargetConfig()
        {
            return new GameGenerationConfig
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
                            TargetPlanets = new List<string>
                            {
                                "PLSUM06",
                                GameGenerationConfig.FactionHqSentinel,
                            },
                            FactionID = "FNALL1",
                            ShipEntries = new List<FixedFleetShip>
                            {
                                new FixedFleetShip { TypeID = "ALCS006", Count = 1 },
                                new FixedFleetShip
                                {
                                    TypeID = "ALCS003",
                                    Count = 1,
                                    Cargo = new List<UnitEntry>
                                    {
                                        new UnitEntry { TypeID = "REAL001", Count = 2 },
                                    },
                                },
                            },
                        },
                    },
                    FactionBudgets = new List<FactionBudget>(),
                },
            };
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
