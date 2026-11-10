using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game.SpaceCombat;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Routes typed combat sounds and authored notifications through the encounter's shared scheduler.
    /// Playback handles remain owned by that scheduler instead of a separate duration-based sound queue.
    /// </summary>
    public sealed class TacticalCombatAudio : IDisposable
    {
        private readonly HashSet<int> _authoredMessages;
        private bool _playbackAvailable = true;
        public TacticalCombatNotifications Notifications { get; }
        public IReadOnlyList<TacticalCombatSoundClip> Clips { get; }
        public Func<
            TacticalCombatSoundClip,
            bool,
            ITacticalCombatNotificationPlayback
        > Playback { get; set; }

        /// <summary>
        /// Captures sound and selected-faction notification banks on the shared battle random stream.
        /// </summary>
        /// <param name="definitions">Typed combat sound variants.</param>
        /// <param name="random">The shared battle random-number stream.</param>
        /// <param name="notifications">Shared and faction-specific notification banks.</param>
        /// <param name="factionInstanceID">The faction whose spoken notification bank is selected.</param>
        /// <param name="isPaused">Whether the tactical simulation is currently paused.</param>
        /// <param name="clock">An optional unscaled millisecond clock for replay.</param>
        public TacticalCombatAudio(
            IReadOnlyList<TacticalCombatSoundDefinition> definitions,
            IRandomNumberProvider random,
            IReadOnlyList<TacticalCombatNotificationDefinition> notifications = null,
            string factionInstanceID = null,
            Func<bool> isPaused = null,
            Func<uint> clock = null
        )
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            Dictionary<int, TacticalCombatNotificationDefinition> messages = new();
            foreach (TacticalCombatSoundDefinition definition in definitions)
            {
                if (definition.Variants.Count == 0)
                    throw new InvalidDataException(
                        "A tactical sound requires at least one variant."
                    );
                foreach (TacticalCombatSoundClip clip in definition.Variants)
                    if (clip == null || string.IsNullOrWhiteSpace(clip.Path))
                        throw new InvalidDataException(
                            "A tactical sound requires an addressed clip."
                        );
                int messageID = GetMessageID(definition.Kind);
                messages.Add(
                    messageID,
                    new TacticalCombatNotificationDefinition
                    {
                        MessageID = messageID,
                        Variants = definition.Variants,
                    }
                );
            }
            if (notifications != null)
            {
                HashSet<int> selected = new();
                foreach (TacticalCombatNotificationDefinition definition in notifications)
                {
                    if (
                        !string.IsNullOrEmpty(definition.FactionInstanceID)
                        && definition.FactionInstanceID != factionInstanceID
                    )
                        continue;
                    if (!selected.Add(definition.MessageID))
                        throw new InvalidDataException(
                            "The selected notification bank contains duplicate message IDs."
                        );
                    messages[definition.MessageID] = definition;
                }
            }
            _authoredMessages = messages.Keys.ToHashSet();
            Clips = Array.AsReadOnly(
                messages.Values.SelectMany(message => message.Variants).ToArray()
            );
            Notifications = new TacticalCombatNotifications(
                messages.Values.ToArray(),
                random,
                clock ?? CreatePresentationClock(),
                isPaused ?? (() => false),
                Play
            );
        }

        /// <summary>
        /// Requests an authored typed combat sound as a one-shot notification.
        /// </summary>
        /// <param name="sound">The weapon or unit event requesting sound.</param>
        public void Enqueue(TacticalCombatSound sound)
        {
            EnqueueNotification(GetMessageID(sound));
        }

        /// <summary>
        /// Requests an authored notification through the selected content bank's shared scheduler.
        /// </summary>
        /// <param name="messageID">The addressed notification.</param>
        /// <param name="loop">Whether playback repeats until its queue handle is stopped.</param>
        /// <returns>The queue handle, or zero when the selected content bank omits this message.</returns>
        public uint EnqueueNotification(int messageID, bool loop = false) =>
            _authoredMessages.Contains(messageID) ? Notifications.Enqueue(messageID, loop) : 0;

        /// <summary>
        /// Refreshes owned playbacks and pending notifications using unscaled presentation time.
        /// </summary>
        public void Update() => Notifications.Update();

        /// <summary>
        /// Releases active sounds when playback becomes unavailable and permits future sounds on return.
        /// Pending notifications retain their queue state and continue their normal status updates.
        /// </summary>
        /// <param name="available">Whether this encounter can acquire sound playback.</param>
        public void SetPlaybackAvailable(bool available)
        {
            if (_playbackAvailable == available)
                return;
            _playbackAvailable = available;
            if (!available)
                Notifications.ReleasePlaybacks();
        }

        /// <summary>
        /// Stops the encounter's active notification playbacks and discards pending requests.
        /// </summary>
        public void Dispose()
        {
            Notifications.Dispose();
            Playback = null;
        }

        /// <summary>
        /// Starts an unscaled monotonic millisecond clock for this notification queue.
        /// </summary>
        /// <returns>The provider used for relative pending-message lifetimes.</returns>
        private static Func<uint> CreatePresentationClock()
        {
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            return () => unchecked((uint)clock.ElapsedMilliseconds);
        }

        /// <summary>
        /// Starts a playback through the presentation owner's provider.
        /// </summary>
        /// <param name="clip">The already selected playback variant.</param>
        /// <param name="loop">Whether the queue handle must be stopped explicitly.</param>
        /// <returns>The owned playback, or null when audio playback is unavailable.</returns>
        private ITacticalCombatNotificationPlayback Play(TacticalCombatSoundClip clip, bool loop)
        {
            return _playbackAvailable ? Playback?.Invoke(clip, loop) : null;
        }

        /// <summary>
        /// Resolves a typed weapon or unit sound to its addressed notification record.
        /// </summary>
        /// <param name="sound">The typed combat sound.</param>
        /// <returns>The notification message ID.</returns>
        private static int GetMessageID(TacticalCombatSound sound) =>
            sound switch
            {
                TacticalCombatSound.CapitalLaser => 0,
                TacticalCombatSound.HeavyWeapon => 1,
                TacticalCombatSound.CapitalIon => 2,
                TacticalCombatSound.AllianceFighterLaser => 3,
                TacticalCombatSound.EmpireFighterLaser => 4,
                TacticalCombatSound.FighterIon => 5,
                TacticalCombatSound.TractorEngaged => 6,
                TacticalCombatSound.TractorReleased => 7,
                TacticalCombatSound.TargetLocked => 8,
                TacticalCombatSound.SmallExplosion => 9,
                TacticalCombatSound.MediumExplosion => 10,
                TacticalCombatSound.LargeExplosion => 11,
                TacticalCombatSound.HeavyShieldHit => 12,
                TacticalCombatSound.LaserShieldHit => 13,
                TacticalCombatSound.IonShieldHit => 14,
                TacticalCombatSound.HeavyHullHit => 17,
                TacticalCombatSound.LaserHullHit => 18,
                TacticalCombatSound.IonHullHit => 19,
                TacticalCombatSound.Superlaser => 20,
                TacticalCombatSound.FighterLaunch => 27,
                _ => throw new ArgumentOutOfRangeException(nameof(sound)),
            };
    }
}
