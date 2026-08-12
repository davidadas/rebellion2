using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventRuntimeStateTests
    {
        [Test]
        public void Complete_ValidEventID_AddsCompletedEventID()
        {
            GameEventRuntimeState state = new GameEventRuntimeState();

            state.Complete("EVENT1");

            Assert.IsTrue(state.CompletedEventIDs.Contains("EVENT1"));
        }

        [Test]
        public void IsComplete_UncompletedEvent_ReturnsFalse()
        {
            GameEventRuntimeState state = new GameEventRuntimeState();
            state.Complete("EVENT1");

            bool result = state.IsComplete("EVENT2");

            Assert.IsFalse(result);
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
