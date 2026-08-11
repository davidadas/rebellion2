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
    public class CustomMissionTests
    {
        [Test]
        public void RollDuration_AuthoredBaseAndSpread_UsesDefinition()
        {
            (GameRoot game, Planet _, Planet location, Officer officer, _) =
                MissionSceneBuilder.Build();
            CustomMissionDefinition definition = CaptureDefinition();
            definition.DurationTicks = 5;
            definition.DurationRandomTicks = 11;
            CustomMission mission = CreateMission(game, location, definition, officer);

            int duration = mission.RollDuration(new QueueRNG(0.5));

            Assert.AreEqual(10, duration);
        }

        [Test]
        public void Execute_OfficerCapture_CapturesBoundTarget()
        {
            (GameRoot game, Planet _, Planet location, Officer _, _) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, location);
            CustomMissionDefinition definition = CaptureDefinition();
            definition.CaptorFactionInstanceID = "empire";
            definition.TargetCanEscape = false;
            CustomMission mission = CreateMission(game, location, definition, target);

            List<GameResult> results = mission.Execute(game, new FixedRNG(0));

            Assert.IsTrue(target.IsCaptured);
            Assert.AreEqual("empire", target.CaptorInstanceID);
            Assert.IsFalse(target.CanEscape);
            Assert.IsTrue(results.OfType<OfficerCaptureStateResult>().Single().IsCaptured);
            Assert.IsTrue(results.OfType<OfficerCaptureAttemptResult>().Single().WasCaptured);
            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void Execute_OfficerCapture_TargetCapturedElsewhere_EndsUnavailableWithoutOverwrite()
        {
            (GameRoot game, Planet _, Planet location, Officer _, _) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, location);
            CustomMission mission = CreateMission(game, location, CaptureDefinition(), target);
            target.IsCaptured = true;
            target.CaptorInstanceID = "third-party";

            List<GameResult> results = mission.Execute(game, new FixedRNG(0));

            Assert.AreEqual("third-party", target.CaptorInstanceID);
            Assert.IsEmpty(results.OfType<OfficerCaptureStateResult>());
            MissionCompletedResult completion = results.OfType<MissionCompletedResult>().Single();
            Assert.AreEqual(MissionOutcome.Failed, completion.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completion.CompletionReason);
        }

        [Test]
        public void Execute_OfficerRescue_FreesCaptiveAndRewardsRescuer()
        {
            (GameRoot game, Planet _, Planet location, Officer rescuer, _) =
                MissionSceneBuilder.Build();
            rescuer.SetBaseRating(OfficerRating.Combat, 90);
            rescuer.SetBaseRating(OfficerRating.Espionage, 90);
            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            captive.CaptorInstanceID = "rebels";
            game.AttachNode(captive, location);
            CustomMissionDefinition definition = new CustomMissionDefinition
            {
                InstanceID = "rescue",
                DisplayName = "Rescue",
                Resolution = CustomMissionResolution.OfficerRescue,
                RatingDivisor = 3,
                SuccessCombatBonus = 1,
                SuccessEspionageBonus = 1,
            };
            CustomMission mission = CreateMission(game, location, definition, captive, rescuer);

            List<GameResult> results = mission.Execute(game, new FixedRNG(0));

            Assert.IsFalse(captive.IsCaptured);
            Assert.IsNull(captive.CaptorInstanceID);
            Assert.AreEqual(91, rescuer.GetBaseRating(OfficerRating.Combat));
            Assert.AreEqual(91, rescuer.GetBaseRating(OfficerRating.Espionage));
            Assert.AreSame(captive, results.OfType<OfficerRescuedResult>().Single().Officer);
        }

        [Test]
        public void Execute_PrisonerPickup_TransfersEligiblePrisonersToCollectorFaction()
        {
            (GameRoot game, Planet _, Planet location, Officer collector, _) =
                MissionSceneBuilder.Build();
            Officer prisoner = EntityFactory.CreateOfficer("prisoner", "empire");
            prisoner.IsCaptured = true;
            prisoner.CaptorInstanceID = "rebels";
            game.AttachNode(prisoner, location);
            CustomMissionDefinition definition = new CustomMissionDefinition
            {
                InstanceID = "pickup",
                DisplayName = "Pickup",
                Resolution = CustomMissionResolution.PrisonerPickup,
                CaptiveFactionInstanceID = "empire",
                CaptivesCanEscapeAfterPickup = true,
            };
            CustomMission mission = CreateMission(game, location, definition, prisoner, collector);

            List<GameResult> results = mission.Execute(game, new FixedRNG(0));

            Assert.AreEqual("empire", prisoner.CaptorInstanceID);
            Assert.IsTrue(prisoner.CanEscape);
            CollectionAssert.AreEqual(
                new[] { prisoner },
                results.OfType<PrisonerPickupCompletedResult>().Single().Prisoners
            );
        }

        [Test]
        public void Execute_GatherPhase_RequestsFollowUpWithTargetAndParticipant()
        {
            (GameRoot game, Planet location, Planet _, Officer opponent, _) =
                MissionSceneBuilder.Build();
            Officer subject = EntityFactory.CreateOfficer("subject", "rebels");
            Officer authority = EntityFactory.CreateOfficer("authority", "empire");
            subject.IsCaptured = true;
            subject.CaptorInstanceID = "empire";
            game.AttachNode(subject, location);
            game.AttachNode(authority, location);
            CustomMissionDefinition definition = new CustomMissionDefinition
            {
                InstanceID = "gather",
                DisplayName = "Gather",
                Resolution = CustomMissionResolution.ForceConfrontation,
                Phase = CustomMissionPhase.GatherTarget,
                AuthorityUnitInstanceID = authority.InstanceID,
                FollowUpMissionDefinitionID = "escort",
            };
            CustomMission mission = CreateMission(game, location, definition, subject, opponent);

            CustomMissionRequestedResult request = mission
                .Execute(game, new FixedRNG(0))
                .OfType<CustomMissionRequestedResult>()
                .Single();

            Assert.AreEqual("escort", request.MissionDefinitionID);
            Assert.AreEqual(subject.InstanceID, request.TargetInstanceID);
            CollectionAssert.AreEqual(
                new[] { opponent.InstanceID },
                request.MainParticipantInstanceIDs
            );
            Assert.IsEmpty(request.DecoyParticipantInstanceIDs);
        }

        [Test]
        public void HandleResults_ValidRequest_CreatesDefinitionBackedMission()
        {
            (GameRoot game, Planet _, Planet location, Officer _, _) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, location);
            CustomMissionDefinition definition = CaptureDefinition();
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            MissionSystem system = TestSystems.CreateMissionSystem(
                game,
                new FixedRNG(0),
                movement,
                new[] { definition }
            );

            system.HandleResults(
                new[]
                {
                    new CustomMissionRequestedResult
                    {
                        MissionDefinitionID = definition.InstanceID,
                        TargetInstanceID = target.InstanceID,
                    },
                }
            );

            CustomMission mission = game.GetSceneNodesByType<CustomMission>().Single();
            Assert.AreEqual(definition.InstanceID, mission.MissionDefinitionID);
            Assert.AreSame(definition, mission.Definition);
            Assert.AreEqual(definition.DisplayName, mission.DisplayName);
            Assert.IsFalse(mission.CanAbort);
        }

        [Test]
        public void GetChildren_EscortedTarget_IsRetainedWithoutBecomingParticipant()
        {
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            CustomMission mission = new CustomMission
            {
                InstanceID = "escort",
                TargetInstanceID = target.InstanceID,
                HasInitiated = true,
            };

            mission.AddChild(target);
            target.SetParent(mission);

            CollectionAssert.Contains(mission.GetChildren().ToList(), target);
            Assert.IsEmpty(mission.MainParticipants);
            Assert.IsEmpty(mission.DecoyParticipants);
        }

        private static CustomMissionDefinition CaptureDefinition() =>
            new CustomMissionDefinition
            {
                InstanceID = "capture",
                DisplayName = "Capture",
                Resolution = CustomMissionResolution.OfficerCapture,
                OwnerFactionInstanceID = "empire",
                ResistanceRating = OfficerRating.None,
                ProbabilityTableKey = AbductionMission.MissionTypeID,
            };

        private static CustomMission CreateMission(
            GameRoot game,
            Planet location,
            CustomMissionDefinition definition,
            IGameEntity target,
            params IMissionParticipant[] mainParticipants
        )
        {
            CustomMission mission = new CustomMission(
                definition,
                target.InstanceID,
                mainParticipants.Select(participant => participant.InstanceID),
                new List<string>(),
                "event",
                game
            );
            game.AttachNode(mission, location);
            return mission;
        }
    }
}
