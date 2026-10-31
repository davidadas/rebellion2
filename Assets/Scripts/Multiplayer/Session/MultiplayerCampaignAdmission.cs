using System;
using System.IO;
using System.Xml;
using Rebellion.Game;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Preserves authenticated participant and faction ownership when admitting saved campaigns.
    /// </summary>
    internal static class MultiplayerCampaignAdmission
    {
        private const string _hostPlayerID = "MULTIPLAYER_HOST";
        private const string _guestPlayerID = "MULTIPLAYER_GUEST";

        /// <summary>
        /// Describes an account mismatch without accepting peer-supplied participant identifiers.
        /// </summary>
        /// <param name="saved">The saved ownership, or null for a single-player source.</param>
        /// <param name="hostUserID">The authenticated host account.</param>
        /// <param name="guestUserID">The authenticated guest account, when present.</param>
        /// <param name="validateGuest">Whether a guest has already been admitted.</param>
        /// <returns>An actionable rejection reason, or null when the known participants match.</returns>
        public static string GetParticipantError(
            MultiplayerCampaignIdentity saved,
            string hostUserID,
            string guestUserID,
            bool validateGuest
        )
        {
            return
                saved != null
                && (
                    saved.HostUserID != hostUserID
                    || (validateGuest && saved.GuestUserID != guestUserID)
                )
                ? "This saved campaign is reserved for its authenticated participants."
                : null;
        }

        /// <summary>
        /// Rejects a saved campaign that does not belong to the admitted accounts and faction pair.
        /// </summary>
        /// <param name="saved">The saved authenticated ownership.</param>
        /// <param name="admitted">The connection's authenticated accounts and approved factions.</param>
        public static void ValidateSavedCampaign(
            MultiplayerCampaignIdentity saved,
            MultiplayerCampaignIdentity admitted
        )
        {
            if (
                saved == null
                || GetParticipantError(saved, admitted.HostUserID, admitted.GuestUserID, true)
                    != null
                || saved.HostFactionID != admitted.HostFactionID
                || saved.GuestFactionID != admitted.GuestFactionID
            )
                throw new InvalidDataException(
                    "The saved campaign belongs to different participants or factions."
                );
        }

        /// <summary>
        /// Initializes new host ownership or verifies existing saved ownership before admission.
        /// </summary>
        /// <param name="game">The generated or restored graph.</param>
        /// <param name="admitted">The authenticated participants and agreed factions.</param>
        /// <param name="authoritative">Whether the host may initialize campaign metadata.</param>
        public static void InitializeCampaign(
            GameRoot game,
            MultiplayerCampaignIdentity admitted,
            bool authoritative
        )
        {
            if (!authoritative)
            {
                if (game.Metadata?.IsMultiplayer == true)
                    ValidateSavedCampaign(game.Metadata.MultiplayerCampaign, admitted);
                return;
            }
            game.Metadata ??= new GameMetadata();
            if (string.IsNullOrWhiteSpace(game.Metadata.CampaignID))
                game.Metadata.CampaignID = Guid.NewGuid().ToString("N");
            if (!Guid.TryParseExact(game.Metadata.CampaignID, "N", out _))
                throw new InvalidDataException("The saved campaign identity is invalid.");
            if (game.Metadata.MultiplayerCampaign == null)
                game.Metadata.MultiplayerCampaign = admitted;
            else
                ValidateSavedCampaign(game.Metadata.MultiplayerCampaign, admitted);
        }

        /// <summary>
        /// Verifies ownership before changing controllers or the locally viewed faction.
        /// </summary>
        /// <param name="game">The generated or restored graph.</param>
        /// <param name="admitted">The authenticated participants and approved faction pair.</param>
        /// <param name="configuration">The negotiated launch and local faction.</param>
        /// <param name="authoritative">Whether campaign metadata may be initialized.</param>
        /// <param name="hostName">The authenticated host's presentation name.</param>
        /// <param name="guestName">The authenticated guest's presentation name.</param>
        public static void ConfigureGame(
            GameRoot game,
            MultiplayerCampaignIdentity admitted,
            MultiplayerLaunchConfiguration configuration,
            bool authoritative,
            string hostName,
            string guestName
        )
        {
            XmlConvert.VerifyXmlChars(hostName ?? string.Empty);
            XmlConvert.VerifyXmlChars(guestName ?? string.Empty);
            InitializeCampaign(game, admitted, authoritative);
            game.SetFactionController(
                admitted.HostFactionID,
                _hostPlayerID,
                PlayerControllerType.Human
            );
            game.SetFactionController(
                admitted.GuestFactionID,
                _guestPlayerID,
                PlayerControllerType.Human
            );
            game.LocalPlayerFactionID = configuration.LocalFactionID;
            game.UseDeterministicRuntimeIdentifiers = true;
            game.GetFactionPlayer(admitted.HostFactionID).DisplayName = hostName;
            game.GetFactionPlayer(admitted.GuestFactionID).DisplayName = guestName;
        }
    }
}
