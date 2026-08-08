using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class GameEventSystemTests
    {
        private GameRoot _game;
        private GameEventSystem _system;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _system = new GameEventSystem(_game, new FixedRandomProvider(new[] { 0.5 }));
        }

        [Test]
        public void ProcessEvents_UnmetOneShotEvent_RemainsPending()
        {
            GameEvent gameEvent = CreateTickEvent("PENDING", targetTick: 10, repeatable: false);
            _game.CurrentTick = 9;
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.Contains(gameEvent, _game.EventPool);
            Assert.IsFalse(_game.IsEventComplete(gameEvent.InstanceID));
        }

        [Test]
        public void ProcessEvents_MetOneShotEvent_CompletesAndLeavesPool()
        {
            GameEvent gameEvent = CreateTickEvent("ONE_SHOT", targetTick: 10, repeatable: false);
            _game.CurrentTick = 11;
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
            Assert.IsTrue(_game.IsEventComplete(gameEvent.InstanceID));
        }

        [Test]
        public void ProcessEvents_MetRepeatableEvent_CompletesAndRemainsActive()
        {
            GameEvent gameEvent = CreateTickEvent("REPEATABLE", targetTick: 10, repeatable: true);
            _game.CurrentTick = 11;
            _game.EventPool.Add(gameEvent);

            _system.ProcessEvents(_game.EventPool);

            Assert.Contains(gameEvent, _game.EventPool);
            Assert.IsTrue(_game.IsEventComplete(gameEvent.InstanceID));
        }

        [Test]
        public void ProcessEvents_InitialRandomDelay_WaitsUntilRolledAbsoluteTick()
        {
            GameEvent gameEvent = CreateTickEvent("DELAYED", targetTick: 0, repeatable: false);
            gameEvent.InitialDelayTicks = 10;
            gameEvent.InitialDelayRandomTicks = 4;
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 11;
            _system.ProcessEvents(_game.EventPool);
            Assert.Contains(gameEvent, _game.EventPool);

            _game.CurrentTick = 12;
            _system.ProcessEvents(_game.EventPool);
            Assert.IsFalse(_game.EventPool.Contains(gameEvent));
            Assert.AreEqual(12, _game.GetEventState(gameEvent.InstanceID).LastExecutionTick);
        }

        [Test]
        public void ProcessEvents_RepeatDelay_PreventsExecutionUntilCooldownExpires()
        {
            GameEvent gameEvent = CreateTickEvent("COOLDOWN", targetTick: 0, repeatable: true);
            gameEvent.RepeatDelayTicks = 5;
            _game.EventPool.Add(gameEvent);

            _game.CurrentTick = 1;
            _system.ProcessEvents(_game.EventPool);
            _game.CurrentTick = 5;
            _system.ProcessEvents(_game.EventPool);
            Assert.AreEqual(1, _game.GetEventState(gameEvent.InstanceID).ExecutionCount);

            _game.CurrentTick = 6;
            _system.ProcessEvents(_game.EventPool);
            Assert.AreEqual(2, _game.GetEventState(gameEvent.InstanceID).ExecutionCount);
        }

        private static GameEvent CreateTickEvent(string instanceId, int targetTick, bool repeatable)
        {
            return new GameEvent
            {
                InstanceID = instanceId,
                IsRepeatable = repeatable,
                Conditionals = new List<GameConditional>
                {
                    new TickCountConditional
                    {
                        ConditionalType = "GreaterThan",
                        ConditionalValue = targetTick.ToString(),
                    },
                },
            };
        }
    }
}
