using System;

namespace Rebellion.Util.Serialization
{
    /// <summary>
    /// Save file schema version and compatibility gate.
    /// Bump <see cref="CurrentVersion"/> whenever a persisted type changes shape.
    /// </summary>
    public static class SaveSchema
    {
        /// <summary>
        /// The schema version stamped onto newly written saves.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Throws if the supplied save version cannot be loaded by this client.
        /// A save written by a newer client than the one loading it is rejected —
        /// loading it would silently drop fields the newer client added.
        /// </summary>
        /// <param name="saveVersion">The version stamped on the save being loaded.</param>
        public static void GuardCanLoad(int saveVersion)
        {
            if (saveVersion > CurrentVersion)
                throw new SaveVersionTooNewException(saveVersion, CurrentVersion);
        }
    }

    /// <summary>
    /// Thrown when a save file was written by a newer client than the one attempting to load it.
    /// </summary>
    public sealed class SaveVersionTooNewException : Exception
    {
        /// <summary>
        /// The version recorded in the save file.
        /// </summary>
        public int SaveVersion { get; }

        /// <summary>
        /// The highest version this client can load.
        /// </summary>
        public int ClientVersion { get; }

        /// <summary>
        /// Creates a new <see cref="SaveVersionTooNewException"/>.
        /// </summary>
        /// <param name="saveVersion">The version recorded in the save file.</param>
        /// <param name="clientVersion">The highest version this client can load.</param>
        public SaveVersionTooNewException(int saveVersion, int clientVersion)
            : base(
                $"Save was written by a newer client (save version {saveVersion}, "
                    + $"this client supports up to {clientVersion})."
            )
        {
            SaveVersion = saveVersion;
            ClientVersion = clientVersion;
        }
    }
}
