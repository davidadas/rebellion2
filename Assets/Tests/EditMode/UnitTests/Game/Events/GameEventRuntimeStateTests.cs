using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventRuntimeStateTests
    {
        [Test]
        public void GetState_NewEvent_ReturnsIncompleteState()
        {
            GameEventRuntimeState state = new GameEventRuntimeState();

            GameEventState result = state.GetState("EVENT1");

            Assert.IsFalse(result.IsComplete);
        }

        [Test]
        public void GetState_SameEvent_ReturnsCanonicalState()
        {
            GameEventRuntimeState state = new GameEventRuntimeState();

            GameEventState first = state.GetState("EVENT1");
            GameEventState second = state.GetState("EVENT1");

            Assert.AreSame(first, second);
        }

        [Test]
        public void GetVariable_MissingKey_ReturnsZero()
        {
            GameEventRuntimeState state = new GameEventRuntimeState();

            int value = state.GetVariable("unset");

            Assert.AreEqual(0, value);
        }
    }
}
