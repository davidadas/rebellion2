using System.Collections.Generic;
using System.Linq;
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
        public void Constructor_WithFactions_RebuildsResearchCatalogs()
        {
            GameRoot game = new GameRoot();
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            Assume.That(
                alliance.ResearchCatalog,
                Is.Empty,
                "Catalog must start empty to prove the rebuild populates it"
            );
            Assume.That(empire.ResearchCatalog, Is.Empty);

            _ = new GameManager(game);

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
        public void ProcessTick_EventResults_AddsMessages()
        {
            GameRoot game = new GameRoot();
            Faction faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(faction);
            game.EventPool.Add(
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

            GameManager manager = new GameManager(game);

            manager.ProcessTick();

            Assert.AreEqual(1, faction.Messages[MessageType.Manufacturing].Count);
        }

        [Test]
        public void ProcessTick_ExpiredMessage_RemovesMessageAfterTickAdvances()
        {
            GameConfig config = TestConfig.Create();
            config.Messages.RetentionTicks = 300;
            GameRoot game = new GameRoot(config) { CurrentTick = 400 };
            Faction faction = new Faction { InstanceID = "FACTION" };
            game.Factions.Add(faction);
            faction.AddMessage(new Message(MessageType.Conflict, "Expired") { CreatedTick = 100 });
            GameManager manager = new GameManager(game);

            manager.ProcessTick();

            Assert.IsEmpty(faction.Messages[MessageType.Conflict]);
        }

        [Test]
        public void AdvanceTime_CompletedInterval_ProcessesTickAndRaisesTickCompleted()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "FACTION", DisplayName = "Faction" });
            GameManager manager = new GameManager(game);
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
            GameManager manager = new GameManager(game);
            manager.SetGameSpeed(TickSpeed.Fast);
            int completedTicks = 0;
            manager.TickCompleted += () => completedTicks++;

            manager.AdvanceTime(config.GameSpeed.FastTickIntervalSeconds / 2f);

            Assert.AreEqual(0, game.CurrentTick);
            Assert.AreEqual(0, completedTicks);
        }

        [Test]
        public void ProcessTick_SabotageResult_RemovesDestroyedObjectFromActorSnapshot()
        {
            GameRoot game = new GameRoot(ResourceManager.GetConfig<GameConfig>());
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
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "SYS1", DisplayName = "System" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "PLANET1",
                DisplayName = "Coruscant",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(planet, system);

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
            fog.CaptureSnapshot(alliance, planet, system, 0);
            Assert.IsTrue(
                alliance
                    .Fog.Snapshots["SYS1"]
                    .Planets["PLANET1"]
                    .Buildings.Any(b => b.InstanceID == "MINE1")
            );

            game.DetachNode(mine);

            game.EventPool.Add(
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

            GameManager manager = new GameManager(game);

            manager.ProcessTick();

            GalaxyMap view = manager.GetFogOfWarSystem().BuildFactionView(alliance);
            Planet viewedPlanet = view
                .PlanetSystems.Single(s => s.InstanceID == "SYS1")
                .Planets.Single(p => p.InstanceID == "PLANET1");
            Assert.IsFalse(viewedPlanet.Buildings.Any(b => b.InstanceID == "MINE1"));
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
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "SYS1", DisplayName = "System" };
            game.AttachNode(system, game.GetGalaxyMap());
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
            game.AttachNode(origin, system);
            game.AttachNode(destination, system);

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
            defendingFleet.CapitalShips[0].HasGravityWell = true;

            GameManager manager = new GameManager(game);
            manager.MovementSystem.RequestMove(new List<IMovable> { arrivingFleet }, destination);

            manager.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("ARRIVING"));
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
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "SYS1", DisplayName = "System" };
            game.AttachNode(system, game.GetGalaxyMap());
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
            game.AttachNode(origin, system);
            game.AttachNode(destination, system);

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

            GameManager manager = new GameManager(game);
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
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "SYS1", DisplayName = "System" };
            game.AttachNode(system, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "DEST",
                DisplayName = "Destination",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(planet, system);

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

            GameManager manager = new GameManager(game);
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
            GameManager manager = new GameManager(game);
            manager.SetGameSpeed(TickSpeed.Paused);

            manager.ProcessTick();

            Assert.AreEqual(0, game.CurrentTick);
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
            game.Factions.Add(owner);
            game.Factions.Add(opposition);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "SYSTEM",
                SystemType = PlanetSystemType.OuterRim,
            };
            game.AttachNode(system, game.GetGalaxyMap());
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
            game.AttachNode(planet, system);
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
            game.AttachNode(home, system);

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
                AllowedOwnerInstanceIDs = new List<string> { owner.InstanceID },
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            GameManager manager = new GameManager(game);
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
            Assert.IsEmpty(game.GetSceneNodesByType<Mission>());
            Assert.AreSame(home, diplomat.GetParent());
            Assert.IsNotNull(diplomat.Movement);
            Assert.IsTrue(
                owner
                    .Messages[MessageType.PopularSupport]
                    .Any(message => message.ResultType == MessageResultType.UprisingStarted)
            );
        }

        [Test]
        public void MovementCommand_LastSurfaceRegimentNeutralizesPlanet_ReportsImmediately()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction owner = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            Faction opposition = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(owner);
            game.Factions.Add(opposition);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "SYSTEM",
                SystemType = PlanetSystemType.OuterRim,
            };
            game.AttachNode(system, game.GetGalaxyMap());

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
            game.AttachNode(planet, system);

            Regiment departingRegiment = EntityFactory.CreateRegiment("REGIMENT", owner.InstanceID);
            departingRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(departingRegiment, planet);

            Fleet fleet = EntityFactory.CreateFleet("FLEET", owner.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP",
                OwnerInstanceID = owner.InstanceID,
                AllowedOwnerInstanceIDs = new List<string> { owner.InstanceID },
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            GameManager manager = new GameManager(game);

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
            game.Factions.Add(owner);
            game.Factions.Add(opposition);

            PlanetSystem system = new PlanetSystem { InstanceID = "SYSTEM" };
            game.AttachNode(system, game.GetGalaxyMap());
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
            game.AttachNode(planet, system);
            Regiment regiment = EntityFactory.CreateRegiment("REGIMENT", owner.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, planet);
            GameManager manager = new GameManager(game);

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

        private sealed class EmitResultAction : GameAction
        {
            private readonly GameResult _result;

            internal EmitResultAction(GameResult result)
            {
                _result = result;
            }

            public override List<GameResult> Execute(GameRoot game)
            {
                return new List<GameResult> { _result };
            }
        }
    }
}
