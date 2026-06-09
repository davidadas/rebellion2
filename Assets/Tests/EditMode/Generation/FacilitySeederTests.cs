using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class FacilitySeederTests
    {
        private static List<Building> SeedFacilities(
            PlanetSystem[] systems,
            Building[] templates,
            GameGenerationConfig config,
            GalaxyClassificationResult classification,
            IRandomNumberProvider rng
        )
        {
            GenerationContext ctx = new GenerationContext
            {
                Systems = systems,
                Buildings = templates,
                Config = config,
                Classification = classification,
                Rng = rng,
            };
            new FacilitySeeder().Seed(ctx);
            return ctx.DeployedBuildings;
        }

        /// <summary>
        /// Records the (min, max) of every NextInt call and returns min, so the test
        /// can assert the facility seeder stays inside the facility table range.
        /// </summary>
        private class RecordingRNG : IRandomNumberProvider
        {
            public List<(int min, int max)> IntCalls { get; } = new List<(int min, int max)>();

            public int NextInt(int min, int max)
            {
                IntCalls.Add((min, max));
                return min;
            }

            public double NextDouble() => 0.0;
        }

        private Building[] CreateTemplates()
        {
            return new[]
            {
                new Building { TypeID = "BDFA04", BuildingType = BuildingType.Mine },
                new Building { TypeID = "BDFA05", BuildingType = BuildingType.Refinery },
                new Building { TypeID = "BDFA03", BuildingType = BuildingType.Shipyard },
                new Building { TypeID = "BDFA02", BuildingType = BuildingType.TrainingFacility },
                new Building
                {
                    TypeID = "BDFA01",
                    BuildingType = BuildingType.ConstructionFacility,
                },
                new Building { TypeID = "BDDF02", BuildingType = BuildingType.Defense },
                new Building { TypeID = "BDDF01", BuildingType = BuildingType.Defense },
                new Building { TypeID = "BDDF03", BuildingType = BuildingType.Defense },
            };
        }

        private GameGenerationConfig CreateRules()
        {
            return new GameGenerationConfig
            {
                FacilityGeneration = new FacilityGenerationSection
                {
                    CoreMineMultiplier = 4,
                    RimMineMultiplier = 2,
                    MineTypeID = "BDFA04",
                    CoreFacilityTable = new List<WeightedFacilityEntry>
                    {
                        new WeightedFacilityEntry { CumulativeWeight = 0 },
                        new WeightedFacilityEntry { CumulativeWeight = 36, TypeID = "BDFA05" },
                        new WeightedFacilityEntry { CumulativeWeight = 79, TypeID = "BDFA03" },
                        new WeightedFacilityEntry { CumulativeWeight = 82, TypeID = "BDFA02" },
                        new WeightedFacilityEntry { CumulativeWeight = 85, TypeID = "BDFA01" },
                        new WeightedFacilityEntry { CumulativeWeight = 88, TypeID = "BDDF02" },
                        new WeightedFacilityEntry { CumulativeWeight = 96, TypeID = "BDDF01" },
                        new WeightedFacilityEntry { CumulativeWeight = 99, TypeID = "BDDF03" },
                    },
                    RimFacilityTable = new List<WeightedFacilityEntry>
                    {
                        new WeightedFacilityEntry { CumulativeWeight = 0 },
                        new WeightedFacilityEntry { CumulativeWeight = 91, TypeID = "BDFA05" },
                        new WeightedFacilityEntry { CumulativeWeight = 96, TypeID = "BDFA03" },
                        new WeightedFacilityEntry { CumulativeWeight = 97, TypeID = "BDFA02" },
                        new WeightedFacilityEntry { CumulativeWeight = 98, TypeID = "BDFA01" },
                        new WeightedFacilityEntry { CumulativeWeight = 99, TypeID = "BDDF02" },
                        new WeightedFacilityEntry { CumulativeWeight = 100, TypeID = "BDDF01" },
                    },
                    HQLoadouts = new List<HQFacilityLoadout>(),
                },
            };
        }

        private PlanetSystem CreateCoreSystem(int energy, int rawNodes)
        {
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(
                new Planet
                {
                    InstanceID = "p1",
                    TypeID = "p1",
                    OwnerInstanceID = "FNALL1",
                    IsColonized = true,
                    EnergyCapacity = energy,
                    NumRawResourceNodes = rawNodes,
                }
            );
            return system;
        }

        private PlanetSystem CreateRimSystem(int energy, int rawNodes, bool colonized)
        {
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "rim1",
                SystemType = PlanetSystemType.OuterRim,
            };
            system.Planets.Add(
                new Planet
                {
                    InstanceID = "rp1",
                    OwnerInstanceID = colonized ? "FNALL1" : null,
                    IsColonized = colonized,
                    EnergyCapacity = energy,
                    NumRawResourceNodes = rawNodes,
                }
            );
            return system;
        }

        [Test]
        public void Seed_CorePlanetWithEnergy_PlacesFacilities()
        {
            PlanetSystem system = CreateCoreSystem(energy: 3, rawNodes: 0);

            List<Building> deployed = SeedFacilities(
                new[] { system },
                CreateTemplates(),
                CreateRules(),
                new GalaxyClassificationResult(),
                new SequenceRNG(new[] { 36, 36, 36 })
            );

            Assert.IsNotEmpty(
                deployed,
                "Core planet with energy should receive at least one facility."
            );
        }

        [Test]
        public void Seed_RimPlanetColonized_PlacesFacilities()
        {
            PlanetSystem system = CreateRimSystem(energy: 3, rawNodes: 0, colonized: true);

            List<Building> deployed = SeedFacilities(
                new[] { system },
                CreateTemplates(),
                CreateRules(),
                new GalaxyClassificationResult(),
                new SequenceRNG(new[] { 91, 91, 91 })
            );

            Assert.IsNotEmpty(deployed, "Colonized rim planet should receive facilities.");
        }

        [Test]
        public void Seed_RimPlanetUncolonized_PlacesNoFacilities()
        {
            PlanetSystem system = CreateRimSystem(energy: 3, rawNodes: 0, colonized: false);

            List<Building> deployed = SeedFacilities(
                new[] { system },
                CreateTemplates(),
                CreateRules(),
                new GalaxyClassificationResult(),
                new StubRNG()
            );

            Assert.IsEmpty(deployed, "Uncolonized rim planet should be skipped by the seeder.");
        }

        [Test]
        public void Seed_CoreAndRimPlanets_UseTheirRespectiveTables()
        {
            PlanetSystem coreSystem = CreateCoreSystem(energy: 3, rawNodes: 0);
            PlanetSystem rimSystem = CreateRimSystem(energy: 3, rawNodes: 0, colonized: true);

            GameGenerationConfig rules = CreateRules();
            rules.FacilityGeneration.CoreFacilityTable = new List<WeightedFacilityEntry>
            {
                new WeightedFacilityEntry { CumulativeWeight = 100, TypeID = "BDFA01" },
            };
            rules.FacilityGeneration.RimFacilityTable = new List<WeightedFacilityEntry>
            {
                new WeightedFacilityEntry { CumulativeWeight = 100, TypeID = "BDFA02" },
            };

            SeedFacilities(
                new[] { coreSystem, rimSystem },
                CreateTemplates(),
                rules,
                new GalaxyClassificationResult(),
                new StubRNG()
            );

            Assert.IsTrue(
                coreSystem.Planets[0].Buildings.All(b => b.TypeID == "BDFA01"),
                "Core planet should draw from the core facility table."
            );
            Assert.IsTrue(
                rimSystem.Planets[0].Buildings.All(b => b.TypeID == "BDFA02"),
                "Rim planet should draw from the rim facility table."
            );
        }

        [Test]
        public void Seed_EmptyFacilityRoll_LeavesSlotEmptyAndContinues()
        {
            PlanetSystem system = CreateCoreSystem(energy: 2, rawNodes: 0);

            List<Building> deployed = SeedFacilities(
                new[] { system },
                CreateTemplates(),
                CreateRules(),
                new GalaxyClassificationResult(),
                new SequenceRNG(new[] { 0, 36 })
            );

            Assert.AreEqual(1, deployed.Count, "Only the non-empty facility roll should place.");
            Assert.AreEqual(
                "BDFA05",
                deployed[0].TypeID,
                "The second facility roll should still execute after an empty result."
            );
        }

        [Test]
        public void Seed_CorePlanet_RollsStayWithinFacilityTableRange()
        {
            PlanetSystem system = CreateCoreSystem(energy: 5, rawNodes: 0);
            RecordingRNG rng = new RecordingRNG();

            SeedFacilities(
                new[] { system },
                CreateTemplates(),
                CreateRules(),
                new GalaxyClassificationResult(),
                rng
            );

            List<(int min, int max)> outOfRange = rng
                .IntCalls.Where(call => call.max > 101)
                .ToList();

            Assert.IsEmpty(
                outOfRange,
                $"Facility seeder rolls must stay within a 101-value space (max<=101); found: {string.Join(", ", outOfRange)}"
            );
        }

        [Test]
        public void Seed_PlanetWithHQLoadout_PlacesConfiguredFacilitiesAfterRandomFacilities()
        {
            PlanetSystem system = CreateCoreSystem(energy: 1, rawNodes: 0);
            GameGenerationConfig rules = CreateRules();
            rules.FacilityGeneration.HQLoadouts = new List<HQFacilityLoadout>
            {
                new HQFacilityLoadout
                {
                    PlanetTypeID = "p1",
                    FacilityTypeIDs = new List<string> { "BDFA01" },
                },
            };

            List<Building> deployed = SeedFacilities(
                new[] { system },
                CreateTemplates(),
                rules,
                new GalaxyClassificationResult(),
                new SequenceRNG(new[] { 36 })
            );

            Assert.AreEqual(
                "BDFA05",
                deployed[0].TypeID,
                "Random facility seeding should run before HQ loadout placement."
            );
            Assert.AreEqual(
                "BDFA01",
                deployed[1].TypeID,
                "HQ loadout Construction Yard should be placed after random facility seeding."
            );
        }

        [Test]
        public void Seed_FactionHQLoadout_ResolvesToAssignedHQ()
        {
            PlanetSystem system = CreateCoreSystem(energy: 5, rawNodes: 0);
            Planet hqPlanet = system.Planets[0];

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.FactionHQs["FNALL1"] = hqPlanet;

            GameGenerationConfig rules = CreateRules();
            rules.FacilityGeneration.HQLoadouts = new List<HQFacilityLoadout>
            {
                new HQFacilityLoadout
                {
                    PlanetTypeID = GameGenerationConfig.FactionHqSentinel,
                    FactionID = "FNALL1",
                    FacilityTypeIDs = new List<string> { "BDFA01" },
                },
            };

            List<Building> deployed = SeedFacilities(
                new[] { system },
                CreateTemplates(),
                rules,
                classification,
                new StubRNG()
            );

            Assert.IsTrue(
                deployed.Any(b => b.TypeID == "BDFA01"),
                "FACTION_HQ loadout should resolve to the faction's assigned HQ planet."
            );
        }

        [Test]
        public void Seed_HQLoadoutExceedsEnergy_RaisesEnergyCapacity()
        {
            PlanetSystem system = CreateCoreSystem(energy: 2, rawNodes: 0);
            GameGenerationConfig rules = CreateRules();
            rules.FacilityGeneration.HQLoadouts = new List<HQFacilityLoadout>
            {
                new HQFacilityLoadout
                {
                    PlanetTypeID = "p1",
                    FacilityTypeIDs = new List<string> { "BDFA01", "BDFA02", "BDFA03", "BDFA05" },
                },
            };

            SeedFacilities(
                new[] { system },
                CreateTemplates(),
                rules,
                new GalaxyClassificationResult(),
                new StubRNG()
            );

            Planet planet = system.Planets[0];
            Assert.GreaterOrEqual(
                planet.Buildings.Count,
                4,
                "All loadout facilities should be placed, even if initial energy was lower."
            );
            Assert.GreaterOrEqual(
                planet.EnergyCapacity,
                4,
                "EnergyCapacity should be raised to cover the loadout."
            );
        }

        [Test]
        public void Seed_HQLoadoutIncludesMineAboveRawNodes_RaisesRawResourceNodes()
        {
            PlanetSystem system = CreateCoreSystem(energy: 5, rawNodes: 0);
            GameGenerationConfig rules = CreateRules();
            rules.FacilityGeneration.HQLoadouts = new List<HQFacilityLoadout>
            {
                new HQFacilityLoadout
                {
                    PlanetTypeID = "p1",
                    FacilityTypeIDs = new List<string> { "BDFA04", "BDFA04" },
                },
            };

            SeedFacilities(
                new[] { system },
                CreateTemplates(),
                rules,
                new GalaxyClassificationResult(),
                new StubRNG()
            );

            Assert.GreaterOrEqual(
                system.Planets[0].NumRawResourceNodes,
                2,
                "NumRawResourceNodes should be raised to cover every loadout mine."
            );
        }
    }
}
