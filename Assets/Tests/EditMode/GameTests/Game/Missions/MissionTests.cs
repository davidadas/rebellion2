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
        [Test]
        public void GetChildren_ParticipantAssignedBeforeMissionInitiates_ReturnsParticipant()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
            game.MoveNode(officer, mission);

            Assert.IsFalse(mission.HasInitiated);
            CollectionAssert.AreEqual(new[] { officer }, mission.GetChildren().ToArray());

            Mission copy = (Mission)mission.CreateCopy(recursive: true);
            IMissionParticipant copiedParticipant = copy.GetMainParticipants().Single();

            Assert.AreEqual(copy, copiedParticipant.GetParent());
        }

        [Test]
        public void Constructor_ParticipantListsChangedByCaller_PreservesMissionAssignments()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            Regiment target = CreateSabotageTarget(game, enemyPlanet);
            List<IMissionParticipant> mainParticipants = new List<IMissionParticipant> { officer };
            List<IMissionParticipant> decoyParticipants = new List<IMissionParticipant> { decoy };

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                mainParticipants,
                decoyParticipants,
                target
            );
            mainParticipants.Clear();
            decoyParticipants.Clear();

            CollectionAssert.AreEqual(new[] { officer }, mission.GetMainParticipants());
            CollectionAssert.AreEqual(new[] { decoy }, mission.GetDecoyParticipants());
        }

        [Test]
        public void GetAbortReason_MainParticipantRemoved_ReturnsFailure()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
                Planet empirePlanet,
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
        public void GetAbortReason_DecoyParticipantRemoved_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            game.AttachNode(decoy, empirePlanet);
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

            Assert.IsNull(mission.GetAbortReason(game));
        }

        [Test]
        public void ResolveObjective_SuccessOutcome_IncludesMissionCompletedResultWithMissionInstanceID()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.0));
            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().Single();

            Assert.AreEqual(mission.InstanceID, completed.MissionInstanceID);
        }

        [Test]
        public void ResolveObjective_FailOutcome_AlwaysIncludesMissionCompletedResult()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.99));

            Assert.IsTrue(
                results.OfType<MissionCompletedResult>().Any(),
                "Execute should always include MissionCompletedResult even on failure"
            );
        }

        [Test]
        public void ResolveObjective_SuccessfulMission_ImprovesOnlySuccessfulParticipantRating()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            int ratingBefore = officer.GetBaseRating(OfficerRating.Diplomacy);
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            int decoyRatingBefore = decoy.GetBaseRating(OfficerRating.Diplomacy);

            Mission mission = new StubMission("empire", enemyPlanet.InstanceID);
            mission.AddChild(officer);
            mission.AddDecoyParticipant(decoy);
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            mission.ResolveObjective(game, new FixedRNG(0.0));

            Assert.AreEqual(
                ratingBefore + 1,
                officer.GetBaseRating(OfficerRating.Diplomacy),
                "Officer mission rating should improve by 1 on mission success"
            );
            Assert.AreEqual(
                decoyRatingBefore,
                decoy.GetBaseRating(OfficerRating.Diplomacy),
                "Decoy rating should not improve from the objective roll"
            );
        }

        [Test]
        public void ResolveObjective_FailedSuccessRoll_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.99));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Execute should only return Success or Failed, never Foiled"
            );
        }

        [Test]
        public void ResolveObjective_OfficerAttemptFails_SpecialForcesCanSucceed()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = CreateSabotageTarget(game, enemyPlanet);
            officer.SetBaseRating(OfficerRating.Espionage, 0);
            officer.SetBaseRating(OfficerRating.Combat, 0);
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                OwnerInstanceID = "empire",
            };
            specialForces.SetBaseRating(OfficerRating.Espionage, 100);
            specialForces.SetBaseRating(OfficerRating.Combat, 100);
            game.Config.ProbabilityTables.Mission.Sabotage = new Dictionary<int, int>
            {
                { 0, 0 },
                { 100, 100 },
            };

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer, specialForces },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0.5));

            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreSame(
                specialForces,
                results.OfType<GameObjectSabotagedResult>().Single().Saboteur
            );
        }

        [Test]
        public void ResolveObjective_MultipleOfficers_TriesStrongerOfficerAfterWeakestFails()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer weakOfficer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = CreateSabotageTarget(game, enemyPlanet);
            weakOfficer.SetBaseRating(OfficerRating.Espionage, 0);
            weakOfficer.SetBaseRating(OfficerRating.Combat, 0);
            Officer strongOfficer = EntityFactory.CreateOfficer("strong-officer", "empire");
            strongOfficer.SetBaseRating(OfficerRating.Espionage, 100);
            strongOfficer.SetBaseRating(OfficerRating.Combat, 100);
            game.Config.ProbabilityTables.Mission.Sabotage = new Dictionary<int, int>
            {
                { 0, 0 },
                { 100, 100 },
            };

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { strongOfficer, weakOfficer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0));

            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreSame(
                strongOfficer,
                results.OfType<GameObjectSabotagedResult>().Single().Saboteur
            );
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(target.InstanceID));
        }

        [Test]
        public void ResolveObjective_OfficersOnSameProbabilityPlateau_PreservesSelectionOrder()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer lowScoreOfficer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Regiment target = CreateSabotageTarget(game, enemyPlanet);
            lowScoreOfficer.SetBaseRating(OfficerRating.Espionage, 20);
            lowScoreOfficer.SetBaseRating(OfficerRating.Combat, 20);
            Officer highScoreOfficer = EntityFactory.CreateOfficer("high-score", "empire");
            highScoreOfficer.SetBaseRating(OfficerRating.Espionage, 30);
            highScoreOfficer.SetBaseRating(OfficerRating.Combat, 30);
            game.Config.ProbabilityTables.Mission.Sabotage = new Dictionary<int, int>
            {
                { 0, 50 },
                { 100, 100 },
            };

            Mission mission = CreateSabotageMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { highScoreOfficer, lowScoreOfficer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            List<GameResult> results = mission.ResolveObjective(game, new FixedRNG(0));

            Assert.AreSame(
                highScoreOfficer,
                results.OfType<GameObjectSabotagedResult>().Single().Saboteur
            );
        }

        [Test]
        public void CanAcceptChild_WithMissionParticipant_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet empirePlanet,
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
                Planet empirePlanet,
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
        public void Serialize_RoundTripActiveMission_PreservesParticipantSceneGraph()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Officer decoy = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(decoy, empirePlanet);
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

            MovementSystem movement = new MovementSystem(game, fog, new FleetSystem(game));
            movement.RequestMove(officer, mission);
            movement.RequestMove(decoy, mission);

            string xml = SerializationHelper.Serialize(game);
            GameRoot deserialized = SerializationHelper.Deserialize<GameRoot>(xml);

            Mission loadedMission = deserialized.GetSceneNodesByType<Mission>().Single();
            Officer loadedOfficer = deserialized.GetSceneNodeByInstanceID<Officer>("o1");
            Officer loadedDecoy = deserialized.GetSceneNodeByInstanceID<Officer>("o2");

            Assert.AreEqual(loadedMission, loadedOfficer.GetParent());
            Assert.AreEqual(loadedMission, loadedDecoy.GetParent());
            Assert.AreEqual(loadedOfficer, loadedMission.GetMainParticipants().Single());
            Assert.AreEqual(loadedDecoy, loadedMission.GetDecoyParticipants().Single());
        }

        private static Mission CreateSabotageMission(
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode selectedTarget = null
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.Sabotage,
                null,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                selectedTarget
            );
        }

        private static Regiment CreateSabotageTarget(GameRoot game, Planet planet)
        {
            Regiment target = EntityFactory.CreateRegiment("sabotage-target", "rebels");
            target.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(target, planet);
            return target;
        }
    }
}
