using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

/// <summary>Verifies handle results captured mission participant tears down mission at current planet.</summary>
namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class MissionObserverTests
    {
        [Test]
        public void HandleResults_ReleasedParticipant_DoesNotInterruptMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            game.MoveNode(officer, mission);
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new ThrowingRNG(),
                movement
            );
            MissionObserver observer = new MissionObserver(commands);

            List<GameResult> results = observer.HandleResults(
                new[]
                {
                    new OfficerCaptureStateResult { TargetOfficer = officer, IsCaptured = false },
                }
            );

            Assert.IsEmpty(results);
            Assert.AreSame(planet, mission.GetParent());
            Assert.AreSame(mission, officer.GetParent());
        }

        [Test]
        public void HandleResults_NullEntry_ReturnsNoResults()
        {
            (GameRoot game, Planet _, Officer _, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new ThrowingRNG(),
                movement
            );
            MissionObserver observer = new MissionObserver(commands);

            Assert.IsEmpty(observer.HandleResults(new OfficerCaptureStateResult[] { null }));
        }

        [Test]
        public void HandleResults_MissingOfficer_DoesNotInterruptRecordedMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );
            MissionObserver observer = new MissionObserver(commands);

            observer.HandleResults(
                new[]
                {
                    new OfficerCaptureStateResult { IsCaptured = true, ParentAtCapture = mission },
                }
            );

            Assert.AreSame(planet, mission.GetParent());
        }

        [Test]
        public void Constructor_NullCommands_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MissionObserver(null)
            );

            Assert.AreEqual("commands", exception.ParamName);
        }

        [Test]
        public void Connect_CapturedMissionParticipant_RegistersInterruption()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            game.MoveNode(officer, mission);
            mission.Initiate(1);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "rebels";
            officer.IsEnabled = false;
            MissionCommands system = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );

            GameResultBus results = new GameResultBus();
            MissionObserver observer = new MissionObserver(system);
            observer.Connect(results);
            results.Publish(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    ParentAtCapture = mission,
                    Context = planet,
                }
            );

            Assert.IsNull(mission.GetParent());
            Assert.AreSame(planet, officer.GetParent());
            Assert.IsTrue(officer.IsCaptured);
            Assert.IsFalse(officer.IsEnabled);
        }

        [Test]
        public void HandleResults_OfficerMovedBeforeDelivery_InterruptsRecordedMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            game.MoveNode(officer, mission);
            mission.Initiate(1);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "rebels";
            officer.IsEnabled = false;
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );
            GameResultBus results = new GameResultBus();
            results.Subscribe<OfficerCaptureStateResult>(_ => game.MoveNode(officer, planet));
            new MissionObserver(commands).Connect(results);

            results.Publish(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    ParentAtCapture = mission,
                    Context = planet,
                }
            );

            Assert.IsNull(mission.GetParent());
            Assert.AreSame(planet, officer.GetParent());
        }

        [Test]
        public void Constructor_CapturedMissionParticipant_DoesNotSubscribe()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            game.MoveNode(officer, mission);
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );
            GameResultBus results = new GameResultBus();
            MissionObserver observer = new MissionObserver(commands);

            results.Publish(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    Context = planet,
                }
            );

            Assert.AreSame(mission, officer.GetParent());
            observer.Dispose();
        }

        [Test]
        public void Connect_AlreadyConnected_ThrowsInvalidOperationException()
        {
            (GameRoot game, _, _, MovementCommands movement) = BuildScene(factionOwnsPlanet: true);
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );
            MissionObserver observer = new MissionObserver(commands);
            observer.Connect(new GameResultBus());

            Assert.Throws<InvalidOperationException>(() => observer.Connect(new GameResultBus()));
        }

        [Test]
        public void Dispose_CapturedMissionParticipant_StopsInterruption()
        {
            (GameRoot game, Planet planet, Officer officer, MovementCommands movement) = BuildScene(
                factionOwnsPlanet: true
            );
            StubMission mission = CreateMission(game, planet, officer);
            game.MoveNode(officer, mission);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "rebels";
            MissionCommands commands = TestSystems.CreateMissionCommands(
                game,
                new StubRNG(),
                movement
            );
            GameResultBus results = new GameResultBus();
            MissionObserver observer = new MissionObserver(commands);
            observer.Connect(results);
            observer.Dispose();

            results.Publish(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    Context = planet,
                }
            );

            Assert.AreSame(mission, officer.GetParent());
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="factionOwnsPlanet">Whether faction owns planet.</param>
        /// <returns>The constructed scene.</returns>
        private (
            GameRoot game,
            Planet planet,
            Officer officer,
            MovementCommands movement
        ) BuildScene(bool factionOwnsPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
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

            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
                MissionReturnParentInstanceID = planet.InstanceID,
                MissionReturnLocationInstanceID = planet.InstanceID,
            };
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
    }
}
