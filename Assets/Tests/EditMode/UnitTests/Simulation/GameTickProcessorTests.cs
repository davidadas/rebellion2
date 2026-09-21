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

        /// <summary>
        /// Verifies that a paused game does not advance through tick scheduling.
        /// </summary>
        [Test]
        public void ProcessTick_PausedGame_DoesNotAdvanceTick()
        {
            _game.SetGameSpeed(TickSpeed.Paused);

            _tick.ProcessTick();

            Assert.AreEqual(0, _game.CurrentTick);
        }

        /// <summary>
        /// Verifies that completing the full phase sequence announces exactly one tick.
        /// </summary>
        [Test]
        public void ProcessTick_CompletedTick_NotifiesOnce()
        {
            int completions = 0;
            _tick.TickCompleted += () => completions++;

            _tick.ProcessTick();

            Assert.AreEqual(1, completions);
            Assert.AreEqual(1, _game.CurrentTick);
        }

        /// <summary>
        /// Verifies that iterator disposal releases the busy guard without completing the remaining phases.
        /// </summary>
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

        /// <summary>
        /// Verifies that completion observer exceptions propagate while the tick's guard is released.
        /// </summary>
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

        /// <summary>
        /// Verifies that result-delivery failures retain their propagation and cleanup boundary.
        /// </summary>
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

        /// <summary>
        /// Verifies that replacement reset does not clear the guard owned by a suspended iterator.
        /// </summary>
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

        /// <summary>
        /// Verifies that loaded combat reconciliation does not consume a game tick.
        /// </summary>
        [Test]
        public void ReconcileLoadedState_NoEncounter_DoesNotAdvanceTick()
        {
            _tick.ReconcileLoadedState();

            Assert.AreEqual(0, _game.CurrentTick);
            Assert.IsTrue(_tick.IsSettled);
        }

        /// <summary>
        /// Connects tick scheduling to explicit current-runtime dependencies without a session lookup API.
        /// </summary>
        /// <returns>The tick processor under test.</returns>
        private GameTickProcessor CreateTickProcessor()
        {
            return new GameTickProcessor(
                () => _game,
                () => _session.MessageCommands,
                () => _session.FactionAutomationCommands,
                () => _session.ResourceProductionCommands,
                () => _session.ManufacturingCommands,
                () => _session.MaintenanceCommands,
                () => _session.RecoveryCommands,
                () => _session.CaptiveCommands,
                () => _session.MovementCommands,
                () => _session.SpaceCombatCommands,
                () => _session.MissionCommands,
                () => _session.GameEventExecutor,
                () => _session.NamingCommands,
                () => _session.AIDirector,
                () => _session.BlockadeCommands,
                () => _session.PlanetaryControlCommands,
                () => _session.UprisingCommands,
                () => _session.ResearchCommands,
                () => _session.JediCommands,
                () => _session.VictoryCommands,
                _processResults,
                _ => { }
            );
        }
    }
}
