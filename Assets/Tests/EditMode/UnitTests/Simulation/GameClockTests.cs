using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class GameClockTests
    {
        /// <summary>
        /// Verifies elapsed time reaches the active speed's configured interval.
        /// </summary>
        [Test]
        public void Advance_ConfiguredSpeedInterval_BecomesDueAtInterval()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.FastTickIntervalSeconds = 2.5f;
            GameClock clock = new GameClock(new GameRoot(config));
            clock.SetSpeed(TickSpeed.Fast);

            Assert.IsFalse(clock.Advance(2.49f, true));
            Assert.IsTrue(clock.Advance(0.01f, true));
        }

        /// <summary>
        /// Verifies paused time is not retained when the clock resumes.
        /// </summary>
        [Test]
        public void Advance_PausedSpeed_DoesNotAccumulateTime()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.FastTickIntervalSeconds = 1f;
            GameClock clock = new GameClock(new GameRoot(config));
            clock.SetSpeed(TickSpeed.Paused);

            Assert.IsFalse(clock.Advance(10f, true));

            clock.SetSpeed(TickSpeed.Fast);
            Assert.IsFalse(clock.Advance(0.5f, true));
        }

        /// <summary>
        /// Verifies blocked session time is not retained when advancement becomes available.
        /// </summary>
        [Test]
        public void Advance_BlockedSession_DoesNotAccumulateTime()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.FastTickIntervalSeconds = 1f;
            GameClock clock = new GameClock(new GameRoot(config));
            clock.SetSpeed(TickSpeed.Fast);

            Assert.IsFalse(clock.Advance(10f, false));
            Assert.IsFalse(clock.Advance(0.5f, true));
        }

        /// <summary>
        /// Verifies resetting the clock discards a partial interval.
        /// </summary>
        [Test]
        public void Reset_AccumulatedTime_RequiresFullNewInterval()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.FastTickIntervalSeconds = 1f;
            GameClock clock = new GameClock(new GameRoot(config));
            clock.SetSpeed(TickSpeed.Fast);
            Assert.IsFalse(clock.Advance(0.75f, true));

            clock.Reset();

            Assert.IsFalse(clock.Advance(0.75f, true));
        }
    }
}
