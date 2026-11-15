using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Authenticates reliable battle commands and effects independently of replaceable state updates.
    /// </summary>
    internal sealed class MultiplayerTacticalProtocol : IDisposable
    {
        internal const int MaximumQueuedEvents = 256;
        internal const int MaximumPendingCommands = 256;
        internal const int MaximumPendingEffectBatches = 64;
        internal const int MaximumCommandBytes = MultiplayerPacketCodec.MaximumTacticalCommandBytes;
        internal const double ReadinessTimeoutMilliseconds = 120000;
        internal const double InactivityTimeoutMilliseconds = 30000;
        internal const double TerminalTimeoutMilliseconds = 30000;
        private const int MaximumQueuedBytes = 8 * 1024 * 1024;
        private const double CheckpointIntervalMilliseconds = 1000;
        private const double ResyncIntervalMilliseconds = 2000;
        private readonly MultiplayerRole _role;
        private readonly string _initializationID;
        private readonly string _battleID;
        private readonly CombatSide _hostSide;
        private readonly Func<CombatSide, TacticalCombatNetworkCommand, bool> _applyCommand;
        private readonly Func<long, TacticalCombatNetworkFrame> _captureFrame;
        private readonly Action<TacticalCombatNetworkFrame> _applyFrame;
        private readonly Action<TacticalCombatNetworkEffect[]> _applyEffects;
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
        private Received _replaceableFrame;
        private TacticalCombatNetworkFrame _latestFrame;
        private byte[] _lastEffectPayload;
        private long _nextRequestID;
        private long _lastRemoteRequestID = 2000000000;
        private long _lastFrame;
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
        public bool TerminalFrameAcknowledged => _terminalAcknowledged;
        public event Action<Exception> Faulted;

        /// <summary>
        /// Connects authoritative simulation and passive presentation through independent state and event streams.
        /// </summary>
        /// <param name="role">The authenticated local role.</param>
        /// <param name="initializationID">The initialized campaign identity.</param>
        /// <param name="battleID">The ordered host-issued encounter identity.</param>
        /// <param name="hostSide">The encounter side bound to the host faction.</param>
        /// <param name="applyCommand">Applies commands using the authenticated actor's side.</param>
        /// <param name="captureFrame">Captures state and consumes accumulated effects once.</param>
        /// <param name="applyFrame">Applies complete state without announcing effects.</param>
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
            Func<CombatSide, TacticalCombatNetworkCommand, bool> applyCommand,
            Func<long, TacticalCombatNetworkFrame> captureFrame,
            Action<TacticalCombatNetworkFrame> applyFrame,
            Action<TacticalCombatNetworkEffect[]> applyEffects,
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
            _captureFrame = captureFrame ?? throw new ArgumentNullException(nameof(captureFrame));
            _applyFrame = applyFrame ?? throw new ArgumentNullException(nameof(applyFrame));
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
        public Task<PlayerRequestOutcome> ExecuteAsync(TacticalCombatNetworkCommand command)
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
        /// Validates identities and bounded payloads before retaining authenticated traffic.
        /// </summary>
        /// <param name="packet">The admitted counterpart's packet.</param>
        public void Receive(MultiplayerPacket packet)
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                if (
                    packet == null
                    || Encoding.UTF8.GetByteCount(packet.Metadata)
                        > MultiplayerPacketCodec.MaximumTacticalMetadataBytes
                )
                    throw new InvalidDataException("Battle metadata exceeds its limit.");
                Envelope envelope =
                    JsonUtility.FromJson<Envelope>(packet.Metadata)
                    ?? throw new InvalidDataException("Battle metadata is missing.");
                if (
                    !MultiplayerSessionIdentity.Matches(
                        _initializationID,
                        envelope.initializationID
                    ) || !string.Equals(_battleID, envelope.battleID, StringComparison.Ordinal)
                )
                    return;
                ValidatePayload(packet);
                switch (packet.Type)
                {
                    case MultiplayerMessageType.TacticalReady:
                        if (!_remoteReady)
                            _lastPeerActivity = _lastStateAt = Now();
                        _remoteReady = true;
                        break;
                    case MultiplayerMessageType.TacticalCommand:
                        RequireRole(MultiplayerRole.Host);
                        envelope.actor = MultiplayerRole.Guest;
                        if (envelope.requestID < 2000000001)
                            throw new InvalidDataException(
                                "The battle request identity is invalid."
                            );
                        Enqueue(new Received(packet.Type, envelope, packet.Payload));
                        break;
                    case MultiplayerMessageType.TacticalCommandResult:
                    case MultiplayerMessageType.TacticalCheckpoint:
                    case MultiplayerMessageType.TacticalEffects:
                        RequireRole(MultiplayerRole.Guest);
                        Enqueue(new Received(packet.Type, envelope, packet.Payload));
                        break;
                    case MultiplayerMessageType.TacticalFrame:
                        RequireRole(MultiplayerRole.Guest);
                        if (envelope.sequence <= 0)
                            throw new InvalidDataException(
                                "The battle projection identity is invalid."
                            );
                        if (
                            envelope.sequence > _lastFrame
                            && (
                                _replaceableFrame == null
                                || envelope.sequence > _replaceableFrame.Envelope.sequence
                            )
                        )
                            _replaceableFrame = new Received(packet.Type, envelope, packet.Payload);
                        break;
                    case MultiplayerMessageType.TacticalFrameReceipt:
                        RequireRole(MultiplayerRole.Host);
                        if (
                            envelope.sequence <= 0
                            || envelope.sequence > _lastFrame
                            || envelope.effectSequence < 0
                            || envelope.effectSequence > _lastPublishedEffect
                        )
                            throw new InvalidDataException(
                                "The peer acknowledged unpublished battle state or effects."
                            );
                        if (
                            _terminalSequence != 0
                            && envelope.sequence >= _terminalSequence
                            && envelope.effectSequence >= _terminalEffects
                        )
                            _terminalAcknowledged = true;
                        break;
                    case MultiplayerMessageType.TacticalMovieReceipt:
                        RequireRole(MultiplayerRole.Host);
                        if (envelope.movieID <= _completedMovie)
                            break;
                        if (envelope.movieID != _getMovieID())
                            throw new InvalidDataException(
                                "The movie receipt does not address the active movie."
                            );
                        _remoteMovieAck = envelope.movieID;
                        break;
                    case MultiplayerMessageType.TacticalResync:
                        RequireRole(MultiplayerRole.Host);
                        if (
                            _latestFrame != null
                            && Now() - _lastCheckpointAt >= CheckpointIntervalMilliseconds
                        )
                            SendCheckpoint();
                        break;
                    default:
                        throw new InvalidDataException("The packet is not a tactical event.");
                }
                _lastPeerActivity = Now();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Applies reliable events and newest state without coupling the host clock to receipts.
        /// </summary>
        public void Pump()
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                CheckDeadlines();
                while (_events.Count != 0 && Failure == null)
                {
                    if (
                        _events.Peek().Type == MultiplayerMessageType.TacticalCommand
                        && (!_localReady || !_remoteReady)
                    )
                        break;
                    Received item = _events.Dequeue();
                    _queuedBytes -= item.Payload.Length;
                    switch (item.Type)
                    {
                        case MultiplayerMessageType.TacticalCommand:
                            ApplyCommand(item);
                            break;
                        case MultiplayerMessageType.TacticalCommandResult:
                            ApplyResult(item);
                            break;
                        case MultiplayerMessageType.TacticalEffects:
                            RetainEffects(item);
                            break;
                        default:
                            ApplyFrame(item);
                            break;
                    }
                }
                if (_replaceableFrame != null && _localReady && _remoteReady)
                {
                    Received state = _replaceableFrame;
                    _replaceableFrame = null;
                    ApplyFrame(state);
                }
                ApplyReadyEffects();
                int movieID = _getMovieID();
                if (
                    _role == MultiplayerRole.Host
                    && movieID > _completedMovie
                    && _localMovieAck == movieID
                    && _remoteMovieAck == movieID
                )
                {
                    if (!_completeMovie(movieID))
                        throw new InvalidDataException(
                            "The acknowledged movie could not complete."
                        );
                    _completedMovie = movieID;
                }
                if (
                    CanAdvance
                    && _latestFrame != null
                    && Now() - _lastCheckpointAt >= CheckpointIntervalMilliseconds
                )
                    SendCheckpoint();
                else if (
                    _role == MultiplayerRole.Guest
                    && _localReady
                    && _remoteReady
                    && Now() - _lastStateAt >= ResyncIntervalMilliseconds
                    && Now() - _lastResyncAt >= ResyncIntervalMilliseconds
                )
                {
                    _lastResyncAt = Now();
                    Send(
                        MultiplayerMessageType.TacticalResync,
                        new Envelope { sequence = _lastFrame }
                    );
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Captures complete state and sends its effects through a separate reliable ordered batch.
        /// </summary>
        public void PublishFrame()
        {
            if (!CanAdvance || _terminalSequence != 0)
                return;
            try
            {
                long sequence = checked(_lastFrame + 1);
                TacticalCombatNetworkFrame frame =
                    _captureFrame(sequence)
                    ?? throw new InvalidDataException("The captured battle projection is missing.");
                if (frame.Sequence != sequence)
                    throw new InvalidDataException(
                        "The captured battle projection identity is inconsistent."
                    );
                TacticalCombatNetworkEffect[] effects =
                    frame.Effects
                    ?? throw new InvalidDataException("The captured battle effects are missing.");
                frame.Effects = Array.Empty<TacticalCombatNetworkEffect>();
                frame.TimestampMilliseconds = Now();
                long watermark =
                    effects.Length == 0 ? _lastPublishedEffect : checked(_lastPublishedEffect + 1);
                bool checkpoint =
                    _latestFrame == null
                    || frame.MovieID != _latestFrame.MovieID
                    || frame.Complete
                    || Now() - _lastCheckpointAt >= CheckpointIntervalMilliseconds;
                _latestFrame = frame;
                _latestEffectWatermark = watermark;
                _lastFrame = sequence;
                _lastPublishedEffect = watermark;
                if (frame.Complete)
                {
                    _terminalSequence = sequence;
                    _terminalEffects = watermark;
                    _terminalAt = Now();
                }
                if (checkpoint)
                    SendCheckpoint();
                else
                    Send(
                        MultiplayerMessageType.TacticalFrame,
                        new Envelope { sequence = sequence },
                        MultiplayerTacticalFrameCodec.Encode(frame)
                    );
                if (Failure == null && effects.Length != 0)
                    Send(
                        MultiplayerMessageType.TacticalEffects,
                        new Envelope
                        {
                            sequence = sequence,
                            effectSequence = watermark,
                            timestampMilliseconds = frame.TimestampMilliseconds,
                        },
                        MultiplayerTacticalFrameCodec.EncodeEffects(effects)
                    );
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Records local movie completion without releasing the other presentation's pause.
        /// </summary>
        /// <param name="movieID">The exact displayed movie identity.</param>
        public void AcknowledgeMovie(int movieID)
        {
            if (
                _disposed
                || Failure != null
                || movieID <= 0
                || movieID != _getMovieID()
                || _localMovieAck == movieID
            )
                return;
            _localMovieAck = movieID;
            if (_role == MultiplayerRole.Guest)
                Send(
                    MultiplayerMessageType.TacticalMovieReceipt,
                    new Envelope { movieID = movieID }
                );
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
        /// Applies an authenticated command once and replays only its latest duplicate outcome.
        /// </summary>
        /// <param name="item">The immutable captured command request.</param>
        private void ApplyCommand(Received item)
        {
            if (
                _lastCommands.TryGetValue(item.Envelope.actor, out Received previous)
                && item.Envelope.requestID <= previous.Envelope.requestID
            )
            {
                if (item.Envelope.requestID == previous.Envelope.requestID)
                {
                    if (!item.Payload.AsSpan().SequenceEqual(previous.Payload))
                        throw new InvalidDataException(
                            "A duplicate battle request changed its parameters."
                        );
                    SendResult(previous.Envelope);
                }
                return;
            }
            if (item.Envelope.actor == MultiplayerRole.Guest)
            {
                if (item.Envelope.requestID != checked(_lastRemoteRequestID + 1))
                    throw new InvalidDataException("The battle command sequence is incomplete.");
                _lastRemoteRequestID = item.Envelope.requestID;
            }
            TacticalCombatNetworkCommand command =
                JsonUtility.FromJson<TacticalCombatNetworkCommand>(
                    Encoding.UTF8.GetString(item.Payload)
                ) ?? throw new InvalidDataException("The battle command is missing.");
            CombatSide actor =
                item.Envelope.actor == MultiplayerRole.Host ? _hostSide
                : _hostSide == CombatSide.Attacker ? CombatSide.Defender
                : CombatSide.Attacker;
            item.Envelope.accepted = _localReady && _remoteReady && _applyCommand(actor, command);
            _lastCommands[item.Envelope.actor] = item;
            SendResult(item.Envelope);
        }

        /// <summary>
        /// Completes a guest request only for an identity it actually submitted.
        /// </summary>
        /// <param name="item">The authenticated host's command outcome.</param>
        private void ApplyResult(Received item)
        {
            if (
                item.Envelope.actor != MultiplayerRole.Guest
                || item.Envelope.requestID < 2000000001
                || item.Envelope.requestID >= _nextRequestID
            )
                throw new InvalidDataException(
                    "The battle outcome does not address a submitted command."
                );
            if (_requests.Remove(item.Envelope.requestID, out var completion))
            {
                _requestTimes.Remove(item.Envelope.requestID);
                completion.TrySetResult(
                    new PlayerRequestOutcome(
                        item.Envelope.accepted,
                        item.Envelope.accepted ? null : "The battle command is unavailable."
                    )
                );
            }
        }

        /// <summary>
        /// Returns an executed command outcome to its authenticated submitting role.
        /// </summary>
        /// <param name="envelope">The admitted command outcome.</param>
        private void SendResult(Envelope envelope)
        {
            if (envelope.actor == MultiplayerRole.Host)
            {
                if (_requests.Remove(envelope.requestID, out var completion))
                {
                    _requestTimes.Remove(envelope.requestID);
                    completion.TrySetResult(
                        new PlayerRequestOutcome(
                            envelope.accepted,
                            envelope.accepted ? null : "The battle command is unavailable."
                        )
                    );
                }
            }
            else
                Send(MultiplayerMessageType.TacticalCommandResult, envelope);
        }

        /// <summary>
        /// Applies newer complete states while permitting dropped or overtaken replaceable updates.
        /// </summary>
        /// <param name="item">The complete authoritative state or reliable checkpoint.</param>
        private void ApplyFrame(Received item)
        {
            if (
                !_localReady
                || !_remoteReady
                || item.Envelope.sequence <= 0
                || item.Envelope.effectSequence < 0
            )
                throw new InvalidDataException(
                    "The battle projection is unavailable or has an invalid identity."
                );
            if (item.Envelope.sequence > _lastFrame)
            {
                TacticalCombatNetworkFrame frame = MultiplayerTacticalFrameCodec.Decode(
                    item.Payload
                );
                if (
                    frame.Sequence != item.Envelope.sequence
                    || frame.Effects == null
                    || frame.Effects.Length != 0
                    || !double.IsFinite(frame.TimestampMilliseconds)
                )
                    throw new InvalidDataException(
                        "The battle projection identity or event separation is inconsistent."
                    );
                _applyFrame(frame);
                _lastFrame = frame.Sequence;
                _lastStateAt = Now();
            }
            if (item.Type == MultiplayerMessageType.TacticalCheckpoint)
            {
                if (
                    _checkpointReceipts.Count >= MaximumPendingEffectBatches
                    && !_checkpointReceipts.ContainsKey(item.Envelope.sequence)
                )
                    throw new InvalidDataException(
                        "Too many battle checkpoints await their effects."
                    );
                if (
                    _checkpointReceipts.TryGetValue(item.Envelope.sequence, out long existing)
                    && existing != item.Envelope.effectSequence
                )
                    throw new InvalidDataException(
                        "A repeated battle checkpoint changed its event fence."
                    );
                _checkpointReceipts[item.Envelope.sequence] = item.Envelope.effectSequence;
            }
            ApplyReadyEffects();
        }

        /// <summary>
        /// Retains each reliable effect batch once until sufficiently recent full state is available.
        /// </summary>
        /// <param name="item">The reliable batch and its required state identity.</param>
        private void RetainEffects(Received item)
        {
            if (
                item.Envelope.sequence <= 0
                || item.Envelope.effectSequence <= 0
                || !double.IsFinite(item.Envelope.timestampMilliseconds)
                || item.Envelope.timestampMilliseconds < 0
            )
                throw new InvalidDataException(
                    "The battle effect identity or timestamp is invalid."
                );
            if (item.Envelope.effectSequence <= _lastReceivedEffect)
            {
                if (
                    item.Envelope.effectSequence == _lastReceivedEffect
                    && !_lastEffectPayload.AsSpan().SequenceEqual(item.Payload)
                )
                    throw new InvalidDataException(
                        "A repeated battle effect batch changed its contents."
                    );
                return;
            }
            if (item.Envelope.effectSequence != checked(_lastReceivedEffect + 1))
                throw new InvalidDataException(
                    "The reliable battle effect sequence is incomplete."
                );
            if (
                _pendingEffects.Count >= MaximumPendingEffectBatches
                || item.Payload.Length > MaximumQueuedBytes - _pendingEffectBytes
            )
                throw new InvalidDataException("Too many battle effects await applicable state.");
            _lastReceivedEffect = item.Envelope.effectSequence;
            _lastEffectPayload = item.Payload;
            _pendingEffectBytes += item.Payload.Length;
            _pendingEffects.Enqueue(item);
            ApplyReadyEffects();
        }

        /// <summary>
        /// Applies effects once at their playback time before acknowledging the associated checkpoint fences.
        /// </summary>
        private void ApplyReadyEffects()
        {
            double cursor = _presentationTimestamp?.Invoke() ?? double.PositiveInfinity;
            if (double.IsNaN(cursor))
                throw new InvalidDataException("The battle presentation timestamp is invalid.");
            while (
                _pendingEffects.Count != 0
                && _pendingEffects.Peek().Envelope.sequence <= _lastFrame
                && _pendingEffects.Peek().Envelope.timestampMilliseconds <= cursor
            )
            {
                Received item = _pendingEffects.Dequeue();
                _pendingEffectBytes -= item.Payload.Length;
                TacticalCombatNetworkEffect[] effects = MultiplayerTacticalFrameCodec.DecodeEffects(
                    item.Payload
                );
                if (effects == null || effects.Length == 0)
                    throw new InvalidDataException("The reliable battle effect batch is empty.");
                _applyEffects(effects);
                _lastAppliedEffect = item.Envelope.effectSequence;
            }
            List<long> acknowledged = null;
            foreach (var checkpoint in _checkpointReceipts)
            {
                if (checkpoint.Key > _lastFrame || checkpoint.Value > _lastAppliedEffect)
                    continue;
                Send(
                    MultiplayerMessageType.TacticalFrameReceipt,
                    new Envelope { sequence = checkpoint.Key, effectSequence = checkpoint.Value }
                );
                (acknowledged ??= new()).Add(checkpoint.Key);
            }
            if (acknowledged != null)
                foreach (long sequence in acknowledged)
                    _checkpointReceipts.Remove(sequence);
        }

        /// <summary>
        /// Sends the latest captured state without consuming newly accumulated simulation effects.
        /// </summary>
        private void SendCheckpoint()
        {
            _lastCheckpointAt = Now();
            Send(
                MultiplayerMessageType.TacticalCheckpoint,
                new Envelope
                {
                    sequence = _latestFrame.Sequence,
                    effectSequence = _latestEffectWatermark,
                },
                MultiplayerTacticalFrameCodec.Encode(_latestFrame)
            );
        }

        /// <summary>
        /// Enforces bounded reliable retention before the next main-thread pump.
        /// </summary>
        /// <param name="item">The authenticated reliable event.</param>
        private void Enqueue(Received item)
        {
            if (
                _events.Count >= MaximumQueuedEvents
                || item.Payload.Length > MaximumQueuedBytes - _queuedBytes
            )
                throw new InvalidDataException("The battle event queue exceeded its limit.");
            _queuedBytes += item.Payload.Length;
            _events.Enqueue(item);
        }

        /// <summary>
        /// Applies message-specific limits before decoding or retaining peer-controlled bytes.
        /// </summary>
        /// <param name="packet">The addressed authenticated packet.</param>
        private static void ValidatePayload(MultiplayerPacket packet)
        {
            int limit = packet.Type switch
            {
                MultiplayerMessageType.TacticalFrame or MultiplayerMessageType.TacticalCheckpoint =>
                    MultiplayerTacticalFrameCodec.MaxFramePayloadBytes,
                MultiplayerMessageType.TacticalEffects =>
                    MultiplayerTacticalFrameCodec.MaxEffectsPayloadBytes,
                MultiplayerMessageType.TacticalCommand => MaximumCommandBytes,
                _ => 0,
            };
            if (packet.Payload.Length > limit)
                throw new InvalidDataException("The battle event exceeds its payload limit.");
        }

        /// <summary>
        /// Fails stalled preparation, peer traffic, command confirmations, or terminal agreement.
        /// </summary>
        private void CheckDeadlines()
        {
            double now = Now();
            if ((!_localReady || !_remoteReady) && now - _createdAt >= ReadinessTimeoutMilliseconds)
                throw new TimeoutException("The peer did not finish preparing the shared battle.");
            if (
                _terminalSequence != 0
                && !_terminalAcknowledged
                && now - _terminalAt >= TerminalTimeoutMilliseconds
            )
                throw new TimeoutException(
                    "The peer did not confirm final battle state and effects."
                );
            if (
                _localReady
                && _remoteReady
                && now - _lastPeerActivity >= InactivityTimeoutMilliseconds
            )
                throw new TimeoutException("The shared battle peer stopped responding.");
            foreach (double submitted in _requestTimes.Values)
                if (now - submitted >= InactivityTimeoutMilliseconds)
                    throw new TimeoutException(
                        "A battle command was not confirmed before its deadline."
                    );
        }

        /// <summary>
        /// Reads the configured finite monotonic time source.
        /// </summary>
        /// <returns>The elapsed milliseconds.</returns>
        private double Now()
        {
            double value = _milliseconds();
            if (!double.IsFinite(value) || value < 0)
                throw new InvalidOperationException("The battle transport clock is invalid.");
            return value;
        }

        /// <summary>
        /// Adds both negotiated identities to event metadata.
        /// </summary>
        /// <param name="envelope">The event metadata.</param>
        /// <returns>The addressed event metadata.</returns>
        private Envelope CreateEnvelope(Envelope envelope)
        {
            envelope.initializationID = _initializationID;
            envelope.battleID = _battleID;
            return envelope;
        }

        /// <summary>
        /// Sends an addressed packet through its message-specific transport policy.
        /// </summary>
        /// <param name="type">The closed wire discriminator.</param>
        /// <param name="envelope">The event metadata.</param>
        /// <param name="payload">The closed typed payload.</param>
        private void Send(MultiplayerMessageType type, Envelope envelope, byte[] payload = null)
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                _send(
                    new MultiplayerPacket(
                        type,
                        JsonUtility.ToJson(CreateEnvelope(envelope)),
                        payload
                    )
                );
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Rejects authority-bearing traffic delivered to the other role.
        /// </summary>
        /// <param name="role">The required receiving role.</param>
        private void RequireRole(MultiplayerRole role)
        {
            if (_role != role)
                throw new InvalidDataException("The battle event was sent to the wrong role.");
        }

        /// <summary>
        /// Records synchronization failure and releases retained work once.
        /// </summary>
        /// <param name="exception">The failed transport or domain boundary.</param>
        private void Fail(Exception exception)
        {
            if (_disposed || Failure != null)
                return;
            Failure = exception;
            CompleteUnknown();
            ClearTraffic();
            Faulted?.Invoke(exception);
        }

        /// <summary>
        /// Releases bounded traffic retained by a failed or disposed battle.
        /// </summary>
        private void ClearTraffic()
        {
            _events.Clear();
            _pendingEffects.Clear();
            _checkpointReceipts.Clear();
            _replaceableFrame = null;
            _latestFrame = null;
            _lastEffectPayload = null;
            _queuedBytes = _pendingEffectBytes = 0;
        }

        /// <summary>
        /// Completes unresolved operations without falsely reporting a domain rejection.
        /// </summary>
        private void CompleteUnknown()
        {
            foreach (var completion in _requests.Values)
                completion.TrySetResult(Unknown());
            _requests.Clear();
            _requestTimes.Clear();
        }

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
