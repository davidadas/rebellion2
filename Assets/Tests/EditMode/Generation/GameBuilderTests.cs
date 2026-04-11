using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;
using UnityEngine;

namespace Rebellion.Tests.Generation
{
    [TestFixture(GameSize.Small)]
    [TestFixture(GameSize.Medium)]
    [TestFixture(GameSize.Large)]
    public class GameBuilderTests
    {
        private readonly GameSize _size;
        private GameRoot _game;

        public GameBuilderTests(GameSize size)
        {
            _size = size;
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Conquest,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            GameBuilder builder = new GameBuilder(summary);
            _game = builder.BuildGame();
        }

        [Test]
        public void BuildGame_SetsConsistentOwners()
        {
            _game.Galaxy.Traverse(node =>
            {
                if (node.GetOwnerInstanceID() == null)
                {
                    return;
                }

                List<ISceneNode> children = node.GetChildren().ToList();

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

        [Test]
        public void BuildGame_SetsChildParentRelationships()
        {
            _game.Galaxy.Traverse(node =>
            {
                List<ISceneNode> children = node.GetChildren().ToList();

                foreach (ISceneNode child in children)
                {
                    Assert.AreEqual(
                        node,
                        child.GetParent(),
                        "Child should have the parent as its parent."
                    );
                }
            });
        }

        [Test]
        public void BuildGame_SetsGameSummary()
        {
            Assert.IsNotNull(_game, "Game should not be null.");
            Assert.IsNotNull(_game.Summary, "Game summary should not be null.");

            Assert.IsTrue(
                Enum.IsDefined(typeof(GameSize), _game.Summary.GalaxySize),
                "GalaxySize should be a valid enum value."
            );
            Assert.IsTrue(
                Enum.IsDefined(typeof(GameDifficulty), _game.Summary.Difficulty),
                "Difficulty should be a valid enum value."
            );
            Assert.IsTrue(
                Enum.IsDefined(typeof(GameVictoryCondition), _game.Summary.VictoryCondition),
                "VictoryCondition should be a valid enum value."
            );
            Assert.IsTrue(
                Enum.IsDefined(
                    typeof(GameResourceAvailability),
                    _game.Summary.ResourceAvailability
                ),
                "ResourceAvailability should be a valid enum value."
            );

            Assert.IsFalse(
                string.IsNullOrEmpty(_game.Summary.PlayerFactionID),
                "PlayerFactionID should not be null or empty."
            );
        }

        [Test]
        public void BuildGame_SetsFactions()
        {
            Assert.IsNotNull(_game.Factions, "Factions should not be null.");

            Assert.GreaterOrEqual(
                _game.Factions.Count,
                2,
                "Game should have at least two factions."
            );
        }

        [Test]
        public void BuildGame_SetsFactionResearchQueues()
        {
            foreach (Faction faction in _game.Factions)
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

        [Test]
        public void BuildGame_TechnologiesSurviveRebuild()
        {
            IManufacturable[] templates = ResourceManager
                .GetGameData<Building>()
                .Cast<IManufacturable>()
                .Concat(ResourceManager.GetGameData<CapitalShip>())
                .Concat(ResourceManager.GetGameData<Starfighter>())
                .Concat(ResourceManager.GetGameData<Regiment>())
                .ToArray();

            foreach (Faction faction in _game.Factions)
            {
                int techCountBefore = faction.ResearchQueue.Values.Sum(q => q.Count);

                Assert.Greater(
                    techCountBefore,
                    0,
                    $"Faction {faction.GetDisplayName()} should have technologies before rebuild."
                );

                faction.RebuildResearchQueues(templates);

                int techCountAfter = faction.ResearchQueue.Values.Sum(q => q.Count);

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

        [Test]
        public void BuildGame_RebuildTechnologies_IncludesAllManufacturingTypes()
        {
            IManufacturable[] templates = ResourceManager
                .GetGameData<Building>()
                .Cast<IManufacturable>()
                .Concat(ResourceManager.GetGameData<CapitalShip>())
                .Concat(ResourceManager.GetGameData<Starfighter>())
                .Concat(ResourceManager.GetGameData<Regiment>())
                .ToArray();

            foreach (Faction faction in _game.Factions)
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

        [Test]
        public void BuildGame_SetsHQs()
        {
            Assert.IsNotNull(_game.Factions, "Factions should not be null.");
            Assert.IsNotNull(_game.Galaxy, "GalaxyMap should not be null.");

            foreach (Faction faction in _game.Factions)
            {
                bool hasHQ = _game
                    .Galaxy.PlanetSystems.SelectMany(ps => ps.Planets)
                    .Any(planet =>
                        planet.OwnerInstanceID == faction.InstanceID && planet.IsHeadquarters
                    );

                Assert.IsTrue(
                    hasHQ,
                    $"Faction {faction.GetDisplayName()} should have a headquarters."
                );
            }
        }

        [Test]
        public void BuildGame_AssignsFactionsPlanets()
        {
            Dictionary<string, List<Planet>> factionPlanets =
                new Dictionary<string, List<Planet>>();

            _game.Galaxy.Traverse(node =>
            {
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
                Assert.GreaterOrEqual(planets.Count, 1, "Faction should have at least one planet.");
            }
        }

        [Test]
        public void BuildGame_DeploysOfficers()
        {
            List<Officer> officers = new List<Officer>();

            _game.Galaxy.Traverse(node =>
            {
                if (node is Officer officer)
                {
                    officers.Add(officer);
                }
            });

            Assert.GreaterOrEqual(officers.Count, 2, "Game should have at least two officers.");
        }

        [Test]
        public void BuildGame_InitializesOfficers()
        {
            _game.Galaxy.Traverse(node =>
            {
                if (node is Officer officer)
                {
                    bool hasNonZeroSkill = officer.Skills.Values.Any(skillValue => skillValue > 0);
                    Assert.IsTrue(
                        hasNonZeroSkill,
                        $"Officer {officer.GetDisplayName()} should have at least one non-zero skill."
                    );
                }
            });
        }

        [Test]
        public void BuildGame_DeploysFleets()
        {
            Dictionary<string, int> fleetsPerFaction = new Dictionary<string, int>();

            _game.Galaxy.Traverse(node =>
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

            foreach (Faction faction in _game.Factions)
            {
                Assert.IsTrue(
                    fleetsPerFaction.ContainsKey(faction.GetInstanceID()),
                    $"Faction {faction.GetDisplayName()} should have at least one fleet."
                );
            }
        }

        [Test]
        public void BuildGame_DeploysMaxOneFleet()
        {
            _game.Galaxy.Traverse(node =>
            {
                if (node is Planet planet)
                {
                    Assert.LessOrEqual(
                        planet.GetFleets().Count,
                        1,
                        $"Planet {planet.GetDisplayName()} should have at most one fleet."
                    );
                }
            });
        }

        [Test]
        public void BuildGame_DeploysCapitalShips()
        {
            _game.Galaxy.Traverse(node =>
            {
                if (node is Fleet fleet)
                {
                    bool hasCapitalShips = fleet.GetChildren().Any();

                    Assert.IsTrue(
                        hasCapitalShips,
                        $"Fleet {fleet.InstanceID} should have at least one capital ship."
                    );
                }
            });
        }

        [Test]
        public void BuildGame_SetsGameEvents()
        {
            Assert.GreaterOrEqual(
                _game.GetEventPool().Count,
                1,
                "Game should have at least one event in the event pool."
            );
        }

        [Test]
        public void BuildGame_FogOfWar_CoreSystemsHaveInitialSnapshotsForNonOwners()
        {
            foreach (
                PlanetSystem system in _game.Galaxy.PlanetSystems.Where(s =>
                    s.SystemType == PlanetSystemType.CoreSystem
                )
            )
            {
                foreach (Faction faction in _game.Factions)
                {
                    foreach (Planet planet in system.Planets)
                    {
                        bool isOwner = planet.OwnerInstanceID == faction.InstanceID;
                        if (isOwner)
                            continue;

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

        [Test]
        public void BuildGame_FogOfWar_OuterRimPlanetsStartUnexplored()
        {
            GameGenerationRules rules = ResourceManager.GetConfig<GameGenerationRules>();
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
                PlanetSystem system in _game.Galaxy.PlanetSystems.Where(s =>
                    s.SystemType == PlanetSystemType.OuterRim
                )
            )
            {
                foreach (Faction faction in _game.Factions)
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

        [Test]
        public void BuildGame_FogOfWar_OuterRimOwnerCanSeeOwnPlanet()
        {
            FogOfWarSystem fogSystem = new FogOfWarSystem(_game);

            foreach (
                PlanetSystem system in _game.Galaxy.PlanetSystems.Where(s =>
                    s.SystemType == PlanetSystemType.OuterRim
                )
            )
            {
                foreach (Planet planet in system.Planets.Where(p => p.OwnerInstanceID != null))
                {
                    Faction owner = _game.Factions.First(f =>
                        f.InstanceID == planet.OwnerInstanceID
                    );

                    Assert.IsTrue(
                        fogSystem.IsPlanetVisible(planet, owner),
                        $"Owner '{owner.GetDisplayName()}' should be able to see their outer rim planet '{planet.GetDisplayName()}'"
                    );
                }
            }
        }

        [Test]
        public void BuildGame_FogOfWar_OuterRimEnemyPlanetNotVisible()
        {
            FogOfWarSystem fogSystem = new FogOfWarSystem(_game);

            foreach (
                PlanetSystem system in _game.Galaxy.PlanetSystems.Where(s =>
                    s.SystemType == PlanetSystemType.OuterRim
                )
            )
            {
                foreach (Planet planet in system.Planets.Where(p => p.OwnerInstanceID != null))
                {
                    foreach (
                        Faction other in _game.Factions.Where(f =>
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
