using ConnectionClosedReason = Epic.OnlineServices.P2P.ConnectionClosedReason;
using LobbyMemberStatus = Epic.OnlineServices.Lobby.LobbyMemberStatus;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Translates provider lifecycle causes without treating every closed socket as a lost connection.
    /// </summary>
    internal static class EpicMultiplayerDisconnect
    {
        /// <summary>
        /// Classifies the membership status that ended an admitted connection.
        /// </summary>
        /// <param name="status">The provider's lobby membership status.</param>
        /// <returns>The observed departure cause.</returns>
        internal static MultiplayerDisconnectReason FromLobbyStatus(LobbyMemberStatus status)
        {
            return status switch
            {
                LobbyMemberStatus.Left => MultiplayerDisconnectReason.PeerEndedSession,
                LobbyMemberStatus.Closed => MultiplayerDisconnectReason.LobbyClosed,
                LobbyMemberStatus.Kicked => MultiplayerDisconnectReason.RemovedFromLobby,
                LobbyMemberStatus.Disconnected => MultiplayerDisconnectReason.ConnectionLost,
                _ => MultiplayerDisconnectReason.Unknown,
            };
        }

        /// <summary>
        /// Separates explicit socket closures, connectivity loss, and provider or protocol errors.
        /// </summary>
        /// <param name="reason">The provider's P2P closure cause.</param>
        /// <returns>The observed cause; a peer closing its socket does not establish why it did so.</returns>
        internal static MultiplayerDisconnectReason FromConnectionReason(
            ConnectionClosedReason reason
        )
        {
            return reason switch
            {
                ConnectionClosedReason.ClosedByLocalUser =>
                    MultiplayerDisconnectReason.LocalSessionClosed,
                ConnectionClosedReason.ClosedByPeer =>
                    MultiplayerDisconnectReason.PeerClosedConnection,
                ConnectionClosedReason.TimedOut
                or ConnectionClosedReason.ConnectionFailed
                or ConnectionClosedReason.ConnectionClosed
                or ConnectionClosedReason.NegotiationFailed =>
                    MultiplayerDisconnectReason.ConnectionLost,
                ConnectionClosedReason.InvalidMessage or ConnectionClosedReason.InvalidData =>
                    MultiplayerDisconnectReason.ProtocolError,
                ConnectionClosedReason.TooManyConnections
                or ConnectionClosedReason.UnexpectedError
                or ConnectionClosedReason.ConnectionIgnored =>
                    MultiplayerDisconnectReason.TransportError,
                _ => MultiplayerDisconnectReason.Unknown,
            };
        }
    }
}
