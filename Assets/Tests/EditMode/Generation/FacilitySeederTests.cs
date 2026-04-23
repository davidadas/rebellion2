using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Generation;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class FacilitySeederTests
    {
        /// <summary>
        /// Records the (min, max) of every NextInt call and returns min, so the test
        /// can assert the facility seeder never rolls past the original table's range.
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

        private GameGenerationRules CreateRules()
        {
            return new GameGenerationRules
            {
                FacilityGeneration = new FacilityGenerationSection
                {
                    CoreMineMultiplier = 4,
                    RimMineMultiplier = 2,
                    MineTypeID = "BDFA04",
                    CoreFacilityTable = new List<WeightedFacilityEntry>
                    {
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
                        new WeightedFacilityEntry { CumulativeWeight = 100, TypeID = "BDFA05" },
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

            List<Building> deployed = new FacilitySeeder().Seed(
                new[] { system },
                CreateTemplates(),
                CreateRules(),
                new GalaxyClassificationResult(),
                new StubRNG()
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

            List<Building> deployed = new FacilitySeeder().Seed(
                new[] { system },
                CreateTemplates(),
                CreateRules(),
                new GalaxyClassificationResult(),
                new StubRNG()
            );

            Assert.IsNotEmpty(deployed, "Colonized rim planet should receive facilities.");
        }

        [Test]
        public void Seed_RimPlanetUncolonized_PlacesNoFacilities()
        {
            PlanetSystem system = CreateRimSystem(energy: 3, rawNodes: 0, colonized: false);

            List<Building> deployed = new FacilitySeeder().Seed(
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

            GameGenerationRules rules = CreateRules();
            rules.FacilityGeneration.CoreFacilityTable = new List<WeightedFacilityEntry>
            {
                new WeightedFacilityEntry { CumulativeWeight = 100, TypeID = "BDFA01" },
            };
            rules.FacilityGeneration.RimFacilityTable = new List<WeightedFacilityEntry>
            {
                new WeightedFacilityEntry { CumulativeWeight = 100, TypeID = "BDFA02" },
            };

            new FacilitySeeder().Seed(
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
        public void Seed_CorePlanet_RollsStayWithinHundredValueRange()
        {
            PlanetSystem system = CreateCoreSystem(energy: 5, rawNodes: 0);
            RecordingRNG rng = new RecordingRNG();

            new FacilitySeeder().Seed(
                new[] { system },
                CreateTemplates(),
                CreateRules(),
                new GalaxyClassificationResult(),
                rng
            );

            List<(int min, int max)> outOfRange = rng
                .IntCalls.Where(call => call.max > 100)
                .ToList();

            Assert.IsEmpty(
                outOfRange,
                $"Facility seeder rolls must stay within a 100-value space (max<=100); found: {string.Join(", ", outOfRange)}"
            );
        }

        [Test]
        public void Seed_PlanetWithHQLoadout_PlacesConfiguredFacilitiesFirst()
        {
            PlanetSystem system = CreateCoreSystem(energy: 5, rawNodes: 0);
            GameGenerationRules rules = CreateRules();
            rules.FacilityGeneration.HQLoadouts = new List<HQFacilityLoadout>
            {
                new HQFacilityLoadout
                {
                    PlanetInstanceID = "p1",
                    FacilityTypeIDs = new List<string> { "BDFA01" },
                },
            };

            List<Building> deployed = new FacilitySeeder().Seed(
                new[] { system },
                CreateTemplates(),
                rules,
                new GalaxyClassificationResult(),
                new StubRNG()
            );

            Assert.IsNotEmpty(deployed, "Facility seeding should produce deployed buildings.");
            Assert.AreEqual(
                "BDFA01",
                deployed[0].TypeID,
                "HQ loadout Construction Yard should be placed before any randomly seeded facilities."
            );
        }

        [Test]
        public void Seed_FactionHQLoadout_ResolvesToAssignedHQ()
        {
            PlanetSystem system = CreateCoreSystem(energy: 5, rawNodes: 0);
            Planet hqPlanet = system.Planets[0];

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.FactionHQs["FNALL1"] = hqPlanet;

            GameGenerationRules rules = CreateRules();
            rules.FacilityGeneration.HQLoadouts = new List<HQFacilityLoadout>
            {
                new HQFacilityLoadout
                {
                    PlanetInstanceID = GameGenerationRules.FactionHqSentinel,
                    FactionID = "FNALL1",
                    FacilityTypeIDs = new List<string> { "BDFA01" },
                },
            };

            List<Building> deployed = new FacilitySeeder().Seed(
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
            GameGenerationRules rules = CreateRules();
            rules.FacilityGeneration.HQLoadouts = new List<HQFacilityLoadout>
            {
                new HQFacilityLoadout
                {
                    PlanetInstanceID = "p1",
                    FacilityTypeIDs = new List<string> { "BDFA01", "BDFA02", "BDFA03", "BDFA05" },
                },
            };

            new FacilitySeeder().Seed(
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
            GameGenerationRules rules = CreateRules();
            rules.FacilityGeneration.HQLoadouts = new List<HQFacilityLoadout>
            {
                new HQFacilityLoadout
                {
                    PlanetInstanceID = "p1",
                    FacilityTypeIDs = new List<string> { "BDFA04", "BDFA04" },
                },
            };

            new FacilitySeeder().Seed(
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
