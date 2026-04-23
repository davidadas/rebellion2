using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Generation;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class GalaxyClassifierTests
    {
        private Faction[] _factions;
        private GameSummary _summary;

        [SetUp]
        public void SetUp()
        {
            _factions = new[]
            {
                new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" },
                new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" },
            };

            _summary = new GameSummary
            {
                PlayerFactionID = "FNALL1",
                Difficulty = GameDifficulty.Medium,
            };
        }

        private GameGenerationRules CreateRules(
            int allianceStrongPct,
            int allianceWeakPct,
            int empireStrongPct,
            int empireWeakPct
        )
        {
            return new GameGenerationRules
            {
                GalaxyClassification = new GalaxyClassificationSection
                {
                    FactionSetups = new List<FactionSetup>
                    {
                        new FactionSetup
                        {
                            FactionID = "FNALL1",
                            StartingPlanets = new List<StartingPlanet>(),
                        },
                        new FactionSetup
                        {
                            FactionID = "FNEMP1",
                            StartingPlanets = new List<StartingPlanet>(),
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
                                    StrongPct = allianceStrongPct,
                                    WeakPct = allianceWeakPct,
                                },
                                new FactionBucketConfig
                                {
                                    FactionID = "FNEMP1",
                                    StrongPct = empireStrongPct,
                                    WeakPct = empireWeakPct,
                                },
                            },
                        },
                    },
                },
            };
        }

        private PlanetSystem[] CreateCoreGalaxy(int planetCount)
        {
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            for (int i = 0; i < planetCount; i++)
            {
                system.Planets.Add(new Planet { InstanceID = $"p{i}" });
            }
            return new[] { system };
        }

        [Test]
        public void Classify_StrongBucketPlanet_IsAssignedOwnership()
        {
            PlanetSystem[] systems = CreateCoreGalaxy(10);
            GameGenerationRules rules = CreateRules(
                allianceStrongPct: 20,
                allianceWeakPct: 0,
                empireStrongPct: 0,
                empireWeakPct: 0
            );

            GalaxyClassificationResult result = new GalaxyClassifier().Classify(
                systems,
                _factions,
                _summary,
                rules,
                new StubRNG()
            );

            List<Planet> strongPlanets = result
                .BucketMap.Where(kvp => kvp.Value.Strength == BucketStrength.Strong)
                .Select(kvp => kvp.Key)
                .ToList();

            Assert.AreEqual(2, strongPlanets.Count);
            foreach (Planet strongPlanet in strongPlanets)
            {
                Assert.AreEqual("FNALL1", strongPlanet.OwnerInstanceID);
                Assert.IsTrue(strongPlanet.IsColonized);
            }
        }

        [Test]
        public void Classify_WeakBucketPlanet_IsAssignedOwnership()
        {
            PlanetSystem[] systems = CreateCoreGalaxy(10);
            GameGenerationRules rules = CreateRules(
                allianceStrongPct: 0,
                allianceWeakPct: 30,
                empireStrongPct: 0,
                empireWeakPct: 0
            );

            GalaxyClassificationResult result = new GalaxyClassifier().Classify(
                systems,
                _factions,
                _summary,
                rules,
                new StubRNG()
            );

            List<Planet> weakPlanets = result
                .BucketMap.Where(kvp => kvp.Value.Strength == BucketStrength.Weak)
                .Select(kvp => kvp.Key)
                .ToList();

            Assert.AreEqual(
                3,
                weakPlanets.Count,
                "Expected 3 Weak-bucket planets from 30% of 10 core planets."
            );
            foreach (Planet weakPlanet in weakPlanets)
            {
                Assert.AreEqual(
                    "FNALL1",
                    weakPlanet.OwnerInstanceID,
                    $"Weak-bucket planet {weakPlanet.InstanceID} should be owned by its bucket faction."
                );
                Assert.IsTrue(
                    weakPlanet.IsColonized,
                    $"Weak-bucket planet {weakPlanet.InstanceID} should be colonized."
                );
            }
        }

        [Test]
        public void Classify_NeutralBucketPlanet_RemainsUnowned()
        {
            PlanetSystem[] systems = CreateCoreGalaxy(10);
            GameGenerationRules rules = CreateRules(
                allianceStrongPct: 10,
                allianceWeakPct: 10,
                empireStrongPct: 10,
                empireWeakPct: 10
            );

            GalaxyClassificationResult result = new GalaxyClassifier().Classify(
                systems,
                _factions,
                _summary,
                rules,
                new StubRNG()
            );

            List<Planet> neutralPlanets = result
                .BucketMap.Where(kvp => kvp.Value.Strength == BucketStrength.Neutral)
                .Select(kvp => kvp.Key)
                .ToList();

            Assert.AreEqual(
                6,
                neutralPlanets.Count,
                "Expected 6 Neutral planets: 10 - (1+1+1+1) owned buckets."
            );
            foreach (Planet neutralPlanet in neutralPlanets)
            {
                Assert.IsNull(
                    neutralPlanet.OwnerInstanceID,
                    $"Neutral planet {neutralPlanet.InstanceID} should remain unowned."
                );
            }
        }

        [Test]
        public void Classify_ProfileWithStrongAndWeakBuckets_OwnsSumOfBoth()
        {
            PlanetSystem[] systems = CreateCoreGalaxy(20);
            GameGenerationRules rules = CreateRules(
                allianceStrongPct: 20,
                allianceWeakPct: 0,
                empireStrongPct: 25,
                empireWeakPct: 10
            );

            GalaxyClassificationResult result = new GalaxyClassifier().Classify(
                systems,
                _factions,
                _summary,
                rules,
                new StubRNG()
            );

            int allianceOwned = systems
                .SelectMany(s => s.Planets)
                .Count(p => p.OwnerInstanceID == "FNALL1");
            int empireOwned = systems
                .SelectMany(s => s.Planets)
                .Count(p => p.OwnerInstanceID == "FNEMP1");

            Assert.AreEqual(4, allianceOwned, "Alliance should own 4 planets (20% strong of 20).");
            Assert.AreEqual(
                7,
                empireOwned,
                "Empire should own 7 planets (25% strong + 10% weak of 20) — the Weak half was previously dropped."
            );
        }

        [Test]
        public void Classify_StartingPlanetInBucket_PreservesOriginalOwnership()
        {
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            Planet startingPlanet = new Planet { InstanceID = "CORUSCANT" };
            system.Planets.Add(startingPlanet);
            for (int i = 0; i < 9; i++)
            {
                system.Planets.Add(new Planet { InstanceID = $"p{i}" });
            }

            GameGenerationRules rules = CreateRules(
                allianceStrongPct: 0,
                allianceWeakPct: 50,
                empireStrongPct: 0,
                empireWeakPct: 0
            );
            rules
                .GalaxyClassification.FactionSetups[1]
                .StartingPlanets.Add(
                    new StartingPlanet
                    {
                        PlanetInstanceID = "CORUSCANT",
                        IsHeadquarters = true,
                        Loyalty = 100,
                    }
                );

            new GalaxyClassifier().Classify(
                new[] { system },
                _factions,
                _summary,
                rules,
                new StubRNG()
            );

            Assert.AreEqual(
                "FNEMP1",
                startingPlanet.OwnerInstanceID,
                "Starting planet ownership should be preserved even if a bucket would overwrite it."
            );
        }
    }
}
