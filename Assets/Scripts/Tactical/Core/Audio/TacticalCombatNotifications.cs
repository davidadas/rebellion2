using System;
using System.Collections.Generic;
using System.IO;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Schedules tactical effects and spoken notifications in their shared ordered queue.
    /// Variant selection uses the battle stream before each capacity check; playback activity,
    /// pending lifetimes, and simulation pause are resolved through their owning boundaries.
    /// </summary>
    internal sealed class TacticalCombatNotifications : IDisposable
    {
        private const int _messageCount = 317;
        private const int _maximumVariantCount = 4;
        private const int _firstSpokenMessageID = 32;
        private const int _firstOtherMessageID = 286;
        private const int _spokenGroup = 1;
        private const int _effectGroup = 2;
        private const int _otherGroup = 3;
        private const int _effectPlaybackLimit = 5;
        private const uint _spokenLifetimeMilliseconds = 8000;
        private const uint _effectLifetimeMilliseconds = 225;
        private const uint _otherLifetimeMilliseconds = 3000;
        private const int _pausedLifetimeShift = 2;
        private readonly Dictionary<int, TacticalCombatNotificationDefinition> _definitions = new();
        private readonly LinkedList<Notification> _active = new();
        private readonly LinkedList<Notification> _pending = new();
        private readonly IRandomNumberProvider _random;
        private readonly Func<uint> _clock;
        private readonly Func<bool> _isPaused;
        private readonly Func<
            TacticalCombatSoundClip,
            bool,
            ITacticalCombatNotificationPlayback
        > _play;
        private uint _nextQueueID = 1;
        private uint _activeIDSeed = 1;
        private uint _pendingIDSeed = 1;
        private bool _disposed;
        private ActiveBattle.NotificationQueue _loadedQueue;

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Captures the selected faction's authored message bank and live playback boundaries.
        /// </summary>
        /// <param name="definitions">Shared and selected-faction records with unique message IDs.</param>
        /// <param name="random">The battle's shared random-number stream.</param>
        /// <param name="clock">The unscaled presentation clock in unsigned milliseconds.</param>
        /// <param name="isPaused">Whether tactical simulation is currently paused.</param>
        /// <param name="play">Starts an owned playback, or returns null when playback fails.</param>
        public TacticalCombatNotifications(
            IReadOnlyList<TacticalCombatNotificationDefinition> definitions,
            IRandomNumberProvider random,
            Func<uint> clock,
            Func<bool> isPaused,
            Func<TacticalCombatSoundClip, bool, ITacticalCombatNotificationPlayback> play
        )
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _isPaused = isPaused ?? throw new ArgumentNullException(nameof(isPaused));
            _play = play ?? throw new ArgumentNullException(nameof(play));
            foreach (TacticalCombatNotificationDefinition definition in definitions)
            {
                if (
                    definition == null
                    || definition.MessageID < 0
                    || definition.MessageID >= _messageCount
                    || definition.Variants == null
                    || definition.Variants.Count == 0
                    || definition.Variants.Count > _maximumVariantCount
                )
                    throw new InvalidDataException(
                        "A tactical notification requires a bounded message ID and one to four variant slots."
                    );
                foreach (TacticalCombatSoundClip clip in definition.Variants)
                    if (clip == null)
                        throw new InvalidDataException(
                            "A tactical notification variant slot must be explicit."
                        );
                _definitions.Add(definition.MessageID, definition);
            }
        }

        /// <summary>
        /// Checks whether the selected faction's bank contains the addressed message.
        /// </summary>
        /// <param name="messageID">The authored message identifier.</param>
        /// <returns>Whether the message can be enqueued from this bank.</returns>
        internal bool HasMessage(int messageID) => _definitions.ContainsKey(messageID);

        /// <summary>
        /// Requests a message, retaining blocked one-shot requests and dropping blocked loops.
        /// </summary>
        /// <param name="messageID">The addressed notification.</param>
        /// <param name="loop">Whether playback repeats until its returned queue handle is stopped.</param>
        /// <returns>The active or pending queue handle, or zero when the request is rejected.</returns>
        public uint Enqueue(int messageID, bool loop = false)
        {
            ThrowIfDisposed();
            ResumePlayback();
            uint timestamp = _clock();
            uint queueID = _nextQueueID = unchecked(_nextQueueID + 1);
            if (!Enabled || messageID < 0 || messageID >= _messageCount)
                return 0;
            if (
                !_definitions.TryGetValue(
                    messageID,
                    out TacticalCombatNotificationDefinition definition
                )
            )
                throw new InvalidDataException(
                    $"Tactical notification {messageID} has no authored sound bank."
                );

            Notification record = new(queueID, definition, timestamp, loop);
            Update();
            if (TryStart(record))
            {
                if (Find(_active, record.QueueID) != null)
                    return 0;
                Insert(_active, record, ref _activeIDSeed);
                return record.QueueID;
            }
            if (loop)
                return 0;
            Insert(_pending, record, ref _pendingIDSeed);
            return record.QueueID;
        }

        /// <summary>
        /// Removes finished active playbacks, then starts or expires pending requests in queue-ID order.
        /// Active cleanup continues while notification scheduling is disabled.
        /// </summary>
        public void Update()
        {
            ThrowIfDisposed();
            ResumePlayback();
            LinkedListNode<Notification> node = _active.First;
            while (node != null)
            {
                LinkedListNode<Notification> next = node.Next;
                Notification record = node.Value;
                record.Started = record.Playback?.IsPlaying == true;
                if (!record.Started)
                    Remove(_active, record.QueueID);
                node = next;
            }
            if (!Enabled)
                return;

            Span<int> counts = stackalloc int[_otherGroup + 1];
            counts[_spokenGroup] = CountActive(_spokenGroup);
            counts[_effectGroup] = CountActive(_effectGroup);
            counts[_otherGroup] = CountActive(_otherGroup);
            uint now = _clock();
            node = _pending.First;
            while (node != null)
            {
                LinkedListNode<Notification> next = node.Next;
                Notification record = node.Value;
                int group = GetGroup(record.Definition.MessageID);
                uint lifetime = GetPendingLifetime(group);
                if (group == _spokenGroup && _isPaused())
                    lifetime <<= _pausedLifetimeShift;
                if (unchecked(now - record.Created) > lifetime)
                    _pending.Remove(node);
                else if (counts[group] < GetActiveLimit(group))
                {
                    _pending.Remove(node);
                    if (TryStart(record))
                    {
                        counts[group]++;
                        Insert(_active, record, ref _activeIDSeed);
                    }
                }
                node = next;
            }
        }

        /// <summary>
        /// Captures notification queues with timestamps relative to their presentation clock.
        /// </summary>
        /// <returns>The notification state to rebind when the loaded view is ready.</returns>
        internal ActiveBattle.NotificationQueue CaptureState()
        {
            uint now = _clock();
            ActiveBattle.NotificationQueue state = new()
            {
                Enabled = Enabled,
                NextID = _nextQueueID,
                ActiveIDSeed = _activeIDSeed,
                PendingIDSeed = _pendingIDSeed,
            };
            foreach (Notification notification in _active)
                state.Active.Add(SaveNotification(notification, now));
            foreach (Notification notification in _pending)
                state.Pending.Add(SaveNotification(notification, now));
            return state;
        }

        /// <summary>
        /// Defers playback rebinding until the first notification operation of the loaded view.
        /// </summary>
        /// <param name="state">The saved notification queues.</param>
        internal void RestoreState(ActiveBattle.NotificationQueue state)
        {
            _loadedQueue = state;
            Enabled = state.Enabled;
            _nextQueueID = state.NextID;
            _activeIDSeed = state.ActiveIDSeed;
            _pendingIDSeed = state.PendingIDSeed;
        }

        /// <summary>
        /// Copies one queue entry without selecting an audio variant.
        /// </summary>
        /// <param name="notification">The current notification.</param>
        /// <param name="now">The current clock sample.</param>
        /// <returns>The cue and any active playback position.</returns>
        private static ActiveBattle.NotificationState SaveNotification(
            Notification notification,
            uint now
        ) =>
            new()
            {
                QueueID = notification.QueueID,
                MessageID = notification.Definition.MessageID,
                AgeMilliseconds = unchecked(now - notification.Created),
                Loop = notification.Loop,
                VariantIndex = notification.VariantIndex,
                PlaybackSample =
                    notification.Playback?.IsPlaying == true
                        ? (
                            notification.Playback as ITacticalCombatNotificationPosition
                        )?.PositionSamples
                            ?? 0
                        : null,
            };

        /// <summary>
        /// Restores existing cue handles without advancing the shared random stream.
        /// </summary>
        private void ResumePlayback()
        {
            if (_loadedQueue == null)
                return;

            ActiveBattle.NotificationQueue state = _loadedQueue;
            _loadedQueue = null;
            uint now = _clock();
            foreach (ActiveBattle.NotificationState active in state.Active)
            {
                Notification notification = RestoreNotification(active, now);
                if (active.PlaybackSample is int sample)
                {
                    TacticalCombatSoundClip clip = notification.Definition.Variants[
                        active.VariantIndex
                    ];
                    notification.Started = true;
                    notification.Playback = string.IsNullOrEmpty(clip.Path)
                        ? null
                        : _play(clip, notification.Loop);
                    if (notification.Playback is ITacticalCombatNotificationPosition position)
                        position.PositionSamples = sample;
                }
                _active.AddLast(notification);
            }
            foreach (ActiveBattle.NotificationState pending in state.Pending)
                _pending.AddLast(RestoreNotification(pending, now));
        }

        /// <summary>
        /// Resolves a stored bank and aligns its age with the loaded view's clock.
        /// </summary>
        /// <param name="state">The saved notification.</param>
        /// <param name="now">The loaded presentation clock sample.</param>
        /// <returns>The restored queue entry.</returns>
        private Notification RestoreNotification(ActiveBattle.NotificationState state, uint now) =>
            new(
                state.QueueID,
                _definitions[state.MessageID],
                unchecked(now - state.AgeMilliseconds),
                state.Loop
            )
            {
                VariantIndex = state.VariantIndex,
            };

        /// <summary>
        /// Reports whether an owned active record remains started after its latest playback refresh.
        /// </summary>
        /// <param name="queueID">The active queue identity held by a playback consumer.</param>
        /// <returns>Whether the active record exists and retains its started state.</returns>
        public bool IsActive(uint queueID)
        {
            ThrowIfDisposed();
            return Find(_active, queueID)?.Value.Started == true;
        }

        /// <summary>
        /// Stops and removes an active queue handle; pending handles are unaffected.
        /// </summary>
        /// <param name="queueID">The active playback to stop.</param>
        public void Stop(uint queueID)
        {
            ThrowIfDisposed();
            Notification record = Find(_active, queueID)?.Value;
            if (record == null)
                return;
            record.Playback?.Stop();
            Remove(_active, record.QueueID);
        }

        /// <summary>
        /// Releases active playback resources while retaining their records for the next status refresh.
        /// Pending requests, scheduling eligibility, and queue handles remain unchanged.
        /// </summary>
        public void ReleasePlaybacks()
        {
            ThrowIfDisposed();
            foreach (Notification record in _active)
            {
                record.Playback?.Stop();
                record.Playback = null;
            }
        }

        /// <summary>
        /// Stops every active owned playback before discarding pending requests.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            while (_active.First != null)
                Stop(_active.First.Value.QueueID);
            _pending.Clear();
            _disposed = true;
        }

        /// <summary>
        /// Selects an ordered variant before checking the notification's active group capacity.
        /// A failed wave start still owns an active record until the next cleanup sweep.
        /// </summary>
        /// <param name="record">The queued notification to prepare.</param>
        /// <returns>Whether a playback start was attempted.</returns>
        private bool TryStart(Notification record)
        {
            List<TacticalCombatSoundClip> variants = record.Definition.Variants;
            int index = variants.Count > 1 ? _random.NextInt(0, variants.Count) : 0;
            TacticalCombatSoundClip clip = variants[index];
            int group = GetGroup(record.Definition.MessageID);
            if (CountActive(group) >= GetActiveLimit(group))
            {
                record.Started = false;
                return false;
            }
            if (record.Started)
                return false;
            record.Started = true;
            record.VariantIndex = index;
            record.Playback = string.IsNullOrEmpty(clip.Path) ? null : _play(clip, record.Loop);
            return true;
        }

        /// <summary>
        /// Counts currently linked active records in one authored message group.
        /// </summary>
        /// <param name="group">The group to count.</param>
        /// <returns>The linked record count, including starts awaiting their first status refresh.</returns>
        private int CountActive(int group)
        {
            int count = 0;
            foreach (Notification record in _active)
                if (GetGroup(record.Definition.MessageID) == group)
                    count++;
            return count;
        }

        /// <summary>
        /// Maps a bounded message ID to its notification scheduling group.
        /// </summary>
        /// <param name="messageID">The addressed message.</param>
        /// <returns>The scheduling group from one to three.</returns>
        private static int GetGroup(int messageID) =>
            messageID < _firstSpokenMessageID ? _effectGroup
            : messageID < _firstOtherMessageID ? _spokenGroup
            : _otherGroup;

        /// <summary>
        /// Gets the simultaneous playback limit for a scheduling group.
        /// </summary>
        /// <param name="group">The scheduling group.</param>
        /// <returns>The permitted active record count.</returns>
        private static int GetActiveLimit(int group) =>
            group == _effectGroup ? _effectPlaybackLimit : 1;

        /// <summary>
        /// Gets a group's unscaled pending lifetime before any simulation-pause adjustment.
        /// </summary>
        /// <param name="group">The scheduling group.</param>
        /// <returns>The pending lifetime in milliseconds.</returns>
        private static uint GetPendingLifetime(int group) =>
            group == _spokenGroup ? _spokenLifetimeMilliseconds
            : group == _effectGroup ? _effectLifetimeMilliseconds
            : _otherLifetimeMilliseconds;

        /// <summary>
        /// Links a record in ascending unsigned queue-ID order and advances its owning ID seed.
        /// </summary>
        /// <param name="records">The active or pending owner.</param>
        /// <param name="record">The record to link.</param>
        /// <param name="seed">The owner's next assignable identifier.</param>
        private static void Insert(
            LinkedList<Notification> records,
            Notification record,
            ref uint seed
        )
        {
            if (record.QueueID == 0)
            {
                record.QueueID = seed;
                seed = unchecked(seed + 1);
            }
            else if (seed <= record.QueueID)
                seed = unchecked(record.QueueID + 1);
            LinkedListNode<Notification> previous = records.Last;
            while (previous != null && previous.Value.QueueID > record.QueueID)
                previous = previous.Previous;
            if (previous == null)
                records.AddFirst(record);
            else
                records.AddAfter(previous, record);
        }

        /// <summary>
        /// Resolves the first linked record with an active or pending queue identifier.
        /// </summary>
        /// <param name="records">The owning ordered queue.</param>
        /// <param name="queueID">The identifier to find.</param>
        /// <returns>The matching node, or null when the identifier is absent.</returns>
        private static LinkedListNode<Notification> Find(
            LinkedList<Notification> records,
            uint queueID
        )
        {
            for (LinkedListNode<Notification> node = records.First; node != null; node = node.Next)
                if (node.Value.QueueID == queueID)
                    return node;
            return null;
        }

        /// <summary>
        /// Removes a linked record without stopping an already finished playback.
        /// </summary>
        /// <param name="records">The record owner.</param>
        /// <param name="queueID">The identifier to unlink.</param>
        private static void Remove(LinkedList<Notification> records, uint queueID)
        {
            LinkedListNode<Notification> node = Find(records, queueID);
            if (node != null)
                records.Remove(node);
        }

        /// <summary>
        /// Rejects calls after the encounter releases notification ownership.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TacticalCombatNotifications));
        }

        private sealed class Notification
        {
            public uint QueueID { get; set; }
            public TacticalCombatNotificationDefinition Definition { get; }
            public uint Created { get; }
            public bool Loop { get; }
            public bool Started { get; set; }
            public int VariantIndex { get; set; }
            public ITacticalCombatNotificationPlayback Playback { get; set; }

            /// <summary>
            /// Captures a notification request before it enters an active or pending queue.
            /// </summary>
            /// <param name="queueID">The initial queue identifier.</param>
            /// <param name="definition">The addressed message bank.</param>
            /// <param name="created">The request's unscaled clock timestamp.</param>
            /// <param name="loop">Whether playback repeats until explicitly stopped.</param>
            public Notification(
                uint queueID,
                TacticalCombatNotificationDefinition definition,
                uint created,
                bool loop
            )
            {
                QueueID = queueID;
                Definition = definition;
                Created = created;
                Loop = loop;
            }
        }
    }
}
