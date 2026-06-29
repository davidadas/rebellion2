using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MissionSystemTests
    {
        // Builds a game with one planet, one officer parented to the planet (not the mission),
        // and optionally assigns the planet to the faction so GetNearestFriendlyPlanetTo returns it.
        private (GameRoot game, Planet planet, Officer officer, MovementSystem movement) BuildScene(
            bool factionOwnsPlanet
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction { InstanceID = "empire" };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = factionOwnsPlanet ? "empire" : null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
            };
            // Parent to planet so IsOnMission() = false and IsMovable() = true.
            game.AttachNode(officer, planet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            return (game, planet, officer, movement);
        }

        // Creates a mission with the officer in MainParticipants (but officer stays parented to
        // the planet, not the mission) so IncrementProgress counts down and IsMovable() holds.
        private StubMission CreateMission(GameRoot game, Planet planet, Officer officer)
        {
            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            return mission;
        }

        private static void SetFoilTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.Foil = table;
        }

        private static void SetDecoyTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.Decoy = table;
        }

        private static void SetKillOrCaptureTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.KillOrCapture = table;
        }

        private static Regiment CreateCompletedRegiment(string id, string ownerInstanceID)
        {
            return new Regiment
            {
                InstanceID = id,
                OwnerInstanceID = ownerInstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static void AddResearchFacilities(GameRoot game, Planet planet)
        {
            planet.EnergyCapacity = 10;
            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = planet.OwnerInstanceID,
                    ProductionType = ManufacturingType.Ship,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Building
                {
                    InstanceID = "construction",
                    OwnerInstanceID = planet.OwnerInstanceID,
                    ProductionType = ManufacturingType.Building,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
        }

        private static MissionStartRequest CreateRequest(
            string missionTypeID,
            IMissionParticipant participant,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode specificTarget = null
        )
        {
            return CreateRequest(
                missionTypeID,
                new List<IMissionParticipant> { participant },
                new List<IMissionParticipant>(),
                target,
                targetOfficer,
                discipline,
                specificTarget
            );
        }

        private static MissionStartRequest CreateRequest(
            string missionTypeID,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode specificTarget = null
        )
        {
            return new MissionStartRequest
            {
                MissionTypeID = missionTypeID,
                Target = target,
                TargetOfficer = targetOfficer,
                Discipline = discipline,
                SpecificTarget = specificTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
            };
        }

        private (
            GameRoot game,
            Planet planet,
            Officer spy,
            Officer defender,
            MovementSystem movement
        ) BuildDetectionScene()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "rebels", 50 } },
            };
            game.AttachNode(planet, system);

            Officer spy = EntityFactory.CreateOfficer("spy", "empire");
            Officer defender = EntityFactory.CreateOfficer("defender", "rebels");
            game.AttachNode(defender, planet);

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "rebels",
                DefenseRating = 100,
            };
            game.AttachNode(regiment, planet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            return (game, planet, spy, defender, movement);
        }

        private (
            GameRoot game,
            Planet origin,
            Planet targetPlanet,
            Officer participant,
            Officer target,
            MissionSystem missions
        ) BuildOfficerTargetMissionScene(bool friendlyTarget, bool capturedTarget)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet targetPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                EnergyCapacity = 5,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(origin, system);
            game.AttachNode(targetPlanet, system);

            Officer participant = EntityFactory.CreateOfficer("participant", "empire");
            game.AttachNode(participant, origin);

            Officer target = EntityFactory.CreateOfficer(
                "target",
                friendlyTarget ? "empire" : "rebels"
            );
            target.IsCaptured = capturedTarget;
            target.CaptorInstanceID = capturedTarget ? "rebels" : null;
            game.AttachNode(target, targetPlanet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            MissionSystem missions = new MissionSystem(game, new FixedRNG(0.0), movement);
            return (game, origin, targetPlanet, participant, target, missions);
        }

        [Test]
        public void UpdateMission_CompletedNoFriendlyPlanet_SkipsMovement()
        {
            // Faction owns no planets and mission planet is unowned — no valid destination,
            // movement skipped. Officer is not attached to the scene graph so the planet
            // ownership check is never triggered during setup.
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem planetSystem = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetSystem, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, planetSystem);

            // Officer is NOT attached to the scene graph — just in MainParticipants so
            // IncrementProgress counts down and IsOnMission() returns false.
            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
            };

            FogOfWarSystem fogOfWar = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fogOfWar);
            MissionSystem missionSystem = new MissionSystem(game, new StubRNG(), movement);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = null;
            Assert.DoesNotThrow(
                () => results = missionSystem.UpdateMission(mission),
                "Should not throw when faction owns no planets"
            );

            Assert.IsNull(
                officer.Movement,
                "Officer should not have movement queued when no valid destination exists"
            );
        }

        [Test]
        public void UpdateMission_MissingOwnerFaction_DetachesMission()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);

            PlanetSystem planetSystem = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(planetSystem, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, planetSystem);

            StubMission mission = new StubMission(null, planet.InstanceID);
            game.AttachNode(mission, planet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            MissionSystem missionSystem = new MissionSystem(game, new StubRNG(), movement);

            Assert.DoesNotThrow(() => missionSystem.UpdateMission(mission));
            Assert.IsFalse(game.GetSceneNodesByType<StubMission>().Contains(mission));
        }

        [Test]
        public void UpdateMission_CompletedParticipantParentedToMission_DoesNotThrow()
        {
            // Regression: officer parented to the mission (as happens after Initiate moves them
            // there) caused IsMovable() to return false and RequestMove to throw on teardown.
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);

            // Simulate the officer having arrived at the mission mid-execution.
            game.DetachNode(officer);
            officer.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            Assert.DoesNotThrow(() => system.UpdateMission(mission));
        }

        [Test]
        public void UpdateMission_CompletedParticipantOnNeutralPlanet_DoesNotThrow()
        {
            // Regression: neutral planet (null owner) must not be used as reparent target —
            // AddOfficer rejects officers whose faction doesn't match the planet owner.
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Officer officer = new Officer { InstanceID = "o1", OwnerInstanceID = "empire" };
            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            officer.SetParent(mission);

            MissionSystem missionSystem = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            Assert.DoesNotThrow(() => missionSystem.UpdateMission(mission));
        }

        [Test]
        public void UpdateMission_OnCompletion_DetachesMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.UpdateMission(mission);

            Assert.IsNull(
                mission.GetParent(),
                "Mission should be detached from scene graph after completion"
            );
        }

        /// <summary>
        /// Builds a scene with a rebels-owned planet, a rebels officer running Mission,
        /// and an empire officer running Mission. Both missions are advanced to
        /// MaxProgress - 1 so a single UpdateMission call completes each one.
        /// The InciteUprising table is seeded to guarantee success with StubRNG.
        /// </summary>
        private (
            GameRoot game,
            Mission diplomacyMission,
            Mission inciteMission,
            MissionSystem missionSystem
        ) BuildConcurrentMissionsScene()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);

            Faction rebels = new Faction { InstanceID = "rebels" };
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(rebels);
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet rebelsPlanet = new Planet
            {
                InstanceID = "rebels_planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "rebels", 60 } },
            };
            game.AttachNode(rebelsPlanet, system);

            Planet empirePlanet = new Planet
            {
                InstanceID = "empire_planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 60 } },
            };
            game.AttachNode(empirePlanet, system);

            Officer rebelsOfficer = EntityFactory.CreateOfficer("rebels_o1", "rebels");
            game.AttachNode(rebelsOfficer, rebelsPlanet);

            Officer empireOfficer = EntityFactory.CreateOfficer("empire_o1", "empire");
            game.AttachNode(empireOfficer, empirePlanet);

            rebelsPlanet.AddVisitor("rebels");

            Mission diplomacyMission = MissionTestFactory.TryCreate(
                MissionTypeIDs.Diplomacy,
                game,
                "rebels",
                rebelsPlanet,
                new List<IMissionParticipant> { rebelsOfficer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(diplomacyMission, rebelsPlanet);
            game.Config.ProbabilityTables.Mission.Diplomacy = new Dictionary<int, int>
            {
                { -200, 100 },
            };

            Mission inciteMission = MissionTestFactory.TryCreate(
                MissionTypeIDs.InciteUprising,
                game,
                "empire",
                rebelsPlanet,
                new List<IMissionParticipant> { empireOfficer },
                new List<IMissionParticipant>()
            );
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -200, 100 },
            };
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { 0, 0 } };
            game.AttachNode(inciteMission, rebelsPlanet);

            diplomacyMission.Initiate(0);
            inciteMission.Initiate(0);

            while (diplomacyMission.CurrentProgress < diplomacyMission.MaxProgress - 1)
                diplomacyMission.IncrementProgress();
            while (inciteMission.CurrentProgress < inciteMission.MaxProgress - 1)
                inciteMission.IncrementProgress();

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            MissionSystem missionSystem = new MissionSystem(game, new StubRNG(), movement);

            return (game, diplomacyMission, inciteMission, missionSystem);
        }

        [Test]
        public void UpdateMission_DiploBeforeIncite_DiploCanceledAfterUprisingFires()
        {
            // Diplo completes before incite on the same turn: it re-initiates because the
            // uprising hasn't fired yet when its ShouldRepeatAfterCompletion is evaluated.
            // Incite then fires, starting the uprising.
            // On the following UpdateMission, the pre-tick abort check cancels diplo.
            (
                GameRoot game,
                Mission diplomacyMission,
                Mission inciteMission,
                MissionSystem missionSystem
            ) = BuildConcurrentMissionsScene();

            missionSystem.UpdateMission(diplomacyMission); // diplo completes, re-initiates
            missionSystem.UpdateMission(inciteMission); // incite completes, uprising starts

            // Diplo survived this turn (uprising fired after it ran), but is now re-initiated.
            // The next UpdateMission triggers the pre-tick abort check.
            missionSystem.UpdateMission(diplomacyMission);

            Assert.AreEqual(
                0,
                game.GetSceneNodesByType<Mission>().Count,
                "Mission should be canceled on the tick after the uprising fires"
            );
        }

        [Test]
        public void UpdateMission_InciteBeforeDiplo_DiploCanceledImmediately()
        {
            // Incite completes first: uprising fires before diplo gets its turn this turn.
            // When UpdateMission runs for diplo, the pre-tick abort check catches it
            // immediately — diplo never executes.
            (
                GameRoot game,
                Mission diplomacyMission,
                Mission inciteMission,
                MissionSystem missionSystem
            ) = BuildConcurrentMissionsScene();

            missionSystem.UpdateMission(inciteMission); // incite completes, uprising starts
            missionSystem.UpdateMission(diplomacyMission); // pre-tick guard fires, diplo canceled

            Assert.AreEqual(
                0,
                game.GetSceneNodesByType<Mission>().Count,
                "Mission should be canceled immediately when uprising fires before its turn"
            );
        }

        [Test]
        public void TearDownMission_ParticipantAttachedToMissionViaSceneGraph_DoesNotThrow()
        {
            // Regression: when BeginMission reparents an officer to the mission via
            // game.AttachNode, TearDownMission previously threw "cannot attach node because
            // it already has a parent" because it called AttachNode without DetachNode first.
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);

            // Simulate BeginMission: move officer to mission via scene graph (not SetParent).
            game.DetachNode(officer);
            game.AttachNode(officer, mission);

            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            Assert.DoesNotThrow(() => system.UpdateMission(mission));
            Assert.AreEqual(
                planet,
                officer.GetParent(),
                "Officer should be reparented to the mission planet on teardown"
            );
        }

        [Test]
        public void TearDownMission_OriginIsCapitalShipInFleet_ParticipantsReturnToShip()
        {
            // When the officer's recorded origin is a capital ship (i.e. they boarded from a fleet),
            // teardown should return them to that ship, not to the planet directly.
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );

            Fleet fleet = new Fleet { InstanceID = "fleet1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            // Move officer from planet onto the capital ship (pre-mission state).
            game.DetachNode(officer);
            game.AttachNode(officer, ship);

            StubMission mission = CreateMission(game, planet, officer);

            // Simulate BeginMission: record the ship as origin, then move officer to mission.
            mission.OriginInstanceID = ship.InstanceID;
            game.DetachNode(officer);
            game.AttachNode(officer, mission);

            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.UpdateMission(mission);

            Assert.AreEqual(
                ship,
                officer.GetParent(),
                "Officer should return to the capital ship they came from"
            );
        }

        [Test]
        public void TearDownMission_OriginFleetHasMoved_ParticipantsReturnToNearestFriendlyPlanet()
        {
            // Fleet moved away mid-mission — officer falls back to nearest friendly planet.
            (GameRoot game, Planet planetA, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );

            PlanetSystem systemB = new PlanetSystem
            {
                InstanceID = "sys2",
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(systemB, game.Galaxy);
            Planet planetB = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planetB, systemB);

            Fleet fleet = new Fleet { InstanceID = "fleet1", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planetA);
            game.AttachNode(ship, fleet);

            game.DetachNode(officer);
            game.AttachNode(officer, ship);

            StubMission mission = CreateMission(game, planetA, officer);
            mission.OriginInstanceID = ship.InstanceID;
            game.DetachNode(officer);
            game.AttachNode(officer, mission);

            // Fleet moves away from planet A to planet B while the mission is in progress.
            game.DetachNode(ship);
            game.DetachNode(fleet);
            game.AttachNode(fleet, planetB);
            game.AttachNode(ship, fleet);

            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.UpdateMission(mission);

            Assert.AreEqual(
                planetA,
                officer.GetParent(),
                "Officer should return to the nearest friendly planet when the origin fleet has moved"
            );
        }

        [Test]
        public void BeginMission_ParticipantAssigned_SetsParticipantParentToMission()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(empirePlanet, system);

            Planet targetPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(targetPlanet, system);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, empirePlanet);
            Regiment sabotageTarget = CreateCompletedRegiment("r1", "rebels");
            game.AttachNode(sabotageTarget, targetPlanet);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            MissionSystem missionSystem = new MissionSystem(game, new StubRNG(), movement);

            missionSystem.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    officer,
                    targetPlanet,
                    specificTarget: sabotageTarget
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().FirstOrDefault();
            Assert.IsNotNull(mission, "Mission should be created");
            Assert.AreEqual(
                mission,
                officer.GetParent(),
                "Participant should be parented to the mission after BeginMission"
            );
        }

        [Test]
        public void IsOnMission_AfterBeginMission_ReturnsTrue()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(empirePlanet, system);

            Planet targetPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(targetPlanet, system);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, empirePlanet);
            Regiment sabotageTarget = CreateCompletedRegiment("r1", "rebels");
            game.AttachNode(sabotageTarget, targetPlanet);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            MissionSystem missionSystem = new MissionSystem(game, new StubRNG(), movement);

            missionSystem.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    officer,
                    targetPlanet,
                    specificTarget: sabotageTarget
                )
            );

            Assert.IsTrue(
                officer.IsOnMission(),
                "Officer should report IsOnMission after BeginMission"
            );
        }

        [Test]
        public void ProcessTick_WithCompletedMission_ReturnsMissionCompletedResult()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.ProcessTick();

            Assert.IsTrue(
                results.Any(r => r is MissionCompletedResult),
                "ProcessTick should aggregate results from all missions and include MissionCompletedResult"
            );
        }

        [Test]
        public void Execute_WithSpecialForcesParticipant_AppearsInParticipants()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );

            SpecialForces sf = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                Movement = null,
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(sf);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.Execute(game, new StubRNG());
            MissionCompletedResult completedResult = results
                .OfType<MissionCompletedResult>()
                .First();

            Assert.IsTrue(
                completedResult.Participants.Any(p => p.InstanceID == "sf1"),
                "SpecialForces participant must appear in Participants"
            );
        }

        [Test]
        public void Execute_WithDecoyParticipant_DecoyAppearsInParticipants()
        {
            // Both main and decoy participants should appear in MissionCompletedResult.Participants.
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );

            Officer decoy = new Officer
            {
                InstanceID = "o2",
                DisplayName = "o2",
                OwnerInstanceID = "empire",
                Movement = null,
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            mission.DecoyParticipants.Add(decoy);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.Execute(game, new StubRNG());
            MissionCompletedResult completedResult = results
                .OfType<MissionCompletedResult>()
                .First();

            Assert.IsTrue(
                completedResult.Participants.Any(p => p.InstanceID == "o2"),
                "Decoy must appear in Participants"
            );
        }

        [Test]
        public void UpdateMission_AnyParticipantInTransit_DoesNotProgressOrExecute()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            Officer traveler = new Officer
            {
                InstanceID = "o2",
                OwnerInstanceID = "empire",
                Movement = new MovementState { TransitTicks = 10, TicksElapsed = 0 },
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            mission.MainParticipants.Add(traveler);
            officer.SetParent(mission);
            traveler.SetParent(mission);
            mission.Initiate(0);

            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.AreEqual(0, mission.CurrentProgress);
            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, game.GetSceneNodesByType<StubMission>().Count);
        }

        [Test]
        public void UpdateMission_AnyParticipantInTransit_NoDetectionOrCapture()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();
            Officer traveler = new Officer
            {
                InstanceID = "o2",
                OwnerInstanceID = "empire",
                Movement = new MovementState { TransitTicks = 10, TicksElapsed = 0 },
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.MainParticipants.Add(traveler);
            spy.SetParent(mission);
            traveler.SetParent(mission);
            mission.Initiate(0);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsFalse(spy.IsCaptured);
            Assert.IsFalse(traveler.IsCaptured);
            Assert.IsFalse(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(r => r.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void UpdateMission_MainParticipantRemoved_ReturnsFailedMissionCompletedResult()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            mission.Initiate(0);
            mission.RemoveChild(officer);
            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            List<GameResult> results = system.UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.Failure, completed.CompletionReason);
            Assert.IsFalse(completed.CanContinue);
            Assert.AreEqual(0, game.GetSceneNodesByType<StubMission>().Count);
        }

        [Test]
        public void UpdateMission_DetectionRollFails_MissionContinues()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 10 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.Initiate(1);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.99), movement);

            system.UpdateMission(mission);

            Assert.IsFalse(
                spy.IsCaptured,
                "Officer should not be captured when detection roll fails"
            );
            Assert.AreEqual(
                1,
                mission.CurrentProgress,
                "Mission progress should increment when not detected"
            );
        }

        [Test]
        public void UpdateMission_RatingBasedFoilScore_DetectsMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            spy.SetBaseRating(OfficerRating.Diplomacy, 200);
            defender.SetBaseRating(OfficerRating.Espionage, 10);
            planet.Regiments.Single().DefenseRating = 10;

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.Config.ProbabilityTables.Mission.FoilDefenderScalingPercent = 35;
            game.Config.ProbabilityTables.Mission.FoilFlatScoreAdjustment = -1;
            SetFoilTable(game, new Dictionary<int, int> { { 0, 0 }, { 50, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsTrue(spy.IsCaptured);
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void UpdateMission_FoilSupport_DetectsMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            spy.SetBaseRating(OfficerRating.Diplomacy, 100);
            defender.SetBaseRating(OfficerRating.Espionage, 0);
            planet.Regiments.Single().DefenseRating = 0;

            SpecialForces support = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            support.SetBaseRating(OfficerRating.Espionage, 100);
            game.AttachNode(support, planet);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.Config.ProbabilityTables.Mission.FoilDefenderScalingPercent = 35;
            game.Config.ProbabilityTables.Mission.FoilFlatScoreAdjustment = -1;
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 }, { 2, 0 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsTrue(spy.IsCaptured);
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void UpdateMission_DetectionSucceedsHighCapture_CapturesParticipant()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsTrue(spy.IsCaptured, "Officer should be captured when detection succeeds");
            Assert.AreEqual(
                "rebels",
                spy.CaptorInstanceID,
                "CaptorInstanceID should be set to the planet owner's faction"
            );
            Assert.IsTrue(
                results.Any(r => r is OfficerCaptureStateResult),
                "Should produce OfficerCaptureStateResult"
            );
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(r => r.Outcome == MissionOutcome.Foiled),
                "Should produce MissionCompletedResult with Foiled outcome"
            );
        }

        [Test]
        public void UpdateMission_EspionageDetected_FoilsWithoutCaptureOrKill()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            planet.VisitingFactionIDs.Add("empire");
            Mission mission = MissionTestFactory.TryCreate(
                MissionTypeIDs.Espionage,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { spy },
                new List<IMissionParticipant>()
            );
            Assert.IsNotNull(mission);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsFalse(spy.IsCaptured);
            Assert.IsFalse(spy.IsKilled);
            Assert.IsFalse(results.Any(r => r is OfficerCaptureStateResult));
            Assert.IsFalse(results.Any(r => r is OfficerKilledResult));
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void UpdateMission_DetectionSucceedsWithoutCaptureOrKill_FoilsMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementSystem movement) =
                BuildDetectionScene();

            spy.IsCaptured = true;
            spy.CaptorInstanceID = "rebels";

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);
            mission.Initiate(0);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsFalse(results.Any(result => result is OfficerCaptureStateResult));
            Assert.IsFalse(results.Any(result => result is OfficerKilledResult));
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void UpdateMission_DetectionSucceedsHighCapture_MovesCaptiveToMissionPlanet()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.AreEqual(
                planet,
                spy.GetParent(),
                "Captured mission participant should stay on the mission planet"
            );
            Assert.AreEqual(
                0,
                game.GetSceneNodesByType<StubMission>().Count,
                "Mission should be removed after a participant is captured"
            );
        }

        [Test]
        public void UpdateMission_DetectionSucceedsLowCapture_KillsParticipant()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 0 } });
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsTrue(spy.IsKilled, "Officer should be killed when capture probability is 0");
            Assert.IsTrue(
                results.Any(r => r is OfficerKilledResult),
                "Should produce OfficerKilledResult"
            );
        }

        [Test]
        public void UpdateMission_ParticipantInjuredAfterInitiation_DoesNotAbortMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            mission.Initiate(0);
            mission.SetExecutionTick(5);

            officer.InjuryPoints = 1;

            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            system.UpdateMission(mission);

            Assert.AreEqual(
                1,
                game.GetSceneNodesByType<StubMission>().Count,
                "Mission should not abort when participant membership is unchanged"
            );
        }

        [Test]
        public void UpdateMission_DetectionSucceedsWithoutCaptureTable_UsesConfiguredDefault()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            game.Config.ProbabilityTables.Mission.DefaultKillOrCaptureProbability = 100;

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int>());
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsTrue(spy.IsCaptured, "Officer should use default capture probability");
            Assert.IsTrue(
                results.Any(r => r is OfficerCaptureStateResult),
                "Should produce OfficerCaptureStateResult"
            );
        }

        [Test]
        public void UpdateMission_DetectionOnOwnPlanet_NeverDetected()
        {
            (GameRoot game, Planet planet, Officer spy, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.IsFalse(spy.IsCaptured, "Missions on own planets should never be detected");
        }

        [Test]
        public void UpdateMission_NoDefender_DoesNotFoil()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            game.DetachNode(defender);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 0 } });
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsFalse(spy.IsKilled);
            Assert.IsFalse(spy.IsCaptured);
            Assert.IsFalse(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void UpdateMission_DetectionWithDecoy_PreventsCapture()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            decoy.SetBaseRating(OfficerRating.Espionage, 200);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            mission.DecoyParticipantRating = OfficerRating.Espionage;
            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.DecoyParticipants.Add(decoy);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.IsFalse(spy.IsCaptured, "Successful decoy should prevent capture");
        }

        [Test]
        public void UpdateMission_DetectionWithDecoySkill_UsesConfiguredSkill()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            // Decoy has Espionage=0 but Combat=200. DecoyParticipantRating is set to Combat.
            // If the system incorrectly uses Espionage, the decoy fails and the spy is captured.
            Officer decoy = new Officer
            {
                InstanceID = "decoy",
                OwnerInstanceID = "empire",
                Ratings = new Dictionary<OfficerRating, int>
                {
                    { OfficerRating.Espionage, 0 },
                    { OfficerRating.Combat, 200 },
                    { OfficerRating.Diplomacy, 0 },
                    { OfficerRating.Leadership, 0 },
                },
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            mission.DecoyParticipantRating = OfficerRating.Combat;
            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.DecoyParticipants.Add(decoy);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.IsFalse(
                spy.IsCaptured,
                "Decoy should use DecoyParticipantRating (Combat=80) not always Espionage"
            );
        }

        [Test]
        public void UpdateMission_DetectionWithStrongDefense_DecoyFails()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            // High defense score makes decoy probability very low.
            for (int i = 0; i < 5; i++)
            {
                Regiment regiment = new Regiment
                {
                    InstanceID = $"extra_r{i}",
                    OwnerInstanceID = "rebels",
                    DefenseRating = 50,
                };
                game.AttachNode(regiment, planet);
            }

            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            mission.DecoyParticipantRating = OfficerRating.Espionage;
            SetDecoyTable(game, new Dictionary<int, int> { { -200, 0 }, { 200, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.DecoyParticipants.Add(decoy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.IsTrue(
                spy.IsCaptured,
                "Strong target defense should make decoy fail, allowing capture"
            );
        }

        [Test]
        public void UpdateMission_DetectionPicksOneRandomDecoy_NotAll()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            // Two decoys: one with Espionage=0 (will fail), one with Espionage=200 (would pass).
            // FixedRNG NextInt returns min (0), so first decoy is always picked.
            // If all decoys were checked, the second would save the spy.
            Officer weakDecoy = EntityFactory.CreateOfficer("decoy_weak", "empire");
            weakDecoy.SetBaseRating(OfficerRating.Espionage, 0);

            Officer strongDecoy = EntityFactory.CreateOfficer("decoy_strong", "empire");
            strongDecoy.SetBaseRating(OfficerRating.Espionage, 200);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            mission.DecoyParticipantRating = OfficerRating.Espionage;
            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.DecoyParticipants.Add(weakDecoy);
            mission.DecoyParticipants.Add(strongDecoy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.IsTrue(
                spy.IsCaptured,
                "Only one random decoy should be rolled, not all — weak decoy picked first should fail"
            );
        }

        [Test]
        public void UpdateMission_DetectionCapturesParticipant_CancelsMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            Officer secondSpy = EntityFactory.CreateOfficer("o2", "empire");

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.MainParticipants.Add(secondSpy);
            spy.SetParent(mission);
            secondSpy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsNull(
                mission.GetParent(),
                "Mission should be torn down when any participant is captured"
            );
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(r => r.Outcome == MissionOutcome.Foiled),
                "Should produce Foiled outcome when mission is canceled by detection"
            );
        }

        [Test]
        public void UpdateMission_DetectionWithSpecialForces_DestroysUnit()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            SpecialForces sf = new SpecialForces { InstanceID = "sf1", OwnerInstanceID = "empire" };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetKillOrCaptureTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(sf);
            sf.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsNull(sf.GetParent(), "SpecialForces should be detached when detected");
            Assert.IsTrue(
                results.Any(r => r is GameObjectDestroyedResult),
                "Should produce GameObjectDestroyedResult for destroyed SpecialForces"
            );
        }

        [Test]
        public void InitiateMission_ResearchWithDiscipline_AttachesResearchMissionToPlanet()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            FogOfWarSystem fog = new FogOfWarSystem(game);
            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            system.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Research,
                    officer,
                    planet,
                    discipline: ResearchDiscipline.FacilityDesign
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().FirstOrDefault();
            Assert.IsNotNull(mission, "Research mission should be created and attached");
            Assert.AreEqual(
                ResearchDiscipline.FacilityDesign,
                ((ResearchMission)mission).Discipline
            );
            Assert.AreEqual(planet, mission.GetParent());
        }

        [Test]
        public void GetAvailableMissionOptions_OwnPlanetResearch_ReturnsResearchOptions()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.ShipResearch = 1;
            officer.TroopResearch = 1;
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            MissionSystem missions = new MissionSystem(game, new StubRNG(), movement);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, officer, planet)
            );

            MissionOption[] researchOptions = options
                .Where(option => option.MissionTypeID == MissionTypeIDs.Research)
                .ToArray();
            Assert.AreEqual(3, researchOptions.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    ResearchDiscipline.ShipDesign,
                    ResearchDiscipline.TroopTraining,
                    ResearchDiscipline.FacilityDesign,
                },
                researchOptions.Select(option => option.Discipline).ToArray()
            );
        }

        [Test]
        public void GetAvailableMissionOptions_ResearchWithSingleMatchingRating_ReturnsMatchingResearchOption()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.ShipResearch = 1;
            AddResearchFacilities(game, planet);
            MissionSystem missions = new MissionSystem(game, new StubRNG(), movement);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, officer, planet)
            );

            MissionOption[] researchOptions = options
                .Where(option => option.MissionTypeID == MissionTypeIDs.Research)
                .ToArray();
            Assert.AreEqual(1, researchOptions.Length);
            Assert.AreEqual(ResearchDiscipline.ShipDesign, researchOptions.Single().Discipline);
        }

        [Test]
        public void GetAvailableMissionOptions_TroopTrainingWithoutFacility_ExcludesResearchOption()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.TroopResearch = 1;
            MissionSystem missions = new MissionSystem(game, new StubRNG(), movement);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, officer, planet)
            );

            Assert.IsFalse(options.Any(option => option.MissionTypeID == MissionTypeIDs.Research));
        }

        [Test]
        public void GetAvailableMissionOptions_ResearchWithoutMatchingRating_ExcludesResearchOptions()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            MissionSystem missions = new MissionSystem(game, new StubRNG(), movement);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, officer, planet)
            );

            Assert.IsFalse(options.Any(option => option.MissionTypeID == MissionTypeIDs.Research));
        }

        [Test]
        public void GetAvailableMissionOptions_DisallowedResearch_ExcludesResearchOptions()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.ShipResearch = 1;
            officer.TroopResearch = 1;
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            game.Factions.Single().DisallowedMissionTypeIDs.Add(MissionTypeIDs.Research);
            MissionSystem missions = new MissionSystem(game, new StubRNG(), movement);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, officer, planet)
            );

            Assert.IsFalse(options.Any(option => option.MissionTypeID == MissionTypeIDs.Research));
        }

        [Test]
        public void GetAvailableMissionOptions_EnemyPlanetRecruitment_ExcludesRecruitmentOption()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            participant.IsMain = true;
            game.UnrecruitedOfficers.Add(
                new Officer
                {
                    InstanceID = "unrecruited",
                    AllowedOwnerInstanceIDs = new List<string> { "empire" },
                }
            );

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, participant, targetPlanet)
            );

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == MissionTypeIDs.Recruitment)
            );
        }

        [Test]
        public void GetAvailableMissionOptions_PlanetOnlySabotageTarget_ExcludesSabotageOption()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = CreateCompletedRegiment("r1", "rebels");
            game.AttachNode(regiment, targetPlanet);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, participant, targetPlanet)
            );

            Assert.IsFalse(options.Any(option => option.MissionTypeID == MissionTypeIDs.Sabotage));
        }

        [Test]
        public void GetAvailableMissionOptions_ManufacturableSabotageTarget_ReturnsSabotageOption()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = CreateCompletedRegiment("r1", "rebels");
            game.AttachNode(regiment, targetPlanet);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, participant, targetPlanet, specificTarget: regiment)
            );

            Assert.IsTrue(options.Any(option => option.MissionTypeID == MissionTypeIDs.Sabotage));
        }

        [Test]
        public void GetAvailableMissionOptions_ReconnaissanceSpecialForces_ReturnsReconnaissanceOption()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet target = new Planet
            {
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(origin, system);
            game.AttachNode(target, system);

            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Reconnaissance },
            };
            game.AttachNode(specialForces, origin);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            MissionSystem missions = new MissionSystem(game, new StubRNG(), movement);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateRequest(null, specialForces, target)
            );

            Assert.AreEqual(1, options.Count);
            Assert.AreEqual(MissionTypeIDs.Reconnaissance, options.Single().MissionTypeID);
        }

        [Test]
        public void InitiateMission_WithFactionViewObjects_UsesLiveSceneGraphNodes()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = CreateCompletedRegiment("regiment", "rebels");
            game.AttachNode(regiment, targetPlanet);
            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Officer viewParticipant = EntityFactory.CreateOfficer(participant.InstanceID, "empire");
            Regiment viewRegiment = CreateCompletedRegiment(regiment.InstanceID, "rebels");
            viewRegiment.SetParent(viewPlanet);

            bool created = missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    new List<IMissionParticipant> { viewParticipant },
                    new List<IMissionParticipant>(),
                    viewPlanet,
                    specificTarget: viewRegiment
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(targetPlanet, mission.GetParent());
            Assert.AreEqual(participant, mission.MainParticipants.Single());
        }

        [Test]
        public void InitiateMission_EnemyRegimentFactionViewTarget_AttachesToLivePlanet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "rebels");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, targetPlanet);
            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Regiment viewRegiment = EntityFactory.CreateRegiment(regiment.InstanceID, "rebels");
            viewRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            viewRegiment.SetParent(viewPlanet);

            bool created = missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    participant,
                    viewPlanet,
                    specificTarget: viewRegiment
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(targetPlanet, mission.GetParent());
            Assert.AreEqual(targetPlanet.InstanceID, mission.TargetInstanceID);
            Assert.AreEqual(
                regiment.InstanceID,
                ((SabotageMission)mission).SabotageTargetInstanceID
            );
        }

        [Test]
        public void InitiateMission_EnemyOfficerFactionViewTarget_AttachesToLivePlanet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Officer viewTarget = EntityFactory.CreateOfficer(target.InstanceID, "rebels");
            viewTarget.SetParent(viewPlanet);

            bool created = missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Abduction,
                    participant,
                    viewPlanet,
                    specificTarget: viewTarget
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(targetPlanet, mission.GetParent());
            Assert.AreEqual(target.InstanceID, ((AbductionMission)mission).TargetOfficerInstanceID);
        }

        [Test]
        public void CanCreateMission_StaleCompletedViewTarget_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment liveRegiment = EntityFactory.CreateRegiment("regiment", "rebels");
            liveRegiment.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(liveRegiment, targetPlanet);

            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Regiment viewRegiment = EntityFactory.CreateRegiment(liveRegiment.InstanceID, "rebels");
            viewRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            viewRegiment.SetParent(viewPlanet);

            bool canCreate = missions.CanCreateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    participant,
                    viewPlanet,
                    specificTarget: viewRegiment
                )
            );

            Assert.IsTrue(canCreate);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void InitiateMission_StaleCompletedViewTarget_CreatesMissionFromKnownIntel()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment liveRegiment = EntityFactory.CreateRegiment("regiment", "rebels");
            liveRegiment.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(liveRegiment, targetPlanet);

            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Regiment viewRegiment = EntityFactory.CreateRegiment(liveRegiment.InstanceID, "rebels");
            viewRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            viewRegiment.SetParent(viewPlanet);

            bool created = missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    participant,
                    viewPlanet,
                    specificTarget: viewRegiment
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(targetPlanet, mission.GetParent());
            Assert.AreEqual(targetPlanet.InstanceID, mission.TargetInstanceID);
            Assert.AreEqual(
                liveRegiment.InstanceID,
                ((SabotageMission)mission).SabotageTargetInstanceID
            );
        }

        [Test]
        public void UpdateMission_StaleCompletedViewTargetStillBuildingAtArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment liveRegiment = EntityFactory.CreateRegiment("regiment", "rebels");
            liveRegiment.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(liveRegiment, targetPlanet);

            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Regiment viewRegiment = EntityFactory.CreateRegiment(liveRegiment.InstanceID, "rebels");
            viewRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            viewRegiment.SetParent(viewPlanet);

            missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    participant,
                    viewPlanet,
                    specificTarget: viewRegiment
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;

            List<GameResult> results = missions.UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void InitiateMission_IneligibleSpecificTarget_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);

            bool created = missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    participant,
                    targetPlanet,
                    specificTarget: target
                )
            );

            Assert.IsFalse(created);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void InitiateMission_SabotageTargetOnDifferentPlanet_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Planet otherPlanet = new Planet
            {
                InstanceID = "other-planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "rebels", 50 } },
            };
            game.AttachNode(otherPlanet, targetPlanet.GetParent());
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "rebels");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, otherPlanet);

            bool created = missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    participant,
                    targetPlanet,
                    specificTarget: regiment
                )
            );

            Assert.IsFalse(created);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void UpdateMission_FactionViewSabotageTargetMissingAtArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "rebels");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, targetPlanet);

            missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Sabotage,
                    participant,
                    targetPlanet,
                    specificTarget: regiment
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            game.DetachNode(regiment);

            List<GameResult> results = missions.UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void UpdateMission_AbductionTargetCapturedBeforeArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Abduction,
                    participant,
                    targetPlanet,
                    specificTarget: target
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            target.IsCaptured = true;

            List<GameResult> results = missions.UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void UpdateMission_AbductionTargetMovedAfterFactionViewSnapshot_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Planet otherPlanet = new Planet
            {
                InstanceID = "other-planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "rebels", 50 } },
            };
            game.AttachNode(otherPlanet, targetPlanet.GetParent());

            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Officer viewTarget = EntityFactory.CreateOfficer(target.InstanceID, "rebels");
            viewTarget.SetParent(viewPlanet);

            game.MoveNode(target, otherPlanet);

            bool created = missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Abduction,
                    participant,
                    viewPlanet,
                    specificTarget: viewTarget
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;

            List<GameResult> results = missions.UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void UpdateMission_AssassinationTargetCapturedBeforeArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Assassination,
                    participant,
                    targetPlanet,
                    specificTarget: target
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            target.IsCaptured = true;

            List<GameResult> results = missions.UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void UpdateMission_RescueTargetFreedBeforeArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionSystem missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: true, capturedTarget: true);
            missions.InitiateMission(
                CreateRequest(
                    MissionTypeIDs.Rescue,
                    participant,
                    targetPlanet,
                    specificTarget: target
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            target.IsCaptured = false;

            List<GameResult> results = missions.UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void TearDownMission_CapturedParticipant_SkipsMovement()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            officer.SetParent(mission);
            officer.IsCaptured = true;

            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.UpdateMission(mission);

            Assert.IsNull(
                officer.Movement,
                "Captured officer should not have movement queued during teardown"
            );
        }

        [Test]
        public void UpdateMission_CapturedParticipantWithDifferentCaptor_StaysOnMissionPlanet()
        {
            (GameRoot game, Planet missionPlanet, Officer officer, MovementSystem movement) =
                BuildScene(factionOwnsPlanet: true);
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            Planet rebelPlanet = new Planet
            {
                InstanceID = "rebel_planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "rebels", 50 } },
            };
            game.AttachNode(rebelPlanet, missionPlanet.GetParent());

            StubMission mission = CreateMission(game, missionPlanet, officer);
            game.MoveNode(officer, mission);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "rebels";

            MissionSystem system = new MissionSystem(game, new StubRNG(), movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.UpdateMission(mission);

            Assert.AreEqual(
                missionPlanet,
                officer.GetParent(),
                "Captured participant should not be moved to a separate captor planet"
            );
        }
    }
}
