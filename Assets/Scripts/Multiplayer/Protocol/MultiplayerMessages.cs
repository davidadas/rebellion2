using System;

namespace Rebellion.Multiplayer
{
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
}
