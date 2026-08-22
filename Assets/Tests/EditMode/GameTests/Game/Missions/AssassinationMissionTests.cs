using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class AssassinationMissionTests
    {
        [Test]
        public void TryCreate_ValidTarget_ReturnsNotNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNotNull(
                mission,
                "TryCreate should succeed with a valid enemy officer on the target planet"
            );
            Assert.AreEqual("target", ((AssassinationMission)mission).TargetOfficerInstanceID);
        }

        [Test]
        public void TryCreate_TargetInTransit_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.Movement = new MovementState();
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_NullTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                null,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                null
            );

            Assert.IsNull(mission, "TryCreate should return null when target is null");
        }

        [Test]
        public void TryCreate_NonPlanetTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                officer,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                null
            );

            Assert.IsNull(mission, "TryCreate should return null when target is not a Planet");
        }

        [Test]
        public void TryCreate_NoValidTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                null
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when no valid target officers exist"
            );
        }

        [Test]
        public void TryCreate_FriendlyOfficerAsTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer friendly = EntityFactory.CreateOfficer("friendly", "empire");
            game.AttachNode(friendly, empirePlanet);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                friendly
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when target belongs to the same faction"
            );
        }

        [Test]
        public void TryCreate_TargetAlreadyCaptured_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.IsCaptured = true;
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNull(mission, "TryCreate should return null when target is already captured");
        }

        [Test]
        public void Execute_FirstHitSurvivedAndSecondHitKills_CreditsSecondAssassin()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer firstAssassin,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Officer secondAssassin = EntityFactory.CreateOfficer("second-assassin", "empire");
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.IsMain = false;
            game.AttachNode(target, enemyPlanet);
            firstAssassin.SetBaseRating(OfficerRating.Combat, 100);
            secondAssassin.SetBaseRating(OfficerRating.Combat, 100);
            target.SetBaseRating(OfficerRating.Combat, 0);
            int firstRating = firstAssassin.GetBaseRating(OfficerRating.Combat);
            int secondRating = secondAssassin.GetBaseRating(OfficerRating.Combat);
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { 0, 100 },
                { 100, 100 },
            };
            game.Config.Assassination.KillProbability = 50;

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { firstAssassin, secondAssassin },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);
            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.Execute(
                game,
                new SequenceRNG(
                    intValues: new[] { 0, 0, 99, 0, 0, 0 },
                    doubleValues: new[] { 0.0, 0.0 }
                )
            );

            Assert.AreEqual(2, results.OfType<OfficerInjuredResult>().Count());
            Assert.AreSame(secondAssassin, results.OfType<OfficerKilledResult>().Single().Assassin);
            Assert.AreEqual(firstRating, firstAssassin.GetBaseRating(OfficerRating.Combat));
            Assert.AreEqual(secondRating + 1, secondAssassin.GetBaseRating(OfficerRating.Combat));
            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void TryCreate_TargetAlreadyKilled_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.IsKilled = true;
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNull(mission, "TryCreate should return null when target is already killed");
        }

        [Test]
        public void TryCreate_TargetOnWrongPlanet_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            Assert.IsNull(
                mission,
                "TryCreate should return null when target is not on the mission target planet"
            );
        }

        [Test]
        public void Execute_SpecialForcesSucceedsAndKillCheckPasses_CreditsSpecialForces()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            officer.SetBaseRating(OfficerRating.Combat, 0);
            target.SetBaseRating(OfficerRating.Combat, 0);
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                OwnerInstanceID = "empire",
            };
            specialForces.SetBaseRating(OfficerRating.Combat, 100);
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { 0, 0 },
                { 100, 100 },
            };

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer, specialForces },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            Assert.IsTrue(
                results.Any(r => r is OfficerInjuredResult),
                "Should produce OfficerInjuredResult before kill"
            );

            OfficerKilledResult killed = results.OfType<OfficerKilledResult>().First();
            Assert.AreEqual("target", killed.TargetOfficer.InstanceID);
            Assert.AreEqual(
                specialForces.InstanceID,
                killed.Assassin.InstanceID,
                "Assassin should be the participant whose attempt succeeded"
            );
            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void Execute_SuccessKillCheckFails_TargetSurvivesWithInjury()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            int originalCombat = officer.GetBaseRating(OfficerRating.Combat);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { 0, 100 },
            };
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.Execute(
                game,
                new SequenceRNG(intValues: new[] { 0, 0, 99 }, doubleValues: new[] { 0.0 })
            );

            Assert.IsFalse(target.IsKilled, "Target should survive when kill check fails");
            Assert.Greater(target.InjuryPoints, 0, "Target should have injury points");
            Assert.IsTrue(
                results.Any(r => r is OfficerInjuredResult),
                "Should produce OfficerInjuredResult"
            );
            Assert.IsFalse(
                results.Any(r => r is OfficerKilledResult),
                "Should not produce OfficerKilledResult when target survives"
            );
            Assert.AreEqual(
                MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreEqual(
                originalCombat,
                officer.GetBaseRating(OfficerRating.Combat),
                "A hit that the target survives must not improve the assassin"
            );
        }

        [Test]
        public void Execute_MainCharacterHit_TargetSurvivesAndMissionFails()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.IsMain = true;
            game.AttachNode(target, enemyPlanet);
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { 0, 100 },
            };
            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);

            List<GameResult> results = mission.Execute(game, new FixedRNG(0));

            Assert.IsNotEmpty(results.OfType<OfficerInjuredResult>());
            Assert.IsEmpty(results.OfType<OfficerKilledResult>());
            Assert.AreEqual(
                MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void Execute_MinorCharacterKilled_ImprovesSuccessfulAssassinCombat()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            int originalCombat = officer.GetBaseRating(OfficerRating.Combat);
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { 0, 100 },
            };
            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);

            List<GameResult> results = mission.Execute(game, new FixedRNG(0));

            Assert.IsNotEmpty(results.OfType<OfficerKilledResult>());
            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
            Assert.AreEqual(originalCombat + 1, officer.GetBaseRating(OfficerRating.Combat));
        }

        [Test]
        public void Execute_TargetAlreadyKilled_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            // Target is killed after mission creation but before execution
            target.IsKilled = true;

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target is already killed before execution"
            );
        }

        [Test]
        public void Execute_TargetMovedToDifferentPlanet_DoesNotRollOrImproveParticipant()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            // A second enemy planet the target can legally move to
            Planet anotherEnemyPlanet = new Planet
            {
                InstanceID = "another_enemy",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(
                anotherEnemyPlanet,
                game.GetSceneNodeByInstanceID<PlanetSector>("sector1")
            );

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);
            int originalCombat = officer.GetBaseRating(OfficerRating.Combat);

            // Target moves to a different planet before mission executes
            game.MoveNode(target, anotherEnemyPlanet);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new ThrowingRNG());

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(MissionOutcome.Failed, completed.Outcome);
            Assert.AreEqual(MissionCompletionReason.TargetUnavailable, completed.CompletionReason);
            Assert.AreEqual(originalCombat, officer.GetBaseRating(OfficerRating.Combat));
        }

        [Test]
        public void Execute_TargetRemovedFromScene_ReturnsFailed()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();

            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);

            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            game.DetachNode(target);

            while (!mission.IsComplete())
                mission.IncrementProgress();
            List<GameResult> results = mission.Execute(game, new FixedRNG(0.0));

            MissionCompletedResult completed = results.OfType<MissionCompletedResult>().First();
            Assert.AreEqual(
                MissionOutcome.Failed,
                completed.Outcome,
                "Mission should fail when target has left the scene before execution"
            );
        }

        [Test]
        public void RollParticipantSuccess_SubtractsTargetCombatFromParticipantCombat()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarSystem fog
            ) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, enemyPlanet);
            officer.SetBaseRating(OfficerRating.Combat, 80);
            target.SetBaseRating(OfficerRating.Combat, 60);
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { 0, 0 },
                { 20, 100 },
            };
            Mission mission = CreateAssassinationMission(
                game,
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>(),
                target
            );

            bool advantageSucceeded = mission.RollParticipantSuccess(
                officer,
                new FixedRNG(0.5),
                game
            );
            target.SetBaseRating(OfficerRating.Combat, 80);
            bool equalCombatSucceeded = mission.RollParticipantSuccess(
                officer,
                new FixedRNG(0),
                game
            );

            Assert.IsTrue(advantageSucceeded);
            Assert.IsFalse(equalCombatSucceeded);
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new AssassinationMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "Assassination",
                DisplayName = "Assassination",
                LocationInstanceID = "PLANET1",
                ParticipantRating = OfficerRating.Combat,
                TargetOfficerInstanceID = "OFFICER1",
                HasInitiated = true,
                MaxProgress = 2,
                CurrentProgress = 1,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("Assassination", deserialized.ConfigKey);
            Assert.AreEqual(
                "OFFICER1",
                ((AssassinationMission)deserialized).TargetOfficerInstanceID
            );
            Assert.AreEqual(OfficerRating.Combat, deserialized.ParticipantRating);
            Assert.IsTrue(deserialized.HasInitiated);
            Assert.AreEqual(2, deserialized.MaxProgress);
            Assert.AreEqual(1, deserialized.CurrentProgress);
        }

        private static Mission CreateAssassinationMission(
            GameRoot game,
            string ownerInstanceId,
            ISceneNode target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            Officer targetOfficer
        )
        {
            return MissionTestFactory.TryCreate(
                MissionTypeIDs.Assassination,
                game,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants,
                targetOfficer: targetOfficer
            );
        }
    }
}
