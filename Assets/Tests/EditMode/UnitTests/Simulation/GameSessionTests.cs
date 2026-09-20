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

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class GameSessionTests
    {
        [Test]
        public void Create_WithFactions_RebuildsResearchCatalogs()
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
        public void ReconcileLoadedState_ContestedPlayerFleet_RestoresPendingCombat()
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
            manager.CombatDecisionRequired += () => decisionsRequired++;
            manager.TickCompleted += () => completedTicks++;

            manager.ReconcileLoadedState();

            Assert.AreEqual(40, game.CurrentTick);
            Assert.IsTrue(manager.Features.SpaceCombat.HasPendingDecision);
            Assert.IsFalse(manager.IsTickSettled);
            Assert.AreEqual(1, decisionsRequired);

            manager.ResolveCombat(true);

            Assert.AreEqual(40, game.CurrentTick);
            Assert.AreEqual(1, completedTicks);
            Assert.IsTrue(manager.IsTickSettled);
        }

        [Test]
        public void ProcessFactionAutomation_ManageNaming_AssignsNameImmediately()
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

            manager.ProcessFactionAutomation(faction);

            Assert.AreEqual("Named Ship", ship.DisplayName);
            Assert.IsTrue(ship.HasAssignedName);
        }

        [Test]
        public void ProcessTick_AdvisorOrderCompletes_RefillsReleasedLaneOnly()
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

            GameSession manager = GameSessionFactory.Create(
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
                manager.Features.Manufacturing.Enqueue(
                    producer,
                    completingOrder,
                    destination,
                    ignoreCost: true
                )
            );

            manager.Tick.ExecuteImmediately();

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
        public void ProcessTick_EventResults_DoesNotAddAutomaticMessages()
        {
            GameRoot game = new GameRoot();
            Faction faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.GetFactions().Add(faction);
            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "EVENT_RESEARCH_EXHAUSTED",
                        Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                        Actions = new List<GameAction>
                        {
                            new EmitResultAction(
                                new ResearchExhaustedResult
                                {
                                    Faction = faction,
                                    Discipline = ResearchDiscipline.ShipDesign,
                                }
                            ),
                        },
                    }
                );

            GameSession manager = TestContent.CreateGameSession(game);

            manager.Tick.ExecuteImmediately();

            Assert.IsEmpty(faction.Messages[MessageType.Manufacturing]);
        }

        [Test]
        public void ProcessTick_FullyRecoveredUnits_DeliversRecoveryMessages()
        {
            (GameSession manager, Officer officer, CapitalShip ship, Starfighter fighter) =
                CreateRecoveryGame();
            List<MessageResultType> deliveredResultTypes = new List<MessageResultType>();
            manager.Results.MessageDelivered += result =>
                deliveredResultTypes.Add(result.Message.ResultType);

            manager.Tick.ExecuteImmediately();

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
        public void ProcessTick_InjuredOfficerAtFriendlyPlanet_Heals()
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

            manager.Tick.ExecuteImmediately();

            Assert.AreEqual(1, officer.InjuryPoints);
        }

        [Test]
        public void ProcessTick_CapturedOfficerWithDueEscapeAttempt_FreesOfficer()
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
            GameSession manager = GameSessionFactory.Create(game, TestGameData.Create(config));

            manager.Tick.ExecuteImmediately();

            Assert.IsFalse(captive.IsCaptured);
        }

        [Test]
        public void ProcessTick_EventCapturesMissionParticipant_CompletesCaptureLifecycle()
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
                        },
                    }
                );
            GameSession manager = GameSessionFactory.Create(game, TestGameData.Create(config));

            manager.Tick.ExecuteImmediately();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Mission>(mission.InstanceID));
            Assert.AreSame(captorPlanet, officer.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.IsTrue(officer.IsCaptured);
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
        public void ProcessTick_VictoryConditionMet_RaisesVictoryDeclaredOnce()
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
            manager.Results.VictoryDeclared += declarations.Add;

            manager.Tick.ExecuteImmediately();
            manager.Tick.ExecuteImmediately();

            Assert.AreEqual(1, declarations.Count);
            Assert.AreSame(alliance, declarations[0].Winner);
            Assert.AreSame(empire, declarations[0].Loser);
        }

        [Test]
        public void ProcessTick_PlanetaryAssaultResult_RaisesResolvedEvent()
        {
            GameRoot game = new GameRoot();
            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "EVENT_PLANETARY_ASSAULT",
                        Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                        Actions = new List<GameAction>
                        {
                            new EmitResultAction(new PlanetaryAssaultResult()),
                        },
                    }
                );
            GameSession manager = TestContent.CreateGameSession(game);
            IReadOnlyList<PlanetaryAssaultResult> observedResults = null;
            manager.Results.PlanetaryAssaultsResolved += results => observedResults = results;

            manager.Tick.ExecuteImmediately();

            Assert.That(observedResults, Has.Count.EqualTo(1));
        }

        [Test]
        public void ProcessTick_VictoryResult_RaisesResolvedEvent()
        {
            GameRoot game = new GameRoot();
            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "EVENT_VICTORY",
                        Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                        Actions = new List<GameAction>
                        {
                            new EmitResultAction(new VictoryResult()),
                        },
                    }
                );
            GameSession manager = TestContent.CreateGameSession(game);
            IReadOnlyList<VictoryResult> observedResults = null;
            manager.Results.VictoriesResolved += results => observedResults = results;

            manager.Tick.ExecuteImmediately();

            Assert.That(observedResults, Has.Count.EqualTo(1));
        }

        [Test]
        public void ProcessTick_ExpiredMessage_RemovesMessageAfterTickAdvances()
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

            manager.Tick.ExecuteImmediately();

            Assert.IsEmpty(faction.Messages[MessageType.Conflict]);
        }

        [Test]
        public void ProcessTick_BlockadeStarts_ReroutesInboundStarfighter()
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
            manager.Features.Movement.RequestMove(starfighter, destination);

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

            manager.Tick.ExecuteImmediately();

            Assert.AreSame(fallback, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        [Test]
        public void ProcessTick_SabotageResult_RemovesDestroyedObjectFromActorSnapshot()
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
            FogOfWar fog = new FogOfWar(game);
            fog.CaptureSnapshot(alliance, planet, sector, 0);
            Assert.IsTrue(
                alliance
                    .Fog.Snapshots["SECTOR1"]
                    .Planets["PLANET1"]
                    .Buildings.Any(b => b.InstanceID == "MINE1")
            );

            game.DetachNode(mine);

            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "EVENT_SABOTAGE",
                        Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                        Actions = new List<GameAction>
                        {
                            new EmitResultAction(
                                new GameObjectSabotagedResult
                                {
                                    DestroyedObject = mine,
                                    DestroyedBy = han,
                                    Context = planet,
                                }
                            ),
                        },
                    }
                );

            GameSession manager = TestContent.CreateGameSession(game);

            manager.Tick.ExecuteImmediately();

            GalaxyMap view = manager.Features.FogOfWar.BuildFactionView(alliance);
            Planet viewedPlanet = view.GetChildren<PlanetSector>()
                .Single(s => s.InstanceID == "SECTOR1")
                .GetChildren<Planet>()
                .Single(p => p.InstanceID == "PLANET1");
            Assert.IsFalse(viewedPlanet.GetChildren<Building>().Any(b => b.InstanceID == "MINE1"));
        }

        [Test]
        public void ProcessTick_FleetDestroyedAfterArrival_AddsFleetArrivalAndBattleMessages()
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
            manager.Features.Movement.RequestMove(
                new List<IMovable> { arrivingFleet },
                destination
            );

            manager.Tick.ExecuteImmediately();

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
        public void ProcessTick_LoadedConvergingMultipleFleets_ResolvesSingleCombinedCombat()
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

                GameSession initialManager = GameSessionFactory.Create(
                    game,
                    TestGameData.Create(config)
                );
                foreach (Fleet fleet in attackingFleets)
                {
                    initialManager.Features.Movement.RequestMove(
                        new List<IMovable> { fleet },
                        destination
                    );
                }

                HashSet<string> expectedShipIds = attackingFleets
                    .Concat(defendingFleets)
                    .SelectMany(fleet => fleet.GetChildren<CapitalShip>())
                    .Select(ship => ship.InstanceID)
                    .ToHashSet();
                SaveGameManager saveManager = new SaveGameManager(saveDirectoryPath);
                saveManager.SaveGameData(game, "multi-fleet-combat");

                GameRoot loadedGame = saveManager.LoadGameData("multi-fleet-combat");
                GameSession loadedManager = GameSessionFactory.Create(
                    loadedGame,
                    TestGameData.Create(config)
                );
                for (
                    int tick = 0;
                    tick < 100 && !loadedManager.Features.SpaceCombat.HasPendingDecision;
                    tick++
                )
                {
                    loadedManager.Tick.ExecuteImmediately();
                }

                Assert.IsTrue(loadedManager.Features.SpaceCombat.HasPendingDecision);
                SpaceCombatResult result = loadedManager.ResolveCombat(autoResolve: true);
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
        public void ProcessTick_FleetArrivesAtPlanetaryStarfighters_CreatesPendingCombat()
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
            manager.Features.Movement.RequestMove(
                new List<IMovable> { arrivingFleet },
                destination
            );

            manager.Tick.ExecuteImmediately();

            Assert.IsTrue(
                manager.Features.SpaceCombat.TryGetPendingCombat(out PendingCombatResult pending)
            );
            Assert.AreSame(arrivingFleet, pending.AttackerFleet);
            Assert.IsNull(pending.DefenderFleet);
            Assert.AreEqual(alliance.InstanceID, pending.AttackerOwnerInstanceID);
            Assert.AreEqual(empire.InstanceID, pending.DefenderOwnerInstanceID);
            Assert.AreSame(destination, pending.Planet);
        }

        [Test]
        public void ProcessTick_FleetReachesWaypoint_StartsNextLegAfterCombatDetection()
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
                manager.Features.Movement.TrySetFleetWaypointRoute(
                    new ISceneNode[] { fleet },
                    new[] { waypoint.InstanceID, destination.InstanceID },
                    alliance.InstanceID
                )
            );
            fleet.Movement.TicksElapsed = fleet.Movement.TransitTicks - 1;

            manager.Tick.ExecuteImmediately();

            Assert.IsFalse(manager.Features.SpaceCombat.HasPendingDecision);
            Assert.AreSame(destination, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            CollectionAssert.AreEqual(new[] { destination.InstanceID }, fleet.Waypoints);
        }

        [Test]
        public void ProcessTick_PendingCombat_CompletesTickAfterResolution()
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
            manager.TickCompleted += () => completedTicks++;

            manager.Tick.ExecuteImmediately();
            int pendingCombatTick = game.CurrentTick;
            manager.Tick.ExecuteImmediately();

            Assert.AreEqual(pendingCombatTick, game.CurrentTick);
            Assert.AreEqual(0, completedTicks);
            Assert.IsFalse(manager.IsTickSettled);

            manager.ResolveCombat(true);

            Assert.AreEqual(1, completedTicks);
            Assert.IsTrue(manager.IsTickSettled);
        }

        [Test]
        public void ProcessTick_PausedGame_DoesNotAdvanceTick()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            GameSession manager = TestContent.CreateGameSession(game);
            manager.SetGameSpeed(TickSpeed.Paused);

            manager.Tick.ExecuteImmediately();

            Assert.AreEqual(0, game.CurrentTick);
        }

        [Test]
        public void ResolveCombat_UnrelatedFleetReachedWaypoint_StartsDeferredNextLeg()
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
                manager.Features.Movement.TrySetFleetWaypointRoute(
                    new ISceneNode[] { routeFleet },
                    new[] { waypoint.InstanceID, destination.InstanceID },
                    alliance.InstanceID
                )
            );
            routeFleet.Movement.TicksElapsed = routeFleet.Movement.TransitTicks - 1;

            manager.Tick.ExecuteImmediately();

            Assert.IsTrue(manager.Features.SpaceCombat.HasPendingDecision);
            Assert.AreSame(waypoint, routeFleet.GetParent());
            Assert.IsNull(routeFleet.Movement);
            CollectionAssert.AreEqual(new[] { destination.InstanceID }, routeFleet.Waypoints);

            manager.ResolveCombat(autoResolve: true);

            Assert.AreSame(destination, routeFleet.GetParent());
            Assert.IsNotNull(routeFleet.Movement);
            CollectionAssert.AreEqual(new[] { destination.InstanceID }, routeFleet.Waypoints);
        }

        [Test]
        public void ProcessTickIncrementally_DisposedBeforeCompletion_AllowsNextTick()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            GameSession manager = TestContent.CreateGameSession(game);
            IEnumerator tick = manager.Tick.ExecuteIncrementally();
            Assert.IsTrue(tick.MoveNext());

            (tick as IDisposable)?.Dispose();
            manager.Tick.ExecuteImmediately();

            Assert.AreEqual(2, game.CurrentTick);
        }

        [Test]
        public void MovementCommand_SurfaceRegimentCreatesGarrisonDeficit_StartsUprisingImmediately()
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
                manager.Features.Missions.InitiateMission(
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
                manager.Features.Movement.TryRequestMove(
                    new ISceneNode[] { departingRegiment },
                    ship,
                    owner.InstanceID
                )
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
            List<GameResult> missionResults = manager.Features.Missions.ProcessTick();

            Assert.AreEqual(
                MissionCompletionReason.Failure,
                missionResults.OfType<MissionCompletedResult>().Single().CompletionReason
            );
            Assert.IsEmpty(game.GetSceneNodesByType<Mission>());
            Assert.AreSame(planet, diplomat.GetParent());
            Assert.IsNull(diplomat.Movement);
        }

        [Test]
        public void MovementCommand_LastSurfaceRegimentNeutralizesPlanet_ReportsImmediately()
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
                manager.Features.Movement.TryRequestMove(
                    new ISceneNode[] { departingRegiment },
                    ship,
                    owner.InstanceID
                )
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
        public void ScrapCommand_LastSurfaceRegiment_ReconcilesPlanetImmediately()
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

            bool scrapped = manager.Features.Maintenance.TryScrap(
                new IManufacturable[] { regiment },
                owner.InstanceID
            );

            Assert.IsTrue(scrapped);
            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
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
            GameSession manager = GameSessionFactory.Create(
                game,
                TestGameData.Create(config, definitions)
            );
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

        private sealed class EmitResultAction : GameAction
        {
            private readonly GameResult _result;

            /// <summary>
            /// Initializes a new instance of the EmitResultAction class.
            /// </summary>
            /// <param name="result">The result.</param>
            internal EmitResultAction(GameResult result)
            {
                _result = result;
            }

            /// <summary>
            /// Executes the requested operation.
            /// </summary>
            /// <param name="context">The context.</param>
            internal override void Execute(GameActionContext context)
            {
                context.Record(_result);
            }
        }
    }
}
