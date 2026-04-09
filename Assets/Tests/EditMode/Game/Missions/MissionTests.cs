using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
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
        public void Execute_MissionWithDecoy_UsesDecoyParticipantSkill()
        {
            // Decoy has Espionage=0, Combat=80. DecoyParticipantSkill=Combat.
            // DecoyProbabilityTable requires score >= 50 to pass.
            // FoilProbabilityTable is 100% so without a working decoy the mission is Foiled.
            // If DecoyParticipantSkill is respected (Combat=80 → score=80 → passes),
            // foil is nullified and the mission Succeeds.
            // If the field is ignored and Espionage=0 is used instead (score=0 → fails),
            // the mission is Foiled.
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer decoy = new Officer
            {
                InstanceID = "decoy",
                OwnerInstanceID = "empire",
                Skills = new System.Collections.Generic.Dictionary<MissionParticipantSkill, int>
                {
                    { MissionParticipantSkill.Espionage, 0 },
                    { MissionParticipantSkill.Combat, 80 },
                    { MissionParticipantSkill.Diplomacy, 0 },
                    { MissionParticipantSkill.Leadership, 0 },
                },
            };
            game.AttachNode(decoy, empPlanet);

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant> { decoy }
            );
            mission.SuccessProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 0, 100 } }
            );
            mission.DecoyParticipantSkill = MissionParticipantSkill.Combat;
            mission.DecoyProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 50, 100 } }
            );
            mission.FoilProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 0, 100 } }
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.01));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Success,
                completed.Outcome,
                "Decoy should use DecoyParticipantSkill (Combat=80) not always Espionage (Espionage=0)"
            );
        }

        [Test]
        public void IsCanceled_MainParticipantCaptured_ReturnsTrue()
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
                mission.IsCanceled(game),
                "Mission should be canceled when main participant is captured"
            );
        }

        [Test]
        public void IsCanceled_MainParticipantKilled_ReturnsTrue()
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
                mission.IsCanceled(game),
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
        public void Execute_EnemyRegimentsPresent_CanBeFoiledByEnemyForces()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "rebels",
                DefenseRating = 100,
            };
            game.AttachNode(regiment, enemyPlanet);

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            mission.SuccessProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 0, 100 } }
            );
            mission.FoilProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 0, 100 } }
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.5));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Foiled,
                completed.Outcome,
                "Mission should be foiled when foil probability is 100 and roll succeeds"
            );
        }

        [Test]
        public void Execute_MissionWithDecoy_DecoyNullifiesFoil()
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

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "rebels",
                DefenseRating = 100,
            };
            game.AttachNode(regiment, enemyPlanet);

            SabotageMission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant> { decoy }
            );
            mission.SuccessProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 0, 100 } }
            );
            mission.FoilProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 0, 100 } }
            );
            mission.DecoyProbabilityTable = new ProbabilityTable(
                new System.Collections.Generic.Dictionary<int, int> { { 0, 100 } }
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(new StubRNG());

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Success,
                completed.Outcome,
                "Successful decoy should nullify foil and allow mission to succeed"
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

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

            int skillBefore = officer.GetSkillValue(MissionParticipantSkill.Combat);

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
            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(
                skillBefore + 1,
                officer.GetSkillValue(MissionParticipantSkill.Combat),
                "Officer skill should improve by 1 on mission success"
            );
        }

        [Test]
        public void Execute_Success_GrowsForceOnMissionOfficer()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            officer.GrowsForceOnMission = true;
            officer.IsForceEligible = true;
            officer.ForceValue = 10;
            int before = officer.ForceValue;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

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
            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(
                before + game.Config.Jedi.ForceGrowthPerMission,
                officer.ForceValue,
                "Officer with GrowsForceOnMission should gain ForceValue on success"
            );
        }

        [Test]
        public void Execute_Success_DoesNotGrowForce_WhenFlagFalse()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            officer.GrowsForceOnMission = false;
            officer.IsForceEligible = true;
            officer.ForceValue = 10;
            int before = officer.ForceValue;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

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
            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(
                before,
                officer.ForceValue,
                "Officer without GrowsForceOnMission should not gain ForceValue"
            );
        }

        [Test]
        public void Execute_Success_DoesNotGrowForce_WhenNotForceEligible()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            officer.GrowsForceOnMission = true;
            officer.IsForceEligible = false;
            officer.ForceValue = 10;
            int before = officer.ForceValue;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(building, enemyPlanet);

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
            mission.Execute(game, new FixedRNG(0.0));

            Assert.AreEqual(
                before,
                officer.ForceValue,
                "Officer not force-eligible should not gain ForceValue even with GrowsForceOnMission"
            );
        }

        [Test]
        public void Execute_Failure_DoesNotGrowForce()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            officer.GrowsForceOnMission = true;
            officer.IsForceEligible = true;
            officer.ForceValue = 10;
            int before = officer.ForceValue;

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
            mission.Execute(game, new FixedRNG(0.99));

            Assert.AreEqual(
                before,
                officer.ForceValue,
                "Force growth should not apply on mission failure"
            );
        }

        [Test]
        public void IsCanceled_DecoyParticipantCaptured_ReturnsFalse()
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
                mission.IsCanceled(game),
                "Mission should not be canceled when only decoy participant is captured"
            );
        }
    }
}
