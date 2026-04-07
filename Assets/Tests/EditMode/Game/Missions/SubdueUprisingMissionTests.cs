using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class SubdueUprisingMissionTests
    {
        [Test]
        public void Execute_ActiveUprising_EndsUprising()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();

            SubdueUprisingMission mission = new SubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(new StubRNG());

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsFalse(
                empPlanet.IsInUprising,
                "SubdueUprising success should end uprising on planet"
            );
        }

        [Test]
        public void Execute_ActiveUprising_ReturnsPlanetUprisingEndedResult()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();

            SubdueUprisingMission mission = new SubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            Assert.IsTrue(
                results.OfType<PlanetUprisingEndedResult>().Any(),
                "Should return PlanetUprisingEndedResult on success"
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

            empPlanet.BeginUprising();

            SubdueUprisingMission mission = new SubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                "Subdue Uprising",
                completed.MissionName,
                "MissionName in result should be the human-readable display name"
            );
        }

        [Test]
        public void IsCanceled_UprisingEndedBeforeExecution_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();

            SubdueUprisingMission mission = new SubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(new StubRNG());

            empPlanet.EndUprising();

            Assert.IsTrue(
                mission.IsCanceled(game),
                "Mission should be canceled when uprising ends before mission executes"
            );
        }

        [Test]
        public void Execute_UprisingAlreadyEnded_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();

            SubdueUprisingMission mission = new SubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(new StubRNG());

            empPlanet.EndUprising();

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when uprising ended before execution"
            );
        }

        [Test]
        public void Constructor_PlanetNotInUprising_ThrowsInvalidOperationException()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.Throws<System.InvalidOperationException>(
                () =>
                    new SubdueUprisingMission(
                        "empire",
                        empPlanet,
                        new List<IMissionParticipant> { officer },
                        new List<IMissionParticipant>()
                    ),
                "Constructor should throw when target planet is not in uprising"
            );
        }

        [Test]
        public void Constructor_EnemyOwnedPlanet_ThrowsInvalidOperationException()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.BeginUprising();

            Assert.Throws<System.InvalidOperationException>(
                () =>
                    new SubdueUprisingMission(
                        "empire",
                        enemyPlanet,
                        new List<IMissionParticipant> { officer },
                        new List<IMissionParticipant>()
                    ),
                "Constructor should throw when target planet is owned by another faction"
            );
        }

        [Test]
        public void SerializesAndDeserializes()
        {
            SubdueUprisingMission mission = new SubdueUprisingMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "SubdueUprising",
                DisplayName = "Subdue Uprising",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Diplomacy,
                HasInitiated = true,
                MaxProgress = 3,
                CurrentProgress = 2,
            };

            string xml = SerializationHelper.Serialize(mission);
            SubdueUprisingMission deserialized =
                SerializationHelper.Deserialize<SubdueUprisingMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("SubdueUprising", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Diplomacy, deserialized.ParticipantSkill);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(3, deserialized.MaxProgress);
            Assert.AreEqual(2, deserialized.CurrentProgress);
        }
    }
}
