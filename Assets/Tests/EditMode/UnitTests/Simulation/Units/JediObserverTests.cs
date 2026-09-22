using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class JediObserverTests
    {
        private GameRoot _game;
        private JediObserver _observer;

        /// <summary>Creates a mission-growth listener with the active game configuration.</summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _observer = new JediObserver(
                new JediCommands(_game, new FixedRNG()),
                new GameResultBus()
            );
        }

        [Test]
        public void HandleResults_SuccessfulMission_AppliesForceGrowth()
        {
            Officer luke = new Officer
            {
                InstanceID = "LUKE",
                OwnerInstanceID = "owner",
                GrowsForceOnMission = true,
                IsForceEligible = true,
                ForceValue = 10,
            };
            Mission mission = new DiplomacyMission();
            mission.AddChild(luke);

            List<GameResult> results = _observer.HandleResults(
                new MissionCompletedResult[]
                {
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        Outcome = MissionOutcome.Success,
                    },
                }
            );

            Assert.AreEqual(10 + _game.Config.Jedi.ForceGrowthPerMission, luke.ForceValue);
            Assert.AreEqual(1, results.OfType<ForceExperienceResult>().Count());
        }

        [Test]
        public void HandleResults_RepeatedMissionSuccess_ReturnsSequentialGrowth()
        {
            Officer officer = new Officer
            {
                GrowsForceOnMission = true,
                IsForceEligible = true,
                ForceValue = 10,
            };
            Mission mission = new DiplomacyMission();
            mission.AddChild(officer);
            int growth = _game.Config.Jedi.ForceGrowthPerMission;
            _game.CurrentTick = 42;

            List<ForceExperienceResult> results = _observer
                .HandleResults(
                    new[]
                    {
                        new MissionCompletedResult
                        {
                            Mission = mission,
                            Outcome = MissionOutcome.Success,
                        },
                        new MissionCompletedResult
                        {
                            Mission = mission,
                            Outcome = MissionOutcome.Failed,
                        },
                        new MissionCompletedResult
                        {
                            Mission = null,
                            Outcome = MissionOutcome.Success,
                        },
                        new MissionCompletedResult
                        {
                            Mission = mission,
                            Outcome = MissionOutcome.Success,
                        },
                    }
                )
                .OfType<ForceExperienceResult>()
                .ToList();

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(10, results[0].PreviousForceRank);
            Assert.AreEqual(10 + growth, results[1].PreviousForceRank);
            Assert.AreEqual(10 + 2 * growth, officer.ForceValue);
            Assert.IsTrue(results.All(result => result.Tick == 42));
        }

        [Test]
        public void HandleResults_NullBatch_ReturnsEmpty()
        {
            Assert.IsEmpty(_observer.HandleResults(null));
        }

        [Test]
        public void HandleResults_NullEntry_ThrowsNullReferenceException()
        {
            Assert.Throws<System.NullReferenceException>(() =>
                _observer.HandleResults(new MissionCompletedResult[] { null })
            );
        }
    }
}
