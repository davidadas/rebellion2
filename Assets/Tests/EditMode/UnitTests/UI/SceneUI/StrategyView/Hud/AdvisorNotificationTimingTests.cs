using NUnit.Framework;
using Rebellion.Game.Messages;
using Rebellion.Game.UIState;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class AdvisorNotificationTimingTests
    {
        private PlayerUIState state;
        private AdvisorNotificationTiming timing;

        /// <summary>
        /// Creates isolated pending state and its notification clock for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            state = new PlayerUIState();
            timing = new AdvisorNotificationTiming(state);
        }

        [Test]
        public void RecordArrival_ClockNotSampled_UsesInitialZeroCounter()
        {
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            timing.SampleClock(51);

            Assert.IsFalse(timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
        }

        [TestCase(62, true)]
        [TestCase(63, false)]
        public void TryConsume_PendingDeadlineBoundary_IncludesFinalTick(
            int currentTick,
            bool expected
        )
        {
            timing.SampleClock(12);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            timing.SampleClock(currentTick);

            Assert.AreEqual(expected, timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
            Assert.IsFalse(timing.HasPending(AdvisorNotificationType.ChatReceived));
        }

        [Test]
        public void RecordArrival_RepeatedPendingArrival_ExtendsDeadlineFromLastSample()
        {
            timing.SampleClock(12);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            timing.SampleClock(40);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            timing.SampleClock(80);

            Assert.IsTrue(timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        public void TryConsume_InitialCooldownBoundary_RequiresPositiveSampledCounter(
            int tick,
            bool expected
        )
        {
            timing.SampleClock(tick);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);

            Assert.AreEqual(expected, timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
        }

        [TestCase(61, false)]
        [TestCase(62, true)]
        public void TryConsume_RepeatCooldownBoundary_RequiresStrictlyLaterTick(
            int tick,
            bool expected
        )
        {
            timing.SampleClock(1);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            Assert.IsTrue(timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
            timing.SampleClock(tick);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);

            Assert.AreEqual(expected, timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
        }

        [Test]
        public void TryConsume_CooldownBlockedPending_DiscardsInsteadOfDeferring()
        {
            timing.SampleClock(1);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            timing.TryConsume(AdvisorNotificationType.ChatReceived, 60);
            timing.SampleClock(40);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            Assert.IsFalse(timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
            timing.SampleClock(62);

            Assert.IsFalse(timing.HasPending(AdvisorNotificationType.ChatReceived));
            Assert.IsFalse(timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
        }

        [Test]
        public void Constructor_SerializedPendingState_RestoresDeadlineAndResetsCooldown()
        {
            timing.SampleClock(1);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            Assert.IsTrue(timing.TryConsume(AdvisorNotificationType.ChatReceived, 60));
            timing.SampleClock(20);
            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);
            PlayerUIState restored = SerializationHelper.Deserialize<PlayerUIState>(
                SerializationHelper.Serialize(state)
            );
            AdvisorNotificationTiming reloaded = new AdvisorNotificationTiming(restored);
            reloaded.SampleClock(20);

            Assert.IsTrue(reloaded.HasPending(AdvisorNotificationType.ChatReceived));
            Assert.IsTrue(reloaded.TryConsume(AdvisorNotificationType.ChatReceived, 60));
        }

        [Test]
        public void RecordArrival_SeparatePlayerState_DoesNotSharePendingSlot()
        {
            AdvisorNotificationTiming other = new AdvisorNotificationTiming(new PlayerUIState());

            timing.RecordArrival(AdvisorNotificationType.ChatReceived, 50);

            Assert.IsFalse(other.HasPending(AdvisorNotificationType.ChatReceived));
        }
    }
}
