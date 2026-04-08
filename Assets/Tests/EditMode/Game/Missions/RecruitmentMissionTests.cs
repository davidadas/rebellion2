using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;

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

        private RecruitmentMission CreateMission(
            GameRoot game,
            Planet planet,
            Officer participant,
            string targetOfficerInstanceId
        )
        {
            RecruitmentMission mission = new RecruitmentMission(
                "empire",
                planet,
                new List<IMissionParticipant> { participant },
                new List<IMissionParticipant>(),
                targetOfficerInstanceId
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

            RecruitmentMission mission = CreateMission(game, empPlanet, officer, "target");
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

            RecruitmentMission mission = CreateMission(game, empPlanet, officer, "target");
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

            RecruitmentMission mission = CreateMission(game, empPlanet, officer, "target");
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

            RecruitmentMission mission = CreateMission(game, empPlanet, officer, "target");

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

            RecruitmentMission mission = CreateMission(game, empPlanet, officer, "target");

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

            RecruitmentMission mission = CreateMission(game, empPlanet, officer, "target");

            Assert.IsTrue(mission.CanContinue(game));
        }

        [Test]
        public void CanContinue_NoUnrecruitedOfficersAvailable_ReturnsFalse()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            RecruitmentMission mission = CreateMission(game, empPlanet, officer, "target");

            Assert.IsFalse(mission.CanContinue(game));
        }

        [Test]
        public void GetFoilProbability_AlwaysReturnsZero()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.AllowedOwnerInstanceIDs = new List<string> { "empire" };
            game.UnrecruitedOfficers.Add(target);

            RecruitmentMission mission = CreateMission(game, empPlanet, officer, "target");

            while (!mission.IsComplete())
                mission.IncrementProgress();

            // Even a perfect defense score should not foil a recruitment mission
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreNotEqual(MissionOutcome.Foiled, completed.Outcome);
        }

        [Test]
        public void Constructor_NonMainParticipant_ThrowsArgumentException()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();
            officer.IsMain = false;

            Assert.Throws<ArgumentException>(() =>
                new RecruitmentMission(
                    "empire",
                    empPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    "target"
                )
            );
        }

        [Test]
        public void Constructor_EmptyTargetOfficerInstanceId_ThrowsArgumentNullException()
        {
            (GameRoot game, Planet empPlanet, Officer officer) = BuildScene();

            Assert.Throws<ArgumentNullException>(() =>
                new RecruitmentMission(
                    "empire",
                    empPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>(),
                    ""
                )
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
