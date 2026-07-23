using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class AbductionMissionTests
    {
        private static Mission CreateAbductionMission(
            GameRoot game,
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            Officer targetOfficer
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.Abduction,
                game,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                targetOfficer: targetOfficer
            );
        }

        private static void MakeAbductionAlwaysSucceed(GameRoot game)
        {
            game.Config.ProbabilityTables.Mission.Abduction = new Dictionary<int, int>
            {
                { 0, 100 },
            };
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { 0, 0 } };
        }

        [Test]
        public void Execute_TargetOnEnemyPlanet_SetsTargetCaptured()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            MakeAbductionAlwaysSucceed(game);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsTrue(
                target.IsCaptured,
                "Target officer should be marked captured on abduction success"
            );
        }

        [Test]
        public void UpdateMission_SuccessfulAbduction_MovesTargetToAbductorOrigin()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            MakeAbductionAlwaysSucceed(game);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            game.MoveNode(officer, mission);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            MovementSystem movement = new MovementSystem(game, fog, new FleetSystem(game));
            MissionSystem missionSystem = TestSystems.CreateMissionSystem(
                game,
                new FixedRNG(0.0),
                movement
            );

            missionSystem.UpdateMission(mission);

            Assert.IsTrue(target.IsCaptured, "Target officer should remain captured");
            Assert.AreEqual(
                "empire",
                target.CaptorInstanceID,
                "CaptorInstanceID should be the abducting faction"
            );
            Assert.AreEqual(
                empPlanet,
                target.GetParent(),
                "Abducted officer should return to the abductor's origin"
            );
        }

        [Test]
        public void GetSuccessfulReturnPassengers_TargetCapturedByOwner_ReturnsTarget()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            target.IsCaptured = true;
            target.CaptorInstanceID = "empire";

            List<IMovable> passengers = mission.GetSuccessfulReturnPassengers(game).ToList();

            CollectionAssert.AreEqual(new IMovable[] { target }, passengers);
        }

        [Test]
        public void GetSuccessfulReturnPassengers_TargetNotCapturedByOwner_ReturnsEmpty()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            target.IsCaptured = true;
            target.CaptorInstanceID = "rebels";

            List<IMovable> passengers = mission.GetSuccessfulReturnPassengers(game).ToList();

            Assert.IsEmpty(passengers);
        }

        [Test]
        public void UpdateMission_SuccessfulAbductionWithSpecialForces_MovesTargetToAbductorOrigin()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            SpecialForces commando = new SpecialForces
            {
                InstanceID = "sf1",
                DisplayName = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Abduction },
            };
            game.AttachNode(commando, empPlanet);
            commando.MissionReturnParentInstanceID = empPlanet.InstanceID;
            commando.MissionReturnLocationInstanceID = empPlanet.InstanceID;

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            MakeAbductionAlwaysSucceed(game);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { commando },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            game.MoveNode(commando, mission);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            MovementSystem movement = new MovementSystem(game, fog, new FleetSystem(game));
            MissionSystem missionSystem = TestSystems.CreateMissionSystem(
                game,
                new FixedRNG(0.0),
                movement
            );

            missionSystem.UpdateMission(mission);

            Assert.IsTrue(target.IsCaptured, "Target officer should remain captured");
            Assert.AreEqual(
                empPlanet,
                target.GetParent(),
                "Abducted officer should return with the special-forces escort"
            );
        }

        [Test]
        public void Execute_TargetOnEnemyPlanet_ReturnsCharacterCapturedResult()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            OfficerCaptureStateResult captured = results
                .OfType<OfficerCaptureStateResult>()
                .FirstOrDefault();
            Assert.IsNotNull(captured, "Should return OfficerCaptureStateResult on success");
            Assert.AreEqual("target", captured.TargetOfficer.InstanceID);
        }

        [Test]
        public void Execute_TargetAlreadyCaptured_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            // Target is captured after mission creation but before execution
            target.IsCaptured = true;

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target is already captured before execution"
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

            // A second enemy planet the target can legally move to
            Planet anotherEnemyPlanet = new Planet
            {
                InstanceID = "another_enemy",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(
                anotherEnemyPlanet,
                game.GetSceneNodeByInstanceID<PlanetSystem>("sys1")
            );

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            // Target moves to a different planet before mission executes
            game.MoveNode(target, anotherEnemyPlanet);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target officer has moved to a different planet before execution"
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

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            game.DetachNode(target);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target has left the scene before execution"
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

            Mission mission = CreateAbductionMission(
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

            Mission mission = CreateAbductionMission(
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
            Mission mission = CreateAbductionMission(
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
        public void TryCreate_FriendlyOfficerAsTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer friendly = EntityFactory.CreateOfficer("friendly", "empire");
            game.AttachNode(friendly, empPlanet);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                friendly
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when target belongs to the same faction"
            );
        }

        [Test]
        public void TryCreate_TargetAlreadyCaptured_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.IsCaptured = true;
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNull(mission, "TryCreate should return null when target is already captured");
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

            Officer target = EntityFactory.CreateOfficer("target", "rebels");

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
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

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAbductionMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNotNull(
                mission,
                "TryCreate should succeed with a valid enemy officer on the target planet"
            );
            Assert.AreEqual("target", ((AbductionMission)mission).TargetOfficerInstanceID);
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new AbductionMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Abduction",
                DisplayName = "Abduction",
                LocationInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Espionage,
                TargetOfficerInstanceID = "OFFICER2",
                HasInitiated = false,
                MaxProgress = 5,
                CurrentProgress = 0,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Abduction", deserialized.ConfigKey);
            Assert.AreEqual("OFFICER2", ((AbductionMission)deserialized).TargetOfficerInstanceID);
            Assert.AreEqual(OfficerRating.Espionage, deserialized.ParticipantRating);
            Assert.IsFalse(deserialized.HasInitiated);
            Assert.AreEqual(5, deserialized.MaxProgress);
        }
    }
}
