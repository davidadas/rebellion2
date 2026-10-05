using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class GameEventTickProcessorTests
    {
        [Test]
        public void Constructor_NullCommands_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GameEventTickProcessor(null));
        }

        [Test]
        public void ProcessTick_NullGame_ThrowsArgumentNullException()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEventTickProcessor processor = new(new GameEventCommands(game, new FixedRNG()));

            Assert.Throws<ArgumentNullException>(() => processor.ProcessTick(null));
        }

        [Test]
        public void ProcessTick_EligibleScheduledEvent_ActivatesEvent()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            GameEvent gameEvent = new()
            {
                InstanceID = "scheduled-event",
                Schedule = new GameEventSchedule { At = new AtTick { Tick = 1 } },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "scheduled-event-fired", Operand = 1 },
                },
            };
            game.GetEventPool().Add(gameEvent);
            game.CurrentTick = 1;
            GameEventTickProcessor processor = new(new GameEventCommands(game, new FixedRNG()));

            processor.ProcessTick(game);

            Assert.AreEqual(1, game.EventRuntime.GetVariable("scheduled-event-fired"));
            Assert.IsFalse(game.GetEventPool().Contains(gameEvent));
        }
    }
}
