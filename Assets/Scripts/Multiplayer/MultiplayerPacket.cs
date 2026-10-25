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
        SnapshotProposal = 4,
        SnapshotCommit = 5,
        SnapshotRejected = 6,
        Disconnect = 7,
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
