using System;
using Rebellion.Util.Attributes;

namespace Rebellion.Game
{
    /// <summary>
    /// Metadata about the game, such as save name, player faction, last saved time, and version.
    /// </summary>
    [PersistableObject]
    public sealed class GameMetadata
    {
        public string SaveDisplayName;

        public string PlayerFactionID;

        public DateTime LastSavedUtc;

        /// <summary>
        /// Schema version of the save file. Stamped on save, checked on load.
        /// See <see cref="Rebellion.Util.Serialization.SaveSchema"/>.
        /// </summary>
        public int SaveVersion;
    }
}
