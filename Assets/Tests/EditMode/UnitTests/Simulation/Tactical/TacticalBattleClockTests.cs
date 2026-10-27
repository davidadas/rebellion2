using System;
using NUnit.Framework;
using Rebellion.Simulation.Tactical;

namespace Rebellion.Tests.Simulation.Tactical
{
    [TestFixture]
    public sealed class TacticalBattleClockTests
    {
        /// <summary>
        /// Checks a new tactical clock advances at normal speed.
        /// </summary>
        [Test]
        public void Speed_NewClock_DefaultsToNormal()
        {
            TacticalBattleClock clock = new TacticalBattleClock();

            Assert.AreEqual(TacticalBattleSpeed.Normal, clock.Speed);
            Assert.AreEqual(1f, clock.TimeScale);
            Assert.IsFalse(clock.IsPaused);
        }

        /// <summary>
        /// Checks every supported selection maps real elapsed time to the intended battle time.
        /// </summary>
        /// <param name="speed">The selected tactical speed.</param>
        /// <param name="expected">The expected elapsed battle time for two real seconds.</param>
        [TestCase(TacticalBattleSpeed.Paused, 0f)]
        [TestCase(TacticalBattleSpeed.Half, 1f)]
        [TestCase(TacticalBattleSpeed.Normal, 2f)]
        [TestCase(TacticalBattleSpeed.Double, 4f)]
        [TestCase(TacticalBattleSpeed.Quadruple, 8f)]
        public void ScaleDeltaTime_SupportedSpeed_ReturnsScaledBattleTime(
            TacticalBattleSpeed speed,
            float expected
        )
        {
            TacticalBattleClock clock = new TacticalBattleClock();
            clock.SetSpeed(speed);

            Assert.AreEqual(expected, clock.ScaleDeltaTime(2f));
            Assert.AreEqual(speed == TacticalBattleSpeed.Paused, clock.IsPaused);
        }

        /// <summary>
        /// Checks unsupported enum values cannot enter the tactical clock.
        /// </summary>
        [Test]
        public void SetSpeed_UnsupportedValue_ThrowsArgumentOutOfRangeException()
        {
            TacticalBattleClock clock = new TacticalBattleClock();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                clock.SetSpeed((TacticalBattleSpeed)99)
            );
        }

        /// <summary>
        /// Checks suspension pauses tactical time without replacing the player's selected speed.
        /// </summary>
        [Test]
        public void Suspend_RunningClock_PausesWithoutChangingSpeed()
        {
            TacticalBattleClock clock = new TacticalBattleClock();
            clock.SetSpeed(TacticalBattleSpeed.Double);

            clock.Suspend();

            Assert.IsTrue(clock.IsPaused);
            Assert.AreEqual(TacticalBattleSpeed.Double, clock.Speed);
            Assert.AreEqual(0f, clock.ScaleDeltaTime(1800f));
        }

        /// <summary>
        /// Checks resuming discards the stale focus-loss sample before normal scaling continues.
        /// </summary>
        [Test]
        public void Resume_SuspendedClock_DiscardsOnlyFirstElapsedTime()
        {
            TacticalBattleClock clock = new TacticalBattleClock();
            clock.SetSpeed(TacticalBattleSpeed.Double);
            clock.Suspend();

            clock.Resume();

            Assert.IsFalse(clock.IsPaused);
            Assert.AreEqual(0f, clock.ScaleDeltaTime(1800f));
            Assert.AreEqual(2f, clock.ScaleDeltaTime(1f));
        }

        /// <summary>
        /// Checks focus restoration cannot override a pause explicitly selected by the player.
        /// </summary>
        [Test]
        public void Resume_PlayerPausedClock_RemainsPaused()
        {
            TacticalBattleClock clock = new TacticalBattleClock();
            clock.SetSpeed(TacticalBattleSpeed.Paused);
            clock.Suspend();

            clock.Resume();

            Assert.IsTrue(clock.IsPaused);
            Assert.AreEqual(TacticalBattleSpeed.Paused, clock.Speed);
            Assert.AreEqual(0f, clock.ScaleDeltaTime(1800f));
        }

        /// <summary>
        /// Checks invalid real elapsed time cannot advance a tactical battle.
        /// </summary>
        /// <param name="elapsed">The invalid elapsed time.</param>
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ScaleDeltaTime_InvalidElapsedTime_ThrowsArgumentOutOfRangeException(
            float elapsed
        )
        {
            TacticalBattleClock clock = new TacticalBattleClock();

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.ScaleDeltaTime(elapsed));
        }
    }
}
