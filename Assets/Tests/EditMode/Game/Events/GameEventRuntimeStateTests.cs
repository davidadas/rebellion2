using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventRuntimeStateTests
    {
        [Test]
        public void GetState_NewEvent_ReturnsUnexhaustedState()
        {
            GameEventRuntimeState state = new GameEventRuntimeState();

            GameEventState result = state.GetState("EVENT1");

            Assert.IsFalse(result.IsExhausted);
        }

        [Test]
        public void GetState_SameEvent_ReturnsCanonicalState()
        {
            GameEventRuntimeState state = new GameEventRuntimeState();

            GameEventState first = state.GetState("EVENT1");
            GameEventState second = state.GetState("EVENT1");

            Assert.AreSame(first, second);
        }
    }
}
