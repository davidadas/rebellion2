using System;
using Rebellion.Game;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Resolves installed multiplayer content through the same exact-identity rules as saved games.
    /// </summary>
    public static class MultiplayerContentSelection
    {
        /// <summary>
        /// Opens the exact pack, scenario, and enabled mod versions advertised by the host.
        /// </summary>
        /// <param name="activePack">The application's current content root.</param>
        /// <param name="summary">The host's complete content identity.</param>
        /// <returns>The verified installed content.</returns>
        public static ContentPack Open(ContentPack activePack, GameSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));
            if (activePack.MatchesContentIdentity(summary))
                return activePack;
            return SaveContentResolver
                .Resolve(
                    activePack,
                    new GameMetadata
                    {
                        PackID = summary.PackID,
                        PackVersion = summary.PackVersion,
                        ScenarioID = summary.ScenarioID,
                        ModIDs = summary.ModIDs,
                        ModVersions = summary.ModVersions,
                    }
                )
                .Pack;
        }
    }
}
