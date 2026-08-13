using System;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventTests
    {
        [Test]
        public void GetTriggerCount_PositiveInteger_ReturnsCount()
        {
            GameEvent gameEvent = new GameEvent { TriggerCount = 3 };

            int? result = gameEvent.GetTriggerCount();

            Assert.AreEqual(3, result);
        }

        [Test]
        public void GetTriggerCount_Omitted_ReturnsNull()
        {
            GameEvent gameEvent = new GameEvent();

            int? result = gameEvent.GetTriggerCount();

            Assert.IsNull(result);
        }

        [Test]
        public void GetTriggerCount_InvalidValue_ThrowsInvalidOperationException()
        {
            GameEvent gameEvent = new GameEvent { TriggerCount = 0 };

            TestDelegate getTriggerCount = () => gameEvent.GetTriggerCount();

            Assert.Throws<InvalidOperationException>(getTriggerCount);
        }
    }
}
