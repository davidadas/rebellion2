using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;

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

        private static RecruitmentMission CreateRecruitmentMission(
            GameRoot game,
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            MissionContext ctx = new MissionContext
            {
                Game = game,
                OwnerInstanceId = ownerInstanceId,
                Target = target,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                RandomProvider = new StubRNG(),
            };
            return RecruitmentMission.TryCreate(ctx);
        }

        private RecruitmentMission CreateMission(GameRoot game, Planet planet, Officer participant)
        {
            RecruitmentMission mission = CreateRecruitmentMission(
                game,
                "empire",
                planet,
                new List<IMissionParticipant> { participant },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);
            mission.Initiate(new StubRNG());
            return mission;
        }

        [Test]
        public void Execute_TargetInUnrecruitedPool_TransfersOfficerToFaction()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            RecruitmentMission mission = CreateMission(game, empPlanet, officer);
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

            RecruitmentMission mission = CreateMission(game, empPlanet, officer);
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

            RecruitmentMission mission = CreateMission(game, empPlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsFalse(game.UnrecruitedOfficers.Contains(target));
        }

        [Test]
        public void Execute_TargetAlreadyJoinedFaction_ReturnsFailed()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            RecruitmentMission mission = CreateMission(game, empPlanet, officer);

            // Target joins before mission executes
            target.OwnerInstanceID = "empire";

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
        }

        [Test]
        public void Execute_TargetRemovedFromPool_ReturnsFailed()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            RecruitmentMission mission = CreateMission(game, empPlanet, officer);

            // Officer leaves the unrecruited pool before the mission executes
            game.UnrecruitedOfficers.Remove(target);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
        }

        [Test]
        public void CanContinue_UnrecruitedOfficersAvailable_ReturnsTrue()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            RecruitmentMission mission = CreateMission(game, empPlanet, officer);

            Assert.IsTrue(mission.CanContinue(game));
        }

        [Test]
        public void CanContinue_NoUnrecruitedOfficersAvailable_ReturnsFalse()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            // Add a target so TryCreate succeeds
            Officer target = EntityFactory.CreateOfficer("temp", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            RecruitmentMission mission = CreateMission(game, empPlanet, officer);

            // Remove all unrecruited officers after mission creation
            game.UnrecruitedOfficers.Clear();

            Assert.IsFalse(mission.CanContinue(game));
        }

        [Test]
        public void GetFoilProbability_AnyInput_ReturnsZero()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            RecruitmentMission mission = CreateMission(game, empPlanet, officer);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            // Even a perfect defense score should not foil a recruitment mission
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreNotEqual(MissionOutcome.Foiled, completed.Outcome);
        }

        [Test]
        public void TryCreate_NonMainParticipant_ThrowsArgumentException()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();
            officer.IsMain = false;

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            Assert.Throws<ArgumentException>(() =>
                CreateRecruitmentMission(
                    game,
                    "empire",
                    empPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                )
            );
        }

        [Test]
        public void TryCreate_NoValidTarget_ReturnsNull()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            // No unrecruited officers — TryCreate has no valid target
            RecruitmentMission mission = CreateRecruitmentMission(
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
            RecruitmentMission mission = new RecruitmentMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Recruitment",
                DisplayName = "Recruitment",
                TargetInstanceID = "PLANET1",
                ParticipantSkill = MissionParticipantSkill.Diplomacy,
                TargetOfficerInstanceID = "OFFICER4",
            };

            string xml = SerializationHelper.Serialize(mission);
            RecruitmentMission deserialized = SerializationHelper.Deserialize<RecruitmentMission>(
                xml
            );

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Recruitment", deserialized.ConfigKey);
            Assert.AreEqual("OFFICER4", deserialized.TargetOfficerInstanceID);
            Assert.AreEqual(MissionParticipantSkill.Diplomacy, deserialized.ParticipantSkill);
        }
    }
}
