using System;
using Rebellion.Game;

namespace Rebellion.Multiplayer
{
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
}
