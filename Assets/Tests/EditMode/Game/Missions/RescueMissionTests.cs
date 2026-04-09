using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class RescueMissionTests
    {
        [Test]
        public void Execute_CapturedOfficerOnTargetPlanet_FreesOfficer()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            game.AttachNode(captive, enemyPlanet);

            RescueMission mission = new RescueMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "captive"
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsFalse(captive.IsCaptured, "Rescued officer should no longer be captured");
        }

        [Test]
        public void Execute_CapturedOfficerOnTargetPlanet_ReturnsOfficerRescuedResult()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            game.AttachNode(captive, enemyPlanet);

            RescueMission mission = new RescueMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "captive"
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            OfficerRescuedResult rescueResult = results
                .OfType<OfficerRescuedResult>()
                .FirstOrDefault();
            Assert.IsNotNull(rescueResult, "Should return OfficerRescuedResult on success");
            Assert.AreEqual("captive", rescueResult.Officer.InstanceID);
        }

        [Test]
        public void Execute_CapturedOfficerOnTargetPlanet_EmitsCaptureStateReleased()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            game.AttachNode(captive, enemyPlanet);

            RescueMission mission = new RescueMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "captive"
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            OfficerCaptureStateResult captureState = results
                .OfType<OfficerCaptureStateResult>()
                .FirstOrDefault();
            Assert.IsNotNull(captureState, "Should emit OfficerCaptureStateResult on rescue");
            Assert.AreEqual("captive", captureState.TargetOfficer.InstanceID);
            Assert.IsFalse(captureState.IsCaptured, "IsCaptured should be false on release");
        }

        [Test]
        public void Execute_OfficerNotCaptured_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "empire");
            target.IsCaptured = false;
            game.AttachNode(target, empPlanet);

            RescueMission mission = new RescueMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "target"
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target officer is not captured"
            );
        }

        [Test]
        public void Execute_TargetOfficerAlreadyFreed_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            game.AttachNode(captive, enemyPlanet);

            RescueMission mission = new RescueMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "captive"
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            // Captive is freed before mission executes
            captive.IsCaptured = false;

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target officer was freed before execution"
            );
        }

        [Test]
        public void Execute_TargetMovedToDifferentPlanet_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            game.AttachNode(captive, enemyPlanet);

            RescueMission mission = new RescueMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "captive"
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            // Captive is moved to a different planet before mission executes
            game.MoveNode(captive, empPlanet);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when captive has been moved to a different planet before execution"
            );
        }

        [Test]
        public void Execute_TargetRemovedFromScene_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            game.AttachNode(captive, enemyPlanet);

            RescueMission mission = new RescueMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "captive"
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            // Captive removed from scene before mission executes
            game.DetachNode(captive);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target officer has left the scene before execution"
            );
        }

        [Test]
        public void Constructor_NullTarget_ThrowsArgumentException()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.Throws<System.ArgumentNullException>(() =>
                new RescueMission(
                    "empire",
                    null,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    "captive"
                )
            );
        }

        [Test]
        public void Constructor_NonPlanetTarget_ThrowsInvalidOperationException()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.Throws<System.InvalidOperationException>(() =>
                new RescueMission(
                    "empire",
                    officer,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    "captive"
                )
            );
        }

        [Test]
        public void Constructor_NullTargetOfficerInstanceId_ThrowsArgumentException()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.Throws<System.ArgumentNullException>(() =>
                new RescueMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    null
                )
            );
        }

        [Test]
        public void SerializesAndDeserializes()
        {
            RescueMission mission = new RescueMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Rescue",
                DisplayName = "Rescue",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Espionage,
                TargetOfficerInstanceID = "OFFICER3",
                HasInitiated = true,
                MaxProgress = 8,
                CurrentProgress = 8,
            };

            string xml = SerializationHelper.Serialize(mission);
            RescueMission deserialized = SerializationHelper.Deserialize<RescueMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Rescue", deserialized.ConfigKey);
            Assert.AreEqual("OFFICER3", deserialized.TargetOfficerInstanceID);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(8, deserialized.MaxProgress);
            Assert.AreEqual(8, deserialized.CurrentProgress);
        }
    }
}
