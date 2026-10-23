using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.Reflection;

namespace Rebellion.Tests.Generation
{
    [TestFixture(GameSize.Small)]
    [TestFixture(GameSize.Medium)]
    [TestFixture(GameSize.Large)]
    public class GameBuilderTests
    {
        private readonly GameSize _size;
        private GameRoot _game;

        /// <summary>
        /// Initializes a new instance of the GameBuilderTests class.
        /// </summary>
        /// <param name="size">The size.</param>
        public GameBuilderTests(GameSize size)
        {
            _size = size;
        }

        /// <summary>
        /// Executes one time set up.
        /// </summary>
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

            GameBuilder builder = new GameBuilder(summary, TestContent.Data);
            _game = builder.Build();
        }

        [Test]
        public void Build_ValidConfig_SetsConsistentOwners()
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
        public void Build_ValidConfig_SetsChildParentRelationships()
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
        public void Build_ValidConfig_SetsGameSummary()
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
        public void Build_InputSetupMutatedAfterConstruction_UsesDetachedSnapshot()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Small,
                PlayerFactionID = "FNALL1",
                Seed = 12345,
            };
            GameBuilder builder = new GameBuilder(summary, TestContent.Data);

            summary.GalaxySize = GameSize.Large;
            summary.PlayerFactionID = "FNEMP1";
            GameRoot game = builder.Build();

            Assert.AreNotSame(summary, game.Summary);
            Assert.AreEqual(GameSize.Small, game.Summary.GalaxySize);
            Assert.AreEqual("FNALL1", game.Summary.PlayerFactionID);
            Assert.IsEmpty(summary.StartingFactionIDs);
            Assert.IsNotEmpty(game.Summary.StartingFactionIDs);
        }

        [Test]
        public void Build_CustomGenerationConfig_UsesSettingsForPlanetResources()
        {
            GameGenerationConfig generation = TestContent.Data.GenerationConfig.GetDeepCopy();
            PlanetResourceProfile resources = generation.PlanetResources.Profiles.Single(profile =>
                profile.Availability == GameResourceAvailability.Normal
            );
            resources.EnergyMin = 6;
            resources.EnergyMax = 6;
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                Difficulty = GameDifficulty.Medium,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
                Seed = 12345,
                CustomSettings = new CustomGameSettings { GenerationConfig = generation },
            };

            GameRoot game = new GameBuilder(summary, TestContent.Data).Build();

            foreach (
                Planet planet in game
                    .Galaxy.GetChildren<PlanetSector>()
                    .SelectMany(sector => sector.GetChildren<Planet>())
            )
                Assert.AreEqual(6, planet.EnergyCapacity);
        }

        [Test]
        public void Build_CustomGarrisonRules_AppliesRuntimeConfiguration()
        {
            int authoredThreshold = TestContent.Data.GameConfig.AI.Garrison.SupportThreshold;
            int authoredDivisor = TestContent.Data.GameConfig.AI.Garrison.GarrisonDivisor;
            int authoredMultiplier = TestContent.Data.GameConfig.AI.Garrison.UprisingMultiplier;
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                Difficulty = GameDifficulty.Medium,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
                Seed = 12345,
                CustomSettings = new CustomGameSettings
                {
                    GarrisonRules = new GarrisonRuleSettings
                    {
                        SupportThreshold = 70,
                        SupportDeficitPerRegiment = 5,
                        UprisingMultiplier = 3,
                    },
                },
            };

            GameRoot game = new GameBuilder(summary, TestContent.Data).Build();

            Assert.AreEqual(70, game.Config.AI.Garrison.SupportThreshold);
            Assert.AreEqual(5, game.Config.AI.Garrison.GarrisonDivisor);
            Assert.AreEqual(3, game.Config.AI.Garrison.UprisingMultiplier);
            Assert.AreEqual(
                authoredThreshold,
                TestContent.Data.GameConfig.AI.Garrison.SupportThreshold
            );
            Assert.AreEqual(
                authoredDivisor,
                TestContent.Data.GameConfig.AI.Garrison.GarrisonDivisor
            );
            Assert.AreEqual(
                authoredMultiplier,
                TestContent.Data.GameConfig.AI.Garrison.UprisingMultiplier
            );
        }

        [Test]
        public void Build_CustomMovementRules_AppliesTravelSpeedWithoutMutatingContent()
        {
            double authoredDivisor = TestContent.Data.GameConfig.Movement.DistanceDivisor;
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                Difficulty = GameDifficulty.Medium,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
                Seed = 12345,
                CustomSettings = new CustomGameSettings
                {
                    MovementRules = new MovementRuleSettings { TravelSpeedPercent = 150 },
                },
            };

            GameRoot game = new GameBuilder(summary, TestContent.Data).Build();

            Assert.AreEqual(authoredDivisor * 1.5d, game.Config.Movement.DistanceDivisor, 0.0001d);
            Assert.AreEqual(authoredDivisor, TestContent.Data.GameConfig.Movement.DistanceDivisor);
        }

        [Test]
        public void Build_FactionModifierOverride_AppliesToGeneratedFactionOnly()
        {
            Faction template = TestContent.Data.Factions[0];
            int authored = template.Settings.GarrisonEfficiency;
            FactionSettings settings = template.Settings.GetDeepCopy();
            settings.GarrisonEfficiency = authored + 2;
            GameGenerationConfig generation = TestContent.Data.GenerationConfig.GetDeepCopy();
            generation.FactionModifiers = new List<FactionModifierConfig>
            {
                new FactionModifierConfig { FactionID = template.InstanceID, Settings = settings },
            };
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                Difficulty = GameDifficulty.Medium,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
                Seed = 12345,
                CustomSettings = new CustomGameSettings { GenerationConfig = generation },
            };

            GameRoot game = new GameBuilder(summary, TestContent.Data).Build();

            Assert.AreEqual(
                authored + 2,
                game.GetFactions()
                    .Single(faction => faction.InstanceID == template.InstanceID)
                    .Settings.GarrisonEfficiency
            );
            Assert.AreEqual(authored, template.Settings.GarrisonEfficiency);
        }

        [Test]
        public void Build_DuplicateFactionModifier_ThrowsInvalidOperationException()
        {
            string factionID = TestContent.Data.Factions[0].InstanceID;
            GameGenerationConfig generation = TestContent.Data.GenerationConfig.GetDeepCopy();
            generation.FactionModifiers = new List<FactionModifierConfig>
            {
                new FactionModifierConfig { FactionID = factionID },
                new FactionModifierConfig { FactionID = factionID },
            };
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                PlayerFactionID = "FNALL1",
                CustomSettings = new CustomGameSettings { GenerationConfig = generation },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                new GameBuilder(summary, TestContent.Data).Build()
            );

            StringAssert.Contains("duplicate faction modifier", exception.Message);
        }

        [Test]
        public void Build_ValidConfig_CreatesOnePlayerPerFaction()
        {
            Assert.AreEqual(_game.GetFactions().Count, _game.GetPlayers().Count);
            Assert.AreEqual(
                PlayerControllerType.Human,
                _game.GetFactionPlayer(_game.Summary.PlayerFactionID).ControllerType
            );
            Assert.IsTrue(
                _game
                    .GetPlayers()
                    .Where(player => player.FactionID != _game.Summary.PlayerFactionID)
                    .All(player => player.ControllerType == PlayerControllerType.AI)
            );
        }

        [Test]
        public void Build_ValidConfig_SetsFactions()
        {
            Assert.IsNotNull(_game.GetFactions(), "Factions should not be null.");

            Assert.GreaterOrEqual(
                _game.GetFactions().Count,
                2,
                "Game should have at least two factions."
            );
        }

        [Test]
        public void Build_ValidConfig_SetsFactionResearchCatalog()
        {
            foreach (Faction faction in _game.GetFactions())
            {
                Assert.IsNotEmpty(faction.ResearchCatalog, "Faction should have research catalog.");

                foreach (
                    KeyValuePair<
                        ResearchDiscipline,
                        List<ResearchCatalogEntry>
                    > entry in faction.ResearchCatalog
                )
                {
                    Assert.IsNotEmpty(
                        entry.Value,
                        $"Faction should have technologies in {entry.Key} research catalog."
                    );
                }
            }
        }

        [Test]
        public void Build_AfterRebuild_TechnologiesSurvive()
        {
            IManufacturable[] templates = TestContent
                .Data.Buildings.Cast<IManufacturable>()
                .Concat(TestContent.Data.CapitalShips)
                .Concat(TestContent.Data.Starfighters)
                .Concat(TestContent.Data.Regiments)
                .Concat(TestContent.Data.SpecialForces)
                .ToArray();

            foreach (Faction faction in _game.GetFactions())
            {
                int techCountBefore = faction.ResearchCatalog.Values.Sum(q => q.Count);

                Assert.Greater(
                    techCountBefore,
                    0,
                    $"Faction {faction.GetDisplayName()} should have technologies before rebuild."
                );

                faction.RebuildResearchCatalog(templates);

                int techCountAfter = faction.ResearchCatalog.Values.Sum(q => q.Count);

                Assert.Greater(
                    techCountAfter,
                    0,
                    $"Faction {faction.GetDisplayName()} should still have technologies after RebuildResearchCatalog."
                );

                Assert.AreEqual(
                    techCountBefore,
                    techCountAfter,
                    $"Faction {faction.GetDisplayName()} should have the same number of technologies after rebuild."
                );
            }
        }

        [Test]
        public void Build_RebuildTechnologies_IncludesAllManufacturingTypes()
        {
            IManufacturable[] templates = TestContent
                .Data.Buildings.Cast<IManufacturable>()
                .Concat(TestContent.Data.CapitalShips)
                .Concat(TestContent.Data.Starfighters)
                .Concat(TestContent.Data.Regiments)
                .ToArray();

            foreach (Faction faction in _game.GetFactions())
            {
                faction.RebuildResearchCatalog(templates);

                Assert.IsTrue(
                    faction.GetUnlockedTechnologies(ResearchDiscipline.ShipDesign).Count > 0,
                    $"Faction {faction.GetDisplayName()} should have Ship technologies after rebuild."
                );

                Assert.IsTrue(
                    faction.GetUnlockedTechnologies(ResearchDiscipline.FacilityDesign).Count > 0,
                    $"Faction {faction.GetDisplayName()} should have Building technologies after rebuild."
                );

                Assert.IsTrue(
                    faction.GetUnlockedTechnologies(ResearchDiscipline.TroopTraining).Count > 0,
                    $"Faction {faction.GetDisplayName()} should have Troop technologies after rebuild."
                );
            }
        }

        [Test]
        public void Build_ValidConfig_SetsHQs()
        {
            Assert.IsNotNull(_game.GetFactions(), "Factions should not be null.");
            Assert.IsNotNull(_game.Galaxy, "GalaxyMap should not be null.");

            foreach (Faction faction in _game.GetFactions())
            {
                bool hasHQ = _game
                    .Galaxy.GetChildren<PlanetSector>()
                    .SelectMany(ps => ps.GetChildren<Planet>())
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
        public void Build_ValidConfig_AssignsFactionsPlanets()
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
        public void Build_ColonizedPlanets_DoNotExceedEnergyCapacity()
        {
            foreach (PlanetSector sector in _game.Galaxy.GetChildren<PlanetSector>())
            {
                foreach (Planet planet in sector.GetChildren<Planet>().Where(p => p.IsColonized))
                {
                    Assert.LessOrEqual(
                        planet.GetChildren<Building>().Count,
                        planet.EnergyCapacity,
                        $"Planet {planet.GetDisplayName()} has {planet.GetChildren<Building>().Count} buildings for {planet.EnergyCapacity} energy capacity."
                    );
                }
            }
        }

        [Test]
        public void Build_EachFaction_OwnsAtLeastOneConstructionFacility()
        {
            foreach (Faction faction in _game.GetFactions())
            {
                bool ownsOne = _game
                    .Galaxy.GetChildren<PlanetSector>()
                    .SelectMany(s => s.GetChildren<Planet>())
                    .SelectMany(p => p.GetChildren<Building>())
                    .Any(b =>
                        b.OwnerInstanceID == faction.InstanceID
                        && b.BuildingType == BuildingType.ConstructionFacility
                    );

                Assert.IsTrue(
                    ownsOne,
                    $"Faction '{faction.GetDisplayName()}' should own at least one construction facility."
                );
            }
        }

        [Test]
        public void Build_ValidConfig_DeploysOfficers()
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
        public void Build_ValidConfig_InitializesOfficerSkills()
        {
            _game.Galaxy.Traverse(node =>
            {
                if (node is Officer officer)
                {
                    bool hasNonZeroSkill = officer.Ratings.Values.Any(skillValue => skillValue > 0);
                    Assert.IsTrue(
                        hasNonZeroSkill,
                        $"Officer {officer.GetDisplayName()} should have at least one non-zero skill."
                    );
                }
            });
        }

        [Test]
        public void Build_ValidConfig_DeploysFleets()
        {
            Dictionary<string, int> fleetsPerFaction = new Dictionary<string, int>();

            _game.Galaxy.Traverse(node =>
            {
                if (node is Fleet fleet)
                {
                    string ownerInstanceId = fleet.GetOwnerInstanceID();
                    if (fleetsPerFaction.ContainsKey(ownerInstanceId))
                    {
                        fleetsPerFaction[ownerInstanceId]++;
                    }
                    else
                    {
                        fleetsPerFaction[ownerInstanceId] = 1;
                    }
                }
            });

            foreach (Faction faction in _game.GetFactions())
            {
                Assert.IsTrue(
                    fleetsPerFaction.ContainsKey(faction.GetInstanceID()),
                    $"Faction {faction.GetDisplayName()} should have at least one fleet."
                );
            }
        }

        [Test]
        public void Build_ValidConfig_DeploysMaxOneFleetPerPlanet()
        {
            _game.Galaxy.Traverse(node =>
            {
                if (node is Planet planet)
                {
                    Assert.LessOrEqual(
                        planet.GetChildren<Fleet>().Count,
                        1,
                        $"Planet {planet.GetDisplayName()} should have at most one fleet."
                    );
                }
            });
        }

        [Test]
        public void Build_ValidConfig_DeploysCapitalShips()
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
        public void Build_ValidConfig_SetsGameEvents()
        {
            Assert.GreaterOrEqual(
                _game.GetEventPool().Count,
                1,
                "Game should have at least one event in the event pool."
            );
        }

        [TestCase(GameEventCategory.Story)]
        [TestCase(GameEventCategory.Disaster)]
        public void Build_DisabledEventCategory_ExcludesOnlyThatCategory(
            GameEventCategory disabledCategory
        )
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                PlayerFactionID = "FNALL1",
                Seed = 12345,
                CustomSettings = new CustomGameSettings
                {
                    GenerationConfig = TestContent.Data.GenerationConfig.GetDeepCopy(),
                },
            };
            summary.CustomSettings.GenerationConfig.DisabledEventCategories =
                new List<GameEventCategory> { disabledCategory };

            GameRoot game = new GameBuilder(summary, TestContent.Data).Build();

            Assert.IsFalse(
                game.GetEventPool().Any(gameEvent => gameEvent.Category == disabledCategory)
            );
            foreach (
                GameEvent template in TestContent.Data.GameEvents.Where(gameEvent =>
                    gameEvent.Category != disabledCategory
                )
            )
            {
                Assert.IsTrue(
                    game.GetEventPool()
                        .Any(gameEvent => gameEvent.InstanceID == template.InstanceID)
                );
            }
        }

        [Test]
        public void Build_AllClassifiedEventsDisabled_RetainsUnclassifiedEvents()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                PlayerFactionID = "FNALL1",
                Seed = 12345,
                CustomSettings = new CustomGameSettings
                {
                    GenerationConfig = TestContent.Data.GenerationConfig.GetDeepCopy(),
                },
            };
            summary.CustomSettings.GenerationConfig.DisabledEventCategories =
                new List<GameEventCategory> { GameEventCategory.Story, GameEventCategory.Disaster };

            GameRoot game = new GameBuilder(summary, TestContent.Data).Build();

            Assert.IsNotEmpty(game.GetEventPool());
            Assert.IsTrue(
                game.GetEventPool().All(gameEvent => gameEvent.Category == GameEventCategory.None)
            );
        }

        [Test]
        public void Build_EnabledEventsDependOnDisabledEvent_ExcludesDependentChains()
        {
            GameEvent story = new GameEvent
            {
                InstanceID = "STORY_EVENT",
                Category = GameEventCategory.Story,
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 10 } },
            };
            GameEvent dependent = new GameEvent
            {
                InstanceID = "DEPENDENT_EVENT",
                Category = GameEventCategory.Disaster,
                Schedule = new GameEventSchedule
                {
                    After = new AfterEvent { EventInstanceID = story.InstanceID, DelayTicks = 5 },
                },
            };
            GameEvent transitiveDependent = new GameEvent
            {
                InstanceID = "TRANSITIVE_DEPENDENT_EVENT",
                Schedule = new GameEventSchedule
                {
                    After = new AfterEvent
                    {
                        EventInstanceID = dependent.InstanceID,
                        DelayTicks = 5,
                    },
                },
            };
            GameEvent dependentAfterAll = new GameEvent
            {
                InstanceID = "DEPENDENT_AFTER_ALL_EVENT",
                Schedule = new GameEventSchedule
                {
                    AfterAll = new AfterEvents
                    {
                        Events = new List<EventDependency>
                        {
                            new EventDependency { EventInstanceID = story.InstanceID },
                        },
                    },
                },
            };
            GameEvent dependentAfterAny = new GameEvent
            {
                InstanceID = "DEPENDENT_AFTER_ANY_EVENT",
                Schedule = new GameEventSchedule
                {
                    AfterAny = new AfterEvents
                    {
                        Events = new List<EventDependency>
                        {
                            new EventDependency { EventInstanceID = story.InstanceID },
                        },
                    },
                },
            };
            GameEvent independent = new GameEvent
            {
                InstanceID = "INDEPENDENT_EVENT",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 20 } },
            };
            GameDataCatalog gameData = CopyGameDataWithEvents(
                story,
                dependent,
                transitiveDependent,
                dependentAfterAll,
                dependentAfterAny,
                independent
            );
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                PlayerFactionID = "FNALL1",
                Seed = 12345,
                CustomSettings = new CustomGameSettings
                {
                    GenerationConfig = gameData.GenerationConfig.GetDeepCopy(),
                },
            };
            summary.CustomSettings.GenerationConfig.DisabledEventCategories =
                new List<GameEventCategory> { GameEventCategory.Story };

            GameRoot game = new GameBuilder(summary, gameData).Build();

            CollectionAssert.AreEqual(
                new[] { independent.InstanceID },
                game.GetEventPool().Select(gameEvent => gameEvent.InstanceID).ToArray()
            );
            Assert.DoesNotThrow(() =>
            {
                using GameSession session = new GameSession(game, gameData);
            });
        }

        [Test]
        public void Build_FogOfWar_CoresHaveInitialResourceSnapshotsForNonOwners()
        {
            foreach (
                PlanetSector sector in _game
                    .Galaxy.GetChildren<PlanetSector>()
                    .Where(s => s.SectorType == PlanetSectorType.Core)
            )
            {
                foreach (Faction faction in _game.GetFactions())
                {
                    foreach (Planet planet in sector.GetChildren<Planet>())
                    {
                        bool isOwner = planet.OwnerInstanceID == faction.InstanceID;
                        if (isOwner)
                            continue;

                        Assert.IsTrue(
                            faction.Fog.Snapshots.TryGetValue(
                                sector.InstanceID,
                                out PlanetSectorSnapshot sectorSnapshot
                            ),
                            $"Faction '{faction.GetDisplayName()}' should have an initial snapshot for core sector '{sector.GetDisplayName()}'"
                        );
                        Assert.IsTrue(
                            sectorSnapshot.Planets.TryGetValue(
                                planet.InstanceID,
                                out PlanetSnapshot snapshot
                            ),
                            $"Faction '{faction.GetDisplayName()}' should have an initial snapshot for core planet '{planet.GetDisplayName()}'"
                        );
                        Assert.AreEqual(
                            planet.EnergyCapacity,
                            snapshot.EnergyCapacity,
                            $"Faction '{faction.GetDisplayName()}' should know the initial energy capacity for core planet '{planet.GetDisplayName()}'"
                        );
                        Assert.AreEqual(
                            planet.NumRawResourceNodes,
                            snapshot.NumRawResourceNodes,
                            $"Faction '{faction.GetDisplayName()}' should know the initial raw resources for core planet '{planet.GetDisplayName()}'"
                        );
                    }
                }
            }
        }

        [Test]
        public void Build_FogOfWar_OuterRimPlanetsStartUnexplored()
        {
            GameGenerationConfig rules = TestContent.Data.GenerationConfig;
            Dictionary<string, string> planetInstanceIdsByTypeId = _game
                .Galaxy.GetChildren<PlanetSector>()
                .SelectMany(sector => sector.GetChildren<Planet>())
                .Where(planet => !string.IsNullOrEmpty(planet.TypeID))
                .ToDictionary(planet => planet.TypeID, planet => planet.InstanceID);
            HashSet<(string planetId, string factionId)> visibilityOverrides = new HashSet<(
                string planetId,
                string factionId
            )>(
                rules
                    .GalaxyClassification.FactionSetups.SelectMany(fs =>
                        fs.StartingPlanets ?? new List<StartingPlanet>()
                    )
                    .Where(sp =>
                        !string.IsNullOrEmpty(sp.PlanetTypeID)
                        && sp.VisibleToFactionIDs != null
                        && planetInstanceIdsByTypeId.ContainsKey(sp.PlanetTypeID)
                    )
                    .SelectMany(sp =>
                        sp.VisibleToFactionIDs.Select(fid =>
                            (planetInstanceIdsByTypeId[sp.PlanetTypeID], fid)
                        )
                    )
            );

            foreach (
                PlanetSector sector in _game
                    .Galaxy.GetChildren<PlanetSector>()
                    .Where(s => s.SectorType == PlanetSectorType.OuterRim)
            )
            {
                foreach (Faction faction in _game.GetFactions())
                {
                    foreach (Planet planet in sector.GetChildren<Planet>())
                    {
                        bool isOwner = planet.OwnerInstanceID == faction.InstanceID;
                        if (isOwner)
                            continue;

                        if (visibilityOverrides.Contains((planet.InstanceID, faction.InstanceID)))
                            continue;

                        bool hasSnapshot =
                            faction.Fog.Snapshots.TryGetValue(
                                sector.InstanceID,
                                out PlanetSectorSnapshot ss
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
        public void Build_FogOfWar_OuterRimOwnerCanSeeOwnPlanet()
        {
            FogOfWarQueries fogSystem = new FogOfWarQueries(_game);

            foreach (
                PlanetSector sector in _game
                    .Galaxy.GetChildren<PlanetSector>()
                    .Where(s => s.SectorType == PlanetSectorType.OuterRim)
            )
            {
                foreach (
                    Planet planet in sector
                        .GetChildren<Planet>()
                        .Where(p => p.OwnerInstanceID != null)
                )
                {
                    Faction owner = _game
                        .GetFactions()
                        .First(f => f.InstanceID == planet.OwnerInstanceID);

                    Assert.IsTrue(
                        fogSystem.IsPlanetVisible(planet, owner),
                        $"Owner '{owner.GetDisplayName()}' should be able to see their outer rim planet '{planet.GetDisplayName()}'"
                    );
                }
            }
        }

        [Test]
        public void Build_FogOfWar_OuterRimEnemyPlanetNotVisible()
        {
            FogOfWarQueries fogSystem = new FogOfWarQueries(_game);

            foreach (
                PlanetSector sector in _game
                    .Galaxy.GetChildren<PlanetSector>()
                    .Where(s => s.SectorType == PlanetSectorType.OuterRim)
            )
            {
                foreach (
                    Planet planet in sector
                        .GetChildren<Planet>()
                        .Where(p => p.OwnerInstanceID != null)
                )
                {
                    foreach (
                        Faction other in _game
                            .GetFactions()
                            .Where(f => f.InstanceID != planet.OwnerInstanceID)
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

        /// <summary>
        /// Copies the shared game-data catalog with a replacement event collection.
        /// </summary>
        /// <param name="events">The replacement event definitions.</param>
        /// <returns>A game-data catalog containing the replacement events.</returns>
        private static GameDataCatalog CopyGameDataWithEvents(params GameEvent[] events)
        {
            GameDataCatalog source = TestContent.Data;
            return new GameDataCatalog(
                source.GameConfig,
                source.GenerationConfig,
                source.Factions,
                source.PlanetSectors,
                source.Buildings,
                source.CapitalShips,
                source.Starfighters,
                source.Regiments,
                source.SpecialForces,
                source.Officers,
                events,
                source.MessageDefinitions,
                source.EncyclopediaEntries,
                source.FactionThemes,
                source.OfficerTraits
            );
        }
    }
}
