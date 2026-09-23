using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.DependencyInjection;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class GameSessionTests
    {
        private GameRoot _game;
        private Faction _faction;
        private Planet _planet;
        private GameSession _session;

        /// <summary>
        /// Creates one prepared graph and its connected runtime components.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _faction = new Faction { InstanceID = "owner" };
            _game.GetFactions().Add(_faction);
            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector",
                SectorType = PlanetSectorType.OuterRim,
            };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _faction.InstanceID,
                IsColonized = true,
            };
            _planet.SetPopularSupport(_faction.InstanceID, 80);
            _game.AttachNode(_planet, sector);
            _session = new GameSession(_game, TestGameData.Create(_game.Config));
        }

        /// <summary>
        /// Releases the subscriptions owned by the test session.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
        }

        [Test]
        public void Constructor_NullContent_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new GameSession(_game, null)
            );

            Assert.AreEqual("gameData", exception.ParamName);
        }

        [Test]
        public void Constructor_NullGame_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new GameSession(null, TestGameData.Create(_game.Config))
            );
        }

        [Test]
        public void Constructor_FactionWithoutResearchTimer_SchedulesRefresh()
        {
            Assert.Greater(_faction.ResearchState.NextRefreshTick, _game.CurrentTick);
        }

        [Test]
        public void Constructor_MobileHeadquarters_EnablesRelocation()
        {
            _faction.Settings = new FactionSettings
            {
                Headquarters = new HeadquartersSettings { IsMobile = true },
            };
            _faction.HQInstanceID = _planet.InstanceID;
            _planet.EnergyCapacity = 1;
            Building headquarters = new Building
            {
                InstanceID = "headquarters",
                OwnerInstanceID = _faction.InstanceID,
                BuildingType = BuildingType.Headquarters,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(headquarters, _planet);
            Planet destination = new Planet
            {
                InstanceID = "destination",
                OwnerInstanceID = _faction.InstanceID,
                IsColonized = true,
                EnergyCapacity = 1,
                PositionX = 100,
            };
            _game.AttachNode(destination, _planet.GetParent());

            Assert.IsTrue(
                _session.GetService<HeadquartersCommands>().TryRelocate(headquarters, destination)
            );
        }

        [Test]
        public void Constructor_ResultPublished_InvokesRegisteredReaction()
        {
            _session.Results.Publish(CreateSupportShift());

            Assert.AreEqual(85, _planet.GetPopularSupport(_faction.InstanceID));
        }

        [Test]
        public void Constructor_ImmediateScrap_ForwardsOneBatch()
        {
            Regiment regiment = CreateRegiment();
            int batches = 0;
            _session.Pipeline.ResultsResolved += _ => batches++;

            Assert.IsTrue(
                _session
                    .GetService<MaintenanceCommands>()
                    .TryScrap(new[] { regiment }, _faction.InstanceID)
            );

            Assert.AreEqual(1, batches);
        }

        [Test]
        public void GetService_RegisteredCommand_ReturnsActiveSessionInstance()
        {
            IServiceLocator services = _session;

            Assert.AreSame(
                _session.GetService<MovementCommands>(),
                services.GetService<MovementCommands>()
            );
            Assert.AreSame(
                _session.GetService<MovementCommands>(),
                services.GetService(typeof(MovementCommands))
            );
        }

        [Test]
        public void GetService_RegisteredMessageObserver_ReturnsConnectedInstance()
        {
            Assert.AreSame(_session.MessageObserver, _session.GetService<MessageObserver>());
        }

        [Test]
        public void GetService_RegisteredGameEventExecutor_ReturnsConnectedInstance()
        {
            Assert.AreSame(_session.GameEventExecutor, _session.GetService<GameEventExecutor>());
        }

        [Test]
        public void ReplaceGame_ExistingLocator_ResolvesReplacementObserver()
        {
            IServiceLocator services = _session;
            MessageObserver previous = services.GetService<MessageObserver>();

            _session.ReplaceGame(new GameRoot(_game.Config));

            Assert.AreNotSame(previous, services.GetService<MessageObserver>());
            Assert.AreSame(_session.MessageObserver, services.GetService<MessageObserver>());
        }

        [Test]
        public void ReplaceGame_ExistingLocator_ResolvesReplacementCommand()
        {
            IServiceLocator services = _session;
            MovementCommands previous = services.GetService<MovementCommands>();

            _session.ReplaceGame(new GameRoot(_game.Config));

            Assert.AreNotSame(previous, services.GetService<MovementCommands>());
            Assert.AreSame(
                _session.GetService<MovementCommands>(),
                services.GetService<MovementCommands>()
            );
        }

        [Test]
        public void ReplaceGame_ValidReplacement_DetachesPreviousBus()
        {
            GameResultBus previousBus = _session.Results;
            _session.ReplaceGame(new GameRoot(_game.Config));

            previousBus.Publish(CreateSupportShift());

            Assert.AreEqual(80, _planet.GetPopularSupport(_faction.InstanceID));
        }

        [Test]
        public void ReplaceGame_ValidReplacement_DetachesPreviousProducer()
        {
            Regiment regiment = CreateRegiment();
            MaintenanceCommands previousMaintenance = _session.GetService<MaintenanceCommands>();
            int batches = 0;
            _session.Pipeline.ResultsResolved += _ => batches++;
            _session.ReplaceGame(new GameRoot(_game.Config));

            Assert.IsTrue(previousMaintenance.TryScrap(new[] { regiment }, _faction.InstanceID));

            Assert.AreEqual(0, batches);
        }

        [Test]
        public void ReplaceGame_InvalidEvent_PreservesPreviousBusConnections()
        {
            GameResultBus previousBus = _session.Results;
            GameRoot replacement = new(_game.Config);
            replacement
                .GetEventPool()
                .Add(new GameEvent { InstanceID = "INVALID", MaximumActivations = 0 });

            Assert.Throws<InvalidOperationException>(() => _session.ReplaceGame(replacement));
            _session.Results.Publish(CreateSupportShift());

            Assert.AreSame(previousBus, _session.Results);
            Assert.AreEqual(85, _planet.GetPopularSupport(_faction.InstanceID));
        }

        [Test]
        public void Dispose_PublishedResult_DoesNotInvokeOwnedReactions()
        {
            _session.Dispose();

            _session.Results.Publish(CreateSupportShift());

            Assert.AreEqual(80, _planet.GetPopularSupport(_faction.InstanceID));
        }

        [Test]
        public void Dispose_ImmediateScrap_DoesNotForwardBatch()
        {
            Regiment regiment = CreateRegiment();
            MaintenanceCommands maintenance = _session.GetService<MaintenanceCommands>();
            int batches = 0;
            _session.Pipeline.ResultsResolved += _ => batches++;
            _session.Dispose();

            Assert.IsTrue(maintenance.TryScrap(new[] { regiment }, _faction.InstanceID));

            Assert.AreEqual(0, batches);
        }

        [Test]
        public void Constructor_WithFactions_RebuildsResearchCatalogs()
        {
            GameRoot game = new GameRoot();
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);

            Assume.That(
                alliance.ResearchCatalog,
                Is.Empty,
                "Catalog must start empty to prove the rebuild populates it"
            );
            Assume.That(empire.ResearchCatalog, Is.Empty);

            _ = TestContent.CreateGameSession(game);

            Assert.IsNotEmpty(
                alliance.ResearchCatalog,
                "Alliance research catalog should be rebuilt after GameSession construction"
            );
            Assert.IsNotEmpty(
                empire.ResearchCatalog,
                "Empire research catalog should be rebuilt after GameSession construction"
            );
        }

        [Test]
        public void ReplaceGame_InvalidEvent_LeavesReplacementGameAssigned()
        {
            GameConfig config = TestConfig.Create();
            GameSession manager = new(new GameRoot(config), TestGameData.Create(config));
            GameRoot replacement = new(config);
            replacement
                .GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "INVALID",
                        MaximumActivations = 0,
                        Schedule = new GameEventSchedule { At = new AtTick { Tick = 0 } },
                    }
                );

            Assert.Throws<InvalidOperationException>(() => manager.ReplaceGame(replacement));

            Assert.AreSame(replacement, manager.Game);
        }

        [Test]
        public void ReplaceGame_NullGame_PreservesActiveRuntime()
        {
            GameRoot game = new(TestConfig.Create());
            GameSession manager = new(game, TestGameData.Create(game.Config));
            MovementCommands movement = manager.GetService<MovementCommands>();

            Assert.Throws<InvalidOperationException>(() => manager.ReplaceGame(null));

            Assert.AreSame(game, manager.Game);
            Assert.AreSame(movement, manager.GetService<MovementCommands>());
        }

        [Test]
        public void ReplaceGame_InvalidEvent_RetainsEarlierComponentReplacement()
        {
            GameConfig config = TestConfig.Create();
            GameSession manager = new(new GameRoot(config), TestGameData.Create(config));
            MovementCommands movement = manager.GetService<MovementCommands>();
            GameRoot replacement = new(config);
            replacement
                .GetEventPool()
                .Add(new GameEvent { InstanceID = "INVALID", MaximumActivations = 0 });

            Assert.Throws<InvalidOperationException>(() => manager.ReplaceGame(replacement));

            Assert.AreNotSame(movement, manager.GetService<MovementCommands>());
        }

        [Test]
        public void Tick_ContestedPlayerFleet_RestoresPendingCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create()) { CurrentTick = 40 };
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            game.SetFactionController(alliance.InstanceID, "player", PlayerControllerType.Human);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet planet = CreatePlanet("PLANET", empire.InstanceID, 0);
            game.AttachNode(planet, sector);
            CreateCombatFleet(
                game,
                "ALLIANCE_FLEET",
                alliance.InstanceID,
                planet,
                hullStrength: 1000,
                weaponPower: 100
            );
            CreateCombatFleet(
                game,
                "EMPIRE_FLEET",
                empire.InstanceID,
                planet,
                hullStrength: 1000,
                weaponPower: 100
            );
            GameSession manager = TestContent.CreateGameSession(game);
            int decisionsRequired = 0;
            int completedTicks = 0;
            manager.Tick.CombatDecisionRequired += () => decisionsRequired++;
            manager.Tick.TickCompleted += () => completedTicks++;

            manager.Tick.ReconcileLoadedState();

            Assert.AreEqual(40, game.CurrentTick);
            Assert.IsTrue(manager.GetService<SpaceCombatCommands>().HasPendingDecision);
            Assert.IsFalse(manager.Tick.IsSettled);
            Assert.AreEqual(1, decisionsRequired);

            manager.Tick.ResolveCombat(true);

            Assert.AreEqual(40, game.CurrentTick);
            Assert.AreEqual(1, completedTicks);
            Assert.IsTrue(manager.Tick.IsSettled);
        }

        [Test]
        public void FactionAutomationCommands_ManageNaming_AssignsNameImmediately()
        {
            GameRoot game = new GameRoot();
            Faction faction = new Faction { InstanceID = "FACTION", ManageNaming = true };
            faction.ShipNamePools.Add(
                new FactionNamePool
                {
                    NamePoolID = "POOL",
                    Names = new List<string> { "Named Ship" },
                }
            );
            game.GetFactions().Add(faction);
            game.SetFactionController(faction.InstanceID, "PLAYER", PlayerControllerType.Human);
            GameSession manager = TestContent.CreateGameSession(game);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP",
                TypeID = "SHIP_TYPE",
                DisplayName = "Generic Ship",
                OwnerInstanceID = faction.InstanceID,
                ShipNamePoolID = "POOL",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            faction.AddOwnedUnit(ship);

            manager.GetService<FactionAutomationCommands>().ProcessFaction(faction);
            manager.GetService<NamingCommands>().ProcessFaction(faction);

            Assert.AreEqual("Named Ship", ship.DisplayName);
            Assert.IsTrue(ship.HasAssignedName);
        }

        [Test]
        public void Tick_AdvisorOrderCompletes_RefillsReleasedLaneOnly()
        {
            const string factionId = "FACTION";
            const string regimentTypeId = "GARRISON";
            GameConfig config = TestConfig.Create();
            config.AI.Garrison.SupportThreshold = 50;
            config.AI.Garrison.GarrisonDivisor = 10;
            config.AI.Garrison.UprisingMultiplier = 2;
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction
            {
                InstanceID = factionId,
                GarrisonTroopTypeID = regimentTypeId,
                ManageGarrisons = true,
                ManageProduction = false,
            };
            faction.Settings.ResourceProcessingPointsPerFacility = 50;
            game.GetFactions().Add(faction);
            game.SetFactionController(factionId, "PLAYER", PlayerControllerType.Human);

            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet producer = CreatePlanet("PRODUCER", factionId, 0);
            producer.EnergyCapacity = 10;
            producer.NumRawResourceNodes = 2;
            Planet destination = CreatePlanet("DESTINATION", factionId, 10);
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(producer, sector);
            game.AttachNode(destination, sector);
            game.AttachNode(
                new Building
                {
                    InstanceID = "TRAINING",
                    OwnerInstanceID = factionId,
                    BuildingType = BuildingType.TrainingFacility,
                    ProductionType = ManufacturingType.Troop,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                producer
            );
            for (int index = 0; index < 2; index++)
            {
                game.AttachNode(
                    new Building
                    {
                        InstanceID = $"MINE_{index}",
                        OwnerInstanceID = factionId,
                        BuildingType = BuildingType.Mine,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    producer
                );
                game.AttachNode(
                    new Building
                    {
                        InstanceID = $"REFINERY_{index}",
                        OwnerInstanceID = factionId,
                        BuildingType = BuildingType.Refinery,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    producer
                );
            }

            GameSession manager = new GameSession(
                game,
                CreateAutomationGameData(config, factionId, regimentTypeId)
            );
            Regiment completingOrder = new Regiment
            {
                InstanceID = "COMPLETING_ORDER",
                TypeID = regimentTypeId,
                OwnerInstanceID = factionId,
                ConstructionCost = 1,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            Assert.IsTrue(
                manager
                    .GetService<ManufacturingCommands>()
                    .Enqueue(producer, completingOrder, destination, ignoreCost: true)
            );

            manager.Tick.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Delivering, completingOrder.ManufacturingStatus);
            Assert.AreEqual(1, producer.GetManufacturingQueue()[ManufacturingType.Troop].Count);
            Assert.AreEqual(
                ManufacturingStatus.Building,
                producer
                    .GetManufacturingQueue()[ManufacturingType.Troop]
                    .Single()
                    .ManufacturingStatus
            );
            Assert.IsFalse(
                producer
                    .GetManufacturingQueue()
                    .TryGetValue(
                        ManufacturingType.Building,
                        out List<IManufacturable> buildingOrders
                    )
                    && buildingOrders.Count > 0
            );
        }

        [Test]
        public void Tick_EventResults_DoesNotAddAutomaticMessages()
        {
            GameRoot game = new GameRoot();
            Faction faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = CreatePlanet("PLANET", faction.InstanceID, 0);
            Officer officer = EntityFactory.CreateOfficer("OFFICER", faction.InstanceID);
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(officer, planet);
            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "EVENT_INJURY",
                        Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                        Actions = new List<GameAction>
                        {
                            new ApplyOfficerInjuryAction
                            {
                                OfficerInstanceID = officer.InstanceID,
                                MinimumInjury = 1,
                                MaximumInjury = 1,
                            },
                        },
                    }
                );
            GameSession manager = TestContent.CreateGameSession(game);
            List<OfficerInjuredResult> injuries = new List<OfficerInjuredResult>();
            manager.Pipeline.ResultsResolved += results =>
                injuries.AddRange(results.OfType<OfficerInjuredResult>());

            manager.Tick.ProcessTick();

            Assert.That(injuries, Has.Count.EqualTo(1));
            Assert.IsFalse(
                faction
                    .Messages.Values.SelectMany(messages => messages)
                    .Any(message => message.ResultType == MessageResultType.OfficerInjured)
            );
        }

        [Test]
        public void Tick_FullyRecoveredUnits_DeliversRecoveryMessages()
        {
            (GameSession manager, Officer officer, CapitalShip ship, Starfighter fighter) =
                CreateRecoveryGame();
            List<MessageResultType> deliveredResultTypes = new List<MessageResultType>();
            manager.Pipeline.MessageDelivered += result =>
                deliveredResultTypes.Add(result.Message.ResultType);

            manager.Tick.ProcessTick();

            Assert.AreEqual(0, officer.InjuryPoints);
            Assert.AreEqual(ship.MaxHullStrength, ship.CurrentHullStrength);
            Assert.AreEqual(fighter.MaxSquadronSize, fighter.CurrentSquadronSize);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    MessageResultType.OfficerRecovered,
                    MessageResultType.CapitalShipRepaired,
                    MessageResultType.StarfighterRepaired,
                },
                deliveredResultTypes
            );
        }

        [Test]
        public void Tick_InjuredOfficerAtFriendlyPlanet_Heals()
        {
            GameConfig config = TestConfig.Create();
            config.Recovery.NormalHealAmount = 1;
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction { InstanceID = "FNALL1" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
            };
            Officer officer = EntityFactory.CreateOfficer("OFFICER", faction.InstanceID);
            officer.InjuryPoints = 2;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(officer, planet);
            GameSession manager = TestContent.CreateGameSession(game);

            manager.Tick.ProcessTick();

            Assert.AreEqual(1, officer.InjuryPoints);
        }

        [Test]
        public void Tick_CapturedOfficerWithDueEscapeAttempt_FreesOfficer()
        {
            GameConfig config = new GameConfig();
            config.Captive.EscapeTable = new Dictionary<int, int> { { 0, 100 } };
            config.Smuggling.LossPercentByMinimumSupport[0] = 0;
            GameRoot game = new GameRoot(config);
            Faction owner = new Faction { InstanceID = "OWNER" };
            Faction captor = new Faction { InstanceID = "CAPTOR" };
            game.GetFactions().Add(owner);
            game.GetFactions().Add(captor);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet ownerPlanet = CreatePlanet("OWNER_PLANET", owner.InstanceID, 0);
            Planet captorPlanet = CreatePlanet("CAPTOR_PLANET", captor.InstanceID, 100);
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(ownerPlanet, sector);
            game.AttachNode(captorPlanet, sector);
            Officer captive = EntityFactory.CreateOfficer("CAPTIVE", owner.InstanceID);
            captive.IsCaptured = true;
            captive.CaptorInstanceID = captor.InstanceID;
            captive.CanEscape = true;
            captive.NextEscapeAttemptTick = 1;
            game.AttachNode(captive, captorPlanet);
            GameSession manager = new GameSession(game, TestGameData.Create(config));

            manager.Tick.ProcessTick();

            Assert.IsFalse(captive.IsCaptured);
        }

        [Test]
        public void Tick_CaptureBeforeDeactivation_CompletesCaptureLifecycleInActionOrder()
        {
            GameConfig config = new GameConfig();
            config.Smuggling.LossPercentByMinimumSupport[0] = 0;
            GameRoot game = new GameRoot(config);
            Faction owner = new Faction { InstanceID = "OWNER" };
            Faction captor = new Faction { InstanceID = "CAPTOR" };
            game.GetFactions().Add(owner);
            game.GetFactions().Add(captor);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet captorPlanet = new Planet
            {
                InstanceID = "CAPTOR_PLANET",
                OwnerInstanceID = captor.InstanceID,
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(captorPlanet, sector);
            Officer officer = EntityFactory.CreateOfficer("OFFICER", owner.InstanceID);
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "MISSION",
                OwnerInstanceID = owner.InstanceID,
                LocationInstanceID = planet.InstanceID,
            };
            game.AttachNode(officer, planet);
            game.AttachNode(mission, planet);
            game.MoveNode(officer, mission);
            mission.Initiate(100);
            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "CAPTURE_OFFICER",
                        Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                        Actions = new List<GameAction>
                        {
                            new SetCaptureStatusAction
                            {
                                OfficerInstanceID = officer.InstanceID,
                                IsCaptured = true,
                                CaptorFactionInstanceID = captor.InstanceID,
                            },
                            new SetNodeStateAction
                            {
                                InstanceID = officer.InstanceID,
                                State = SceneNodeState.Inactive,
                            },
                        },
                    }
                );
            GameSession manager = new GameSession(game, TestGameData.Create(config));

            manager.Tick.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Mission>(mission.InstanceID));
            Assert.AreSame(captorPlanet, officer.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.IsTrue(officer.IsCaptured);
            Assert.IsFalse(officer.IsEnabled);
            Assert.AreEqual(captor.InstanceID, officer.CaptorInstanceID);
            PlanetSnapshot snapshot = owner.Fog.Snapshots[sector.InstanceID].Planets[
                captorPlanet.InstanceID
            ];
            Officer observed = snapshot.Officers.Single(candidate =>
                candidate.InstanceID == officer.InstanceID
            );
            Assert.IsTrue(observed.IsCaptured);
            Assert.IsNull(observed.Movement);
        }

        [Test]
        public void Tick_VictoryConditionMet_RaisesVictoryDeclaredOnce()
        {
            GameRoot game = new GameRoot(TestConfig.Create())
            {
                Summary = new GameSummary { VictoryCondition = GameVictoryCondition.Headquarters },
            };
            Faction empire = new Faction
            {
                InstanceID = "empire",
                DisplayName = "Empire",
                HQInstanceID = "coruscant",
            };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);
            PlanetSector sector = new PlanetSector { InstanceID = "core" };
            Planet coruscant = new Planet
            {
                InstanceID = "coruscant",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(coruscant, sector);
            GameSession manager = TestContent.CreateGameSession(game);
            List<VictoryResult> declarations = new List<VictoryResult>();
            manager.Pipeline.VictoryDeclared += declarations.Add;

            manager.Tick.ProcessTick();
            manager.Tick.ProcessTick();

            Assert.AreEqual(1, declarations.Count);
            Assert.AreSame(alliance, declarations[0].Winner);
            Assert.AreSame(empire, declarations[0].Loser);
        }

        [Test]
        public void PlanetaryAssaultCommands_CompletedAssault_RaisesResolvedEvent()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction attacker = new Faction { InstanceID = "ATTACKER" };
            Faction defender = new Faction { InstanceID = "DEFENDER" };
            game.GetFactions().Add(attacker);
            game.GetFactions().Add(defender);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = CreatePlanet("PLANET", defender.InstanceID, 0);
            Fleet fleet = EntityFactory.CreateFleet("FLEET", attacker.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP",
                OwnerInstanceID = attacker.InstanceID,
                RegimentCapacity = 1,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Regiment regiment = EntityFactory.CreateRegiment("REGIMENT", attacker.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(regiment, ship);
            GameSession manager = TestContent.CreateGameSession(game);
            IReadOnlyList<PlanetaryAssaultResult> observedResults = null;
            manager.Pipeline.PlanetaryAssaultsResolved += results => observedResults = results;

            PlanetaryAssaultResult result = manager
                .GetService<PlanetaryAssaultCommands>()
                .TryExecute(new[] { fleet }, planet);

            Assert.IsNotNull(result);
            Assert.That(observedResults, Has.Count.EqualTo(1));
            Assert.AreSame(result, observedResults[0]);
        }

        [Test]
        public void PlanetaryAssaultCommands_HeadquartersCapture_PreservesNotificationOrder()
        {
            (GameSession manager, Fleet fleet, Planet target) = CreateHeadquartersAssaultGame();
            List<string> calls = new();
            manager.Pipeline.ResultsResolved += _ => calls.Add("results");
            manager.Pipeline.PlanetaryAssaultsResolved += _ => calls.Add("assaults");
            manager.Pipeline.VictoriesResolved += _ => calls.Add("victories");
            manager.Pipeline.MessageDelivered += _ => calls.Add("message");
            manager.Pipeline.HeadquartersLost += _ => calls.Add("headquarters");
            manager.Pipeline.VictoryDeclared += _ => calls.Add("victory");

            manager.GetService<PlanetaryAssaultCommands>().TryExecute(new[] { fleet }, target);

            CollectionAssert.AreEqual(
                new[]
                {
                    "results",
                    "assaults",
                    "victories",
                    "message",
                    "message",
                    "headquarters",
                    "victory",
                },
                calls
            );
        }

        [Test]
        public void PlanetaryAssaultCommands_ResolvedObserverThrows_DoesNotDeliverMessages()
        {
            (GameSession manager, Fleet fleet, Planet target) = CreateHeadquartersAssaultGame();
            InvalidOperationException failure = new("presentation failed");
            manager.Pipeline.ResultsResolved += _ => throw failure;

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() =>
                manager.GetService<PlanetaryAssaultCommands>().TryExecute(new[] { fleet }, target)
            );

            Assert.AreSame(failure, actual);
            Assert.IsEmpty(
                manager
                    .Game.GetFactions()
                    .SelectMany(faction => faction.Messages.Values)
                    .SelectMany(messages => messages)
            );
        }

        [Test]
        public void PlanetaryAssaultCommands_MessageObserverThrows_SkipsLaterNotifications()
        {
            (GameSession manager, Fleet fleet, Planet target) = CreateHeadquartersAssaultGame();
            InvalidOperationException failure = new("message presentation failed");
            List<string> calls = new();
            manager.Pipeline.MessageDelivered += _ => throw failure;
            manager.Pipeline.HeadquartersLost += _ => calls.Add("headquarters");
            manager.Pipeline.VictoryDeclared += _ => calls.Add("victory");

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() =>
                manager.GetService<PlanetaryAssaultCommands>().TryExecute(new[] { fleet }, target)
            );

            Assert.AreSame(failure, actual);
            Assert.IsEmpty(calls);
        }

        [Test]
        public void PlanetaryAssaultCommands_MessageObserverThrows_RetainsDeliveredMessages()
        {
            (GameSession manager, Fleet fleet, Planet target) = CreateHeadquartersAssaultGame();
            manager.Pipeline.MessageDelivered += _ =>
                throw new InvalidOperationException("presentation failed");

            Assert.Throws<InvalidOperationException>(() =>
                manager.GetService<PlanetaryAssaultCommands>().TryExecute(new[] { fleet }, target)
            );

            Assert.AreEqual(
                2,
                manager
                    .Game.GetFactions()
                    .SelectMany(faction => faction.Messages.Values)
                    .Sum(messages => messages.Count)
            );
        }

        [Test]
        public void Tick_VictoryResult_RaisesResolvedEvent()
        {
            GameRoot game = new GameRoot(TestConfig.Create())
            {
                Summary = new GameSummary { VictoryCondition = GameVictoryCondition.Headquarters },
            };
            Faction defeated = new Faction
            {
                InstanceID = "DEFEATED",
                HQInstanceID = "HEADQUARTERS",
            };
            Faction winner = new Faction { InstanceID = "WINNER" };
            game.GetFactions().Add(defeated);
            game.GetFactions().Add(winner);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet headquarters = CreatePlanet("HEADQUARTERS", winner.InstanceID, 0);
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(headquarters, sector);
            GameSession manager = TestContent.CreateGameSession(game);
            IReadOnlyList<VictoryResult> observedResults = null;
            manager.Pipeline.VictoriesResolved += results => observedResults = results;

            manager.Tick.ProcessTick();

            Assert.That(observedResults, Has.Count.EqualTo(1));
            Assert.AreSame(winner, observedResults[0].Winner);
        }

        [Test]
        public void Tick_ExpiredMessage_RemovesMessageAfterTickAdvances()
        {
            GameConfig config = TestConfig.Create();
            config.Messages.RetentionTicks = 300;
            GameRoot game = new GameRoot(config) { CurrentTick = 400 };
            Faction faction = new Faction { InstanceID = "FACTION" };
            game.GetFactions().Add(faction);
            faction.AddMessage(
                new StatusMessage(MessageType.Conflict, "Expired") { CreatedTick = 100 }
            );
            GameSession manager = TestContent.CreateGameSession(game);

            manager.Tick.ProcessTick();

            Assert.IsEmpty(faction.Messages[MessageType.Conflict]);
        }

        [Test]
        public void Tick_BlockadeStarts_ReroutesInboundStarfighter()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction owner = new Faction { InstanceID = "OWNER" };
            Faction opposition = new Faction { InstanceID = "OPPOSITION" };
            game.GetFactions().Add(owner);
            game.GetFactions().Add(opposition);

            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet origin = new Planet
            {
                InstanceID = "ORIGIN",
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet destination = new Planet
            {
                InstanceID = "DESTINATION",
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            Planet fallback = new Planet
            {
                InstanceID = "FALLBACK",
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                PositionX = 120,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(destination, sector);
            game.AttachNode(fallback, sector);

            Starfighter starfighter = EntityFactory.CreateStarfighter(
                "STARFIGHTER",
                owner.InstanceID
            );
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(starfighter, origin);

            GameSession manager = TestContent.CreateGameSession(game);
            manager.GetService<MovementCommands>().RequestMove(starfighter, destination);

            Fleet blockadingFleet = EntityFactory.CreateFleet(
                "BLOCKADING_FLEET",
                opposition.InstanceID
            );
            CapitalShip blockadingShip = new CapitalShip
            {
                InstanceID = "BLOCKADING_SHIP",
                OwnerInstanceID = opposition.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(blockadingFleet, destination);
            game.AttachNode(blockadingShip, blockadingFleet);

            manager.Tick.ProcessTick();

            Assert.AreSame(fallback, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        [Test]
        public void Tick_SabotageResult_RemovesDestroyedObjectFromActorSnapshot()
        {
            GameRoot game = new GameRoot(TestContent.Data.GameConfig);
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            game.SetFactionController(
                alliance.InstanceID,
                "alliance_player",
                PlayerControllerType.Human
            );
            game.SetFactionController(
                empire.InstanceID,
                "empire_player",
                PlayerControllerType.Human
            );

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "SECTOR1",
                DisplayName = "Sector",
            };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "PLANET1",
                DisplayName = "Coruscant",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(planet, sector);

            Building mine = new Building
            {
                InstanceID = "MINE1",
                DisplayName = "Mine",
                OwnerInstanceID = empire.InstanceID,
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(mine, planet);

            Officer han = EntityFactory.CreateOfficer("HAN", alliance.InstanceID);
            FogOfWarCommands fog = new FogOfWarCommands(game);
            fog.CaptureSnapshot(alliance, planet, sector, 0);
            Assert.IsTrue(
                alliance
                    .Fog.Snapshots["SECTOR1"]
                    .Planets["PLANET1"]
                    .Buildings.Any(b => b.InstanceID == "MINE1")
            );

            game.Random = new FixedRNG(0.0);
            Mission mission = SabotageMission.TryCreate(
                new MissionContext
                {
                    Game = game,
                    OwnerInstanceId = alliance.InstanceID,
                    Location = planet,
                    MainParticipants = new List<IMissionParticipant> { han },
                    DecoyParticipants = new List<IMissionParticipant>(),
                    SelectedTarget = mine,
                }
            );
            Assert.IsNotNull(mission);
            game.AttachNode(mission, planet);
            game.AttachNode(han, mission);
            mission.Initiate(1);
            mission.DetectionResolved = true;

            GameSession manager = TestContent.CreateGameSession(game);

            manager.Tick.ProcessTick();

            GalaxyMap view = manager.GetService<FogOfWarQueries>().BuildFactionView(alliance);
            Planet viewedPlanet = view.GetChildren<PlanetSector>()
                .Single(s => s.InstanceID == "SECTOR1")
                .GetChildren<Planet>()
                .Single(p => p.InstanceID == "PLANET1");
            Assert.IsFalse(viewedPlanet.GetChildren<Building>().Any(b => b.InstanceID == "MINE1"));
        }

        [Test]
        public void Tick_FleetDestroyedAfterArrival_AddsFleetArrivalAndBattleMessages()
        {
            GameRoot game = new GameRoot(TestConfig.Create())
            {
                Random = new QueueRNG(0.5, 0.5, 0.5, 0.5),
            };
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "SECTOR1",
                DisplayName = "Sector",
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet origin = new Planet
            {
                InstanceID = "ORIGIN",
                DisplayName = "Origin",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            Planet destination = new Planet
            {
                InstanceID = "DEST",
                DisplayName = "Destination",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(destination, sector);

            Fleet arrivingFleet = CreateCombatFleet(
                game,
                "ARRIVING",
                alliance.InstanceID,
                origin,
                hullStrength: 1,
                weaponPower: 0
            );
            Fleet defendingFleet = CreateCombatFleet(
                game,
                "DEFENDING",
                empire.InstanceID,
                destination,
                hullStrength: 1000,
                weaponPower: 100
            );
            defendingFleet.GetChildren<CapitalShip>()[0].HasGravityWell = true;

            GameSession manager = TestContent.CreateGameSession(game);
            manager
                .GetService<MovementCommands>()
                .RequestMove(new List<IMovable> { arrivingFleet }, destination);

            manager.Tick.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>(arrivingFleet.InstanceID));
            List<Message> fleetMessages = alliance.Messages.TryGetValue(
                MessageType.Fleet,
                out List<Message> messages
            )
                ? messages
                : new List<Message>();
            Assert.IsTrue(
                fleetMessages.Any(message => message.Body == "ARRIVING has arrived at Destination.")
            );

            List<Message> conflictMessages = alliance.Messages.TryGetValue(
                MessageType.Conflict,
                out List<Message> battles
            )
                ? battles
                : new List<Message>();
            Assert.IsTrue(
                conflictMessages.Any(message => message.Title == "Battle at Destination")
            );
        }

        [Test]
        public void Tick_LoadedConvergingMultipleFleets_ResolvesSingleCombinedCombat()
        {
            string saveDirectoryPath = Path.Combine(
                Path.GetTempPath(),
                nameof(GameSessionTests),
                Guid.NewGuid().ToString("N")
            );

            try
            {
                GameConfig config = TestConfig.Create();
                GameRoot game = new GameRoot(config);
                Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
                Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
                game.GetFactions().Add(alliance);
                game.GetFactions().Add(empire);
                game.SetFactionController(
                    alliance.InstanceID,
                    "player",
                    PlayerControllerType.Human
                );

                PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
                game.AttachNode(sector, game.GetGalaxyMap());
                Planet origin = CreatePlanet("ORIGIN", alliance.InstanceID, 0);
                Planet destination = CreatePlanet("DESTINATION", empire.InstanceID, 1);
                game.AttachNode(origin, sector);
                game.AttachNode(destination, sector);

                List<Fleet> attackingFleets = new List<Fleet>
                {
                    CreateCombatFleet(game, "ATTACKER_1", alliance.InstanceID, origin, 1, 0),
                    CreateCombatFleet(game, "ATTACKER_2", alliance.InstanceID, origin, 1, 0),
                    CreateCombatFleet(game, "ATTACKER_3", alliance.InstanceID, origin, 1, 0),
                };
                List<Fleet> defendingFleets = new List<Fleet>
                {
                    CreateCombatFleet(
                        game,
                        "DEFENDER_1",
                        empire.InstanceID,
                        destination,
                        1000,
                        100
                    ),
                    CreateCombatFleet(
                        game,
                        "DEFENDER_2",
                        empire.InstanceID,
                        destination,
                        1000,
                        100
                    ),
                };
                foreach (Fleet fleet in defendingFleets)
                    fleet.GetChildren<CapitalShip>().Single().HasGravityWell = true;

                GameSession initialManager = new GameSession(game, TestGameData.Create(config));
                foreach (Fleet fleet in attackingFleets)
                {
                    initialManager
                        .GetService<MovementCommands>()
                        .RequestMove(new List<IMovable> { fleet }, destination);
                }

                HashSet<string> expectedShipIds = attackingFleets
                    .Concat(defendingFleets)
                    .SelectMany(fleet => fleet.GetChildren<CapitalShip>())
                    .Select(ship => ship.InstanceID)
                    .ToHashSet();
                SaveGameManager saveManager = new SaveGameManager(saveDirectoryPath);
                saveManager.SaveGameData(game, "multi-fleet-combat");

                GameRoot loadedGame = saveManager.LoadGameData("multi-fleet-combat");
                GameSession loadedManager = new GameSession(
                    loadedGame,
                    TestGameData.Create(config)
                );
                for (
                    int tick = 0;
                    tick < 100
                        && !loadedManager.GetService<SpaceCombatCommands>().HasPendingDecision;
                    tick++
                )
                {
                    loadedManager.Tick.ProcessTick();
                }

                Assert.IsTrue(loadedManager.GetService<SpaceCombatCommands>().HasPendingDecision);
                SpaceCombatResult result = loadedManager.Tick.ResolveCombat(autoResolve: true);
                HashSet<string> participatingShipIds = result
                    .AttackingUnits.Concat(result.DefendingUnits)
                    .Select(unit => unit.Unit)
                    .OfType<CapitalShip>()
                    .Select(ship => ship.InstanceID)
                    .ToHashSet();
                CollectionAssert.AreEquivalent(expectedShipIds, participatingShipIds);
                Assert.IsTrue(
                    attackingFleets.All(fleet =>
                        loadedGame.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID) == null
                    )
                );
                Assert.IsTrue(
                    defendingFleets.All(fleet =>
                        loadedGame.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID) != null
                    )
                );
            }
            finally
            {
                if (Directory.Exists(saveDirectoryPath))
                    Directory.Delete(saveDirectoryPath, recursive: true);
            }
        }

        [Test]
        public void Tick_FleetArrivesAtPlanetaryStarfighters_CreatesPendingCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            game.SetFactionController(alliance.InstanceID, "player", PlayerControllerType.Human);

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "SECTOR1",
                DisplayName = "Sector",
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet origin = new Planet
            {
                InstanceID = "ORIGIN",
                DisplayName = "Origin",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            Planet destination = new Planet
            {
                InstanceID = "DEST",
                DisplayName = "Destination",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(destination, sector);

            Fleet arrivingFleet = CreateCombatFleet(
                game,
                "ARRIVING",
                alliance.InstanceID,
                origin,
                hullStrength: 1000,
                weaponPower: 100
            );
            Starfighter defender = new Starfighter
            {
                InstanceID = "DEFENDER",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                LaserCannon = 5,
            };
            game.AttachNode(defender, destination);
            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "UNRELATED_SOURCE_FILTERED_ARRIVAL",
                        Triggers = new List<GameEventTrigger>
                        {
                            new UnitArrivedTrigger { SourceEventInstanceID = "SOME_OTHER_EVENT" },
                        },
                    }
                );

            GameSession manager = TestContent.CreateGameSession(game);
            manager
                .GetService<MovementCommands>()
                .RequestMove(new List<IMovable> { arrivingFleet }, destination);

            manager.Tick.ProcessTick();

            Assert.IsTrue(
                manager
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );
            Assert.AreSame(arrivingFleet, pending.AttackerFleet);
            Assert.IsNull(pending.DefenderFleet);
            Assert.AreEqual(alliance.InstanceID, pending.AttackerOwnerInstanceID);
            Assert.AreEqual(empire.InstanceID, pending.DefenderOwnerInstanceID);
            Assert.AreSame(destination, pending.Planet);
        }

        [Test]
        public void Tick_FleetReachesWaypoint_StartsNextLegAfterCombatDetection()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.GetFactions().Add(alliance);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet origin = CreatePlanet("ORIGIN", alliance.InstanceID, 0);
            Planet waypoint = CreatePlanet("WAYPOINT", alliance.InstanceID, 10);
            Planet destination = CreatePlanet("DESTINATION", alliance.InstanceID, 20);
            game.AttachNode(origin, sector);
            game.AttachNode(waypoint, sector);
            game.AttachNode(destination, sector);
            Fleet fleet = CreateCombatFleet(
                game,
                "ROUTE_FLEET",
                alliance.InstanceID,
                origin,
                hullStrength: 1000,
                weaponPower: 100
            );
            GameSession manager = TestContent.CreateGameSession(game);
            Assert.IsTrue(
                manager
                    .GetService<MovementCommands>()
                    .TrySetFleetWaypointRoute(
                        new ISceneNode[] { fleet },
                        new[] { waypoint.InstanceID, destination.InstanceID },
                        alliance.InstanceID
                    )
            );
            fleet.Movement.TicksElapsed = fleet.Movement.TransitTicks - 1;

            manager.Tick.ProcessTick();

            Assert.IsFalse(manager.GetService<SpaceCombatCommands>().HasPendingDecision);
            Assert.AreSame(destination, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            CollectionAssert.AreEqual(new[] { destination.InstanceID }, fleet.Waypoints);
        }

        [Test]
        public void Tick_PendingCombat_CompletesTickAfterResolution()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            game.SetFactionController(alliance.InstanceID, "player", PlayerControllerType.Human);

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "SECTOR1",
                DisplayName = "Sector",
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "DEST",
                DisplayName = "Destination",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(planet, sector);

            CreateCombatFleet(
                game,
                "ALLIANCE",
                alliance.InstanceID,
                planet,
                hullStrength: 1000,
                weaponPower: 100
            );
            CreateCombatFleet(
                game,
                "EMPIRE",
                empire.InstanceID,
                planet,
                hullStrength: 1000,
                weaponPower: 100
            );

            GameSession manager = TestContent.CreateGameSession(game);
            int completedTicks = 0;
            manager.Tick.TickCompleted += () => completedTicks++;

            manager.Tick.ProcessTick();
            int pendingCombatTick = game.CurrentTick;
            manager.Tick.ProcessTick();

            Assert.AreEqual(pendingCombatTick, game.CurrentTick);
            Assert.AreEqual(0, completedTicks);
            Assert.IsFalse(manager.Tick.IsSettled);

            manager.Tick.ResolveCombat(true);

            Assert.AreEqual(1, completedTicks);
            Assert.IsTrue(manager.Tick.IsSettled);
        }

        [Test]
        public void Tick_PausedGame_DoesNotAdvanceTick()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            GameSession manager = TestContent.CreateGameSession(game);
            manager.Game.SetGameSpeed(TickSpeed.Paused);

            manager.Tick.ProcessTick();

            Assert.AreEqual(0, game.CurrentTick);
        }

        [Test]
        public void Tick_UnrelatedFleetReachedWaypoint_StartsDeferredNextLeg()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            game.SetFactionController(alliance.InstanceID, "player", PlayerControllerType.Human);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet origin = CreatePlanet("ORIGIN", alliance.InstanceID, 0);
            Planet waypoint = CreatePlanet("WAYPOINT", alliance.InstanceID, 10);
            Planet destination = CreatePlanet("DESTINATION", alliance.InstanceID, 20);
            Planet combatPlanet = CreatePlanet("COMBAT", empire.InstanceID, 30);
            game.AttachNode(origin, sector);
            game.AttachNode(waypoint, sector);
            game.AttachNode(destination, sector);
            game.AttachNode(combatPlanet, sector);
            Fleet routeFleet = CreateCombatFleet(
                game,
                "ROUTE_FLEET",
                alliance.InstanceID,
                origin,
                hullStrength: 1000,
                weaponPower: 100
            );
            CreateCombatFleet(
                game,
                "ALLIANCE_COMBAT",
                alliance.InstanceID,
                combatPlanet,
                hullStrength: 1000,
                weaponPower: 100
            );
            CreateCombatFleet(
                game,
                "EMPIRE_COMBAT",
                empire.InstanceID,
                combatPlanet,
                hullStrength: 1000,
                weaponPower: 100
            );
            GameSession manager = TestContent.CreateGameSession(game);
            Assert.IsTrue(
                manager
                    .GetService<MovementCommands>()
                    .TrySetFleetWaypointRoute(
                        new ISceneNode[] { routeFleet },
                        new[] { waypoint.InstanceID, destination.InstanceID },
                        alliance.InstanceID
                    )
            );
            routeFleet.Movement.TicksElapsed = routeFleet.Movement.TransitTicks - 1;

            manager.Tick.ProcessTick();

            Assert.IsTrue(manager.GetService<SpaceCombatCommands>().HasPendingDecision);
            Assert.AreSame(waypoint, routeFleet.GetParent());
            Assert.IsNull(routeFleet.Movement);
            CollectionAssert.AreEqual(new[] { destination.InstanceID }, routeFleet.Waypoints);

            manager.Tick.ResolveCombat(autoResolve: true);

            Assert.AreSame(destination, routeFleet.GetParent());
            Assert.IsNotNull(routeFleet.Movement);
            CollectionAssert.AreEqual(new[] { destination.InstanceID }, routeFleet.Waypoints);
        }

        [Test]
        public void Tick_DisposedBeforeCompletion_AllowsNextTick()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            GameSession manager = TestContent.CreateGameSession(game);
            IEnumerator tick = manager.Tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());

            (tick as IDisposable)?.Dispose();
            manager.Tick.ProcessTick();

            Assert.AreEqual(2, game.CurrentTick);
        }

        [Test]
        public void Tick_CompletionObserverThrows_AllowsSubsequentTick()
        {
            GameRoot game = new(TestConfig.Create());
            GameSession manager = new(game, TestGameData.Create(game.Config));
            manager.Game.SetGameSpeed(TickSpeed.Fast);
            InvalidOperationException expected = new("observer failure");
            Action fail = () => throw expected;
            manager.Tick.TickCompleted += fail;

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                manager.Tick.ProcessTick
            );
            manager.Tick.TickCompleted -= fail;
            manager.Tick.ProcessTick();

            Assert.AreSame(expected, actual);
            Assert.AreEqual(2, game.CurrentTick);
        }

        [Test]
        public void MovementCommands_SurfaceRegimentCreatesGarrisonDeficit_StartsUprisingImmediately()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction owner = new Faction { InstanceID = "OWNER", DisplayName = "Owner" };
            Faction opposition = new Faction
            {
                InstanceID = "OPPOSITION",
                DisplayName = "Opposition",
            };
            game.GetFactions().Add(owner);
            game.GetFactions().Add(opposition);

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "SECTOR",
                SectorType = PlanetSectorType.OuterRim,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                DisplayName = "Planet",
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { owner.InstanceID, 10 },
                    { opposition.InstanceID, 90 },
                },
            };
            game.AttachNode(planet, sector);
            planet.AddVisitor(owner.InstanceID);
            Planet home = new Planet
            {
                InstanceID = "HOME_PLANET",
                TypeID = "HOME",
                DisplayName = "Home",
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                PositionX = 100,
            };
            game.AttachNode(home, sector);

            Officer diplomat = EntityFactory.CreateOfficer("DIPLOMAT", owner.InstanceID);
            game.AttachNode(diplomat, home);

            Regiment departingRegiment = null;
            for (int i = 0; i < 5; i++)
            {
                Regiment regiment = EntityFactory.CreateRegiment($"REGIMENT_{i}", owner.InstanceID);
                regiment.ManufacturingStatus = ManufacturingStatus.Complete;
                game.AttachNode(regiment, planet);
                departingRegiment ??= regiment;
            }

            Fleet fleet = EntityFactory.CreateFleet("FLEET", owner.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP",
                OwnerInstanceID = owner.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            GameSession manager = TestContent.CreateGameSession(game);
            Assert.IsTrue(
                manager
                    .GetService<MissionCommands>()
                    .InitiateMission(
                        new MissionContext
                        {
                            MissionTypeID = DiplomacyMission.MissionTypeID,
                            Location = planet,
                            MainParticipants = new List<IMissionParticipant> { diplomat },
                        }
                    )
            );
            Assert.IsNotNull(diplomat.Movement);

            Assert.IsTrue(
                manager
                    .GetService<MovementCommands>()
                    .TryRequestMove(new ISceneNode[] { departingRegiment }, ship, owner.InstanceID)
            );

            Assert.AreEqual(0, game.CurrentTick);
            Assert.IsTrue(planet.IsInUprising);
            Mission diplomacyMission = game.GetSceneNodesByType<Mission>().Single();
            Assert.AreSame(diplomacyMission, diplomat.GetParent());
            Assert.IsNotNull(diplomat.Movement);
            Assert.IsTrue(
                owner
                    .Messages[MessageType.PopularSupport]
                    .Any(message => message.ResultType == MessageResultType.UprisingStarted)
            );

            diplomat.Movement = null;
            IReadOnlyList<GameResult> missionResults = new MissionTickProcessor(
                manager.GetService<MissionCommands>()
            ).ProcessTick(game);

            Assert.AreEqual(
                MissionCompletionReason.Failure,
                missionResults.OfType<MissionCompletedResult>().Single().CompletionReason
            );
            Assert.IsEmpty(game.GetSceneNodesByType<Mission>());
            Assert.AreSame(planet, diplomat.GetParent());
            Assert.IsNull(diplomat.Movement);
        }

        [Test]
        public void MovementCommands_LastSurfaceRegimentNeutralizesPlanet_ReportsImmediately()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction owner = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            Faction opposition = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.GetFactions().Add(owner);
            game.GetFactions().Add(opposition);

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "SECTOR",
                SectorType = PlanetSectorType.OuterRim,
            };
            game.AttachNode(sector, game.GetGalaxyMap());

            int ownershipThreshold = game.Config.SupportShift.OwnershipTransferThreshold;
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                DisplayName = "Planet",
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { owner.InstanceID, ownershipThreshold - 1 },
                    { opposition.InstanceID, 100 - ownershipThreshold + 1 },
                },
            };
            game.AttachNode(planet, sector);

            Regiment departingRegiment = EntityFactory.CreateRegiment("REGIMENT", owner.InstanceID);
            departingRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(departingRegiment, planet);

            Fleet fleet = EntityFactory.CreateFleet("FLEET", owner.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP",
                OwnerInstanceID = owner.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            GameSession manager = TestContent.CreateGameSession(game);

            Assert.IsTrue(
                manager
                    .GetService<MovementCommands>()
                    .TryRequestMove(new ISceneNode[] { departingRegiment }, ship, owner.InstanceID)
            );

            Assert.AreEqual(0, game.CurrentTick);
            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.IsTrue(
                owner
                    .Messages[MessageType.PopularSupport]
                    .Any(message =>
                        message.ResultType == MessageResultType.PlanetDeclaredNeutralityBySupport
                    )
            );
        }

        [Test]
        public void MaintenanceCommands_LastSurfaceRegiment_ReconcilesPlanetImmediately()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction owner = new Faction { InstanceID = "OWNER", DisplayName = "Owner" };
            Faction opposition = new Faction
            {
                InstanceID = "OPPOSITION",
                DisplayName = "Opposition",
            };
            game.GetFactions().Add(owner);
            game.GetFactions().Add(opposition);

            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            game.AttachNode(sector, game.GetGalaxyMap());
            int ownershipThreshold = game.Config.SupportShift.OwnershipTransferThreshold;
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                OwnerInstanceID = owner.InstanceID,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { owner.InstanceID, ownershipThreshold - 1 },
                    { opposition.InstanceID, 100 - ownershipThreshold + 1 },
                },
            };
            game.AttachNode(planet, sector);
            Regiment regiment = EntityFactory.CreateRegiment("REGIMENT", owner.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, planet);
            GameSession manager = TestContent.CreateGameSession(game);

            bool scrapped = manager
                .GetService<MaintenanceCommands>()
                .TryScrap(new IManufacturable[] { regiment }, owner.InstanceID);

            Assert.IsTrue(scrapped);
            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        /// <summary>
        /// Creates an existing support result that exercises a registered gameplay reaction.
        /// </summary>
        /// <returns>The requested support shift.</returns>
        private PopularSupportShiftResult CreateSupportShift()
        {
            return new PopularSupportShiftResult
            {
                Planet = _planet,
                Faction = _faction,
                Shift = 5,
            };
        }

        /// <summary>
        /// Adds a complete regiment eligible for the existing immediate scrap operation.
        /// </summary>
        /// <returns>The attached regiment.</returns>
        private Regiment CreateRegiment()
        {
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = _faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(regiment, _planet);
            return regiment;
        }

        /// <summary>
        /// Creates combat fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="hullStrength">The hull strength.</param>
        /// <param name="weaponPower">The weapon power.</param>
        /// <returns>The created combat fleet.</returns>
        private static Fleet CreateCombatFleet(
            GameRoot game,
            string instanceId,
            string ownerId,
            Planet planet,
            int hullStrength,
            int weaponPower
        )
        {
            Fleet fleet = new Fleet
            {
                InstanceID = instanceId,
                DisplayName = instanceId,
                OwnerInstanceID = ownerId,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId + "_SHIP",
                DisplayName = instanceId + " Ship",
                OwnerInstanceID = ownerId,
                MaxHullStrength = hullStrength,
                CurrentHullStrength = hullStrength,
                ShieldRechargeRate = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            if (weaponPower > 0)
            {
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new int[]
                {
                    weaponPower,
                    weaponPower,
                    weaponPower,
                    weaponPower,
                    100,
                };
            }

            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return fleet;
        }

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <param name="positionX">The position x.</param>
        /// <returns>The created planet.</returns>
        private static Planet CreatePlanet(string instanceId, string ownerId, int positionX)
        {
            return new Planet
            {
                InstanceID = instanceId,
                DisplayName = instanceId,
                OwnerInstanceID = ownerId,
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = positionX,
            };
        }

        /// <summary>Creates an undefended fixed headquarters and an invasion fleet with deterministic capture.</summary>
        /// <returns>The manager, attacking fleet, and target headquarters planet.</returns>
        private static (
            GameSession Manager,
            Fleet Fleet,
            Planet Target
        ) CreateHeadquartersAssaultGame()
        {
            GameRoot game = new(TestConfig.Create())
            {
                Summary = new GameSummary { VictoryCondition = GameVictoryCondition.Headquarters },
            };
            Faction attacker = new() { InstanceID = "ATTACKER", DisplayName = "Attacker" };
            Faction defender = new()
            {
                InstanceID = "DEFENDER",
                DisplayName = "Defender",
                HQInstanceID = "HEADQUARTERS",
            };
            game.GetFactions().Add(attacker);
            game.GetFactions().Add(defender);
            PlanetSector sector = new() { InstanceID = "SECTOR" };
            Planet target = CreatePlanet("HEADQUARTERS", defender.InstanceID, 0);
            target.IsHeadquarters = true;
            Fleet fleet = EntityFactory.CreateFleet("FLEET", attacker.InstanceID);
            CapitalShip ship = new()
            {
                InstanceID = "SHIP",
                OwnerInstanceID = attacker.InstanceID,
                RegimentCapacity = 1,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Regiment regiment = EntityFactory.CreateRegiment("REGIMENT", attacker.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(target, sector);
            game.AttachNode(fleet, target);
            game.AttachNode(ship, fleet);
            game.AttachNode(regiment, ship);
            GameSession manager = new(
                game,
                TestGameData.Create(
                    game.Config,
                    new[]
                    {
                        new MessageDefinition
                        {
                            ResultType = MessageResultType.PlanetaryAssault,
                            MessageType = MessageType.Conflict,
                            Outcome = MessageResultOutcome.Success,
                            PlanetOwnership = MessagePlanetOwnership.Owned,
                            Subject = "Headquarters assaulted",
                            Body = "Headquarters assaulted",
                        },
                    }
                )
            );
            return (manager, fleet, target);
        }

        /// <summary>
        /// Creates recovery game.
        /// </summary>
        /// <returns>The created recovery game.</returns>
        private static (
            GameSession manager,
            Officer officer,
            CapitalShip ship,
            Starfighter fighter
        ) CreateRecoveryGame()
        {
            GameConfig config = new GameConfig();
            config.Recovery.NormalHealAmount = 1;
            config.Recovery.FastRepairAmount = 1;
            config.Recovery.FastReplacementAmount = 1;
            config.Smuggling.LossPercentByMinimumSupport[0] = 0;
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction { InstanceID = "FACTION", DisplayName = "Faction" };
            game.GetFactions().Add(faction);
            game.SetFactionController(faction.InstanceID, "PLAYER", PlayerControllerType.Human);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                DisplayName = "Planet",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                EnergyCapacity = 1,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

            Officer officer = EntityFactory.CreateOfficer("OFFICER", faction.InstanceID);
            officer.DisplayName = "Officer";
            officer.InjuryPoints = 1;
            Fleet fleet = EntityFactory.CreateFleet("FLEET", faction.InstanceID);
            fleet.DisplayName = "Fleet";
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP",
                DisplayName = "Ship",
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = 99,
                StarfighterCapacity = 1,
            };
            Starfighter fighter = new Starfighter
            {
                InstanceID = "FIGHTER",
                DisplayName = "Fighter",
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 11,
            };
            game.AttachNode(officer, planet);
            game.AttachNode(
                new Building
                {
                    InstanceID = "SHIPYARD",
                    OwnerInstanceID = faction.InstanceID,
                    BuildingType = BuildingType.Shipyard,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(fighter, ship);

            MessageDefinition[] definitions =
            {
                CreateMessageDefinition(MessageResultType.OfficerRecovered, MessageType.Mission),
                CreateMessageDefinition(MessageResultType.CapitalShipRepaired, MessageType.Fleet),
                CreateMessageDefinition(MessageResultType.StarfighterRepaired, MessageType.Fleet),
            };
            GameSession manager = new GameSession(game, TestGameData.Create(config, definitions));
            return (manager, officer, ship, fighter);
        }

        /// <summary>
        /// Creates message definition.
        /// </summary>
        /// <param name="resultType">The result type.</param>
        /// <param name="messageType">The message type.</param>
        /// <returns>The created message definition.</returns>
        private static MessageDefinition CreateMessageDefinition(
            MessageResultType resultType,
            MessageType messageType
        )
        {
            return new MessageDefinition
            {
                ResultType = resultType,
                MessageType = messageType,
                Subject = resultType.ToString(),
                Body = resultType.ToString(),
            };
        }

        /// <summary>
        /// Creates the minimal content catalog required for advisor-managed garrison production.
        /// </summary>
        /// <param name="config">The game configuration shared with the test game.</param>
        /// <param name="factionId">The faction allowed to manufacture the garrison.</param>
        /// <param name="regimentTypeId">The configured garrison regiment type.</param>
        /// <returns>A catalog containing the requested garrison template.</returns>
        private static GameDataCatalog CreateAutomationGameData(
            GameConfig config,
            string factionId,
            string regimentTypeId
        )
        {
            GameGenerationConfig generationConfig = new GameGenerationConfig();
            Regiment garrison = new Regiment
            {
                TypeID = regimentTypeId,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
                ManufacturingFactionInstanceIDs = new List<string> { factionId },
            };
            return new GameDataCatalog(
                config,
                generationConfig,
                Array.Empty<Faction>(),
                Array.Empty<PlanetSector>(),
                Array.Empty<Building>(),
                Array.Empty<CapitalShip>(),
                Array.Empty<Starfighter>(),
                new[] { garrison },
                Array.Empty<SpecialForces>(),
                Array.Empty<Officer>(),
                Array.Empty<GameEvent>(),
                Array.Empty<MessageDefinition>(),
                new EncyclopediaEntries(),
                new FactionThemes()
            );
        }
    }
}
