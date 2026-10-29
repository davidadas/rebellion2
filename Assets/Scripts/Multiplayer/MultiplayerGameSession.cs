using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.App;
using Rebellion.App.PlayerActions;
using Rebellion.Game;
using Rebellion.Gameplay;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Orders player requests and day barriers without replacing the live game or presentation.
    /// The host validates requests; each peer executes committed requests once on its own session.
    /// </summary>
    internal sealed class MultiplayerGameSession : IPlayerActionDispatcher, IDisposable
    {
        private readonly MultiplayerRole _role;
        private readonly string _hostFactionID;
        private readonly string _guestFactionID;
        private readonly string _initializationID;
        private readonly GameSession _session;
        private readonly PlayerActionExecutor _executor;
        private readonly Action<MultiplayerPacket> _send;
        private readonly Queue<ReceivedEvent> _events = new();
        private readonly Queue<Envelope> _dayReceipts = new();
        private readonly Dictionary<long, TaskCompletionSource<PlayerActionResult>> _requests =
            new();
        private readonly Dictionary<long, Envelope> _awaitingReceipts = new();
        private readonly Dictionary<long, ReceivedEvent> _completedRequests = new();
        private long _nextRequestID;
        private long _lastHostRequestID;
        private long _lastGuestRequestID = 2000000000;
        private long _lastSequence;
        private long _lastAppliedSequence;
        private int _expectedTick;
        private Envelope _localDayReceipt;
        private Envelope _remoteDayReceipt;
        private bool _guestDayPending;
        private bool _dayStarted;
        private bool _verificationPending;
        private bool _disposed;

        /// <summary>
        /// Gets the synchronization failure, including failures after a partially applied operation.
        /// </summary>
        public Exception Failure { get; private set; }

        /// <summary>
        /// Gets whether the host may consume another configured clock interval.
        /// </summary>
        public bool CanBeginHostTick =>
            !_disposed
            && Failure == null
            && _role == MultiplayerRole.Host
            && !_dayStarted
            && _events.Count == 0
            && _awaitingReceipts.Count == 0
            && _session.Tick.IsSettled;

        /// <summary>
        /// Raised after a committed operation changes the existing live state.
        /// </summary>
        public event Action StateChanged;

        /// <summary>
        /// Raised when continued replay cannot be established as safe.
        /// </summary>
        public event Action<Exception> Faulted;

        /// <summary>
        /// Connects ordered replay to an already synchronized pair of human factions.
        /// </summary>
        /// <param name="role">The local authenticated session role.</param>
        /// <param name="hostFactionID">The faction bound to the host.</param>
        /// <param name="guestFactionID">The faction bound to the admitted guest.</param>
        /// <param name="session">The stable local simulation session.</param>
        /// <param name="clock">The local session clock.</param>
        /// <param name="send">The reliable ordered peer transport.</param>
        /// <param name="initializationID">The host-issued identity agreed for this exact initialized game.</param>
        public MultiplayerGameSession(
            MultiplayerRole role,
            string hostFactionID,
            string guestFactionID,
            GameSession session,
            GameManager clock,
            Action<MultiplayerPacket> send,
            string initializationID
        )
        {
            if (role != MultiplayerRole.Host && role != MultiplayerRole.Guest)
                throw new ArgumentOutOfRangeException(nameof(role));
            if (
                string.IsNullOrEmpty(hostFactionID)
                || string.IsNullOrEmpty(guestFactionID)
                || hostFactionID == guestFactionID
            )
                throw new ArgumentException("Two distinct player factions are required.");
            _role = role;
            if (!Guid.TryParseExact(initializationID, "N", out _))
                throw new ArgumentException(
                    "A negotiated initialization identity is required.",
                    nameof(initializationID)
                );
            _initializationID = initializationID;
            _hostFactionID = hostFactionID;
            _guestFactionID = guestFactionID;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (!session.CanExecuteCommands)
                throw new InvalidOperationException(
                    "An executing simulation operation cannot enter ordered gameplay."
                );
            _executor = new PlayerActionExecutor(session, clock);
            clock.EnableCoordinatedSpeeds();
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _nextRequestID = role == MultiplayerRole.Host ? 1 : 2000000001;
            _expectedTick = session.Game.CurrentTick;
            if (!session.Tick.IsSettled)
            {
                if (
                    !session.CanExecuteCommands
                    || !session.GetService<SpaceCombatCommands>().HasPendingDecision
                )
                    throw new InvalidOperationException(
                        "An executing simulation phase cannot enter ordered gameplay."
                    );
                StartDay(session.Game.CurrentTick);
            }
            _session.Tick.TickCompleted += HandleTickCompleted;
        }

        /// <summary>
        /// Queues a local request and completes it only after domain execution.
        /// </summary>
        /// <param name="action">The local player's typed operation.</param>
        /// <returns>The execution result, or an unknown outcome when the connection is lost.</returns>
        public Task<PlayerActionResult> ExecuteAsync(PlayerAction action)
        {
            ThrowIfUnavailable();
            byte[] payload = MultiplayerPlayerActionCodec.Serialize(action);
            long requestID = _nextRequestID;
            if (_role == MultiplayerRole.Host && requestID >= 2000000001)
                throw new InvalidOperationException(
                    "The host request identity range is exhausted."
                );
            _nextRequestID = checked(_nextRequestID + 1);
            TaskCompletionSource<PlayerActionResult> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            _requests.Add(requestID, completion);
            Envelope envelope = new()
            {
                requestID = requestID,
                kind = action.Kind,
                actor = _role,
            };
            if (_role == MultiplayerRole.Host)
                _events.Enqueue(
                    new ReceivedEvent(MultiplayerMessageType.PlayerActionRequest, envelope, payload)
                );
            else
                Send(MultiplayerMessageType.PlayerActionRequest, envelope, payload);
            return completion.Task;
        }

        /// <summary>
        /// Receives an event without executing it inside the transport callback.
        /// </summary>
        /// <param name="packet">The admitted peer's complete packet.</param>
        public void Receive(MultiplayerPacket packet)
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                Envelope envelope =
                    JsonUtility.FromJson<Envelope>(packet.Metadata)
                    ?? throw new InvalidDataException("The game event metadata is missing.");
                if (
                    !MultiplayerSessionIdentity.Matches(
                        _initializationID,
                        envelope.initializationID
                    )
                )
                    return;
                switch (packet.Type)
                {
                    case MultiplayerMessageType.PlayerActionRequest:
                        RequireRole(MultiplayerRole.Host);
                        envelope.actor = MultiplayerRole.Guest;
                        if (envelope.requestID < 2000000001)
                            throw new InvalidDataException(
                                "The guest request identity is invalid."
                            );
                        _events.Enqueue(new ReceivedEvent(packet.Type, envelope, packet.Payload));
                        break;
                    case MultiplayerMessageType.PlayerActionCommit:
                    case MultiplayerMessageType.AdvanceDay:
                        RequireRole(MultiplayerRole.Guest);
                        _events.Enqueue(new ReceivedEvent(packet.Type, envelope, packet.Payload));
                        break;
                    case MultiplayerMessageType.PlayerActionRejected:
                        RequireRole(MultiplayerRole.Guest);
                        CompleteRequest(
                            envelope.requestID,
                            new PlayerActionResult(false, envelope.reason)
                        );
                        break;
                    case MultiplayerMessageType.PlayerActionReceipt:
                        RequireRole(MultiplayerRole.Host);
                        ReceiveActionReceipt(envelope);
                        break;
                    case MultiplayerMessageType.DayReceipt:
                        if (
                            envelope.tick > _expectedTick
                            && (
                                _role != MultiplayerRole.Guest
                                || !_events.Any(item =>
                                    item.Type == MultiplayerMessageType.AdvanceDay
                                    && item.Envelope.tick == envelope.tick
                                )
                            )
                        )
                            throw new InvalidDataException(
                                "The peer acknowledged a day that was not admitted."
                            );
                        _dayReceipts.Enqueue(envelope);
                        break;
                    case MultiplayerMessageType.SynchronizationFailed:
                        Fail(
                            new InvalidOperationException(
                                envelope.reason ?? "The peer could not continue synchronization."
                            ),
                            false
                        );
                        break;
                    default:
                        throw new InvalidDataException("The packet is not an ordered game event.");
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Executes queued operations only at a writable simulation boundary.
        /// </summary>
        public void Pump()
        {
            if (_disposed || Failure != null)
                return;
            if (_session.CommandFailure != null)
            {
                Fail(_session.CommandFailure);
                return;
            }
            if (_guestDayPending)
                return;
            try
            {
                if (_verificationPending && _session.CanExecuteCommands)
                {
                    _localDayReceipt = CaptureReceipt();

                    _localDayReceipt.stateFingerprint =
                        MultiplayerStateFingerprint.ComputeSimulation(_session);
                    _verificationPending = false;
                    Send(MultiplayerMessageType.DayReceipt, _localDayReceipt);
                    CompleteDayBarrier();
                }
                DrainDayReceipts();
                while (_events.Count > 0 && Failure == null && !_disposed)
                {
                    if (!_session.CanExecuteCommands || (_dayStarted && _session.Tick.IsSettled))
                        break;
                    ReceivedEvent item = _events.Dequeue();
                    if (_role == MultiplayerRole.Host)
                        ExecuteHostRequest(item);
                    else if (item.Type == MultiplayerMessageType.PlayerActionCommit)
                        ExecuteGuestCommit(item);
                    else
                    {
                        ReceiveDay(item.Envelope);
                        DrainDayReceipts();
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Starts a shared day after preceding operations and the previous day are acknowledged.
        /// </summary>
        /// <param name="hostClockElapsed">Whether the host's configured interval has elapsed.</param>
        /// <returns>Whether the caller should begin its normal incremental tick.</returns>
        public bool TryBeginTick(bool hostClockElapsed)
        {
            if (_disposed || Failure != null || !_session.CanExecuteCommands)
                return false;
            if (_role == MultiplayerRole.Guest)
            {
                if (!_guestDayPending)
                    return false;
                _guestDayPending = false;
                return true;
            }
            if (!hostClockElapsed || !CanBeginHostTick)
                return false;
            Envelope day = CaptureReceipt();
            day.tick = checked(day.tick + 1);
            day.sequence = checked(++_lastSequence);
            StartDay(day.tick);
            Send(MultiplayerMessageType.AdvanceDay, day);
            return Failure == null;
        }

        /// <summary>
        /// Stops both peers when tick execution or asynchronous transport fails outside the event pump.
        /// </summary>
        /// <param name="exception">The failure that prevents safe continued replay.</param>
        public void ReportFailure(Exception exception)
        {
            Fail(exception ?? throw new ArgumentNullException(nameof(exception)));
        }

        /// <summary>
        /// Completes unresolved requests without confirmation and releases connection-owned callbacks.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _session.Tick.TickCompleted -= HandleTickCompleted;
            CompleteUnconfirmedRequests();
            _events.Clear();
            _dayReceipts.Clear();
            _verificationPending = false;
            _awaitingReceipts.Clear();
            _completedRequests.Clear();
        }

        /// <summary>
        /// Validates and executes one local or admitted-guest request on the ordering host.
        /// </summary>
        /// <param name="item">The typed request and its immutable parameter bytes.</param>
        private void ExecuteHostRequest(ReceivedEvent item)
        {
            Envelope envelope = item.Envelope;
            if (_completedRequests.TryGetValue(envelope.requestID, out ReceivedEvent previous))
            {
                Send(
                    MultiplayerMessageType.PlayerActionCommit,
                    previous.Envelope,
                    previous.Payload
                );
                return;
            }
            long appliedRequestID =
                envelope.actor == MultiplayerRole.Host ? _lastHostRequestID : _lastGuestRequestID;
            if (envelope.requestID <= appliedRequestID)
                return;
            PlayerAction action = MultiplayerPlayerActionCodec.Deserialize(
                envelope.kind,
                item.Payload
            );
            PlayerActionResult result = _executor.Execute(GetFaction(envelope.actor), action);
            envelope.accepted = result.Accepted;
            envelope.reason = result.RejectionReason;
            envelope.createdEntityID = result.CreatedEntityID ?? string.Empty;
            _completedRequests.Add(envelope.requestID, item);
            if (envelope.actor == MultiplayerRole.Host)
                _lastHostRequestID = envelope.requestID;
            else
                _lastGuestRequestID = envelope.requestID;
            envelope.sequence = checked(++_lastSequence);
            _awaitingReceipts.Add(envelope.sequence, envelope);
            Send(MultiplayerMessageType.PlayerActionCommit, envelope, item.Payload);
            if (envelope.actor == MultiplayerRole.Host)
                CompleteRequest(envelope.requestID, result);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Replays a committed request against the existing guest graph and acknowledges its outcome.
        /// </summary>
        /// <param name="item">The host-ordered operation.</param>
        private void ExecuteGuestCommit(ReceivedEvent item)
        {
            Envelope envelope = item.Envelope;
            if (envelope.sequence <= _lastAppliedSequence)
            {
                Send(MultiplayerMessageType.PlayerActionReceipt, envelope);
                return;
            }
            if (envelope.sequence != _lastAppliedSequence + 1)
                throw new InvalidDataException("The ordered game event sequence is incomplete.");
            PlayerAction action = MultiplayerPlayerActionCodec.Deserialize(
                envelope.kind,
                item.Payload
            );
            PlayerActionResult result = _executor.Execute(GetFaction(envelope.actor), action);
            if (
                result.Accepted != envelope.accepted
                || !string.Equals(
                    result.CreatedEntityID ?? string.Empty,
                    envelope.createdEntityID ?? string.Empty,
                    StringComparison.Ordinal
                )
            )
                throw new InvalidDataException(
                    $"The committed {envelope.kind} operation produced a different outcome: "
                        + $"accepted {result.Accepted}/{envelope.accepted}; "
                        + $"entity '{result.CreatedEntityID ?? "<null>"}'/'{envelope.createdEntityID ?? "<null>"}'."
                );
            _lastAppliedSequence = envelope.sequence;
            Send(MultiplayerMessageType.PlayerActionReceipt, envelope);
            if (envelope.actor == MultiplayerRole.Guest)
                CompleteRequest(envelope.requestID, result);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Releases a committed host operation after the guest has applied it.
        /// </summary>
        /// <param name="envelope">The completed operation receipt.</param>
        private void ReceiveActionReceipt(Envelope envelope)
        {
            if (!_awaitingReceipts.TryGetValue(envelope.sequence, out Envelope expected))
                return;
            if (
                envelope.accepted != expected.accepted
                || envelope.createdEntityID != expected.createdEntityID
                || envelope.requestID != expected.requestID
            )
                throw new InvalidDataException(
                    "The peer could not apply the committed player operation."
                );
            _awaitingReceipts.Remove(envelope.sequence);
            _completedRequests.Remove(expected.requestID);
        }

        /// <summary>
        /// Validates the next day against the guest's completed simulation position.
        /// </summary>
        /// <param name="envelope">The host's next-day barrier.</param>
        private void ReceiveDay(Envelope envelope)
        {
            if (envelope.sequence <= _lastAppliedSequence)
                return;
            if (envelope.sequence != _lastAppliedSequence + 1)
                throw new InvalidDataException("The ordered game event sequence is incomplete.");
            GameRoot game = _session.Game;
            if (
                _dayStarted
                || !_session.Tick.IsSettled
                || envelope.tick != game.CurrentTick + 1
                || envelope.randomIndex != game.RandomIndex
                || envelope.identifierIndex != game.RuntimeIdentifierIndex
            )
                throw new InvalidDataException(
                    "The peer reached a different position before the next day."
                );
            _lastAppliedSequence = envelope.sequence;
            StartDay(envelope.tick);
            _guestDayPending = true;
        }

        /// <summary>
        /// Records the day being processed while clearing the preceding acknowledgements.
        /// </summary>
        /// <param name="tick">The expected completed day.</param>
        private void StartDay(int tick)
        {
            _expectedTick = tick;
            _dayStarted = true;
            _localDayReceipt = null;
            _remoteDayReceipt = null;
            _verificationPending = false;
        }

        /// <summary>
        /// Sends the completed local position without traversing or serializing the game graph.
        /// </summary>
        private void HandleTickCompleted()
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                if (!_dayStarted || _session.Game.CurrentTick != _expectedTick)
                    throw new InvalidDataException(
                        "The local simulation completed an unordered day."
                    );
                _verificationPending = true;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Captures the current day and deterministic stream positions without traversing the graph.
        /// </summary>
        /// <returns>The bounded simulation receipt.</returns>
        private Envelope CaptureReceipt()
        {
            return new Envelope
            {
                tick = _session.Game.CurrentTick,
                randomIndex = _session.Game.RandomIndex,
                identifierIndex = _session.Game.RuntimeIdentifierIndex,
            };
        }

        /// <summary>
        /// Opens the next day only after both peers have verified the same simulation continuation.
        /// </summary>
        private void CompleteDayBarrier()
        {
            if (_localDayReceipt == null || _remoteDayReceipt == null)
                return;
            if (
                _localDayReceipt.randomIndex != _remoteDayReceipt.randomIndex
                || _localDayReceipt.identifierIndex != _remoteDayReceipt.identifierIndex
                || _localDayReceipt.stateFingerprint != _remoteDayReceipt.stateFingerprint
            )
                throw new InvalidDataException(
                    "The peer simulation state diverged during the day."
                );
            _dayStarted = false;
        }

        /// <summary>
        /// Admits a receipt after an earlier start marker in the same transport batch.
        /// </summary>
        /// <param name="envelope">The completed peer position.</param>
        private void ReceiveDayReceipt(Envelope envelope)
        {
            if (envelope.tick == _expectedTick && !_dayStarted && _remoteDayReceipt != null)
                return;
            if (envelope.tick != _expectedTick || !_dayStarted)
                throw new InvalidDataException("The peer acknowledged an unexpected day.");
            _remoteDayReceipt = envelope;
            CompleteDayBarrier();
        }

        /// <summary>
        /// Processes verification control traffic independently of commands waiting at the day barrier.
        /// Receipts for a newly received start marker wait until that marker has been admitted.
        /// </summary>
        private void DrainDayReceipts()
        {
            while (_dayReceipts.Count > 0 && _dayReceipts.Peek().tick <= _expectedTick)
                ReceiveDayReceipt(_dayReceipts.Dequeue());
        }

        /// <summary>
        /// Resolves the actor from the authenticated role rather than a request-supplied faction.
        /// </summary>
        /// <param name="role">The role bound by this connection.</param>
        /// <returns>The authorized faction.</returns>
        private string GetFaction(MultiplayerRole role)
        {
            return role switch
            {
                MultiplayerRole.Host => _hostFactionID,
                MultiplayerRole.Guest => _guestFactionID,
                _ => throw new InvalidDataException("The operation has no admitted player."),
            };
        }

        /// <summary>
        /// Completes a local request after domain execution or an explicit host rejection.
        /// </summary>
        /// <param name="requestID">The original player request identity.</param>
        /// <param name="result">The completed outcome.</param>
        private void CompleteRequest(long requestID, PlayerActionResult result)
        {
            if (
                _requests.Remove(requestID, out TaskCompletionSource<PlayerActionResult> completion)
            )
                completion.TrySetResult(result);
        }

        /// <summary>
        /// Sends a typed event through the connection's ordered transport.
        /// </summary>
        /// <param name="type">The event type.</param>
        /// <param name="envelope">The bounded event metadata.</param>
        /// <param name="payload">The optional typed operation parameters.</param>
        private void Send(MultiplayerMessageType type, Envelope envelope, byte[] payload = null)
        {
            envelope.initializationID = _initializationID;
            try
            {
                _send(new MultiplayerPacket(type, JsonUtility.ToJson(envelope), payload));
            }
            catch (Exception exception)
            {
                Fail(exception, false);
            }
        }

        /// <summary>
        /// Rejects a peer event addressed to the other protocol role.
        /// </summary>
        /// <param name="required">The receiving role required by the event.</param>
        private void RequireRole(MultiplayerRole required)
        {
            if (_role != required)
                throw new InvalidDataException("The game event was sent to the wrong role.");
        }

        /// <summary>
        /// Rejects new requests after the connection has ended or replay has failed.
        /// </summary>
        private void ThrowIfUnavailable()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MultiplayerGameSession));
            if (Failure != null)
                throw new InvalidOperationException("Game synchronization has stopped.", Failure);
        }

        /// <summary>
        /// Stops replay rather than retrying a domain operation that may already have mutated state.
        /// </summary>
        /// <param name="exception">The replay failure.</param>
        /// <param name="notifyPeer">Whether the peer also needs to stop.</param>
        private void Fail(Exception exception, bool notifyPeer = true)
        {
            if (_disposed || Failure != null)
                return;
            Failure = exception;
            CompleteUnconfirmedRequests();
            if (notifyPeer)
            {
                try
                {
                    _send(
                        new MultiplayerPacket(
                            MultiplayerMessageType.SynchronizationFailed,
                            JsonUtility.ToJson(
                                new Envelope
                                {
                                    initializationID = _initializationID,
                                    reason = exception.Message,
                                }
                            )
                        )
                    );
                }
                catch (Exception)
                {
                    // The local failure must still pause gameplay when the peer cannot be notified.
                }
            }
            Faulted?.Invoke(exception);
        }

        /// <summary>
        /// Completes requests without claiming acceptance or rejection when their execution outcome is unknown.
        /// </summary>
        private void CompleteUnconfirmedRequests()
        {
            List<TaskCompletionSource<PlayerActionResult>> pending = new(_requests.Values);
            _requests.Clear();
            foreach (TaskCompletionSource<PlayerActionResult> completion in pending)
                completion.TrySetResult(
                    new PlayerActionResult(
                        false,
                        "The connection ended before the action outcome was confirmed.",
                        outcomeKnown: false
                    )
                );
        }

        /// <summary>
        /// Carries bounded ordering metadata independently of the model parameters.
        /// </summary>
        [Serializable]
        private sealed class Envelope
        {
            public string initializationID;
            public long requestID;
            public long sequence;
            public PlayerActionKind kind;
            public MultiplayerRole actor;
            public bool accepted;
            public string reason;
            public string createdEntityID;
            public int tick;
            public long randomIndex;
            public string stateFingerprint;
            public long identifierIndex;
        }

        /// <summary>
        /// Holds immutable parameter bytes until the local simulation is writable.
        /// </summary>
        private sealed class ReceivedEvent
        {
            public MultiplayerMessageType Type { get; }
            public Envelope Envelope { get; }
            public byte[] Payload { get; }

            /// <summary>
            /// Captures a received event without invoking domain or presentation code.
            /// </summary>
            /// <param name="type">The event type.</param>
            /// <param name="envelope">The ordering metadata.</param>
            /// <param name="payload">The immutable operation parameter bytes.</param>
            public ReceivedEvent(MultiplayerMessageType type, Envelope envelope, byte[] payload)
            {
                Type = type;
                Envelope = envelope;
                Payload = payload;
            }
        }
    }
}
