using System;
using System.Collections.Generic;
using Rebellion.Game.SpaceCombat;
using Rebellion.Tactical;

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

            public uint Enqueue(int messageID, bool loop = false) => 0;

            public void Update() { }

            public bool IsActive(uint queueID) => false;

            public void Stop(uint queueID) { }

            public void ReleasePlaybacks() { }

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

        internal static ITacticalCombatAudio Create(
            TacticalCombatDefinition definition,
            Rebellion.Util.Random.IRandomNumberProvider random,
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

        public void Enqueue(TacticalCombatSound sound) { }

        public uint EnqueueNotification(int messageID, bool loop = false)
        {
            NotificationRequested?.Invoke(messageID, loop);
            return 0;
        }

        public void Update() { }

        public void SetPlaybackAvailable(bool available) { }

        public void Dispose() => Notifications.Dispose();
    }
}
