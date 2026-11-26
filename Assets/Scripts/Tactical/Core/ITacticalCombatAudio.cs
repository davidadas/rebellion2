using System;
using System.Collections.Generic;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Creates the encounter's notification service with its shared random stream and live pause clock.
    /// </summary>
    /// <param name="definition">The selected authored sound and notification definitions.</param>
    /// <param name="random">The encounter stream used for ordered variant selection.</param>
    /// <param name="factionInstanceID">The faction whose notification bank is selected.</param>
    /// <param name="isPaused">The encounter's live pause state.</param>
    /// <param name="clock">The optional controlled presentation clock.</param>
    /// <returns>The notification service owned by this encounter.</returns>
    public delegate ITacticalCombatAudio TacticalCombatAudioFactory(
        TacticalCombatDefinition definition,
        IRandomNumberProvider random,
        string factionInstanceID,
        Func<bool> isPaused,
        Func<uint> clock
    );

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

    /// <summary>
    /// Exposes queue admission and status at the encounter's established command refresh points.
    /// </summary>
    public interface ITacticalCombatNotificationQueue : IDisposable
    {
        bool Enabled { get; set; }

        /// <summary>
        /// Requests an authored notification in the shared ordered queue.
        /// </summary>
        /// <param name="messageID">The addressed notification.</param>
        /// <param name="loop">Whether playback repeats until stopped.</param>
        /// <returns>The admitted queue identity, or zero after rejection.</returns>
        uint Enqueue(int messageID, bool loop = false);

        /// <summary>
        /// Refreshes completed playback and pending notification state.
        /// </summary>
        void Update();

        /// <summary>
        /// Reads the activity retained by the most recent playback refresh.
        /// </summary>
        /// <param name="queueID">The owned queue identity.</param>
        /// <returns>Whether the notification remains active.</returns>
        bool IsActive(uint queueID);

        /// <summary>
        /// Releases the requested notification's owned playback.
        /// </summary>
        /// <param name="queueID">The owned queue identity.</param>
        void Stop(uint queueID);

        /// <summary>
        /// Releases active playback while retaining pending queue records.
        /// </summary>
        void ReleasePlaybacks();
    }

    /// <summary>
    /// Supplies notification scheduling and playback ownership without constructing audio infrastructure in the battle.
    /// </summary>
    public interface ITacticalCombatAudio : IDisposable
    {
        event Action<int, bool> NotificationRequested;
        ITacticalCombatNotificationQueue Notifications { get; }
        IReadOnlyList<TacticalCombatSoundClip> Clips { get; }
        Func<
            TacticalCombatSoundClip,
            bool,
            ITacticalCombatNotificationPlayback
        > Playback { get; set; }

        /// <summary>
        /// Requests the sound associated with one combat transition.
        /// </summary>
        /// <param name="sound">The combat transition.</param>
        void Enqueue(TacticalCombatSound sound);

        /// <summary>
        /// Requests one authored notification with the encounter's shared ordering.
        /// </summary>
        /// <param name="messageID">The addressed notification.</param>
        /// <param name="loop">Whether playback repeats until stopped.</param>
        /// <returns>The admitted queue identity, or zero after rejection.</returns>
        uint EnqueueNotification(int messageID, bool loop = false);

        /// <summary>
        /// Refreshes playback-backed notification state at a command refresh boundary.
        /// </summary>
        void Update();

        /// <summary>
        /// Changes whether the encounter can acquire playback resources.
        /// </summary>
        /// <param name="available">Whether playback is available.</param>
        void SetPlaybackAvailable(bool available);
    }
}
