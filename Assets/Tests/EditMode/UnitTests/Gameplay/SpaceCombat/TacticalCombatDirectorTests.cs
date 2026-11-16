using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatDirectorTests
    {
        [TestCase(0, 32)]
        [TestCase(1, 154)]
        public void Advance_FirstActiveFrame_QueuesPlayerFactionBriefing(int faction, int message)
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(faction, messages.Add);
            director.Advance(0, State());
            CollectionAssert.AreEqual(new[] { message }, messages);
        }

        [Test]
        public void Advance_ArrivalStage_DoesNotConsumeBriefingTimer()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            director.Advance(100000, State(arriving: true));
            Assert.IsEmpty(messages);
            director.Advance(0, State());
            CollectionAssert.AreEqual(new[] { 32 }, messages);
        }

        [Test]
        public void Advance_ProtectedStation_QueuesOrderedStationBriefings()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            TacticalCombatDirectorState state = State(
                station: true,
                operational: true,
                protectedStation: true
            );
            director.Advance(0, state);
            director.Advance(5999, state);
            CollectionAssert.AreEqual(new[] { 32 }, messages);
            director.Advance(1, state);
            director.Advance(6000, state);
            CollectionAssert.AreEqual(new[] { 32, 144, 147 }, messages);
        }

        [Test]
        public void Advance_MultipleIntervalsInOneFrame_DoesNotCatchUpBriefings()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            TacticalCombatDirectorState state = State(
                station: true,
                operational: true,
                protectedStation: true
            );
            director.Advance(0, state);
            director.Advance(60000, state);
            CollectionAssert.AreEqual(new[] { 32, 144 }, messages);
        }

        [Test]
        public void Advance_StationRemovedAfterFirstBriefing_StillQueuesSecondBriefing()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            director.Advance(0, State(station: true, operational: true));
            director.Advance(6000, State());
            director.Advance(6000, State());
            CollectionAssert.AreEqual(new[] { 32, 144 }, messages);
        }

        [Test]
        public void Advance_FighterWarning_ExcludesAllianceAttackRunMembersOnly()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            TacticalCombatDirectorState state = State(
                station: true,
                operational: true,
                allianceChain: true,
                empireChain: true,
                allianceCount: 2,
                empireCount: 1,
                allianceOutsideRuns: 0,
                attackRunOrdered: true
            );
            director.Advance(0, state);
            director.Advance(6000, state);
            director.Advance(6000, state);
            CollectionAssert.AreEqual(new[] { 32, 144, 148 }, messages);
        }

        [Test]
        public void Advance_ExistingAttackRunHelper_SuppressesFighterWarning()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            TacticalCombatDirectorState state = State(
                station: true,
                operational: true,
                allianceChain: true,
                attackRunActive: true,
                allianceCount: 0,
                empireCount: 2
            );
            director.Advance(0, state);
            director.Advance(6000, state);
            director.Advance(6000, state);
            CollectionAssert.AreEqual(new[] { 32, 144 }, messages);
        }

        [TestCase(0, 146)]
        [TestCase(1, 284)]
        public void Advance_WaitingPeriodExpires_EvaluatesAdmissionInSameFrame(
            int faction,
            int message
        )
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(faction, messages.Add);
            director.Advance(0, State());
            director.Advance(6000, Admitted());
            Assert.AreEqual(message, messages[1]);
        }

        [Test]
        public void Advance_AdmissionChangesAfterWaiting_ObservesEveryFrameWithoutNewDelay()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            director.Advance(0, State());
            director.Advance(6000, State());
            director.Advance(1, Admitted());
            director.Advance(1, State(allianceChain: true, empireChain: true));
            CollectionAssert.AreEqual(new[] { 32, 146, 148 }, messages);
        }

        [Test]
        public void Advance_SuppressedFallingEdge_StillRecordsNewAdmissionState()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            director.Advance(0, State());
            director.Advance(6000, Admitted());
            director.Advance(1, State());
            director.Advance(1, State(allianceChain: true, empireChain: true));
            CollectionAssert.AreEqual(new[] { 32, 146 }, messages);
        }

        [Test]
        public void Advance_EmpireRisingEdgeWithoutAllianceChain_DoesNotRepeatWhenChainAppears()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(1, messages.Add);
            director.Advance(0, State());
            director.Advance(6000, State(station: true, operational: true, allianceCount: 1));
            director.Advance(1, Admitted());
            CollectionAssert.AreEqual(new[] { 154 }, messages);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void Advance_FallingEdgeDuringAttackRun_DoesNotQueueWarning(
            bool active,
            bool ordered
        )
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            director.Advance(0, State());
            director.Advance(6000, Admitted());
            director.Advance(
                1,
                State(
                    allianceChain: true,
                    empireChain: true,
                    attackRunActive: active,
                    attackRunOrdered: ordered
                )
            );
            CollectionAssert.AreEqual(new[] { 32, 146 }, messages);
        }

        [Test]
        public void Advance_UnorderedCountdown_ExpiresBriefingWithoutLooping()
        {
            List<int> messages = new();
            TacticalCombatDirector director = new(0, messages.Add);
            director.Advance(float.NaN, State());
            CollectionAssert.AreEqual(new[] { 32 }, messages);
        }

        [Test]
        public void CanAttackBattleStation_AttackRunMembersInActiveRoot_CountTowardsAdmission()
        {
            TacticalCombatDirectorState state = State(
                station: true,
                operational: true,
                allianceCount: 2,
                empireCount: 1,
                allianceOutsideRuns: 0
            );
            Assert.IsTrue(state.CanAttackBattleStation);
        }

        [Test]
        public void CanAttackBattleStation_EqualActiveFighters_RejectsAdmission()
        {
            Assert.IsFalse(
                State(
                    station: true,
                    operational: true,
                    allianceCount: 2,
                    empireCount: 2
                ).CanAttackBattleStation
            );
        }

        [TestCase(false, true, false, false, false)]
        [TestCase(true, false, false, false, false)]
        [TestCase(true, true, true, false, false)]
        [TestCase(true, true, false, true, false)]
        [TestCase(true, true, false, false, true)]
        public void CanAttackBattleStation_RequiredGateMissing_RejectsAdmission(
            bool station,
            bool operational,
            bool protectedStation,
            bool active,
            bool ordered
        )
        {
            Assert.IsFalse(
                State(
                    station: station,
                    operational: operational,
                    protectedStation: protectedStation,
                    attackRunActive: active,
                    attackRunOrdered: ordered,
                    allianceCount: 2
                ).CanAttackBattleStation
            );
        }

        /// <summary>
        /// Captures a globally admitted attack run with populated fighter chains.
        /// </summary>
        /// <returns>The admitted director facts.</returns>
        private static TacticalCombatDirectorState Admitted() =>
            State(
                station: true,
                operational: true,
                allianceChain: true,
                empireChain: true,
                allianceCount: 2,
                empireCount: 1,
                allianceOutsideRuns: 2
            );

        /// <summary>
        /// Captures explicit director facts without a simulation or presentation clock.
        /// </summary>
        /// <param name="arriving">Whether arrival is active.</param>
        /// <param name="station">Whether the station exists.</param>
        /// <param name="operational">Whether the station is operational.</param>
        /// <param name="protectedStation">Whether the protection gate is set.</param>
        /// <param name="attackRunActive">Whether the attack-run helper exists.</param>
        /// <param name="allianceChain">Whether the Alliance fighter chain is populated.</param>
        /// <param name="empireChain">Whether the Empire fighter chain is populated.</param>
        /// <param name="attackRunOrdered">Whether an Alliance group requests or performs an attack run.</param>
        /// <param name="allianceCount">Every Alliance active-root fighter.</param>
        /// <param name="empireCount">Every Empire active-root fighter.</param>
        /// <param name="allianceOutsideRuns">Alliance active-root fighters outside attack runs.</param>
        /// <returns>The explicit director facts.</returns>
        private static TacticalCombatDirectorState State(
            bool arriving = false,
            bool station = false,
            bool operational = false,
            bool protectedStation = false,
            bool attackRunActive = false,
            bool allianceChain = false,
            bool empireChain = false,
            bool attackRunOrdered = false,
            int allianceCount = 0,
            int empireCount = 0,
            int allianceOutsideRuns = 0
        ) =>
            new(
                arriving,
                station,
                operational,
                protectedStation,
                attackRunActive,
                allianceChain,
                empireChain,
                attackRunOrdered,
                allianceCount,
                empireCount,
                allianceOutsideRuns
            );
    }
}
