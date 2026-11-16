using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.SpaceCombat;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatNotificationsTests
    {
        private uint _clock;
        private bool _paused;
        private SystemRandomProvider _random;
        private TacticalCombatNotifications _notifications;
        private readonly List<Playback> _played = new();

        /// <summary>
        /// Creates a controlled presentation clock and an authored bank spanning all scheduling groups.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _clock = 0;
            _paused = false;
            _played.Clear();
            _random = new SystemRandomProvider(42);
            _notifications = new TacticalCombatNotifications(
                new[]
                {
                    Message(0, 4),
                    Message(1, 1),
                    Message(32, 1),
                    Message(33, 1),
                    Message(286, 1),
                    Message(287, 1),
                    Message(34, 1, true),
                },
                _random,
                () => _clock,
                () => _paused,
                Play
            );
        }

        /// <summary>
        /// Releases the owned notification playbacks between cases.
        /// </summary>
        [TearDown]
        public void TearDown() => _notifications.Dispose();

        [Test]
        public void Enqueue_FirstMessage_ReturnsIncrementedQueueHandle()
        {
            Assert.AreEqual(2u, _notifications.Enqueue(32));
        }

        [Test]
        public void Enqueue_IndependentGroups_UsesSeparateActiveCapacities()
        {
            for (int index = 0; index < 5; index++)
                _notifications.Enqueue(0);
            _notifications.Enqueue(32);
            _notifications.Enqueue(286);
            Assert.AreEqual(7, _played.Count);
        }

        [Test]
        public void Enqueue_FullEffectCapacity_DrawsVariantBeforeDeferring()
        {
            for (int index = 0; index < 6; index++)
                _notifications.Enqueue(0);
            Assert.AreEqual(6, _random.CallCount);
            Assert.AreEqual(5, _played.Count);
        }

        [Test]
        public void Update_DeferredEffectStarts_DrawsAnotherVariant()
        {
            for (int index = 0; index < 6; index++)
                _notifications.Enqueue(0);
            _played[0].IsPlaying = false;
            _notifications.Update();
            Assert.AreEqual(7, _random.CallCount);
            Assert.AreEqual(6, _played.Count);
        }

        [TestCase(225u, true)]
        [TestCase(226u, false)]
        public void Update_EffectPendingLifetime_UsesStrictExpiry(uint elapsed, bool shouldStart)
        {
            for (int index = 0; index < 6; index++)
                _notifications.Enqueue(1);
            _clock = elapsed;
            _played[0].IsPlaying = false;
            _notifications.Update();
            Assert.AreEqual(shouldStart ? 6 : 5, _played.Count);
        }

        [TestCase(8000u, false, true)]
        [TestCase(8001u, false, false)]
        [TestCase(32000u, true, true)]
        [TestCase(32001u, true, false)]
        public void Update_AdvisorPendingLifetime_ExtendsOnlyWhileSimulationPaused(
            uint elapsed,
            bool paused,
            bool shouldStart
        )
        {
            _notifications.Enqueue(32);
            _notifications.Enqueue(33);
            _clock = elapsed;
            _paused = paused;
            _played[0].IsPlaying = false;
            _notifications.Update();
            Assert.AreEqual(shouldStart ? 2 : 1, _played.Count);
        }

        [TestCase(3000u, true)]
        [TestCase(3001u, false)]
        public void Update_ThirdGroupPendingLifetime_UsesThreeSeconds(
            uint elapsed,
            bool shouldStart
        )
        {
            _notifications.Enqueue(286);
            _notifications.Enqueue(287);
            _clock = elapsed;
            _paused = true;
            _played[0].IsPlaying = false;
            _notifications.Update();
            Assert.AreEqual(shouldStart ? 2 : 1, _played.Count);
        }

        [Test]
        public void Update_DisabledScheduling_RemovesFinishedActiveWithoutStartingPending()
        {
            _notifications.Enqueue(32);
            _notifications.Enqueue(33);
            _notifications.Enabled = false;
            _played[0].IsPlaying = false;
            _notifications.Update();
            Assert.AreEqual(1, _played.Count);
            _notifications.Enabled = true;
            _notifications.Update();
            Assert.AreEqual(2, _played.Count);
        }

        [Test]
        public void Enqueue_DisabledScheduling_ReturnsZeroAndDoesNotSelectVariant()
        {
            _notifications.Enabled = false;
            Assert.AreEqual(0u, _notifications.Enqueue(0));
            Assert.AreEqual(0, _random.CallCount);
            _notifications.Enabled = true;
            Assert.AreEqual(3u, _notifications.Enqueue(0));
        }

        [Test]
        public void Enqueue_LoopWithAvailableCapacity_PassesLoopToPlaybackOwner()
        {
            uint queueID = _notifications.Enqueue(32, true);
            Assert.IsTrue(_played[0].Loop);
            _notifications.Stop(queueID);
            Assert.AreEqual(1, _played[0].StopCount);
        }

        [Test]
        public void Enqueue_LoopWithFullCapacity_DropsInsteadOfDeferring()
        {
            _notifications.Enqueue(32);
            Assert.AreEqual(0u, _notifications.Enqueue(33, true));
            _played[0].IsPlaying = false;
            _notifications.Update();
            Assert.AreEqual(1, _played.Count);
        }

        [Test]
        public void Stop_PendingHandle_LeavesPendingRequestOwnedByQueue()
        {
            _notifications.Enqueue(32);
            uint pending = _notifications.Enqueue(33);
            _notifications.Stop(pending);
            _played[0].IsPlaying = false;
            _notifications.Update();
            Assert.AreEqual(2, _played.Count);
        }

        [Test]
        public void Update_MultiplePendingMessages_PreservesAscendingQueueHandleOrder()
        {
            _notifications.Enqueue(32);
            _notifications.Enqueue(33);
            _notifications.Enqueue(32);
            _played[0].IsPlaying = false;
            _notifications.Update();
            _played[1].IsPlaying = false;
            _notifications.Update();
            Assert.That(
                _played.ConvertAll(playback => playback.Path),
                Is.EqualTo(new[] { "32-0", "33-0", "32-0" })
            );
        }

        [Test]
        public void Update_MissingResourceSlot_ReleasesFailedStartBeforeNextRequest()
        {
            uint missing = _notifications.Enqueue(34);
            Assert.AreEqual(2u, missing);
            Assert.IsEmpty(_played);
            _notifications.Enqueue(32);
            Assert.AreEqual(1, _played.Count);
        }

        [Test]
        public void Update_UnsignedClockWrap_PreservesElapsedPendingLifetime()
        {
            _clock = uint.MaxValue - 100;
            _notifications.Enqueue(32);
            _notifications.Enqueue(33);
            _clock = 100;
            _played[0].IsPlaying = false;
            _notifications.Update();
            Assert.AreEqual(2, _played.Count);
        }

        [Test]
        public void Dispose_ActiveAndPendingMessages_StopsOnlyActiveOwnedPlayback()
        {
            _notifications.Enqueue(32);
            _notifications.Enqueue(33);
            _notifications.Dispose();
            Assert.AreEqual(1, _played.Count);
            Assert.AreEqual(1, _played[0].StopCount);
        }

        [Test]
        public void ReleasePlaybacks_ActiveGroups_StopsResourcesBeforeRemovingQueueRecords()
        {
            uint effect = _notifications.Enqueue(0);
            uint advisor = _notifications.Enqueue(32);
            uint message = _notifications.Enqueue(286);

            _notifications.ReleasePlaybacks();

            Assert.AreEqual(3, _played.Count);
            foreach (Playback playback in _played)
            {
                Assert.AreEqual(1, playback.StopCount);
                Assert.IsFalse(playback.IsPlaying);
            }
            Assert.IsTrue(_notifications.IsActive(effect));
            Assert.IsTrue(_notifications.IsActive(advisor));
            Assert.IsTrue(_notifications.IsActive(message));
            _notifications.Update();
            Assert.IsFalse(_notifications.IsActive(effect));
            Assert.IsFalse(_notifications.IsActive(advisor));
            Assert.IsFalse(_notifications.IsActive(message));
        }

        [Test]
        public void ReleasePlaybacks_RepeatedRelease_DoesNotStopReleasedHandleTwice()
        {
            _notifications.Enqueue(32);

            _notifications.ReleasePlaybacks();
            _notifications.ReleasePlaybacks();
            _notifications.Dispose();

            Assert.AreEqual(1, _played[0].StopCount);
        }

        [Test]
        public void ReleasePlaybacks_DeferredRequest_PreservesPendingQueueAndSchedulingState()
        {
            _notifications.Enqueue(32);
            uint pending = _notifications.Enqueue(33);
            _notifications.Enabled = false;

            _notifications.ReleasePlaybacks();
            _notifications.Update();

            Assert.AreEqual(1, _played.Count);
            Assert.IsFalse(_notifications.IsActive(pending));
            Assert.IsFalse(_notifications.Enabled);
            _notifications.Enabled = true;
            _notifications.Update();
            Assert.AreEqual(2, _played.Count);
            Assert.IsTrue(_notifications.IsActive(pending));
            StringAssert.StartsWith("33-", _played[1].Path);
        }

        /// <summary>
        /// Starts and records an owned playback through the scheduler's public playback boundary.
        /// </summary>
        /// <param name="clip">The selected clip.</param>
        /// <param name="loop">Whether the playback repeats.</param>
        /// <returns>The playback activity handle.</returns>
        private ITacticalCombatNotificationPlayback Play(TacticalCombatSoundClip clip, bool loop)
        {
            Playback playback = new(clip.Path, loop);
            _played.Add(playback);
            return playback;
        }

        /// <summary>
        /// Creates a bounded message with explicit ordered variant slots.
        /// </summary>
        /// <param name="id">The notification ID.</param>
        /// <param name="count">The number of variants.</param>
        /// <param name="missing">Whether the source bank contains no resource in those slots.</param>
        /// <returns>The addressed message bank.</returns>
        private static TacticalCombatNotificationDefinition Message(
            int id,
            int count,
            bool missing = false
        )
        {
            TacticalCombatNotificationDefinition result = new() { MessageID = id };
            for (int index = 0; index < count; index++)
                result.Variants.Add(new() { Path = missing ? null : id + "-" + index });
            return result;
        }

        private sealed class Playback : ITacticalCombatNotificationPlayback
        {
            public string Path { get; }
            public bool Loop { get; }
            public bool IsPlaying { get; set; } = true;
            public int StopCount { get; private set; }

            /// <summary>
            /// Captures the playback request independently of the scheduler's active queue.
            /// </summary>
            /// <param name="path">The selected clip address.</param>
            /// <param name="loop">Whether playback repeats.</param>
            public Playback(string path, bool loop)
            {
                Path = path;
                Loop = loop;
            }

            /// <summary>
            /// Records stopping this owned playback.
            /// </summary>
            public void Stop()
            {
                StopCount++;
                IsPlaying = false;
            }
        }
    }
}
