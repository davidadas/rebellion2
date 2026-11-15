using System;
using System.Collections.Generic;
using System.Diagnostics;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.MultiplayerIntegration
{
    /// <summary>
    /// Observes authenticated SDK route notifications for one test lobby without changing production transport.
    /// </summary>
    internal sealed class EosTacticalRouteObserver : IDisposable
    {
        private readonly P2PInterface _p2p;
        private readonly ProductUserId _localUser;
        private readonly string _socketName;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<EosTacticalRouteObservation> _observations = new();
        private readonly ulong _notification;
        private bool _disposed;

        /// <summary>
        /// Registers an independent SDK notification before the test's peer negotiation begins.
        /// </summary>
        /// <param name="p2p">The authenticated local platform's P2P interface.</param>
        /// <param name="localProductUserID">The product identity reported by the authenticated local platform.</param>
        /// <param name="lobbyID">The exact lobby selected through the production browser.</param>
        internal EosTacticalRouteObserver(
            P2PInterface p2p,
            string localProductUserID,
            string lobbyID
        )
        {
            _p2p = p2p ?? throw new ArgumentNullException(nameof(p2p));
            _localUser = ProductUserId.FromString(localProductUserID);
            if (_localUser?.IsValid() != true)
                throw new ArgumentException(
                    "An authenticated local product identity is required.",
                    nameof(localProductUserID)
                );
            _socketName = EpicMultiplayerPeer.CreateSocketName(lobbyID);
            AddNotifyPeerConnectionEstablishedOptions options = new()
            {
                LocalUserId = _localUser,
                SocketId = new SocketId { SocketName = _socketName },
            };
            _notification = _p2p.AddNotifyPeerConnectionEstablished(
                ref options,
                null,
                OnEstablished
            );
            if (_notification == Common.INVALID_NOTIFICATIONID)
                throw new InvalidOperationException(
                    "The test could not observe the admitted peer's network route."
                );
        }

        /// <summary>
        /// Applies and reads back the requested policy before a new test connection is negotiated.
        /// This reports configuration only; route evidence comes from the separate connection callback.
        /// </summary>
        /// <param name="p2p">The authenticated local platform's P2P interface.</param>
        /// <param name="relayControl">The policy selected before the guest is allowed to join.</param>
        /// <returns>The policy read back from the SDK.</returns>
        internal static RelayControl SetRelayPolicy(P2PInterface p2p, RelayControl relayControl)
        {
            if (p2p == null)
                throw new ArgumentNullException(nameof(p2p));
            if (
                relayControl
                is not RelayControl.NoRelays
                    and not RelayControl.AllowRelays
                    and not RelayControl.ForceRelays
            )
                throw new ArgumentOutOfRangeException(nameof(relayControl));
            SetRelayControlOptions setOptions = new() { RelayControl = relayControl };
            Result set = p2p.SetRelayControl(ref setOptions);
            if (set != Result.Success)
                throw new InvalidOperationException("The test relay policy was rejected: " + set);
            GetRelayControlOptions getOptions = new();
            Result get = p2p.GetRelayControl(ref getOptions, out RelayControl actual);
            if (get != Result.Success || actual != relayControl)
                throw new InvalidOperationException(
                    "The SDK did not confirm the requested test relay policy: " + get
                );
            return actual;
        }

        /// <summary>
        /// Captures every observed route transition with authenticated endpoint and socket identities.
        /// </summary>
        /// <returns>The detached route observations, containing no store credentials or access tokens.</returns>
        internal EosTacticalRouteObservation[] Capture() => _observations.ToArray();

        /// <summary>
        /// Tests actual callback evidence for the expected authenticated counterpart and network route.
        /// </summary>
        /// <param name="remoteProductUserID">The counterpart admitted by the production lobby.</param>
        /// <param name="networkType">The network route that must have been observed.</param>
        /// <returns>Whether the SDK reported that route for the exact admitted connection.</returns>
        internal bool HasObserved(string remoteProductUserID, NetworkConnectionType networkType)
        {
            foreach (EosTacticalRouteObservation observation in _observations)
                if (
                    observation.RemoteUserID == remoteProductUserID
                    && observation.NetworkType == networkType
                )
                    return true;
            return false;
        }

        /// <summary>
        /// Releases only this test's independent native notification before platform shutdown.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _p2p.RemoveNotifyPeerConnectionEstablished(_notification);
        }

        /// <summary>
        /// Retains real route changes only for the exact local identity and lobby socket being tested.
        /// </summary>
        /// <param name="info">The SDK's actual established or reconnected peer observation.</param>
        private void OnEstablished(ref OnPeerConnectionEstablishedInfo info)
        {
            if (
                _disposed
                || info.LocalUserId?.Equals(_localUser) != true
                || info.RemoteUserId?.IsValid() != true
                || !string.Equals(info.SocketId?.SocketName, _socketName, StringComparison.Ordinal)
            )
                return;
            _observations.Add(
                new EosTacticalRouteObservation
                {
                    LocalUserID = info.LocalUserId.ToString(),
                    RemoteUserID = info.RemoteUserId.ToString(),
                    SocketName = info.SocketId.Value.SocketName,
                    ConnectionType = info.ConnectionType,
                    ConnectionTypeName = info.ConnectionType.ToString(),
                    NetworkType = info.NetworkType,
                    NetworkTypeName = info.NetworkType.ToString(),
                    ElapsedMilliseconds = _clock.Elapsed.TotalMilliseconds,
                }
            );
        }
    }

    /// <summary>
    /// Reports an observed SDK route transition independently of the requested relay policy.
    /// </summary>
    [Serializable]
    internal sealed class EosTacticalRouteObservation
    {
        public string LocalUserID;
        public string RemoteUserID;
        public string SocketName;
        public ConnectionEstablishedType ConnectionType;
        public string ConnectionTypeName;
        public NetworkConnectionType NetworkType;
        public string NetworkTypeName;
        public double ElapsedMilliseconds;
    }
}
