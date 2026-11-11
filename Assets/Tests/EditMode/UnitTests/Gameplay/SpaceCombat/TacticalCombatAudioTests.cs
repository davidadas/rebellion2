using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.SpaceCombat;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatAudioTests
    {
        [Test]
        public void Enqueue_SingleVariant_DoesNotDrawFromBattleStream()
        {
            SystemRandomProvider random = new(42);
            Harness harness = new(random, 1, 100);
            harness.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            Assert.AreEqual(0, random.CallCount);
        }

        [Test]
        public void Enqueue_FullPlaybackCapacity_SelectsVariantBeforeDeferring()
        {
            SystemRandomProvider random = new(42);
            Harness harness = new(random, 4, 100);
            for (int index = 0; index < 6; index++)
                harness.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            Assert.AreEqual(5, harness.Playbacks.Count);
            Assert.AreEqual(6, random.CallCount);
        }

        [Test]
        public void Update_PlaybackCapacityAvailable_ReselectsPendingVariant()
        {
            SystemRandomProvider random = new(42);
            Harness harness = new(random, 4, 100);
            for (int index = 0; index < 6; index++)
                harness.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            harness.Now = 100;
            harness.Audio.Update();
            Assert.AreEqual(6, harness.Playbacks.Count);
            Assert.AreEqual(7, random.CallCount);
        }

        [Test]
        public void Update_PendingSoundExpired_DropsRequestWithoutAnotherDraw()
        {
            SystemRandomProvider random = new(42);
            Harness harness = new(random, 4, 1000);
            for (int index = 0; index < 6; index++)
                harness.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            harness.Now = 226;
            harness.Audio.Update();
            harness.Now = 1000;
            harness.Audio.Update();
            Assert.AreEqual(5, harness.Playbacks.Count);
            Assert.AreEqual(6, random.CallCount);
        }

        [Test]
        public void Enqueue_UnauthoredSound_DoesNotChangeBattleStream()
        {
            SystemRandomProvider random = new(42);
            Harness harness = new(random, 4, 100);
            harness.Audio.Enqueue(TacticalCombatSound.SmallExplosion);
            Assert.AreEqual(0, random.CallCount);
        }

        [Test]
        public void Enqueue_ReadySound_UsesOneOfItsAuthoredVariants()
        {
            SystemRandomProvider random = new(42);
            Harness harness = new(random, 4, 100);
            TacticalCombatSoundClip selected = null;
            harness.Audio.Playback = (clip, _) =>
            {
                selected = clip;
                return null;
            };
            harness.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            Assert.IsNotNull(selected);
            StringAssert.StartsWith("clip-", selected.Path);
        }

        [TestCase(TacticalCombatSound.HeavyHullHit, 17)]
        [TestCase(TacticalCombatSound.LaserHullHit, 18)]
        [TestCase(TacticalCombatSound.IonHullHit, 19)]
        [TestCase(TacticalCombatSound.Superlaser, 20)]
        [TestCase(TacticalCombatSound.FighterLaunch, 27)]
        public void Enqueue_TypedSound_SelectsItsAddressedNotification(
            TacticalCombatSound sound,
            int messageID
        )
        {
            TacticalCombatAudio audio = new(
                Array.Empty<TacticalCombatSoundDefinition>(),
                new SystemRandomProvider(42),
                new[] { CreateMessage(messageID, null, "addressed") }
            );
            string selected = null;
            audio.Playback = (clip, _) =>
            {
                selected = clip.Path;
                return null;
            };
            audio.Enqueue(sound);
            Assert.AreEqual("addressed", selected);
        }

        [Test]
        public void Constructor_FactionBanks_SelectsOnlyThePlayerFaction()
        {
            TacticalCombatAudio audio = new(
                Array.Empty<TacticalCombatSoundDefinition>(),
                new SystemRandomProvider(42),
                new[]
                {
                    CreateMessage(32, "FNALL1", "alliance"),
                    CreateMessage(32, "FNEMP1", "empire"),
                },
                "FNEMP1"
            );
            string selected = null;
            audio.Playback = (clip, _) =>
            {
                selected = clip.Path;
                return null;
            };
            audio.Notifications.Enqueue(32);
            Assert.AreEqual("empire", selected);
        }

        [Test]
        public void Update_PlaybackUnavailable_DoesNotReserveSyntheticDuration()
        {
            TacticalCombatAudio audio = new(
                Array.Empty<TacticalCombatSoundDefinition>(),
                new SystemRandomProvider(42),
                new[] { CreateMessage(32, null, "voice") }
            );
            int requested = 0;
            audio.Playback = (_, _) =>
            {
                requested++;
                return null;
            };
            audio.Notifications.Enqueue(32);
            audio.Notifications.Enqueue(32);
            Assert.AreEqual(2, requested);
        }

        [Test]
        public void SetPlaybackAvailable_Unavailable_StopsOwnedSoundBeforeStatusSweep()
        {
            Harness harness = new(new SystemRandomProvider(42), 1, 1000);
            uint queueID = harness.Audio.EnqueueNotification(0);

            harness.Audio.SetPlaybackAvailable(false);

            Assert.IsFalse(harness.Playbacks[0].IsPlaying);
            Assert.IsTrue(harness.Audio.Notifications.IsActive(queueID));
            harness.Audio.Update();
            Assert.IsFalse(harness.Audio.Notifications.IsActive(queueID));
        }

        [Test]
        public void SetPlaybackAvailable_Returning_DoesNotResumeReleasedSound()
        {
            Harness harness = new(new SystemRandomProvider(42), 1, 1000);
            harness.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            harness.Audio.SetPlaybackAvailable(false);

            harness.Audio.SetPlaybackAvailable(true);

            Assert.AreEqual(1, harness.Playbacks.Count);
            Assert.IsFalse(harness.Playbacks[0].IsPlaying);
            harness.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            Assert.AreEqual(2, harness.Playbacks.Count);
            Assert.IsTrue(harness.Playbacks[1].IsPlaying);
        }

        [Test]
        public void SetPlaybackAvailable_Unavailable_RetainsVariantDrawWithoutAcquiringPlayback()
        {
            SystemRandomProvider random = new(42);
            Harness harness = new(random, 4, 1000);
            harness.Audio.SetPlaybackAvailable(false);

            uint queueID = harness.Audio.EnqueueNotification(0);

            Assert.AreEqual(1, random.CallCount);
            Assert.IsEmpty(harness.Playbacks);
            Assert.IsTrue(harness.Audio.Notifications.IsActive(queueID));
            harness.Audio.Update();
            Assert.IsFalse(harness.Audio.Notifications.IsActive(queueID));
        }

        [Test]
        public void SetPlaybackAvailable_Returning_PreservesDeferredRequests()
        {
            Harness harness = new(new SystemRandomProvider(42), 1, 1000);
            for (int index = 0; index < 6; index++)
                harness.Audio.Enqueue(TacticalCombatSound.CapitalLaser);
            harness.Audio.SetPlaybackAvailable(false);

            harness.Audio.SetPlaybackAvailable(true);
            harness.Audio.Update();

            Assert.AreEqual(6, harness.Playbacks.Count);
            Assert.IsTrue(harness.Playbacks[5].IsPlaying);
            for (int index = 0; index < 5; index++)
                Assert.IsFalse(harness.Playbacks[index].IsPlaying);
        }

        [Test]
        public void SetPlaybackAvailable_SchedulingDisabled_PreservesEnabledFlag()
        {
            Harness harness = new(new SystemRandomProvider(42), 1, 1000);
            harness.Audio.Notifications.Enabled = false;

            harness.Audio.SetPlaybackAvailable(false);
            harness.Audio.SetPlaybackAvailable(true);

            Assert.IsFalse(harness.Audio.Notifications.Enabled);
        }

        /// <summary>
        /// Creates one addressed notification bank for an optional faction.
        /// </summary>
        /// <param name="messageID">The addressed message.</param>
        /// <param name="faction">The selected faction, or null for shared sounds.</param>
        /// <param name="path">The synthetic clip address.</param>
        /// <returns>The authored message record.</returns>
        private static TacticalCombatNotificationDefinition CreateMessage(
            int messageID,
            string faction,
            string path
        ) =>
            new()
            {
                MessageID = messageID,
                FactionInstanceID = faction,
                Variants = new() { new() { Path = path } },
            };

        private sealed class Harness
        {
            private readonly float _duration;
            public uint Now { get; set; }
            public TacticalCombatAudio Audio { get; }
            public List<Playback> Playbacks { get; } = new();

            /// <summary>
            /// Creates an authored effect bank with a controlled clock and actual owned playback states.
            /// </summary>
            /// <param name="random">The shared random-number provider.</param>
            /// <param name="count">The number of ordered clip variants.</param>
            /// <param name="duration">The duration reported by each synthetic playback.</param>
            public Harness(IRandomNumberProvider random, int count, float duration)
            {
                _duration = duration;
                List<TacticalCombatSoundClip> clips = new();
                for (int index = 0; index < count; index++)
                    clips.Add(new() { Path = "clip-" + index });
                Audio = new TacticalCombatAudio(
                    new[]
                    {
                        new TacticalCombatSoundDefinition
                        {
                            Kind = TacticalCombatSound.CapitalLaser,
                            Variants = clips,
                        },
                    },
                    random,
                    clock: () => Now
                )
                {
                    Playback = Play,
                };
            }

            /// <summary>
            /// Starts and retains the playback returned to the scheduler.
            /// </summary>
            /// <param name="clip">The selected clip.</param>
            /// <param name="loop">Whether the playback repeats.</param>
            /// <returns>The owned playback state.</returns>
            private ITacticalCombatNotificationPlayback Play(
                TacticalCombatSoundClip clip,
                bool loop
            )
            {
                Playback playback = new(_duration, loop, () => Now);
                Playbacks.Add(playback);
                return playback;
            }
        }

        private sealed class Playback : ITacticalCombatNotificationPlayback
        {
            private readonly float _duration;
            private readonly bool _loop;
            private readonly Func<uint> _clock;
            private readonly uint _started;
            private bool _stopped;
            public bool IsPlaying =>
                !_stopped && (_loop || unchecked(_clock() - _started) < _duration);

            /// <summary>
            /// Captures a playback's controlled start clock and duration.
            /// </summary>
            /// <param name="duration">The clip duration in milliseconds.</param>
            /// <param name="loop">Whether the clip repeats until stopped.</param>
            /// <param name="clock">The controlled unscaled clock.</param>
            public Playback(float duration, bool loop, Func<uint> clock)
            {
                _duration = duration;
                _loop = loop;
                _clock = clock;
                _started = clock();
            }

            /// <summary>
            /// Stops this owned playback.
            /// </summary>
            public void Stop() => _stopped = true;
        }
    }
}
