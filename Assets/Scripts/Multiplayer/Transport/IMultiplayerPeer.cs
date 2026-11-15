using System;
using System.Threading.Tasks;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Exchanges ordered game packets independently of the connection provider.
    /// </summary>
    public interface IMultiplayerPeer : IDisposable
    {
        /// <summary>
        /// Gets whether the remote participant is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Sends one complete ordered game packet.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <returns>The pending send.</returns>
        Task SendAsync(MultiplayerPacket packet);

        /// <summary>
        /// Retrieves the next connection or packet event.
        /// </summary>
        /// <param name="transportEvent">The next event when available.</param>
        /// <returns>True when an event was available.</returns>
        bool TryDequeue(out MultiplayerTransportEvent transportEvent);
    }
}
