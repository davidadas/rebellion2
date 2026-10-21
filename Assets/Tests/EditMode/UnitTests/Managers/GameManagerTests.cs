using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Simulation;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerTests
    {
        /// <summary>
        /// Verifies multiple small work units share a frame without reordering them.
        /// </summary>
        [Test]
        public void AdvanceTick_AvailableBudget_CompletesOrderedWork()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Config.GameSpeed.TickWorkBudgetMilliseconds = 10000;
            var tick = new GameTickProcessor(
                (results, _) => new List<GameResult>(results),
                _ => { }
            );
            var manager = new GameManager(() => game, tick);
            List<int> completed = new();
            using var work = EnumerateWork(completed, 3).GetEnumerator();

            bool remains = manager.AdvanceTick(work);

            Assert.IsFalse(remains);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, completed);
        }

        /// <summary>
        /// Verifies disabled batching retains one work unit per call.
        /// </summary>
        [Test]
        public void AdvanceTick_ZeroBudget_AdvancesOneWorkUnit()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Config.GameSpeed.TickWorkBudgetMilliseconds = 0;
            var tick = new GameTickProcessor(
                (results, _) => new List<GameResult>(results),
                _ => { }
            );
            var manager = new GameManager(() => game, tick);
            List<int> completed = new();
            using var work = EnumerateWork(completed, 3).GetEnumerator();

            Assert.IsTrue(manager.AdvanceTick(work));
            CollectionAssert.AreEqual(new[] { 0 }, completed);
            Assert.IsTrue(manager.AdvanceTick(work));
            CollectionAssert.AreEqual(new[] { 0, 1 }, completed);
        }

        /// <summary>
        /// Verifies a tiny budget still makes progress and resumes the same ordered work.
        /// </summary>
        [Test]
        public void AdvanceTick_ExhaustedBudget_ResumesWithoutRepeatingWork()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Config.GameSpeed.TickWorkBudgetMilliseconds = double.Epsilon;
            var tick = new GameTickProcessor(
                (results, _) => new List<GameResult>(results),
                _ => { }
            );
            long timestamp = 0;
            var manager = new GameManager(
                () => game,
                tick,
                () => timestamp += System.Diagnostics.Stopwatch.Frequency
            );
            List<int> completed = new();
            using var work = EnumerateWork(completed, 2).GetEnumerator();

            Assert.IsTrue(manager.AdvanceTick(work));
            Assert.IsTrue(manager.AdvanceTick(work));
            Assert.IsFalse(manager.AdvanceTick(work));

            CollectionAssert.AreEqual(new[] { 0, 1 }, completed);
        }

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
            GameRoot game = TestGame.Create(config);
            using GameSession session = new GameSession(game, TestGameData.Create(config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(speed);

            Assert.IsFalse(manager.TryAdvanceTickTimer(interval / 2f));
            Assert.IsTrue(manager.TryAdvanceTickTimer(interval / 2f));
        }

        [Test]
        public void SetGameSpeed_PartialVerySlowTickChangedToMedium_PreservesFractionalProgress()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.VerySlowTickIntervalSeconds = 120f;
            config.GameSpeed.MediumTickIntervalSeconds = 10f;
            GameRoot game = TestGame.Create(config);
            GameTickProcessor tick = new GameTickProcessor(
                (results, _) => new List<GameResult>(results),
                _ => { }
            );
            GameManager manager = new GameManager(() => game, tick);
            manager.SetGameSpeed(TickSpeed.VerySlow);
            Assert.IsFalse(manager.TryAdvanceTickTimer(60f));

            manager.SetGameSpeed(TickSpeed.Medium);

            Assert.IsFalse(manager.TryAdvanceTickTimer(4.9f));
            Assert.IsTrue(manager.TryAdvanceTickTimer(0.1f));
        }

        [Test]
        public void SetGameSpeed_PartialVerySlowTickPausedThenMedium_PreservesFractionalProgress()
        {
            GameConfig config = new GameConfig();
            config.GameSpeed.VerySlowTickIntervalSeconds = 120f;
            config.GameSpeed.MediumTickIntervalSeconds = 10f;
            GameRoot game = TestGame.Create(config);
            GameTickProcessor tick = new GameTickProcessor(
                (results, _) => new List<GameResult>(results),
                _ => { }
            );
            GameManager manager = new GameManager(() => game, tick);
            manager.SetGameSpeed(TickSpeed.VerySlow);
            Assert.IsFalse(manager.TryAdvanceTickTimer(60f));
            manager.SetGameSpeed(TickSpeed.Paused);
            Assert.IsFalse(manager.TryAdvanceTickTimer(120f));

            manager.SetGameSpeed(TickSpeed.Medium);

            Assert.IsFalse(manager.TryAdvanceTickTimer(4.9f));
            Assert.IsTrue(manager.TryAdvanceTickTimer(0.1f));
        }

        [Test]
        public void TryAdvanceTickTimer_CompletedInterval_ProcessesTickAndRaisesTickCompleted()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
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

        [Test]
        public void TryAdvanceTickTimer_BelowCompletedInterval_DoesNotProcessTick()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
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

        [Test]
        public void TryAdvanceTickTimer_InsideTickCompleted_RejectsTickWhileStateIsSettled()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
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

        [Test]
        public void TryAdvanceTickTimer_UnstartedIterator_DoesNotBlockClock()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameSession session = new(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            IEnumerator tick = session.Tick.ProcessTickIncrementally();

            bool ready = manager.TryAdvanceTickTimer(game.Config.GameSpeed.FastTickIntervalSeconds);
            (tick as IDisposable)?.Dispose();

            Assert.IsTrue(ready);
            Assert.AreEqual(0, game.CurrentTick);
        }

        [Test]
        public void Reset_SuspendedTick_KeepsClockBlockedUntilIteratorDisposal()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            GameSession session = new(game, TestGameData.Create(game.Config));
            GameManager manager = new GameManager(() => session.Game, session.Tick);
            manager.SetGameSpeed(TickSpeed.Fast);
            IEnumerator tick = session.Tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());
            GameRoot replacement = TestGame.Create(game.Config);

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

        /// <summary>
        /// Records each completed work unit before its cooperative checkpoint.
        /// </summary>
        /// <param name="completed">The destination recording execution order.</param>
        /// <param name="count">Number of work units.</param>
        /// <returns>The ordered resumable work.</returns>
        private static IEnumerable<object> EnumerateWork(List<int> completed, int count)
        {
            for (int index = 0; index < count; index++)
            {
                completed.Add(index);
                yield return null;
            }
        }
    }
}
