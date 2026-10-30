using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.NativeAudit
{
    /// <summary>
    /// Provides a reliable ordered TCP connection for verification tools.
    /// </summary>
    public sealed class MultiplayerPeer : IMultiplayerPeer
    {
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly ConcurrentQueue<MultiplayerTransportEvent> _events = new();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private TcpListener _listener;
        private TcpClient _client;
        private Task _connectionTask;
        private Task _receiveTask;
        private bool _disposed;

        /// <summary>
        /// Gets whether a peer socket is currently connected.
        /// </summary>
        public bool IsConnected => _client?.Connected == true;

        /// <summary>
        /// Gets the actual listening port, including when zero requested an ephemeral port.
        /// </summary>
        public int ListeningPort => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

        /// <summary>
        /// Begins listening for one direct peer connection.
        /// </summary>
        /// <param name="port">The local TCP port, or zero for an ephemeral port.</param>
        public void StartHost(int port)
        {
            ThrowIfDisposed();
            if (_listener != null || _client != null)
                throw new InvalidOperationException("The peer transport is already active.");
            if (port < 0 || port > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(port));

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start(1);
            _connectionTask = AcceptAsync();
        }

        /// <summary>
        /// Connects to a host over TCP.
        /// </summary>
        /// <param name="host">The host name or address.</param>
        /// <param name="port">The remote TCP port.</param>
        /// <returns>A task that completes when the socket is connected.</returns>
        public async Task ConnectAsync(string host, int port)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("A host is required.", nameof(host));
            if (port <= 0 || port > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(port));
            if (_listener != null || _client != null)
                throw new InvalidOperationException("The peer transport is already active.");

            TcpClient client = new TcpClient { NoDelay = true };
            try
            {
                await client.ConnectAsync(host.Trim(), port);
                AttachClient(client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Sends one packet to the connected peer.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <returns>A task that completes after the packet is flushed.</returns>
        public async Task SendAsync(MultiplayerPacket packet)
        {
            ThrowIfDisposed();
            TcpClient client = _client;
            if (client?.Connected != true)
                throw new InvalidOperationException("No multiplayer peer is connected.");

            await _sendLock.WaitAsync(_cancellation.Token);
            try
            {
                await MultiplayerPacketCodec.WriteAsync(
                    client.GetStream(),
                    packet,
                    _cancellation.Token
                );
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Attempts to dequeue one connection, packet, or disconnection event.
        /// </summary>
        /// <param name="transportEvent">Receives the next event.</param>
        /// <returns>True when an event was available.</returns>
        public bool TryDequeue(out MultiplayerTransportEvent transportEvent)
        {
            return _events.TryDequeue(out transportEvent);
        }

        /// <summary>
        /// Stops the listener and closes the peer socket.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _cancellation.Cancel();
            _listener?.Stop();
            _client?.Close();
            _client?.Dispose();
        }

        /// <summary>
        /// Accepts the first incoming client and then closes the listener.
        /// </summary>
        /// <returns>A task representing the pending accept.</returns>
        private async Task AcceptAsync()
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                if (_disposed)
                {
                    client.Dispose();
                    return;
                }
                _listener.Stop();
                _listener = null;
                AttachClient(client);
            }
            catch (Exception exception) when (_disposed || exception is SocketException)
            {
                if (!_disposed)
                    QueueDisconnect(exception.Message);
            }
        }

        /// <summary>
        /// Configures an established socket and starts its receive loop.
        /// </summary>
        /// <param name="client">The connected socket.</param>
        private void AttachClient(TcpClient client)
        {
            client.NoDelay = true;
            _client = client;
            _events.Enqueue(new MultiplayerTransportEvent(MultiplayerTransportEventType.Connected));
            _receiveTask = ReceiveAsync(client);
        }

        /// <summary>
        /// Receives packets until the connection closes or decoding fails.
        /// </summary>
        /// <param name="client">The connected socket.</param>
        /// <returns>A task representing the receive loop.</returns>
        private async Task ReceiveAsync(TcpClient client)
        {
            string error = null;
            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    MultiplayerPacket packet = await MultiplayerPacketCodec.ReadAsync(
                        client.GetStream(),
                        _cancellation.Token
                    );
                    if (packet == null)
                        break;
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
                error = exception.Message;
            }
            finally
            {
                if (!_disposed)
                    QueueDisconnect(error);
            }
        }

        /// <summary>
        /// Enqueues a single peer-disconnection event.
        /// </summary>
        /// <param name="error">The optional failure reason.</param>
        private void QueueDisconnect(string error)
        {
            _events.Enqueue(
                new MultiplayerTransportEvent(
                    MultiplayerTransportEventType.Disconnected,
                    error: error
                )
            );
        }

        /// <summary>
        /// Rejects operations after disposal.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MultiplayerPeer));
        }
    }
}
