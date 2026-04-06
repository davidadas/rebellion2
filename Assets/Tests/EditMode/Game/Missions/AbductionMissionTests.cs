using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class AbductionMissionTests
    {
        [Test]
        public void OnSuccess_SetsTargetCaptured()
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

            AbductionMission mission = new AbductionMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "target"
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsTrue(
                target.IsCaptured,
                "Target officer should be marked captured on abduction success"
            );
        }

        [Test]
        public void OnSuccess_ReturnsCharacterCapturedResult()
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

            AbductionMission mission = new AbductionMission(
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

            CharacterCapturedResult captured = results
                .OfType<CharacterCapturedResult>()
                .FirstOrDefault();
            Assert.IsNotNull(captured, "Should return CharacterCapturedResult on success");
            Assert.AreEqual("target", captured.OfficerInstanceID);
        }

        [Test]
        public void OnSuccess_TargetAlreadyCaptured_ReturnsFailed()
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

            AbductionMission mission = new AbductionMission(
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
                "Mission should fail when target is already captured before execution"
            );
        }

        [Test]
        public void OnSuccess_TargetMovedToDifferentPlanet_ReturnsFailed()
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

            AbductionMission mission = new AbductionMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "target"
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
        public void OnSuccess_TargetRemovedFromScene_ReturnsFailed()
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

            AbductionMission mission = new AbductionMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                "target"
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
        public void Constructor_NullTarget_Throws()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.Throws<System.ArgumentNullException>(() =>
                new AbductionMission(
                    "empire",
                    null,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    "target"
                )
            );
        }

        [Test]
        public void Constructor_NonPlanetTarget_Throws()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.Throws<System.InvalidOperationException>(() =>
                new AbductionMission(
                    "empire",
                    officer,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    "target"
                )
            );
        }

        [Test]
        public void SerializesAndDeserializes()
        {
            AbductionMission mission = new AbductionMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                Name = "Abduction",
                DisplayName = "Abduction",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Espionage,
                TargetOfficerInstanceID = "OFFICER2",
                HasInitiated = false,
                MaxProgress = 5,
                CurrentProgress = 0,
            };

            string xml = SerializationHelper.Serialize(mission);
            AbductionMission deserialized = SerializationHelper.Deserialize<AbductionMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Abduction", deserialized.Name);
            Assert.AreEqual("OFFICER2", deserialized.TargetOfficerInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Espionage, deserialized.ParticipantSkill);
            Assert.IsFalse(deserialized.HasInitiated);
            Assert.AreEqual(5, deserialized.MaxProgress);
        }
    }
}
