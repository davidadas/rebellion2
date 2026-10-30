using System;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Retains the newest complete authoritative state until the next presentation update.
    /// Intermediate full snapshots are superseded; commands and chat retain their ordered delivery.
    /// This buffer is owned exclusively by the session's main thread.
    /// </summary>
    internal sealed class MultiplayerSnapshotBuffer
    {
        private MultiplayerPacket _packet;
        private long _revision;

        /// <summary>
        /// Retains the newest revision, including a later correction at the same revision.
        /// </summary>
        /// <param name="packet">The complete authoritative state or correction.</param>
        /// <param name="revision">The host's state revision.</param>
        public void Enqueue(MultiplayerPacket packet, long revision)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));
            if (_packet != null && revision < _revision)
                return;
            _packet = packet;
            _revision = revision;
        }

        /// <summary>
        /// Consumes the newest complete state once without replaying superseded intermediate graphs.
        /// </summary>
        /// <returns>The newest state, or null when no state is waiting.</returns>
        public MultiplayerPacket Take()
        {
            MultiplayerPacket packet = _packet;
            _packet = null;
            return packet;
        }

        /// <summary>
        /// Discards state belonging to a disconnected session.
        /// </summary>
        public void Clear()
        {
            _packet = null;
        }
    }
}
