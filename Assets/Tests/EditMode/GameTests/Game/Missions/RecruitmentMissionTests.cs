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
    public class RecruitmentMissionTests
    {
        [Test]
        public void ResolveObjective_AvailableCandidate_TransfersOfficerToFaction()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);

            Mission mission = CreateMission(game, empirePlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual("empire", target.OwnerInstanceID);
        }

        [Test]
        public void ResolveObjective_AvailableCandidate_AttachesOfficerToPlanet()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);

            Mission mission = CreateMission(game, empirePlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual(empirePlanet, target.GetParent());
        }

        [Test]
        public void ResolveObjective_AvailableCandidate_RemovesOfficerFromUnrecruitedPool()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);

            Mission mission = CreateMission(game, empirePlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.IsFalse(game.GetUnrecruitedOfficers().Contains(target));
        }

        [Test]
        public void ResolveObjective_CreatedBeforePoolChanges_RecruitsCurrentAvailableOfficer()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            Officer removedTarget = EntityFactory.CreateOfficer("target", "rebels");
            removedTarget.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(removedTarget);

            Mission mission = CreateMission(game, empirePlanet, officer);

            game.GetUnrecruitedOfficers().Remove(removedTarget);
            Officer replacementTarget = EntityFactory.CreateOfficer("replacement", "rebels");
            replacementTarget.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(replacementTarget);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
            Assert.AreEqual("empire", replacementTarget.OwnerInstanceID);
            Assert.AreEqual(
                "replacement",
                ((RecruitmentMission)mission).RecruitedOfficerInstanceID
            );
        }

        [Test]
        public void UpdateMission_NoCandidatesRemain_DoesNotRollOrImproveRecruiter()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);

            Mission mission = CreateMission(game, empirePlanet, officer);
            int originalLeadership = officer.GetBaseRating(OfficerRating.Leadership);

            // The candidate pool empties before the mission executes.
            game.GetUnrecruitedOfficers().Remove(target);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog, new FleetSystem(game));
            MissionSystem missionSystem = TestSystems.CreateMissionSystem(
                game,
                new ThrowingRNG(),
                movement
            );

            List<GameResult> results = missionSystem.UpdateMission(mission);

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.IsFalse(completed.CanContinue);
            Assert.AreEqual(originalLeadership, officer.GetBaseRating(OfficerRating.Leadership));
        }

        [Test]
        public void ResolveObjective_SuccessProbability_UsesOpposingSupportAndLeadershipRating()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);
            officer.SetBaseRating(OfficerRating.Leadership, 40);

            Mission mission = CreateMission(game, empirePlanet, officer);
            game.Config.ProbabilityTables.Mission.Recruitment = new Dictionary<int, int>
            {
                { -40, 0 },
                { 20, 100 },
                { 21, 0 },
            };

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.99));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Success, completed.Outcome);
        }

        [Test]
        public void ResolveObjective_SecondSuccess_SelectsNextOfficerFromCurrentPool()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            Officer firstTarget = EntityFactory.CreateOfficer("first", "rebels");
            firstTarget.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            Officer secondTarget = EntityFactory.CreateOfficer("second", "rebels");
            secondTarget.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(firstTarget);
            game.GetUnrecruitedOfficers().Add(secondTarget);

            Mission mission = CreateMission(game, empirePlanet, officer);
            MissionSceneBuilder.RunToSuccess(mission, game);

            mission.Initiate(0);
            MissionSceneBuilder.RunToSuccess(mission, game);

            Assert.AreEqual("empire", firstTarget.OwnerInstanceID);
            Assert.AreEqual("empire", secondTarget.OwnerInstanceID);
            Assert.IsFalse(game.GetUnrecruitedOfficers().Contains(firstTarget));
            Assert.IsFalse(game.GetUnrecruitedOfficers().Contains(secondTarget));
            Assert.AreEqual("second", ((RecruitmentMission)mission).RecruitedOfficerInstanceID);
        }

        [Test]
        public void ResolveObjective_MultipleRecruitersSucceed_FirstSuccessStopsFurtherAttempts()
        {
            (GameRoot game, Planet empirePlanet, Officer firstRecruiter) = BuildScene();
            Officer secondRecruiter = EntityFactory.CreateOfficer("second-recruiter", "empire");
            secondRecruiter.IsMain = true;
            firstRecruiter.SetBaseRating(OfficerRating.Leadership, 100);
            secondRecruiter.SetBaseRating(OfficerRating.Leadership, 100);
            int firstRating = firstRecruiter.GetBaseRating(OfficerRating.Leadership);
            int secondRating = secondRecruiter.GetBaseRating(OfficerRating.Leadership);

            Officer firstTarget = EntityFactory.CreateOfficer("first-target", "rebels");
            firstTarget.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            Officer secondTarget = EntityFactory.CreateOfficer("second-target", "rebels");
            secondTarget.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(firstTarget);
            game.GetUnrecruitedOfficers().Add(secondTarget);
            game.Config.ProbabilityTables.Mission.Recruitment = new Dictionary<int, int>
            {
                { 0, 100 },
                { 100, 100 },
            };

            Mission mission = CreateRecruitmentMission(
                game,
                "empire",
                empirePlanet,
                new List<IMissionParticipant> { firstRecruiter, secondRecruiter },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, empirePlanet);
            mission.Initiate(0);
            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.ResolveObjective(
                game,
                new SequenceRNG(intValues: new[] { 0 }, doubleValues: new[] { 0.0, 0.0 })
            );

            Assert.AreEqual(
                "first-target",
                ((RecruitmentMission)mission).RecruitedOfficerInstanceID
            );
            Assert.AreEqual("empire", firstTarget.OwnerInstanceID);
            Assert.IsTrue(game.GetUnrecruitedOfficers().Contains(secondTarget));
            Assert.AreEqual(
                firstRating + 1,
                firstRecruiter.GetBaseRating(OfficerRating.Leadership)
            );
            Assert.AreEqual(secondRating, secondRecruiter.GetBaseRating(OfficerRating.Leadership));
            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void ShouldRepeatAfterCompletion_UnrecruitedOfficersAvailable_ReturnsTrue()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);

            Mission mission = CreateMission(game, empirePlanet, officer);

            Assert.IsTrue(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void ShouldRepeatAfterCompletion_NoUnrecruitedOfficersAvailable_ReturnsFalse()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            // Add a target so TryCreate succeeds
            Officer target = EntityFactory.CreateOfficer("temp", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);

            Mission mission = CreateMission(game, empirePlanet, officer);

            // Remove all unrecruited officers after mission creation
            game.GetUnrecruitedOfficers().Clear();

            Assert.IsFalse(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void TryCreate_NonMainParticipant_ReturnsNull()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();
            officer.IsMain = false;

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);

            Mission mission = CreateRecruitmentMission(
                game,
                "empire",
                empirePlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_MixedMainAndNonMainParticipants_ReturnsNull()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();
            Officer secondOfficer = EntityFactory.CreateOfficer("second", "empire");
            secondOfficer.IsMain = false;
            game.AttachNode(secondOfficer, empirePlanet);

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.RecruitingFactionInstanceIDs = new List<string> { "empire" };
            game.GetUnrecruitedOfficers().Add(target);

            Mission mission = CreateRecruitmentMission(
                game,
                "empire",
                empirePlanet,
                new List<IMissionParticipant> { officer, secondOfficer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_NoAvailableCandidates_ReturnsNull()
        {
            (GameRoot game, Planet empirePlanet, Officer officer) = BuildScene();

            // No unrecruited officers are available at the target planet.
            Mission mission = CreateRecruitmentMission(
                game,
                "empire",
                empirePlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when no recruitment candidates exist"
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
                LocationInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Diplomacy,
                RecruitedOfficerInstanceID = "OFFICER4",
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Recruitment", deserialized.ConfigKey);
            Assert.AreEqual(
                "OFFICER4",
                ((RecruitmentMission)deserialized).RecruitedOfficerInstanceID
            );
            Assert.AreEqual(OfficerRating.Diplomacy, deserialized.ParticipantRating);
        }

        private (GameRoot game, Planet empirePlanet, Officer officer) BuildScene()
        {
            (GameRoot game, Planet empirePlanet, Planet _, Officer officer, FogOfWarSystem _) =
                MissionSceneBuilder.Build();
            officer.IsMain = true;
            return (game, empirePlanet, officer);
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
    }
}
