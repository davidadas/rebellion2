using NUnit.Framework;
using Rebellion.Tactical;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatEndingTests
    {
        [Test]
        public void TryBegin_BothFactionsActive_LeavesEndingPending()
        {
            TacticalCombatEnding ending = new();
            Assert.IsFalse(ending.TryBegin(Force(1), Force(1), false, false));
            Assert.AreEqual(0, ending.RemainingMilliseconds);
        }

        [Test]
        public void TryBegin_OperationalStationAndAllianceActive_KeepsBattleActive()
        {
            TacticalCombatEnding ending = new();
            Assert.IsFalse(ending.TryBegin(Force(1), Force(0), true, false));
            Assert.AreEqual(0, ending.RemainingMilliseconds);
        }

        [Test]
        public void TryBegin_AllianceInactive_EndsEvenWithOperationalStation()
        {
            TacticalCombatEnding ending = new();
            Assert.IsTrue(ending.TryBegin(Force(0), Force(1), true, false));
            Assert.AreEqual(153, ending.GetTerminalNotification(0));
        }

        [TestCase(0, 152)]
        [TestCase(1, 283)]
        public void TryBegin_EmpireDestroyed_SelectsPlayerFactionTerminalCue(
            int faction,
            int message
        )
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(1), Force(0), false, false);
            Assert.AreEqual(message, ending.GetTerminalNotification(faction));
            CollectionAssert.AreEqual(
                new[] { false, true, false, false, false, false, false },
                ending.Flags
            );
        }

        [TestCase(0, 153)]
        [TestCase(1, 282)]
        public void TryBegin_AllianceDestroyed_SelectsPlayerFactionTerminalCue(
            int faction,
            int message
        )
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(0), Force(1), false, false);
            Assert.AreEqual(message, ending.GetTerminalNotification(faction));
        }

        [TestCase(0, 150)]
        [TestCase(1, 281)]
        public void TryBegin_AllianceDeparted_PrioritizesWithdrawalCue(int faction, int message)
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(0, 3, departed: true), Force(1), false, false);
            Assert.AreEqual(message, ending.GetTerminalNotification(faction));
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
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(1), Force(0, 4, departed: true), false, false);
            Assert.AreEqual(message, ending.GetTerminalNotification(faction));
            Assert.IsTrue(ending.Flags[2]);
        }

        [Test]
        public void TryBegin_StationDeparted_ContributesToEmpireDepartureFlag()
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(1), Force(0, 5), false, true);
            Assert.AreEqual(151, ending.GetTerminalNotification(0));
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
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(0, state, departed: true), Force(1), false, false);
            Assert.AreEqual(expected, ending.Flags[0]);
        }

        [Test]
        public void TryBegin_DepartedMemberWithStillActiveRoster_DoesNotMarkFactionWithdrawn()
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(0, 3, stillActive: true, departed: true), Force(1), false, false);
            Assert.IsFalse(ending.Flags[0]);
        }

        [Test]
        public void TryBegin_WithdrawRequestedWithoutDeparture_DoesNotMarkFactionWithdrawn()
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(0, 3), Force(1), false, false);
            Assert.IsFalse(ending.Flags[0]);
        }

        [Test]
        public void TryBegin_MutualDestruction_ClearsWinnerFlagsAndOmitsCue()
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(0), Force(0), false, false);
            CollectionAssert.AreEqual(
                new[] { false, false, false, false, false, false, false },
                ending.Flags
            );
            Assert.IsNull(ending.GetTerminalNotification(0));
        }

        [Test]
        public void TryBegin_RepeatedAssessment_PreservesTheFirstAssessment()
        {
            TacticalCombatEnding ending = new();
            Assert.IsTrue(ending.TryBegin(Force(1), Force(0), false, false));
            Assert.IsFalse(ending.TryBegin(Force(0), Force(1), false, false));
            Assert.AreEqual(152, ending.GetTerminalNotification(0));
            Assert.IsTrue(ending.Flags[1]);
        }

        [Test]
        public void AdvanceTerminalTick_BeforeAssessment_DoesNotComplete()
        {
            TacticalCombatEnding ending = new();
            Assert.IsFalse(ending.AdvanceTerminalTick());
        }

        [Test]
        public void AdvanceTerminalTick_FourAdmittedTickBoundaries_CompletesOnFourth()
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(1), Force(0), false, false);
            Assert.AreEqual(4000, ending.RemainingMilliseconds);
            for (int tick = 0; tick < 3; tick++)
                Assert.IsFalse(ending.AdvanceTerminalTick());
            Assert.AreEqual(1000, ending.RemainingMilliseconds);
            Assert.IsTrue(ending.AdvanceTerminalTick());
            Assert.AreEqual(0, ending.RemainingMilliseconds);
        }

        [Test]
        public void FinishWithoutAssessment_AdmittedCommand_CompletesWithoutDelay()
        {
            TacticalCombatEnding ending = new();
            ending.FinishWithoutAssessment();
            Assert.IsTrue(ending.IsComplete);
            Assert.IsTrue(ending.ImmediateResultRequested);
            Assert.AreEqual(0, ending.RemainingMilliseconds);
        }

        [Test]
        public void FinishWithoutAssessment_AfterCapturedResult_PreservesOtherSnapshotFlags()
        {
            TacticalCombatEnding ending = new();
            ending.TryBegin(Force(1), Force(0), false, false);
            ending.FinishWithoutAssessment();
            Assert.IsTrue(ending.Flags[1]);
            Assert.IsTrue(ending.ImmediateResultRequested);
        }

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
