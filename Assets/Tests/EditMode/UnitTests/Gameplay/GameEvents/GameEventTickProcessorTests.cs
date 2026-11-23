using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Scripting;
using Rebellion.Gameplay.GameEvents;
using Rebellion.Gameplay.Scripting;

namespace Rebellion.Tests.Gameplay.GameEvents
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
            var eventRandom = new FixedRNG();
            GameEventTickProcessor processor = new(
                new GameEventCommands(game, eventRandom, new GameActionCommands(game, eventRandom))
            );

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
            var eventRandom = new FixedRNG();
            GameEventTickProcessor processor = new(
                new GameEventCommands(game, eventRandom, new GameActionCommands(game, eventRandom))
            );

            processor.ProcessTick(game);

            Assert.AreEqual(1, game.EventRuntime.GetVariable("scheduled-event-fired"));
            Assert.IsFalse(game.GetEventPool().Contains(gameEvent));
        }
    }
}
