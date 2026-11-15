using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Rebellion.Game;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Defines admission through compiled simulation identity and actual composed gameplay data.
    /// The same policy applies to initial admission, content changes, shared loading and reconnects.
    /// </summary>
    internal static class MultiplayerCompatibility
    {
        public const string BuildMetadataKey = "SimulationBuild";

        public static string SimulationBuild =>
            typeof(GameRoot)
                .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == BuildMetadataKey)
                .Value;

        /// <summary>
        /// Rejects code identities other than the simulation build actually compiled into this process.
        /// </summary>
        /// <param name="remoteBuild">The remote process's compiled simulation identity.</param>
        public static void ValidateBuild(string remoteBuild)
        {
            if (GetBuildError(remoteBuild) is string error)
                throw new InvalidDataException(error);
        }

        /// <summary>
        /// Reports a build mismatch through the initial accept-or-reject handshake.
        /// </summary>
        /// <param name="remoteBuild">The remote process's compiled identity.</param>
        /// <returns>The rejection reason, or null for the same simulation build.</returns>
        public static string GetBuildError(string remoteBuild) =>
            string.Equals(remoteBuild, SimulationBuild, StringComparison.Ordinal)
                ? null
                : "The simulation builds do not match.";

        /// <summary>
        /// Rejects different composed gameplay data even when public pack versions match.
        /// </summary>
        /// <param name="localContent">The actual loaded gameplay catalog's digest.</param>
        /// <param name="remoteContent">The remote process's gameplay-data digest.</param>
        public static void ValidateContent(string localContent, string remoteContent)
        {
            if (
                string.IsNullOrEmpty(localContent)
                || !string.Equals(remoteContent, localContent, StringComparison.Ordinal)
            )
                throw new InvalidDataException("The loaded gameplay rules and data do not match.");
        }
    }
}
