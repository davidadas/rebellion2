using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Results;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class GameEventObserverTests
    {
        [Test]
        public void Constructor_NullCommands_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GameEventObserver(null));
        }

        [Test]
        public void Connect_NullResultBus_ThrowsArgumentNullException()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEventObserver observer = new(new GameEventCommands(game, new FixedRNG()));

            Assert.Throws<ArgumentNullException>(() => observer.Connect(null));
        }

        [Test]
        public void Connect_AlreadyConnected_ThrowsInvalidOperationException()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEventObserver observer = new(new GameEventCommands(game, new FixedRNG()));
            observer.Connect(new GameResultBus());

            Assert.Throws<InvalidOperationException>(() => observer.Connect(new GameResultBus()));
        }

        [Test]
        public void Connect_MatchingResult_ActivatesTriggeredEvent()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEvent gameEvent = CreateTriggeredEvent();
            game.GetEventPool().Add(gameEvent);
            GameEventObserver observer = new(new GameEventCommands(game, new FixedRNG()));
            GameResultBus results = new();
            observer.Connect(results);

            results.Publish(new DuelResult());

            Assert.AreEqual(1, game.EventRuntime.GetVariable("triggered-event-fired"));
            Assert.IsFalse(game.GetEventPool().Contains(gameEvent));
        }

        [Test]
        public void Dispose_ConnectedObserver_StopsTriggeredEvents()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEvent gameEvent = CreateTriggeredEvent();
            game.GetEventPool().Add(gameEvent);
            GameEventObserver observer = new(new GameEventCommands(game, new FixedRNG()));
            GameResultBus results = new();
            observer.Connect(results);
            observer.Dispose();

            results.Publish(new DuelResult());

            Assert.AreEqual(0, game.EventRuntime.GetVariable("triggered-event-fired"));
            Assert.IsTrue(game.GetEventPool().Contains(gameEvent));
        }

        /// <summary>
        /// Creates a one-shot result-triggered event for observer boundary tests.
        /// </summary>
        /// <returns>The event definition.</returns>
        private static GameEvent CreateTriggeredEvent()
        {
            return new GameEvent
            {
                InstanceID = "triggered-event",
                MaximumActivations = 1,
                Triggers = new List<GameEventTrigger> { new DuelCompletedTrigger() },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "triggered-event-fired", Operand = 1 },
                },
            };
        }
    }
}
