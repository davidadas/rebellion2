using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class ReconnaissanceMissionTests
    {
        private static ReconnaissanceMission CreateMission(
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
            return ReconnaissanceMission.TryCreate(ctx);
        }

        private static SpecialForces CreateReconTeam(string owner)
        {
            return new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = owner,
                AllowedMissionTypeIDs = new List<string> { ReconnaissanceMission.MissionTypeID },
            };
        }

        [Test]
        public void Execute_UnvisitedPlanet_MarksVisitedOnly()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            SpecialForces reconTeam = CreateReconTeam("empire");
            game.AttachNode(reconTeam, empPlanet);

            ReconnaissanceMission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { reconTeam },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsTrue(enemyPlanet.WasVisitedBy("empire"));

            Faction empire = game.GetFactionByOwnerInstanceID("empire");
            Assert.IsFalse(empire.Fog.Snapshots.ContainsKey("sys1"));
        }

        [Test]
        public void TryCreate_VisitedPlanet_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.AddVisitor("empire");
            SpecialForces reconTeam = CreateReconTeam("empire");
            game.AttachNode(reconTeam, empPlanet);

            ReconnaissanceMission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { reconTeam },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_OfficerParticipantOnly_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            ReconnaissanceMission mission = CreateMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            ReconnaissanceMission mission = new ReconnaissanceMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Reconnaissance",
                DisplayName = "Reconnaissance",
                TargetInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Espionage,
                HasInitiated = true,
                MaxProgress = 10,
                CurrentProgress = 5,
            };

            string xml = SerializationHelper.Serialize(mission);
            ReconnaissanceMission deserialized =
                SerializationHelper.Deserialize<ReconnaissanceMission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Reconnaissance", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.TargetInstanceID);
            Assert.AreEqual(OfficerRating.Espionage, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(10, deserialized.MaxProgress);
            Assert.AreEqual(5, deserialized.CurrentProgress);
        }
    }
}
