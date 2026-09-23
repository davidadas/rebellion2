using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class GameTickProcessorTests
    {
        private GameRoot _game;
        private GameSession _session;
        private GameTickProcessor _tick;
        private Func<IEnumerable<GameResult>, bool, List<GameResult>> _processResults;

        /// <summary>
        /// Creates a prepared game and the runtime dependencies used by tick scheduling.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.SetGameSpeed(TickSpeed.Fast);
            _session = new GameSession(_game, TestGameData.Create(_game.Config));
            _processResults = (results, _) => _session.Results.Publish(results);
            _tick = CreateTickProcessor();
        }

        /// <summary>
        /// Releases the result subscriptions owned by the test runtime.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
        }

        [Test]
        public void ProcessTick_PausedGame_DoesNotAdvanceTick()
        {
            _game.SetGameSpeed(TickSpeed.Paused);

            _tick.ProcessTick();

            Assert.AreEqual(0, _game.CurrentTick);
        }

        [Test]
        public void ProcessTick_CompletedTick_NotifiesOnce()
        {
            int completions = 0;
            _tick.TickCompleted += () => completions++;

            _tick.ProcessTick();

            Assert.AreEqual(1, completions);
            Assert.AreEqual(1, _game.CurrentTick);
        }

        [Test]
        public void ProcessTickIncrementally_DisposedBeforeCompletion_ReleasesBusyGuard()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            IEnumerator tick = _tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());

            (tick as IDisposable)?.Dispose();

            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTick_CompletionObserverThrows_RestoresIdleState()
        {
            InvalidOperationException expected = new("observer failure");
            _tick.TickCompleted += () => throw expected;

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                _tick.ProcessTick
            );

            Assert.AreSame(expected, actual);
            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTick_ResultDeliveryThrows_ReleasesBusyGuard()
        {
            InvalidOperationException expected = new("delivery failure");
            _processResults = (_, _) => throw expected;
            _tick = CreateTickProcessor();

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                _tick.ProcessTick
            );

            Assert.AreSame(expected, actual);
            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void Reset_SuspendedIterator_PreservesBusyGuardUntilDisposal()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            IEnumerator tick = _tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());

            _tick.Reset();

            Assert.IsTrue(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
            (tick as IDisposable)?.Dispose();
            Assert.IsFalse(_tick.IsBusy);
        }

        [Test]
        public void ReconcileLoadedState_NoEncounter_DoesNotAdvanceTick()
        {
            _tick.ReconcileLoadedState();

            Assert.AreEqual(0, _game.CurrentTick);
            Assert.IsTrue(_tick.IsSettled);
        }

        /// <summary>
        /// Connects tick scheduling to the current session's services.
        /// </summary>
        /// <returns>The tick processor under test.</returns>
        private GameTickProcessor CreateTickProcessor()
        {
            GameTickProcessor tick = new GameTickProcessor(_processResults, _ => { });
            tick.ConnectRuntime(_session);
            return tick;
        }
    }
}
