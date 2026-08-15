using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class JediTrainingMissionTests
    {
        private GameRoot _game;
        private Planet _planet;
        private Officer _trainer;
        private Officer _student;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);

            _planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            _game.AttachNode(_planet, system);

            _trainer = CreateJedi("trainer", 120, isTrainer: true);
            _student = CreateJedi("student", 40);
        }

        private Officer CreateJedi(string instanceID, int forceValue, bool isTrainer = false)
        {
            Officer officer = EntityFactory.CreateOfficer(instanceID, "rebels");
            officer.IsJedi = true;
            officer.IsJediTrainer = isTrainer;
            officer.IsForceEligible = true;
            officer.ForceValue = forceValue;
            _game.AttachNode(officer, _planet);
            return officer;
        }

        private JediTrainingMission CreateMission(
            List<IMissionParticipant> participants = null,
            Planet planet = null
        )
        {
            participants ??= new List<IMissionParticipant> { _trainer, _student };
            planet ??= _planet;

            Mission mission = MissionTestFactory.TryCreate(
                MissionTypeIDs.JediTraining,
                _game,
                "rebels",
                planet,
                participants,
                new List<IMissionParticipant>()
            );
            if (mission != null)
                _game.AttachNode(mission, planet);

            return mission as JediTrainingMission;
        }

        [Test]
        public void TryCreate_EnemyPlanet_ReturnsNull()
        {
            _planet.OwnerInstanceID = "empire";

            Assert.IsNull(CreateMission());
        }

        [Test]
        public void TryCreate_NoParticipants_ReturnsNull()
        {
            Assert.IsNull(CreateMission(new List<IMissionParticipant>()));
        }

        [Test]
        public void TryCreate_TrainerOnly_ReturnsNull()
        {
            Assert.IsNull(CreateMission(new List<IMissionParticipant> { _trainer }));
        }

        [Test]
        public void TryCreate_StudentOnly_ReturnsNull()
        {
            Assert.IsNull(CreateMission(new List<IMissionParticipant> { _student }));
        }

        [Test]
        public void TryCreate_NonOfficerParticipant_ReturnsNull()
        {
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "rebels",
            };
            _game.AttachNode(specialForces, _planet);

            Assert.IsNull(
                CreateMission(new List<IMissionParticipant> { _trainer, _student, specialForces })
            );
        }

        [Test]
        public void TryCreate_ParticipantWithoutKnownForceAbility_ReturnsNull()
        {
            _student.IsForceEligible = false;

            Assert.IsNull(CreateMission());
        }

        [Test]
        public void TryCreate_NonJediParticipant_ReturnsNull()
        {
            _student.IsJedi = false;

            Assert.IsNull(CreateMission());
        }

        [Test]
        public void TryCreate_TrainerBelowQualifiedThreshold_ReturnsNull()
        {
            _trainer.ForceValue = _game.Config.Jedi.ForceQualifiedThreshold - 1;

            Assert.IsNull(CreateMission());
        }

        [Test]
        public void TryCreate_ParticipantAtTrainerRank_ReturnsNull()
        {
            _student.ForceValue = _trainer.ForceRank;

            Assert.IsNull(CreateMission());
        }

        [Test]
        public void TryCreate_QualifiedParticipantBelowTrainer_ReturnsMission()
        {
            _student.ForceValue = _game.Config.Jedi.ForceQualifiedThreshold;

            Assert.IsNotNull(CreateMission());
        }

        [Test]
        public void TryCreate_MultipleTrainers_SelectsHighestRankedTrainer()
        {
            Officer higherTrainer = CreateJedi("higher-trainer", 160, isTrainer: true);

            JediTrainingMission mission = CreateMission(
                new List<IMissionParticipant> { _trainer, _student, higherTrainer }
            );

            Assert.AreEqual(higherTrainer.InstanceID, mission.TrainerInstanceID);
        }

        [Test]
        public void TryCreate_ValidTeam_StoresSelectedTrainerAndTeam()
        {
            JediTrainingMission mission = CreateMission();

            Assert.AreEqual(_trainer.InstanceID, mission.TrainerInstanceID);
            Assert.AreEqual(_trainer, mission.Trainer);
            CollectionAssert.AreEqual(
                new IMissionParticipant[] { _trainer, _student },
                mission.MainParticipants
            );
        }

        [Test]
        public void Execute_IndependentParticipantRolls_TrainsOnlySuccessfulParticipant()
        {
            Officer secondStudent = CreateJedi("student2", 30);
            JediTrainingMission mission = CreateMission(
                new List<IMissionParticipant> { _trainer, _student, secondStudent }
            );

            List<GameResult> results = mission.Execute(
                _game,
                new SequenceRNG(intValues: new[] { 0, 0, 20, 99 })
            );

            ForceTrainingResult training = results.OfType<ForceTrainingResult>().Single();
            Assert.AreEqual(_student, training.Officer);
            Assert.AreEqual(20, training.Progress);
            Assert.AreEqual(20, _student.ForceTrainingAdjustment);
            Assert.AreEqual(0, secondStudent.ForceTrainingAdjustment);
            Assert.AreEqual(
                MissionOutcome.Success,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void Execute_PassedTrainingRollWithZeroProgress_ReturnsFailedReport()
        {
            JediTrainingMission mission = CreateMission();

            List<GameResult> results = mission.Execute(
                _game,
                new SequenceRNG(intValues: new[] { 0, 0, 0 })
            );

            Assert.IsEmpty(results.OfType<ForceTrainingResult>());
            Assert.AreEqual(0, _student.ForceTrainingAdjustment);
            Assert.AreEqual(
                MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void Execute_FailedTrainingRoll_ReturnsFailedReport()
        {
            JediTrainingMission mission = CreateMission();

            List<GameResult> results = mission.Execute(
                _game,
                new SequenceRNG(intValues: new[] { 0, 99 })
            );

            Assert.IsEmpty(results.OfType<ForceTrainingResult>());
            Assert.AreEqual(
                MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void Execute_TrainingProgress_DoesNotImproveDiplomacyRating()
        {
            int trainerDiplomacy = _trainer.GetBaseRating(OfficerRating.Diplomacy);
            int studentDiplomacy = _student.GetBaseRating(OfficerRating.Diplomacy);
            JediTrainingMission mission = CreateMission();

            mission.Execute(_game, new SequenceRNG(intValues: new[] { 0, 0, 20 }));

            Assert.AreEqual(trainerDiplomacy, _trainer.GetBaseRating(OfficerRating.Diplomacy));
            Assert.AreEqual(studentDiplomacy, _student.GetBaseRating(OfficerRating.Diplomacy));
        }

        [Test]
        public void GetAbortReason_TrainerCaptured_ReturnsFailure()
        {
            JediTrainingMission mission = CreateMission();
            _trainer.IsCaptured = true;

            Assert.AreEqual(MissionCompletionReason.Failure, mission.GetAbortReason(_game));
        }

        [Test]
        public void GetAbortReason_ParticipantKilled_ReturnsFailure()
        {
            JediTrainingMission mission = CreateMission();
            _student.IsKilled = true;

            Assert.AreEqual(MissionCompletionReason.Failure, mission.GetAbortReason(_game));
        }

        [Test]
        public void GetAbortReason_PlanetLost_ReturnsTargetUnavailable()
        {
            JediTrainingMission mission = CreateMission();
            _planet.OwnerInstanceID = "empire";

            Assert.AreEqual(
                MissionCompletionReason.TargetUnavailable,
                mission.GetAbortReason(_game)
            );
        }

        [Test]
        public void ShouldRepeatAfterCompletion_ValidAssignment_ReturnsFalse()
        {
            JediTrainingMission mission = CreateMission();

            Assert.IsFalse(mission.ShouldRepeatAfterCompletion(_game));
        }
    }
}
