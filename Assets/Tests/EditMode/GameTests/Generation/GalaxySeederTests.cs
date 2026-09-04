using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Generation;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class GalaxySeederTests
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

        [Test]
        public void Seed_StrongBucketPlanet_IsAssignedOwnership()
        {
            PlanetSector[] sectors = CreateCoreGalaxy(10);
            GameGenerationConfig rules = CreateRules(
                allianceStrongPct: 20,
                allianceWeakPct: 0,
                empireStrongPct: 0,
                empireWeakPct: 0
            );

            GalaxyClassificationResult result = Classify(
                sectors,
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
        public void Seed_WeakBucketPlanet_IsAssignedOwnership()
        {
            PlanetSector[] sectors = CreateCoreGalaxy(10);
            GameGenerationConfig rules = CreateRules(
                allianceStrongPct: 0,
                allianceWeakPct: 30,
                empireStrongPct: 0,
                empireWeakPct: 0
            );

            GalaxyClassificationResult result = Classify(
                sectors,
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
        public void Seed_NeutralBucketPlanet_RemainsUnowned()
        {
            PlanetSector[] sectors = CreateCoreGalaxy(10);
            GameGenerationConfig rules = CreateRules(
                allianceStrongPct: 10,
                allianceWeakPct: 10,
                empireStrongPct: 10,
                empireWeakPct: 10
            );

            GalaxyClassificationResult result = Classify(
                sectors,
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
        public void Seed_DifferentDifficulties_UseSamePlanetOwnershipProfile()
        {
            GameGenerationConfig rules = CreateRules(
                allianceStrongPct: 20,
                allianceWeakPct: 0,
                empireStrongPct: 25,
                empireWeakPct: 10
            );
            DifficultyProfile mediumProfile = rules.GalaxyClassification.Profiles.Single();
            mediumProfile.Name = "Alliance_Medium";
            mediumProfile.PlayerFactionID = "FNALL1";
            mediumProfile.Difficulty = (int)GameDifficulty.Medium;
            rules.GalaxyClassification.Profiles.Insert(
                0,
                CreateProfile("Alliance_Easy", GameDifficulty.Easy, 5, 0, 5, 0)
            );
            rules.GalaxyClassification.Profiles.Add(
                CreateProfile("Alliance_Hard", GameDifficulty.Hard, 5, 0, 50, 20)
            );

            string[] easyOwners = ClassifyForDifficulty(GameDifficulty.Easy);
            string[] mediumOwners = ClassifyForDifficulty(GameDifficulty.Medium);
            string[] hardOwners = ClassifyForDifficulty(GameDifficulty.Hard);

            CollectionAssert.AreEqual(mediumOwners, easyOwners);
            CollectionAssert.AreEqual(mediumOwners, hardOwners);

            string[] ClassifyForDifficulty(GameDifficulty difficulty)
            {
                _summary.Difficulty = difficulty;
                PlanetSector[] sectors = CreateCoreGalaxy(20);
                Classify(sectors, _factions, _summary, rules, new StubRNG());
                return sectors
                    .SelectMany(sector => sector.GetChildren<Planet>())
                    .OrderBy(planet => planet.InstanceID)
                    .Select(planet => planet.OwnerInstanceID)
                    .ToArray();
            }
        }

        [Test]
        public void Seed_ProfileWithStrongAndWeakBuckets_OwnsSumOfBoth()
        {
            PlanetSector[] sectors = CreateCoreGalaxy(20);
            GameGenerationConfig rules = CreateRules(
                allianceStrongPct: 20,
                allianceWeakPct: 0,
                empireStrongPct: 25,
                empireWeakPct: 10
            );

            GalaxyClassificationResult result = Classify(
                sectors,
                _factions,
                _summary,
                rules,
                new StubRNG()
            );

            int allianceOwned = sectors
                .SelectMany(s => s.GetChildren<Planet>())
                .Count(p => p.OwnerInstanceID == "FNALL1");
            int empireOwned = sectors
                .SelectMany(s => s.GetChildren<Planet>())
                .Count(p => p.OwnerInstanceID == "FNEMP1");

            Assert.AreEqual(4, allianceOwned, "Alliance should own 4 planets (20% strong of 20).");
            Assert.AreEqual(
                7,
                empireOwned,
                "Empire should own 7 planets (25% strong + 10% weak of 20) — the Weak half was previously dropped."
            );
        }

        [Test]
        public void Seed_StartingPlanetInBucket_PreservesOriginalOwnership()
        {
            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            Planet startingPlanet = new Planet { InstanceID = "CORUSCANT", TypeID = "PLSEW05" };
            sector.AddChild(startingPlanet);
            for (int i = 0; i < 9; i++)
            {
                sector.AddChild(new Planet { InstanceID = $"p{i}" });
            }

            GameGenerationConfig rules = CreateRules(
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
                        PlanetTypeID = "PLSEW05",
                        IsHeadquarters = true,
                        Loyalty = 100,
                    }
                );

            Classify(new[] { sector }, _factions, _summary, rules, new StubRNG());

            Assert.AreEqual(
                "FNEMP1",
                startingPlanet.OwnerInstanceID,
                "Starting planet ownership should be preserved even if a bucket would overwrite it."
            );
        }

        private GameGenerationConfig CreateRules(
            int allianceStrongPct,
            int allianceWeakPct,
            int empireStrongPct,
            int empireWeakPct
        )
        {
            return new GameGenerationConfig
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

        private static DifficultyProfile CreateProfile(
            string name,
            GameDifficulty difficulty,
            int allianceStrongPct,
            int allianceWeakPct,
            int empireStrongPct,
            int empireWeakPct
        )
        {
            return new DifficultyProfile
            {
                Name = name,
                PlayerFactionID = "FNALL1",
                Difficulty = (int)difficulty,
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
            };
        }

        private static GalaxyClassificationResult Classify(
            PlanetSector[] sectors,
            Faction[] factions,
            GameSummary summary,
            GameGenerationConfig config,
            IRandomNumberProvider rng
        )
        {
            GenerationContext ctx = new GenerationContext
            {
                Sectors = sectors,
                Factions = factions,
                Summary = summary,
                Config = config,
                Rng = rng,
            };
            new GalaxySeeder().Seed(ctx);
            return ctx.Classification;
        }

        private PlanetSector[] CreateCoreGalaxy(int planetCount)
        {
            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            for (int i = 0; i < planetCount; i++)
            {
                sector.AddChild(new Planet { InstanceID = $"p{i}", TypeID = $"p{i}" });
            }
            return new[] { sector };
        }
    }
}
