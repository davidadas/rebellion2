using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class JediObserverTests
    {
        private GameRoot _game;
        private JediObserver _observer;

        /// <summary>
        /// Creates an encounter-growth listener with the active game configuration.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _observer = new JediObserver(new JediCommands(_game, new FixedRNG()));
        }

        [Test]
        public void HandleResults_EncounteredOfficerEvadesCapture_AppliesForceGrowth()
        {
            Officer luke = new Officer
            {
                InstanceID = "LUKE",
                OwnerInstanceID = "owner",
                GrowsForceOnMission = true,
                IsForceEligible = true,
                ForceValue = 10,
            };
            List<GameResult> results = _observer.HandleResults(
                new[]
                {
                    new DuelResult
                    {
                        EncounteredOfficer = luke,
                        EncounteredOfficerEvadedCapture = true,
                    },
                }
            );

            Assert.AreEqual(10 + _game.Config.Jedi.ForceGrowthPerMission, luke.ForceValue);
            Assert.AreEqual(1, results.OfType<ForceExperienceResult>().Count());
        }

        [Test]
        public void HandleResults_CapturedEncounteredOfficer_DoesNotApplyForceGrowth()
        {
            Officer officer = new Officer
            {
                GrowsForceOnMission = true,
                IsForceEligible = true,
                ForceValue = 10,
            };
            List<GameResult> results = _observer.HandleResults(
                new[]
                {
                    new DuelResult
                    {
                        EncounteredOfficer = officer,
                        EncounteredOfficerCaptured = true,
                    },
                }
            );

            Assert.IsEmpty(results);
            Assert.AreEqual(10, officer.ForceValue);
        }

        [Test]
        public void HandleResults_OpposingOfficerEvadesCapture_DoesNotApplyForceGrowth()
        {
            Officer opposing = new Officer
            {
                GrowsForceOnMission = true,
                IsForceEligible = true,
                ForceValue = 10,
            };

            List<GameResult> results = _observer.HandleResults(
                new[]
                {
                    new DuelResult
                    {
                        OpposingOfficer = opposing,
                        EncounteredOfficerEvadedCapture = true,
                    },
                }
            );

            Assert.IsEmpty(results);
            Assert.AreEqual(10, opposing.ForceValue);
        }

        [Test]
        public void Connect_SuccessfulMission_DoesNotApplyForceGrowth()
        {
            Officer officer = new Officer
            {
                GrowsForceOnMission = true,
                IsForceEligible = true,
                ForceValue = 10,
            };
            Mission mission = new DiplomacyMission();
            mission.AddChild(officer);
            GameResultBus results = new GameResultBus();
            _observer.Connect(results);

            List<GameResult> settled = results.Publish(
                new MissionCompletedResult { Mission = mission, Outcome = MissionOutcome.Success }
            );

            Assert.AreEqual(10, officer.ForceValue);
            Assert.IsEmpty(settled.OfType<ForceExperienceResult>());
        }

        [Test]
        public void HandleResults_NullBatch_ReturnsEmpty()
        {
            Assert.IsEmpty(_observer.HandleResults(null));
        }

        [Test]
        public void HandleResults_NullEntry_ReturnsEmpty()
        {
            Assert.IsEmpty(_observer.HandleResults(new DuelResult[] { null }));
        }
    }
}
