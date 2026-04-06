using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using UnityEngine;

namespace Rebellion.Tests.Generation
{
    [TestFixture]
    public class GameBuilderTests
    {
        private static readonly Lazy<GameRoot[]> LazyGameTestCases = new Lazy<GameRoot[]>(() =>
            new[]
            {
                CreateGame(GameSize.Small, GameDifficulty.Medium, GameVictoryCondition.Conquest),
                CreateGame(GameSize.Medium, GameDifficulty.Medium, GameVictoryCondition.Conquest),
                CreateGame(GameSize.Large, GameDifficulty.Medium, GameVictoryCondition.Conquest),
            }
        );

        private static GameRoot[] GameTestCases => LazyGameTestCases.Value;

        private static GameRoot CreateGame(
            GameSize size,
            GameDifficulty difficulty,
            GameVictoryCondition victoryCondition
        )
        {
            // Create a new GameSummary object with specific configurations.
            GameSummary summary = new GameSummary
            {
                GalaxySize = size,
                Difficulty = difficulty,
                VictoryCondition = victoryCondition,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            // Create a new GameBuilder instance with the summary.
            GameBuilder builder = new GameBuilder(summary);

            // Build the game using the GameBuilder.
            return builder.BuildGame();
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_SetsConsistentOwners(GameRoot game)
        {
            // Traverse the galaxy map to find planets.
            game.Galaxy.Traverse(node =>
            {
                // Skip nodes without an owner.
                if (node.GetOwnerInstanceID() == null)
                {
                    return;
                }

                List<ISceneNode> children = node.GetChildren().ToList();

                // Ensure each child has the same owner as its parent.
                foreach (ISceneNode child in children)
                {
                    Assert.AreEqual(
                        node.GetOwnerInstanceID(),
                        child.GetOwnerInstanceID(),
                        $"Child \"{child.GetDisplayName()}\" should have the same owner as its parent, \"{node.GetDisplayName()}\"."
                    );
                }
            });
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_SetsChildParentRelationships(GameRoot game)
        {
            game.Galaxy.Traverse(node =>
            {
                List<ISceneNode> children = node.GetChildren().ToList();

                foreach (ISceneNode child in children)
                {
                    // Ensure the child has the parent as its parent.
                    Assert.AreEqual(
                        node,
                        child.GetParent(),
                        "Child should have the parent as its parent."
                    );
                }
            });
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_SetsGameSummary(GameRoot game)
        {
            Assert.IsNotNull(game, "Game should not be null.");
            Assert.IsNotNull(game.Summary, "Game summary should not be null.");

            // Check that the game's summary properties are within expected ranges.
            Assert.IsTrue(
                Enum.IsDefined(typeof(GameSize), game.Summary.GalaxySize),
                "GalaxySize should be a valid enum value."
            );
            Assert.IsTrue(
                Enum.IsDefined(typeof(GameDifficulty), game.Summary.Difficulty),
                "Difficulty should be a valid enum value."
            );
            Assert.IsTrue(
                Enum.IsDefined(typeof(GameVictoryCondition), game.Summary.VictoryCondition),
                "VictoryCondition should be a valid enum value."
            );
            Assert.IsTrue(
                Enum.IsDefined(typeof(GameResourceAvailability), game.Summary.ResourceAvailability),
                "ResourceAvailability should be a valid enum value."
            );

            // Check that PlayerFactionID is not null or empty.
            Assert.IsFalse(
                string.IsNullOrEmpty(game.Summary.PlayerFactionID),
                "PlayerFactionID should not be null or empty."
            );
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_SetsFactions(GameRoot game)
        {
            Assert.IsNotNull(game.Factions, "Factions should not be null.");

            // Ensure the game has at least two factions.
            Assert.GreaterOrEqual(
                game.Factions.Count,
                2,
                "Game should have at least two factions."
            );
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_SetsFactionResearchQueues(GameRoot game)
        {
            foreach (Faction faction in game.Factions)
            {
                Assert.IsNotEmpty(faction.ResearchQueue, "Faction should have research queues.");

                foreach (
                    KeyValuePair<ManufacturingType, List<Technology>> entry in faction.ResearchQueue
                )
                {
                    Assert.IsNotEmpty(
                        entry.Value,
                        $"Faction should have technologies in {entry.Key} research queue."
                    );
                }
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_RebuildAfterInitialBuild_PreservesTechnologies(GameRoot game)
        {
            IResourceManager resourceManager = ResourceManager.Instance;
            IManufacturable[] templates = resourceManager
                .GetGameData<Building>()
                .Cast<IManufacturable>()
                .Concat(resourceManager.GetGameData<CapitalShip>())
                .Concat(resourceManager.GetGameData<Starfighter>())
                .Concat(resourceManager.GetGameData<Regiment>())
                .ToArray();

            foreach (Faction faction in game.Factions)
            {
                int techCountBefore = faction.ResearchQueue.Values.SelectMany(q => q).Count();

                Assert.Greater(
                    techCountBefore,
                    0,
                    $"Faction {faction.GetDisplayName()} should have technologies before rebuild."
                );

                faction.RebuildResearchQueues(templates);

                int techCountAfter = faction.ResearchQueue.Values.SelectMany(q => q).Count();

                Assert.Greater(
                    techCountAfter,
                    0,
                    $"Faction {faction.GetDisplayName()} should still have technologies after RebuildResearchQueues."
                );

                Assert.AreEqual(
                    techCountBefore,
                    techCountAfter,
                    $"Faction {faction.GetDisplayName()} should have the same number of technologies after rebuild."
                );
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_RebuildTechnologies_IncludesAllManufacturingTypes(GameRoot game)
        {
            IResourceManager resourceManager = ResourceManager.Instance;
            IManufacturable[] templates = resourceManager
                .GetGameData<Building>()
                .Cast<IManufacturable>()
                .Concat(resourceManager.GetGameData<CapitalShip>())
                .Concat(resourceManager.GetGameData<Starfighter>())
                .Concat(resourceManager.GetGameData<Regiment>())
                .ToArray();

            foreach (Faction faction in game.Factions)
            {
                faction.RebuildResearchQueues(templates);

                Assert.IsTrue(
                    faction.GetUnlockedTechnologies(ManufacturingType.Ship).Count > 0,
                    $"Faction {faction.GetDisplayName()} should have Ship technologies after rebuild."
                );

                Assert.IsTrue(
                    faction.GetUnlockedTechnologies(ManufacturingType.Building).Count > 0,
                    $"Faction {faction.GetDisplayName()} should have Building technologies after rebuild."
                );

                Assert.IsTrue(
                    faction.GetUnlockedTechnologies(ManufacturingType.Troop).Count > 0,
                    $"Faction {faction.GetDisplayName()} should have Troop technologies after rebuild."
                );
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_SetsHQs(GameRoot game)
        {
            // Assert that the game's factions and galaxy map are not null.
            Assert.IsNotNull(game.Factions, "Factions should not be null.");
            Assert.IsNotNull(game.Galaxy, "GalaxyMap should not be null.");

            foreach (Faction faction in game.Factions)
            {
                // Check if the faction has a headquarters on any planet in the galaxy map.
                bool hasHQ = game
                    .Galaxy.PlanetSystems.SelectMany(ps => ps.Planets)
                    .Any(planet =>
                        planet.OwnerInstanceID == faction.InstanceID && planet.IsHeadquarters
                    );

                // Assert that the faction has a headquarters
                Assert.IsTrue(
                    hasHQ,
                    $"Faction {faction.GetDisplayName()} should have a headquarters."
                );
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_AssignsFactionsPlanets(GameRoot game)
        {
            Dictionary<string, List<Planet>> factionPlanets =
                new Dictionary<string, List<Planet>>();

            // Traverse the galaxy map to find planets owned by each faction.
            game.Galaxy.Traverse(node =>
            {
                // Check if the node is a planet and has an owner.
                // If so, add the planet to the faction's list of planets.
                if (node is Planet planet && planet.GetOwnerInstanceID() != null)
                {
                    if (factionPlanets.ContainsKey(planet.OwnerInstanceID))
                    {
                        factionPlanets[planet.OwnerInstanceID].Add(planet);
                    }
                    else
                    {
                        factionPlanets[planet.OwnerInstanceID] = new List<Planet> { planet };
                    }
                }
            });

            foreach (List<Planet> planets in factionPlanets.Values)
            {
                // Ensure the faction has at least one planet.
                Assert.Greater(planets.Count, 1, "Faction should have at least one planet.");
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_DeploysOfficers(GameRoot game)
        {
            List<Officer> officers = new List<Officer>();

            // Traverse the galaxy map to find officers.
            game.Galaxy.Traverse(node =>
            {
                if (node is Officer officer)
                {
                    officers.Add(officer);
                }
            });

            // Ensure the game has at least two officers.
            Assert.Greater(officers.Count, 2, "Game should have at least two officers.");
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_InitializesOfficers(GameRoot game)
        {
            // Traverse the galaxy map to find officers.
            game.Galaxy.Traverse(node =>
            {
                if (node is Officer officer)
                {
                    // Ensure at least one skill is non-zero.
                    bool hasNonZeroSkill = officer.Skills.Values.Any(skillValue => skillValue > 0);
                    Assert.IsTrue(
                        hasNonZeroSkill,
                        $"Officer {officer.GetDisplayName()} should have at least one non-zero skill."
                    );
                }
            });
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_DeploysFleets(GameRoot game)
        {
            Dictionary<string, int> fleetsPerFaction = new Dictionary<string, int>();

            // Traverse the galaxy map to find fleets.
            game.Galaxy.Traverse(node =>
            {
                if (node is Fleet fleet)
                {
                    string ownerInstanceID = fleet.GetOwnerInstanceID();
                    if (fleetsPerFaction.ContainsKey(ownerInstanceID))
                    {
                        fleetsPerFaction[ownerInstanceID]++;
                    }
                    else
                    {
                        fleetsPerFaction[ownerInstanceID] = 1;
                    }
                }
            });

            foreach (Faction faction in game.Factions)
            {
                // Ensure the faction has at least one fleet.
                Assert.IsTrue(
                    fleetsPerFaction.ContainsKey(faction.GetInstanceID()),
                    $"Faction {faction.GetDisplayName()} should have at least one fleet."
                );
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_DeploysAtMostOneFleetPerPlanet(GameRoot game)
        {
            // Traverse the galaxy map to find planets.
            game.Galaxy.Traverse(node =>
            {
                if (node is Planet planet)
                {
                    // Ensure the planet has at most one fleet.
                    Assert.LessOrEqual(
                        planet.GetFleets().Count,
                        1,
                        $"Planet {planet.GetDisplayName()} should have at most one fleet."
                    );
                }
            });
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_DeploysCapitalShips(GameRoot game)
        {
            // Traverse the galaxy map to find fleets.
            game.Galaxy.Traverse(node =>
            {
                if (node is Fleet fleet)
                {
                    bool hasCapitalShips = fleet.GetChildren().Any();

                    // Ensure the fleet has at least one capital ship.
                    Assert.IsTrue(
                        hasCapitalShips,
                        $"Fleet {fleet.InstanceID} should have at least one capital ship."
                    );
                }
            });
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_ValidConfig_SetsGameEvents(GameRoot game)
        {
            // Ensure the game has at least one event in the event pool.
            Assert.GreaterOrEqual(
                game.GetEventPool().Count,
                1,
                "Game should have at most one event in the event pool."
            );
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_FogOfWar_CoreSystemsHaveInitialSnapshotsForNonOwners(GameRoot game)
        {
            foreach (
                PlanetSystem system in game.Galaxy.PlanetSystems.Where(s =>
                    s.SystemType == PlanetSystemType.CoreSystem
                )
            )
            {
                foreach (Faction faction in game.Factions)
                {
                    foreach (Planet planet in system.Planets)
                    {
                        bool isOwner = planet.OwnerInstanceID == faction.InstanceID;
                        if (isOwner)
                            continue; // owner sees live — no snapshot required

                        bool hasSnapshot =
                            faction.Fog.Snapshots.TryGetValue(
                                system.InstanceID,
                                out SystemSnapshot ss
                            ) && ss.Planets.ContainsKey(planet.InstanceID);

                        Assert.IsTrue(
                            hasSnapshot,
                            $"Faction '{faction.GetDisplayName()}' should have an initial snapshot for core planet '{planet.GetDisplayName()}'"
                        );
                    }
                }
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_FogOfWar_OuterRimPlanetsStartUnexplored(GameRoot game)
        {
            GameGenerationRules rules = ResourceManager.Instance.GetConfig<GameGenerationRules>();
            HashSet<(string planetId, string factionId)> visibilityOverrides = new HashSet<(
                string planetId,
                string factionId
            )>(
                rules
                    .GalaxyClassification.FactionSetups.SelectMany(fs =>
                        fs.StartingPlanets ?? new List<StartingPlanet>()
                    )
                    .Where(sp =>
                        !string.IsNullOrEmpty(sp.PlanetInstanceID) && sp.VisibleToFactionIDs != null
                    )
                    .SelectMany(sp =>
                        sp.VisibleToFactionIDs.Select(fid => (sp.PlanetInstanceID, fid))
                    )
            );

            foreach (
                PlanetSystem system in game.Galaxy.PlanetSystems.Where(s =>
                    s.SystemType == PlanetSystemType.OuterRim
                )
            )
            {
                foreach (Faction faction in game.Factions)
                {
                    foreach (Planet planet in system.Planets)
                    {
                        bool isOwner = planet.OwnerInstanceID == faction.InstanceID;
                        if (isOwner)
                            continue;

                        if (visibilityOverrides.Contains((planet.InstanceID, faction.InstanceID)))
                            continue;

                        bool hasSnapshot =
                            faction.Fog.Snapshots.TryGetValue(
                                system.InstanceID,
                                out SystemSnapshot ss
                            ) && ss.Planets.ContainsKey(planet.InstanceID);

                        Assert.IsFalse(
                            hasSnapshot,
                            $"Faction '{faction.GetDisplayName()}' should not have a snapshot for outer rim planet '{planet.GetDisplayName()}' at game start"
                        );
                    }
                }
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_FogOfWar_OuterRimOwnerCanSeeOwnPlanet(GameRoot game)
        {
            FogOfWarSystem fogSystem = new FogOfWarSystem(game);

            foreach (
                PlanetSystem system in game.Galaxy.PlanetSystems.Where(s =>
                    s.SystemType == PlanetSystemType.OuterRim
                )
            )
            {
                foreach (Planet planet in system.Planets.Where(p => p.OwnerInstanceID != null))
                {
                    Faction owner = game.Factions.First(f =>
                        f.InstanceID == planet.OwnerInstanceID
                    );

                    Assert.IsTrue(
                        fogSystem.IsPlanetVisible(planet, owner),
                        $"Owner '{owner.GetDisplayName()}' should be able to see their outer rim planet '{planet.GetDisplayName()}'"
                    );
                }
            }
        }

        [Test, TestCaseSource(nameof(GameTestCases))]
        public void BuildGame_FogOfWar_OuterRimEnemyPlanetNotVisible(GameRoot game)
        {
            FogOfWarSystem fogSystem = new FogOfWarSystem(game);

            foreach (
                PlanetSystem system in game.Galaxy.PlanetSystems.Where(s =>
                    s.SystemType == PlanetSystemType.OuterRim
                )
            )
            {
                foreach (Planet planet in system.Planets.Where(p => p.OwnerInstanceID != null))
                {
                    foreach (
                        Faction other in game.Factions.Where(f =>
                            f.InstanceID != planet.OwnerInstanceID
                        )
                    )
                    {
                        Assert.IsFalse(
                            fogSystem.IsPlanetVisible(planet, other),
                            $"Faction '{other.GetDisplayName()}' should not be able to see enemy outer rim planet '{planet.GetDisplayName()}' at game start"
                        );
                    }
                }
            }
        }
    }
}
