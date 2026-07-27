using System;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Metadata about the game (e.g. save game name, player id, etc).
    /// </summary>
    [PersistableObject]
    public sealed class GameMetadata
    {
        public const int CurrentSaveVersion = 2;

        public string SaveDisplayName;

        public string PlayerFactionID;

        public string PackID;

        public string PackVersion;

        public string ScenarioID;

        public DateTime LastSavedUtc;

        public int SaveVersion;
    }
}
