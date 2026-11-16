using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatSchedulerTests
    {
        [Test]
        public void Constructor_MissingClock_RejectsProvider()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TacticalCombatScheduler(null, new Steps())
            );
        }

        [Test]
        public void Constructor_MissingSteps_RejectsOwner()
        {
            Assert.Throws<ArgumentNullException>(() => new TacticalCombatScheduler(() => 0, null));
        }

        [Test]
        public void Advance_FirstTick_InitializesBeforeDrainingWithoutEarlierClockRead()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = new(clock.Read, steps);
            Assert.AreEqual(0, clock.ReadCount);
            steps.Draining = () =>
            {
                Assert.AreEqual(0, scheduler.Progress);
                Assert.AreEqual(3, scheduler.SystemTime);
            };

            scheduler.Advance(0);

            Assert.AreEqual(1, clock.ReadCount);
            CollectionAssert.AreEqual(new[] { "drain" }, steps.Events);
        }

        [Test]
        public void Advance_DetailedPhases_DrainsBeforeDispatchSystemsAndMovement()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);
            clock.Advance(17);
            scheduler.Advance(1);
            clock.Advance(983);
            scheduler.Advance(1);
            scheduler.Advance(0);

            CollectionAssert.AreEqual(
                new[] { "drain", "move:17", "drain", "dispatch:3", "move:983", "drain", "systems" },
                steps.Events
            );
            Assert.AreEqual(4, scheduler.Progress);
            Assert.AreEqual(8, scheduler.SystemTime);
        }

        [Test]
        public void Advance_LongFrame_AdmitsOneCoarseTickAndRemainderMovement()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);

            clock.Advance(2500);
            scheduler.Advance(1000);

            Assert.AreEqual(4, scheduler.Progress);
            Assert.AreEqual(500, steps.Movement[0].Elapsed);
            Assert.AreEqual(1000, steps.Movement[0].Start);
        }

        [Test]
        public void Advance_ExactWholeSecond_DoesNotMove()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);

            clock.Advance(1000);
            scheduler.Advance(400);

            Assert.AreEqual(4, scheduler.Progress);
            Assert.IsEmpty(steps.Movement);
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void Advance_ZeroOrUnorderedFrame_StillAdvancesCoarsePhaseWithoutSecondSample(
            float frame
        )
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);
            int before = clock.ReadCount;

            clock.Advance(1500);
            scheduler.Advance(frame);

            Assert.AreEqual(4, scheduler.Progress);
            Assert.AreEqual(before + 1, clock.ReadCount);
            Assert.IsEmpty(steps.Movement);
        }

        [TestCase(-1f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Advance_OrderedNonzeroFrame_UsesClockRatherThanFrameMagnitude(float frame)
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);

            clock.Advance(25);
            scheduler.Advance(frame);

            Assert.AreEqual(25, steps.Movement[0].Elapsed);
        }

        [Test]
        public void Advance_SuspendedCalls_RetainsBothClockBases()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);

            clock.Advance(2500);
            clock.Advance(17);
            scheduler.Advance(6.8f);

            Assert.AreEqual(4, scheduler.Progress);
            Assert.AreEqual(517, steps.Movement[0].Elapsed);
        }

        [Test]
        public void Advance_ClockWrap_PreservesUnsignedDeltaAndFloatStartTimestamp()
        {
            ManualClock clock = new(uint.MaxValue - 3015);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);
            for (int frame = 0; frame < 30; frame++)
            {
                clock.Advance(100);
                scheduler.Advance(1);
            }
            steps.Movement.Clear();

            clock.Advance(32);
            scheduler.Advance(1);

            Assert.AreEqual(32, steps.Movement[0].Elapsed);
            Assert.AreEqual(-16, steps.Movement[0].Start);
        }

        [Test]
        public void Advance_UnadmittedMovement_RetainsTimeForLaterOrderedFrame()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);
            clock.Advance(25);
            scheduler.Advance(0);

            clock.Advance(10);
            scheduler.Advance(1);

            Assert.AreEqual(35, steps.Movement[0].Elapsed);
        }

        [Test]
        public void Advance_ClockChangesDuringCommands_UsesSeparateContinuousSample()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);
            steps.Draining = () => clock.Advance(3);

            clock.Advance(25);
            scheduler.Advance(1);

            Assert.AreEqual(28, steps.Movement[0].Elapsed);
        }

        [Test]
        public void Advance_EndingTicks_CompletesAtFourthBoundaryBeforeFurtherProgress()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);
            steps.IsRunning = false;
            for (int tick = 0; tick < 3; tick++)
            {
                clock.Advance(1000);
                Assert.IsFalse(scheduler.Advance(1));
                scheduler.Advance(0);
            }
            clock.Advance(1000);

            Assert.IsTrue(scheduler.Advance(1));

            Assert.AreEqual(4, steps.EndingTicks);
            Assert.AreEqual(6, scheduler.Progress);
            Assert.IsEmpty(steps.Movement);
        }

        [Test]
        public void Advance_TerminalResult_CompletesOnlyAfterDrainingAtReadyBoundary()
        {
            ManualClock clock = new(1000);
            Steps steps = new();
            TacticalCombatScheduler scheduler = CreateRunning(clock, steps);
            steps.HasTerminalResult = true;
            Assert.IsFalse(scheduler.Advance(0));
            steps.Events.Clear();
            clock.Advance(1000);

            Assert.IsTrue(scheduler.Advance(1));

            Assert.AreEqual(3, scheduler.Progress);
            CollectionAssert.AreEqual(new[] { "drain" }, steps.Events);
        }

        /// <summary>
        /// Reaches the public running phase through its actual startup and whole-second progression.
        /// </summary>
        /// <param name="clock">The manually controlled millisecond provider.</param>
        /// <param name="steps">The recorded encounter callbacks.</param>
        /// <returns>The scheduler at the first admitted movement progress interval.</returns>
        private static TacticalCombatScheduler CreateRunning(ManualClock clock, Steps steps)
        {
            TacticalCombatScheduler scheduler = new(clock.Read, steps);
            scheduler.Advance(0);
            scheduler.Advance(0);
            for (int tick = 0; tick < 3; tick++)
            {
                clock.Advance(1000);
                scheduler.Advance(1);
                scheduler.Advance(0);
            }
            steps.Events.Clear();
            steps.Movement.Clear();
            return scheduler;
        }

        private sealed class ManualClock
        {
            public uint Now { get; private set; }
            public int ReadCount { get; private set; }

            /// <summary>
            /// Starts the manual provider at an explicitly chosen timestamp.
            /// </summary>
            /// <param name="now">The initial wrapping timestamp.</param>
            public ManualClock(uint now) => Now = now;

            /// <summary>
            /// Samples the current timestamp and records its provider call count.
            /// </summary>
            /// <returns>The current timestamp.</returns>
            public uint Read()
            {
                ReadCount++;
                return Now;
            }

            /// <summary>
            /// Moves the clock independently from the scheduler.
            /// </summary>
            /// <param name="elapsed">The unsigned elapsed milliseconds.</param>
            public void Advance(uint elapsed) => Now = unchecked(Now + elapsed);
        }

        private sealed class Steps : ITacticalCombatSchedulerSteps
        {
            public bool IsRunning { get; set; } = true;
            public bool HasTerminalResult { get; set; }
            public int EndingTicks { get; private set; }
            public Action Draining { get; set; }
            public List<string> Events { get; } = new();
            public List<(float Start, float Elapsed)> Movement { get; } = new();

            /// <summary>
            /// Records command processing before invoking the explicit test-side effect.
            /// </summary>
            public void DrainCommands()
            {
                Events.Add("drain");
                Draining?.Invoke();
            }

            /// <summary>
            /// Records the admitted command interval.
            /// </summary>
            /// <param name="iteration">The completed interval.</param>
            public void DispatchCommands(uint iteration) => Events.Add("dispatch:" + iteration);

            /// <summary>
            /// Records the ordered one-second operations.
            /// </summary>
            public void AdvanceSystems() => Events.Add("systems");

            /// <summary>
            /// Consumes a terminal interval and completes the fourth one.
            /// </summary>
            /// <returns>Whether four whole-second ending intervals have completed.</returns>
            public bool AdvanceEndingTick() => ++EndingTicks >= 4;

            /// <summary>
            /// Records an admitted continuous interval.
            /// </summary>
            /// <param name="startMilliseconds">The float interval start timestamp.</param>
            /// <param name="elapsedMilliseconds">The admitted duration.</param>
            public void AdvanceMovement(float startMilliseconds, float elapsedMilliseconds)
            {
                Movement.Add((startMilliseconds, elapsedMilliseconds));
                Events.Add("move:" + elapsedMilliseconds);
            }
        }
    }
}
