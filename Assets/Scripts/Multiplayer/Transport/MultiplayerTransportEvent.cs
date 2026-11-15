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
        /// Creates a transport event.
        /// </summary>
        /// <param name="type">The event type.</param>
        /// <param name="packet">The received packet.</param>
        /// <param name="error">The disconnect reason.</param>
        public MultiplayerTransportEvent(
            MultiplayerTransportEventType type,
            MultiplayerPacket packet = null,
            string error = null
        )
        {
            Type = type;
            Packet = packet;
            Error = error;
        }
    }
}
