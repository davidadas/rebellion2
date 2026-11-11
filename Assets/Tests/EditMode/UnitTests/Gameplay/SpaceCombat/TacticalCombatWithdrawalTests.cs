using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatWithdrawalTests
    {
        [TestCase(0f, 0, false, 30000f)]
        [TestCase(0f, 0, true, 30000f)]
        [TestCase(0f, 1, false, 30000f)]
        [TestCase(0f, 1, true, 150000f)]
        [TestCase(30000f, 0, false, 30000f)]
        [TestCase(150000f, 1, true, 150000f)]
        [TestCase(45000f, 0, false, 45000f)]
        [TestCase(200000f, 1, true, 200000f)]
        [TestCase(float.NaN, 0, false, 30000f)]
        [TestCase(float.NaN, 1, true, 150000f)]
        [TestCase(float.NegativeInfinity, 0, false, 30000f)]
        [TestCase(float.PositiveInfinity, 1, true, float.PositiveInfinity)]
        public void CalculateInitialDelay_RouteAndStationFacts_SelectsApplicableMinimum(
            float route,
            int faction,
            bool station,
            float expected
        )
        {
            Assert.AreEqual(
                expected,
                TacticalCombatWithdrawal.CalculateInitialDelay(route, faction, station)
            );
        }

        [Test]
        public void AdvanceCountdown_PositiveDelay_RemainsWaiting()
        {
            TacticalCombatWithdrawal withdrawal = Create(0, 1001, new());
            Assert.IsTrue(withdrawal.AdvanceCountdown(1000, true, false, false));
            Assert.AreEqual(TacticalCombatWithdrawalPhase.Waiting, withdrawal.Phase);
            Assert.AreEqual(1, withdrawal.RemainingMilliseconds);
        }

        [TestCase(1000f)]
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void AdvanceCountdown_DelayExpiresOrIsUnordered_EntersDeparture(float delay)
        {
            TacticalCombatWithdrawal withdrawal = Create(0, delay, new());
            withdrawal.AdvanceCountdown(1000, true, false, false);
            Assert.AreEqual(TacticalCombatWithdrawalPhase.Departing, withdrawal.Phase);
        }

        [Test]
        public void AdvanceCountdown_IndividualTransitionsDeferred_ChangesPhaseWithoutStationCommand()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Create(1, 0, events);
            Assert.IsFalse(withdrawal.AdvanceCountdown(1000, false, true, true));
            Assert.AreEqual(TacticalCombatWithdrawalPhase.Departing, withdrawal.Phase);
            Assert.IsEmpty(events);
        }

        [Test]
        public void AdvanceCountdown_PlayerStation_DeparturePreparationPrecedesCommand()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Create(1, 1000, events);
            withdrawal.AdvanceCountdown(1000, true, true, true);
            CollectionAssert.AreEqual(new[] { "prepare", "station" }, events);
        }

        [Test]
        public void AdvanceCountdown_OpposingStation_RequestsCommandWithoutPlayerControlCleanup()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Create(1, 1000, events);
            withdrawal.AdvanceCountdown(1000, true, true, false);
            CollectionAssert.AreEqual(new[] { "station" }, events);
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void AdvanceCountdown_NoOperationalOwnedStation_OmitsStationCommand(
            int faction,
            bool operational
        )
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Create(faction, 1000, events);
            withdrawal.AdvanceCountdown(1000, true, operational, true);
            Assert.IsEmpty(events);
        }

        [Test]
        public void AdvanceCountdown_AfterCrossing_DoesNotRepeatCommandOrConsumeDelay()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Create(1, 1000, events);
            withdrawal.AdvanceCountdown(1000, true, true, true);
            withdrawal.AdvanceCountdown(1000, true, true, true);
            Assert.AreEqual(0, withdrawal.RemainingMilliseconds);
            CollectionAssert.AreEqual(new[] { "prepare", "station" }, events);
        }

        [Test]
        public void AdvanceMembers_Waiting_DoesNotInspectReadiness()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Create(0, 1000, events);
            withdrawal.AdvanceMembers(
                new[] { Member(1, events) },
                Array.Empty<ITacticalCombatWithdrawalMember>(),
                index => events.Add($"scuttle:{index}")
            );
            Assert.IsEmpty(events);
        }

        [Test]
        public void AdvanceMembers_ReadyCapital_StopsBeforeLaterCapitalAndFighters()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Departing(events);
            withdrawal.AdvanceMembers(
                new[] { Member(1, events, true), Member(2, events) },
                new[] { Member(3, events) },
                index => events.Add($"scuttle:{index}")
            );
            CollectionAssert.AreEqual(new[] { "ready:1" }, events);
            Assert.AreEqual(TacticalCombatWithdrawalPhase.Departing, withdrawal.Phase);
        }

        [Test]
        public void AdvanceMembers_ReadyDeparturePendingMember_KeepsDeparturePhase()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Departing(events);
            withdrawal.AdvanceMembers(
                Array.Empty<ITacticalCombatWithdrawalMember>(),
                new[] { Member(1, events, true, active: false, departing: true) },
                index => events.Add($"scuttle:{index}")
            );
            Assert.AreEqual(TacticalCombatWithdrawalPhase.Departing, withdrawal.Phase);
        }

        [Test]
        public void AdvanceMembers_ReadyMemberOutsideActiveAndDeparture_DoesNotBlockScuttling()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Departing(events);
            withdrawal.AdvanceMembers(
                new[] { Member(1, events, true, active: false) },
                Array.Empty<ITacticalCombatWithdrawalMember>(),
                index => events.Add($"scuttle:{index}")
            );
            Assert.AreEqual(TacticalCombatWithdrawalPhase.Scuttling, withdrawal.Phase);
            CollectionAssert.AreEqual(new[] { "ready:1", "ready:1" }, events);
        }

        [Test]
        public void AdvanceMembers_NoReadyForce_RechecksCapitalsThenFightersBeforeCommands()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Departing(events);
            withdrawal.AdvanceMembers(
                new[] { Member(1, events), Member(2, events, active: false) },
                new[] { Member(3, events) },
                index => events.Add($"scuttle:{index}")
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "ready:1",
                    "ready:2",
                    "ready:3",
                    "ready:1",
                    "scuttle:1",
                    "ready:2",
                    "scuttle:2",
                    "ready:3",
                    "scuttle:3",
                },
                events
            );
            Assert.AreEqual(TacticalCombatWithdrawalPhase.Scuttling, withdrawal.Phase);
        }

        [Test]
        public void AdvanceMembers_ReadinessChangesAfterFirstPass_UsesSecondReadForScuttle()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Departing(events);
            int reads = 0;
            WithdrawalMember member = Member(1, events);
            member.Readiness = () => ++reads == 2;
            withdrawal.AdvanceMembers(
                new[] { member },
                Array.Empty<ITacticalCombatWithdrawalMember>(),
                index => events.Add($"scuttle:{index}")
            );
            CollectionAssert.AreEqual(new[] { "ready:1", "ready:1" }, events);
            Assert.AreEqual(TacticalCombatWithdrawalPhase.Scuttling, withdrawal.Phase);
        }

        [Test]
        public void AdvanceMembers_AlreadyScuttling_DoesNotQueueAgain()
        {
            List<string> events = new();
            TacticalCombatWithdrawal withdrawal = Departing(events);
            WithdrawalMember[] members = { Member(1, events) };
            withdrawal.AdvanceMembers(
                members,
                Array.Empty<ITacticalCombatWithdrawalMember>(),
                index => events.Add($"scuttle:{index}")
            );
            events.Clear();
            withdrawal.AdvanceMembers(
                members,
                Array.Empty<ITacticalCombatWithdrawalMember>(),
                index => events.Add($"scuttle:{index}")
            );
            Assert.IsEmpty(events);
        }

        [TestCase(0, 45000f, false, 45000f)]
        [TestCase(1, 45000f, true, 150000f)]
        [TestCase(0, float.NaN, false, 30000f)]
        public void SelectInitialDelay_AfterGroupSetup_AppliesCapturedRouteMinimum(
            int faction,
            float route,
            bool station,
            float expected
        )
        {
            TacticalCombatWithdrawal withdrawal = Create(faction, route, new());

            withdrawal.SelectInitialDelay(station);

            Assert.AreEqual(expected, withdrawal.RemainingMilliseconds);
        }

        /// <summary>
        /// Creates a withdrawal whose admitted countdown has expired.
        /// </summary>
        /// <param name="events">The callback observations.</param>
        /// <returns>The departure-phase subject.</returns>
        private static TacticalCombatWithdrawal Departing(List<string> events)
        {
            TacticalCombatWithdrawal withdrawal = Create(0, 0, events);
            withdrawal.AdvanceCountdown(1000, true, false, false);
            return withdrawal;
        }

        /// <summary>
        /// Creates the subject with observed station callbacks.
        /// </summary>
        /// <param name="faction">The authored faction index.</param>
        /// <param name="delay">The already selected delay.</param>
        /// <param name="events">The callback observations.</param>
        /// <returns>The withdrawal subject.</returns>
        private static TacticalCombatWithdrawal Create(
            int faction,
            float delay,
            List<string> events
        ) => new(faction, delay, () => events.Add("prepare"), () => events.Add("station"));

        /// <summary>
        /// Creates one explicitly described ordered member.
        /// </summary>
        /// <param name="index">The member registration identifier.</param>
        /// <param name="events">The callback observations.</param>
        /// <param name="ready">The initial readiness result.</param>
        /// <param name="active">Whether it is an active-root member accepted by the active predicate.</param>
        /// <param name="departing">Whether its animation or withdrawal command is pending.</param>
        /// <returns>The member fixture.</returns>
        private static WithdrawalMember Member(
            int index,
            List<string> events,
            bool ready = false,
            bool active = true,
            bool departing = false
        ) => new(index, events, ready, active, departing);

        private sealed class WithdrawalMember : ITacticalCombatWithdrawalMember
        {
            private readonly List<string> _events;
            public int Index { get; }
            public bool IsActiveMember { get; }
            public bool IsAwaitingDeparture { get; }
            public Func<bool> Readiness { get; set; }

            /// <summary>
            /// Captures the member's independently supplied readiness and participation facts.
            /// </summary>
            /// <param name="index">The member registration identifier.</param>
            /// <param name="events">The callback observations.</param>
            /// <param name="ready">The initial readiness result.</param>
            /// <param name="active">The active-root predicate result.</param>
            /// <param name="departing">Whether departure is pending.</param>
            public WithdrawalMember(
                int index,
                List<string> events,
                bool ready,
                bool active,
                bool departing
            )
            {
                Index = index;
                _events = events;
                IsActiveMember = active;
                IsAwaitingDeparture = departing;
                Readiness = () => ready;
            }

            /// <summary>
            /// Records each ordered readiness evaluation.
            /// </summary>
            /// <returns>The currently supplied readiness result.</returns>
            public bool CheckWithdrawalReadiness()
            {
                _events.Add($"ready:{Index}");
                return Readiness();
            }
        }
    }
}
