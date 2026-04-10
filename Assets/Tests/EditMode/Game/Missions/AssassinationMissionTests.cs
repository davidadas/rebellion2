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
    public class AssassinationMissionTests
    {
        private static AssassinationMission CreateAssassinationMission(
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
            return AssassinationMission.TryCreate(ctx);
        }

        [Test]
        public void Execute_TargetOnEnemyPlanet_SetsTargetKilled()
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

            AssassinationMission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsTrue(
                target.IsKilled,
                "Target officer should be marked killed on assassination success"
            );
        }

        [Test]
        public void Execute_TargetOnEnemyPlanet_ReturnsCharacterKilledResult()
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

            AssassinationMission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            OfficerKilledResult killed = results.OfType<OfficerKilledResult>().FirstOrDefault();
            Assert.IsNotNull(killed, "Should return OfficerKilledResult on success");
            Assert.AreEqual("target", killed.TargetOfficer.InstanceID);
        }

        [Test]
        public void Execute_TargetOnEnemyPlanet_SetsAssassinOnResult()
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

            AssassinationMission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            OfficerKilledResult killed = results.OfType<OfficerKilledResult>().First();
            Assert.AreEqual(
                officer.InstanceID,
                killed.Assassin.InstanceID,
                "Assassin should be the main participant"
            );
        }

        [Test]
        public void Execute_TargetOnEnemyPlanet_DetachesTargetFromSceneGraph()
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

            AssassinationMission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsNull(target.GetParent(), "Killed officer should be detached from scene graph");
        }

        [Test]
        public void Execute_TargetAlreadyKilled_ReturnsFailed()
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

            AssassinationMission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            // Target is killed after mission creation but before execution
            target.IsKilled = true;

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target is already killed before execution"
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

            AssassinationMission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

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

            AssassinationMission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

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

            AssassinationMission mission = CreateAssassinationMission(
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

            AssassinationMission mission = CreateAssassinationMission(
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

            // No target officer provided — TryCreate should return null
            AssassinationMission mission = CreateAssassinationMission(
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
        public void SerializesAndDeserializes()
        {
            AssassinationMission mission = new AssassinationMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Assassination",
                DisplayName = "Assassination",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Combat,
                TargetOfficerInstanceID = "OFFICER1",
                HasInitiated = true,
                MaxProgress = 2,
                CurrentProgress = 1,
            };

            string xml = SerializationHelper.Serialize(mission);
            AssassinationMission deserialized =
                SerializationHelper.Deserialize<AssassinationMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Assassination", deserialized.ConfigKey);
            Assert.AreEqual("OFFICER1", deserialized.TargetOfficerInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Combat, deserialized.ParticipantSkill);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(2, deserialized.MaxProgress);
            Assert.AreEqual(1, deserialized.CurrentProgress);
        }
    }
}
