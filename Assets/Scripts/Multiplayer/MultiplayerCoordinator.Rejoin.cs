using System;
using System.IO;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Gameplay;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Retains a paused campaign and restores its authenticated participants after connection loss.
    /// </summary>
    public sealed partial class MultiplayerCoordinator
    {
        private string _admittedGuestUserID;
        private MultiplayerPacket _pendingRejoinPacket;
        private bool _waitingForRejoin;
        private bool _checkpointPending;
        private bool _recoveryPausePending;
        private bool _recoveryOfferSent;
        private bool _returningToRecovery;
        private bool _publishedRecoveryOwner;
        private Task _recoveryPublication = Task.CompletedTask;
        private MultiplayerRecoveryRecord _recoveryRecord;
        private MultiplayerSnapshot _recoverySnapshot;

        /// <summary>
        /// Gets whether this authenticated account has a durable multiplayer continuation to reopen.
        /// </summary>
        public bool HasRecovery => _recoveryRecord != null && _launchConfiguration == null;

        /// <summary>
        /// Gets whether scene startup is reopening an existing paused multiplayer campaign.
        /// </summary>
        public bool IsRejoining => _returningToRecovery || _waitingForRejoin;

        /// <summary>
        /// Opens the authenticated account's protected checkpoint store.
        /// </summary>
        /// <returns>The recovery store in the selected application save directory.</returns>
        private MultiplayerRecoveryStore GetRecoveryStore() =>
            new MultiplayerRecoveryStore(
                (_runtime?.GetSaveGameManager() ?? SaveGameManager.Instance).GetSaveDirectoryPath()
            );

        /// <summary>
        /// Requests one continuation after an existing shared save or load completes.
        /// </summary>
        private void RequestRecoveryCheckpoint()
        {
            _checkpointPending = true;
        }

        /// <summary>
        /// Reads the small last-session locator after authentication without decoding its world.
        /// </summary>
        private void RefreshRecoveryRecord()
        {
            if (_backend == null || _launchConfiguration != null)
                return;
            try
            {
                _recoveryRecord = GetRecoveryStore().ReadRecord(_backend.LocalUserID);
            }
            catch (Exception exception)
            {
                _recoveryRecord = null;
                Debug.LogWarning("Could not read multiplayer recovery: " + exception.Message);
            }
        }

        /// <summary>
        /// Rejoins the surviving room or lets the original host reopen its durable campaign.
        /// </summary>
        /// <param name="selected">The explicitly selected room, or null to discover the retained campaign.</param>
        /// <returns>The pending authenticated room lookup and recovery launch.</returns>
        public async Task ResumeAsync(MatchmakerGame selected = null)
        {
            MultiplayerRecoveryRecord record = _recoveryRecord;
            if (record == null)
                throw new InvalidOperationException("There is no multiplayer recovery checkpoint.");
            if (selected != null)
                record.ValidateListing(selected);
            var cancellationToken = BeginSession(MultiplayerRole.Guest);
            _recoveryRecord = record;
            _returningToRecovery = true;
            try
            {
                await _leaveTask;
                cancellationToken.ThrowIfCancellationRequested();
                _backend = await _onlineClient.ConnectAsync();
                cancellationToken.ThrowIfCancellationRequested();
                MultiplayerRole role = record.GetRole(_backend.LocalUserID);
                MultiplayerCompatibility.ValidateBuild(record.simulationBuild);
                _playerName = NormalizePlayerName(_onlineClient.Session.DisplayName);
                _gameName = record.gameName;
                _hostPlayerName = record.hostName;
                _expectedFingerprint = record.contentFingerprint;
                SetStatus("Finding the paused game");
                MatchmakerGame room =
                    selected
                    ?? await _backend.FindRecoveryAsync(
                        record.lobbyID,
                        record.campaignID,
                        cancellationToken
                    );
                cancellationToken.ThrowIfCancellationRequested();
                if (room != null)
                {
                    record.ValidatePeer(_backend.LocalUserID, room.address);
                    _peer = await _backend.JoinAsync(room, cancellationToken, true);
                    cancellationToken.ThrowIfCancellationRequested();
                    SetStatus(
                        "Reconnecting to "
                            + (role == MultiplayerRole.Host ? record.guestName : record.hostName)
                    );
                    return;
                }
                if (role != MultiplayerRole.Host)
                    throw new InvalidOperationException(
                        "The host has not reopened this campaign yet. Try Resume game again when they return."
                    );
                GameRoot state = GetRecoveryStore().ReadState(_backend.LocalUserID);
                record.ValidateState(state);
                _role = role;
                _hostSummary = state.Summary;
                ContentPack active = AppBootstrap.EnsureExists().GetContentPack();
                _lobbyContent = await Task.Run(() =>
                    SaveContentResolver.Open(active, _hostSummary)
                );
                cancellationToken.ThrowIfCancellationRequested();
                MultiplayerCompatibility.ValidateContent(
                    GetGameplayContentIdentity(_lobbyContent),
                    record.gameplayContent
                );
                _listingState = "rejoining";
                _peer = await _backend.HostAsync(CreateListing(), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                record.lobbyID = _backend.LobbyId;
                _admittedGuestUserID = record.guestUserID;
                _waitingForRejoin = true;
                _recoveryPausePending = true;
                _checkpointPending = true;
                BeginInitialization(Guid.NewGuid().ToString("N"));
                LaunchRecovery(state);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                Disconnect();
                _recoveryRecord = record;
                Failure = exception;
                Debug.LogException(exception);
                SetStatus("Could not resume: " + exception.Message);
            }
        }

        /// <summary>
        /// Blocks new orders while an executing tick finishes or returns to the last completed checkpoint.
        /// </summary>
        private void EndForPeerDeparture()
        {
            if (
                _runtime?.GetActiveGameSession() == null
                || LoadingScreenController.IsLoadInProgress
                || IsTransferringScene
            )
            {
                bool interruptedLaunch = LoadingScreenController.IsLoadInProgress;
                _runtime?.BlockGameOperations();
                _runtime?.GetActiveGameManager()?.ApplyGameSpeed(TickSpeed.Paused);
                SessionEndNotice.ShowOpponentDeparture();
                Disconnect();
                if (interruptedLaunch)
                    AppBootstrap.RecoverInterruptedMultiplayerLoad();
                return;
            }
            _synchronizationReady = false;
            _waitingForRejoin = true;
            _recoveryOfferSent = false;
            _returningToRecovery = false;
            _publishedRecoveryOwner = false;
            _recoveryPausePending = true;
            _checkpointPending = true;
            _pendingRejoinPacket = null;
            _rejectingGuest = false;
            _pendingCommits.Clear();
            _snapshotAdmission.Clear();
            _remoteReady = false;
            _runtime.BlockGameOperations();
            ReleaseGameplay();
            _readyPresentationSession = _runtime.GetActiveGameSession();
            BindLocalRecoveryPersistence();
            _runtime.GetActiveGameManager().BindSpeedRequests(RequestGameSpeed);
            _backend.ReopenRecoveryPeer();
            _peer = _backend.Peer;
            SetStatus(CreateRejoinNotice());
        }

        /// <summary>
        /// Gives the surviving session local save ownership without admitting independent loads or orders.
        /// </summary>
        private void BindLocalRecoveryPersistence()
        {
            var runtime = _runtime;
            GameSession session = runtime.GetActiveGameSession();
            runtime.BindPersistenceCoordinator(
                new MultiplayerLocalPersistence(
                    runtime,
                    _role,
                    () =>
                    {
                        if (
                            !_disposed
                            && _waitingForRejoin
                            && ReferenceEquals(_runtime, runtime)
                            && ReferenceEquals(runtime.GetActiveGameSession(), session)
                        )
                            RequestRecoveryCheckpoint();
                    }
                )
            );
        }

        /// <summary>
        /// Describes the reserved counterpart without exposing transport implementation details.
        /// </summary>
        /// <returns>The compact galaxy-area connection notice.</returns>
        private string CreateRejoinNotice() =>
            "Waiting for "
            + (_role == MultiplayerRole.Host ? _recoveryRecord?.guestName : _hostPlayerName)
            + " to reconnectâ€¦";

        /// <summary>
        /// Captures only requested lifecycle checkpoints at a writable simulation boundary.
        /// </summary>
        private void ProcessRejoinRecovery()
        {
            GameSession session = _runtime?.GetActiveGameSession();
            if (session == null)
                return;
            if (_recoveryPausePending)
            {
                if (!session.CanExecuteCommands)
                    return;
                if (!session.CanCaptureSettledState)
                {
                    try
                    {
                        RestoreCompletedRecoveryCheckpoint();
                    }
                    catch (Exception exception)
                    {
                        _recoveryPausePending = false;
                        HandleGameplayFailure(exception);
                        return;
                    }
                }
                _recoveryPausePending = false;
                _runtime.GetActiveGameManager().ApplyGameSpeed(TickSpeed.Paused);
                foreach (Player player in session.Game.GetPlayers())
                {
                    if (player.ControllerType != PlayerControllerType.Human)
                        continue;
                    player.RequestedGameSpeed = TickSpeed.Paused;
                    player.GameSpeedSelectionRevision = checked(
                        player.GameSpeedSelectionRevision + 1
                    );
                }
                _runtime.ResumeMultiplayerOperations();
                if (ReferenceEquals(_readyPresentationSession, session))
                    _runtime.NotifyGameStateChanged();
            }
            if (!session.CanCaptureSettledState)
                return;
            if (_waitingForRejoin && _backend.IsLobbyOwner && !_publishedRecoveryOwner)
            {
                _publishedRecoveryOwner = true;
                _listingState = "rejoining";
                _ = UpdateHostedListingAsync(_sessionCancellation.Token);
            }
            if (_checkpointPending && _recoveryPublication.IsCompleted)
            {
                _checkpointPending = false;
                try
                {
                    MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(session);
                    MultiplayerRecoveryRecord record = CreateRecoveryRecord(snapshot.Hash);
                    _recoverySnapshot = snapshot;
                    _recoveryRecord = record;
                    _recoveryPublication = PublishRecoveryAsync(
                        _backend.LocalUserID,
                        _recoveryRecord,
                        _recoverySnapshot
                    );
                }
                catch (Exception exception)
                {
                    HandleGameplayFailure(exception);
                    SetStatus(
                        "The recovery checkpoint could not be prepared: " + exception.Message
                    );
                    return;
                }
            }
            if (_waitingForRejoin && _pendingRejoinPacket != null && _peer?.IsConnected == true)
            {
                MultiplayerPacket packet = _pendingRejoinPacket;
                _pendingRejoinPacket = null;
                if (packet.Type == MultiplayerMessageType.RecoveryWelcome)
                    HandleRecoveryWelcome(packet);
                else
                    HandleRecoveryHello(packet);
            }
        }

        /// <summary>
        /// Discards an interrupted turn and restores the retained completed world before negotiating rejoin.
        /// </summary>
        private void RestoreCompletedRecoveryCheckpoint()
        {
            MultiplayerSnapshot snapshot =
                _recoverySnapshot
                ?? throw new InvalidOperationException(
                    "No completed recovery checkpoint is available."
                );
            GameRoot state = MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash);
            _recoveryRecord.ValidateState(state);
            ConfigureGame(state);
            ReplaceGame(state);
        }

        /// <summary>
        /// Publishes owned checkpoint bytes without letting a departed connection cancel durable storage.
        /// </summary>
        /// <param name="userID">The authenticated local account captured before publication.</param>
        /// <param name="record">The immutable room and checkpoint metadata.</param>
        /// <param name="snapshot">The captured continuation bytes.</param>
        /// <returns>The observed durable publication.</returns>
        private async Task PublishRecoveryAsync(
            string userID,
            MultiplayerRecoveryRecord record,
            MultiplayerSnapshot snapshot
        )
        {
            try
            {
                await GetRecoveryStore().WriteAsync(userID, record, snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!_disposed && ReferenceEquals(_recoveryRecord, record))
                    SetStatus("The recovery checkpoint could not be saved: " + exception.Message);
            }
        }

        /// <summary>
        /// Records original participant roles independently of room ownership migration.
        /// </summary>
        /// <param name="hash">The exact captured continuation hash.</param>
        /// <returns>The account-bound room locator and content contract.</returns>
        private MultiplayerRecoveryRecord CreateRecoveryRecord(string hash)
        {
            GameRoot game = _runtime.GetActiveGame();
            MultiplayerCampaignIdentity identity = game.Metadata.MultiplayerCampaign;
            return new MultiplayerRecoveryRecord
            {
                lobbyID = _backend.LobbyId ?? _recoveryRecord?.lobbyID,
                campaignID = game.Metadata.CampaignID,
                gameName = _gameName,
                hostName = game.GetFactionPlayer(identity.HostFactionID).DisplayName,
                guestName = game.GetFactionPlayer(identity.GuestFactionID).DisplayName,
                hostUserID = identity.HostUserID,
                guestUserID = identity.GuestUserID,
                hostFactionID = identity.HostFactionID,
                guestFactionID = identity.GuestFactionID,
                contentFingerprint = EpicLobbyListing.HashContent(
                    CreateContentFingerprint(game.Summary)
                ),
                simulationBuild = MultiplayerCompatibility.SimulationBuild,
                gameplayContent = GetGameplayContentIdentity(_lobbyContent),
                snapshotHash = hash,
            };
        }

        /// <summary>
        /// Admits only the reserved counterpart and offers the survivor's settled continuation.
        /// </summary>
        /// <param name="packet">The returning participant's handshake.</param>
        private void HandleRecoveryHello(MultiplayerPacket packet)
        {
            if (!_waitingForRejoin || _recoveryOfferSent)
                return;
            if (_recoveryPausePending || _checkpointPending)
            {
                _pendingRejoinPacket = packet;
                return;
            }
            MultiplayerHelloMessage hello = JsonUtility.FromJson<MultiplayerHelloMessage>(
                packet.Metadata
            );
            try
            {
                if (
                    hello?.protocolVersion != _protocolVersion
                    || hello.recoveryCampaignID != _recoveryRecord?.campaignID
                )
                    throw new InvalidDataException(
                        "This campaign is reserved for its returning player."
                    );
                _recoveryRecord.ValidatePeer(_backend.LocalUserID, _backend.RemoteUserID);
                MultiplayerCompatibility.ValidateBuild(hello.simulationBuild);
                if (hello.contentFingerprint != _recoveryRecord.contentFingerprint)
                    throw new InvalidDataException(
                        "The returning player's content selection does not match."
                    );
                if (
                    _runtime?.GetActiveGameSession()?.CanCaptureSettledState != true
                    || _recoveryPausePending
                )
                {
                    _pendingRejoinPacket = packet;
                    return;
                }
                _recoveryRecord.UpdatePlayerNames(
                    _backend.LocalUserID,
                    _playerName,
                    NormalizePlayerName(hello.playerName)
                );
                _hostPlayerName = _recoveryRecord.hostName;
                _lobby?.UpdateDisplayNames(_recoveryRecord.hostName, _recoveryRecord.guestName);
                BeginInitialization(Guid.NewGuid().ToString("N"));
                MultiplayerSnapshot snapshot = PrepareInitialSnapshot(
                    out MultiplayerSnapshotMessage metadata
                );
                _recoveryRecord = CreateRecoveryRecord(snapshot.Hash);
                _recoveryOfferSent = true;
                _rejectingGuest = false;
                QueueSend(
                    new MultiplayerPacket(
                        MultiplayerMessageType.RecoveryWelcome,
                        JsonUtility.ToJson(
                            new MultiplayerRecoveryOffer
                            {
                                record = _recoveryRecord,
                                snapshot = metadata,
                            }
                        ),
                        snapshot.Payload
                    )
                );
                SetStatus("The returning player is loading the paused game");
            }
            catch (Exception exception)
            {
                QueueSend(
                    new MultiplayerPacket(
                        MultiplayerMessageType.Welcome,
                        JsonUtility.ToJson(
                            new MultiplayerWelcomeMessage
                            {
                                accepted = false,
                                error = exception.Message,
                            }
                        )
                    )
                );
                _rejectingGuest = true;
            }
        }

        /// <summary>
        /// Verifies the authenticated survivor before decoding or launching its campaign state.
        /// </summary>
        /// <param name="packet">The survivor's complete recovery offer.</param>
        private void HandleRecoveryWelcome(MultiplayerPacket packet)
        {
            bool livePeer = _waitingForRejoin && !_recoveryOfferSent && _runtime != null;
            if ((!_returningToRecovery && !livePeer) || _recoveryRecord == null)
                throw new InvalidOperationException("No campaign recovery is being negotiated.");
            if (livePeer && !_runtime.GetActiveGameSession().CanExecuteCommands)
            {
                _pendingRejoinPacket = packet;
                return;
            }
            MultiplayerRecoveryOffer offer = JsonUtility.FromJson<MultiplayerRecoveryOffer>(
                packet.Metadata
            );
            _recoveryRecord.ValidateOffer(offer?.record);
            _recoveryRecord.ValidatePeer(_backend.LocalUserID, _backend.RemoteUserID);
            MultiplayerCompatibility.ValidateBuild(offer.record.simulationBuild);
            if (
                offer.snapshot == null
                || offer.snapshot.hash != offer.record.snapshotHash
                || !Guid.TryParseExact(offer.snapshot.initializationID, "N", out _)
            )
                throw new InvalidDataException("The recovery has no initialization identity.");
            GameRoot state = MultiplayerSnapshotCodec.Restore(
                packet.Payload,
                offer.record.snapshotHash
            );
            offer.record.ValidateState(state);
            _recoveryRecord = offer.record;
            _gameName = offer.record.gameName;
            _role = offer.record.GetRole(_backend.LocalUserID);
            _hostPlayerName = offer.record.hostName;
            _lobby?.UpdateDisplayNames(offer.record.hostName, offer.record.guestName);
            _admittedGuestUserID = offer.record.guestUserID;
            BeginInitialization(offer.snapshot.initializationID);
            if (
                !_snapshotAdmission.Accepts(
                    offer.snapshot.initializationID,
                    offer.snapshot.revision
                )
            )
                throw new InvalidDataException("The rejoin snapshot revision is invalid.");
            _pendingRejoinSnapshot = offer.snapshot;
            if (livePeer)
            {
                ConfigureGame(state);
                ReplaceGame(state);
                _runtime.GetActiveGameManager().BindSpeedRequests(RequestGameSpeed);
                _recoveryPausePending = false;
                _checkpointPending = false;
                _recoveryOfferSent = true;
                NotifyPresentationReady(_runtime);
                SetStatus("Synchronizing the game");
                return;
            }
            PrepareRecoveryContent(state);
        }

        /// <summary>
        /// Resolves the exact installed content before restoring the offered continuation.
        /// </summary>
        /// <param name="state">The authenticated independently decoded runtime state.</param>
        private async void PrepareRecoveryContent(GameRoot state)
        {
            var cancellationToken = _sessionCancellation.Token;
            try
            {
                ContentPack active = AppBootstrap.EnsureExists().GetContentPack();
                ContentPack content = await Task.Run(() =>
                    SaveContentResolver.Open(active, state.Summary)
                );
                cancellationToken.ThrowIfCancellationRequested();
                MultiplayerCompatibility.ValidateContent(
                    GetGameplayContentIdentity(content),
                    _recoveryRecord.gameplayContent
                );
                _lobbyContent = content;
                LaunchRecovery(state);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                Disconnect();
                RefreshRecoveryRecord();
                Failure = exception;
                SetStatus("Could not rejoin: " + exception.Message);
            }
        }

        /// <summary>
        /// Restores each account to its original faction and hands the continuation to scene composition.
        /// </summary>
        /// <param name="state">The verified saved or survivor-supplied continuation.</param>
        private void LaunchRecovery(GameRoot state)
        {
            state.SetGameSpeed(TickSpeed.Paused);
            foreach (Player player in state.GetPlayers())
                if (player.ControllerType == PlayerControllerType.Human)
                    player.RequestedGameSpeed = TickSpeed.Paused;
            _hostSummary = state.Summary;
            _launchConfiguration = new MultiplayerLaunchConfiguration(
                _hostSummary,
                _role == MultiplayerRole.Host
                    ? _recoveryRecord.hostFactionID
                    : _recoveryRecord.guestFactionID,
                _recoveryRecord.hostFactionID,
                _recoveryRecord.guestFactionID,
                state
            );
            SetStatus("Rejoining the paused game");
            LaunchRequested?.Invoke(_launchConfiguration);
        }

        /// <summary>
        /// Releases connection-scoped recovery work while retaining the durable last-session locator.
        /// </summary>
        private void ResetRecovery()
        {
            _admittedGuestUserID = null;
            _pendingRejoinPacket = null;
            _waitingForRejoin = _checkpointPending = _recoveryPausePending = false;
            _recoveryOfferSent = _returningToRecovery = _publishedRecoveryOwner = false;
            _recoverySnapshot = null;
            _pendingRejoinSnapshot = null;
        }
    }

    /// <summary>
    /// Associates one complete paused continuation with a fresh gameplay initialization.
    /// </summary>
    [Serializable]
    internal sealed class MultiplayerRecoveryOffer
    {
        public MultiplayerRecoveryRecord record;
        public MultiplayerSnapshotMessage snapshot;
    }
}
