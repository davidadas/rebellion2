using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.Random;

/// <summary>Verifies mission execution and participant cleanup.</summary>
namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class MissionCommandsTests
    {
        [Test]
        public void ProcessTick_BetrayingOfficer_AbortsWithoutDetectorConfrontation()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true,
                canBetray: true,
                loyalty: 0
            );
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            Planet returnPlanet = AddMissionReturnPlanet(
                game,
                planet.GetParentOfType<PlanetSector>(),
                "return-planet",
                "empire",
                positionX: 100
            );
            officer.MissionReturnParentInstanceID = returnPlanet.InstanceID;
            officer.MissionReturnLocationInstanceID = returnPlanet.InstanceID;
            planet.OwnerInstanceID = "rebels";
            Officer defender = EntityFactory.CreateOfficer("defender", "rebels");
            defender.CurrentRank = OfficerRank.General;
            game.AttachNode(defender, planet);
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "detector",
                    OwnerInstanceID = "rebels",
                    DetectionRating = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            StubMission mission = CreateMission(game, planet, officer);
            mission.Initiate(0);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Foiled, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.Foiled, completed.CompletionReason);
            Assert.IsNull(completed.FoilingFactionInstanceID);
            Assert.IsFalse(officer.IsCaptured);
            Assert.IsFalse(results.OfType<OfficerCaptureStateResult>().Any());
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void ProcessTick_BetrayingDecoy_DoesNotResolveDetectorConfrontation()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            Planet returnPlanet = AddMissionReturnPlanet(
                game,
                planet.GetParentOfType<PlanetSector>(),
                "return-planet",
                "empire",
                positionX: 100
            );
            planet.OwnerInstanceID = "rebels";
            Officer defender = EntityFactory.CreateOfficer("defender", "rebels");
            defender.CurrentRank = OfficerRank.General;
            game.AttachNode(defender, planet);
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "detector",
                    OwnerInstanceID = "rebels",
                    DetectionRating = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            Officer decoy = EntityFactory.CreateOfficer(
                "decoy",
                "empire",
                canBetray: true,
                loyalty: 0
            );
            decoy.MissionReturnParentInstanceID = returnPlanet.InstanceID;
            decoy.MissionReturnLocationInstanceID = returnPlanet.InstanceID;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            StubMission mission = CreateMission(game, planet, officer);
            mission.AddDecoyParticipant(decoy);
            game.AttachNode(decoy, mission);
            mission.Initiate(0);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(decoy.IsCaptured);
            Assert.IsFalse(
                results
                    .OfType<OfficerCaptureStateResult>()
                    .Any(result => result.TargetOfficer == decoy)
            );
        }

        [Test]
        public void ProcessTick_DiplomacyBetrayal_TerminatesRepeatingMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true,
                canBetray: true,
                loyalty: 0
            );
            planet.AddVisitor("empire");
            Mission mission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            game.MoveNode(officer, mission);
            mission.Initiate(0);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.IsFalse(completed.CanContinue);
            Assert.IsFalse(results.OfType<PopularSupportShiftResult>().Any());
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void ProcessTick_ResearchBetrayal_TerminatesRepeatingMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true,
                canBetray: true,
                loyalty: 0
            );
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            Mission mission = MissionTestFactory.TryCreate(
                ResearchMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                discipline: ResearchDiscipline.FacilityDesign
            );
            game.AttachNode(mission, planet);
            game.MoveNode(officer, mission);
            mission.Initiate(0);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.IsFalse(completed.CanContinue);
            Assert.IsFalse(results.OfType<ResearchOrderedResult>().Any());
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void ProcessTick_CompletedWithoutReturnDestination_CapturesOfficerAndDetachesMission()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, planetSector);

            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
            };

            FogOfWarCommands fogOfWar = new FogOfWarCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                fogOfWar,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands missionSystem = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            game.AttachNode(officer, mission);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = missionSystem.ProcessMissionTick(game);
            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();

            Assert.IsNull(mission.GetParent());
            Assert.AreSame(planet, officer.GetParent());
            Assert.AreSame(officer, game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID));
            Assert.IsNull(officer.Movement);
            Assert.IsTrue(officer.IsCaptured);
            Assert.IsTrue(officer.CanEscape);
            Assert.IsNull(completed.ReturnDestination);
            Assert.IsTrue(
                results.Any(result =>
                    result is OfficerCaptureStateResult capture
                    && capture.TargetOfficer == officer
                    && capture.IsCaptured
                    && capture.Context == planet
                )
            );
        }

        [Test]
        public void ProcessTick_MissingOwnerFaction_DetachesMission()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, planetSector);

            StubMission mission = new StubMission(null, planet.InstanceID);
            game.AttachNode(mission, planet);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands missionSystem = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            Assert.DoesNotThrow(() => missionSystem.ProcessMissionTick(game));
            Assert.IsFalse(game.GetSceneNodesByType<StubMission>().Contains(mission));
        }

        [Test]
        public void ProcessTick_CompletedParticipantParentedToMission_ReturnsParticipantToPlanet()
        {
            // Regression: officer parented to the mission (as happens after Initiate moves them
            // there) caused IsMovable() to return false and RequestMove to throw on teardown.
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);

            // Simulate the officer having arrived at the mission mid-execution.
            game.MoveNode(officer, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.ProcessMissionTick(game);

            Assert.AreSame(planet, officer.GetParent());
            Assert.IsFalse(game.GetSceneNodesByType<StubMission>().Contains(mission));
        }

        [Test]
        public void ProcessTick_CompletedParticipantOnNeutralPlanet_ReturnsToNearestFriendlyPlanet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, sector);

            Planet homePlanet = new Planet
            {
                InstanceID = "home",
                TypeID = "home-planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(homePlanet, sector);

            Officer officer = new Officer { InstanceID = "o1", OwnerInstanceID = "empire" };
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.AddChild(officer);
            officer.SetParent(mission);

            MissionCommands missionSystem = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            Assert.DoesNotThrow(() => missionSystem.ProcessMissionTick(game));
            Assert.AreSame(homePlanet, officer.GetParent());
            Assert.AreNotSame(planet, officer.GetParent());
            Assert.IsFalse(officer.IsCaptured);
        }

        [Test]
        public void ProcessTick_FailedWithNearestFriendlyFleet_ReturnsParticipantsToFleet()
        {
            (
                GameRoot game,
                PlanetSector sector,
                Planet missionPlanet,
                StubMission mission,
                Officer officer,
                SpecialForces specialForces,
                MissionCommands system
            ) = BuildFailedMissionReturnScene();
            Planet fleetLocation = AddMissionReturnPlanet(
                game,
                sector,
                "fleet-location",
                ownerInstanceId: null,
                positionX: 25
            );
            (_, CapitalShip carrier) = AddMissionReturnFleet(
                game,
                fleetLocation,
                "return",
                "empire"
            );
            AddMissionReturnPlanet(game, sector, "far-planet", "empire", positionX: 100);
            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreSame(carrier, officer.GetParent());
            Assert.AreSame(carrier, specialForces.GetParent());
            Assert.AreSame(fleetLocation, completed.ReturnDestination);
            Assert.IsFalse(officer.IsCaptured);
        }

        [Test]
        public void ProcessTick_FailedWithNearestFriendlyPlanet_ReturnsParticipantsToPlanet()
        {
            (
                GameRoot game,
                PlanetSector sector,
                Planet _,
                StubMission mission,
                Officer officer,
                SpecialForces specialForces,
                MissionCommands system
            ) = BuildFailedMissionReturnScene();
            Planet destination = AddMissionReturnPlanet(
                game,
                sector,
                "nearest-planet",
                "empire",
                positionX: 25
            );
            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreSame(destination, officer.GetParent());
            Assert.AreSame(destination, specialForces.GetParent());
            Assert.AreSame(destination, completed.ReturnDestination);
            Assert.IsFalse(officer.IsCaptured);
        }

        [Test]
        public void ProcessTick_FailedWithBlockadedNearestPlanet_ReturnsParticipantsToSafePlanet()
        {
            (
                GameRoot game,
                PlanetSector sector,
                Planet _,
                StubMission mission,
                Officer officer,
                SpecialForces specialForces,
                MissionCommands system
            ) = BuildFailedMissionReturnScene();
            Planet blockadedPlanet = AddMissionReturnPlanet(
                game,
                sector,
                "blockaded-planet",
                "empire",
                positionX: 25
            );
            AddMissionReturnFleet(game, blockadedPlanet, "blockading", "rebels");
            Planet safePlanet = AddMissionReturnPlanet(
                game,
                sector,
                "safe-planet",
                "empire",
                positionX: 100
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(blockadedPlanet.IsBlockaded());
            Assert.AreEqual(
                MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreSame(safePlanet, officer.GetParent());
            Assert.AreSame(safePlanet, specialForces.GetParent());
            Assert.IsFalse(officer.IsCaptured);
        }

        [Test]
        public void ProcessTick_FailedWithBlockadedRecordedPlanet_ReturnsParticipantsToSafePlanet()
        {
            (
                GameRoot game,
                PlanetSector sector,
                Planet _,
                StubMission mission,
                Officer officer,
                SpecialForces specialForces,
                MissionCommands system
            ) = BuildFailedMissionReturnScene();
            Planet blockadedPlanet = AddMissionReturnPlanet(
                game,
                sector,
                "recorded-planet",
                "empire",
                positionX: 25
            );
            AddMissionReturnFleet(game, blockadedPlanet, "blockading-recorded", "rebels");
            Planet safePlanet = AddMissionReturnPlanet(
                game,
                sector,
                "safe-planet",
                "empire",
                positionX: 100
            );
            officer.MissionReturnParentInstanceID = blockadedPlanet.InstanceID;
            officer.MissionReturnLocationInstanceID = blockadedPlanet.InstanceID;
            specialForces.MissionReturnParentInstanceID = blockadedPlanet.InstanceID;
            specialForces.MissionReturnLocationInstanceID = blockadedPlanet.InstanceID;

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(blockadedPlanet.IsBlockaded());
            Assert.AreEqual(
                MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreSame(safePlanet, officer.GetParent());
            Assert.AreSame(safePlanet, specialForces.GetParent());
            Assert.IsFalse(officer.IsCaptured);
        }

        [Test]
        public void ProcessTick_FailedWithNoSafeDestination_CapturesOfficerAndDestroysSpecialForces()
        {
            (
                GameRoot game,
                PlanetSector sector,
                Planet missionPlanet,
                StubMission mission,
                Officer officer,
                SpecialForces specialForces,
                MissionCommands system
            ) = BuildFailedMissionReturnScene();
            Planet blockadedPlanet = AddMissionReturnPlanet(
                game,
                sector,
                "blockaded-planet",
                "empire",
                positionX: 25
            );
            AddMissionReturnFleet(game, blockadedPlanet, "blockading", "rebels");

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(blockadedPlanet.IsBlockaded());
            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual("rebels", officer.CaptorInstanceID);
            Assert.AreSame(missionPlanet, officer.GetParent());
            Assert.IsNull(
                game.GetSceneNodeByInstanceID<SpecialForces>(specialForces.InstanceID, true)
            );
            Assert.IsTrue(
                results
                    .OfType<GameObjectDestroyedResult>()
                    .Any(result => result.DestroyedObject == specialForces)
            );
        }

        [Test]
        public void ProcessTick_OnCompletion_DetachesMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.ProcessMissionTick(game);

            Assert.IsNull(
                mission.GetParent(),
                "Mission should be detached from scene graph after completion"
            );
        }

        [Test]
        public void ProcessTick_DiploBeforeIncite_DiploAbortsOnNextLifecycleStep()
        {
            (
                GameRoot game,
                Mission diplomacyMission,
                Mission inciteMission,
                MissionCommands missionSystem
            ) = BuildConcurrentMissionsScene();

            Planet planet = inciteMission.GetParentOfType<Planet>();
            IMissionParticipant participant = inciteMission.GetMainParticipants().Single();
            int leadershipBefore = participant.GetEffectiveRating(SkillRating.Leadership);
            List<GameResult> results = missionSystem.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Last();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.IsTrue(results.OfType<PlanetUprisingStartedResult>().Any());
            Assert.AreEqual(planet, diplomacyMission.GetParent());
            Assert.IsTrue(planet.IsInUprising);
            Assert.AreEqual(
                leadershipBefore,
                participant.GetEffectiveRating(SkillRating.Leadership)
            );

            List<GameResult> diplomacyResults = missionSystem.ProcessMissionTick(game);

            Assert.AreEqual(
                MissionCompletionReason.Failure,
                diplomacyResults.OfType<MissionCompletedResult>().Single().CompletionReason
            );
            Assert.IsNull(diplomacyMission.GetParent());
        }

        [Test]
        public void ProcessTick_InciteBeforeDiplo_DiploAbortsInSameTick()
        {
            (
                GameRoot game,
                Mission diplomacyMission,
                Mission inciteMission,
                MissionCommands missionSystem
            ) = BuildConcurrentMissionsScene(inciteFirst: true);

            List<GameResult> results = missionSystem.ProcessMissionTick(game);

            Assert.IsTrue(results.OfType<PlanetUprisingStartedResult>().Any());
            MissionCompletedResult diplomacyResult = results
                .OfType<MissionCompletedResult>()
                .Single(result => result.MissionInstanceID == diplomacyMission.InstanceID);
            Assert.AreEqual(MissionCompletionReason.Failure, diplomacyResult.CompletionReason);
            Assert.IsNull(diplomacyMission.GetParent());
        }

        [Test]
        public void ProcessTick_InciteRemovesOpposingControlWithoutOwnTroops_SucceedsAndImprovesAgent()
        {
            (
                GameRoot game,
                Mission diplomacyMission,
                Mission inciteMission,
                MissionCommands missionSystem
            ) = BuildConcurrentMissionsScene(ownerSupport: 60, hasGarrison: false);
            Officer participant = (Officer)inciteMission.GetMainParticipants().Single();
            int leadershipBefore = participant.GetBaseRating(SkillRating.Leadership);

            List<GameResult> results = missionSystem.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Last();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
            Assert.IsNull(game.GetSceneNodeByInstanceID<Planet>("rebels_planet").OwnerInstanceID);
            Assert.AreEqual(
                leadershipBefore + 1,
                participant.GetBaseRating(SkillRating.Leadership)
            );
        }

        [Test]
        public void ProcessTick_DiplomacyCompletionFromFleet_ParticipantRemainsAtTargetPlanet()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(faction);
            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector",
                SectorType = PlanetSectorType.OuterRim,
            };
            game.AttachNode(planetSector, game.Galaxy);
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet target = new Planet
            {
                InstanceID = "target",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { faction.InstanceID, 99 } },
            };
            target.AddVisitor(faction.InstanceID);
            game.AttachNode(origin, planetSector);
            game.AttachNode(target, planetSector);
            Fleet fleet = EntityFactory.CreateFleet("fleet", faction.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, origin);
            game.AttachNode(ship, fleet);
            Officer officer = EntityFactory.CreateOfficer("diplomat", faction.InstanceID);
            game.AttachNode(officer, ship);
            game.Config.ProbabilityTables.Mission.Diplomacy = new Dictionary<int, int>
            {
                { -200, 100 },
            };
            game.Config.SupportShift.DiplomacyOwnedPlanetSupportBase = 1;
            game.Config.SupportShift.DiplomacyOwnedPlanetSupportRange = 0;
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands missions = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0),
                movement
            );

            Assert.IsTrue(
                missions.InitiateMission(
                    CreateContext(DiplomacyMission.MissionTypeID, officer, target)
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            officer.Movement = null;
            mission.SetExecutionTick(0);

            List<GameResult> results = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
            Assert.AreSame(target, completed.ReturnDestination);
            Assert.AreSame(target, officer.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void ProcessTick_AnyParticipantInTransit_DoesNotProgressOrExecute()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
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
            mission.AddChild(officer);
            mission.AddChild(traveler);
            officer.SetParent(mission);
            traveler.SetParent(mission);
            mission.Initiate(0);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.AreEqual(0, mission.CurrentProgress);
            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, game.GetSceneNodesByType<StubMission>().Count);
        }

        [Test]
        public void ProcessTick_AnyParticipantInTransit_NoDetectionOrCapture()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            Officer traveler = new Officer
            {
                InstanceID = "o2",
                OwnerInstanceID = "empire",
                Movement = new MovementState { TransitTicks = 10, TicksElapsed = 0 },
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            game.AttachNode(traveler, mission);
            mission.Initiate(0);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(spy.IsCaptured);
            Assert.IsFalse(traveler.IsCaptured);
            Assert.IsFalse(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(r => r.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void ProcessTick_MainParticipantRemoved_ReturnsFailedMissionCompletedResult()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            mission.Initiate(0);
            mission.RemoveChild(officer);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.Failure, completed.CompletionReason);
            Assert.IsFalse(completed.CanContinue);
            Assert.AreEqual(0, game.GetSceneNodesByType<StubMission>().Count);
        }

        [Test]
        public void ProcessTick_DetectionRollFails_MissionContinues()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 10 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.Initiate(1);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.99),
                movement
            );

            system.ProcessMissionTick(game);

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
        public void ProcessTick_DiplomacyWithHostileDetector_CanBeFoiled()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            planet.OwnerInstanceID = null;
            planet.PopularSupport["empire"] = 50;
            planet.AddVisitor("empire");

            Mission mission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { spy },
                new List<IMissionParticipant>()
            );
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.Initiate(0);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results
                .OfType<MissionCompletedResult>()
                .Single(result => result.Outcome == MissionOutcome.Foiled);
            Assert.AreEqual(defender.OwnerInstanceID, completed.FoilingFactionInstanceID);
        }

        [Test]
        public void ProcessTick_DiplomacyWithoutHostileDetector_DoesNotInjureParticipant()
        {
            (GameRoot game, Planet planet, Officer diplomat, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            planet.OwnerInstanceID = null;
            planet.PopularSupport["empire"] = 50;
            planet.AddVisitor("empire");
            foreach (Regiment regiment in planet.GetChildren<Regiment>().ToList())
                game.DeleteNode(regiment);
            foreach (Fleet fleet in planet.GetChildren<Fleet>().ToList())
                game.DeleteNode(fleet);

            Mission mission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { diplomat },
                new List<IMissionParticipant>()
            );
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            game.AttachNode(mission, planet);
            game.MoveNode(diplomat, mission);
            mission.Initiate(0);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
            Assert.IsFalse(diplomat.IsCaptured);
            Assert.Zero(diplomat.InjuryPoints);
            Assert.IsFalse(results.OfType<OfficerInjuredResult>().Any());
        }

        [Test]
        public void ProcessTick_RecruitmentOnFriendlyPlanetWithHostileDetector_CanBeFoiled()
        {
            (
                GameRoot game,
                Planet planet,
                Officer recruiter,
                Officer _,
                MovementCommands movement
            ) = BuildDetectionScene();
            planet.OwnerInstanceID = "empire";
            recruiter.IsMain = true;
            Officer candidate = EntityFactory.CreateOfficer("candidate", "rebels");
            candidate.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(candidate);

            Mission mission = MissionTestFactory.TryCreate(
                RecruitmentMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { recruiter }
            );
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(recruiter, mission);
            mission.Initiate(2);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void ProcessTick_RecruitmentOnFriendlyPlanetWithSuccessfulDecoy_Continues()
        {
            (
                GameRoot game,
                Planet planet,
                Officer recruiter,
                Officer _,
                MovementCommands movement
            ) = BuildDetectionScene();
            planet.OwnerInstanceID = "empire";
            recruiter.IsMain = true;
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            Officer candidate = EntityFactory.CreateOfficer("candidate", "rebels");
            candidate.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(candidate);

            Mission mission = MissionTestFactory.TryCreate(
                RecruitmentMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { recruiter },
                new List<IMissionParticipant> { decoy }
            );
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(recruiter, mission);
            game.AttachNode(decoy, mission);
            mission.Initiate(2);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
            Assert.IsFalse(recruiter.IsCaptured);
        }

        [Test]
        public void ProcessTick_FoilScore_UsesEspionageInsteadOfMissionRating()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            spy.SetBaseRating(SkillRating.Diplomacy, 200);
            spy.SetBaseRating(SkillRating.Espionage, 0);
            defender.SetBaseRating(SkillRating.Espionage, 10);
            planet.GetChildren<Regiment>().Single().DetectionRating = 10;

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.Config.ProbabilityTables.Mission.FoilDefenderScalingPercent = 35;
            game.Config.ProbabilityTables.Mission.FoilFlatScoreAdjustment = -1;
            SetFoilTable(game, new Dictionary<int, int> { { -100, 100 }, { 50, 0 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(spy.IsCaptured);
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void ProcessTick_DetectorRatingAndRank_SelectMatchingCommander()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer general,
                MovementCommands movement
            ) = BuildDetectionScene();
            general.SetBaseRating(SkillRating.Espionage, 40);
            Officer admiral = EntityFactory.CreateOfficer("admiral", "rebels");
            admiral.CurrentRank = OfficerRank.Admiral;
            admiral.SetBaseRating(SkillRating.Espionage, 100);
            game.AttachNode(admiral, planet);
            Regiment detector = planet.GetChildren<Regiment>().Single();
            detector.DefenseRating = 999;
            detector.DetectionRating = 17;

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { -10000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            Regiment selectedDetector = planet.GetChildren<Regiment>().Single();
            Assert.AreSame(general, mission.FindDetectorCommander(selectedDetector));
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.0),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void ProcessTick_DirectDetectorWithoutFleet_CanFoilMission()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            foreach (Fleet fleet in planet.GetChildren<Fleet>().ToList())
                game.DetachNode(fleet);
            spy.SetBaseRating(SkillRating.Espionage, 0);
            defender.SetBaseRating(SkillRating.Espionage, 0);
            SetFoilTable(game, new Dictionary<int, int> { { -10000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -10000, 0 } });
            DisableCaptureEvasionInjury(game);
            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.0),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(spy.IsCaptured);
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void GetDecoyProbability_DeparturePlanet_UsesOriginCommander()
        {
            (
                GameRoot game,
                Planet target,
                Officer spy,
                Officer targetGeneral,
                MovementCommands movement
            ) = BuildDetectionScene();
            targetGeneral.SetBaseRating(SkillRating.Espionage, 100);
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(origin, target.GetParent());
            Officer originGeneral = EntityFactory.CreateOfficer("origin-general", "rebels");
            originGeneral.CurrentRank = OfficerRank.General;
            originGeneral.SetBaseRating(SkillRating.Espionage, 0);
            game.AttachNode(originGeneral, origin);
            Regiment detector = CreateCompletedRegiment("origin-detector", "rebels");
            detector.DetectionRating = 0;
            game.AttachNode(detector, origin);
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            decoy.SetBaseRating(SkillRating.Espionage, 100);
            StubMission mission = new StubMission("empire", target.InstanceID);
            game.AttachNode(mission, target);
            game.Config.ProbabilityTables.Mission.DecoyDefenderScalingPercent = 100;
            SetDecoyTable(game, new Dictionary<int, int> { { 0, 10 }, { 100, 99 } });

            double probability = new MissionQueries(game).GetDecoyProbability(
                mission,
                decoy,
                detector,
                origin
            );

            Assert.AreEqual(99, probability);
        }

        [Test]
        public void ProcessTick_TwoDetectors_FoilsWhenOnlySecondSucceeds()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            spy.SetBaseRating(SkillRating.Espionage, 0);
            defender.SetBaseRating(SkillRating.Espionage, 0);
            planet.GetChildren<Regiment>().Single().DetectionRating = 0;
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "r2",
                    OwnerInstanceID = "rebels",
                    DetectionRating = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.Config.ProbabilityTables.Mission.FoilFlatScoreAdjustment = -1;
            SetFoilTable(game, new Dictionary<int, int> { { -100, 100 }, { 1, 0 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 100 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
            Assert.IsFalse(spy.IsCaptured);
        }

        [Test]
        public void ProcessTick_CompletedBuilding_DoesNotDetectMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            game.DeleteNode(planet.GetChildren<Regiment>().Single());
            game.DeleteNode(planet.GetChildren<Fleet>().Single());
            planet.EnergyCapacity = 1;
            game.AttachNode(
                new Building
                {
                    InstanceID = "building",
                    OwnerInstanceID = "rebels",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_DetectionAlreadyResolved_DoesNotRollAgain()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.IsFalse(spy.IsCaptured);
            Assert.AreEqual(2, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_CompletedUnitOnIncompleteCapitalShip_DoesNotDetectMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            game.DeleteNode(planet.GetChildren<Regiment>().Single());
            game.DeleteNode(planet.GetChildren<Fleet>().Single());
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(capitalShip, fleet);
            game.AttachNode(starfighter, capitalShip);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_FleetDetector_UsesFleetDecoyTable()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            game.DeleteNode(planet.GetChildren<Regiment>().Single());
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(capitalShip, fleet);
            game.AttachNode(starfighter, capitalShip);
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            decoy.SetBaseRating(SkillRating.Espionage, 200);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = new Dictionary<int, int>
            {
                { -1000, 0 },
            };
            game.Config.ProbabilityTables.Mission.FleetDecoy = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.AddDecoyParticipant(decoy);
            game.AttachNode(decoy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_UnblockedFleetDetector_FoilsMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            game.DeleteNode(planet.GetChildren<Regiment>().Single());
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(capitalShip, fleet);
            game.AttachNode(starfighter, capitalShip);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void ProcessTick_InTransitFleetDetector_DoesNotFoilMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            game.DeleteNode(planet.GetChildren<Regiment>().Single());
            game.DeleteNode(planet.GetChildren<Fleet>().Single());
            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                OwnerInstanceID = "rebels",
                Movement = new MovementState { TransitTicks = 10 },
            };
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(capitalShip, fleet);
            game.AttachNode(starfighter, capitalShip);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_FriendlyBuilding_BlocksFleetDetection()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            game.DeleteNode(planet.GetChildren<Regiment>().Single());
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            planet.OwnerInstanceID = "empire";
            planet.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "detection-blocker",
                OwnerInstanceID = "empire",
                IsDetectionBlocker = true,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(capitalShip, fleet);
            game.AttachNode(starfighter, capitalShip);
            game.AttachNode(building, planet);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_FriendlyBuilding_BlocksApproachPlanetaryDetection()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            planet.OwnerInstanceID = "empire";
            planet.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "detection-blocker",
                OwnerInstanceID = "empire",
                IsDetectionBlocker = true,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_MainSpecialForces_ReducesFoilScore()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            spy.SetBaseRating(SkillRating.Espionage, 0);
            defender.SetBaseRating(SkillRating.Espionage, 0);
            planet.GetChildren<Regiment>().Single().DetectionRating = 0;

            SpecialForces support = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            support.SetBaseRating(SkillRating.Espionage, 0);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.Config.ProbabilityTables.Mission.FoilDefenderScalingPercent = 35;
            game.Config.ProbabilityTables.Mission.FoilFlatScoreAdjustment = -1;
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 }, { 1, 0 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            game.AttachNode(support, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(spy.IsCaptured);
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void ProcessTick_EvasionFails_CapturesParticipant()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

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
        public void ProcessTick_CapturedByOrbitalFleetOverOwnPlanet_RecordsCapturingUnit()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                CapitalShip capturingUnit,
                MovementCommands movement
            ) = BuildOrbitalDetectionScene(planetOwnerId: "empire");

            List<GameResult> results = RunOrbitalCaptureMission(game, planet, spy, movement);

            Assert.IsTrue(spy.IsCaptured);
            Assert.AreEqual(
                "rebels",
                spy.CaptorInstanceID,
                "Captor should be the hostile detector's faction, not the planet owner"
            );
            Assert.AreSame(
                capturingUnit,
                results.OfType<OfficerCaptureStateResult>().Single().CapturingUnit
            );
        }

        [Test]
        public void ProcessTick_CapturedByOrbitalFleetOverNeutralPlanet_RecordsCapturingUnit()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                CapitalShip capturingUnit,
                MovementCommands movement
            ) = BuildOrbitalDetectionScene(planetOwnerId: null);

            List<GameResult> results = RunOrbitalCaptureMission(game, planet, spy, movement);

            Assert.IsTrue(spy.IsCaptured);
            Assert.AreEqual("rebels", spy.CaptorInstanceID);
            Assert.AreSame(
                capturingUnit,
                results.OfType<OfficerCaptureStateResult>().Single().CapturingUnit
            );
        }

        [Test]
        public void ProcessTick_CapturedByGarrisonOnEnemyPlanet_StaysOnCaptorPlanet()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.IsTrue(spy.IsCaptured);
            Assert.AreEqual("rebels", spy.CaptorInstanceID);
            Assert.AreSame(
                planet,
                spy.GetParent(),
                "An officer caught by a planet's own garrison stays on that captor world"
            );
            Assert.IsNull(spy.GetParentOfType<CapitalShip>());
        }

        [Test]
        public void ProcessTick_EspionageDetected_AppliesFoiledParticipantConsequences()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            planet.VisitingFactionIDs.Add("empire");
            Mission mission = MissionTestFactory.TryCreate(
                EspionageMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { spy },
                new List<IMissionParticipant>()
            );
            Assert.IsNotNull(mission);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(spy.IsCaptured);
            Assert.IsFalse(spy.IsKilled);
            Assert.IsTrue(results.Any(r => r is OfficerCaptureStateResult));
            Assert.IsFalse(results.Any(r => r is OfficerKilledResult));
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void ProcessTick_NoFreeMainParticipant_SkipsDetection()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();

            spy.IsCaptured = true;
            spy.CaptorInstanceID = "rebels";

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.Initiate(0);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.Any(result => result is OfficerCaptureStateResult));
            Assert.IsFalse(results.Any(result => result is OfficerKilledResult));
            Assert.IsFalse(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void ProcessTick_EvasionFails_MovesCaptiveToMissionPlanet()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);

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
        public void ProcessTick_EvasionSucceeds_ReturnsParticipant()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 100 } });
            DisableCaptureEvasionInjury(game);
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(spy.IsKilled);
            Assert.IsFalse(spy.IsCaptured);
            Assert.AreEqual("empire-home", spy.GetParent()?.GetInstanceID());
            Assert.IsFalse(results.Any(result => result is OfficerKilledResult));
            Assert.IsFalse(results.Any(result => result is OfficerCaptureStateResult));
        }

        [Test]
        public void ProcessTick_ParticipantInjuredAfterInitiation_DoesNotAbortMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            mission.Initiate(0);
            mission.SetExecutionTick(5);

            officer.InjuryPoints = 1;

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.AreEqual(
                1,
                game.GetSceneNodesByType<StubMission>().Count,
                "Mission should not abort when participant membership is unchanged"
            );
        }

        [Test]
        public void ProcessTick_DetectionWithoutEvasionTable_UsesConfiguredDefault()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            game.Config.ProbabilityTables.Mission.DefaultEvasionProbability = 0;
            DisableCaptureEvasionInjury(game);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int>());
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(spy.IsCaptured, "Officer should use the default evasion probability");
            Assert.IsTrue(
                results.Any(r => r is OfficerCaptureStateResult),
                "Should produce OfficerCaptureStateResult"
            );
        }

        [Test]
        public void ProcessTick_DetectionOnOwnPlanet_NeverDetected()
        {
            (GameRoot game, Planet planet, Officer spy, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.IsFalse(spy.IsCaptured, "Missions on own planets should never be detected");
        }

        [Test]
        public void ProcessTick_DetectorWithoutCommander_CanFoil()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            game.DetachNode(defender);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(spy.IsKilled);
            Assert.IsTrue(spy.IsCaptured);
            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
            Assert.AreEqual(0, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_DetectionWithDecoy_PreventsCapture()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            decoy.SetBaseRating(SkillRating.Espionage, 200);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });

            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.AddDecoyParticipant(decoy);
            game.AttachNode(decoy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.IsFalse(spy.IsCaptured, "Successful decoy should prevent capture");
        }

        [Test]
        public void ProcessTick_HostileForceUserDetectsMainParticipant_FoilsMission()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            spy.IsForceSensitive = true;
            spy.IsForceEligible = true;
            spy.ForceValue = 50;
            defender.IsForceSensitive = true;
            defender.IsForceEligible = true;
            defender.ForceValue = 80;
            game.MoveNode(
                defender,
                planet
                    .GetChildren<Fleet>()
                    .Single(fleet => fleet.InstanceID == "detection-fleet")
                    .GetChildren<CapitalShip>()
                    .Single()
            );
            game.Config.Jedi.EncounterProbabilityOffset = -100;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results
                .OfType<MissionCompletedResult>()
                .Single(result => result.Outcome == MissionOutcome.Foiled);
            Assert.AreEqual(defender.OwnerInstanceID, completed.FoilingFactionInstanceID);
            Assert.IsTrue(spy.IsCaptured);
            Assert.AreEqual(
                mission.InstanceID,
                results.OfType<OfficerCaptureStateResult>().Single().MissionInstanceID
            );
        }

        [Test]
        public void ProcessTick_ForceDetectionWithoutDetector_FoilsWithoutCapture()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            spy.IsForceSensitive = true;
            spy.IsForceEligible = true;
            spy.ForceValue = 50;
            defender.IsForceSensitive = true;
            defender.IsForceEligible = true;
            defender.ForceValue = 80;
            foreach (Regiment detector in planet.GetChildren<Regiment>().ToList())
                game.DetachNode(detector);
            foreach (Fleet fleet in planet.GetChildren<Fleet>().ToList())
                game.DetachNode(fleet);
            game.Config.Jedi.EncounterProbabilityOffset = -100;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.DetectionResolved = true;
            mission.SetExecutionTick(0);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
            Assert.IsFalse(spy.IsCaptured);
            Assert.IsFalse(results.OfType<OfficerCaptureStateResult>().Any());
        }

        [Test]
        public void ProcessTick_ArrivalWithPlanetaryForceUser_DoesNotTriggerForceEncounter()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            spy.IsForceSensitive = true;
            spy.IsForceEligible = true;
            spy.ForceValue = 100;
            defender.IsForceSensitive = true;
            defender.IsForceEligible = true;
            defender.ForceValue = 100;
            game.Config.Jedi.EncounterProbabilityOffset = 100;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_ArrivalWithDetectionBlocker_DoesNotTriggerFleetForceEncounter()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            spy.IsForceSensitive = true;
            spy.IsForceEligible = true;
            spy.ForceValue = 100;
            defender.IsForceSensitive = true;
            defender.IsForceEligible = true;
            defender.ForceValue = 100;
            game.MoveNode(
                defender,
                planet
                    .GetChildren<Fleet>()
                    .Single(fleet => fleet.InstanceID == "detection-fleet")
                    .GetChildren<CapitalShip>()
                    .Single()
            );
            planet.OwnerInstanceID = "empire";
            planet.EnergyCapacity = 1;
            game.AttachNode(
                new Building
                {
                    InstanceID = "detection-blocker",
                    OwnerInstanceID = "empire",
                    IsDetectionBlocker = true,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.Config.Jedi.EncounterProbabilityOffset = 100;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_ZeroForceRankParticipant_DoesNotTriggerForceEncounter()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            spy.IsForceSensitive = true;
            spy.IsForceEligible = true;
            spy.ForceValue = 0;
            defender.IsForceSensitive = true;
            defender.IsForceEligible = true;
            defender.ForceValue = 80;
            game.MoveNode(
                defender,
                planet
                    .GetChildren<Fleet>()
                    .Single(fleet => fleet.InstanceID == "detection-fleet")
                    .GetChildren<CapitalShip>()
                    .Single()
            );
            game.Config.Jedi.EncounterProbabilityOffset = 100;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_ForceRankDefenderBelowMinimum_DoesNotTriggerForceEncounter()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            spy.IsForceSensitive = true;
            spy.IsForceEligible = true;
            spy.ForceValue = 1;
            defender.IsForceSensitive = true;
            defender.IsForceEligible = true;
            defender.ForceValue = 1;
            game.MoveNode(
                defender,
                planet
                    .GetChildren<Fleet>()
                    .Single(fleet => fleet.InstanceID == "detection-fleet")
                    .GetChildren<CapitalShip>()
                    .Single()
            );
            game.Config.Jedi.EncounterProbabilityOffset = 100;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_ForceDefenderAssignedToAnotherMission_DoesNotTriggerForceEncounter()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();
            spy.ForceValue = 100;
            defender.ForceValue = 100;
            game.Config.Jedi.EncounterProbabilityOffset = 100;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });

            StubMission defendingMission = new StubMission("rebels", planet.InstanceID);
            defendingMission.SetExecutionTick(5);
            game.AttachNode(defendingMission, planet);
            game.MoveNode(defender, defendingMission);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(results.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_DecoyCheck_AlwaysUsesEspionage()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            Officer decoy = new Officer
            {
                InstanceID = "decoy",
                OwnerInstanceID = "empire",
                Ratings = new Dictionary<SkillRating, int>
                {
                    { SkillRating.Espionage, 0 },
                    { SkillRating.Combat, 200 },
                    { SkillRating.Diplomacy, 0 },
                    { SkillRating.Leadership, 0 },
                },
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });

            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.AddDecoyParticipant(decoy);
            game.AttachNode(decoy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.IsTrue(spy.IsCaptured);
        }

        [Test]
        public void ProcessTick_HighDetectorRating_DecoyFails()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            // A high detector rating makes decoy probability very low.
            for (int i = 0; i < 5; i++)
            {
                Regiment regiment = new Regiment
                {
                    InstanceID = $"extra_r{i}",
                    OwnerInstanceID = "rebels",
                    DetectionRating = 50,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                game.AttachNode(regiment, planet);
            }

            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });

            SetDecoyTable(game, new Dictionary<int, int> { { -200, 0 }, { 200, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.AddDecoyParticipant(decoy);
            game.AttachNode(decoy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.IsTrue(
                spy.IsCaptured,
                "A high detector rating should make the decoy fail, allowing capture"
            );
        }

        [Test]
        public void ProcessTick_DetectionPicksOneRandomDecoy_NotAll()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            // Two decoys: one with Espionage=0 (will fail), one with Espionage=200 (would pass).
            // FixedRNG NextInt returns min (0), so first decoy is always picked.
            // If all decoys were checked, the second would save the spy.
            Officer weakDecoy = EntityFactory.CreateOfficer("decoy_weak", "empire");
            weakDecoy.SetBaseRating(SkillRating.Espionage, 0);

            Officer strongDecoy = EntityFactory.CreateOfficer("decoy_strong", "empire");
            strongDecoy.SetBaseRating(SkillRating.Espionage, 200);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });

            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.AddDecoyParticipant(weakDecoy);
            mission.AddDecoyParticipant(strongDecoy);
            game.AttachNode(weakDecoy, mission);
            game.AttachNode(strongDecoy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.IsTrue(
                spy.IsCaptured,
                "Only one random decoy should be rolled, not all — weak decoy picked first should fail"
            );
        }

        [Test]
        public void ProcessTick_FailedDecoyEscapes_ReturnsSeparatelyAndCannotBeReused()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            Officer escapedDecoy = EntityFactory.CreateOfficer("decoy_escaped", "empire");
            escapedDecoy.SetBaseRating(SkillRating.Espionage, 0);
            escapedDecoy.SetBaseRating(SkillRating.Combat, 200);
            escapedDecoy.MissionReturnLocationInstanceID = "empire-home";
            Officer remainingDecoy = EntityFactory.CreateOfficer("decoy_remaining", "empire");
            remainingDecoy.SetBaseRating(SkillRating.Espionage, 200);
            remainingDecoy.MissionReturnLocationInstanceID = "empire-home";
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "second_detector",
                    OwnerInstanceID = "rebels",
                    DetectionRating = 50,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.AddDecoyParticipant(escapedDecoy);
            mission.AddDecoyParticipant(remainingDecoy);
            game.AttachNode(escapedDecoy, mission);
            game.AttachNode(remainingDecoy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.AreEqual("empire-home", escapedDecoy.GetParent()?.InstanceID);
            Assert.IsNotNull(escapedDecoy.Movement);
            Assert.AreSame(mission, remainingDecoy.GetParent());
            Assert.AreEqual(1, mission.CurrentProgress);
        }

        [Test]
        public void ProcessTick_DetectorFoilsAfterSuccessfulDecoy_ResolvesDecoyConfrontation()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            Officer successfulDecoy = EntityFactory.CreateOfficer("successful-decoy", "empire");
            successfulDecoy.SetBaseRating(SkillRating.Espionage, 200);
            Officer failedDecoy = EntityFactory.CreateOfficer("failed-decoy", "empire");
            failedDecoy.SetBaseRating(SkillRating.Espionage, 0);
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "second-detector",
                    OwnerInstanceID = "rebels",
                    DetectionRating = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            mission.SetExecutionTick(5);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.AddDecoyParticipant(successfulDecoy);
            mission.AddDecoyParticipant(failedDecoy);
            game.AttachNode(successfulDecoy, mission);
            game.AttachNode(failedDecoy, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new SequenceRNG(intValues: new[] { 0, 1, 0, 0 }),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(failedDecoy.IsCaptured);
            Assert.IsTrue(successfulDecoy.IsCaptured);
            Assert.AreEqual(
                2,
                results
                    .OfType<OfficerCaptureStateResult>()
                    .Count(result =>
                        result.TargetOfficer == successfulDecoy
                        || result.TargetOfficer == failedDecoy
                    )
            );
        }

        [Test]
        public void ProcessTick_DetectionCapturesParticipant_CancelsMission()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            Officer secondSpy = EntityFactory.CreateOfficer("o2", "empire");

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            game.AttachNode(secondSpy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

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
        public void ProcessTick_DetectionWithSpecialForces_DestroysUnit()
        {
            (
                GameRoot game,
                Planet planet,
                Officer spy,
                Officer defender,
                MovementCommands movement
            ) = BuildDetectionScene();

            SpecialForces sf = new SpecialForces { InstanceID = "sf1", OwnerInstanceID = "empire" };
            sf.MissionReturnParentInstanceID = spy.MissionReturnParentInstanceID;
            sf.MissionReturnLocationInstanceID = spy.MissionReturnLocationInstanceID;

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            game.AttachNode(mission, planet);
            mission.AddChild(sf);
            sf.SetParent(mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsNull(sf.GetParent(), "SpecialForces should be detached when detected");
            Assert.IsTrue(
                results.Any(r => r is GameObjectDestroyedResult),
                "Should produce GameObjectDestroyedResult for destroyed SpecialForces"
            );
        }

        [Test]
        public void ProcessTick_SpecialForcesEvadesDetector_IsNotDestroyed()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                MissionReturnParentInstanceID = spy.MissionReturnParentInstanceID,
                MissionReturnLocationInstanceID = spy.MissionReturnLocationInstanceID,
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.AttachNode(specialForces, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.AreSame(
                specialForces,
                game.GetSceneNodeByInstanceID<SpecialForces>(specialForces.InstanceID)
            );
            Assert.IsFalse(
                results
                    .OfType<GameObjectDestroyedResult>()
                    .Any(result => result.DestroyedObject == specialForces)
            );
        }

        [Test]
        public void ProcessTick_OfficerEvadesDetector_EscapesWithoutInjury()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            spy.IsMain = true;
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = 100;
            game.Config.DuelResolution.MinimumInjuryChance = 100;
            game.Config.DuelResolution.InjuryBase = 1;
            game.Config.DuelResolution.InjurySecondaryRollMaximum = 0;
            game.Config.Recovery.MaxInjuryPoints = 100;

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(spy.IsCaptured);
            Assert.AreEqual(0, spy.InjuryPoints);
            Assert.IsEmpty(results.OfType<OfficerInjuredResult>());
        }

        [Test]
        public void ProcessTick_MinorOfficerEvadesDetector_DoesNotRollPostInjuryDeath()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            spy.IsMain = false;
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = 100;
            game.Config.DuelResolution.MinimumInjuryChance = 100;
            game.Config.DuelResolution.InjuryBase = 1;
            game.Config.DuelResolution.InjurySecondaryRollMaximum = 0;
            game.Config.Recovery.MaxInjuryPoints = 100;
            game.Config.Assassination.KillProbability = 100;

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 100 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(spy.IsKilled);
            Assert.IsFalse(spy.IsCaptured);
            Assert.AreEqual(0, spy.InjuryPoints);
            Assert.IsEmpty(results.OfType<OfficerInjuredResult>());
            Assert.IsEmpty(results.OfType<OfficerKilledResult>());
        }

        [Test]
        public void ProcessTick_OfficerFailsToEvadeDetector_AppliesInjuryBeforeCapture()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            spy.IsMain = true;
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = 100;
            game.Config.DuelResolution.MinimumInjuryChance = 100;
            game.Config.DuelResolution.InjuryBase = 1;
            game.Config.DuelResolution.InjurySecondaryRollMaximum = 0;
            game.Config.Recovery.MaxInjuryPoints = 100;
            game.Config.Assassination.KillProbability = 0;

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsTrue(spy.IsCaptured);
            Assert.Greater(spy.InjuryPoints, 0);
            Assert.AreSame(spy, results.OfType<OfficerInjuredResult>().Single().Officer);
        }

        [Test]
        public void ProcessTick_FailedDecoyEvasionDeath_DoesNotReuseDecoy()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _, MovementCommands movement) =
                BuildDetectionScene();
            spy.IsMain = true;
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = 100;
            game.Config.DuelResolution.MinimumInjuryChance = 100;
            game.Config.DuelResolution.InjuryBase = 1;
            game.Config.DuelResolution.InjurySecondaryRollMaximum = 0;
            game.Config.Recovery.MaxInjuryPoints = 100;
            game.Config.Assassination.KillProbability = 100;
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "r2",
                    OwnerInstanceID = "rebels",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -1000, 0 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);
            mission.AddDecoyParticipant(decoy);
            game.AttachNode(decoy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(decoy.IsCaptured);
            Assert.IsTrue(decoy.IsKilled);
            Assert.Greater(decoy.InjuryPoints, 0);
            Assert.AreEqual(
                0,
                results
                    .OfType<OfficerCaptureStateResult>()
                    .Count(result => result.TargetOfficer == decoy && result.IsCaptured)
            );
            Assert.AreEqual(
                1,
                results.OfType<OfficerKilledResult>().Count(result => result.TargetOfficer == decoy)
            );
        }

        [Test]
        public void ProcessTick_FactionViewSabotageTargetMissingAtArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "rebels");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, targetPlanet);

            missions.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: regiment
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            game.DetachNode(regiment);

            List<GameResult> results = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void ProcessTick_SabotageTargetBeginsConstructionBeforeArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "rebels");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, targetPlanet);
            missions.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: regiment
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            regiment.ManufacturingStatus = ManufacturingStatus.Building;

            List<GameResult> results = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void ProcessTick_AbductionTargetCapturedBeforeArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            missions.InitiateMission(
                CreateContext(
                    AbductionMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: target
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            target.IsCaptured = true;

            List<GameResult> results = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void ProcessTick_AbductionTargetBeginsTransitBeforeArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            missions.InitiateMission(
                CreateContext(
                    AbductionMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: target
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            target.Movement = new MovementState();

            List<GameResult> results = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void ProcessTick_AbductionTargetMovedAfterFactionViewSnapshot_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
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
                CreateContext(
                    AbductionMission.MissionTypeID,
                    participant,
                    viewPlanet,
                    selectedTarget: viewTarget
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;

            List<GameResult> results = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void ProcessTick_StaleMissingViewTarget_WaitsForArrivalThenFailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Officer viewTarget = EntityFactory.CreateOfficer(target.InstanceID, "rebels");
            viewTarget.SetParent(viewPlanet);
            game.DetachNode(target);

            bool created = missions.InitiateMission(
                CreateContext(
                    AssassinationMission.MissionTypeID,
                    participant,
                    viewPlanet,
                    selectedTarget: viewTarget
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();

            List<GameResult> travellingResults = missions.ProcessMissionTick(game);

            Assert.IsTrue(created);
            Assert.IsTrue(participant.Movement != null);
            Assert.IsFalse(travellingResults.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(1, game.GetSceneNodesByType<Mission>().Count);

            participant.Movement = null;
            List<GameResult> arrivalResults = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = arrivalResults
                .OfType<MissionCompletedResult>()
                .Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void ProcessTick_TargetPlanetDestroyedDuringTravel_WaitsForArrivalThenFails()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            missions.InitiateMission(
                CreateContext(
                    AssassinationMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: target
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            targetPlanet.IsDestroyed = true;

            List<GameResult> travellingResults = missions.ProcessMissionTick(game);

            Assert.IsTrue(participant.Movement != null);
            Assert.IsFalse(travellingResults.OfType<MissionCompletedResult>().Any());
            Assert.AreEqual(mission, game.GetSceneNodesByType<Mission>().Single());

            participant.Movement = null;
            List<GameResult> arrivalResults = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = arrivalResults
                .OfType<MissionCompletedResult>()
                .Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.IsEmpty(game.GetSceneNodesByType<Mission>());
        }

        [Test]
        public void ProcessTick_AssassinationTargetCapturedBeforeArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            missions.InitiateMission(
                CreateContext(
                    AssassinationMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: target
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            target.IsCaptured = true;

            List<GameResult> results = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void ProcessTick_RescueTargetFreedBeforeArrival_FailsAndTearsDown()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: true, capturedTarget: true);
            missions.InitiateMission(
                CreateContext(
                    RescueMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: target
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            participant.Movement = null;
            target.IsCaptured = false;

            List<GameResult> results = missions.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void ProcessTick_CapturedParticipantWithDifferentCaptor_StaysOnMissionPlanet()
        {
            (GameRoot game, Planet missionPlanet, Officer officer, MovementCommands movement) =
                BuildScene(factionOwnsPlanet: true);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

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

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.ProcessMissionTick(game);

            Assert.AreEqual(
                missionPlanet,
                officer.GetParent(),
                "Captured participant should not be moved to a separate captor planet"
            );
        }

        [Test]
        public void ProcessTick_OfficerKilledResult_DisablesAndRetainsKilledOfficer()
        {
            (GameRoot game, Planet planet, Officer participant, MovementCommands movement) =
                BuildScene(factionOwnsPlanet: true);
            Officer target = EntityFactory.CreateOfficer("target", "empire");
            game.AttachNode(target, planet);
            OfficerKillingMission mission = new OfficerKillingMission(
                "empire",
                planet.InstanceID,
                participant,
                target
            );
            game.AttachNode(mission, planet);
            game.MoveNode(participant, mission);
            mission.SetExecutionTick(0);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            system.ProcessMissionTick(game);

            Assert.IsTrue(target.IsKilled);
            Assert.IsFalse(target.IsActive());
            Assert.AreSame(
                target,
                game.GetSceneNodeByInstanceID<Officer>(target.InstanceID, includeDisabled: true)
            );
        }

        [Test]
        public void ProcessTick_ParticipantAttachedToMissionViaSceneGraph_DoesNotThrow()
        {
            // Regression: when BeginMission reparents an officer to the mission via
            // game.AttachNode, TearDownMission previously threw "cannot attach node because
            // it already has a parent" because it called AttachNode without DetachNode first.
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);

            // Simulate BeginMission: move officer to mission via scene graph (not SetParent).
            game.DetachNode(officer);
            game.AttachNode(officer, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            Assert.DoesNotThrow(() => system.ProcessMissionTick(game));
            Assert.AreEqual(
                planet,
                officer.GetParent(),
                "Officer should be reparented to the mission planet on teardown"
            );
        }

        [Test]
        public void ProcessTick_FriendlyLocation_ParticipantsRemainAtPlanet()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
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

            game.DetachNode(officer);
            game.AttachNode(officer, ship);

            StubMission mission = CreateMission(game, planet, officer);

            officer.MissionReturnParentInstanceID = ship.InstanceID;
            officer.MissionReturnLocationInstanceID = planet.InstanceID;
            game.DetachNode(officer);
            game.AttachNode(officer, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.ProcessMissionTick(game);

            Assert.AreEqual(
                planet,
                officer.GetParent(),
                "Officer should remain at a friendly mission location"
            );
        }

        [Test]
        public void ProcessTick_FriendlyUncolonizedLocation_RetainsOfficerLocally()
        {
            (GameRoot game, Planet origin, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            Planet missionPlanet = new Planet
            {
                InstanceID = "mission-planet",
                OwnerInstanceID = officer.OwnerInstanceID,
                IsColonized = false,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(missionPlanet, origin.GetParent());
            StubMission mission = CreateMission(game, missionPlanet, officer);
            game.MoveNode(officer, mission);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.AreSame(missionPlanet, officer.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.IsFalse(officer.IsCaptured);
            Assert.IsFalse(results.OfType<OfficerCaptureStateResult>().Any());
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void ProcessTick_HostileLocation_OriginFleetMoved_ReturnsToRecordedShip()
        {
            (GameRoot game, Planet planetA, Officer officer, MovementCommands movement) =
                BuildScene(factionOwnsPlanet: true);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sectorB = new PlanetSector
            {
                InstanceID = "sector2",
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(sectorB, game.Galaxy);
            Planet planetB = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planetB, sectorB);

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
            officer.MissionReturnParentInstanceID = ship.InstanceID;
            officer.MissionReturnLocationInstanceID = planetA.InstanceID;
            game.DetachNode(officer);
            game.AttachNode(officer, mission);

            // Fleet moves away from planet A to planet B while the mission is in progress.
            game.DetachNode(ship);
            game.DetachNode(fleet);
            game.AttachNode(fleet, planetB);
            game.AttachNode(ship, fleet);
            planetA.OwnerInstanceID = "rebels";

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.AreEqual(
                ship,
                officer.GetParent(),
                "Officer should return to its recorded ship when the origin fleet has moved"
            );
            Assert.AreSame(
                planetB,
                results.OfType<MissionCompletedResult>().Single().ReturnDestination
            );
        }

        [Test]
        public void ProcessTick_DiplomacyTargetCaptured_ReturnsOfficerToNearestFriendlyPlanet()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            Planet friendlyPlanet = new Planet
            {
                InstanceID = "friendly-planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(friendlyPlanet, planet.GetParent());
            planet.AddVisitor("empire");
            planet.SetPopularSupport("empire", 50);
            Mission mission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            mission.Initiate(0);
            officer.MissionReturnParentInstanceID = planet.InstanceID;
            officer.MissionReturnLocationInstanceID = planet.InstanceID;
            game.MoveNode(officer, mission);
            planet.OwnerInstanceID = "rebels";

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.ProcessMissionTick(game);

            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
            Assert.AreSame(friendlyPlanet, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
            Assert.IsFalse(results.OfType<OfficerCaptureStateResult>().Any());
        }

        [Test]
        public void ProcessTick_CapturedParticipant_SkipsMovement()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            officer.SetParent(mission);
            officer.IsCaptured = true;

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.ProcessMissionTick(game);

            Assert.IsNull(
                officer.Movement,
                "Captured officer should not have movement queued during teardown"
            );
        }

        [Test]
        public void InitiateMission_SecondDepartureEncounterFoils_FoilsBeforeTravel()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            Planet target = new Planet
            {
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(target, sector);

            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            game.AttachNode(officer, origin);
            Fleet originFleet = new Fleet
            {
                InstanceID = "origin-fleet",
                OwnerInstanceID = "rebels",
            };
            game.AttachNode(originFleet, origin);
            CapitalShip originShip = new CapitalShip
            {
                InstanceID = "origin-ship",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(originShip, originFleet);
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "origin-detector",
                    OwnerInstanceID = "rebels",
                    DetectionRating = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                originShip
            );
            Regiment sabotageTarget = CreateCompletedRegiment("target-regiment", "rebels");
            game.AttachNode(sabotageTarget, target);
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 50 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            DisableCaptureEvasionInjury(game);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            bool initiated = system.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    officer,
                    target,
                    selectedTarget: sabotageTarget
                )
            );
            List<GameResult> results = system.TakePendingResults();

            Assert.IsTrue(initiated);
            Assert.AreSame(origin, officer.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.IsFalse(game.GetSceneNodesByType<Mission>().Any());
            OfficerCaptureStateResult capture = results
                .OfType<OfficerCaptureStateResult>()
                .Single();
            MissionCompletedResult completed = results
                .OfType<MissionCompletedResult>()
                .Single(result => result.Outcome == MissionOutcome.Foiled);
            Assert.AreEqual(completed.MissionInstanceID, capture.MissionInstanceID);
        }

        [Test]
        public void InitiateMission_HostileForceUserOnMission_DoesNotFoilBeforeTravel()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            Planet target = new Planet
            {
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(target, sector);

            Officer participant = EntityFactory.CreateOfficer("participant", "empire");
            participant.IsForceSensitive = true;
            participant.IsForceEligible = true;
            participant.ForceValue = 1;
            game.AttachNode(participant, origin);
            StubMission hostileMission = new StubMission("rebels", origin.InstanceID);
            hostileMission.InstanceID = "hostile-mission";
            game.AttachNode(hostileMission, origin);
            Officer defender = EntityFactory.CreateOfficer("defender", "rebels");
            defender.IsForceSensitive = true;
            defender.IsForceEligible = true;
            defender.ForceValue = 60;
            game.AttachNode(defender, hostileMission);
            Regiment sabotageTarget = CreateCompletedRegiment("target-regiment", "rebels");
            game.AttachNode(sabotageTarget, target);
            game.Config.Jedi.EncounterProbabilityOffset = 100;
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );

            bool initiated = system.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    participant,
                    target,
                    selectedTarget: sabotageTarget
                )
            );
            List<GameResult> results = system.TakePendingResults();

            Assert.IsTrue(initiated);
            Assert.IsInstanceOf<SabotageMission>(participant.GetParent());
            Assert.IsNotNull(participant.Movement);
            Assert.IsFalse(
                results
                    .OfType<MissionCompletedResult>()
                    .Any(result => result.Outcome == MissionOutcome.Foiled)
            );
        }

        [Test]
        public void InitiateMission_ParticipantAssigned_SetsParticipantParentToMission()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(empirePlanet, sector);

            Planet targetPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(targetPlanet, sector);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, empirePlanet);
            Regiment sabotageTarget = CreateCompletedRegiment("r1", "rebels");
            game.AttachNode(sabotageTarget, targetPlanet);

            FogOfWarCommands fog = new FogOfWarCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                fog,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands missionSystem = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            missionSystem.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    officer,
                    targetPlanet,
                    selectedTarget: sabotageTarget
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().FirstOrDefault();
            Assert.IsNotNull(mission, "Mission should be created");
            Assert.AreEqual(
                mission,
                officer.GetParent(),
                "Participant should be parented to the mission after BeginMission"
            );
            Assert.AreEqual(empirePlanet.InstanceID, officer.MissionReturnParentInstanceID);
            Assert.AreEqual(empirePlanet.InstanceID, officer.MissionReturnLocationInstanceID);
        }

        [Test]
        public void InitiateMission_AssignedParticipant_IsOnMission()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(empirePlanet, sector);

            Planet targetPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(targetPlanet, sector);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, empirePlanet);
            Regiment sabotageTarget = CreateCompletedRegiment("r1", "rebels");
            game.AttachNode(sabotageTarget, targetPlanet);

            FogOfWarCommands fog = new FogOfWarCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                fog,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands missionSystem = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            missionSystem.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    officer,
                    targetPlanet,
                    selectedTarget: sabotageTarget
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
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            while (!mission.IsComplete())
                mission.IncrementProgress();

            IReadOnlyList<GameResult> results = new MissionTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(
                results.Any(r => r is MissionCompletedResult),
                "ProcessTick should aggregate results from all missions and include MissionCompletedResult"
            );
        }

        [Test]
        public void ProcessTick_RecruitmentMissionsExhaustCandidates_ReturnsOneRecruitmentExhaustedResult()
        {
            (GameRoot game, Planet planet, Officer firstOfficer, MovementCommands movement) =
                BuildScene(factionOwnsPlanet: true);
            Faction faction = game.GetFactions().Single(faction => faction.InstanceID == "empire");
            firstOfficer.IsMain = true;
            Officer secondOfficer = EntityFactory.CreateOfficer("o2", "empire");
            secondOfficer.IsMain = true;
            game.AttachNode(secondOfficer, planet);

            Officer firstTarget = EntityFactory.CreateOfficer("target1", "rebels");
            firstTarget.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            Officer secondTarget = EntityFactory.CreateOfficer("target2", "rebels");
            secondTarget.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(firstTarget);
            game.GetUnrecruitedOfficers().Add(secondTarget);

            Mission firstMission = MissionTestFactory.TryCreate(
                RecruitmentMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { firstOfficer }
            );
            Mission secondMission = MissionTestFactory.TryCreate(
                RecruitmentMission.MissionTypeID,
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { secondOfficer }
            );
            game.AttachNode(firstMission, planet);
            game.AttachNode(secondMission, planet);
            game.DetachNode(firstOfficer);
            game.DetachNode(secondOfficer);
            game.AttachNode(firstOfficer, firstMission);
            game.AttachNode(secondOfficer, secondMission);
            firstMission.Initiate(0);
            secondMission.Initiate(0);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.0),
                movement
            );
            IReadOnlyList<GameResult> results = new MissionTickProcessor(system).ProcessTick(game);

            RecruitmentExhaustedResult exhausted = results
                .OfType<RecruitmentExhaustedResult>()
                .Single();
            Assert.AreEqual(faction, exhausted.Faction);
            Assert.AreEqual(planet, exhausted.Planet);
        }

        [Test]
        public void ProcessTick_WithSpecialForcesParticipant_AppearsInParticipants()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
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
            game.AttachNode(sf, mission);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );
            List<GameResult> results = system.ProcessMissionTick(game);
            MissionCompletedResult completedResult = results
                .OfType<MissionCompletedResult>()
                .First();

            Assert.IsTrue(
                completedResult.Participants.Any(p => p.InstanceID == "sf1"),
                "SpecialForces participant must appear in Participants"
            );
        }

        [Test]
        public void ProcessTick_WithDecoyParticipant_DecoyAppearsInParticipants()
        {
            // Both main and decoy participants should appear in MissionCompletedResult.Participants.
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
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
            game.MoveNode(officer, mission);
            mission.AddDecoyParticipant(decoy);
            game.AttachNode(decoy, mission);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );
            List<GameResult> results = system.ProcessMissionTick(game);
            MissionCompletedResult completedResult = results
                .OfType<MissionCompletedResult>()
                .First();

            Assert.IsTrue(
                completedResult.Participants.Any(p => p.InstanceID == "o2"),
                "Decoy must appear in Participants"
            );
        }

        [Test]
        public void AbortMission_ActiveMission_ReturnsParticipantAndDetachesMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            game.MoveNode(officer, mission);
            mission.Initiate(1);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            bool aborted = system.AbortMission(mission.InstanceID);

            Assert.IsTrue(aborted);
            Assert.AreEqual(planet, officer.GetParent());
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void AbortMission_ParticipantInTransit_ReturnsParticipantToOrigin()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            game.MoveNode(officer, mission);
            officer.Movement = new MovementState { TransitTicks = 10 };
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            bool aborted = system.AbortMission(mission.InstanceID);

            Assert.IsTrue(aborted);
            Assert.AreEqual(planet, officer.GetParent());
            Assert.IsNull(mission.GetParent());
        }

        [Test]
        public void ProcessTick_AbortedStrandedMission_ReturnsDeferredCaptureOnce()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            planet.OwnerInstanceID = "captor";
            game.GetFactions().Add(new Faction { InstanceID = "captor" });
            StubMission mission = CreateMission(game, planet, officer);
            game.MoveNode(officer, mission);
            game.CurrentTick = 42;
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new ThrowingRNG(),
                movement
            );

            Assert.IsTrue(system.AbortMission(mission.InstanceID));
            Assert.IsTrue(officer.IsCaptured);
            Assert.IsNull(mission.GetParent());
            game.CurrentTick = 43;
            IReadOnlyList<GameResult> results = new MissionTickProcessor(system).ProcessTick(game);

            OfficerCaptureStateResult capture = results
                .OfType<OfficerCaptureStateResult>()
                .Single();
            Assert.AreSame(officer, capture.TargetOfficer);
            Assert.AreEqual(42, capture.Tick);
            Assert.AreEqual(mission.InstanceID, capture.MissionInstanceID);
            Assert.IsEmpty(new MissionTickProcessor(system).ProcessTick(game));
        }

        [Test]
        public void InitiateMission_ResearchWithDiscipline_AttachesResearchMissionToPlanet()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            FogOfWarCommands fog = new FogOfWarCommands(game);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            system.InitiateMission(
                CreateContext(
                    ResearchMission.MissionTypeID,
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
        public void ProcessTick_ResearchFacilityRemoved_FailsWithNoResearchFacilities()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );
            Assert.IsTrue(
                system.InitiateMission(
                    CreateContext(
                        ResearchMission.MissionTypeID,
                        officer,
                        planet,
                        discipline: ResearchDiscipline.FacilityDesign
                    )
                )
            );
            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            Building constructionYard = planet
                .GetProductionFacilities(ManufacturingType.Building)
                .Single();
            game.DetachNode(constructionYard);

            List<GameResult> results = system.ProcessMissionTick(game);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(
                MissionCompletionReason.NoResearchFacilities,
                completed.CompletionReason
            );
            Assert.IsEmpty(game.GetSceneNodesByType<Mission>());
        }

        [Test]
        public void ProcessTick_StartedMission_ReturnsMissionStartedResult()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            bool initiated = commands.InitiateMission(
                CreateContext(
                    ResearchMission.MissionTypeID,
                    officer,
                    planet,
                    discipline: ResearchDiscipline.FacilityDesign
                )
            );
            IReadOnlyList<GameResult> results = new MissionTickProcessor(commands).ProcessTick(
                game
            );

            Assert.IsTrue(initiated);
            MissionStartedResult started = results.OfType<MissionStartedResult>().Single();
            Assert.AreEqual(ResearchMission.MissionTypeID, started.MissionTypeID);
            Assert.AreSame(planet, started.Location);
            Assert.AreSame(started.Mission, game.GetSceneNodesByType<Mission>().Single());
            Assert.AreEqual(new[] { officer }, started.Participants);
        }

        [Test]
        public void InitiateMission_CommandingOfficer_ClearsCommandRank()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.FacilityResearch = 1;
            officer.CurrentRank = OfficerRank.Commander;
            AddResearchFacilities(game, planet);
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            bool initiated = commands.InitiateMission(
                CreateContext(
                    ResearchMission.MissionTypeID,
                    officer,
                    planet,
                    discipline: ResearchDiscipline.FacilityDesign
                )
            );
            List<GameResult> results = commands.TakePendingResults();

            Assert.IsTrue(initiated);
            Assert.AreEqual(OfficerRank.None, officer.CurrentRank);
            CommandKindChangedResult rankChanged = results
                .OfType<CommandKindChangedResult>()
                .Single();
            Assert.AreEqual((int)OfficerRank.None, rankChanged.CommandKind);
            Assert.AreEqual((int)OfficerRank.Commander, rankChanged.Detail);
            Assert.IsNull(results.OfType<OfficerCommandingResult>().Single().CommandTarget);
        }

        [Test]
        public void InitiateMission_ExhaustedResearch_ReturnsFalse()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            officer.ShipResearch = 1;
            AddResearchFacilities(game, planet);
            game.GetFactions().Single().ResearchCatalog[ResearchDiscipline.ShipDesign] =
                new List<ResearchCatalogEntry>();
            MissionCommands missions = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            bool initiated = missions.InitiateMission(
                CreateContext(
                    ResearchMission.MissionTypeID,
                    officer,
                    planet,
                    discipline: ResearchDiscipline.ShipDesign
                )
            );

            Assert.IsFalse(initiated);
            Assert.IsEmpty(game.GetSceneNodesByType<Mission>());
        }

        [TestCase(GameDifficulty.Easy, 0, 5)]
        [TestCase(GameDifficulty.Medium, 0, 5)]
        [TestCase(GameDifficulty.Hard, 50, 4)]
        public void InitiateMission_AIFaction_AppliesExecutionSpeedIncrease(
            GameDifficulty difficulty,
            int increasePercent,
            int expectedTicks
        )
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            game.Summary = new GameSummary { Difficulty = difficulty, PlayerFactionID = "empire" };
            game.SetFactionController("empire", "player", PlayerControllerType.AI);
            planet.AddVisitor("empire");
            game.Config.DifficultyModifiers[difficulty] = new DifficultyModifiers
            {
                MissionExecutionSpeedIncreasePercent = increasePercent,
            };
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            bool created = system.InitiateMission(
                CreateContext(DiplomacyMission.MissionTypeID, officer, planet)
            );

            Assert.IsTrue(created);
            Assert.AreEqual(
                expectedTicks,
                game.GetSceneNodesByType<DiplomacyMission>().Single().MaxProgress
            );
        }

        [Test]
        public void InitiateMission_HumanFaction_DoesNotApplyExecutionSpeedIncrease()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            game.Summary = new GameSummary
            {
                Difficulty = GameDifficulty.Hard,
                PlayerFactionID = "empire",
            };
            game.SetFactionController("empire", "player", PlayerControllerType.Human);
            planet.AddVisitor("empire");
            game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MissionExecutionSpeedIncreasePercent = 50,
            };
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            bool created = system.InitiateMission(
                CreateContext(DiplomacyMission.MissionTypeID, officer, planet)
            );

            Assert.IsTrue(created);
            Assert.AreEqual(5, game.GetSceneNodesByType<DiplomacyMission>().Single().MaxProgress);
        }

        [TestCase(0, 60)]
        [TestCase(30, 90)]
        public void InitiateMission_JediTraining_UsesConfiguredExecutionRange(
            int rolledSpread,
            int expectedTicks
        )
        {
            (GameRoot game, Planet planet, Officer trainer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            trainer.IsForceSensitive = true;
            trainer.IsJediTrainer = true;
            trainer.IsForceEligible = true;
            trainer.ForceValue = 120;
            Officer student = EntityFactory.CreateOfficer("student", "empire");
            student.IsForceSensitive = true;
            student.IsForceEligible = true;
            student.ForceValue = 40;
            game.AttachNode(student, planet);
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new SequenceRNG(intValues: new[] { rolledSpread }),
                movement
            );

            bool created = system.InitiateMission(
                CreateContext(
                    JediTrainingMission.MissionTypeID,
                    new List<IMissionParticipant> { trainer, student },
                    new List<IMissionParticipant>(),
                    planet
                )
            );

            Assert.IsTrue(created);
            Assert.AreEqual(
                expectedTicks,
                game.GetSceneNodesByType<JediTrainingMission>().Single().MaxProgress
            );
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
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = CreateCompletedRegiment("regiment", "rebels");
            game.AttachNode(regiment, targetPlanet);
            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Officer viewParticipant = EntityFactory.CreateOfficer(participant.InstanceID, "empire");
            Regiment viewRegiment = CreateCompletedRegiment(regiment.InstanceID, "rebels");
            viewRegiment.SetParent(viewPlanet);

            bool created = missions.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    new List<IMissionParticipant> { viewParticipant },
                    new List<IMissionParticipant>(),
                    viewPlanet,
                    selectedTarget: viewRegiment
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(targetPlanet, mission.GetParent());
            Assert.AreEqual(participant, mission.GetMainParticipants().Single());
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
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "rebels");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, targetPlanet);
            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Regiment viewRegiment = EntityFactory.CreateRegiment(regiment.InstanceID, "rebels");
            viewRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            viewRegiment.SetParent(viewPlanet);

            bool created = missions.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    participant,
                    viewPlanet,
                    selectedTarget: viewRegiment
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(targetPlanet, mission.GetParent());
            Assert.AreEqual(targetPlanet.InstanceID, mission.LocationInstanceID);
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
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Officer viewTarget = EntityFactory.CreateOfficer(target.InstanceID, "rebels");
            viewTarget.SetParent(viewPlanet);

            bool created = missions.InitiateMission(
                CreateContext(
                    AbductionMission.MissionTypeID,
                    participant,
                    viewPlanet,
                    selectedTarget: viewTarget
                )
            );

            Mission mission = game.GetSceneNodesByType<Mission>().Single();
            Assert.IsTrue(created);
            Assert.AreEqual(targetPlanet, mission.GetParent());
            Assert.AreEqual(target.InstanceID, ((AbductionMission)mission).TargetOfficerInstanceID);
        }

        [Test]
        public void InitiateMission_StaleCompletedViewTarget_CreatesMissionFromObservedState()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment liveRegiment = EntityFactory.CreateRegiment("regiment", "rebels");
            liveRegiment.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(liveRegiment, targetPlanet);

            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Regiment viewRegiment = EntityFactory.CreateRegiment(liveRegiment.InstanceID, "rebels");
            viewRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            viewRegiment.SetParent(viewPlanet);

            bool created = missions.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    participant,
                    viewPlanet,
                    selectedTarget: viewRegiment
                )
            );

            Assert.IsTrue(created);
            Assert.AreEqual(1, game.GetSceneNodesByType<Mission>().Count);
        }

        [Test]
        public void InitiateMission_IneligibleSelectedTarget_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionCommands missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);

            bool created = missions.InitiateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: target
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
                MissionCommands missions
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
                CreateContext(
                    SabotageMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: regiment
                )
            );

            Assert.IsFalse(created);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        // Builds a game with one planet and one officer whose recorded mission return location
        // is that planet. The officer remains parented to the planet until each test moves it.
        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="factionOwnsPlanet">Whether faction owns planet.</param>
        /// <param name="canBetray">Whether the officer's loyalty can change and permit betrayal.</param>
        /// <param name="loyalty">The officer's starting loyalty.</param>
        /// <returns>The constructed scene.</returns>
        private (
            GameRoot game,
            Planet planet,
            Officer officer,
            MovementCommands movement
        ) BuildScene(bool factionOwnsPlanet, bool canBetray = false, int loyalty = 100)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            Faction faction = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(faction);

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                TypeID = "home-planet",
                OwnerInstanceID = factionOwnsPlanet ? "empire" : null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, sector);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire", canBetray, loyalty);
            officer.Movement = null;
            officer.MissionReturnParentInstanceID = planet.InstanceID;
            officer.MissionReturnLocationInstanceID = planet.InstanceID;
            // Parent to planet so IsOnMission() = false and IsMovable() = true.
            game.AttachNode(officer, planet);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            return (game, planet, officer, movement);
        }

        /// <summary>
        /// Builds a hostile one-tick mission whose officer and special-forces participants have
        /// no recorded return destination and are guaranteed to fail.
        /// </summary>
        /// <returns>The failed-mission return scene and its mission command system.</returns>
        private (
            GameRoot game,
            PlanetSector sector,
            Planet missionPlanet,
            StubMission mission,
            Officer officer,
            SpecialForces specialForces,
            MissionCommands system
        ) BuildFailedMissionReturnScene()
        {
            GameConfig config = TestConfig.Create();
            config.ProbabilityTables.Mission.DefaultSuccessProbability = 0;
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "return-sector",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet missionPlanet = AddMissionReturnPlanet(
                game,
                sector,
                "mission-planet",
                "rebels",
                positionX: 0
            );
            Officer officer = EntityFactory.CreateOfficer("return-officer", "empire");
            officer.MissionReturnParentInstanceID = "missing-parent";
            officer.MissionReturnLocationInstanceID = "missing-location";
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "return-special-forces",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MissionReturnParentInstanceID = "missing-parent",
                MissionReturnLocationInstanceID = "missing-location",
            };
            StubMission mission = new StubMission("empire", missionPlanet.InstanceID)
            {
                InstanceID = "failed-return-mission",
            };
            game.AttachNode(mission, missionPlanet);
            game.AttachNode(officer, mission);
            game.AttachNode(specialForces, mission);
            while (!mission.IsComplete())
                mission.IncrementProgress();

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.99),
                movement
            );

            return (game, sector, missionPlanet, mission, officer, specialForces, system);
        }

        /// <summary>
        /// Adds a colonized planet used by failed-mission return tests.
        /// </summary>
        /// <param name="game">The game receiving the planet.</param>
        /// <param name="sector">The sector receiving the planet.</param>
        /// <param name="instanceId">The planet instance identifier.</param>
        /// <param name="ownerInstanceId">The owning faction identifier, or null for neutral.</param>
        /// <param name="positionX">The planet's horizontal position.</param>
        /// <returns>The added planet.</returns>
        private static Planet AddMissionReturnPlanet(
            GameRoot game,
            PlanetSector sector,
            string instanceId,
            string ownerInstanceId,
            int positionX
        )
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                TypeID = instanceId,
                OwnerInstanceID = ownerInstanceId,
                IsColonized = true,
                PositionX = positionX,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, sector);
            return planet;
        }

        /// <summary>
        /// Adds a stationary fleet with one operational capital ship for mission-return tests.
        /// </summary>
        /// <param name="game">The game receiving the fleet.</param>
        /// <param name="planet">The planet hosting the fleet.</param>
        /// <param name="instanceIdPrefix">The instance identifier prefix.</param>
        /// <param name="ownerInstanceId">The fleet owner's faction identifier.</param>
        /// <returns>The added fleet and its capital ship.</returns>
        private static (Fleet fleet, CapitalShip capitalShip) AddMissionReturnFleet(
            GameRoot game,
            Planet planet,
            string instanceIdPrefix,
            string ownerInstanceId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet($"{instanceIdPrefix}-fleet", ownerInstanceId);
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = $"{instanceIdPrefix}-capital-ship",
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                Hyperdrive = 1,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(capitalShip, fleet);
            return (fleet, capitalShip);
        }

        // Creates a mission with the officer in MainParticipants (but officer stays parented to
        // the planet, not the mission) so IncrementProgress counts down and IsMovable() holds.
        /// <summary>
        /// Creates mission.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="officer">The officer.</param>
        /// <returns>The created mission.</returns>
        private StubMission CreateMission(GameRoot game, Planet planet, Officer officer)
        {
            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.AddChild(officer);
            return mission;
        }

        /// <summary>
        /// Sets foil table.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="table">The table.</param>
        private static void SetFoilTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.Foil = table;
        }

        /// <summary>
        /// Sets decoy table.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="table">The table.</param>
        private static void SetDecoyTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = table;
            game.Config.ProbabilityTables.Mission.FleetDecoy = table;
        }

        /// <summary>
        /// Sets evasion table.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="table">The table.</param>
        private static void SetEvasionTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.Evasion = table;
        }

        /// <summary>
        /// Executes disable capture evasion injury.
        /// </summary>
        /// <param name="game">The game.</param>
        private static void DisableCaptureEvasionInjury(GameRoot game)
        {
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = 0;
            game.Config.DuelResolution.MinimumInjuryChance = 0;
        }

        /// <summary>
        /// Creates completed regiment.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="ownerInstanceID">The owner instance id.</param>
        /// <returns>The created completed regiment.</returns>
        private static Regiment CreateCompletedRegiment(string id, string ownerInstanceID)
        {
            return new Regiment
            {
                InstanceID = id,
                OwnerInstanceID = ownerInstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Adds research facilities.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
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
                    InstanceID = "training-facility",
                    OwnerInstanceID = planet.OwnerInstanceID,
                    ProductionType = ManufacturingType.Troop,
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

        /// <summary>
        /// Creates request.
        /// </summary>
        /// <param name="missionTypeId">The mission type id.</param>
        /// <param name="participant">The participant.</param>
        /// <param name="target">The target.</param>
        /// <param name="targetOfficer">The target officer.</param>
        /// <param name="discipline">The discipline.</param>
        /// <param name="selectedTarget">The selected target.</param>
        /// <returns>The created request.</returns>
        private static MissionContext CreateContext(
            string missionTypeId,
            IMissionParticipant participant,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode selectedTarget = null
        )
        {
            return CreateContext(
                missionTypeId,
                new List<IMissionParticipant> { participant },
                new List<IMissionParticipant>(),
                target,
                targetOfficer,
                discipline,
                selectedTarget
            );
        }

        /// <summary>
        /// Creates request.
        /// </summary>
        /// <param name="missionTypeId">The mission type id.</param>
        /// <param name="mainParticipants">The main participants.</param>
        /// <param name="decoyParticipants">The decoy participants.</param>
        /// <param name="target">The target.</param>
        /// <param name="targetOfficer">The target officer.</param>
        /// <param name="discipline">The discipline.</param>
        /// <param name="selectedTarget">The selected target.</param>
        /// <returns>The created request.</returns>
        private static MissionContext CreateContext(
            string missionTypeId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode selectedTarget = null
        )
        {
            return new MissionContext
            {
                MissionTypeID = missionTypeId,
                Location = target,
                Discipline = discipline,
                SelectedTarget = targetOfficer ?? selectedTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
            };
        }

        /// <summary>
        /// Builds orbital detection scene.
        /// </summary>
        /// <param name="planetOwnerId">The planet owner id.</param>
        /// <returns>The constructed orbital detection scene.</returns>
        private (
            GameRoot game,
            Planet planet,
            Officer spy,
            CapitalShip capturingUnit,
            MovementCommands movement
        ) BuildOrbitalDetectionScene(string planetOwnerId = "empire")
        {
            GameConfig config = new GameConfig();
            config.Movement.DistanceDivisor = 5;
            config.Jedi.MissionParticipantEncounterMinimum = 1;
            config.Jedi.MissionDefenderEncounterMinimum = 60;
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet homePlanet = new Planet
            {
                InstanceID = "empire-home",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = -100,
                PositionY = 0,
            };
            game.AttachNode(homePlanet, sector);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = planetOwnerId,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, sector);

            Officer spy = EntityFactory.CreateOfficer("spy", "empire");
            spy.MissionReturnParentInstanceID = homePlanet.InstanceID;
            spy.MissionReturnLocationInstanceID = homePlanet.InstanceID;
            game.AttachNode(spy, homePlanet);

            // The hostile detector is an orbiting capital ship.
            Fleet fleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "rebels" };
            game.AttachNode(fleet, planet);

            CapitalShip captorShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(captorShip, fleet);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            return (game, planet, spy, captorShip, movement);
        }

        /// <summary>
        /// Executes run orbital capture mission.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="spy">The spy.</param>
        /// <param name="movement">The movement.</param>
        /// <returns>The result of run orbital capture mission.</returns>
        private List<GameResult> RunOrbitalCaptureMission(
            GameRoot game,
            Planet planet,
            Officer spy,
            MovementCommands movement
        )
        {
            StubMission mission = new StubMission("empire", planet.InstanceID);
            SetFoilTable(game, new Dictionary<int, int> { { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -200, 0 } });
            DisableCaptureEvasionInjury(game);
            game.AttachNode(mission, planet);
            game.MoveNode(spy, mission);

            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.01),
                movement
            );
            return system.ProcessMissionTick(game);
        }

        /// <summary>
        /// Builds detection scene.
        /// </summary>
        /// <returns>The constructed detection scene.</returns>
        private (
            GameRoot game,
            Planet planet,
            Officer spy,
            Officer defender,
            MovementCommands movement
        ) BuildDetectionScene()
        {
            GameConfig config = new GameConfig();
            config.Movement.DistanceDivisor = 5;
            config.Jedi.MissionParticipantEncounterMinimum = 1;
            config.Jedi.MissionDefenderEncounterMinimum = 60;
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet homePlanet = new Planet
            {
                InstanceID = "empire-home",
                TypeID = "empire-home",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = -100,
                PositionY = 0,
            };
            game.AttachNode(homePlanet, sector);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "rebels", 50 } },
            };
            game.AttachNode(planet, sector);

            Officer spy = EntityFactory.CreateOfficer("spy", "empire");
            spy.MissionReturnParentInstanceID = homePlanet.InstanceID;
            spy.MissionReturnLocationInstanceID = homePlanet.InstanceID;
            game.AttachNode(spy, homePlanet);
            Officer defender = EntityFactory.CreateOfficer("defender", "rebels");
            defender.CurrentRank = OfficerRank.General;
            game.AttachNode(defender, planet);

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            Fleet fleet = new Fleet { InstanceID = "detection-fleet", OwnerInstanceID = "rebels" };
            game.AttachNode(fleet, planet);
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "detection-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(capitalShip, fleet);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            return (game, planet, spy, defender, movement);
        }

        /// <summary>
        /// Builds officer target mission scene.
        /// </summary>
        /// <param name="friendlyTarget">Whether friendly target.</param>
        /// <param name="capturedTarget">Whether captured target.</param>
        /// <returns>The constructed officer target mission scene.</returns>
        private (
            GameRoot game,
            Planet origin,
            Planet targetPlanet,
            Officer participant,
            Officer target,
            MissionCommands missions
        ) BuildOfficerTargetMissionScene(bool friendlyTarget, bool capturedTarget)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

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
            game.AttachNode(origin, sector);
            game.AttachNode(targetPlanet, sector);

            Officer participant = EntityFactory.CreateOfficer("participant", "empire");
            game.AttachNode(participant, origin);

            Officer target = EntityFactory.CreateOfficer(
                "target",
                friendlyTarget ? "empire" : "rebels"
            );
            target.IsCaptured = capturedTarget;
            target.CaptorInstanceID = capturedTarget ? "rebels" : null;
            game.AttachNode(target, targetPlanet);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            MissionCommands missions = TestSystems.CreateMissionCommands(
                game,
                new FixedRNG(0.0),
                movement
            );
            return (game, origin, targetPlanet, participant, target, missions);
        }

        /// <summary>
        /// Builds a scene with a rebels-owned planet, a rebels officer running Mission,
        /// and an empire officer running Mission. Both missions are advanced to
        /// MaxProgress - 1 so a single mission tick completes each one.
        /// The InciteUprising table is seeded to guarantee success with StubRNG.
        /// </summary>
        /// <param name="ownerSupport">The owner support.</param>
        /// <param name="hasGarrison">Whether has garrison.</param>
        /// <param name="inciteFirst">Whether the incite mission is attached first.</param>
        /// <returns>The constructed concurrent missions scene.</returns>
        private (
            GameRoot game,
            Mission diplomacyMission,
            Mission inciteMission,
            MissionCommands missionSystem
        ) BuildConcurrentMissionsScene(
            int ownerSupport = 50,
            bool hasGarrison = true,
            bool inciteFirst = false
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);

            Faction rebels = new Faction { InstanceID = "rebels" };
            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(rebels);
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet rebelsPlanet = new Planet
            {
                InstanceID = "rebels_planet",
                TypeID = "rebels-home",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "rebels", ownerSupport } },
            };
            game.AttachNode(rebelsPlanet, sector);

            if (hasGarrison)
            {
                Regiment garrison = CreateCompletedRegiment("rebels_garrison", "rebels");
                game.AttachNode(garrison, rebelsPlanet);
            }

            Planet empirePlanet = new Planet
            {
                InstanceID = "empire_planet",
                TypeID = "empire-home",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 60 } },
            };
            game.AttachNode(empirePlanet, sector);

            Officer rebelsOfficer = EntityFactory.CreateOfficer("rebels_o1", "rebels");
            game.AttachNode(rebelsOfficer, rebelsPlanet);

            Officer empireOfficer = EntityFactory.CreateOfficer("empire_o1", "empire");
            game.AttachNode(empireOfficer, empirePlanet);

            rebelsPlanet.AddVisitor("rebels");

            Mission diplomacyMission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                game,
                "rebels",
                rebelsPlanet,
                new List<IMissionParticipant> { rebelsOfficer },
                new List<IMissionParticipant>()
            );
            game.Config.ProbabilityTables.Mission.Diplomacy = new Dictionary<int, int>
            {
                { -200, 0 },
            };

            Mission inciteMission = MissionTestFactory.TryCreate(
                InciteUprisingMission.MissionTypeID,
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
            game.Config.Uprising.PrimaryConsequenceTable.Clear();
            game.Config.Uprising.SecondaryConsequenceTable.Clear();
            if (inciteFirst)
            {
                game.AttachNode(inciteMission, rebelsPlanet);
                game.AttachNode(diplomacyMission, rebelsPlanet);
            }
            else
            {
                game.AttachNode(diplomacyMission, rebelsPlanet);
                game.AttachNode(inciteMission, rebelsPlanet);
            }

            diplomacyMission.Initiate(0);
            inciteMission.Initiate(0);

            while (diplomacyMission.CurrentProgress < diplomacyMission.MaxProgress - 1)
                diplomacyMission.IncrementProgress();
            while (inciteMission.CurrentProgress < inciteMission.MaxProgress - 1)
                inciteMission.IncrementProgress();

            StubRNG rng = new StubRNG();
            FogOfWarCommands fog = new FogOfWarCommands(game);
            FleetCommands fleet = new FleetCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                fog,
                fleet,
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            ManufacturingCommands manufacturing = new ManufacturingCommands(
                game,
                fleet,
                new ManufacturingQueries(game),
                movement
            );
            PlanetaryControlCommands control = new PlanetaryControlCommands(
                game,
                movement,
                manufacturing,
                fog,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            UprisingCommands uprising = new UprisingCommands(game, rng, control);
            MissionCommands missionSystem = new MissionCommands(
                game,
                rng,
                movement,
                uprising,
                new MissionQueries(game),
                new MovementQueries(game)
            );

            return (game, diplomacyMission, inciteMission, missionSystem);
        }

        private sealed class OfficerKillingMission : Mission
        {
            private readonly Officer _target;

            /// <summary>
            /// Creates an empty officer-killing mission copy.
            /// </summary>
            /// <returns>An empty officer-killing mission.</returns>
            protected override BaseSceneNode CreateNodeCopy() =>
                new OfficerKillingMission(null, null, null, null);

            /// <summary>
            /// Initializes a new instance of the OfficerKillingMission class.
            /// </summary>
            /// <param name="ownerInstanceId">The owner instance id.</param>
            /// <param name="locationInstanceId">The location instance id.</param>
            /// <param name="participant">The participant.</param>
            /// <param name="target">The target.</param>
            public OfficerKillingMission(
                string ownerInstanceId,
                string locationInstanceId,
                IMissionParticipant participant,
                Officer target
            )
                : base(
                    "OfficerKilling",
                    ownerInstanceId,
                    locationInstanceId,
                    new List<IMissionParticipant> { participant },
                    new List<IMissionParticipant>(),
                    SkillRating.Diplomacy
                )
            {
                _target = target;
            }

            /// <summary>
            /// Executes on success.
            /// </summary>
            /// <param name="game">The game.</param>
            /// <param name="provider">The provider.</param>
            /// <param name="successfulParticipant">The successful participant.</param>
            /// <returns>The result of on success.</returns>
            protected override List<GameResult> OnSuccess(
                GameRoot game,
                IRandomNumberProvider provider,
                IMissionParticipant successfulParticipant
            ) =>
                new List<GameResult>
                {
                    new OfficerKilledResult
                    {
                        TargetOfficer = _target,
                        Context = GetParent() as Planet,
                        Tick = game.CurrentTick,
                    },
                };

            /// <summary>
            /// Checks whether the repeat after completion condition is met.
            /// </summary>
            /// <param name="game">The game.</param>
            /// <returns>True when the repeat after completion condition is met; otherwise false.</returns>
            public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;
        }
    }
}
