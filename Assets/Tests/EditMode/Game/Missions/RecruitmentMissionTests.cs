using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class RecruitmentMissionTests
    {
        private (GameRoot game, Planet empPlanet, Officer officer) BuildScene()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet _,
                Officer officer,
                Rebellion.Systems.FogOfWarSystem _
            ) = MissionSceneBuilder.Build();
            officer.IsMain = true;
            return (game, empPlanet, officer);
        }

        private static Mission CreateRecruitmentMission(
            GameRoot game,
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.Recruitment,
                game,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants
            );
        }

        private Mission CreateMission(GameRoot game, Planet planet, Officer participant)
        {
            Mission mission = CreateRecruitmentMission(
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { participant },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            mission.Initiate(0);
            return mission;
        }

        [Test]
        public void Execute_TargetInUnrecruitedPool_TransfersOfficerToFaction()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Mission mission = CreateMission(game, empPlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual("empire", target.OwnerInstanceID);
        }

        [Test]
        public void Execute_TargetInUnrecruitedPool_AttachesOfficerToPlanet()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Mission mission = CreateMission(game, empPlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(empPlanet, target.GetParent());
        }

        [Test]
        public void Execute_TargetInUnrecruitedPool_RemovesOfficerFromUnrecruitedPool()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Mission mission = CreateMission(game, empPlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsFalse(game.UnrecruitedOfficers.Contains(target));
        }

        [Test]
        public void Execute_CreatedBeforePoolChanges_RecruitsCurrentAvailableOfficer()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer removedTarget = EntityFactory.CreateOfficer("target", "rebels");
            removedTarget.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(removedTarget);

            Mission mission = CreateMission(game, empPlanet, officer);

            game.UnrecruitedOfficers.Remove(removedTarget);
            Officer replacementTarget = EntityFactory.CreateOfficer("replacement", "rebels");
            replacementTarget.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(replacementTarget);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
            Assert.AreEqual("empire", replacementTarget.OwnerInstanceID);
            Assert.AreEqual("replacement", ((RecruitmentMission)mission).TargetOfficerInstanceID);
        }

        [Test]
        public void Execute_TargetRemovedFromPool_ReturnsFailed()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Mission mission = CreateMission(game, empPlanet, officer);

            // Officer leaves the unrecruited pool before the mission executes
            game.UnrecruitedOfficers.Remove(target);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
        }

        [Test]
        public void Execute_SuccessProbability_UsesOpposingSupportAndLeadershipRating()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);
            officer.SetBaseRating(OfficerRating.Leadership, 40);

            Mission mission = CreateMission(game, empPlanet, officer);
            game.Config.ProbabilityTables.Mission.Recruitment = new Dictionary<int, int>
            {
                { -40, 0 },
                { 20, 100 },
                { 21, 0 },
            };

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
        }

        [Test]
        public void ShouldRepeatAfterCompletion_UnrecruitedOfficersAvailable_ReturnsTrue()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Mission mission = CreateMission(game, empPlanet, officer);

            Assert.IsTrue(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void Execute_SecondSuccess_SelectsNextOfficerFromCurrentPool()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer firstTarget = EntityFactory.CreateOfficer("first", "rebels");
            firstTarget.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            Officer secondTarget = EntityFactory.CreateOfficer("second", "rebels");
            secondTarget.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(firstTarget);
            game.UnrecruitedOfficers.Add(secondTarget);

            Mission mission = CreateMission(game, empPlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            mission.Initiate(0);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual("empire", firstTarget.OwnerInstanceID);
            Assert.AreEqual("empire", secondTarget.OwnerInstanceID);
            Assert.IsFalse(game.UnrecruitedOfficers.Contains(firstTarget));
            Assert.IsFalse(game.UnrecruitedOfficers.Contains(secondTarget));
            Assert.AreEqual("second", ((RecruitmentMission)mission).TargetOfficerInstanceID);
        }

        [Test]
        public void ShouldRepeatAfterCompletion_NoUnrecruitedOfficersAvailable_ReturnsFalse()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            // Add a target so TryCreate succeeds
            Officer target = EntityFactory.CreateOfficer("temp", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Mission mission = CreateMission(game, empPlanet, officer);

            // Remove all unrecruited officers after mission creation
            game.UnrecruitedOfficers.Clear();

            Assert.IsFalse(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void GetFoilProbability_AnyInput_ReturnsZero()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Mission mission = CreateMission(game, empPlanet, officer);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            // Even a perfect defense score should not foil a recruitment mission
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreNotEqual(MissionOutcome.Foiled, completed.Outcome);
        }

        [Test]
        public void TryCreate_NonMainParticipant_ReturnsNull()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();
            officer.IsMain = false;

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Mission mission = CreateRecruitmentMission(
                game,
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_NoValidTarget_ReturnsNull()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            // No unrecruited officers — TryCreate has no valid target
            Mission mission = CreateRecruitmentMission(
                game,
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when no unrecruited officers exist"
            );
        }

        [Test]
        public void SerializeAndDeserialize_PopulatedMission_RetainsAllProperties()
        {
            Mission mission = new RecruitmentMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Recruitment",
                DisplayName = "Recruitment",
                TargetInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Diplomacy,
                TargetOfficerInstanceID = "OFFICER4",
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Recruitment", deserialized.ConfigKey);
            Assert.AreEqual("OFFICER4", ((RecruitmentMission)deserialized).TargetOfficerInstanceID);
            Assert.AreEqual(OfficerRating.Diplomacy, deserialized.ParticipantRating);
        }
    }
}
