using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Util.Random;
using Stopwatch = global::System.Diagnostics.Stopwatch;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Routes typed combat sounds and authored notifications through the encounter's shared scheduler.
    /// Playback handles remain owned by that scheduler instead of a separate duration-based sound queue.
    /// </summary>
    public sealed class TacticalCombatAudio : IDisposable
    {
        private bool _playbackAvailable = true;
        public event Action<int, bool> NotificationRequested;
        internal TacticalCombatNotifications Notifications { get; }
        public IReadOnlyList<TacticalCombatSoundClip> Clips { get; }
        public Func<
            TacticalCombatSoundClip,
            bool,
            ITacticalCombatNotificationPlayback
        > Playback { get; set; }

        /// <summary>
        /// Selects the explosion size from the captured unit category and complete hull.
        /// </summary>
        /// <returns>The unit's destruction sound.</returns>
        /// <param name="unit">The destroyed unit whose size selects the explosion.</param>
        internal static TacticalCombatSound GetExplosionSound(CombatUnit unit) =>
            unit.IsFighter || !(unit.MaximumHull >= 1100) ? TacticalCombatSound.SmallExplosion
            : unit.MaximumHull <= 2000 ? TacticalCombatSound.MediumExplosion
            : TacticalCombatSound.LargeExplosion;

        /// <summary>
        /// Selects the impact sound family before any resulting component-damage roll.
        /// </summary>
        /// <param name="weapon">The impacting weapon.</param>
        /// <param name="hull">Whether the impact passed through the shields.</param>
        /// <returns>The sound associated with this impact.</returns>
        internal static TacticalCombatSound GetImpactSound(HardpointWeaponType weapon, bool hull) =>
            weapon switch
            {
                HardpointWeaponType.LaserCannon => hull
                    ? TacticalCombatSound.LaserHullHit
                    : TacticalCombatSound.LaserShieldHit,
                HardpointWeaponType.IonCannon => hull
                    ? TacticalCombatSound.IonHullHit
                    : TacticalCombatSound.IonShieldHit,
                _ => hull ? TacticalCombatSound.HeavyHullHit : TacticalCombatSound.HeavyShieldHit,
            };

        /// <summary>
        /// Composes notification scheduling from the battle's explicitly supplied stream and live clocks.
        /// </summary>
        /// <param name="definition">The authored sound and notification banks.</param>
        /// <param name="random">The shared encounter stream.</param>
        /// <param name="factionInstanceID">The selected spoken notification faction.</param>
        /// <param name="isPaused">The live encounter pause state.</param>
        /// <param name="clock">The optional controlled presentation clock.</param>
        /// <returns>The owned notification service.</returns>
        internal static TacticalCombatAudio Create(
            TacticalCombatDefinition definition,
            IRandomNumberProvider random,
            string factionInstanceID,
            Func<bool> isPaused,
            Func<uint> clock
        ) =>
            new TacticalCombatAudio(
                definition?.Sounds ?? new List<TacticalCombatSoundDefinition>(),
                random,
                definition?.Notifications,
                factionInstanceID,
                isPaused,
                clock
            );

        /// <summary>
        /// Captures sound and selected-faction notification banks on the shared battle random stream.
        /// </summary>
        /// <param name="definitions">Typed combat sound variants.</param>
        /// <param name="random">The shared battle random-number stream.</param>
        /// <param name="notifications">Shared and faction-specific notification banks.</param>
        /// <param name="factionInstanceID">The faction whose spoken notification bank is selected.</param>
        /// <param name="isPaused">Whether the tactical simulation is currently paused.</param>
        /// <param name="clock">An optional unscaled millisecond clock for replay.</param>
        internal TacticalCombatAudio(
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
        public uint EnqueueNotification(int messageID, bool loop = false)
        {
            NotificationRequested?.Invoke(messageID, loop);
            return Notifications.HasMessage(messageID) ? Notifications.Enqueue(messageID, loop) : 0;
        }

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
            Stopwatch clock = Stopwatch.StartNew();
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

    /// <summary>
    /// Exposes the activity and cancellation of one owned notification playback.
    /// </summary>
    public interface ITacticalCombatNotificationPlayback
    {
        bool IsPlaying { get; }

        /// <summary>
        /// Stops this playback without stopping other application audio.
        /// </summary>
        void Stop();
    }
}
