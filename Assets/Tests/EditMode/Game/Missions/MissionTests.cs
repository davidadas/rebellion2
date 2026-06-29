using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class MissionTests
    {
        private static Mission CreateSabotageMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode specificTarget = null
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.Sabotage,
                null,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                specificTarget
            );
        }

        private static Regiment CreateSabotageTarget(GameRoot game, Planet planet)
        {
            Regiment target = EntityFactory.CreateRegiment("sabotage-target", "rebels");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, planet);
            return target;
        }

        [Test]
        public void GetAbortReason_MainParticipantRemoved_ReturnsFailure()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                building
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            mission.RemoveChild(officer);

            Assert.AreEqual(
                MissionCompletionReason.Failure,
                mission.GetAbortReason(game),
                "Mission should be canceled when main participant is removed"
            );
        }

        [Test]
        public void GetAbortReason_MainParticipantUnchanged_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                building
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            Assert.IsNull(
                mission.GetAbortReason(game),
                "Mission should not abort when participant membership is unchanged"
            );
        }

        [Test]
        public void Execute_SuccessOutcome_AlwaysIncludesMissionCompletedResult()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = CreateSabotageTarget(game, enemyPlanet);

            Mission mission = CreateSabotageMission(
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

            Assert.IsTrue(
                results.OfType<MissionCompletedResult>().Any(),
                "Execute should always include MissionCompletedResult"
            );
        }

        [Test]
        public void Execute_FailOutcome_AlwaysIncludesMissionCompletedResult()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = CreateSabotageTarget(game, enemyPlanet);

            Mission mission = CreateSabotageMission(
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
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            Assert.IsTrue(
                results.OfType<MissionCompletedResult>().Any(),
                "Execute should always include MissionCompletedResult even on failure"
            );
        }

        [Test]
        public void Execute_SuccessfulMission_ImprovesMissionRating()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            int ratingBefore = officer.GetBaseRating(OfficerRating.Leadership);

            Mission mission = MissionTestFactory.TryCreate(
                MissionTypeIDs.InciteUprising,
                null,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(
                ratingBefore + 1,
                officer.GetBaseRating(OfficerRating.Leadership),
                "Officer leadership rating should improve by 1 on mission success"
            );
        }

        [Test]
        public void CanAcceptChild_WithMissionParticipant_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = CreateSabotageTarget(game, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);

            Officer other = EntityFactory.CreateOfficer("o2", "empire");

            Assert.IsTrue(mission.CanAcceptChild(other));
        }

        [Test]
        public void CanAcceptChild_NonParticipant_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = CreateSabotageTarget(game, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
            };

            Assert.IsFalse(mission.CanAcceptChild(building));
        }

        [Test]
        public void GetAbortReason_DecoyParticipantRemoved_ReturnsFailure()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            game.AttachNode(decoy, empPlanet);
            Regiment target = CreateSabotageTarget(game, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant> { decoy },
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            mission.RemoveChild(decoy);

            Assert.AreEqual(
                MissionCompletionReason.Failure,
                mission.GetAbortReason(game),
                "Mission should be canceled when any participant is removed"
            );
        }

        [Test]
        public void Execute_FailedSuccessRoll_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = CreateSabotageTarget(game, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.Config.ProbabilityTables.Mission.Sabotage = new Dictionary<int, int> { { 0, 0 } };
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Execute should only return Success or Failed, never Foiled"
            );
        }

        [Test]
        public void Serialize_RoundTrip_RestoresDecoyParticipantRating()
        {
            Mission mission = new SabotageMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = MissionTypeIDs.Sabotage,
                DisplayName = MissionTypeIDs.Sabotage,
                TargetInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Combat,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual(OfficerRating.Espionage, deserialized.DecoyParticipantRating);
        }

        [Test]
        public void Serialize_RoundTripActiveMission_PreservesParticipantSceneGraph()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Officer decoy = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(decoy, empPlanet);
            Regiment target = CreateSabotageTarget(game, enemyPlanet);

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant> { decoy },
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(3);

            MovementSystem movement = new MovementSystem(game, fog);
            movement.RequestMove(officer, mission);
            movement.RequestMove(decoy, mission);

            string xml = SerializationHelper.Serialize(game);
            GameRoot deserialized = SerializationHelper.Deserialize<GameRoot>(xml);

            Mission loadedMission = deserialized.GetSceneNodesByType<Mission>().Single();
            Officer loadedOfficer = deserialized.GetSceneNodeByInstanceID<Officer>("o1");
            Officer loadedDecoy = deserialized.GetSceneNodeByInstanceID<Officer>("o2");

            Assert.AreEqual(loadedMission, loadedOfficer.GetParent());
            Assert.AreEqual(loadedMission, loadedDecoy.GetParent());
            Assert.AreEqual(loadedOfficer, loadedMission.MainParticipants.Single());
            Assert.AreEqual(loadedDecoy, loadedMission.DecoyParticipants.Single());
        }
    }
}
