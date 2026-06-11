using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

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
            List<IMissionParticipant> decoy
        )
        {
            MissionContext ctx = new MissionContext
            {
                Game = game,
                OwnerInstanceId = owner,
                Target = target,
                MainParticipants = main,
                DecoyParticipants = decoy,
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
                new List<IMissionParticipant>()
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
        public void Execute_WithoutFogSystem_DoesNotThrow()
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
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            Assert.DoesNotThrow(() => MissionSceneBuilder.RunToSuccess(mission, game));
        }

        [Test]
        public void Execute_PlanetBecameOwnedByMissionFaction_StillSucceeds()
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
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            // Planet changes hands before execution — espionage is still valid on any visited planet
            enemyPlanet.OwnerInstanceID = "empire";

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Success,
                completed.Outcome,
                "Espionage should still succeed when planet changed ownership before execution"
            );
        }

        [Test]
        public void TryCreate_NotVisitedPlanet_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            // empPlanet has no VisitingFactionIDs — empire has not visited it
            EspionageMission mission = CreateMission(
                game,
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission, "TryCreate should return null when planet has not been visited");
        }

        [Test]
        public void TryCreate_VisitedOwnPlanet_ReturnsNotNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.VisitingFactionIDs.Add("empire");

            EspionageMission mission = CreateMission(
                game,
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNotNull(
                mission,
                "TryCreate should succeed for a visited planet regardless of ownership"
            );
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            EspionageMission mission = new EspionageMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Espionage",
                DisplayName = "Espionage",
                TargetInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Espionage,
                HasInitiated = true,
                MaxProgress = 10,
                CurrentProgress = 5,
            };

            string xml = SerializationHelper.Serialize(mission);
            EspionageMission deserialized = SerializationHelper.Deserialize<EspionageMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Espionage", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(OfficerRating.Espionage, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(10, deserialized.MaxProgress);
            Assert.AreEqual(5, deserialized.CurrentProgress);
        }
    }
}
