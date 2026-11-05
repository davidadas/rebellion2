using NUnit.Framework;
using Rebellion.Game.GameEvents;

namespace Rebellion.Tests.Game.GameEvents
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
        public void GetState_SerializedScopedCompletion_PreservesTargetHistory()
        {
            GameEventRuntimeState runtime = new GameEventRuntimeState();
            GameEventState state = runtime.GetState("event");
            state.ActivationCount = 3;
            state.LastActivationTick = 30;
            state.ScopedStates.Add(
                "target",
                new GameEventState
                {
                    IsInitialized = true,
                    IsComplete = true,
                    ActivationCount = 2,
                    LastActivationTick = 20,
                }
            );

            GameEventRuntimeState restored = SerializationHelper.Deserialize<GameEventRuntimeState>(
                SerializationHelper.Serialize(runtime)
            );

            Assert.AreEqual(3, restored.GetState("event").ActivationCount);
            Assert.IsTrue(restored.GetState("event").ScopedStates["target"].IsComplete);
            Assert.AreEqual(2, restored.GetState("event").ScopedStates["target"].ActivationCount);
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
