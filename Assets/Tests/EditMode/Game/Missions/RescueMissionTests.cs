using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class RescueMissionTests
    {
        private static RescueMission CreateRescueMission(
            GameRoot game,
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            Officer targetOfficer
        )
        {
            MissionContext ctx = new MissionContext
            {
                Game = game,
                OwnerInstanceId = ownerInstanceId,
                Target = target,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                TargetOfficer = targetOfficer,
            };
            return RescueMission.TryCreate(ctx);
        }

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

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
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

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
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

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
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

            // Set up a captured officer so TryCreate succeeds
            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            game.AttachNode(captive, enemyPlanet);

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            // Officer is freed after mission creation but before execution
            captive.IsCaptured = false;

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

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
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

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
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

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
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
        public void TryCreate_NullTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                null,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                null
            );

            Assert.IsNull(mission, "TryCreate should return null when target is null");
        }

        [Test]
        public void TryCreate_NonPlanetTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                officer,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                null
            );

            Assert.IsNull(mission, "TryCreate should return null when target is not a Planet");
        }

        [Test]
        public void TryCreate_NoValidTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                null
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when no valid target officers exist"
            );
        }

        [Test]
        public void TryCreate_EnemyOfficerAsTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer enemy = EntityFactory.CreateOfficer("enemy", "rebels");
            enemy.IsCaptured = true;
            game.AttachNode(enemy, enemyPlanet);

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                enemy
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when target belongs to an enemy faction"
            );
        }

        [Test]
        public void TryCreate_TargetNotCaptured_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = false;

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
            );

            Assert.IsNull(mission, "TryCreate should return null when target is not captured");
        }

        [Test]
        public void TryCreate_TargetOnWrongPlanet_ReturnsNull()
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
            game.AttachNode(captive, empPlanet);

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when target is not on the mission target planet"
            );
        }

        [Test]
        public void TryCreate_ValidTarget_ReturnsNotNull()
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

            RescueMission mission = CreateRescueMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                captive
            );

            Assert.IsNotNull(
                mission,
                "TryCreate should succeed with a valid captured friendly officer on the target planet"
            );
            Assert.AreEqual("captive", mission.TargetOfficerInstanceID);
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
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
