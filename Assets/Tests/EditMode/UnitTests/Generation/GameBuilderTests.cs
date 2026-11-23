using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Messages;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Generation;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Generation
{
    [TestFixture(GameSize.Small)]
    [TestFixture(GameSize.Medium)]
    [TestFixture(GameSize.Large)]
    public class GameBuilderTests
    {
        private readonly GameSize _size;
        private GameRoot _game;
        private string _folder;
        private GameDataCatalog _data;

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
            _folder = Path.Combine(
                Path.GetTempPath(),
                "game-builder-tests-" + Guid.NewGuid().ToString("N")
            );
            _data = CreateCatalog(TestPackFixture.Install(_folder).GameData);
            GameSummary summary = new GameSummary
            {
                GalaxySize = _size,
                Difficulty = GameDifficulty.Medium,
                Seed = 12345,
                VictoryCondition = GameVictoryCondition.Conquest,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            GameBuilder builder = new GameBuilder(summary, _data);
            _game = builder.Build();
        }

        /// <summary>
        /// Removes the temporary generation pack.
        /// </summary>
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [Test]
        public void Build_EditingFactionRules_DoesNotChangeContentDefinition()
        {
            Faction definition = _data.Factions[0];
            string before = SerializationHelper.Serialize(definition);
            GameRoot game = new GameBuilder(
                new GameSummary
                {
                    GalaxySize = _size,
                    PlayerFactionID = "FNALL1",
                    Seed = 12345,
                },
                _data
            ).Build();
            Faction faction = game.GetFactions()
                .Single(value => value.InstanceID == definition.InstanceID);

            faction.Settings.Headquarters.FacilityTypeID = "replacement";
            faction.DisallowedMissionTypeIDs.Clear();
            faction.ShipNamePools[0].Names.Clear();
            faction.ShipNamePools[0].NextNameIndex += 1;
            faction.RawMaterialStockpile += 1;
            faction.AdvisorMessageNotifications[MessageType.Conflict] = false;
            faction.PendingRawMaterialFacilityIDs.Add("facility");
            faction
                .Messages[MessageType.Conflict]
                .Add(new StatusMessage(MessageType.Conflict, "Report", "Body"));

            Assert.AreEqual(before, SerializationHelper.Serialize(definition));
        }

        [Test]
        public void Build_AuthoredStartingAdvice_PreservesMessageContent()
        {
            Faction definition = _data.Factions[0];
            GameRoot game = new GameBuilder(
                new GameSummary
                {
                    GalaxySize = _size,
                    PlayerFactionID = "FNALL1",
                    Seed = 12345,
                },
                _data
            ).Build();
            Faction faction = game.GetFactions()
                .Single(value => value.InstanceID == definition.InstanceID);

            CollectionAssert.AreEqual(
                definition
                    .Messages[MessageType.Advice]
                    .Select(message =>
                        (
                            message.Type,
                            message.Title,
                            message.Body,
                            message.BackgroundImageKey,
                            message.Read
                        )
                    )
                    .ToArray(),
                faction
                    .Messages[MessageType.Advice]
                    .Select(message =>
                        (
                            message.Type,
                            message.Title,
                            message.Body,
                            message.BackgroundImageKey,
                            message.Read
                        )
                    )
                    .ToArray()
            );
        }

        [Test]
        public void Build_ReadingStartingAdvice_DoesNotMarkContentDefinitionRead()
        {
            Faction definition = _data.Factions[0];
            bool before = definition.Messages[MessageType.Advice][0].Read;
            GameRoot game = new GameBuilder(
                new GameSummary
                {
                    GalaxySize = _size,
                    PlayerFactionID = "FNALL1",
                    Seed = 12345,
                },
                _data
            ).Build();
            Faction faction = game.GetFactions()
                .Single(value => value.InstanceID == definition.InstanceID);

            faction.Messages[MessageType.Advice][0].Read = !before;

            Assert.AreEqual(before, definition.Messages[MessageType.Advice][0].Read);
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
            GameBuilder builder = new GameBuilder(summary, _data);

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
            GameGenerationConfig generation = _data.GenerationConfig.CreateCopy();
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

            GameRoot game = new GameBuilder(summary, _data).Build();

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
            int authoredThreshold = _data.GameConfig.AI.Garrison.SupportThreshold;
            int authoredDivisor = _data.GameConfig.AI.Garrison.GarrisonDivisor;
            int authoredMultiplier = _data.GameConfig.AI.Garrison.UprisingMultiplier;
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

            GameRoot game = new GameBuilder(summary, _data).Build();

            Assert.AreEqual(70, game.Config.AI.Garrison.SupportThreshold);
            Assert.AreEqual(5, game.Config.AI.Garrison.GarrisonDivisor);
            Assert.AreEqual(3, game.Config.AI.Garrison.UprisingMultiplier);
            Assert.AreEqual(authoredThreshold, _data.GameConfig.AI.Garrison.SupportThreshold);
            Assert.AreEqual(authoredDivisor, _data.GameConfig.AI.Garrison.GarrisonDivisor);
            Assert.AreEqual(authoredMultiplier, _data.GameConfig.AI.Garrison.UprisingMultiplier);
        }

        [Test]
        public void Build_CustomMovementRules_AppliesTravelSpeedWithoutMutatingContent()
        {
            double authoredDivisor = _data.GameConfig.Movement.DistanceDivisor;
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

            GameRoot game = new GameBuilder(summary, _data).Build();

            Assert.AreEqual(authoredDivisor * 1.5d, game.Config.Movement.DistanceDivisor, 0.0001d);
            Assert.AreEqual(authoredDivisor, _data.GameConfig.Movement.DistanceDivisor);
        }

        [Test]
        public void Build_FactionModifierOverride_AppliesToGeneratedFactionOnly()
        {
            Faction template = _data.Factions[0];
            int authored = template.Settings.GarrisonEfficiency;
            FactionSettings settings = template.Settings.CreateCopy();
            settings.GarrisonEfficiency = authored + 2;
            GameGenerationConfig generation = _data.GenerationConfig.CreateCopy();
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

            GameRoot game = new GameBuilder(summary, _data).Build();

            Assert.AreEqual(
                authored + 2,
                game.GetFactions()
                    .Single(faction => faction.InstanceID == template.InstanceID)
                    .Settings.GarrisonEfficiency
            );
            Assert.AreEqual(authored, template.Settings.GarrisonEfficiency);
        }

        [TestCase("FNALL1")]
        [TestCase("FNEMP1")]
        public void Build_EmptyOverrideConquestObjectives_RetainsAuthoredOfficers(string factionID)
        {
            Faction template = _data.Factions.Single(faction => faction.InstanceID == factionID);
            FactionSettings settings = template.Settings.CreateCopy();
            settings.ConquestOfficerInstanceIDs.Clear();
            settings.GarrisonEfficiency++;
            GameGenerationConfig generation = _data.GenerationConfig.CreateCopy();
            generation.FactionModifiers = new List<FactionModifierConfig>
            {
                new FactionModifierConfig { FactionID = factionID, Settings = settings },
            };
            GameRoot game = new GameBuilder(
                new GameSummary
                {
                    GalaxySize = _size,
                    PlayerFactionID = "FNALL1",
                    Seed = 12345,
                    CustomSettings = new CustomGameSettings { GenerationConfig = generation },
                },
                _data
            ).Build();
            Faction actual = game.GetFactions().Single(faction => faction.InstanceID == factionID);

            Assert.IsNotEmpty(template.Settings.ConquestOfficerInstanceIDs);
            CollectionAssert.AreEqual(
                template.Settings.ConquestOfficerInstanceIDs,
                actual.Settings.ConquestOfficerInstanceIDs
            );
            Assert.AreEqual(settings.GarrisonEfficiency, actual.Settings.GarrisonEfficiency);
            Assert.AreNotSame(
                template.Settings.ConquestOfficerInstanceIDs,
                actual.Settings.ConquestOfficerInstanceIDs
            );
            Assert.IsEmpty(settings.ConquestOfficerInstanceIDs);
        }

        [Test]
        public void Build_DuplicateFactionModifier_ThrowsInvalidOperationException()
        {
            string factionID = _data.Factions[0].InstanceID;
            GameGenerationConfig generation = _data.GenerationConfig.CreateCopy();
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
                new GameBuilder(summary, _data).Build()
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
            IManufacturable[] templates = _data
                .Buildings.Cast<IManufacturable>()
                .Concat(_data.CapitalShips)
                .Concat(_data.Starfighters)
                .Concat(_data.Regiments)
                .Concat(_data.SpecialForces)
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
            IManufacturable[] templates = _data
                .Buildings.Cast<IManufacturable>()
                .Concat(_data.CapitalShips)
                .Concat(_data.Starfighters)
                .Concat(_data.Regiments)
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
                    GenerationConfig = _data.GenerationConfig.CreateCopy(),
                },
            };
            summary.CustomSettings.GenerationConfig.DisabledEventCategories =
                new List<GameEventCategory> { disabledCategory };

            GameRoot game = new GameBuilder(summary, _data).Build();

            Assert.IsFalse(
                game.GetEventPool().Any(gameEvent => gameEvent.Category == disabledCategory)
            );
            foreach (
                GameEvent template in _data.GameEvents.Where(gameEvent =>
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
                    GenerationConfig = _data.GenerationConfig.CreateCopy(),
                },
            };
            summary.CustomSettings.GenerationConfig.DisabledEventCategories =
                new List<GameEventCategory> { GameEventCategory.Story, GameEventCategory.Disaster };

            GameRoot game = new GameBuilder(summary, _data).Build();

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
                    GenerationConfig = gameData.GenerationConfig.CreateCopy(),
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
            GameGenerationConfig rules = _data.GenerationConfig;
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
        private GameDataCatalog CopyGameDataWithEvents(params GameEvent[] events)
        {
            return CopyCatalog(_data, _data.PlanetSectors, events);
        }

        /// <summary>
        /// Adds test starting forces, an Outer Rim sector, and events to the fixture catalog.
        /// </summary>
        /// <param name="source">The catalog loaded from the fixture pack.</param>
        /// <returns>The catalog used by the generation tests.</returns>
        private static GameDataCatalog CreateCatalog(GameDataCatalog source)
        {
            GameGenerationConfig generation = source.GenerationConfig;
            generation.Officers.StartingOfficers.Add(
                new StartingOfficerRule
                {
                    OfficerInstanceID = "EMPEROR_PALPATINE",
                    DestinationTypeID = "FACTION_HQ",
                }
            );
            foreach (StartingOfficerRule officer in generation.Officers.StartingOfficers)
            {
                Faction faction = source.Factions.Single(candidate =>
                    candidate.InstanceID
                    == source
                        .Officers.Single(template =>
                            template.InstanceID == officer.OfficerInstanceID
                        )
                        .OwnerInstanceID
                );
                faction.Settings.ConquestOfficerInstanceIDs.Add(officer.OfficerInstanceID);
                faction.Messages.TryAdd(MessageType.Conflict, new List<Message>());
            }

            generation.FacilityGeneration.HQLoadouts.Add(
                new HQFacilityLoadout
                {
                    PlanetTypeID = "PLSEW02",
                    FacilityTypeIDs = new List<string> { "BDFA01" },
                }
            );
            generation.UnitDeployment.FixedFleets = new List<FixedFleet>
            {
                new FixedFleet
                {
                    PlanetTypeID = "PLSEW01",
                    FactionID = "FNALL1",
                    SpawnChancePct = 100,
                    ShipEntries = new List<FixedFleetShip>
                    {
                        new FixedFleetShip { TypeID = "CSAL001", Count = 1 },
                    },
                },
                new FixedFleet
                {
                    PlanetTypeID = "PLSEW02",
                    FactionID = "FNEMP1",
                    SpawnChancePct = 100,
                    ShipEntries = new List<FixedFleetShip>
                    {
                        new FixedFleetShip { TypeID = "CSEM001", Count = 1 },
                    },
                },
            };
            generation
                .GalaxyClassification.FactionSetups.Single(setup => setup.FactionID == "FNALL1")
                .StartingPlanets.Add(
                    new StartingPlanet { PlanetTypeID = "RIM_OUTPOST", Loyalty = 100 }
                );
            PlanetSector rim = new PlanetSector
            {
                TypeID = "FIXTURE_RIM",
                DisplayName = "Rim sector",
                SectorType = PlanetSectorType.OuterRim,
                Visibility = GameSize.Small,
            };
            rim.AddChild(new Planet { TypeID = "RIM_OUTPOST", DisplayName = "Rim outpost" });
            rim.AddChild(new Planet { TypeID = "RIM_SURVEY", DisplayName = "Rim survey site" });

            return CopyCatalog(
                source,
                source.PlanetSectors.Append(rim).ToArray(),
                source
                    .GameEvents.Concat(
                        new[]
                        {
                            new GameEvent
                            {
                                InstanceID = "FIXTURE_DISASTER",
                                Category = GameEventCategory.Disaster,
                                Schedule = new GameEventSchedule { At = new AtTick { Tick = 50 } },
                            },
                            new GameEvent
                            {
                                InstanceID = "FIXTURE_NEUTRAL_EVENT",
                                Schedule = new GameEventSchedule { At = new AtTick { Tick = 80 } },
                            },
                        }
                    )
                    .ToArray()
            );
        }

        /// <summary>
        /// Creates a catalog with the supplied sectors and events.
        /// </summary>
        /// <param name="source">The templates and rules shared by the new catalog.</param>
        /// <param name="sectors">The sectors to seed.</param>
        /// <param name="events">The events to install.</param>
        /// <returns>The configured catalog.</returns>
        private static GameDataCatalog CopyCatalog(
            GameDataCatalog source,
            PlanetSector[] sectors,
            GameEvent[] events
        )
        {
            return new GameDataCatalog(
                source.GameConfig,
                source.GenerationConfig,
                source.Factions,
                sectors,
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
