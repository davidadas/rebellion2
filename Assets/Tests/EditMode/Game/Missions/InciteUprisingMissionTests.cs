using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class InciteUprisingMissionTests
    {
        private InciteUprisingMission CreateInciteUprisingMission(
            string ownerInstanceId,
            Planet target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            MissionContext ctx = new MissionContext
            {
                OwnerInstanceId = ownerInstanceId,
                Target = target,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
            };
            return InciteUprisingMission.TryCreate(ctx);
        }

        [Test]
        public void Execute_EnemyPlanetTarget_StartsUprising()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            InciteUprisingMission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsTrue(
                enemyPlanet.IsInUprising,
                "InciteUprising success should start uprising on planet"
            );
        }

        [Test]
        public void Execute_EnemyPlanetTarget_ReturnsPlanetUprisingStartedResult()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            InciteUprisingMission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            Assert.IsTrue(
                results.OfType<PlanetUprisingStartedResult>().Any(),
                "Should return PlanetUprisingStartedResult on success"
            );
        }

        [Test]
        public void TryCreate_PlanetAlreadyInUprising_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.BeginUprising();

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is already in uprising"
            );
        }

        [Test]
        public void TryCreate_OwnedPlanetTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    empPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is owned by the mission owner"
            );
        }

        [Test]
        public void TryCreate_NeutralPlanetTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.OwnerInstanceID = null;

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is neutral (no owner to revolt against)"
            );
        }

        [Test]
        public void Execute_SuccessfulMission_CompletedResultHasHumanReadableName()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            InciteUprisingMission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                "Incite Uprising",
                completed.MissionName,
                "MissionName in result should be the human-readable display name"
            );
        }

        [Test]
        public void ShouldAbort_UprisingAlreadyStarted_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            InciteUprisingMission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            enemyPlanet.BeginUprising();

            Assert.IsTrue(
                mission.ShouldAbort(game),
                "Mission should be canceled when planet is already in uprising"
            );
        }

        [Test]
        public void Execute_UprisingAlreadyStarted_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            InciteUprisingMission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            enemyPlanet.BeginUprising();

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when uprising already started before execution"
            );
        }

        [Test]
        public void Execute_PlanetBecameNeutralBeforeExecution_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            InciteUprisingMission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            enemyPlanet.OwnerInstanceID = null;

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target planet became neutral before execution"
            );
        }

        [Test]
        public void Execute_PlanetTakenByMissionOwnerBeforeExecution_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            InciteUprisingMission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            enemyPlanet.OwnerInstanceID = "empire";

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target planet was captured by the mission owner before execution"
            );
        }

        [Test]
        public void GetAgentProbability_HighEnemyStrength_FailsAtHighRoll()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "rebels",
                DefenseRating = 50,
            };
            game.AttachNode(regiment, enemyPlanet);

            InciteUprisingMission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            mission.SuccessProbabilityTable = new ProbabilityTable(
                new Dictionary<int, int> { { -200, 1 }, { 100, 99 } }
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            Assert.IsFalse(
                results.OfType<PlanetUprisingStartedResult>().Any(),
                "High enemy regiment strength should reduce incite probability enough to fail at 99% roll"
            );
        }

        [Test]
        public void SerializesAndDeserializes()
        {
            InciteUprisingMission mission = new InciteUprisingMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "InciteUprising",
                DisplayName = "Incite Uprising",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Diplomacy,
                HasInitiated = false,
                MaxProgress = 20,
                CurrentProgress = 0,
            };

            string xml = SerializationHelper.Serialize(mission);
            InciteUprisingMission deserialized =
                SerializationHelper.Deserialize<InciteUprisingMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("InciteUprising", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Diplomacy, deserialized.ParticipantSkill);
            Assert.IsFalse(deserialized.HasInitiated);
            Assert.AreEqual(20, deserialized.MaxProgress);
        }
    }
}
