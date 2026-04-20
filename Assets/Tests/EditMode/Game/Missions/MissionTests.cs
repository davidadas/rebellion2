using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class MissionTests
    {
        private static SabotageMission CreateSabotageMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            MissionContext ctx = new MissionContext
            {
                OwnerInstanceId = ownerInstanceId,
                Target = target,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
            };
            return SabotageMission.TryCreate(ctx);
        }

        [Test]
        public void ShouldAbort_MainParticipantCaptured_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            officer.IsCaptured = true;

            Assert.IsTrue(
                mission.ShouldAbort(game),
                "Mission should be canceled when main participant is captured"
            );
        }

        [Test]
        public void ShouldAbort_MainParticipantKilled_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            officer.IsKilled = true;

            Assert.IsTrue(
                mission.ShouldAbort(game),
                "Mission should be canceled when main participant is killed"
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

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

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

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.99));

            Assert.IsTrue(
                results.OfType<MissionCompletedResult>().Any(),
                "Execute should always include MissionCompletedResult even on failure"
            );
        }

        [Test]
        public void Execute_SuccessfulMission_ImprovesMissionSkill()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            int skillBefore = officer.GetSkillValue(MissionParticipantSkill.Leadership);

            MissionContext ctx = new MissionContext
            {
                OwnerInstanceId = "empire",
                Target = enemyPlanet,
                MainParticipants = new List<IMissionParticipant> { officer },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            InciteUprisingMission mission = InciteUprisingMission.TryCreate(ctx);
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(
                skillBefore + 1,
                officer.GetSkillValue(MissionParticipantSkill.Leadership),
                "Officer leadership skill should improve by 1 on mission success"
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

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
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

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
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
        public void ShouldAbort_DecoyParticipantCaptured_ReturnsFalse()
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

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant> { decoy }
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            decoy.IsCaptured = true;

            Assert.IsFalse(
                mission.ShouldAbort(game),
                "Mission should not be canceled when only decoy participant is captured"
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

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            mission.SuccessProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 0, 0 } }
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

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
    }
}
