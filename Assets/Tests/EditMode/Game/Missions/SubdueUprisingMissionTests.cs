using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class SubdueUprisingMissionTests
    {
        private Mission CreateSubdueUprisingMission(
            string ownerInstanceId,
            Planet target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.SubdueUprising,
                null,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants
            );
        }

        private static MissionSystem CreateMissionSystem(
            GameRoot game,
            FogOfWarSystem fog,
            FixedRNG rng
        )
        {
            FleetSystem fleet = new FleetSystem(game);
            MovementSystem movement = new MovementSystem(game, fog, fleet);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game, fleet, movement);
            PlanetaryControlSystem control = new PlanetaryControlSystem(
                game,
                movement,
                manufacturing,
                fog
            );
            UprisingSystem uprising = new UprisingSystem(game, rng, control);
            return new MissionSystem(game, rng, movement, uprising);
        }

        [Test]
        public void RollParticipantSuccess_ResistanceRegimentRaisesScoreByOne()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { 30, 0 },
                { 31, 100 },
            };

            Mission mission = CreateSubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);

            Assert.IsFalse(mission.RollParticipantSuccess(officer, new FixedRNG(0), game));

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            regiment.TypeID = game.Config.Uprising.ResistanceRegimentTypeID;
            game.AttachNode(regiment, empPlanet);

            Assert.IsTrue(mission.RollParticipantSuccess(officer, new FixedRNG(0), game));
        }

        [Test]
        public void RollParticipantSuccess_IncompleteResistanceRegimentDoesNotRaiseScore()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { 30, 0 },
                { 31, 100 },
            };

            Mission mission = CreateSubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Building;
            regiment.TypeID = game.Config.Uprising.ResistanceRegimentTypeID;
            game.AttachNode(regiment, empPlanet);

            Assert.IsFalse(mission.RollParticipantSuccess(officer, new FixedRNG(0), game));
        }

        [Test]
        public void DisplayName_IsHumanReadable()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();

            Mission mission = CreateSubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);

            Assert.AreEqual("Subdue Uprising", mission.DisplayName);
        }

        [Test]
        public void GetAbortReason_UprisingEndedBeforeExecution_ReturnsFailure()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();

            Mission mission = CreateSubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);

            empPlanet.EndUprising();

            Assert.AreEqual(
                MissionCompletionReason.Failure,
                mission.GetAbortReason(game),
                "Mission should be canceled when uprising ends before mission executes"
            );
        }

        [Test]
        public void UpdateMission_SuccessfulRollWithInsufficientGarrison_LeavesUprisingAndFails()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            empPlanet.GetParentOfType<PlanetSystem>().SystemType = PlanetSystemType.OuterRim;
            empPlanet.SetPopularSupport("empire", 10);
            empPlanet.SetPopularSupport("rebels", 90);
            empPlanet.BeginUprising();
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -200, 100 },
            };
            game.Config.Uprising.SubdueOwnedSupportBase = 1;
            game.Config.Uprising.SubdueOwnedSupportRange = 0;

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, empPlanet);

            Mission mission = CreateSubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);
            int leadershipBefore = officer.GetBaseRating(OfficerRating.Leadership);

            List<GameResult> results = CreateMissionSystem(game, fog, new FixedRNG(0))
                .UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.IsTrue(empPlanet.IsInUprising);
            Assert.AreEqual(11, empPlanet.GetPopularSupport("empire"));
            Assert.IsEmpty(results.OfType<PlanetUprisingEndedResult>());
            Assert.AreEqual(leadershipBefore, officer.GetBaseRating(OfficerRating.Leadership));
        }

        [Test]
        public void UpdateMission_FirstSuccessfulParticipantEndsAttemptSequence()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            empPlanet.GetParentOfType<PlanetSystem>().SystemType = PlanetSystemType.OuterRim;
            empPlanet.SetPopularSupport("empire", 10);
            empPlanet.SetPopularSupport("rebels", 90);
            empPlanet.BeginUprising();
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -200, 100 },
            };
            game.Config.Uprising.SubdueOwnedSupportBase = 1;
            game.Config.Uprising.SubdueOwnedSupportRange = 0;

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, empPlanet);

            Officer secondOfficer = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(secondOfficer, empPlanet);
            Mission mission = CreateSubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer, secondOfficer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);

            List<GameResult> results = CreateMissionSystem(game, fog, new FixedRNG(0))
                .UpdateMission(mission);

            Assert.AreEqual(
                MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreEqual(11, empPlanet.GetPopularSupport("empire"));
        }

        [Test]
        public void UpdateMission_SuccessfulRollWithSufficientGarrison_EndsUprisingAndImprovesAgent()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            empPlanet.GetParentOfType<PlanetSystem>().SystemType = PlanetSystemType.OuterRim;
            empPlanet.SetPopularSupport("empire", 10);
            empPlanet.SetPopularSupport("rebels", 90);
            empPlanet.BeginUprising();
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -200, 100 },
            };
            game.Config.Uprising.SubdueOwnedSupportBase = 1;
            game.Config.Uprising.SubdueOwnedSupportRange = 0;

            for (int i = 0; i < 10; i++)
            {
                Regiment regiment = EntityFactory.CreateRegiment($"r{i}", "empire");
                regiment.ManufacturingStatus = ManufacturingStatus.Complete;
                game.AttachNode(regiment, empPlanet);
            }

            Mission mission = CreateSubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);
            int leadershipBefore = officer.GetBaseRating(OfficerRating.Leadership);

            List<GameResult> results = CreateMissionSystem(game, fog, new FixedRNG(0))
                .UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
            Assert.IsFalse(empPlanet.IsInUprising);
            Assert.AreEqual(1, results.OfType<PlanetUprisingEndedResult>().Count());
            Assert.AreEqual(leadershipBefore + 1, officer.GetBaseRating(OfficerRating.Leadership));
        }

        [Test]
        public void RollParticipantSuccess_NonResistanceDefenseRatingDoesNotAffectScore()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            empPlanet.BeginUprising();
            game.Config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { 30, 100 },
            };

            Mission mission = CreateSubdueUprisingMission(
                "empire",
                empPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empPlanet);
            mission.Initiate(0);

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.TypeID = "non-resistance";
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            regiment.DefenseRating = 500;
            game.AttachNode(regiment, empPlanet);

            Assert.IsTrue(mission.RollParticipantSuccess(officer, new FixedRNG(0), game));
        }

        [Test]
        public void TryCreate_PlanetNotInUprising_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Assert.IsNull(
                CreateSubdueUprisingMission(
                    "empire",
                    empPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is not in uprising"
            );
        }

        [Test]
        public void TryCreate_EnemyOwnedPlanet_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empPlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.BeginUprising();

            Assert.IsNull(
                CreateSubdueUprisingMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is owned by another faction"
            );
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new SubdueUprisingMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "SubdueUprising",
                DisplayName = "Subdue Uprising",
                LocationInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Diplomacy,
                HasInitiated = true,
                MaxProgress = 3,
                CurrentProgress = 2,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("SubdueUprising", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.LocationInstanceID);
            Assert.AreEqual(OfficerRating.Diplomacy, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(3, deserialized.MaxProgress);
            Assert.AreEqual(2, deserialized.CurrentProgress);
        }
    }
}
