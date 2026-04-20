using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
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
        /// Builds a scene with a rebels-owned planet, a rebels officer running DiplomacyMission,
        /// and an empire officer running InciteUprisingMission. Both missions are advanced to
        /// MaxProgress - 1 so a single UpdateMission call completes each one.
        /// The InciteUprising table is seeded to guarantee success with StubRNG.
        /// </summary>
        private (
            GameRoot game,
            DiplomacyMission diplomacyMission,
            InciteUprisingMission inciteMission,
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

            DiplomacyMission diplomacyMission = DiplomacyMission.TryCreate(
                new MissionContext
                {
                    OwnerInstanceId = "rebels",
                    Target = rebelsPlanet,
                    MainParticipants = new List<IMissionParticipant> { rebelsOfficer },
                    DecoyParticipants = new List<IMissionParticipant>(),
                }
            );
            game.AttachNode(diplomacyMission, rebelsPlanet);

            InciteUprisingMission inciteMission = InciteUprisingMission.TryCreate(
                new MissionContext
                {
                    OwnerInstanceId = "empire",
                    Target = rebelsPlanet,
                    MainParticipants = new List<IMissionParticipant> { empireOfficer },
                    DecoyParticipants = new List<IMissionParticipant>(),
                }
            );
            inciteMission.SuccessProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 100 } }
            );
            game.AttachNode(inciteMission, rebelsPlanet);

            StubRNG rng = new StubRNG();
            diplomacyMission.Initiate(rng);
            inciteMission.Initiate(rng);

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
            // uprising hasn't fired yet when its CanContinue is evaluated.
            // Incite then fires, starting the uprising.
            // On the following UpdateMission, the pre-tick ShouldAbort guard cancels diplo.
            (
                GameRoot game,
                DiplomacyMission diplomacyMission,
                InciteUprisingMission inciteMission,
                MissionSystem missionSystem
            ) = BuildConcurrentMissionsScene();

            missionSystem.UpdateMission(diplomacyMission); // diplo completes, re-initiates
            missionSystem.UpdateMission(inciteMission); // incite completes, uprising starts

            // Diplo survived this turn (uprising fired after it ran), but is now re-initiated.
            // The next UpdateMission triggers the ShouldAbort pre-tick guard.
            missionSystem.UpdateMission(diplomacyMission);

            Assert.AreEqual(
                0,
                game.GetSceneNodesByType<DiplomacyMission>().Count,
                "DiplomacyMission should be canceled on the tick after the uprising fires"
            );
        }

        [Test]
        public void UpdateMission_InciteBeforeDiplo_DiploCanceledImmediately()
        {
            // Incite completes first: uprising fires before diplo gets its turn this turn.
            // When UpdateMission runs for diplo, the ShouldAbort pre-tick guard catches it
            // immediately — diplo never executes.
            (
                GameRoot game,
                DiplomacyMission diplomacyMission,
                InciteUprisingMission inciteMission,
                MissionSystem missionSystem
            ) = BuildConcurrentMissionsScene();

            missionSystem.UpdateMission(inciteMission); // incite completes, uprising starts
            missionSystem.UpdateMission(diplomacyMission); // pre-tick guard fires, diplo canceled

            Assert.AreEqual(
                0,
                game.GetSceneNodesByType<DiplomacyMission>().Count,
                "DiplomacyMission should be canceled immediately when uprising fires before its turn"
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

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            MissionSystem missionSystem = new MissionSystem(game, new StubRNG(), movement, fog);

            missionSystem.InitiateMission(MissionType.Sabotage, officer, targetPlanet);

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

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            MissionSystem missionSystem = new MissionSystem(game, new StubRNG(), movement, fog);

            missionSystem.InitiateMission(MissionType.Sabotage, officer, targetPlanet);

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
        public void UpdateMission_DetectionRollFails_MissionContinues()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 10 } }
            );
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.Initiate(new StubRNG());

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
        public void UpdateMission_DetectionSucceedsHighCapture_CapturesParticipant()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            mission.KillOrCaptureProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 100 } }
            );
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
        public void UpdateMission_DetectionSucceedsLowCapture_KillsParticipant()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            mission.KillOrCaptureProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 0 } }
            );
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
        public void UpdateMission_DetectionOnOwnPlanet_NeverDetected()
        {
            (GameRoot game, Planet planet, Officer spy, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.IsFalse(spy.IsCaptured, "Missions on own planets should never be detected");
        }

        [Test]
        public void UpdateMission_DetectionWithNoDefender_StillAppliesKillOrCapture()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            game.DetachNode(defender);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            mission.KillOrCaptureProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 0 } }
            );
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            spy.SetParent(mission);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            List<GameResult> results = system.UpdateMission(mission);

            Assert.IsTrue(
                spy.IsKilled || spy.IsCaptured,
                "Detected spy should face kill-or-capture even without a defending officer"
            );
        }

        [Test]
        public void UpdateMission_DetectionWithDecoy_PreventsCapture()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            mission.DecoyProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            mission.KillOrCaptureProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 100 } }
            );
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.DecoyParticipants.Add(decoy);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.IsFalse(spy.IsCaptured, "Successful decoy should prevent detection");
        }

        [Test]
        public void UpdateMission_DetectionWithDecoySkill_UsesConfiguredSkill()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            // Decoy has Espionage=0 but Combat=80. DecoyParticipantSkill is set to Combat.
            // If the system incorrectly uses Espionage, the decoy fails and the spy is captured.
            Officer decoy = new Officer
            {
                InstanceID = "decoy",
                OwnerInstanceID = "empire",
                Skills = new Dictionary<MissionParticipantSkill, int>
                {
                    { MissionParticipantSkill.Espionage, 0 },
                    { MissionParticipantSkill.Combat, 80 },
                    { MissionParticipantSkill.Diplomacy, 0 },
                    { MissionParticipantSkill.Leadership, 0 },
                },
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            mission.DecoyParticipantSkill = MissionParticipantSkill.Combat;
            mission.DecoyProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 50, 100 } }
            );
            mission.KillOrCaptureProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 100 } }
            );
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(spy);
            mission.DecoyParticipants.Add(decoy);

            MissionSystem system = new MissionSystem(game, new FixedRNG(0.01), movement);

            system.UpdateMission(mission);

            Assert.IsFalse(
                spy.IsCaptured,
                "Decoy should use DecoyParticipantSkill (Combat=80) not always Espionage"
            );
        }

        [Test]
        public void UpdateMission_DetectionCapturesParticipant_CancelsMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender, MovementSystem movement) =
                BuildDetectionScene();

            Officer secondSpy = EntityFactory.CreateOfficer("o2", "empire");

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            mission.KillOrCaptureProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 100 } }
            );
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
            mission.FoilProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { 0, 100 } }
            );
            mission.KillOrCaptureProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 100 } }
            );
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
    }
}
