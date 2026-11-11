using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatEndingTests
    {
        [Test]
        public void TryBegin_BothFactionsActive_DoesNotAssignOrQueueEnding()
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(0, events);
            Assert.IsFalse(ending.TryBegin(Force(1), Force(1), false, false));
            Assert.IsEmpty(events);
        }

        [Test]
        public void TryBegin_OperationalStationAndAllianceActive_KeepsBattleActive()
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(0, events);
            Assert.IsFalse(ending.TryBegin(Force(1), Force(0), true, false));
            Assert.IsEmpty(events);
        }

        [Test]
        public void TryBegin_AllianceInactive_EndsEvenWithOperationalStation()
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(0, events);
            Assert.IsTrue(ending.TryBegin(Force(0), Force(1), true, false));
            CollectionAssert.AreEqual(new[] { "sound:153", "assign" }, events);
        }

        [TestCase(0, 152)]
        [TestCase(1, 283)]
        public void TryBegin_EmpireDestroyed_QueuesPlayerFactionTerminalCue(
            int faction,
            int message
        )
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(faction, events);
            ending.TryBegin(Force(1), Force(0), false, false);
            CollectionAssert.AreEqual(new[] { $"sound:{message}", "assign" }, events);
            CollectionAssert.AreEqual(
                new[] { false, true, false, false, false, false, false },
                ending.Flags
            );
        }

        [TestCase(0, 153)]
        [TestCase(1, 282)]
        public void TryBegin_AllianceDestroyed_QueuesPlayerFactionTerminalCue(
            int faction,
            int message
        )
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(faction, events);
            ending.TryBegin(Force(0), Force(1), false, false);
            CollectionAssert.AreEqual(new[] { $"sound:{message}", "assign" }, events);
        }

        [TestCase(0, 150)]
        [TestCase(1, 281)]
        public void TryBegin_AllianceDeparted_PrioritizesWithdrawalCue(int faction, int message)
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(faction, events);
            ending.TryBegin(Force(0, 3, departed: true), Force(1), false, false);
            CollectionAssert.AreEqual(new[] { $"sound:{message}", "assign" }, events);
            Assert.IsTrue(ending.Flags[0]);
            Assert.IsTrue(ending.Flags[3]);
        }

        [TestCase(0, 151)]
        [TestCase(1, 280)]
        public void TryBegin_EmpireShipDepartedWithoutStation_PrioritizesWithdrawalCue(
            int faction,
            int message
        )
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(faction, events);
            ending.TryBegin(Force(1), Force(0, 4, departed: true), false, false);
            CollectionAssert.AreEqual(new[] { $"sound:{message}", "assign" }, events);
            Assert.IsTrue(ending.Flags[2]);
        }

        [Test]
        public void TryBegin_StationDeparted_ContributesToEmpireDepartureFlag()
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(0, events);
            ending.TryBegin(Force(1), Force(0, 5), false, true);
            CollectionAssert.AreEqual(new[] { "sound:151", "assign" }, events);
            Assert.IsTrue(ending.Flags[2]);
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        public void TryBegin_WithdrawalLifecycle_UsesOnlyAdmittedDepartureStates(
            int state,
            bool expected
        )
        {
            TacticalCombatEnding ending = Create(0, new());
            ending.TryBegin(Force(0, state, departed: true), Force(1), false, false);
            Assert.AreEqual(expected, ending.Flags[0]);
        }

        [Test]
        public void TryBegin_DepartedMemberWithStillActiveRoster_DoesNotMarkFactionWithdrawn()
        {
            TacticalCombatEnding ending = Create(0, new());
            ending.TryBegin(Force(0, 3, stillActive: true, departed: true), Force(1), false, false);
            Assert.IsFalse(ending.Flags[0]);
        }

        [Test]
        public void TryBegin_WithdrawRequestedWithoutDeparture_DoesNotMarkFactionWithdrawn()
        {
            TacticalCombatEnding ending = Create(0, new());
            ending.TryBegin(Force(0, 3), Force(1), false, false);
            Assert.IsFalse(ending.Flags[0]);
        }

        [Test]
        public void TryBegin_MutualDestruction_ClearsWinnerFlagsAndOmitsCue()
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(0, events);
            ending.TryBegin(Force(0), Force(0), false, false);
            CollectionAssert.AreEqual(
                new[] { false, false, false, false, false, false, false },
                ending.Flags
            );
            CollectionAssert.AreEqual(new[] { "assign" }, events);
        }

        [Test]
        public void TryBegin_RepeatedAssessment_DoesNotRecaptureOrAssignSurvivorsAgain()
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(0, events);
            Assert.IsTrue(ending.TryBegin(Force(1), Force(0), false, false));
            Assert.IsFalse(ending.TryBegin(Force(0), Force(1), false, false));
            CollectionAssert.AreEqual(new[] { "sound:152", "assign" }, events);
            Assert.IsTrue(ending.Flags[1]);
        }

        [Test]
        public void AdvanceTerminalTick_BeforeAssessment_DoesNotComplete()
        {
            TacticalCombatEnding ending = Create(0, new());
            Assert.IsFalse(ending.AdvanceTerminalTick());
        }

        [Test]
        public void AdvanceTerminalTick_FourAdmittedTickBoundaries_CompletesOnFourth()
        {
            TacticalCombatEnding ending = Create(0, new());
            ending.TryBegin(Force(1), Force(0), false, false);
            Assert.AreEqual(4000, ending.RemainingMilliseconds);
            for (int tick = 0; tick < 3; tick++)
                Assert.IsFalse(ending.AdvanceTerminalTick());
            Assert.AreEqual(1000, ending.RemainingMilliseconds);
            Assert.IsTrue(ending.AdvanceTerminalTick());
            Assert.AreEqual(0, ending.RemainingMilliseconds);
        }

        [Test]
        public void FinishWithoutAssessment_AdmittedCommand_CompletesWithoutCueOrAssignment()
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(0, events);
            ending.FinishWithoutAssessment();
            Assert.IsTrue(ending.IsComplete);
            Assert.IsTrue(ending.Flags[6]);
            Assert.AreEqual(0, ending.RemainingMilliseconds);
            CollectionAssert.AreEqual(new[] { "finish" }, events);
        }

        [Test]
        public void FinishWithoutAssessment_AfterCapturedResult_PreservesOtherSnapshotFlags()
        {
            List<string> events = new();
            TacticalCombatEnding ending = Create(0, events);
            ending.TryBegin(Force(1), Force(0), false, false);
            ending.FinishWithoutAssessment();
            Assert.IsTrue(ending.Flags[1]);
            Assert.IsTrue(ending.Flags[6]);
            CollectionAssert.AreEqual(new[] { "sound:152", "assign", "finish" }, events);
        }

        /// <summary>
        /// Creates ending with observed notification and command callbacks.
        /// </summary>
        /// <param name="faction">The player's authored faction index.</param>
        /// <param name="events">The callback observations.</param>
        /// <returns>The ending subject.</returns>
        private static TacticalCombatEnding Create(int faction, List<string> events) =>
            new(
                faction,
                message => events.Add($"sound:{message}"),
                () => events.Add("assign"),
                () => events.Add("finish")
            );

        /// <summary>
        /// Captures explicit active-root and roster facts for one faction.
        /// </summary>
        /// <param name="count">The active-root count.</param>
        /// <param name="state">The withdrawal lifecycle state.</param>
        /// <param name="stillActive">Whether a qualifying roster member remains active.</param>
        /// <param name="departed">Whether a roster member completed departure.</param>
        /// <returns>The force assessment.</returns>
        private static TacticalCombatForceAssessment Force(
            int count,
            int state = 0,
            bool stillActive = false,
            bool departed = false
        ) => new(count, state, stillActive, departed);
    }
}
