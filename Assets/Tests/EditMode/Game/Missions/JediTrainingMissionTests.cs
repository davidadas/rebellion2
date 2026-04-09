using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;

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

        private JediTrainingMission CreateMission(
            Officer student = null,
            Officer trainer = null,
            Planet planet = null
        )
        {
            student = student ?? _student;
            trainer = trainer ?? _trainer;
            planet = planet ?? _planet;

            JediTrainingMission mission = new JediTrainingMission(
                "rebels",
                planet,
                new List<IMissionParticipant> { student },
                new List<IMissionParticipant>(),
                trainer.InstanceID
            );
            _game.AttachNode(mission, planet);
            return mission;
        }

        [Test]
        public void Constructor_NullTrainerInstanceId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new JediTrainingMission(
                    "rebels",
                    _planet,
                    new List<IMissionParticipant> { _student },
                    new List<IMissionParticipant>(),
                    null
                )
            );
        }

        [Test]
        public void Constructor_EmptyTrainerInstanceId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new JediTrainingMission(
                    "rebels",
                    _planet,
                    new List<IMissionParticipant> { _student },
                    new List<IMissionParticipant>(),
                    ""
                )
            );
        }

        [Test]
        public void Constructor_StudentNotJedi_Throws()
        {
            Officer nonJedi = EntityFactory.CreateOfficer("nonjedi", "rebels");
            nonJedi.IsJedi = false;
            nonJedi.IsForceEligible = true;
            _game.AttachNode(nonJedi, _planet);

            Assert.Throws<InvalidOperationException>(() => CreateMission(student: nonJedi));
        }

        [Test]
        public void Constructor_StudentNotForceEligible_Throws()
        {
            Officer dormant = EntityFactory.CreateOfficer("dormant", "rebels");
            dormant.IsJedi = true;
            dormant.IsForceEligible = false;
            _game.AttachNode(dormant, _planet);

            Assert.Throws<InvalidOperationException>(() => CreateMission(student: dormant));
        }

        [Test]
        public void Constructor_EnemyPlanet_Throws()
        {
            _planet.OwnerInstanceID = "empire";

            Assert.Throws<InvalidOperationException>(() =>
                new JediTrainingMission(
                    "rebels",
                    _planet,
                    new List<IMissionParticipant> { _student },
                    new List<IMissionParticipant>(),
                    _trainer.InstanceID
                )
            );
        }

        [Test]
        public void Constructor_ValidArgs_SetsTrainerInstanceID()
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
        public void Execute_DoesNotAwardSkillImprovements()
        {
            int diplomacyBefore = _student.Skills[MissionParticipantSkill.Diplomacy];
            JediTrainingMission mission = CreateMission();
            mission.Initiate(new FixedRNG());
            mission.SetExecutionTick(0);

            mission.Execute(_game, new FixedRNG(0.0));

            Assert.AreEqual(diplomacyBefore, _student.Skills[MissionParticipantSkill.Diplomacy]);
        }

        [Test]
        public void IsCanceled_TrainerCaptured_ReturnsTrue()
        {
            JediTrainingMission mission = CreateMission();
            _trainer.IsCaptured = true;
            Assert.IsTrue(mission.IsCanceled(_game));
        }

        [Test]
        public void IsCanceled_TrainerKilled_ReturnsTrue()
        {
            JediTrainingMission mission = CreateMission();
            _trainer.IsKilled = true;
            Assert.IsTrue(mission.IsCanceled(_game));
        }

        [Test]
        public void IsCanceled_TrainerAlive_ReturnsFalse()
        {
            JediTrainingMission mission = CreateMission();
            Assert.IsFalse(mission.IsCanceled(_game));
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
    }
}
