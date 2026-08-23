using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerTests
    {
        [Test]
        public void SetGameSpeed_ConfiguredIntervals_UpdatesTickInterval()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.FastTickIntervalSeconds = 2.5f;
            config.GameSpeed.MediumTickIntervalSeconds = 12.5f;
            config.GameSpeed.SlowTickIntervalSeconds = 90.5f;
            config.GameSpeed.VerySlowTickIntervalSeconds = 120.5f;
            config.Smuggling.LossPercentByMinimumSupport[0] = 0;
            GameManager manager = new GameManager(
                new GameRoot(config),
                TestGameData.Create(config)
            );

            manager.SetGameSpeed(TickSpeed.Fast);
            Assert.AreEqual(2.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.Medium);
            Assert.AreEqual(12.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.Slow);
            Assert.AreEqual(90.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.VerySlow);
            Assert.AreEqual(120.5f, GetTickInterval(manager));
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

            _ = TestContent.CreateGameManager(game);

            Assert.IsNotEmpty(
                alliance.ResearchCatalog,
                "Alliance research catalog should be rebuilt after GameManager construction"
            );
            Assert.IsNotEmpty(
                empire.ResearchCatalog,
                "Empire research catalog should be rebuilt after GameManager construction"
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

            GameManager manager = TestContent.CreateGameManager(game);

            manager.ProcessTick();

            Assert.IsEmpty(faction.Messages[MessageType.Manufacturing]);
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
            GameManager manager = TestContent.CreateGameManager(game);
            List<VictoryResult> declarations = new List<VictoryResult>();
            manager.VictoryDeclared += declarations.Add;

            manager.ProcessTick();
            manager.ProcessTick();

            Assert.AreEqual(1, declarations.Count);
            Assert.AreSame(alliance, declarations[0].Winner);
            Assert.AreSame(empire, declarations[0].Loser);
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
            GameManager manager = TestContent.CreateGameManager(game);

            manager.ProcessTick();

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

            GameManager manager = TestContent.CreateGameManager(game);
            manager.MovementSystem.RequestMove(starfighter, destination);

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

            manager.ProcessTick();

            Assert.AreSame(fallback, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        [Test]
        public void ProcessTick_SabotageResult_RemovesDestroyedObjectFromActorSnapshot()
        {
            GameRoot game = new GameRoot(TestContent.Data.GameConfig);
            Faction alliance = new Faction
            {
                InstanceID = "FNALL1",
                DisplayName = "Alliance",
                PlayerID = "alliance_player",
            };
            Faction empire = new Faction
            {
                InstanceID = "FNEMP1",
                DisplayName = "Empire",
                PlayerID = "empire_player",
            };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);

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
            FogOfWarSystem fog = new FogOfWarSystem(game);
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
                        Actions = new List<GameAction>
                        {
                            new EmitResultAction(
                                new GameObjectSabotagedResult
                                {
                                    SabotagedObject = mine,
                                    Saboteur = han,
                                    Context = planet,
                                }
                            ),
                        },
                    }
                );

            GameManager manager = TestContent.CreateGameManager(game);

            manager.ProcessTick();

            GalaxyMap view = manager.GetFogOfWarSystem().BuildFactionView(alliance);
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

            GameManager manager = TestContent.CreateGameManager(game);
            manager.MovementSystem.RequestMove(new List<IMovable> { arrivingFleet }, destination);

            manager.ProcessTick();

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
        public void ProcessTick_FleetArrivesAtPlanetaryStarfighters_CreatesPendingCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction alliance = new Faction
            {
                InstanceID = "FNALL1",
                DisplayName = "Alliance",
                PlayerID = "player",
            };
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
                            new GameEventTrigger(
                                "core:unit.arrived",
                                ("SourceEventInstanceID", "sourceEventInstanceID")
                            ),
                        },
                        Conditionals = new List<GameConditional>
                        {
                            new EvaluateBindingConditional
                            {
                                Binding = "$sourceEventInstanceID",
                                Comparison = ComparisonOperator.Equal,
                                CompareTo = "SOME_OTHER_EVENT",
                            },
                        },
                    }
                );

            GameManager manager = TestContent.CreateGameManager(game);
            manager.MovementSystem.RequestMove(new List<IMovable> { arrivingFleet }, destination);

            manager.ProcessTick();

            Assert.IsTrue(
                manager.SpaceCombatSystem.TryGetPendingCombat(out PendingCombatResult pending)
            );
            Assert.AreSame(arrivingFleet, pending.AttackerFleet);
            Assert.IsNull(pending.DefenderFleet);
            Assert.AreEqual(alliance.InstanceID, pending.AttackerOwnerInstanceID);
            Assert.AreEqual(empire.InstanceID, pending.DefenderOwnerInstanceID);
            Assert.AreSame(destination, pending.Planet);
        }

        [Test]
        public void ProcessTick_PendingCombat_CompletesOnlyStartedTick()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction alliance = new Faction
            {
                InstanceID = "FNALL1",
                DisplayName = "Alliance",
                PlayerID = "player",
            };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);

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

            GameManager manager = TestContent.CreateGameManager(game);
            int completedTicks = 0;
            manager.TickCompleted += () => completedTicks++;

            manager.ProcessTick();
            int pendingCombatTick = game.CurrentTick;
            manager.ProcessTick();

            Assert.AreEqual(pendingCombatTick, game.CurrentTick);
            Assert.AreEqual(1, completedTicks);
        }

        [Test]
        public void ProcessTick_PausedGame_DoesNotAdvanceTick()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            GameManager manager = TestContent.CreateGameManager(game);
            manager.SetGameSpeed(TickSpeed.Paused);

            manager.ProcessTick();

            Assert.AreEqual(0, game.CurrentTick);
        }

        [Test]
        public void AdvanceTime_CompletedInterval_ProcessesTickAndRaisesTickCompleted()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "FACTION", DisplayName = "Faction" });
            GameManager manager = TestContent.CreateGameManager(game);
            manager.SetGameSpeed(TickSpeed.Fast);
            int completedTicks = 0;
            manager.TickCompleted += () => completedTicks++;

            manager.AdvanceTime(config.GameSpeed.FastTickIntervalSeconds);

            Assert.AreEqual(1, game.CurrentTick);
            Assert.AreEqual(1, completedTicks);
        }

        [Test]
        public void AdvanceTime_BelowCompletedInterval_DoesNotProcessTick()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            GameManager manager = TestContent.CreateGameManager(game);
            manager.SetGameSpeed(TickSpeed.Fast);
            int completedTicks = 0;
            manager.TickCompleted += () => completedTicks++;

            manager.AdvanceTime(config.GameSpeed.FastTickIntervalSeconds / 2f);

            Assert.AreEqual(0, game.CurrentTick);
            Assert.AreEqual(0, completedTicks);
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

            GameManager manager = TestContent.CreateGameManager(game);
            Assert.IsTrue(
                manager.MissionSystem.InitiateMission(
                    new MissionStartRequest
                    {
                        MissionTypeID = MissionTypeIDs.Diplomacy,
                        Location = planet,
                        MainParticipants = new List<IMissionParticipant> { diplomat },
                    }
                )
            );
            Assert.IsNotNull(diplomat.Movement);

            Assert.IsTrue(
                manager.MovementSystem.TryRequestMove(
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
            List<GameResult> missionResults = manager.MissionSystem.ProcessTick();

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

            GameManager manager = TestContent.CreateGameManager(game);

            Assert.IsTrue(
                manager.MovementSystem.TryRequestMove(
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
            GameManager manager = TestContent.CreateGameManager(game);

            bool scrapped = manager.MaintenanceSystem.TryScrap(
                new IManufacturable[] { regiment },
                owner.InstanceID
            );

            Assert.IsTrue(scrapped);
            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

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
                };
            }

            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return fleet;
        }

        private static float? GetTickInterval(GameManager manager)
        {
            FieldInfo field = typeof(GameManager).GetField(
                "_tickInterval",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            return (float?)field?.GetValue(manager);
        }

        private sealed class EmitResultAction : GameAction
        {
            private readonly GameResult _result;

            internal EmitResultAction(GameResult result)
            {
                _result = result;
            }

            internal override void Execute(GameActionContext context)
            {
                context.Record(_result);
            }
        }
    }
}
