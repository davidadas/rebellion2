using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcceptConnectionOptions = Epic.OnlineServices.P2P.AcceptConnectionOptions;
using AddNotifyPeerConnectionClosedOptions = Epic.OnlineServices.P2P.AddNotifyPeerConnectionClosedOptions;
using AddNotifyPeerConnectionEstablishedOptions = Epic.OnlineServices.P2P.AddNotifyPeerConnectionEstablishedOptions;
using AddNotifyPeerConnectionInterruptedOptions = Epic.OnlineServices.P2P.AddNotifyPeerConnectionInterruptedOptions;
using AddNotifyPeerConnectionRequestOptions = Epic.OnlineServices.P2P.AddNotifyPeerConnectionRequestOptions;
using CloseConnectionOptions = Epic.OnlineServices.P2P.CloseConnectionOptions;
using Common = Epic.OnlineServices.Common;
using OnIncomingConnectionRequestInfo = Epic.OnlineServices.P2P.OnIncomingConnectionRequestInfo;
using OnPeerConnectionEstablishedInfo = Epic.OnlineServices.P2P.OnPeerConnectionEstablishedInfo;
using OnPeerConnectionInterruptedInfo = Epic.OnlineServices.P2P.OnPeerConnectionInterruptedInfo;
using OnRemoteConnectionClosedInfo = Epic.OnlineServices.P2P.OnRemoteConnectionClosedInfo;
using P2PInterface = Epic.OnlineServices.P2P.P2PInterface;
using PacketReliability = Epic.OnlineServices.P2P.PacketReliability;
using ProductUserId = Epic.OnlineServices.ProductUserId;
using ReceivePacketOptions = Epic.OnlineServices.P2P.ReceivePacketOptions;
using RelayControl = Epic.OnlineServices.P2P.RelayControl;
using Result = Epic.OnlineServices.Result;
using SendPacketOptions = Epic.OnlineServices.P2P.SendPacketOptions;
using SetRelayControlOptions = Epic.OnlineServices.P2P.SetRelayControlOptions;
using SocketId = Epic.OnlineServices.P2P.SocketId;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Transfers the game protocol over one admitted, reliable ordered EOS connection.
    /// </summary>
    public sealed class EpicMultiplayerPeer : IMultiplayerPeer
    {
        private readonly P2PInterface _p2p;
        private readonly ProductUserId _localUser;
        private readonly SocketId _socket;
        private readonly Queue<MultiplayerTransportEvent> _events = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly byte[] _receiveBuffer = new byte[P2PInterface.MAX_PACKET_SIZE];
        private readonly MultiplayerPacketStream _stream;
        private readonly MultiplayerPacketFramer _framer;
        private readonly ulong _requestNotification;
        private readonly ulong _connectedNotification;
        private readonly ulong _interruptedNotification;
        private readonly ulong _closedNotification;
        private ReceivePacketOptions _receiveOptions;
        private ProductUserId _receivedUser;
        private SocketId _receivedSocket;
        private ProductUserId _remoteUser;
        private bool _connected;
        private bool _disconnected;
        private bool _disposed;

        /// <summary>
        /// Gets whether the admitted counterpart is connected.
        /// </summary>
        public bool IsConnected => _connected && !_disconnected;

        /// <summary>
        /// Registers a socket that accepts only the admitted counterpart.
        /// </summary>
        /// <param name="p2p">The authenticated EOS P2P interface.</param>
        /// <param name="localUser">The local product user.</param>
        /// <param name="lobbyId">The shared lobby or campaign connection key.</param>
        public EpicMultiplayerPeer(P2PInterface p2p, ProductUserId localUser, string lobbyId)
        {
            _p2p = p2p ?? throw new ArgumentNullException(nameof(p2p));
            _localUser = localUser ?? throw new ArgumentNullException(nameof(localUser));
            _socket = new SocketId { SocketName = CreateSocketName(lobbyId) };
            _framer = new MultiplayerPacketFramer(P2PInterface.MAX_PACKET_SIZE, TrySendFrame);
            _stream = new MultiplayerPacketStream(
                _framer.WriteAsync,
                MultiplayerPacketCodec.MaximumPacketLength
            );
            _receiveOptions = new ReceivePacketOptions
            {
                LocalUserId = localUser,
                MaxDataSizeBytes = P2PInterface.MAX_PACKET_SIZE,
                RequestedChannel = 0,
            };
            SetRelayControlOptions relayOptions = new SetRelayControlOptions
            {
                RelayControl = RelayControl.AllowRelays,
            };
            RequireSuccess(_p2p.SetRelayControl(ref relayOptions), "Enable relay fallback");
            AddNotifyPeerConnectionRequestOptions requestOptions =
                new AddNotifyPeerConnectionRequestOptions
                {
                    LocalUserId = localUser,
                    SocketId = _socket,
                };
            _requestNotification = _p2p.AddNotifyPeerConnectionRequest(
                ref requestOptions,
                null,
                OnConnectionRequested
            );
            AddNotifyPeerConnectionEstablishedOptions connectedOptions =
                new AddNotifyPeerConnectionEstablishedOptions
                {
                    LocalUserId = localUser,
                    SocketId = _socket,
                };
            _connectedNotification = _p2p.AddNotifyPeerConnectionEstablished(
                ref connectedOptions,
                null,
                OnConnectionEstablished
            );
            AddNotifyPeerConnectionInterruptedOptions interruptedOptions =
                new AddNotifyPeerConnectionInterruptedOptions
                {
                    LocalUserId = localUser,
                    SocketId = _socket,
                };
            _interruptedNotification = _p2p.AddNotifyPeerConnectionInterrupted(
                ref interruptedOptions,
                null,
                OnConnectionInterrupted
            );
            AddNotifyPeerConnectionClosedOptions closedOptions =
                new AddNotifyPeerConnectionClosedOptions
                {
                    LocalUserId = localUser,
                    SocketId = _socket,
                };
            _closedNotification = _p2p.AddNotifyPeerConnectionClosed(
                ref closedOptions,
                null,
                OnConnectionClosed
            );
            if (
                _requestNotification == Common.INVALID_NOTIFICATIONID
                || _connectedNotification == Common.INVALID_NOTIFICATIONID
                || _interruptedNotification == Common.INVALID_NOTIFICATIONID
                || _closedNotification == Common.INVALID_NOTIFICATIONID
            )
            {
                Dispose();
                throw new InvalidOperationException(
                    "The multiplayer connection notifications could not be registered."
                );
            }
        }

        /// <summary>
        /// Derives a valid EOS socket name without reusing a previous lobby's stream.
        /// </summary>
        /// <param name="lobbyId">The lobby identifier.</param>
        /// <returns>A 32-character alphanumeric socket name.</returns>
        public static string CreateSocketName(string lobbyId)
        {
            if (string.IsNullOrWhiteSpace(lobbyId))
                throw new ArgumentException("A lobby identifier is required.", nameof(lobbyId));
            using SHA256 hash = SHA256.Create();
            byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(lobbyId));
            return "R2" + BitConverter.ToString(digest, 0, 15).Replace("-", string.Empty);
        }

        /// <summary>
        /// Admits and connects to an authenticated participant.
        /// </summary>
        /// <param name="remoteUser">The authenticated counterpart.</param>
        public void Connect(ProductUserId remoteUser)
        {
            bool admitted = _remoteUser != null;
            Listen(remoteUser);
            if (admitted)
                return;
            SendPacketOptions options = CreateSendOptions(
                MultiplayerPacketFramer.CreateConnectionFrame()
            );
            RequireSuccess(_p2p.SendPacket(ref options), "Connect to the other player");
        }

        /// <summary>
        /// Admits an authenticated counterpart without initiating an outgoing connection.
        /// </summary>
        /// <param name="remoteUser">The authenticated counterpart allowed to connect.</param>
        public void Listen(ProductUserId remoteUser)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EpicMultiplayerPeer));
            if (remoteUser?.Equals(_localUser) != false)
                throw new ArgumentException(
                    "A distinct remote lobby member is required.",
                    nameof(remoteUser)
                );
            if (_remoteUser != null)
            {
                if (!_remoteUser.Equals(remoteUser))
                    throw new InvalidOperationException(
                        "The session already has a remote participant."
                    );
                return;
            }
            _remoteUser = remoteUser;
        }

        /// <summary>
        /// Drains received EOS messages into the bounded protocol stream on the main thread.
        /// </summary>
        public void Poll()
        {
            if (_disposed || _disconnected)
                return;
            while (true)
            {
                Result result = _p2p.ReceivePacket(
                    ref _receiveOptions,
                    ref _receivedUser,
                    ref _receivedSocket,
                    out byte channel,
                    new ArraySegment<byte>(_receiveBuffer),
                    out uint length
                );
                if (result == Result.NotFound)
                    return;
                if (result != Result.Success)
                {
                    Disconnect("Receive failed: " + result);
                    return;
                }
                if (
                    channel != 0
                    || !_receivedUser.Equals(_remoteUser)
                    || !string.Equals(
                        _receivedSocket.SocketName,
                        _socket.SocketName,
                        StringComparison.Ordinal
                    )
                )
                    continue;
                try
                {
                    _stream.Append(
                        MultiplayerPacketFramer.ReadFrame(
                            new ArraySegment<byte>(_receiveBuffer, 0, checked((int)length))
                        )
                    );
                }
                catch (Exception exception)
                {
                    Disconnect(exception.Message);
                    return;
                }
            }
        }

        /// <summary>
        /// Sends a complete game packet in connection order.
        /// </summary>
        /// <param name="packet">The game packet.</param>
        /// <returns>The pending send.</returns>
        public async Task SendAsync(MultiplayerPacket packet)
        {
            if (!IsConnected)
                throw new InvalidOperationException("No multiplayer peer is connected.");
            await _sendGate.WaitAsync(_cancellation.Token);
            try
            {
                await MultiplayerPacketCodec.WriteAsync(_stream, packet, _cancellation.Token);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        /// <summary>
        /// Retrieves the next connection or decoded packet event.
        /// </summary>
        /// <param name="transportEvent">The next event when available.</param>
        /// <returns>True when an event was available.</returns>
        public bool TryDequeue(out MultiplayerTransportEvent transportEvent)
        {
            if (_events.Count > 0)
            {
                transportEvent = _events.Dequeue();
                return true;
            }
            transportEvent = null;
            return false;
        }

        /// <summary>
        /// Ends a connection after a transport or lobby membership failure.
        /// </summary>
        /// <param name="reason">The disconnect reason.</param>
        public void Disconnect(string reason)
        {
            if (_disconnected)
                return;
            CloseConnection();
            _events.Enqueue(
                new MultiplayerTransportEvent(
                    MultiplayerTransportEventType.Disconnected,
                    error: reason
                )
            );
        }

        /// <summary>
        /// Releases this owner's multiplayer resources and pending connection.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _p2p.RemoveNotifyPeerConnectionRequest(_requestNotification);
            _p2p.RemoveNotifyPeerConnectionEstablished(_connectedNotification);
            _p2p.RemoveNotifyPeerConnectionInterrupted(_interruptedNotification);
            _p2p.RemoveNotifyPeerConnectionClosed(_closedNotification);
            CloseConnection();
            _stream.Dispose();
        }

        /// <summary>
        /// Accepts requests only from the participant admitted by the lobby.
        /// </summary>
        /// <param name="data">The incoming connection request.</param>
        private void OnConnectionRequested(ref OnIncomingConnectionRequestInfo data)
        {
            if (_disposed || _disconnected || !data.RemoteUserId.Equals(_remoteUser))
                return;
            AcceptConnectionOptions options = new AcceptConnectionOptions
            {
                LocalUserId = _localUser,
                RemoteUserId = _remoteUser,
                SocketId = _socket,
            };
            Result result = _p2p.AcceptConnection(ref options);
            if (result != Result.Success)
                Disconnect("Accept connection failed: " + result);
        }

        /// <summary>
        /// Starts packet decoding after the first connection establishment.
        /// </summary>
        /// <param name="data">The connection establishment notification.</param>
        private void OnConnectionEstablished(ref OnPeerConnectionEstablishedInfo data)
        {
            if (_disposed || _disconnected || _connected || !data.RemoteUserId.Equals(_remoteUser))
                return;
            _connected = true;
            _events.Enqueue(new MultiplayerTransportEvent(MultiplayerTransportEventType.Connected));
            _ = ReceiveAsync();
        }

        /// <summary>
        /// Ends an interrupted stream so the campaign can negotiate a completed recovery checkpoint.
        /// </summary>
        /// <param name="data">The interrupted connection notification.</param>
        private void OnConnectionInterrupted(ref OnPeerConnectionInterruptedInfo data)
        {
            if (!_disposed && data.RemoteUserId.Equals(_remoteUser))
                Disconnect("The connection to the other player was interrupted.");
        }

        /// <summary>
        /// Propagates a terminal remote connection loss.
        /// </summary>
        /// <param name="data">The closed connection notification.</param>
        private void OnConnectionClosed(ref OnRemoteConnectionClosedInfo data)
        {
            if (!_disposed && data.RemoteUserId.Equals(_remoteUser))
                Disconnect("Connection closed: " + data.Reason);
        }

        /// <summary>
        /// Decodes complete game packets from the ordered stream.
        /// </summary>
        /// <returns>The lifetime of the receive loop.</returns>
        private async Task ReceiveAsync()
        {
            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    MultiplayerPacket packet = await MultiplayerPacketCodec.ReadAsync(
                        _stream,
                        _cancellation.Token
                    );
                    if (packet == null)
                        return;
                    _events.Enqueue(
                        new MultiplayerTransportEvent(
                            MultiplayerTransportEventType.PacketReceived,
                            packet
                        )
                    );
                }
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!_disposed)
                    Disconnect(exception.Message);
            }
        }

        /// <summary>
        /// Submits a frame or reports temporary provider backpressure.
        /// </summary>
        /// <param name="data">The outgoing frame.</param>
        /// <returns>True when EOS accepted the frame.</returns>
        private bool TrySendFrame(ArraySegment<byte> data)
        {
            SendPacketOptions options = CreateSendOptions(data);
            Result result = _p2p.SendPacket(ref options);
            if (result == Result.LimitExceeded)
                return false;
            RequireSuccess(result, "Send game data");
            return true;
        }

        /// <summary>
        /// Builds reliable ordered options for the admitted participant.
        /// </summary>
        /// <param name="data">The outgoing transport frame.</param>
        /// <returns>The EOS send options.</returns>
        private SendPacketOptions CreateSendOptions(ArraySegment<byte> data)
        {
            return new SendPacketOptions
            {
                LocalUserId = _localUser,
                RemoteUserId = _remoteUser,
                SocketId = _socket,
                Channel = 0,
                Data = data,
                Reliability = PacketReliability.ReliableOrdered,
                AllowDelayedDelivery = true,
            };
        }

        /// <summary>
        /// Cancels outstanding I/O and closes this lobby's socket.
        /// </summary>
        private void CloseConnection()
        {
            _disconnected = true;
            _connected = false;
            _cancellation.Cancel();
            _stream.Complete();
            if (_remoteUser == null)
                return;
            CloseConnectionOptions options = new CloseConnectionOptions
            {
                LocalUserId = _localUser,
                RemoteUserId = _remoteUser,
                SocketId = _socket,
            };
            _p2p.CloseConnection(ref options);
        }

        /// <summary>
        /// Rejects an unsuccessful SDK operation.
        /// </summary>
        /// <param name="result">The SDK result.</param>
        /// <param name="operation">The attempted operation.</param>
        private static void RequireSuccess(Result result, string operation)
        {
            if (result != Result.Success)
                throw new InvalidOperationException(operation + ": " + result);
        }
    }
}
