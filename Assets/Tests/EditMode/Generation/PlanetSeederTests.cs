using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.World;
using Rebellion.Generation;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class PlanetSeederTests
    {
        private GalaxyClassificationSection _gc;
        private SystemSupportSection _sup;
        private SystemResourcesSection _res;
        private string[] _factionIds;

        private static void Configure(
            PlanetSystem[] systems,
            GalaxyClassificationResult classification,
            GameGenerationConfig config,
            string[] factionIds,
            IRandomNumberProvider rng
        )
        {
            GenerationContext ctx = new GenerationContext
            {
                Systems = systems,
                Classification = classification,
                Config = config,
                Summary = new GameSummary { StartingFactionIDs = factionIds },
                Rng = rng,
            };
            new PlanetSeeder().Seed(ctx);
        }

        [SetUp]
        public void SetUp()
        {
            _gc = new GalaxyClassificationSection
            {
                FactionSetups = new List<FactionSetup>
                {
                    new FactionSetup
                    {
                        FactionID = "FNALL1",
                        GarrisonTroopTypeID = "REAL002",
                        StartingPlanets = new List<StartingPlanet>
                        {
                            new StartingPlanet { PlanetInstanceID = "YAVIN", Loyalty = 100 },
                            new StartingPlanet
                            {
                                IsHeadquarters = true,
                                Loyalty = 100,
                                PickFromRim = true,
                            },
                        },
                    },
                    new FactionSetup
                    {
                        FactionID = "FNEMP1",
                        GarrisonTroopTypeID = "REEM002",
                        StartingPlanets = new List<StartingPlanet>
                        {
                            new StartingPlanet
                            {
                                PlanetInstanceID = "CORUSCANT",
                                IsHeadquarters = true,
                                Loyalty = 100,
                            },
                        },
                    },
                },
                Profiles = new List<DifficultyProfile>
                {
                    new DifficultyProfile
                    {
                        Name = "Default",
                        Difficulty = -1,
                        FactionBuckets = new List<FactionBucketConfig>
                        {
                            new FactionBucketConfig
                            {
                                FactionID = "FNALL1",
                                StrongPct = 40,
                                WeakPct = 0,
                            },
                            new FactionBucketConfig
                            {
                                FactionID = "FNEMP1",
                                StrongPct = 10,
                                WeakPct = 5,
                            },
                        },
                    },
                },
            };

            _sup = new SystemSupportSection
            {
                Strong = new SupportFormula { Base = 60, Random = 30 },
                Weak = new SupportFormula { Base = 20, Random = 30 },
                Neutral = new SupportFormula { Base = 41, Random = 18 },
                RimSupportRandom = 0,
            };

            _res = new SystemResourcesSection
            {
                Profiles = new List<SystemResourceProfile>
                {
                    new SystemResourceProfile
                    {
                        Availability = GameResourceAvailability.Normal,
                        CoreEnergy = new DiceFormula { Base = 10, Random1 = 4 },
                        RimEnergy = new DiceFormula
                        {
                            Base = 1,
                            Random1 = 9,
                            Random2 = 4,
                        },
                        CoreRawMaterials = new DiceFormula { Base = 5, Random1 = 9 },
                        RimRawMaterials = new DiceFormula { Base = 1, Random1 = 14 },
                        EnergyMin = 0,
                        EnergyMax = 15,
                        RawMaterialsMin = 0,
                        RawMaterialsMax = 15,
                        RimColonizationPct = 31,
                    },
                },
            };

            _factionIds = new[] { "FNALL1", "FNEMP1" };
        }

        private GameGenerationConfig CreateRules()
        {
            return new GameGenerationConfig
            {
                GalaxyClassification = _gc,
                SystemSupport = _sup,
                SystemResources = _res,
            };
        }

        [Test]
        public void Seed_Coruscant_Gets0Alliance100Empire()
        {
            Planet planet = new Planet
            {
                InstanceID = "CORUSCANT",
                IsColonized = true,
                OwnerInstanceID = "FNEMP1",
            };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.BucketMap[planet] = new PlanetBucket
            {
                FactionID = "FNEMP1",
                Strength = BucketStrength.Strong,
            };
            classification.StartingPlanetLoyalty[planet] = 100;

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.AreEqual(0, planet.PopularSupport["FNALL1"]);
            Assert.AreEqual(100, planet.PopularSupport["FNEMP1"]);
        }

        [Test]
        public void Seed_Yavin_Gets100Alliance0Empire()
        {
            Planet planet = new Planet
            {
                InstanceID = "YAVIN",
                IsColonized = true,
                OwnerInstanceID = "FNALL1",
            };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.OuterRim,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.StartingPlanetLoyalty[planet] = 100;

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.AreEqual(100, planet.PopularSupport["FNALL1"]);
            Assert.AreEqual(0, planet.PopularSupport["FNEMP1"]);
        }

        [Test]
        public void Seed_AllianceOwnedRim_Gets100Alliance0Empire()
        {
            Planet planet = new Planet
            {
                InstanceID = "RIM_HQ",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.OuterRim,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.AreEqual(100, planet.PopularSupport["FNALL1"]);
            Assert.AreEqual(0, planet.PopularSupport["FNEMP1"]);
        }

        [Test]
        public void Seed_UnownedRim_Gets50_50()
        {
            Planet planet = new Planet { InstanceID = "RIM_PLANET", IsColonized = true };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.OuterRim,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.AreEqual(50, planet.PopularSupport["FNALL1"]);
            Assert.AreEqual(50, planet.PopularSupport["FNEMP1"]);
        }

        [Test]
        public void Seed_StrongAlliance_RangeIs60To90()
        {
            // StubRNG returns min (0), so support = 60 + 0 = 60
            Planet planet = new Planet { InstanceID = "CORE1", IsColonized = true };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.BucketMap[planet] = new PlanetBucket
            {
                FactionID = "FNALL1",
                Strength = BucketStrength.Strong,
            };

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.AreEqual(60, planet.PopularSupport["FNALL1"]);
            Assert.AreEqual(40, planet.PopularSupport["FNEMP1"]);
        }

        [Test]
        public void Seed_StrongEmpire_GivesEmpire60()
        {
            // StubRNG returns min (0), so support = 60 + 0 = 60 for Empire
            // Alliance gets remainder = 40
            Planet planet = new Planet { InstanceID = "CORE1", IsColonized = true };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.BucketMap[planet] = new PlanetBucket
            {
                FactionID = "FNEMP1",
                Strength = BucketStrength.Strong,
            };

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.AreEqual(40, planet.PopularSupport["FNALL1"]);
            Assert.AreEqual(60, planet.PopularSupport["FNEMP1"]);
        }

        [Test]
        public void Seed_Neutral_RangeIs41To59()
        {
            // StubRNG returns min (0), so support = 41 + 0 = 41
            Planet planet = new Planet { InstanceID = "CORE1", IsColonized = true };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.BucketMap[planet] = new PlanetBucket
            {
                FactionID = null,
                Strength = BucketStrength.Neutral,
            };

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.AreEqual(41, planet.PopularSupport["FNALL1"]);
            Assert.AreEqual(59, planet.PopularSupport["FNEMP1"]);
        }

        [Test]
        public void Seed_CorePlanet_SetsEnergyInRange()
        {
            Planet planet = new Planet { InstanceID = "CORE1", IsColonized = true };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.BucketMap[planet] = new PlanetBucket
            {
                FactionID = null,
                Strength = BucketStrength.Neutral,
            };

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            // Core energy: base=10, random1=4, StubRNG returns min -> 10 + 0 = 10
            Assert.AreEqual(10, planet.EnergyCapacity);
        }

        [Test]
        public void Seed_RawMaterials_ClampedToEnergy()
        {
            // Use a RNG that returns max values to push raw materials above energy
            // SequenceRNG with high int values: energy roll returns max, raw mat roll returns max
            SequenceRNG rng = new SequenceRNG(
                intValues: new[]
                {
                    0, // core energy random1 (10 + 0 = 10)
                    9, // core raw mat random1 (5 + 9 = 14, but clamped to energy=10)
                    50, // colonization (doesn't matter for core)
                    0, // support random
                }
            );

            Planet planet = new Planet { InstanceID = "CORE1", IsColonized = true };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.BucketMap[planet] = new PlanetBucket
            {
                FactionID = null,
                Strength = BucketStrength.Neutral,
            };

            Configure(new[] { system }, classification, CreateRules(), _factionIds, rng);

            Assert.AreEqual(10, planet.EnergyCapacity);
            Assert.LessOrEqual(planet.NumRawResourceNodes, planet.EnergyCapacity);
        }

        [Test]
        public void Seed_CorePlanet_IsAlwaysColonized()
        {
            Planet planet = new Planet { InstanceID = "CORE1", IsColonized = false };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();
            classification.BucketMap[planet] = new PlanetBucket
            {
                FactionID = null,
                Strength = BucketStrength.Neutral,
            };

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.IsTrue(planet.IsColonized);
        }

        [Test]
        public void Seed_RimPlanet_ColonizedAt31Percent()
        {
            // StubRNG returns min (0) for NextInt, so roll=0 < 31 -> colonized
            Planet planet = new Planet { InstanceID = "RIM1", IsColonized = false };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.OuterRim,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();

            Configure(new[] { system }, classification, CreateRules(), _factionIds, new StubRNG());

            Assert.IsTrue(planet.IsColonized);
        }

        [Test]
        public void Seed_RimPlanet_NotColonizedWhenRollAboveThreshold()
        {
            // SequenceRNG: energy rolls, raw mat rolls, then colonization roll = 50 (> 31)
            SequenceRNG rng = new SequenceRNG(
                intValues: new[]
                {
                    0, // rim energy random1
                    0, // rim energy random2
                    0, // rim raw mat random1
                    50, // colonization roll (50 >= 31, not colonized)
                }
            );

            Planet planet = new Planet { InstanceID = "RIM1", IsColonized = false };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.OuterRim,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();

            Configure(new[] { system }, classification, CreateRules(), _factionIds, rng);

            Assert.IsFalse(planet.IsColonized);
        }

        [Test]
        public void Seed_AlreadyColonizedRim_StaysColonized()
        {
            // Even with a high roll, pre-colonized rim planets stay colonized
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 0, 99 });

            Planet planet = new Planet { InstanceID = "RIM1", IsColonized = true };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.OuterRim,
            };
            system.Planets.Add(planet);

            GalaxyClassificationResult classification = new GalaxyClassificationResult();

            Configure(new[] { system }, classification, CreateRules(), _factionIds, rng);

            Assert.IsTrue(planet.IsColonized);
        }

        [Test]
        public void Seed_AbundantAvailability_SelectsMatchingResourceProfile()
        {
            Planet planet = new Planet { InstanceID = "p1" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GameGenerationConfig config = new GameGenerationConfig
            {
                GalaxyClassification = _gc,
                SystemSupport = _sup,
                SystemResources = new SystemResourcesSection
                {
                    Profiles = new List<SystemResourceProfile>
                    {
                        new SystemResourceProfile
                        {
                            Availability = GameResourceAvailability.Normal,
                            CoreEnergy = new DiceFormula { Base = 5 },
                            RimEnergy = new DiceFormula { Base = 5 },
                            CoreRawMaterials = new DiceFormula { Base = 0 },
                            RimRawMaterials = new DiceFormula { Base = 0 },
                            EnergyMin = 0,
                            EnergyMax = 100,
                            RawMaterialsMin = 0,
                            RawMaterialsMax = 100,
                            RimColonizationPct = 0,
                        },
                        new SystemResourceProfile
                        {
                            Availability = GameResourceAvailability.Abundant,
                            CoreEnergy = new DiceFormula { Base = 50 },
                            RimEnergy = new DiceFormula { Base = 50 },
                            CoreRawMaterials = new DiceFormula { Base = 0 },
                            RimRawMaterials = new DiceFormula { Base = 0 },
                            EnergyMin = 0,
                            EnergyMax = 100,
                            RawMaterialsMin = 0,
                            RawMaterialsMax = 100,
                            RimColonizationPct = 0,
                        },
                    },
                },
            };

            GenerationContext ctx = new GenerationContext
            {
                Systems = new[] { system },
                Classification = new GalaxyClassificationResult(),
                Config = config,
                Summary = new GameSummary
                {
                    StartingFactionIDs = _factionIds,
                    ResourceAvailability = GameResourceAvailability.Abundant,
                },
                Rng = new StubRNG(),
            };
            new PlanetSeeder().Seed(ctx);

            Assert.AreEqual(50, planet.EnergyCapacity);
        }

        [Test]
        public void Seed_AvailabilityHasNoProfile_FallsBackToNormalProfile()
        {
            Planet planet = new Planet { InstanceID = "p1" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            system.Planets.Add(planet);

            GameGenerationConfig config = new GameGenerationConfig
            {
                GalaxyClassification = _gc,
                SystemSupport = _sup,
                SystemResources = new SystemResourcesSection
                {
                    Profiles = new List<SystemResourceProfile>
                    {
                        new SystemResourceProfile
                        {
                            Availability = GameResourceAvailability.Normal,
                            CoreEnergy = new DiceFormula { Base = 7 },
                            RimEnergy = new DiceFormula { Base = 7 },
                            CoreRawMaterials = new DiceFormula { Base = 0 },
                            RimRawMaterials = new DiceFormula { Base = 0 },
                            EnergyMin = 0,
                            EnergyMax = 100,
                            RawMaterialsMin = 0,
                            RawMaterialsMax = 100,
                            RimColonizationPct = 0,
                        },
                    },
                },
            };

            GenerationContext ctx = new GenerationContext
            {
                Systems = new[] { system },
                Classification = new GalaxyClassificationResult(),
                Config = config,
                Summary = new GameSummary
                {
                    StartingFactionIDs = _factionIds,
                    ResourceAvailability = GameResourceAvailability.Abundant,
                },
                Rng = new StubRNG(),
            };
            new PlanetSeeder().Seed(ctx);

            Assert.AreEqual(7, planet.EnergyCapacity);
        }
    }
} // namespace Rebellion.Tests.Generation
