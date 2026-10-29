using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Connects the pre-game room to admitted peers without entering the simulation lifecycle.
    /// </summary>
    public sealed partial class MultiplayerCoordinator
    {
        private readonly List<MultiplayerChatMessage> _chat = new();
        private MultiplayerLobby _lobby;
        private MultiplayerLobbyState _guestLobby;
        private ContentPack _lobbyContent;
        private GameRoot _restoredGame;
        private string _password;
        private string _passwordProof;
        private string _expectedFingerprint;
        private string _verifiedFingerprint;
        private long _preparedRevision = -1;
        private bool _preparingContent;
        private bool _rejectingGuest;

        /// <summary>
        /// Gets the current two-player room, or null outside a pre-game session.
        /// </summary>
        public MultiplayerLobbyState Lobby => _lobby?.State ?? _guestLobby;

        /// <summary>
        /// Gets the exact content verified for this room.
        /// </summary>
        public ContentPack LobbyContent => _lobbyContent;

        /// <summary>
        /// Gets the host's verified save for the pending strategy launch.
        /// </summary>
        public GameRoot RestoredGame => _launchConfiguration?.RestoredGame;

        /// <summary>
        /// Gets the ordered messages exchanged since entering this room.
        /// </summary>
        public IReadOnlyList<MultiplayerChatMessage> Chat => _chat;

        /// <summary>
        /// Gets whether save verification is still in progress.
        /// </summary>
        public bool IsLoadingSave { get; private set; }

        /// <summary>
        /// Gets whether the host ended the room while its final chat remains visible.
        /// </summary>
        public bool IsLobbyClosed { get; private set; }

        /// <summary>
        /// Applies the host's new-game setup through the room's revision boundary.
        /// </summary>
        /// <param name="galaxySize">The selected galaxy size.</param>
        /// <param name="victoryCondition">The selected victory condition.</param>
        public void SelectLobbyGameSetup(GameSize galaxySize, GameVictoryCondition victoryCondition)
        {
            if (_role != MultiplayerRole.Host || _lobby == null || IsLoadingSave)
                return;
            if (_lobby.SelectGameSetup(_role, galaxySize, victoryCondition))
                PublishLobby();
        }

        /// <summary>
        /// Changes the local participant's faction through the host's ownership boundary.
        /// </summary>
        /// <param name="factionID">The selected playable faction.</param>
        public void SelectLobbyFaction(string factionID)
        {
            if (_role == MultiplayerRole.Host)
            {
                _lobby.SelectFaction(_role, factionID);
                PublishLobby();
            }
            else if (_guestLobby != null)
                SendLobbyCommand(
                    MultiplayerMessageType.SelectFaction,
                    new MultiplayerLobbyCommand { factionID = factionID }
                );
        }

        /// <summary>
        /// Records the local player's explicit ready choice for the displayed settings.
        /// </summary>
        /// <param name="ready">Whether the local player is ready.</param>
        public void SetLobbyReady(bool ready)
        {
            MultiplayerLobbyState state = Lobby;
            if (state == null || IsLoadingSave)
                return;
            try
            {
                if (_role == MultiplayerRole.Host)
                {
                    _lobby.SetReady(_role, ready, state.settingsRevision);
                    PublishLobby();
                }
                else
                    SendLobbyCommand(
                        MultiplayerMessageType.LobbyReady,
                        new MultiplayerLobbyCommand
                        {
                            ready = ready,
                            settingsRevision = state.settingsRevision,
                        }
                    );
            }
            catch (InvalidOperationException exception)
            {
                SetStatus(exception.Message);
            }
        }

        /// <summary>
        /// Sends a player message to the other admitted participant and the local transcript.
        /// </summary>
        /// <param name="message">The plain-text message entered by the player.</param>
        public void SendLobbyChat(string message)
        {
            if (Lobby == null || IsLobbyClosed || string.IsNullOrWhiteSpace(message))
                return;
            MultiplayerChatMessage chat = new MultiplayerChatMessage
            {
                sender = _playerName,
                text = message.Trim(),
            };
            _chat.Add(chat);
            if (_peer?.IsConnected == true && !string.IsNullOrWhiteSpace(Lobby.guestName))
                QueueSend(
                    new MultiplayerPacket(
                        MultiplayerMessageType.LobbyChat,
                        JsonUtility.ToJson(chat)
                    )
                );
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Verifies and stages a host save without launching or requiring a guest-local save file.
        /// </summary>
        /// <param name="entry">The save selected from the persistence service.</param>
        /// <param name="requestedCancellation">Cancels validation without leaving the room.</param>
        /// <returns>The pending save and exact-content validation.</returns>
        public async Task LoadLobbySaveAsync(
            SaveGameEntry entry,
            System.Threading.CancellationToken requestedCancellation = default
        )
        {
            if (
                _role != MultiplayerRole.Host
                || _lobby == null
                || IsLoadingSave
                || _lobby.State.launching
            )
                return;
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            var sessionToken = _sessionCancellation.Token;
            using var cancellation =
                System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
                    sessionToken,
                    requestedCancellation
                );
            var cancellationToken = cancellation.Token;
            IsLoadingSave = true;
            SetStatus("Verifying saved game and required content");
            try
            {
                ContentPack active = AppBootstrap.EnsureExists().GetContentPack();
                ContentPack content = await Task.Run(() =>
                    SaveContentResolver.Resolve(active, entry.Metadata).Pack
                );
                GameRoot game = await SaveGameManager.Instance.LoadGameDataAsync(entry.FileName);
                cancellationToken.ThrowIfCancellationRequested();
                if (
                    !content.MatchesContentIdentity(game.Summary)
                    || !content.MatchesContentIdentity(game.Metadata)
                )
                    throw new InvalidOperationException(
                        "The saved state and its content metadata do not match."
                    );
                _lobby.SelectSave(
                    game.Summary,
                    entry.Metadata.SaveDisplayName ?? entry.FileName,
                    game.Metadata?.MultiplayerCampaign,
                    _backend?.LocalUserID,
                    _admittedGuestUserID
                );
                _restoredGame = game;
                _hostSummary = MultiplayerSnapshotCodec.DeserializeSummary(
                    MultiplayerSnapshotCodec.SerializeSummary(game.Summary)
                );
                _lobbyContent = content;
                PublishLobby();
                _ = UpdateHostedListingAsync(cancellationToken);
                SetStatus(string.Empty);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Failure = exception;
                    SetStatus("Could not load save: " + exception.Message);
                }
            }
            finally
            {
                if (!sessionToken.IsCancellationRequested)
                {
                    IsLoadingSave = false;
                    StateChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Launches only when both participants approved the current room configuration.
        /// </summary>
        public void LaunchLobby()
        {
            if (_role != MultiplayerRole.Host || _lobby == null || IsLoadingSave)
                return;
            try
            {
                _lobby.ValidateLaunch();
                if (_restoredGame?.Metadata?.IsMultiplayer == true)
                {
                    MultiplayerLobbyState state = _lobby.State;
                    MultiplayerCampaignAdmission.ValidateSavedCampaign(
                        _restoredGame.Metadata.MultiplayerCampaign,
                        GetAdmittedCampaignIdentity(state.hostFactionID, state.guestFactionID)
                    );
                }
            }
            catch (Exception exception)
                when (exception is InvalidOperationException or System.IO.InvalidDataException)
            {
                SetStatus(exception.Message);
                return;
            }
            _lobby.BeginLaunch();
            _listingState = "started";
            _ = UpdateHostedListingAsync(_sessionCancellation.Token);
            PublishLobby();
            QueueSend(new MultiplayerPacket(MultiplayerMessageType.StartGame));
            RequestLobbyLaunch();
        }

        /// <summary>
        /// Publishes the authoritative roster together with the complete content and game settings.
        /// </summary>
        private void PublishLobby()
        {
            if (_lobby == null)
                return;
            MultiplayerLobbyState state = _lobby.State;
            _hostSummary.GalaxySize = state.galaxySize;
            _hostSummary.VictoryCondition = state.victoryCondition;
            state.simulationBuild = MultiplayerCompatibility.SimulationBuild;
            state.gameplayContent = GetGameplayContentIdentity(_lobbyContent);
            if (_peer?.IsConnected == true && _lobby.State.guestName != null)
                QueueSend(
                    new MultiplayerPacket(
                        MultiplayerMessageType.LobbyState,
                        JsonUtility.ToJson(state),
                        MultiplayerSnapshotCodec.SerializeSummary(_hostSummary)
                    )
                );
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Receives the host's roster and asynchronously verifies any newly selected content.
        /// </summary>
        /// <param name="packet">The host's ordered room update.</param>
        private void HandleLobbyState(MultiplayerPacket packet)
        {
            if (
                _role != MultiplayerRole.Guest
                || _hostSummary == null
                || _launchConfiguration != null
            )
                return;
            MultiplayerLobbyState state =
                JsonUtility.FromJson<MultiplayerLobbyState>(packet.Metadata)
                ?? throw new InvalidOperationException("The lobby state is missing.");
            GameSummary summary = MultiplayerSnapshotCodec.DeserializeSummary(packet.Payload);
            state.ValidateGameSetup(summary);
            _guestLobby = state;
            _gameName = state.gameName;
            _hostSummary = summary;
            StateChanged?.Invoke();
            PrepareLobbyContent();
        }

        /// <summary>
        /// Opens host-selected content once per identity and acknowledges the latest settings revision.
        /// </summary>
        private async void PrepareLobbyContent()
        {
            if (_preparingContent || _guestLobby == null)
                return;
            var cancellationToken = _sessionCancellation.Token;
            _preparingContent = true;
            try
            {
                while (!cancellationToken.IsCancellationRequested && _guestLobby != null)
                {
                    GameSummary summary = _hostSummary;
                    string fingerprint = CreateContentFingerprint(summary);
                    if (_verifiedFingerprint != fingerprint)
                    {
                        ContentPack active = AppBootstrap.EnsureExists().GetContentPack();
                        ContentPack opened = await Task.Run(() =>
                            MultiplayerContentSelection.Open(active, summary)
                        );
                        cancellationToken.ThrowIfCancellationRequested();
                        _lobbyContent = opened;
                        _verifiedFingerprint = fingerprint;
                    }
                    if (fingerprint != CreateContentFingerprint(_hostSummary))
                        continue;
                    MultiplayerCompatibility.ValidateBuild(_guestLobby.simulationBuild);
                    MultiplayerCompatibility.ValidateContent(
                        GetGameplayContentIdentity(_lobbyContent),
                        _guestLobby.gameplayContent
                    );
                    if (_preparedRevision != _guestLobby.settingsRevision)
                    {
                        _preparedRevision = _guestLobby.settingsRevision;
                        SendLobbyCommand(
                            MultiplayerMessageType.LobbyContentReady,
                            new MultiplayerLobbyCommand
                            {
                                settingsRevision = _preparedRevision,
                                simulationBuild = MultiplayerCompatibility.SimulationBuild,
                                gameplayContent = GetGameplayContentIdentity(_lobbyContent),
                            }
                        );
                    }
                    StateChanged?.Invoke();
                    break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Disconnect();
                    Failure = exception;
                    SetStatus("Required content is unavailable: " + exception.Message);
                }
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    _preparingContent = false;
            }
        }

        /// <summary>
        /// Applies requests only to the remote guest's own state.
        /// </summary>
        /// <param name="packet">The guest's pre-game command.</param>
        private void HandleLobbyCommand(MultiplayerPacket packet)
        {
            if (
                _role != MultiplayerRole.Host
                || _lobby?.State.guestName == null
                || _launchConfiguration != null
            )
                return;
            MultiplayerLobbyCommand command =
                JsonUtility.FromJson<MultiplayerLobbyCommand>(packet.Metadata)
                ?? throw new InvalidOperationException("The lobby request is missing.");
            switch (packet.Type)
            {
                case MultiplayerMessageType.SelectFaction:
                    _lobby.SelectFaction(MultiplayerRole.Guest, command.factionID);
                    break;
                case MultiplayerMessageType.LobbyReady:
                    _lobby.SetReady(MultiplayerRole.Guest, command.ready, command.settingsRevision);
                    break;
                case MultiplayerMessageType.LobbyContentReady:
                    MultiplayerCompatibility.ValidateBuild(command.simulationBuild);
                    MultiplayerCompatibility.ValidateContent(
                        GetGameplayContentIdentity(_lobbyContent),
                        command.gameplayContent
                    );
                    _lobby.ConfirmContent(command.settingsRevision);
                    break;
            }
            PublishLobby();
        }

        /// <summary>
        /// Associates received text with the admitted roster instead of a peer-supplied sender label.
        /// </summary>
        /// <param name="packet">The received chat message.</param>
        private void HandleLobbyChat(MultiplayerPacket packet)
        {
            if (Lobby?.guestName == null)
                return;
            MultiplayerChatMessage message = JsonUtility.FromJson<MultiplayerChatMessage>(
                packet.Metadata
            );
            if (string.IsNullOrWhiteSpace(message?.text))
                return;
            message.sender = _role == MultiplayerRole.Host ? Lobby.guestName : Lobby.hostName;
            if (_role == MultiplayerRole.Host)
                message.isSystem = false;
            _chat.Add(message);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Accepts a launch only after both explicit approvals and local content verification.
        /// </summary>
        private void HandleStartGame()
        {
            if (
                _role != MultiplayerRole.Guest
                || _launchConfiguration != null
                || _guestLobby?.hostReady != true
                || !_guestLobby.guestReady
                || !_guestLobby.guestContentReady
                || !_guestLobby.HasValidFactionSelection
                || _preparedRevision != _guestLobby.settingsRevision
                || _lobbyContent == null
            )
                throw new InvalidOperationException("The lobby has not agreed to launch.");
            RequestLobbyLaunch();
        }

        /// <summary>
        /// Captures the agreed human factions and hands the verified content to the menu owner.
        /// </summary>
        private void RequestLobbyLaunch()
        {
            MultiplayerLobbyState state = Lobby;
            state.ValidateGameSetup(_hostSummary);
            if (!Guid.TryParseExact(state.initializationID, "N", out _))
                throw new InvalidOperationException(
                    "The agreed launch has no initialization identity."
                );
            BeginInitialization(state.initializationID);
            GameSummary summary = MultiplayerSnapshotCodec.DeserializeSummary(
                MultiplayerSnapshotCodec.SerializeSummary(_hostSummary)
            );
            summary.PlayerFactionID = state.hostFactionID;
            _launchConfiguration = new MultiplayerLaunchConfiguration(
                summary,
                _role == MultiplayerRole.Host ? state.hostFactionID : state.guestFactionID,
                state.hostFactionID,
                state.guestFactionID,
                _role == MultiplayerRole.Host ? _restoredGame : null
            );
            SetStatus("Launching game");
            LaunchRequested?.Invoke(_launchConfiguration);
        }

        /// <summary>
        /// Sends a typed guest request over the existing reliable ordered connection.
        /// </summary>
        /// <param name="type">The owned pre-game operation.</param>
        /// <param name="command">The requested selection or approval.</param>
        private void SendLobbyCommand(MultiplayerMessageType type, MultiplayerLobbyCommand command)
        {
            QueueSend(new MultiplayerPacket(type, JsonUtility.ToJson(command)));
        }

        /// <summary>
        /// Kicks a rejected application handshake from the service lobby.
        /// </summary>
        private async void RejectGuest()
        {
            try
            {
                await _backend.RejectGuestAsync();
            }
            catch (Exception exception)
            {
                SetStatus("Could not remove rejected player: " + exception.Message);
            }
        }

        /// <summary>
        /// Acknowledges a displayed admission error before service removal can close the connection.
        /// </summary>
        /// <param name="error">The host's rejected-join explanation.</param>
        private async void FinishRejectedJoin(string error)
        {
            var cancellationToken = _sessionCancellation.Token;
            try
            {
                await _peer.SendAsync(
                    new MultiplayerPacket(MultiplayerMessageType.AdmissionRejectedAck)
                );
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not acknowledge rejected join: " + exception.Message);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Disconnect();
                    Failure = new InvalidOperationException(error);
                    SetStatus(error);
                }
            }
        }

        /// <summary>
        /// Reopens a waiting host or returns a disconnected guest to discovery.
        /// </summary>
        /// <param name="reason">The transport's departure message.</param>
        private void HandlePeerDeparture(string reason)
        {
            if (_launchConfiguration != null)
            {
                EndForPeerDeparture();
                return;
            }
            MultiplayerLobbyState previous = Lobby;
            if (_launchConfiguration == null && _role == MultiplayerRole.Host && _lobby != null)
            {
                if (previous.guestName != null)
                    AddLobbyNotice(previous.guestName + " left the lobby.", false);
                _lobby.RemoveGuest();
                _rejectingGuest = false;
                _backend.ReopenHostPeer();
                _peer = _backend.Peer;
                _listingState = "waiting";
                _ = UpdateHostedListingAsync(_sessionCancellation.Token);
            }
            else if (_launchConfiguration == null)
            {
                ContentPack content = _lobbyContent;
                MultiplayerChatMessage[] messages = _chat.ToArray();
                Disconnect();
                if (previous != null)
                {
                    _guestLobby = previous;
                    _lobbyContent = content;
                    _chat.AddRange(messages);
                    IsLobbyClosed = true;
                    AddLobbyNotice(previous.hostName + " left the lobby. The lobby has ended.");
                }
            }
            SetStatus(_launchConfiguration == null ? string.Empty : reason);
        }

        /// <summary>
        /// Adds an authoritative roster notice to the same transcript as player conversation.
        /// </summary>
        /// <param name="text">The roster transition to present.</param>
        /// <param name="broadcast">Whether the other participant is still present to receive the notice.</param>
        private void AddLobbyNotice(string text, bool broadcast = true)
        {
            MultiplayerChatMessage message = new MultiplayerChatMessage
            {
                isSystem = true,
                text = text,
            };
            _chat.Add(message);
            if (
                broadcast
                && _role == MultiplayerRole.Host
                && _lobby?.State.guestName != null
                && _peer?.IsConnected == true
            )
                QueueSend(
                    new MultiplayerPacket(
                        MultiplayerMessageType.LobbyChat,
                        JsonUtility.ToJson(message)
                    )
                );
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Releases room-owned data when the session ends.
        /// </summary>
        private void ResetLobby()
        {
            _lobby = null;
            _guestLobby = null;
            _lobbyContent = null;
            _restoredGame = null;
            _password = _passwordProof = _expectedFingerprint = _verifiedFingerprint = null;
            _preparedRevision = -1;
            _preparingContent = IsLoadingSave = false;
            _rejectingGuest = false;
            IsLobbyClosed = false;
            _chat.Clear();
        }
    }
}
