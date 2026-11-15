namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Identifies an event emitted by a peer transport.
    /// </summary>
    public enum MultiplayerTransportEventType
    {
        Connected,
        PacketReceived,
        Disconnected,
    }

    /// <summary>
    /// Preserves the observed cause of a connection ending independently of its display message.
    /// </summary>
    public enum MultiplayerDisconnectReason
    {
        Unknown,
        LocalSessionClosed,
        PeerEndedSession,
        PeerClosedConnection,
        LobbyClosed,
        RemovedFromLobby,
        ConnectionLost,
        TransportError,
        ProtocolError,
    }

    /// <summary>
    /// Describes one transport event for main-thread processing.
    /// </summary>
    public sealed class MultiplayerTransportEvent
    {
        /// <summary>
        /// Gets the event type.
        /// </summary>
        public MultiplayerTransportEventType Type { get; }

        /// <summary>
        /// Gets the received packet, when applicable.
        /// </summary>
        public MultiplayerPacket Packet { get; }

        /// <summary>
        /// Gets the disconnect reason, when applicable.
        /// </summary>
        public string Error { get; }

        /// <summary>
        /// Gets the observed disconnect cause without interpreting exception or display text.
        /// </summary>
        public MultiplayerDisconnectReason DisconnectReason { get; }

        /// <summary>
        /// Creates a transport event.
        /// </summary>
        /// <param name="type">The event type.</param>
        /// <param name="packet">The received packet.</param>
        /// <param name="error">The disconnect reason.</param>
        /// <param name="disconnectReason">The observed cause, or unknown when not supplied.</param>
        public MultiplayerTransportEvent(
            MultiplayerTransportEventType type,
            MultiplayerPacket packet = null,
            string error = null,
            MultiplayerDisconnectReason disconnectReason = MultiplayerDisconnectReason.Unknown
        )
        {
            Type = type;
            Packet = packet;
            Error = error;
            DisconnectReason = disconnectReason;
        }
    }
}
