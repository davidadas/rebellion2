using System;
using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Identifies the local participant's role in a direct multiplayer session.
    /// </summary>
    public enum MultiplayerRole
    {
        None,
        Host,
        Guest,
    }

    /// <summary>
    /// Carries the synchronized launch configuration into the strategy scene.
    /// </summary>
    public sealed class MultiplayerLaunchConfiguration
    {
        /// <summary>
        /// Gets the deterministic game settings supplied by the host.
        /// </summary>
        public GameSummary Summary { get; }

        /// <summary>
        /// Gets the faction controlled on this machine.
        /// </summary>
        public string LocalFactionID { get; }

        /// <summary>
        /// Gets the host-controlled faction.
        /// </summary>
        public string HostFactionID { get; }

        /// <summary>
        /// Gets the guest-controlled faction.
        /// </summary>
        public string GuestFactionID { get; }

        /// <summary>
        /// Creates a synchronized launch configuration.
        /// </summary>
        /// <param name="summary">The deterministic game settings.</param>
        /// <param name="localFactionID">The faction controlled locally.</param>
        /// <param name="hostFactionID">The host-controlled faction.</param>
        /// <param name="guestFactionID">The guest-controlled faction.</param>
        public MultiplayerLaunchConfiguration(
            GameSummary summary,
            string localFactionID,
            string hostFactionID,
            string guestFactionID
        )
        {
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            LocalFactionID = localFactionID;
            HostFactionID = hostFactionID;
            GuestFactionID = guestFactionID;
        }
    }

    /// <summary>
    /// Metadata sent by a joining peer.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerHelloMessage
    {
        public int protocolVersion;
        public string playerName;
        public string contentFingerprint;
    }

    /// <summary>
    /// Metadata accepting or rejecting a joining peer.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerWelcomeMessage
    {
        public bool accepted;
        public string error;
        public string hostPlayerName;
        public string hostFactionID;
        public string guestFactionID;
    }

    /// <summary>
    /// Metadata attached to a revisioned game-state snapshot.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerSnapshotMessage
    {
        public long revision;
        public long baseRevision;
        public string hash;
    }

    /// <summary>
    /// One discoverable player-hosted game returned by the directory service.
    /// </summary>
    [Serializable]
    public sealed class MatchmakerGame
    {
        public string id;
        public string name;
        public string address;
        public int port;
        public string hostFaction;
        public string contentFingerprint;
        public long expiresAt;
    }

    /// <summary>
    /// JSON wrapper used by Unity's array serializer.
    /// </summary>
    [Serializable]
    internal sealed class MatchmakerGameList
    {
        public List<MatchmakerGame> games = new List<MatchmakerGame>();
    }

    /// <summary>
    /// Registration request sent to the directory service.
    /// </summary>
    [Serializable]
    internal sealed class MatchmakerRegistration
    {
        public string id;
        public string name;
        public string address;
        public int port;
        public string hostFaction;
        public string contentFingerprint;
    }
}
