using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Rebellion.Game;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Carries the host's pre-game roster and readiness over the ordered peer connection.
    /// </summary>
    [Serializable]
    public sealed class MultiplayerLobbyState
    {
        public string gameName;
        public string hostName;
        public string guestName;
        public string hostFactionID;
        public string guestFactionID;
        public string saveName;
        public bool hasSavedGame;
        public GameSize galaxySize;
        public GameVictoryCondition victoryCondition;
        public bool hasSavedFactionBinding;
        public string savedHostFactionID;
        public string savedGuestFactionID;
        public bool hostReady;
        public bool guestReady;
        public bool guestContentReady;
        public bool launching;
        public string initializationID;
        public long settingsRevision;
        public string simulationBuild;
        public string gameplayContent;

        /// <summary>
        /// Gets the correction required when selected factions differ from saved ownership.
        /// </summary>
        public string FactionSelectionError =>
            hasSavedFactionBinding
            && (hostFactionID != savedHostFactionID || guestFactionID != savedGuestFactionID)
                ? "Select the factions used by these players in the saved campaign."
                : null;

        /// <summary>
        /// Gets whether the selected factions satisfy both distinctness and saved ownership.
        /// </summary>
        public bool HasValidFactionSelection =>
            hostFactionID != guestFactionID && FactionSelectionError == null;

        /// <summary>
        /// Gets whether both human participants have approved valid factions and verified content.
        /// </summary>
        public bool CanLaunch =>
            !launching
            && !string.IsNullOrWhiteSpace(guestName)
            && guestContentReady
            && hostReady
            && guestReady
            && HasValidFactionSelection;

        /// <summary>
        /// Verifies that the displayed setup describes the settings that will be launched.
        /// </summary>
        /// <param name="summary">The launch settings accompanying this roster.</param>
        public void ValidateGameSetup(GameSummary summary)
        {
            if (
                summary == null
                || !Enum.IsDefined(typeof(GameSize), galaxySize)
                || !Enum.IsDefined(typeof(GameVictoryCondition), victoryCondition)
                || galaxySize != summary.GalaxySize
                || victoryCondition != summary.VictoryCondition
            )
                throw new InvalidOperationException("The lobby and game settings do not match.");
        }
    }

    /// <summary>
    /// Owns pre-game mutations, invalidation of readiness, and the single launch boundary.
    /// </summary>
    public sealed class MultiplayerLobby
    {
        private readonly string[] _factions;
        private readonly MultiplayerLobbyState _state;

        /// <summary>
        /// Gets a detached roster suitable for presentation and serialization.
        /// </summary>
        public MultiplayerLobbyState State => CopyState();

        /// <summary>
        /// Creates a waiting room with exactly two playable faction choices.
        /// </summary>
        /// <param name="gameName">The advertised room name.</param>
        /// <param name="hostName">The authenticated host name.</param>
        /// <param name="summary">The host's selected launch settings.</param>
        public MultiplayerLobby(string gameName, string hostName, GameSummary summary)
        {
            _factions = summary?.StartingFactionIDs?.ToArray() ?? Array.Empty<string>();
            if (
                _factions.Length != 2
                || _factions.Distinct().Count() != 2
                || _factions.Any(string.IsNullOrWhiteSpace)
                || !_factions.Contains(summary.PlayerFactionID)
            )
                throw new ArgumentException(
                    "Multiplayer requires two distinct playable factions.",
                    nameof(summary)
                );
            _state = new MultiplayerLobbyState
            {
                gameName = gameName,
                hostName = hostName,
                hostFactionID = summary.PlayerFactionID,
                guestFactionID = _factions.First(id => id != summary.PlayerFactionID),
                galaxySize = summary.GalaxySize,
                victoryCondition = summary.VictoryCondition,
            };
            _state.ValidateGameSetup(summary);
        }

        /// <summary>
        /// Changes host-owned new-game settings and invalidates both participants' approvals.
        /// </summary>
        /// <param name="role">The participant requesting the change.</param>
        /// <param name="galaxySize">The supported galaxy size.</param>
        /// <param name="victoryCondition">The supported victory condition.</param>
        /// <returns>True when the configuration changed.</returns>
        public bool SelectGameSetup(
            MultiplayerRole role,
            GameSize galaxySize,
            GameVictoryCondition victoryCondition
        )
        {
            EnsureWaiting();
            if (role != MultiplayerRole.Host || _state.hasSavedGame)
                throw new InvalidOperationException("Only the host can configure a new game.");
            if (!Enum.IsDefined(typeof(GameSize), galaxySize))
                throw new ArgumentOutOfRangeException(nameof(galaxySize));
            if (!Enum.IsDefined(typeof(GameVictoryCondition), victoryCondition))
                throw new ArgumentOutOfRangeException(nameof(victoryCondition));
            if (_state.galaxySize == galaxySize && _state.victoryCondition == victoryCondition)
                return false;
            _state.galaxySize = galaxySize;
            _state.victoryCondition = victoryCondition;
            InvalidateReady();
            return true;
        }

        /// <summary>
        /// Adds the admitted guest and invalidates previous ready decisions.
        /// </summary>
        /// <param name="name">The joining player's name.</param>
        public void Join(string name)
        {
            EnsureWaiting();
            if (string.IsNullOrWhiteSpace(name) || _state.guestName != null)
                throw new InvalidOperationException("The guest slot is unavailable.");
            _state.guestName = name;
            InvalidateReady();
        }

        /// <summary>
        /// Refreshes authenticated presentation names without changing the campaign's approved setup.
        /// </summary>
        /// <param name="hostName">The normalized host display name accepted during rejoin.</param>
        /// <param name="guestName">The normalized guest display name accepted during rejoin.</param>
        public void UpdateDisplayNames(string hostName, string guestName)
        {
            _state.hostName = hostName;
            _state.guestName = guestName;
        }

        /// <summary>
        /// Reopens the second slot after a guest leaves before launch.
        /// </summary>
        public void RemoveGuest()
        {
            EnsureWaiting();
            _state.guestName = null;
            InvalidateReady();
        }

        /// <summary>
        /// Changes only the requesting player's faction and clears both approvals.
        /// </summary>
        /// <param name="role">The participant making the selection.</param>
        /// <param name="factionID">One of the two authored factions.</param>
        public void SelectFaction(MultiplayerRole role, string factionID)
        {
            EnsureParticipant(role);
            if (!_factions.Contains(factionID))
                throw new ArgumentException("Select a playable faction.", nameof(factionID));
            string current =
                role == MultiplayerRole.Host ? _state.hostFactionID : _state.guestFactionID;
            if (current == factionID)
                return;
            if (role == MultiplayerRole.Host)
                _state.hostFactionID = factionID;
            else
                _state.guestFactionID = factionID;
            InvalidateReady();
        }

        /// <summary>
        /// Confirms that the guest opened the exact content for the current settings.
        /// </summary>
        /// <param name="settingsRevision">The settings the guest verified.</param>
        public void ConfirmContent(long settingsRevision)
        {
            EnsureParticipant(MultiplayerRole.Guest);
            if (settingsRevision == _state.settingsRevision)
                _state.guestContentReady = true;
        }

        /// <summary>
        /// Records an explicit approval only for the currently displayed configuration.
        /// </summary>
        /// <param name="role">The approving participant.</param>
        /// <param name="ready">Whether the participant is ready.</param>
        /// <param name="settingsRevision">The configuration that was displayed.</param>
        public void SetReady(MultiplayerRole role, bool ready, long settingsRevision)
        {
            EnsureParticipant(role);
            if (settingsRevision != _state.settingsRevision)
                throw new InvalidOperationException(
                    "The lobby changed. Review the settings and ready up again."
                );
            if (ready && _state.FactionSelectionError != null)
                throw new InvalidOperationException(_state.FactionSelectionError);
            if (
                ready
                && (
                    _state.hostFactionID == _state.guestFactionID
                    || (role == MultiplayerRole.Guest && !_state.guestContentReady)
                )
            )
                throw new InvalidOperationException(
                    "Choose different factions and verify the content before readying up."
                );
            if (role == MultiplayerRole.Host)
                _state.hostReady = ready;
            else
                _state.guestReady = ready;
        }

        /// <summary>
        /// Replaces the selected save and resets the roster's content and ready approvals.
        /// </summary>
        /// <param name="summary">The verified saved-game settings.</param>
        /// <param name="saveName">The save's display name.</param>
        /// <param name="savedIdentity">The saved multiplayer ownership, or null for promotion.</param>
        /// <param name="hostUserID">The authenticated host selecting the save.</param>
        /// <param name="guestUserID">The authenticated guest already admitted to this room.</param>
        public void SelectSave(
            GameSummary summary,
            string saveName,
            MultiplayerCampaignIdentity savedIdentity = null,
            string hostUserID = null,
            string guestUserID = null
        )
        {
            EnsureWaiting();
            string participantError = MultiplayerCampaignAdmission.GetParticipantError(
                savedIdentity,
                hostUserID,
                guestUserID,
                _state.guestName != null
            );
            if (participantError != null)
                throw new InvalidOperationException(participantError);
            if (
                summary == null
                || !_factions
                    .OrderBy(id => id)
                    .SequenceEqual(
                        (summary.StartingFactionIDs ?? Array.Empty<string>()).OrderBy(id => id)
                    )
            )
                throw new InvalidOperationException(
                    "The save must contain this lobby's two playable factions."
                );
            if (
                savedIdentity != null
                && (
                    !_factions.Contains(savedIdentity.HostFactionID)
                    || !_factions.Contains(savedIdentity.GuestFactionID)
                    || savedIdentity.HostFactionID == savedIdentity.GuestFactionID
                )
            )
                throw new InvalidOperationException(
                    "The saved campaign must assign its players to both playable factions."
                );
            var setup = new MultiplayerLobbyState
            {
                galaxySize = summary.GalaxySize,
                victoryCondition = summary.VictoryCondition,
            };
            setup.ValidateGameSetup(summary);
            _state.hasSavedGame = true;
            _state.galaxySize = summary.GalaxySize;
            _state.victoryCondition = summary.VictoryCondition;
            _state.hasSavedFactionBinding = savedIdentity != null;
            _state.savedHostFactionID = savedIdentity?.HostFactionID;
            _state.savedGuestFactionID = savedIdentity?.GuestFactionID;
            if (savedIdentity != null)
            {
                _state.hostFactionID = savedIdentity.HostFactionID;
                _state.guestFactionID = savedIdentity.GuestFactionID;
            }
            _state.saveName = saveName;
            InvalidateReady();
        }

        /// <summary>
        /// Locks the agreed configuration against further pre-game changes.
        /// </summary>
        public void BeginLaunch()
        {
            ValidateLaunch();
            _state.launching = true;
            _state.initializationID = Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Verifies readiness and saved ownership without changing the room's launch state.
        /// </summary>
        public void ValidateLaunch()
        {
            if (_state.FactionSelectionError != null)
                throw new InvalidOperationException(_state.FactionSelectionError);
            if (!_state.CanLaunch)
                throw new InvalidOperationException("Both players must be ready before launching.");
        }

        /// <summary>
        /// Clears approvals whenever a selection or participant changes.
        /// </summary>
        private void InvalidateReady()
        {
            _state.settingsRevision++;
            _state.hostReady = _state.guestReady = _state.guestContentReady = false;
        }

        /// <summary>
        /// Rejects changes after the launch boundary.
        /// </summary>
        private void EnsureWaiting()
        {
            if (_state.launching)
                throw new InvalidOperationException("The game is already launching.");
        }

        /// <summary>
        /// Checks ownership and current membership before a participant mutation.
        /// </summary>
        /// <param name="role">The requesting participant.</param>
        private void EnsureParticipant(MultiplayerRole role)
        {
            EnsureWaiting();
            if (
                role == MultiplayerRole.None
                || (role == MultiplayerRole.Guest && _state.guestName == null)
            )
                throw new InvalidOperationException(
                    "Join the lobby before changing player settings."
                );
        }

        /// <summary>
        /// Copies the current state without exposing mutable authoritative fields.
        /// </summary>
        /// <returns>The detached lobby state.</returns>
        private MultiplayerLobbyState CopyState()
        {
            return new MultiplayerLobbyState
            {
                gameName = _state.gameName,
                hostName = _state.hostName,
                guestName = _state.guestName,
                hostFactionID = _state.hostFactionID,
                guestFactionID = _state.guestFactionID,
                saveName = _state.saveName,
                hasSavedGame = _state.hasSavedGame,
                galaxySize = _state.galaxySize,
                victoryCondition = _state.victoryCondition,
                hasSavedFactionBinding = _state.hasSavedFactionBinding,
                savedHostFactionID = _state.savedHostFactionID,
                savedGuestFactionID = _state.savedGuestFactionID,
                hostReady = _state.hostReady,
                guestReady = _state.guestReady,
                guestContentReady = _state.guestContentReady,
                launching = _state.launching,
                initializationID = _state.initializationID,
                settingsRevision = _state.settingsRevision,
            };
        }
    }

    /// <summary>
    /// Computes a lobby-specific password proof without publishing a password in discovery metadata.
    /// </summary>
    public static class MultiplayerLobbyPassword
    {
        /// <summary>
        /// Derives an admission proof scoped to one lobby identifier.
        /// </summary>
        /// <param name="lobbyID">The service-assigned lobby identifier.</param>
        /// <param name="password">The user-entered password.</param>
        /// <returns>The proof, or an empty string for an unprotected room.</returns>
        public static string CreateProof(string lobbyID, string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;
            using SHA256 hash = SHA256.Create();
            return Convert.ToBase64String(
                hash.ComputeHash(Encoding.UTF8.GetBytes(lobbyID + "\n" + password))
            );
        }
    }

    /// <summary>
    /// Carries an owned pre-game request or an acknowledged content revision.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerLobbyCommand
    {
        public string factionID;
        public bool ready;
        public long settingsRevision;
        public string simulationBuild;
        public string gameplayContent;
    }

    /// <summary>
    /// Carries a plain-text player message on the admitted lobby connection.
    /// </summary>
    [Serializable]
    public sealed class MultiplayerChatMessage
    {
        public string sender;
        public string text;
        public bool isSystem;
    }
}
