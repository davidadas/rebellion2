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
        /// Gets the host's already verified saved state, or null for a new game or guest launch.
        /// </summary>
        public GameRoot RestoredGame { get; }

        /// <summary>
        /// Creates a synchronized launch configuration.
        /// </summary>
        /// <param name="summary">The deterministic game settings.</param>
        /// <param name="localFactionID">The faction controlled locally.</param>
        /// <param name="hostFactionID">The host-controlled faction.</param>
        /// <param name="guestFactionID">The guest-controlled faction.</param>
        /// <param name="restoredGame">The host's verified save, when resuming a game.</param>
        public MultiplayerLaunchConfiguration(
            GameSummary summary,
            string localFactionID,
            string hostFactionID,
            string guestFactionID,
            GameRoot restoredGame = null
        )
        {
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            LocalFactionID = localFactionID;
            HostFactionID = hostFactionID;
            GuestFactionID = guestFactionID;
            RestoredGame = restoredGame;
        }
    }

    /// <summary>
    /// Metadata sent by a joining peer.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerHelloMessage
    {
        public int protocolVersion;
        public string simulationBuild;
        public string playerName;
        public string contentFingerprint;
        public string passwordProof;
        public string recoveryCampaignID;
    }

    /// <summary>
    /// Metadata accepting or rejecting a joining peer.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerWelcomeMessage
    {
        public bool accepted;
        public string simulationBuild;
        public string gameplayContent;
        public string error;
        public string hostPlayerName;
        public string hostFactionID;
        public string guestFactionID;
        public MultiplayerChatMessage[] chat;
        public string initializationID;
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
        public string simulationFingerprint;
        public string initializationID;
    }

    /// <summary>
    /// Identifies the specific initialization owning readiness and gameplay messages.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerSessionIdentity
    {
        public string initializationID;
        public string reason;

        /// <summary>
        /// Rejects traffic belonging to an outgoing initialization without admitting its payload.
        /// </summary>
        /// <param name="current">The initialization owned by the receiving session.</param>
        /// <param name="received">The initialization attached to the incoming message.</param>
        /// <returns>True for a message belonging to the current initialized game.</returns>
        public static bool Matches(string current, string received) =>
            !string.IsNullOrEmpty(current)
            && string.Equals(current, received, StringComparison.Ordinal);
    }

    /// <summary>
    /// One discoverable player-hosted game returned by the directory service.
    /// </summary>
    [Serializable]
    public sealed class MatchmakerGame
    {
        public string id;
        public string name;
        public string transport;
        public string joinCode;
        public string address;
        public int port;
        public int currentPlayers;
        public int maxPlayers;
        public string state;
        public string hostFaction;
        public string contentFingerprint;
        public long expiresAt;
        public string packID;
        public string packName;
        public string packVersion;
        public string scenarioID;
        public string[] modIDs;
        public string[] modVersions;
        public bool passwordProtected;
        public string campaignID;
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
        public string transport;
        public string joinCode;
        public string address;
        public int port;
        public int currentPlayers;
        public int maxPlayers;
        public string state;
        public string hostFaction;
        public string contentFingerprint;
    }
}
