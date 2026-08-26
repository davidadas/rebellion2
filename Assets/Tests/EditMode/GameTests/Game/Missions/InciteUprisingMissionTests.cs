using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class InciteUprisingMissionTests
    {
        [Test]
        public void RollParticipantSuccess_GarrisonedRegimentDoesNotAffectScore()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.SetPopularSupport("empire", 40);
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                DefenseRating = 500,
            };
            game.AttachNode(regiment, enemyPlanet);

            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -11, 0 },
                { -10, 100 },
            };
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            Assert.IsTrue(mission.RollParticipantSuccess(officer, new FixedRNG(0), game));
        }

        [Test]
        public void TryCreate_PlanetAlreadyInUprising_ReturnsMission()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.BeginUprising();

            Assert.IsNotNull(
                CreateInciteUprisingMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                )
            );
        }

        [Test]
        public void ShouldRepeatAfterCompletion_NeutralPlanetWithoutFriendlyTroops_ReturnsFalse()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            enemyPlanet.OwnerInstanceID = null;

            Assert.IsFalse(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void ShouldRepeatAfterCompletion_EnemyControlsPlanet_ReturnsTrue()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);

            Assert.IsTrue(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void ShouldRepeatAfterCompletion_NeutralPlanetWithFriendlyTroops_ReturnsTrue()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            enemyPlanet.OwnerInstanceID = "empire";
            Regiment regiment = EntityFactory.CreateRegiment("friendly-regiment", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, enemyPlanet);
            enemyPlanet.OwnerInstanceID = "rebels";
            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            enemyPlanet.OwnerInstanceID = null;

            Assert.IsTrue(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void TryCreate_OwnedPlanetTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    empirePlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is owned by the mission owner"
            );
        }

        [Test]
        public void TryCreate_NeutralPlanetTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.OwnerInstanceID = null;

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is neutral (no owner to revolt against)"
            );
        }

        [Test]
        public void DisplayName_IsHumanReadable()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            Assert.AreEqual("Incite Uprising", mission.DisplayName);
        }

        [Test]
        public void GetAbortReason_UprisingAlreadyStarted_DoesNotAbort()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            enemyPlanet.BeginUprising();

            Assert.IsNull(mission.GetAbortReason(game));
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new InciteUprisingMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "InciteUprising",
                DisplayName = "Incite Uprising",
                LocationInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Diplomacy,
                HasInitiated = false,
                MaxProgress = 20,
                CurrentProgress = 0,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("InciteUprising", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.LocationInstanceID);
            Assert.AreEqual(OfficerRating.Diplomacy, deserialized.ParticipantRating);
            Assert.IsFalse(deserialized.HasInitiated);
            Assert.AreEqual(20, deserialized.MaxProgress);
        }

        private Mission CreateInciteUprisingMission(
            string ownerInstanceId,
            Planet target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.InciteUprising,
                null,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants
            );
        }
    }
}
