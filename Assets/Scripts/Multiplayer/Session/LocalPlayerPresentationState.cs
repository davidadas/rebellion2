using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.UIState;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Retains presentation owned by one admitted local participant across shared graph replacement.
    /// The retained state remains live while its interface updates the existing collections in place.
    /// </summary>
    internal sealed class LocalPlayerPresentationState
    {
        private readonly string _campaignID;
        private readonly string _localFactionID;
        private readonly string _hostUserID;
        private readonly string _guestUserID;
        private readonly string _hostFactionID;
        private readonly string _guestFactionID;
        private readonly string _packID;
        private readonly string _packVersion;
        private readonly string _scenarioID;
        private readonly string[] _modIDs;
        private readonly string[] _modVersions;
        private readonly PlayerUIState _state;

        /// <summary>
        /// Captures immutable ownership and content identity alongside the locally owned presentation.
        /// </summary>
        /// <param name="game">The admitted source campaign.</param>
        /// <param name="localFactionID">The faction bound to this process's authenticated role.</param>
        /// <param name="state">The presentation object maintained by the local interface.</param>
        private LocalPlayerPresentationState(
            GameRoot game,
            string localFactionID,
            PlayerUIState state
        )
        {
            _campaignID = game.Metadata.CampaignID;
            _localFactionID = localFactionID;
            MultiplayerCampaignIdentity identity = game.Metadata.MultiplayerCampaign;
            _hostUserID = identity.HostUserID;
            _guestUserID = identity.GuestUserID;
            _hostFactionID = identity.HostFactionID;
            _guestFactionID = identity.GuestFactionID;
            _packID = game.Summary.PackID;
            _packVersion = game.Summary.PackVersion;
            _scenarioID = game.Summary.ScenarioID;
            _modIDs = (game.Summary.ModIDs ?? Array.Empty<string>()).ToArray();
            _modVersions = (game.Summary.ModVersions ?? Array.Empty<string>()).ToArray();
            _state = state;
        }

        /// <summary>
        /// Retains local presentation only from a campaign with explicit authenticated ownership.
        /// </summary>
        /// <param name="game">The current admitted game.</param>
        /// <param name="localFactionID">The authenticated local role's faction.</param>
        /// <returns>The retained presentation, or null when no admitted local state is available.</returns>
        public static LocalPlayerPresentationState Capture(GameRoot game, string localFactionID)
        {
            MultiplayerCampaignIdentity identity = game?.Metadata?.MultiplayerCampaign;
            if (
                identity == null
                || game.Summary == null
                || string.IsNullOrEmpty(localFactionID)
                || !Guid.TryParseExact(game.Metadata.CampaignID, "N", out _)
                || string.IsNullOrEmpty(identity.HostUserID)
                || string.IsNullOrEmpty(identity.GuestUserID)
                || (
                    localFactionID != identity.HostFactionID
                    && localFactionID != identity.GuestFactionID
                )
            )
                return null;
            Player player = game.GetFactionPlayer(localFactionID);
            if (player?.ControllerType != PlayerControllerType.Human || player.UIState == null)
                return null;
            return new LocalPlayerPresentationState(game, localFactionID, player.UIState);
        }

        /// <summary>
        /// Restores this participant's live presentation on a continuation of the same owned campaign.
        /// </summary>
        /// <param name="game">The incoming authoritative continuation.</param>
        /// <param name="localFactionID">The incoming authenticated local role's faction.</param>
        /// <returns>True when local presentation was preserved without changing another participant.</returns>
        public bool TryRestore(GameRoot game, string localFactionID)
        {
            MultiplayerCampaignIdentity identity = game?.Metadata?.MultiplayerCampaign;
            if (
                identity == null
                || game.Summary == null
                || localFactionID != _localFactionID
                || game.Metadata.CampaignID != _campaignID
                || identity.HostUserID != _hostUserID
                || identity.GuestUserID != _guestUserID
                || identity.HostFactionID != _hostFactionID
                || identity.GuestFactionID != _guestFactionID
                || game.Summary.PackID != _packID
                || game.Summary.PackVersion != _packVersion
                || game.Summary.ScenarioID != _scenarioID
                || !(game.Summary.ModIDs ?? Array.Empty<string>()).SequenceEqual(_modIDs)
                || !(game.Summary.ModVersions ?? Array.Empty<string>()).SequenceEqual(_modVersions)
            )
                return false;
            Player player = game.GetFactionPlayer(localFactionID);
            if (player?.ControllerType != PlayerControllerType.Human)
                return false;
            player.UIState = _state;
            return true;
        }

        /// <summary>
        /// Restores checkpoint presentation only from the participant's matching saved copy.
        /// Current presentation from a later day is never substituted for a saved checkpoint.
        /// </summary>
        /// <param name="authoritative">The host's selected saved world.</param>
        /// <param name="localSave">The participant's independently persisted copy.</param>
        /// <param name="localFactionID">The authenticated local role's faction.</param>
        /// <returns>True when the matching saved presentation was applied.</returns>
        public static bool TryRestoreSavedCheckpoint(
            GameRoot authoritative,
            GameRoot localSave,
            string localFactionID
        )
        {
            if (
                !Guid.TryParseExact(authoritative?.Metadata?.SharedSaveToken, "N", out _)
                || authoritative.Metadata.SharedSaveToken != localSave?.Metadata?.SharedSaveToken
                || authoritative.Metadata.PackID != localSave.Metadata.PackID
                || authoritative.Metadata.PackVersion != localSave.Metadata.PackVersion
                || authoritative.Metadata.ScenarioID != localSave.Metadata.ScenarioID
                || !(authoritative.Metadata.ModIDs ?? Array.Empty<string>()).SequenceEqual(
                    localSave.Metadata.ModIDs ?? Array.Empty<string>()
                )
                || !(authoritative.Metadata.ModVersions ?? Array.Empty<string>()).SequenceEqual(
                    localSave.Metadata.ModVersions ?? Array.Empty<string>()
                )
            )
                return false;
            LocalPlayerPresentationState saved = Capture(localSave, localFactionID);
            return saved?.TryRestore(authoritative, localFactionID) == true;
        }
    }
}
