using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Tactical.Synchronization;
using UnityEngine;

namespace Rebellion.Multiplayer.Tactical
{
    /// <summary>
    /// Authenticates reliable battle commands and effects independently of replaceable state updates.
    /// </summary>
    internal sealed partial class MultiplayerTacticalProtocol : IDisposable
    {
        internal const int MaximumQueuedEvents = 256;
        internal const int MaximumPendingCommands = 256;
        internal const int MaximumPendingEffectBatches = 64;
        internal const int MaximumCommandBytes = MultiplayerPacketCodec.MaximumTacticalCommandBytes;
        internal const double ReadinessTimeoutMilliseconds = 120000;
        internal const double InactivityTimeoutMilliseconds = 30000;
        internal const double TerminalTimeoutMilliseconds = 30000;
        private const int _maximumQueuedBytes = 8 * 1024 * 1024;
        private const double _checkpointIntervalMilliseconds = 1000;
        private const double _resyncIntervalMilliseconds = 2000;
        private readonly MultiplayerRole _role;
        private readonly string _initializationID;
        private readonly string _battleID;
        private readonly CombatSide _hostSide;
        private readonly Func<CombatSide, TacticalCombatCommand, bool> _applyCommand;
        private readonly Func<long, TacticalCombatSnapshot> _captureSnapshot;
        private readonly Action<TacticalCombatSnapshot> _applySnapshot;
        private readonly Action<TacticalCombatEvent[]> _applyEffects;
        private readonly Func<int> _getMovieID;
        private readonly Func<int, bool> _completeMovie;
        private readonly Action<MultiplayerPacket> _send;
        private readonly Func<double> _milliseconds;
        private readonly Func<double> _presentationTimestamp;
        private readonly Queue<Received> _events = new();
        private readonly Queue<Received> _pendingEffects = new();
        private readonly SortedDictionary<long, long> _checkpointReceipts = new();
        private readonly Dictionary<long, TaskCompletionSource<PlayerRequestOutcome>> _requests =
            new();
        private readonly Dictionary<long, double> _requestTimes = new();
        private readonly Dictionary<MultiplayerRole, Received> _lastCommands = new();
        private Received _replaceableSnapshot;
        private TacticalCombatSnapshot _latestSnapshot;
        private byte[] _lastEffectPayload;
        private long _nextRequestID;
        private long _lastRemoteRequestID = 2000000000;
        private long _lastSnapshot;
        private long _lastPublishedEffect;
        private long _lastReceivedEffect;
        private long _lastAppliedEffect;
        private long _latestEffectWatermark;
        private long _terminalSequence;
        private long _terminalEffects;
        private bool _terminalAcknowledged;
        private int _queuedBytes;
        private int _pendingEffectBytes;
        private bool _localReady;
        private bool _remoteReady;
        private int _localMovieAck;
        private int _remoteMovieAck;
        private int _completedMovie;
        private double _createdAt;
        private double _lastPeerActivity;
        private double _lastStateAt;
        private double _lastCheckpointAt = double.NegativeInfinity;
        private double _lastResyncAt = double.NegativeInfinity;
        private double _terminalAt;
        private bool _disposed;

        public Exception Failure { get; private set; }
        public bool CanAdvance =>
            !_disposed
            && Failure == null
            && _role == MultiplayerRole.Host
            && _localReady
            && _remoteReady;
        public bool TerminalSnapshotAcknowledged => _terminalAcknowledged;
        public event Action<Exception> Faulted;

        /// <summary>
        /// Connects authoritative simulation and passive presentation through independent state and event streams.
        /// </summary>
        /// <param name="role">The authenticated local role.</param>
        /// <param name="initializationID">The initialized campaign identity.</param>
        /// <param name="battleID">The ordered host-issued encounter identity.</param>
        /// <param name="hostSide">The encounter side bound to the host faction.</param>
        /// <param name="applyCommand">Applies commands using the authenticated actor's side.</param>
        /// <param name="captureSnapshot">Captures state and consumes accumulated effects once.</param>
        /// <param name="applySnapshot">Applies complete state without announcing effects.</param>
        /// <param name="applyEffects">Applies an ordered reliable presentation batch.</param>
        /// <param name="getMovieID">Reads the currently paused movie identity.</param>
        /// <param name="completeMovie">Releases the movie after both views acknowledge it.</param>
        /// <param name="send">The transport selecting reliability from the message type.</param>
        /// <param name="milliseconds">An optional monotonic clock in milliseconds.</param>
        /// <param name="presentationTimestampMilliseconds">The optional replica playback cursor used to align effects with displayed poses.</param>
        public MultiplayerTacticalProtocol(
            MultiplayerRole role,
            string initializationID,
            string battleID,
            CombatSide hostSide,
            Func<CombatSide, TacticalCombatCommand, bool> applyCommand,
            Func<long, TacticalCombatSnapshot> captureSnapshot,
            Action<TacticalCombatSnapshot> applySnapshot,
            Action<TacticalCombatEvent[]> applyEffects,
            Func<int> getMovieID,
            Func<int, bool> completeMovie,
            Action<MultiplayerPacket> send,
            Func<double> milliseconds = null,
            Func<double> presentationTimestampMilliseconds = null
        )
        {
            if (role is not (MultiplayerRole.Host or MultiplayerRole.Guest))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (
                !Guid.TryParseExact(initializationID, "N", out _)
                || !Guid.TryParseExact(battleID, "N", out _)
            )
                throw new ArgumentException(
                    "Initialized campaign and battle identities are required."
                );
            if (hostSide is not (CombatSide.Attacker or CombatSide.Defender))
                throw new ArgumentOutOfRangeException(nameof(hostSide));
            _role = role;
            _initializationID = initializationID;
            _battleID = battleID;
            _hostSide = hostSide;
            _applyCommand = applyCommand ?? throw new ArgumentNullException(nameof(applyCommand));
            _captureSnapshot =
                captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
            _applySnapshot =
                applySnapshot ?? throw new ArgumentNullException(nameof(applySnapshot));
            _applyEffects = applyEffects ?? throw new ArgumentNullException(nameof(applyEffects));
            _getMovieID = getMovieID ?? throw new ArgumentNullException(nameof(getMovieID));
            _completeMovie =
                completeMovie ?? throw new ArgumentNullException(nameof(completeMovie));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            Stopwatch clock = milliseconds == null ? Stopwatch.StartNew() : null;
            _milliseconds = milliseconds ?? (() => clock.Elapsed.TotalMilliseconds);
            _presentationTimestamp = presentationTimestampMilliseconds;
            _createdAt = _lastPeerActivity = _lastStateAt = Now();
            _nextRequestID = role == MultiplayerRole.Host ? 1 : 2000000001;
        }

        /// <summary>
        /// Announces completed local assets and entry presentation once.
        /// </summary>
        public void Ready()
        {
            if (_disposed || Failure != null || _localReady)
                return;
            _localReady = true;
            Send(MultiplayerMessageType.TacticalReady, new Envelope());
        }

        /// <summary>
        /// Submits a bounded semantic intent without accepting an actor identity from its payload.
        /// </summary>
        /// <param name="command">The local view's captured intent.</param>
        /// <returns>The host's admission result, or an unknown outcome after interruption.</returns>
        public Task<PlayerRequestOutcome> ExecuteAsync(TacticalCombatCommand command)
        {
            if (_disposed || Failure != null)
                return Task.FromResult(Unknown());
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            try
            {
                if (_requests.Count >= MaximumPendingCommands)
                    throw new InvalidDataException("Too many battle commands await confirmation.");
                byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(command));
                if (payload.Length > MaximumCommandBytes)
                    throw new InvalidDataException("The battle command exceeds its payload limit.");
                long requestID = _nextRequestID;
                _nextRequestID = checked(_nextRequestID + 1);
                if (_role == MultiplayerRole.Host && requestID >= 2000000001)
                    throw new InvalidDataException(
                        "The battle request identity range is exhausted."
                    );
                TaskCompletionSource<PlayerRequestOutcome> completion = new(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                _requests.Add(requestID, completion);
                _requestTimes.Add(requestID, Now());
                Envelope envelope = new() { requestID = requestID, actor = _role };
                if (_role == MultiplayerRole.Host)
                    Enqueue(
                        new Received(MultiplayerMessageType.TacticalCommand, envelope, payload)
                    );
                else
                    Send(MultiplayerMessageType.TacticalCommand, envelope, payload);
                return completion.Task;
            }
            catch (Exception exception)
            {
                Fail(exception);
                return Task.FromResult(Unknown());
            }
        }

        /// <summary>
        /// Releases retained traffic and leaves unresolved commands explicitly unconfirmed.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            CompleteUnknown();
            ClearTraffic();
        }

        /// <summary>
        /// Stops the shared battle through the existing connection recovery boundary.
        /// </summary>
        /// <param name="exception">The failed loading or presentation boundary.</param>
        public void ReportFailure(Exception exception) => Fail(exception);

        /// <summary>
        /// Creates the interrupted-operation outcome shared by failure and disposal.
        /// </summary>
        /// <returns>The explicitly unknown outcome.</returns>
        private static PlayerRequestOutcome Unknown() =>
            new(
                false,
                "The connection ended before the battle command was confirmed.",
                outcomeKnown: false
            );

        [Serializable]
        private sealed class Envelope
        {
            public string initializationID;
            public string battleID;
            public long requestID;
            public long sequence;
            public long effectSequence;
            public double timestampMilliseconds;
            public MultiplayerRole actor;
            public bool accepted;
            public int movieID;
        }

        private sealed class Received
        {
            public MultiplayerMessageType Type { get; }
            public Envelope Envelope { get; }
            public byte[] Payload { get; }

            /// <summary>
            /// Retains immutable parameters until their main-thread application boundary.
            /// </summary>
            /// <param name="type">The closed event discriminator.</param>
            /// <param name="envelope">The authenticated metadata.</param>
            /// <param name="payload">The immutable typed payload bytes.</param>
            public Received(MultiplayerMessageType type, Envelope envelope, byte[] payload)
            {
                Type = type;
                Envelope = envelope;
                Payload = payload;
            }
        }
    }
}
