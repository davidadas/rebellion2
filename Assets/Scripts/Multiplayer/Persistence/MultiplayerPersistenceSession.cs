using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Reserves a shared settled boundary before durable checkpoints or an agreed scene transfer.
    /// Storage and content preparation are supplied by the application owner.
    /// </summary>
    internal sealed class MultiplayerPersistenceSession : IDisposable
    {
        private readonly MultiplayerRole _role;
        private readonly string _connectionID;
        private readonly Action<MultiplayerPacket> _send;
        private readonly Func<bool> _canCheckpoint;
        private readonly Func<
            MultiplayerPersistenceCommand,
            byte[],
            Task<MultiplayerPersistenceCheckpoint>
        > _prepareHost;
        private readonly Func<MultiplayerPersistenceCheckpoint, Task> _prepareGuest;
        private readonly Action<MultiplayerPersistenceCheckpoint> _beginTransfer;
        private readonly Queue<MultiplayerPacket> _events = new();
        private readonly Queue<MultiplayerPersistenceCheckpoint> _pending = new();
        private readonly Dictionary<long, TaskCompletionSource<bool>> _requests = new();
        private MultiplayerPersistenceCommand _current;
        private MultiplayerPersistenceCheckpoint _checkpoint;
        private byte[] _sourcePayload;
        private long _nextRequestID;
        private long _lastGuestRequestID;
        private bool _reserved;
        private bool _preparing;
        private bool _guestPrepared;
        private bool _readySent;
        private bool _autosaveQueued;
        private bool _disposed;
        private string _lastCompletedToken;

        /// <summary>
        /// Gets the failure that prevents further shared checkpoint completion.
        /// </summary>
        public Exception Failure { get; private set; }

        /// <summary>
        /// Gets whether ordered domain execution must remain frozen between participant copies.
        /// </summary>
        public bool BlocksGameplay => _reserved;

        /// <summary>
        /// Gets whether the host must settle its outstanding receipts instead of starting another day.
        /// </summary>
        public bool BlocksNewTicks =>
            _reserved || _role == MultiplayerRole.Host && _pending.Count > 0;

        /// <summary>
        /// Gets whether the admitted connection owns the replacement strategy scenes.
        /// </summary>
        public bool IsTransferringScene { get; private set; }

        /// <summary>
        /// Reports checkpoint failures without concealing a possible partial durable write.
        /// </summary>
        public event Action<Exception> Faulted;

        /// <summary>
        /// Reports a completed shared checkpoint for durable lifecycle recovery.
        /// </summary>
        public event Action CheckpointCompleted;

        /// <summary>
        /// Connects shared persistence ordering to explicit transport, stability, and durable operations.
        /// </summary>
        /// <param name="role">The authenticated local connection role.</param>
        /// <param name="send">The reliable ordered peer transport.</param>
        /// <param name="canCheckpoint">Reports the current core's settled receipt barrier.</param>
        /// <param name="prepareHost">Writes the host copy or prepares its authoritative load.</param>
        /// <param name="prepareGuest">Writes the peer copy or verifies its required load content.</param>
        /// <param name="beginTransfer">Starts the already agreed load transition.</param>
        /// <param name="connectionID">The initialized connection owning this persistence channel.</param>
        public MultiplayerPersistenceSession(
            MultiplayerRole role,
            Action<MultiplayerPacket> send,
            Func<bool> canCheckpoint,
            Func<
                MultiplayerPersistenceCommand,
                byte[],
                Task<MultiplayerPersistenceCheckpoint>
            > prepareHost,
            Func<MultiplayerPersistenceCheckpoint, Task> prepareGuest,
            Action<MultiplayerPersistenceCheckpoint> beginTransfer,
            string connectionID
        )
        {
            if (role != MultiplayerRole.Host && role != MultiplayerRole.Guest)
                throw new ArgumentOutOfRangeException(nameof(role));
            _role = role;
            if (!Guid.TryParseExact(connectionID, "N", out _))
                throw new ArgumentException(
                    "An initialized connection is required.",
                    nameof(connectionID)
                );
            _connectionID = connectionID;
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _canCheckpoint =
                canCheckpoint ?? throw new ArgumentNullException(nameof(canCheckpoint));
            _prepareHost = prepareHost ?? throw new ArgumentNullException(nameof(prepareHost));
            _prepareGuest = prepareGuest ?? throw new ArgumentNullException(nameof(prepareGuest));
            _beginTransfer =
                beginTransfer ?? throw new ArgumentNullException(nameof(beginTransfer));
        }

        /// <summary>
        /// Requests a manual operation without treating transport acceptance as durable completion.
        /// </summary>
        /// <param name="fileName">The validated local selection or logical save identifier.</param>
        /// <param name="displayName">The requested save label.</param>
        /// <param name="load">Whether to restore instead of save.</param>
        /// <param name="source">The requesting guest's selected saved world when loading.</param>
        /// <returns>The acknowledged shared completion.</returns>
        public Task<bool> RequestAsync(
            string fileName,
            string displayName,
            bool load,
            MultiplayerSnapshot source = null
        )
        {
            ThrowIfUnavailable();
            SaveGameManager.ValidateSaveFileName(fileName);
            if (
                _role == MultiplayerRole.Guest
                && load
                && (source == null || source.Payload.Length == 0)
            )
                throw new ArgumentException(
                    "A guest load requires its selected saved world.",
                    nameof(source)
                );
            if (source != null && (!load || _role != MultiplayerRole.Guest))
                throw new ArgumentException(
                    "Only a guest load supplies a source snapshot.",
                    nameof(source)
                );
            long requestID = checked(++_nextRequestID);
            TaskCompletionSource<bool> completion = new();
            _requests.Add(requestID, completion);
            MultiplayerPersistenceCommand command = new()
            {
                requestID = requestID,
                guestRequest = _role == MultiplayerRole.Guest,
                fileName = fileName,
                displayName = displayName,
                load = load,
                snapshotHash = source?.Hash,
            };
            byte[] sourcePayload = source == null ? null : (byte[])source.Payload.Clone();
            if (_role == MultiplayerRole.Host)
                _pending.Enqueue(new MultiplayerPersistenceCheckpoint { Command = command });
            else
                Send(MultiplayerMessageType.PersistenceRequest, command, sourcePayload);
            return completion.Task;
        }

        /// <summary>
        /// Coalesces a host-scheduled checkpoint ahead of loads that could replace its completed day.
        /// </summary>
        public void RequestAutosave()
        {
            ThrowIfUnavailable();
            if (_role != MultiplayerRole.Host || _autosaveQueued)
                return;
            _autosaveQueued = true;
            MultiplayerPersistenceCheckpoint[] waiting = _pending.ToArray();
            _pending.Clear();
            _pending.Enqueue(
                new MultiplayerPersistenceCheckpoint
                {
                    Command = new MultiplayerPersistenceCommand
                    {
                        automatic = true,
                        fileName = string.Empty,
                    },
                }
            );
            foreach (MultiplayerPersistenceCheckpoint request in waiting)
                _pending.Enqueue(request);
        }

        /// <summary>
        /// Queues transport input without starting I/O inside the network callback.
        /// </summary>
        /// <param name="packet">The admitted peer's persistence packet.</param>
        public void Receive(MultiplayerPacket packet)
        {
            if (!_disposed && Failure == null)
                _events.Enqueue(packet ?? throw new ArgumentNullException(nameof(packet)));
        }

        /// <summary>
        /// Processes persistence control and reserves the next host operation after prior receipts settle.
        /// </summary>
        public void Pump()
        {
            if (_disposed || Failure != null)
                return;
            try
            {
                while (_events.Count > 0 && Failure == null)
                    ProcessPacket(_events.Dequeue());
                if (Failure != null)
                    return;
                if (
                    _role == MultiplayerRole.Host
                    && _current == null
                    && _pending.Count > 0
                    && _canCheckpoint()
                )
                {
                    MultiplayerPersistenceCheckpoint request = _pending.Dequeue();
                    _current = request.Command;
                    _sourcePayload = request.Payload;
                    _current.token = Guid.NewGuid().ToString("N");
                    _reserved = true;
                    Send(MultiplayerMessageType.PersistencePrepare, _current);
                }
                if (
                    _role == MultiplayerRole.Guest
                    && _current != null
                    && !_readySent
                    && _canCheckpoint()
                )
                {
                    _readySent = true;
                    Send(MultiplayerMessageType.PersistenceReady, _current);
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// Completes a load only after the replacement scene's ordinary state receipt handshake succeeds.
        /// </summary>
        public void NotifySynchronized()
        {
            if (
                _disposed
                || Failure != null
                || !IsTransferringScene
                || _role != MultiplayerRole.Host
            )
                return;
            Send(MultiplayerMessageType.PersistenceComplete, _current);
            if (Failure == null)
                CompleteCurrent();
        }

        /// <summary>
        /// Stops pending persistence after an application or transport failure outside this pump.
        /// </summary>
        /// <param name="exception">The failure that invalidates shared completion.</param>
        public void ReportFailure(Exception exception)
        {
            Fail(exception ?? throw new ArgumentNullException(nameof(exception)), false);
        }

        /// <summary>
        /// Cancels requests when their admitted connection no longer owns a completion outcome.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (TaskCompletionSource<bool> completion in _requests.Values)
                completion.TrySetCanceled();
            _requests.Clear();
            _pending.Clear();
            _events.Clear();
            _sourcePayload = null;
            IsTransferringScene = false;
            _reserved = false;
        }

        /// <summary>
        /// Applies role-checked control messages to the one reserved operation.
        /// </summary>
        /// <param name="packet">The next ordered persistence control message.</param>
        private void ProcessPacket(MultiplayerPacket packet)
        {
            MultiplayerPersistenceCommand command =
                JsonUtility.FromJson<MultiplayerPersistenceCommand>(packet.Metadata)
                ?? throw new InvalidDataException("The persistence operation metadata is missing.");
            if (!MultiplayerSessionIdentity.Matches(_connectionID, command.initializationID))
                return;
            if (
                packet.Type != MultiplayerMessageType.PersistenceRequest
                && packet.Type != MultiplayerMessageType.PersistenceFailed
                && !string.IsNullOrEmpty(command.token)
                && command.token == _lastCompletedToken
            )
                return;
            switch (packet.Type)
            {
                case MultiplayerMessageType.PersistenceRequest:
                    RequireRole(MultiplayerRole.Host);
                    SaveGameManager.ValidateSaveFileName(command.fileName);
                    if (command.load != (packet.Payload.Length > 0))
                        throw new InvalidDataException(
                            "A guest load must carry its selected saved world; save requests carry no snapshot."
                        );
                    if (command.requestID <= _lastGuestRequestID)
                        return;
                    _lastGuestRequestID = command.requestID;
                    command.guestRequest = true;
                    command.automatic = false;
                    command.token = null;
                    _pending.Enqueue(
                        new MultiplayerPersistenceCheckpoint
                        {
                            Command = command,
                            Payload = packet.Payload,
                        }
                    );
                    break;
                case MultiplayerMessageType.PersistencePrepare:
                    RequireRole(MultiplayerRole.Guest);
                    if (!Guid.TryParseExact(command.token, "N", out _))
                        throw new InvalidDataException("The checkpoint identity is invalid.");
                    if (_current != null)
                    {
                        ValidateCurrent(command);
                        return;
                    }
                    _current = command;
                    _reserved = true;
                    break;
                case MultiplayerMessageType.PersistenceReady:
                    RequireRole(MultiplayerRole.Host);
                    ValidateCurrent(command);
                    if (!_preparing && _checkpoint == null)
                        PrepareHostAsync(_current);
                    break;
                case MultiplayerMessageType.PersistenceCommit:
                    RequireRole(MultiplayerRole.Guest);
                    ValidateCurrent(command);
                    if (_guestPrepared)
                        Send(MultiplayerMessageType.PersistenceReceipt, _current);
                    else if (!_preparing)
                        PrepareGuestAsync(
                            new MultiplayerPersistenceCheckpoint
                            {
                                Command = command,
                                Payload = packet.Payload,
                            }
                        );
                    break;
                case MultiplayerMessageType.PersistenceReceipt:
                    RequireRole(MultiplayerRole.Host);
                    ValidateCurrent(command);
                    if (_checkpoint == null)
                        throw new InvalidDataException(
                            "The checkpoint receipt preceded its durable host copy."
                        );
                    if (_current.load)
                    {
                        if (!IsTransferringScene)
                        {
                            Send(MultiplayerMessageType.PersistenceTransfer, _current);
                            if (Failure == null)
                                BeginTransfer();
                        }
                    }
                    else
                    {
                        Send(MultiplayerMessageType.PersistenceComplete, _current);
                        if (Failure == null)
                            CompleteCurrent();
                    }
                    break;
                case MultiplayerMessageType.PersistenceTransfer:
                    RequireRole(MultiplayerRole.Guest);
                    ValidateCurrent(command);
                    if (!_guestPrepared || !_current.load)
                        throw new InvalidDataException(
                            "The shared load was not prepared by both participants."
                        );
                    if (!IsTransferringScene)
                        BeginTransfer();
                    break;
                case MultiplayerMessageType.PersistenceComplete:
                    RequireRole(MultiplayerRole.Guest);
                    if (_current == null)
                        return;
                    ValidateCurrent(command);
                    if (!_guestPrepared)
                        throw new InvalidDataException(
                            "The checkpoint completed before its peer copy."
                        );
                    CompleteCurrent();
                    break;
                case MultiplayerMessageType.PersistenceFailed:
                    Fail(new IOException(command.error), false);
                    break;
                default:
                    throw new InvalidDataException("The persistence message type is unsupported.");
            }
        }

        /// <summary>
        /// Awaits host storage or content preparation while both gameplay pumps remain reserved.
        /// </summary>
        /// <param name="command">The immutable reserved operation.</param>
        private async void PrepareHostAsync(MultiplayerPersistenceCommand command)
        {
            _preparing = true;
            try
            {
                MultiplayerPersistenceCheckpoint checkpoint = await _prepareHost(
                    command,
                    _sourcePayload
                );
                if (_disposed || Failure != null || !ReferenceEquals(_current, command))
                    return;
                _checkpoint =
                    checkpoint ?? throw new InvalidDataException("The host checkpoint is missing.");
                Send(MultiplayerMessageType.PersistenceCommit, command, checkpoint.Payload);
            }
            catch (Exception exception)
            {
                if (!_disposed)
                    Fail(
                        new IOException(
                            "The host could not prepare the shared checkpoint: "
                                + exception.Message,
                            exception
                        )
                    );
            }
            finally
            {
                _preparing = false;
            }
        }

        /// <summary>
        /// Awaits the peer's durable save copy or exact-content verification before acknowledging.
        /// </summary>
        /// <param name="checkpoint">The host's explicit checkpoint representation.</param>
        private async void PrepareGuestAsync(MultiplayerPersistenceCheckpoint checkpoint)
        {
            _preparing = true;
            try
            {
                await _prepareGuest(checkpoint);
                if (_disposed || Failure != null || _current?.token != checkpoint.Command.token)
                    return;
                _checkpoint = checkpoint;
                _guestPrepared = true;
                Send(MultiplayerMessageType.PersistenceReceipt, _current);
            }
            catch (Exception exception)
            {
                if (!_disposed)
                    Fail(
                        new IOException(
                            "The peer copy or load preparation failed; the host copy may already exist: "
                                + exception.Message,
                            exception
                        )
                    );
            }
            finally
            {
                _preparing = false;
            }
        }

        /// <summary>
        /// Starts the preflighted scene transition and retains the operation until a new core synchronizes.
        /// </summary>
        private void BeginTransfer()
        {
            IsTransferringScene = true;
            _beginTransfer(_checkpoint);
        }

        /// <summary>
        /// Releases a completed checkpoint and resolves only the request owned by this process.
        /// </summary>
        private void CompleteCurrent()
        {
            _lastCompletedToken = _current.token;
            bool localRequest = _current.guestRequest == (_role == MultiplayerRole.Guest);
            if (
                !_current.automatic
                && localRequest
                && _requests.Remove(_current.requestID, out TaskCompletionSource<bool> completion)
            )
                completion.TrySetResult(true);
            if (_current.automatic)
                _autosaveQueued = false;
            _current = null;
            _checkpoint = null;
            _sourcePayload = null;
            _reserved = false;
            _preparing = false;
            _guestPrepared = false;
            _readySent = false;
            IsTransferringScene = false;
            CheckpointCompleted?.Invoke();
        }

        /// <summary>
        /// Rejects an acknowledgment or commit belonging to another operation.
        /// </summary>
        /// <param name="command">The received operation identity.</param>
        private void ValidateCurrent(MultiplayerPersistenceCommand command)
        {
            if (
                _current == null
                || command.token != _current.token
                || command.requestID != _current.requestID
                || command.guestRequest != _current.guestRequest
                || command.load != _current.load
                || command.automatic != _current.automatic
                || command.fileName != _current.fileName
            )
                throw new InvalidDataException(
                    "The persistence operation does not match the reserved checkpoint."
                );
        }

        /// <summary>
        /// Sends one ordered control message and reports immediate transport failures.
        /// </summary>
        /// <param name="type">The explicit persistence message type.</param>
        /// <param name="command">The operation metadata.</param>
        /// <param name="payload">The optional shared metadata or authoritative snapshot.</param>
        private void Send(
            MultiplayerMessageType type,
            MultiplayerPersistenceCommand command,
            byte[] payload = null
        )
        {
            try
            {
                command.initializationID = _connectionID;
                _send(new MultiplayerPacket(type, JsonUtility.ToJson(command), payload));
            }
            catch (Exception exception)
            {
                Fail(exception, false);
            }
        }

        /// <summary>
        /// Fails pending completions without pretending that a partial durable write was rolled back.
        /// </summary>
        /// <param name="exception">The storage, content, or transport failure.</param>
        /// <param name="notifyPeer">Whether the other participant must also stop.</param>
        private void Fail(Exception exception, bool notifyPeer = true)
        {
            if (_disposed || Failure != null)
                return;
            Failure = exception;
            _reserved = true;
            foreach (TaskCompletionSource<bool> completion in _requests.Values)
                completion.TrySetException(exception);
            _requests.Clear();
            if (notifyPeer)
            {
                try
                {
                    _send(
                        new MultiplayerPacket(
                            MultiplayerMessageType.PersistenceFailed,
                            JsonUtility.ToJson(
                                new MultiplayerPersistenceCommand
                                {
                                    error = exception.Message,
                                    initializationID = _connectionID,
                                }
                            )
                        )
                    );
                }
                catch (Exception) { }
            }
            Faulted?.Invoke(exception);
        }

        /// <summary>
        /// Enforces the authenticated direction of a protocol message.
        /// </summary>
        /// <param name="role">The required receiving role.</param>
        private void RequireRole(MultiplayerRole role)
        {
            if (_role != role)
                throw new InvalidDataException(
                    "The persistence operation was sent to the wrong role."
                );
        }

        /// <summary>
        /// Rejects requests after their connection has ended or checkpoint safety has failed.
        /// </summary>
        private void ThrowIfUnavailable()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MultiplayerPersistenceSession));
            if (Failure != null)
                throw new InvalidOperationException("Shared persistence has stopped.", Failure);
        }
    }

    /// <summary>
    /// Carries bounded operation metadata independently of the saved model schema.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerPersistenceCommand
    {
        public string initializationID;
        public long requestID;
        public bool guestRequest;
        public bool automatic;
        public bool load;
        public string token;
        public string fileName;
        public string displayName;
        public string snapshotHash;
        public string simulationBuild;
        public string gameplayContent;
        public string error;
    }

    /// <summary>
    /// Holds a prepared explicit-schema metadata payload or an authoritative saved-state snapshot.
    /// </summary>
    internal sealed class MultiplayerPersistenceCheckpoint
    {
        public MultiplayerPersistenceCommand Command { get; set; }
        public byte[] Payload { get; set; }
    }
}
