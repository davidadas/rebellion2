using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventTests
    {
        [Test]
        public void CanExecute_TriggerCountReached_ReturnsFalse()
        {
            GameEvent gameEvent = new GameEvent { TriggerCount = 3 };
            GameEventState state = new GameEventState { ExecutionCount = 3 };

            bool result = gameEvent.CanExecute(state);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanExecute_UnlimitedEvent_ReturnsTrue()
        {
            GameEvent gameEvent = new GameEvent();
            GameEventState state = new GameEventState { ExecutionCount = 100 };

            bool result = gameEvent.CanExecute(state);

            Assert.IsTrue(result);
        }
    }
}
