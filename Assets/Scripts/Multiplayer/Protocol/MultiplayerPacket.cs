using System;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Identifies one wire-level multiplayer message.
    /// </summary>
    public enum MultiplayerMessageType : byte
    {
        Hello = 1,
        Welcome = 2,
        Ready = 3,
        SnapshotCommit = 5,
        Disconnect = 7,
        LobbyState = 8,
        SelectFaction = 9,
        LobbyReady = 10,
        LobbyContentReady = 11,
        LobbyChat = 12,
        StartGame = 13,
        AdmissionRejectedAck = 14,
        PlayerActionRequest = 15,
        PlayerActionCommit = 16,
        PlayerActionReceipt = 17,
        AdvanceDay = 19,
        DayReceipt = 20,
        SynchronizationFailed = 21,
        SnapshotReceipt = 22,
        PersistenceRequest = 23,
        PersistencePrepare = 24,
        PersistenceReady = 25,
        PersistenceCommit = 26,
        PersistenceReceipt = 27,
        PersistenceComplete = 28,
        PersistenceTransfer = 29,
        PersistenceFailed = 30,
        RecoveryWelcome = 31,
    }

    /// <summary>
    /// Carries a typed metadata document and optional binary payload over a peer connection.
    /// </summary>
    public sealed class MultiplayerPacket
    {
        /// <summary>
        /// Gets the message type.
        /// </summary>
        public MultiplayerMessageType Type { get; }

        /// <summary>
        /// Gets the UTF-8 JSON metadata.
        /// </summary>
        public string Metadata { get; }

        /// <summary>
        /// Gets the optional binary payload.
        /// </summary>
        public byte[] Payload { get; }

        /// <summary>
        /// Creates a wire packet.
        /// </summary>
        /// <param name="type">The message type.</param>
        /// <param name="metadata">The UTF-8 JSON metadata.</param>
        /// <param name="payload">The optional binary payload.</param>
        public MultiplayerPacket(
            MultiplayerMessageType type,
            string metadata = null,
            byte[] payload = null
        )
        {
            Type = type;
            Metadata = metadata ?? string.Empty;
            Payload = payload ?? Array.Empty<byte>();
        }
    }
}
