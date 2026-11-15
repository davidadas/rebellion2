using System;
using System.Collections.Generic;
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
    /// Authenticates battle commands and acknowledges authoritative projections over the admitted ordered connection.
    /// </summary>
    internal sealed class MultiplayerTacticalProtocol : IDisposable
    {
        private readonly MultiplayerRole _role;
        private readonly string _initializationID;
        private readonly string _battleID;
        private readonly CombatSide _hostSide;
        private readonly Func<CombatSide, TacticalCombatNetworkCommand, bool> _applyCommand;
        private readonly Func<long, TacticalCombatNetworkFrame> _captureFrame;
        private readonly Action<TacticalCombatNetworkFrame> _applyFrame;
        private readonly Func<int> _getMovieID;
        private readonly Func<int, bool> _completeMovie;
        private readonly Action<MultiplayerPacket> _send;
        private readonly Queue<Received> _events = new();
        private readonly Dictionary<long, TaskCompletionSource<PlayerRequestOutcome>> _requests =
            new();
        private readonly Dictionary<MultiplayerRole, Received> _lastCommands = new();
        private long _nextRequestID;
        private long _lastRemoteRequestID;
        private long _lastFrame;
        private MultiplayerPacket _pendingFrame;
        private bool _localReady;
        private bool _remoteReady;
        private int _localMovieAck;
        private int _remoteMovieAck;
        private int _completedMovie;
        private bool _disposed;

        public Exception Failure { get; private set; }
        public bool CanAdvance =>
            !_disposed
            && Failure == null
            && _role == MultiplayerRole.Host
            && _localReady
            && _remoteReady
            && _pendingFrame == null;
        public event Action<Exception> Faulted;

        /// <summary>
        /// Connects one battle to the authenticated host and guest without replaying the guest simulation.
        /// </summary>
        /// <param name="role">The authenticated local role.</param>
        /// <param name="initializationID">The initialized campaign identity.</param>
        /// <param name="battleID">The ordered host-issued encounter identity.</param>
        /// <param name="hostSide">The encounter side bound to the host faction.</param>
        /// <param name="applyCommand">Applies a command using the authenticated actor's side.</param>
        /// <param name="captureFrame">Captures an authoritative frame.</param>
        /// <param name="applyFrame">Applies one projection without advancing the replica.</param>
        /// <param name="getMovieID">Reads the currently paused movie identity.</param>
        /// <param name="completeMovie">Releases the movie after both presentations acknowledge it.</param>
        /// <param name="send">The admitted reliable ordered transport.</param>
        public MultiplayerTacticalProtocol(
            MultiplayerRole role,
            string initializationID,
            string battleID,
            CombatSide hostSide,
            Func<CombatSide, TacticalCombatNetworkCommand, bool> applyCommand,
            Func<long, TacticalCombatNetworkFrame> captureFrame,
            Action<TacticalCombatNetworkFrame> applyFrame,
            Func<int> getMovieID,
            Func<int, bool> completeMovie,
            Action<MultiplayerPacket> send
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
            _getMovieID = getMovieID ?? throw new ArgumentNullException(nameof(getMovieID));
            _completeMovie =
                completeMovie ?? throw new ArgumentNullException(nameof(completeMovie));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _nextRequestID = role == MultiplayerRole.Host ? 1 : 2000000001;
            _lastRemoteRequestID = 2000000000;
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
        /// Submits a semantic intent without trusting an actor identity from its payload.
        /// </summary>
        /// <param name="command">The local view's captured intent.</param>
        /// <returns>The host's real admission result, or an unknown result after interruption.</returns>
        public Task<PlayerRequestOutcome> ExecuteAsync(TacticalCombatNetworkCommand command)
        {
            if (_disposed || Failure != null)
                return Task.FromResult(Unknown());
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            long requestID = _nextRequestID;
            _nextRequestID = checked(_nextRequestID + 1);
            if (_role == MultiplayerRole.Host && requestID >= 2000000001)
                throw new InvalidOperationException(
                    "The battle request identity range is exhausted."
                );
            TaskCompletionSource<PlayerRequestOutcome> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            _requests.Add(requestID, completion);
            Envelope envelope = new() { requestID = requestID, actor = _role };
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(command));
            if (_role == MultiplayerRole.Host)
                _events.Enqueue(
                    new Received(MultiplayerMessageType.TacticalCommand, envelope, payload)
                );
            else
                Send(MultiplayerMessageType.TacticalCommand, envelope, payload);
            return completion.Task;
        }

        /// <summary>
        /// Validates connection identities before retaining peer-controlled battle traffic.
        /// </summary>
        /// <param name="packet">The admitted peer's packet.</param>
        public void Receive(MultiplayerPacket packet)
        {
            if (_disposed || Failure != null)
                return;
            try
            {
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
                switch (packet.Type)
                {
                    case MultiplayerMessageType.TacticalReady:
                        _remoteReady = true;
                        break;
                    case MultiplayerMessageType.TacticalCommand:
                        RequireRole(MultiplayerRole.Host);
                        envelope.actor = MultiplayerRole.Guest;
                        if (envelope.requestID < 2000000001)
                            throw new InvalidDataException(
                                "The battle request identity is invalid."
                            );
                        _events.Enqueue(new Received(packet.Type, envelope, packet.Payload));
                        break;
                    case MultiplayerMessageType.TacticalCommandResult:
                    case MultiplayerMessageType.TacticalFrame:
                        RequireRole(MultiplayerRole.Guest);
                        _events.Enqueue(new Received(packet.Type, envelope, packet.Payload));
                        break;
                    case MultiplayerMessageType.TacticalFrameReceipt:
                        RequireRole(MultiplayerRole.Host);
                        if (envelope.sequence <= 0 || envelope.sequence > _lastFrame)
                            throw new InvalidDataException(
                                "The peer acknowledged an unpublished battle frame."
                            );
                        if (envelope.sequence == _lastFrame)
                            _pendingFrame = null;
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
                    default:
                        throw new InvalidDataException("The packet is not a tactical event.");
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Executes queued commands and projections on the presentation's main-thread boundary.
        /// </summary>
        public void Pump()
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                while (_events.Count != 0 && Failure == null)
                {
                    if (
                        _events.Peek().Type == MultiplayerMessageType.TacticalCommand
                        && (!_localReady || !_remoteReady)
                    )
                        break;
                    Received item = _events.Dequeue();
                    if (item.Type == MultiplayerMessageType.TacticalCommand)
                        ApplyCommand(item);
                    else if (item.Type == MultiplayerMessageType.TacticalCommandResult)
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
                            completion.TrySetResult(
                                new PlayerRequestOutcome(
                                    item.Envelope.accepted,
                                    item.Envelope.accepted
                                        ? null
                                        : "The battle command is unavailable."
                                )
                            );
                    }
                    else
                        ApplyFrame(item);
                }
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
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Publishes at most one unacknowledged complete projection, retaining effects until delivery.
        /// </summary>
        public void PublishFrame()
        {
            if (!CanAdvance)
                return;
            try
            {
                long sequence = checked(_lastFrame + 1);
                TacticalCombatNetworkFrame frame = _captureFrame(sequence);
                Envelope envelope = CreateEnvelope(new Envelope { sequence = sequence });
                _pendingFrame = new MultiplayerPacket(
                    MultiplayerMessageType.TacticalFrame,
                    JsonUtility.ToJson(envelope),
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame))
                );
                _lastFrame = sequence;
                _send(_pendingFrame);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Records one local movie completion without releasing the other presentation's pause.
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
        /// Stops transport work and leaves every unresolved command explicitly unconfirmed.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            CompleteUnknown();
            _events.Clear();
            _pendingFrame = null;
        }

        /// <summary>
        /// Stops both the battle and its campaign connection when presentation cannot continue.
        /// </summary>
        /// <param name="exception">The failed loading or presentation boundary.</param>
        public void ReportFailure(Exception exception) => Fail(exception);

        /// <summary>
        /// Applies one authenticated command once, replaying only its most recent duplicate outcome.
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
        /// Returns an executed command outcome to its submitting role.
        /// </summary>
        /// <param name="envelope">The immutable admitted command outcome.</param>
        private void SendResult(Envelope envelope)
        {
            if (envelope.actor == MultiplayerRole.Host)
            {
                if (_requests.Remove(envelope.requestID, out var completion))
                    completion.TrySetResult(
                        new PlayerRequestOutcome(
                            envelope.accepted,
                            envelope.accepted ? null : "The battle command is unavailable."
                        )
                    );
            }
            else
                Send(MultiplayerMessageType.TacticalCommandResult, envelope);
        }

        /// <summary>
        /// Applies each ordered frame and its effects once, acknowledging duplicates without replay.
        /// </summary>
        /// <param name="item">The host projection.</param>
        private void ApplyFrame(Received item)
        {
            if (item.Envelope.sequence <= 0)
                throw new InvalidDataException("The battle projection identity is invalid.");
            if (item.Envelope.sequence > _lastFrame)
            {
                if (!_localReady || !_remoteReady || item.Envelope.sequence != _lastFrame + 1)
                    throw new InvalidDataException("The battle projection sequence is incomplete.");
                TacticalCombatNetworkFrame frame =
                    JsonUtility.FromJson<TacticalCombatNetworkFrame>(
                        Encoding.UTF8.GetString(item.Payload)
                    ) ?? throw new InvalidDataException("The battle projection is missing.");
                if (frame.Sequence != item.Envelope.sequence)
                    throw new InvalidDataException(
                        "The battle projection identity is inconsistent."
                    );
                _applyFrame(frame);
                _lastFrame = item.Envelope.sequence;
            }
            Send(MultiplayerMessageType.TacticalFrameReceipt, item.Envelope);
        }

        /// <summary>
        /// Adds both negotiated identities to one metadata value.
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
        /// Sends one addressed packet without executing peer callbacks reentrantly.
        /// </summary>
        /// <param name="type">The closed wire discriminator.</param>
        /// <param name="envelope">The event metadata.</param>
        /// <param name="payload">The closed typed payload.</param>
        private void Send(MultiplayerMessageType type, Envelope envelope, byte[] payload = null)
        {
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
        /// Records a terminal synchronization failure and releases command waiters once.
        /// </summary>
        /// <param name="exception">The failed transport or domain boundary.</param>
        private void Fail(Exception exception)
        {
            if (_disposed || Failure != null)
                return;
            Failure = exception;
            CompleteUnknown();
            Faulted?.Invoke(exception);
        }

        /// <summary>
        /// Completes unresolved operations without falsely reporting domain rejection.
        /// </summary>
        private void CompleteUnknown()
        {
            foreach (var completion in _requests.Values)
                completion.TrySetResult(Unknown());
            _requests.Clear();
        }

        /// <summary>
        /// Creates the interrupted-operation outcome shared by disconnect and transport failure.
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
            /// Retains received bytes until the main-thread battle boundary admits them.
            /// </summary>
            /// <param name="type">The closed event discriminator.</param>
            /// <param name="envelope">The authenticated metadata.</param>
            /// <param name="payload">The immutable typed parameter bytes.</param>
            public Received(MultiplayerMessageType type, Envelope envelope, byte[] payload)
            {
                Type = type;
                Envelope = envelope;
                Payload = payload;
            }
        }
    }
}
