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
        /// <summary>
        /// Verifies roll participant success garrisoned regiment does not affect score.
        /// </summary>
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

        /// <summary>
        /// Verifies try create planet already in uprising returns null.
        /// </summary>
        [Test]
        public void TryCreate_PlanetAlreadyInUprising_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.BeginUprising();

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                )
            );
        }

        /// <summary>
        /// Verifies should repeat after completion neutral planet without friendly troops returns false.
        /// </summary>
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

        /// <summary>
        /// Verifies should repeat after completion enemy controls planet returns true.
        /// </summary>
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

        /// <summary>
        /// Verifies should repeat after completion neutral planet with friendly troops returns true.
        /// </summary>
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

        /// <summary>
        /// Verifies try create owned planet target returns null.
        /// </summary>
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

        /// <summary>
        /// Verifies try create neutral planet target returns null.
        /// </summary>
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

        /// <summary>
        /// Verifies display name is human readable.
        /// </summary>
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

        /// <summary>
        /// Verifies get abort reason uprising already started does not abort.
        /// </summary>
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

        /// <summary>
        /// Verifies serialize round trip preserves data.
        /// </summary>
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

        /// <summary>
        /// Creates incite uprising mission.
        /// </summary>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="target">The target.</param>
        /// <param name="mainParticipants">The main participants.</param>
        /// <param name="decoyParticipants">The decoy participants.</param>
        /// <returns>The created incite uprising mission.</returns>
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
