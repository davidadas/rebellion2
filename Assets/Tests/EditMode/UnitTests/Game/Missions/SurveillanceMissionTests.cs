using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public sealed class SurveillanceMissionTests
    {
        private GameRoot _game;
        private Planet _home;
        private Planet _target;
        private Officer _officer;
        private MissionCommands _commands;

        /// <summary>
        /// Creates an intelligence team and configures deterministic espionage attempts.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            (_game, _home, _target, _officer, _) = MissionSceneBuilder.Build();
            _home.AddVisitor("empire");
            _target.AddVisitor("empire");
            _game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { 0, 100 },
            };
            _game.Config.ProbabilityTables.Mission.TickRanges.Espionage.Base = 2;
            _game.Config.ProbabilityTables.Mission.TickRanges.Espionage.Spread = 0;
            _game.Config.Espionage.CoreSectorBonus = new GameConfig.RandomCountConfig();
            _commands = CreateCommands(_game);
        }

        /// <summary>
        /// Offers the continuous mission to special forces with the existing espionage capability.
        /// </summary>
        [Test]
        public void GetAvailableMissionOptions_EspionageSpecialForces_IncludesSurveillance()
        {
            SpecialForces spy = new SpecialForces
            {
                InstanceID = "spy",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { EspionageMission.MissionTypeID },
            };
            _game.AttachNode(spy, _home);

            MissionOption option = new MissionFactory(_game)
                .GetAvailableMissionOptions(CreateContext(_target, spy))
                .Single(candidate => candidate.MissionTypeID == SurveillanceMission.MissionTypeID);

            Assert.AreEqual("Surveillance", option.DisplayName);
            Assert.AreEqual(SkillRating.Espionage, option.ParticipantRating);
            Assert.AreEqual(SkillRating.Espionage, option.DecoyParticipantRating);
        }

        /// <summary>
        /// Applies explicit surveillance restrictions and the underlying espionage restriction.
        /// </summary>
        /// <param name="disallowedType">The mission capability prohibited by the faction.</param>
        [TestCase(EspionageMission.MissionTypeID)]
        [TestCase(SurveillanceMission.MissionTypeID)]
        public void TryCreateMission_DisallowedMissionCapability_RejectsSurveillance(
            string disallowedType
        )
        {
            _game
                .GetFactionByOwnerInstanceID("empire")
                .DisallowedMissionTypeIDs.Add(disallowedType);

            Assert.IsFalse(
                new MissionFactory(_game).TryCreateMission(CreateContext(_target, _officer), out _)
            );
        }

        /// <summary>
        /// Rejects a team whose special forces cannot perform espionage.
        /// </summary>
        [Test]
        public void TryCreateMission_SpecialForcesWithoutEspionage_RejectsSurveillance()
        {
            SpecialForces unit = new SpecialForces
            {
                InstanceID = "sabotage-team",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { SabotageMission.MissionTypeID },
            };
            _game.AttachNode(unit, _home);

            Assert.IsFalse(
                new MissionFactory(_game).TryCreateMission(CreateContext(_target, unit), out _)
            );
        }

        /// <summary>
        /// Rolls the first surveillance attempt using the existing espionage duration.
        /// </summary>
        [Test]
        public void InitiateMission_Surveillance_UsesEspionageDurationAndDistinctIdentity()
        {
            Assert.IsTrue(_commands.InitiateMission(CreateContext(_home, _officer)));
            SurveillanceMission mission = _game.GetSceneNodesByType<SurveillanceMission>().Single();

            Assert.AreEqual(2, mission.MaxProgress);
            Assert.AreEqual(SurveillanceMission.MissionTypeID, mission.TypeID);
            Assert.AreEqual(SurveillanceMission.MissionTypeID, mission.ConfigKey);
            Assert.AreEqual("Surveillance", mission.DisplayName);
        }

        /// <summary>
        /// Refreshes changing intelligence on successive attempts without releasing the team.
        /// </summary>
        [Test]
        public void ProcessTick_RepeatedSuccess_RefreshesIntelligenceAndKeepsTeamAssigned()
        {
            SurveillanceMission mission = CreateActiveMission();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                string buildingID = "building-" + cycle;
                _game.AttachNode(
                    new Building { InstanceID = buildingID, OwnerInstanceID = "rebels" },
                    _target
                );
                List<GameResult> results = new List<GameResult>();
                for (int tick = 0; tick < 2; tick++)
                {
                    _game.CurrentTick++;
                    results.AddRange(_commands.ProcessMissionTick(_game));
                }

                MissionCompletedResult completed = results
                    .OfType<MissionCompletedResult>()
                    .Single();
                PlanetSnapshot snapshot = _game
                    .GetFactionByOwnerInstanceID("empire")
                    .Fog.Snapshots["sector1"]
                    .Planets[_target.InstanceID];
                Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
                Assert.IsTrue(completed.CanContinue);
                Assert.AreEqual(SurveillanceMission.MissionTypeID, completed.MissionTypeID);
                Assert.AreEqual(_game.CurrentTick, snapshot.TickCaptured);
                Assert.IsTrue(
                    snapshot.Buildings.Any(building => building.InstanceID == buildingID)
                );
                Assert.AreSame(_target, mission.GetParent());
                Assert.AreSame(mission, _officer.GetParent());
                Assert.AreEqual(0, mission.CurrentProgress);
                Assert.AreEqual(2, mission.MaxProgress);
                Assert.IsTrue(mission.DetectionResolved);
                Assert.IsFalse(mission.PreObjectiveEncounterResolved);
            }
        }

        /// <summary>
        /// Retries unsuccessful objective rolls without creating a new mission or returning agents.
        /// </summary>
        [Test]
        public void ProcessTick_FailedObjective_StartsAnotherAttempt()
        {
            _game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { 0, 0 },
            };
            SurveillanceMission mission = CreateActiveMission();
            _commands.ProcessMissionTick(_game);

            MissionCompletedResult completed = _commands
                .ProcessMissionTick(_game)
                .OfType<MissionCompletedResult>()
                .Single();

            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.IsTrue(completed.CanContinue);
            Assert.AreSame(_target, mission.GetParent());
            Assert.AreSame(mission, _officer.GetParent());
            Assert.AreEqual(0, mission.CurrentProgress);
            Assert.AreEqual(2, mission.MaxProgress);
        }

        /// <summary>
        /// Uses the existing recall operation to stop the mission and release its participants.
        /// </summary>
        [Test]
        public void AbortMission_ActiveSurveillance_DetachesMissionAndReleasesTeam()
        {
            SurveillanceMission mission = CreateActiveMission();

            Assert.IsTrue(_commands.AbortMission(mission.InstanceID));

            Assert.IsNull(mission.GetParent());
            Assert.AreNotSame(mission, _officer.GetParent());
            Assert.IsFalse(_officer.IsOnMission());
            Assert.IsEmpty(_commands.ProcessMissionTick(_game).OfType<MissionCompletedResult>());
        }

        /// <summary>
        /// Terminates surveillance when its assigned main participant leaves the mission.
        /// </summary>
        [Test]
        public void ProcessTick_MainParticipantRemoved_StopsSurveillance()
        {
            SurveillanceMission mission = CreateActiveMission();
            _game.MoveNode(_officer, _home);

            MissionCompletedResult completed = _commands
                .ProcessMissionTick(_game)
                .OfType<MissionCompletedResult>()
                .Single();

            Assert.IsFalse(completed.CanContinue);
            Assert.IsNull(mission.GetParent());
        }

        /// <summary>
        /// Restores the concrete mission, participants, and partial progress before continuing.
        /// </summary>
        [Test]
        public void Serialize_ActiveSurveillance_ResumesRepeatingAfterRoundTrip()
        {
            SurveillanceMission mission = CreateActiveMission();
            _commands.ProcessMissionTick(_game);
            string xml = SerializationHelper.Serialize(_game);

            GameRoot loaded = SerializationHelper.Deserialize<GameRoot>(xml);
            loaded.SetConfig(_game.Config);
            SurveillanceMission restored = loaded
                .GetSceneNodesByType<SurveillanceMission>()
                .Single();
            Assert.AreEqual(mission.InstanceID, restored.InstanceID);
            Assert.AreEqual(SurveillanceMission.MissionTypeID, restored.TypeID);
            Assert.AreEqual(1, restored.CurrentProgress);
            Assert.AreSame(restored, restored.GetMainParticipants().Single().GetParent());

            MissionCompletedResult completed = CreateCommands(loaded)
                .ProcessMissionTick(loaded)
                .OfType<MissionCompletedResult>()
                .Single();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
            Assert.IsTrue(completed.CanContinue);
            Assert.AreEqual(0, restored.CurrentProgress);
            Assert.AreEqual(2, restored.MaxProgress);
        }

        /// <summary>
        /// Preserves surveillance identity and participant roles when copying observed missions.
        /// </summary>
        [Test]
        public void CreateCopy_ActiveSurveillance_PreservesConcreteTypeAndTeam()
        {
            SurveillanceMission mission = CreateActiveMission();

            SurveillanceMission copy = (SurveillanceMission)mission.CreateCopy(recursive: true);

            Assert.AreEqual(SurveillanceMission.MissionTypeID, copy.TypeID);
            Assert.AreEqual(SurveillanceMission.MissionTypeID, copy.ConfigKey);
            Assert.AreEqual(
                _officer.InstanceID,
                copy.GetMainParticipants().Single().GetInstanceID()
            );
            Assert.AreSame(copy, copy.GetMainParticipants().Single().GetParent());
            Assert.IsTrue(copy.ShouldRepeatAfterCompletion(_game));
        }

        /// <summary>
        /// Creates a context for the selected planet and participant.
        /// </summary>
        /// <param name="planet">The target planet.</param>
        /// <param name="participant">The main participant.</param>
        /// <returns>The surveillance assignment request.</returns>
        private MissionContext CreateContext(Planet planet, IMissionParticipant participant)
        {
            return new MissionContext
            {
                Game = _game,
                MissionTypeID = SurveillanceMission.MissionTypeID,
                OwnerInstanceId = "empire",
                Location = planet,
                MainParticipants = new List<IMissionParticipant> { participant },
            };
        }

        /// <summary>
        /// Places the team on station after its initial arrival encounter.
        /// </summary>
        /// <returns>The active surveillance mission.</returns>
        private SurveillanceMission CreateActiveMission()
        {
            Assert.IsTrue(
                new MissionFactory(_game).TryCreateMission(
                    CreateContext(_target, _officer),
                    out Mission mission
                )
            );
            _game.AttachNode(mission, _target);
            _game.MoveNode(_officer, mission);
            mission.Initiate(2);
            mission.DetectionResolved = true;
            return (SurveillanceMission)mission;
        }

        /// <summary>
        /// Creates the normal mission runtime for a live or restored game.
        /// </summary>
        /// <param name="game">The game to advance.</param>
        /// <returns>The mission lifecycle commands.</returns>
        private static MissionCommands CreateCommands(GameRoot game)
        {
            FogOfWarCommands fog = new FogOfWarCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                fog,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            return TestSystems.CreateMissionCommands(game, new FixedRNG(0.01), movement);
        }
    }
}
