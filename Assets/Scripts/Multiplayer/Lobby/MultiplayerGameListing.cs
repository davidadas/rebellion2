using System;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// One player-hosted game returned by online discovery.
    /// </summary>
    [Serializable]
    public sealed class MultiplayerGameListing
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
}
