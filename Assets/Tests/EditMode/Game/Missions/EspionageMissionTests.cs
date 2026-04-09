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
    public class EspionageMissionTests
    {
        private static EspionageMission CreateMission(
            GameRoot game,
            string owner,
            Planet target,
            List<IMissionParticipant> main,
            List<IMissionParticipant> decoy,
            FogOfWarSystem fogOfWar
        )
        {
            MissionContext ctx = new MissionContext
            {
                Game = game,
                OwnerInstanceId = owner,
                Target = target,
                MainParticipants = main,
                DecoyParticipants = decoy,
                FogOfWar = fogOfWar,
            };
            return EspionageMission.TryCreate(ctx);
        }

        [Test]
        public void Execute_EnemyPlanetTarget_CapturesSnapshotForFaction()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");

            EspionageMission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                fog
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            MissionSceneBuilder.RunToSuccess(mission, game);

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            Assert.IsTrue(
                empire.Fog.Snapshots.ContainsKey("sys1"),
                "Espionage success should capture a FOW snapshot for the faction"
            );
        }

        [Test]
        public void Execute_NullFogOfWar_DoesNotThrow()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");

            EspionageMission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                fogOfWar: null
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            Assert.DoesNotThrow(() => MissionSceneBuilder.RunToSuccess(mission, game));
        }

        [Test]
        public void Execute_PlanetBecameOwnedBeforeExecution_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.VisitingFactionIDs.Add("empire");

            EspionageMission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                fog
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
                "Espionage should fail when target planet is now owned by mission owner"
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

            EspionageMission mission = CreateMission(
                game,
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                fog
            );

            Assert.IsNull(mission, "TryCreate should return null for an owned planet target");
        }

        [Test]
        public void SerializesAndDeserializes()
        {
            EspionageMission mission = new EspionageMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Espionage",
                DisplayName = "Espionage",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Espionage,
                HasInitiated = true,
                MaxProgress = 10,
                CurrentProgress = 5,
            };

            string xml = SerializationHelper.Serialize(mission);
            EspionageMission deserialized = SerializationHelper.Deserialize<EspionageMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Espionage", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Espionage, deserialized.ParticipantSkill);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(10, deserialized.MaxProgress);
            Assert.AreEqual(5, deserialized.CurrentProgress);
        }
    }
}
