using NUnit.Framework;
using Rebellion.Multiplayer;
using ConnectionClosedReason = Epic.OnlineServices.P2P.ConnectionClosedReason;
using LobbyMemberStatus = Epic.OnlineServices.Lobby.LobbyMemberStatus;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class EpicMultiplayerDisconnectTests
    {
        [TestCase(LobbyMemberStatus.Left, MultiplayerDisconnectReason.PeerEndedSession)]
        [TestCase(LobbyMemberStatus.Closed, MultiplayerDisconnectReason.LobbyClosed)]
        [TestCase(LobbyMemberStatus.Kicked, MultiplayerDisconnectReason.RemovedFromLobby)]
        [TestCase(LobbyMemberStatus.Disconnected, MultiplayerDisconnectReason.ConnectionLost)]
        [TestCase((LobbyMemberStatus)999, MultiplayerDisconnectReason.Unknown)]
        public void FromLobbyStatus_Departure_PreservesObservedCause(
            LobbyMemberStatus status,
            MultiplayerDisconnectReason expected
        )
        {
            Assert.That(EpicMultiplayerDisconnect.FromLobbyStatus(status), Is.EqualTo(expected));
        }

        [TestCase(ConnectionClosedReason.Unknown, MultiplayerDisconnectReason.Unknown)]
        [TestCase(
            ConnectionClosedReason.ClosedByLocalUser,
            MultiplayerDisconnectReason.LocalSessionClosed
        )]
        [TestCase(
            ConnectionClosedReason.ClosedByPeer,
            MultiplayerDisconnectReason.PeerClosedConnection
        )]
        [TestCase(ConnectionClosedReason.TimedOut, MultiplayerDisconnectReason.ConnectionLost)]
        [TestCase(
            ConnectionClosedReason.ConnectionFailed,
            MultiplayerDisconnectReason.ConnectionLost
        )]
        [TestCase(
            ConnectionClosedReason.ConnectionClosed,
            MultiplayerDisconnectReason.ConnectionLost
        )]
        [TestCase(
            ConnectionClosedReason.NegotiationFailed,
            MultiplayerDisconnectReason.ConnectionLost
        )]
        [TestCase(ConnectionClosedReason.InvalidMessage, MultiplayerDisconnectReason.ProtocolError)]
        [TestCase(ConnectionClosedReason.InvalidData, MultiplayerDisconnectReason.ProtocolError)]
        [TestCase(
            ConnectionClosedReason.TooManyConnections,
            MultiplayerDisconnectReason.TransportError
        )]
        [TestCase(
            ConnectionClosedReason.UnexpectedError,
            MultiplayerDisconnectReason.TransportError
        )]
        [TestCase(
            ConnectionClosedReason.ConnectionIgnored,
            MultiplayerDisconnectReason.TransportError
        )]
        [TestCase((ConnectionClosedReason)999, MultiplayerDisconnectReason.Unknown)]
        public void FromConnectionReason_Closure_PreservesObservedCause(
            ConnectionClosedReason reason,
            MultiplayerDisconnectReason expected
        )
        {
            Assert.That(
                EpicMultiplayerDisconnect.FromConnectionReason(reason),
                Is.EqualTo(expected)
            );
        }
    }
}
