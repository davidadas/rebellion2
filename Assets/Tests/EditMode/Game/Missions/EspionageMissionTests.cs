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
        [Test]
        public void OnSuccess_CapturesSnapshotForFaction()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            EspionageMission mission = new EspionageMission(
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
        public void OnSuccess_NullFogOfWar_DoesNotThrow()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            EspionageMission mission = new EspionageMission(
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
        public void OnSuccess_PlanetBecameOwnedBeforeExecution_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            EspionageMission mission = new EspionageMission(
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
        public void Constructor_OwnPlanetTarget_Throws()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.Throws<System.InvalidOperationException>(() =>
                new EspionageMission(
                    "empire",
                    empPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    fog
                )
            );
        }

        [Test]
        public void SerializesAndDeserializes()
        {
            EspionageMission mission = new EspionageMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                Name = "Espionage",
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
            Assert.AreEqual("Espionage", deserialized.Name);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Espionage, deserialized.ParticipantSkill);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(10, deserialized.MaxProgress);
            Assert.AreEqual(5, deserialized.CurrentProgress);
        }
    }
}
