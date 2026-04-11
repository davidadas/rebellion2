using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

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
            GameConfig config = TestConfig.Create();
            _game = new GameRoot(config);
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

            _trainer = EntityFactory.CreateOfficer("trainer", "rebels");
            _trainer.IsJedi = true;
            _trainer.IsJediTrainer = true;
            _trainer.IsForceEligible = true;
            _trainer.ForceValue = 120;
            _game.AttachNode(_trainer, _planet);

            _student = EntityFactory.CreateOfficer("student", "rebels");
            _student.IsJedi = true;
            _student.IsForceEligible = true;
            _student.ForceValue = 40;
            _student.JediProbability = 100;
            _game.AttachNode(_student, _planet);
        }

        private JediTrainingMission CreateMission(Officer student = null, Planet planet = null)
        {
            student = student ?? _student;
            planet = planet ?? _planet;

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = "rebels",
                Target = planet,
                MainParticipants = new List<IMissionParticipant> { student },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            JediTrainingMission mission = JediTrainingMission.TryCreate(ctx);
            if (mission != null)
                _game.AttachNode(mission, planet);
            return mission;
        }

        [Test]
        public void TryCreate_EnemyPlanet_ReturnsNull()
        {
            _planet.OwnerInstanceID = "empire";

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = "rebels",
                Target = _planet,
                MainParticipants = new List<IMissionParticipant> { _student },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            JediTrainingMission mission = JediTrainingMission.TryCreate(ctx);
            Assert.IsNull(mission);
        }

        [Test]
        public void TryCreate_ValidArgs_SetsTrainerInstanceID()
        {
            JediTrainingMission mission = CreateMission();
            Assert.AreEqual(_trainer.InstanceID, mission.TrainerInstanceID);
        }

        [Test]
        public void OnSuccess_StudentBelowTrainer_GainsTrainingAdjustment()
        {
            JediTrainingMission mission = CreateMission();
            mission.Initiate(new FixedRNG());
            mission.SetExecutionTick(0);

            // QueueRNG(0.0, 0.99): first value passes the success roll,
            // second value (0.99) makes NextInt return near-max of catch-up range
            mission.Execute(_game, new QueueRNG(0.0, 0.99));

            Assert.Greater(_student.ForceTrainingAdjustment, 0);
        }

        [Test]
        public void OnSuccess_StudentAboveTrainer_NoAdjustment()
        {
            _student.ForceValue = 150;
            JediTrainingMission mission = CreateMission();
            mission.Initiate(new FixedRNG());
            mission.SetExecutionTick(0);

            mission.Execute(_game, new FixedRNG(0.0));

            Assert.AreEqual(0, _student.ForceTrainingAdjustment);
        }

        [Test]
        public void Execute_Success_DoesNotAwardSkillImprovements()
        {
            int diplomacyBefore = _student.Skills[MissionParticipantSkill.Diplomacy];
            JediTrainingMission mission = CreateMission();
            mission.Initiate(new FixedRNG());
            mission.SetExecutionTick(0);

            mission.Execute(_game, new FixedRNG(0.0));

            Assert.AreEqual(diplomacyBefore, _student.Skills[MissionParticipantSkill.Diplomacy]);
        }

        [Test]
        public void ShouldAbort_TrainerCaptured_ReturnsTrue()
        {
            JediTrainingMission mission = CreateMission();
            _trainer.IsCaptured = true;
            Assert.IsTrue(mission.ShouldAbort(_game));
        }

        [Test]
        public void ShouldAbort_TrainerKilled_ReturnsTrue()
        {
            JediTrainingMission mission = CreateMission();
            _trainer.IsKilled = true;
            Assert.IsTrue(mission.ShouldAbort(_game));
        }

        [Test]
        public void ShouldAbort_TrainerAlive_ReturnsFalse()
        {
            JediTrainingMission mission = CreateMission();
            Assert.IsFalse(mission.ShouldAbort(_game));
        }

        [Test]
        public void CanContinue_OwnPlanet_ReturnsTrue()
        {
            JediTrainingMission mission = CreateMission();
            Assert.IsTrue(mission.CanContinue(_game));
        }

        [Test]
        public void CanContinue_PlanetLost_ReturnsFalse()
        {
            JediTrainingMission mission = CreateMission();
            _planet.OwnerInstanceID = "empire";
            Assert.IsFalse(mission.CanContinue(_game));
        }

        [Test]
        public void CanContinue_StudentForceQualified_ReturnsFalse()
        {
            JediTrainingMission mission = CreateMission();
            _student.ForceValue = _game.Config.Jedi.ForceQualifiedThreshold;
            Assert.IsFalse(mission.CanContinue(_game));
        }

        [Test]
        public void CanContinue_StudentBelowForceQualified_ReturnsTrue()
        {
            JediTrainingMission mission = CreateMission();
            _student.ForceValue = _game.Config.Jedi.ForceQualifiedThreshold - 1;
            Assert.IsTrue(mission.CanContinue(_game));
        }

        [Test]
        public void OnSuccess_StudentBelowTrainer_ReturnsForceTrainingResult()
        {
            JediTrainingMission mission = CreateMission();
            mission.Initiate(new FixedRNG());
            mission.SetExecutionTick(0);

            List<GameResult> results = mission.Execute(_game, new QueueRNG(0.0, 0.99));

            Assert.IsTrue(
                results.Any(r => r is ForceTrainingResult),
                "Should return a ForceTrainingResult on successful training"
            );

            ForceTrainingResult trainingResult = results.OfType<ForceTrainingResult>().First();
            Assert.AreEqual(_student, trainingResult.Officer);
            Assert.Greater(trainingResult.Progress, 0);
        }

        [Test]
        public void OnSuccess_MultipleStudents_TrainsAll()
        {
            Officer student2 = EntityFactory.CreateOfficer("student2", "rebels");
            student2.IsJedi = true;
            student2.IsForceEligible = true;
            student2.ForceValue = 30;
            student2.JediProbability = 100;
            _game.AttachNode(student2, _planet);

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = "rebels",
                Target = _planet,
                MainParticipants = new List<IMissionParticipant> { _student, student2 },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            JediTrainingMission mission = JediTrainingMission.TryCreate(ctx);
            _game.AttachNode(mission, _planet);
            mission.Initiate(new FixedRNG());
            mission.SetExecutionTick(0);

            // 0.0 passes the success roll (first student triggers early return),
            // 0.5 gives mid-range catch-up for student1,
            // 0.5 gives mid-range catch-up for student2
            mission.Execute(_game, new QueueRNG(0.0, 0.5, 0.5));

            Assert.Greater(
                _student.ForceTrainingAdjustment,
                0,
                "First student should gain training adjustment"
            );
            Assert.Greater(
                student2.ForceTrainingAdjustment,
                0,
                "Second student should gain training adjustment"
            );
        }

        [Test]
        public void CanContinue_MultipleStudents_OneQualified_ReturnsTrue()
        {
            Officer student2 = EntityFactory.CreateOfficer("student2", "rebels");
            student2.IsJedi = true;
            student2.IsForceEligible = true;
            student2.ForceValue = 10;
            _game.AttachNode(student2, _planet);

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = "rebels",
                Target = _planet,
                MainParticipants = new List<IMissionParticipant> { _student, student2 },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            JediTrainingMission mission = JediTrainingMission.TryCreate(ctx);
            _game.AttachNode(mission, _planet);

            _student.ForceValue = _game.Config.Jedi.ForceQualifiedThreshold;

            Assert.IsTrue(
                mission.CanContinue(_game),
                "Should continue while any student is below threshold"
            );
        }

        [Test]
        public void CanContinue_MultipleStudents_AllQualified_ReturnsFalse()
        {
            Officer student2 = EntityFactory.CreateOfficer("student2", "rebels");
            student2.IsJedi = true;
            student2.IsForceEligible = true;
            student2.ForceValue = _game.Config.Jedi.ForceQualifiedThreshold;
            _game.AttachNode(student2, _planet);

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = "rebels",
                Target = _planet,
                MainParticipants = new List<IMissionParticipant> { _student, student2 },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
            JediTrainingMission mission = JediTrainingMission.TryCreate(ctx);
            _game.AttachNode(mission, _planet);

            _student.ForceValue = _game.Config.Jedi.ForceQualifiedThreshold;

            Assert.IsFalse(
                mission.CanContinue(_game),
                "Should stop when all students are qualified"
            );
        }
    }
}
