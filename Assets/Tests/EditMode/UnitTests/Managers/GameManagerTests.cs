using System;
using System.Collections;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Simulation;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerTests
    {
        /// <summary>Verifies each configured speed through elapsed-time readiness.</summary>
        /// <param name="speed">The selected game speed.</param>
        /// <param name="interval">The configured interval in seconds.</param>
        [TestCase(TickSpeed.Fast, 2.5f)]
        [TestCase(TickSpeed.Medium, 12.5f)]
        [TestCase(TickSpeed.Slow, 90.5f)]
        [TestCase(TickSpeed.VerySlow, 120.5f)]
        public void SetGameSpeed_ConfiguredInterval_ControlsTickReadiness(
            TickSpeed speed,
            float interval
        )
        {
            GameConfig config = TestConfig.Create();
            config.GameSpeed.FastTickIntervalSeconds = 2.5f;
            config.GameSpeed.MediumTickIntervalSeconds = 12.5f;
            config.GameSpeed.SlowTickIntervalSeconds = 90.5f;
            config.GameSpeed.VerySlowTickIntervalSeconds = 120.5f;
            GameRoot game = new GameRoot(config);
            using GameSession session = new GameSession(game, TestGameData.Create(config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(speed);

            Assert.IsFalse(manager.TryAdvanceTickTimer(interval / 2f));
            Assert.IsTrue(manager.TryAdvanceTickTimer(interval / 2f));
        }

        /// <summary>Verifies completed interval and processes tick and raises tick completed.</summary>
        [Test]
        public void TryAdvanceTickTimer_CompletedInterval_ProcessesTickAndRaisesTickCompleted()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "FACTION", DisplayName = "Faction" });
            GameSession session = TestContent.CreateGameSession(game);
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            int completedTicks = 0;
            session.Tick.TickCompleted += () => completedTicks++;

            if (manager.TryAdvanceTickTimer(config.GameSpeed.FastTickIntervalSeconds))
                session.Tick.ProcessTick();

            Assert.AreEqual(1, game.CurrentTick);
            Assert.AreEqual(1, completedTicks);
        }

        /// <summary>Verifies below completed interval and does not process tick.</summary>
        [Test]
        public void TryAdvanceTickTimer_BelowCompletedInterval_DoesNotProcessTick()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            GameSession session = TestContent.CreateGameSession(game);
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            int completedTicks = 0;
            session.Tick.TickCompleted += () => completedTicks++;

            if (manager.TryAdvanceTickTimer(config.GameSpeed.FastTickIntervalSeconds / 2f))
                session.Tick.ProcessTick();

            Assert.AreEqual(0, game.CurrentTick);
            Assert.AreEqual(0, completedTicks);
        }

        /// <summary>
        /// Verifies that completion observers see a saveable state without permitting a nested tick.
        /// </summary>
        [Test]
        public void TryAdvanceTickTimer_InsideTickCompleted_RejectsTickWhileStateIsSettled()
        {
            GameRoot game = new(TestConfig.Create());
            GameSession session = new(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            bool? timerReady = null;
            bool? settled = null;
            session.Tick.TickCompleted += () =>
            {
                settled = session.Tick.IsSettled;
                timerReady = manager.TryAdvanceTickTimer(
                    game.Config.GameSpeed.FastTickIntervalSeconds
                );
            };

            session.Tick.ProcessTick();

            Assert.IsTrue(settled);
            Assert.IsFalse(timerReady);
        }

        /// <summary>
        /// Verifies that creating an iterator does not enter the tick until it is advanced.
        /// </summary>
        [Test]
        public void TryAdvanceTickTimer_UnstartedIterator_DoesNotBlockClock()
        {
            GameRoot game = new(TestConfig.Create());
            GameSession session = new(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            IEnumerator tick = session.Tick.ProcessTickIncrementally();

            bool ready = manager.TryAdvanceTickTimer(game.Config.GameSpeed.FastTickIntervalSeconds);
            (tick as IDisposable)?.Dispose();

            Assert.IsTrue(ready);
            Assert.AreEqual(0, game.CurrentTick);
        }

        /// <summary>
        /// Records that replacement clears scheduling state but leaves an active iterator's guard intact.
        /// </summary>
        [Test]
        public void Reset_SuspendedTick_KeepsClockBlockedUntilIteratorDisposal()
        {
            GameRoot game = new(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            GameSession session = new(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            IEnumerator tick = session.Tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());
            GameRoot replacement = new(game.Config);

            session.ReplaceGame(replacement);
            manager.Reset();
            manager.SetGameSpeed(TickSpeed.Fast);
            bool readyBeforeDisposal = manager.TryAdvanceTickTimer(
                game.Config.GameSpeed.FastTickIntervalSeconds
            );
            (tick as IDisposable)?.Dispose();
            bool readyAfterDisposal = manager.TryAdvanceTickTimer(
                game.Config.GameSpeed.FastTickIntervalSeconds
            );

            Assert.IsFalse(readyBeforeDisposal);
            Assert.IsTrue(readyAfterDisposal);
        }
    }
}
