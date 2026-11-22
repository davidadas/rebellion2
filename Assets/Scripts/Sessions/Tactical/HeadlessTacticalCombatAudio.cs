using System;
using System.Collections.Generic;
using Rebellion.Game.SpaceCombat;
using Rebellion.Tactical;
using Rebellion.Util.Random;

namespace Rebellion.Sessions.Tactical
{
    /// <summary>
    /// Supplies a silent tactical boundary for simulations that have no presentation host.
    /// </summary>
    internal sealed class HeadlessTacticalCombatAudio : ITacticalCombatAudio
    {
        private sealed class NotificationQueue : ITacticalCombatNotificationQueue
        {
            public bool Enabled { get; set; }

            /// <summary>
            /// Accepts a silent notification.
            /// </summary>
            /// <param name="messageID">The authored notification identifier.</param>
            /// <param name="loop">Whether playback would repeat.</param>
            /// <returns>The inert playback identifier.</returns>
            public uint Enqueue(int messageID, bool loop = false) => 0;

            /// <summary>
            /// Advances no playback in a headless simulation.
            /// </summary>
            public void Update() { }

            /// <summary>
            /// Reports that a silent playback is never active.
            /// </summary>
            /// <param name="queueID">The playback identifier.</param>
            /// <returns>Always false.</returns>
            public bool IsActive(uint queueID) => false;

            /// <summary>
            /// Stops no playback in a headless simulation.
            /// </summary>
            /// <param name="queueID">The playback identifier.</param>
            public void Stop(uint queueID) { }

            /// <summary>
            /// Releases no playback in a headless simulation.
            /// </summary>
            public void ReleasePlaybacks() { }

            /// <summary>
            /// Releases the inert notification queue.
            /// </summary>
            public void Dispose() { }
        }

        private static readonly IReadOnlyList<TacticalCombatSoundClip> _noClips =
            Array.Empty<TacticalCombatSoundClip>();

        public event Action<int, bool> NotificationRequested;

        public ITacticalCombatNotificationQueue Notifications { get; } = new NotificationQueue();

        public IReadOnlyList<TacticalCombatSoundClip> Clips => _noClips;

        public Func<
            TacticalCombatSoundClip,
            bool,
            ITacticalCombatNotificationPlayback
        > Playback { get; set; }

        /// <summary>
        /// Creates a silent audio boundary for a headless tactical encounter.
        /// </summary>
        /// <param name="definition">The encounter's authored tactical definition.</param>
        /// <param name="random">The encounter's deterministic random provider.</param>
        /// <param name="factionInstanceID">The local faction identifier.</param>
        /// <param name="isPaused">Reports whether the encounter is paused.</param>
        /// <param name="clock">Reports the encounter clock.</param>
        /// <returns>The silent encounter audio boundary.</returns>
        internal static ITacticalCombatAudio Create(
            TacticalCombatDefinition definition,
            IRandomNumberProvider random,
            string factionInstanceID,
            Func<bool> isPaused,
            Func<uint> clock
        )
        {
            _ = definition;
            _ = random;
            _ = factionInstanceID;
            _ = isPaused;
            _ = clock;
            return new HeadlessTacticalCombatAudio();
        }

        /// <summary>
        /// Accepts a silent tactical sound.
        /// </summary>
        /// <param name="sound">The authored tactical sound.</param>
        public void Enqueue(TacticalCombatSound sound) { }

        /// <summary>
        /// Raises the notification request without starting presentation playback.
        /// </summary>
        /// <param name="messageID">The authored notification identifier.</param>
        /// <param name="loop">Whether presentation playback would repeat.</param>
        /// <returns>The inert playback identifier.</returns>
        public uint EnqueueNotification(int messageID, bool loop = false)
        {
            NotificationRequested?.Invoke(messageID, loop);
            return 0;
        }

        /// <summary>
        /// Advances no audio in a headless simulation.
        /// </summary>
        public void Update() { }

        /// <summary>
        /// Accepts presentation availability without creating playback.
        /// </summary>
        /// <param name="available">Whether a presentation host is available.</param>
        public void SetPlaybackAvailable(bool available) { }

        /// <summary>
        /// Releases the silent audio boundary.
        /// </summary>
        public void Dispose() => Notifications.Dispose();
    }
}
