using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Rebellion.App;
using Rebellion.Game;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Owns shared checkpoint storage, participant namespaces, and agreed saved-game scene transfers.
    /// </summary>
    public sealed partial class MultiplayerCoordinator : IGamePersistenceCoordinator
    {
        private MultiplayerPersistenceSession _persistence;
        private UserGameplaySettings _hostAutosaveSettings;
        private GameRoot _persistenceLoadedGame;
        private ContentPack _persistenceContent;

        /// <summary>
        /// Gets whether the admitted connection owns an agreed transition between strategy scenes.
        /// </summary>
        public bool IsTransferringScene => _persistence?.IsTransferringScene == true;

        /// <summary>
        /// Gets whether either admitted player can queue a save for the next settled checkpoint boundary.
        /// </summary>
        public bool CanRequestSave =>
            _synchronizationReady && _runtime != null && _persistence != null && Failure == null;

        /// <summary>
        /// Preserves this participant's live presentation before notifying the scene of a replacement.
        /// </summary>
        /// <param name="game">The configured incoming graph with authenticated participant ownership.</param>
        private void ReplaceGame(GameRoot game)
        {
            _runtime.ValidateGameContent(game);
            GameRoot current = _runtime.GetActiveGame();
            LocalPlayerPresentationState presentation = LocalPlayerPresentationState.Capture(
                current,
                current.LocalPlayerFactionID
            );
            presentation?.TryRestore(game, game.LocalPlayerFactionID);
            _runtime.ReplaceGame(game);
        }

        /// <summary>
        /// Lists this role's multiplayer copies while retaining single-player sources for promotion.
        /// </summary>
        /// <param name="store">The persistence store owned by the requesting interface.</param>
        /// <returns>The visible local save entries.</returns>
        public IReadOnlyList<SaveGameEntry> GetVisibleSavedGames(SaveGameManager store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            return store.GetSavedGames(
                _role switch
                {
                    MultiplayerRole.Host => true,
                    MultiplayerRole.Guest => false,
                    _ => (bool?)null,
                }
            );
        }

        /// <summary>
        /// Requests a manual checkpoint on both participants and waits for both durable copies.
        /// </summary>
        /// <param name="fileName">The requested logical or existing save identifier.</param>
        /// <param name="displayName">The requested save label.</param>
        /// <returns>True only after both copies have been acknowledged.</returns>
        public Task<bool> SaveGameAsync(string fileName, string displayName)
        {
            if (!_synchronizationReady || _runtime == null || _persistence == null)
                return Task.FromResult(false);
            return _persistence.RequestAsync(fileName, displayName, false);
        }

        /// <summary>
        /// Transfers the requesting participant's selected saved world and waits for both replacement scenes.
        /// </summary>
        /// <param name="fileName">The selected local copy or host save identifier.</param>
        /// <returns>True only after the replacement game's state handshake completes.</returns>
        public async Task<bool> LoadGameAsync(string fileName)
        {
            if (!_synchronizationReady || _runtime == null || _persistence == null)
                return false;
            MultiplayerPersistenceSession owner = _persistence;
            GameRuntime runtime = _runtime;
            var cancellationToken = _sessionCancellation.Token;
            MultiplayerSnapshot source = null;
            if (_role == MultiplayerRole.Guest)
            {
                string localFileName =
                    fileName == SaveGameManager.QuickSaveFileName
                        ? SaveGameManager.GetMultiplayerSaveFileName(
                            runtime.GetActiveGame().Metadata.CampaignID,
                            false,
                            fileName
                        )
                        : fileName;
                GameRoot selected = await runtime
                    .GetSaveGameManager()
                    .LoadGameDataAsync(localFileName);
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_persistence, owner) || !ReferenceEquals(_runtime, runtime))
                    throw new OperationCanceledException(
                        "The requesting game no longer owns this load."
                    );
                source = MultiplayerSnapshotCodec.Capture(selected);
            }
            return await owner.RequestAsync(fileName, null, true, source);
        }

        /// <summary>
        /// Schedules shared automatic metadata from the host's configured cadence and retention only.
        /// </summary>
        /// <param name="settings">The local runtime's current automatic-save preferences.</param>
        public void ProcessAutosaveTick(UserGameplaySettings settings)
        {
            if (
                _role != MultiplayerRole.Host
                || !_synchronizationReady
                || _persistence == null
                || settings?.AutosaveEnabled != true
                || settings.AutosaveIntervalTicks <= 0
                || _runtime.GetActiveGame().CurrentTick <= 0
                || _runtime.GetActiveGame().CurrentTick % settings.AutosaveIntervalTicks != 0
            )
                return;
            _hostAutosaveSettings = new UserGameplaySettings
            {
                AutosaveEnabled = settings.AutosaveEnabled,
                AutosaveIntervalTicks = settings.AutosaveIntervalTicks,
                AutosavesToKeep = settings.AutosavesToKeep,
            };
            _persistence.RequestAutosave();
        }

        /// <summary>
        /// Releases the outgoing scene's core while retaining the admitted connection and load completion.
        /// </summary>
        /// <param name="runtime">The outgoing scene's runtime owner.</param>
        public void DetachGameForSceneTransfer(GameRuntime runtime)
        {
            if (!IsTransferringScene || !ReferenceEquals(_runtime, runtime))
                return;
            _gameSession?.Dispose();
            _gameSession = null;
            _runtime?.GetActiveGameManager()?.UnbindSpeedRequests();
            _runtime?.UnbindPersistenceCoordinator();
            _runtime = null;
            _synchronizationReady = false;
        }

        /// <summary>
        /// Creates the connection-owned persistence protocol once, including across an agreed scene transfer.
        /// </summary>
        private void EnsurePersistenceSession()
        {
            if (_persistence != null)
                return;
            _persistence = new MultiplayerPersistenceSession(
                _role,
                QueueSend,
                CanCheckpointPersistence,
                PrepareHostCheckpointAsync,
                PrepareGuestCheckpointAsync,
                BeginPersistenceTransfer,
                _initializationID
            );
            _persistence.Faulted += HandleGameplayFailure;
            _persistence.CheckpointCompleted += RequestRecoveryCheckpoint;
        }

        /// <summary>
        /// Uses the existing day and action receipt boundary without traversing the scene graph.
        /// </summary>
        /// <returns>True when this participant has reached the host's reserved checkpoint position.</returns>
        private bool CanCheckpointPersistence()
        {
            return _synchronizationReady
                && _runtime?.CanSave == true
                && (
                    _role == MultiplayerRole.Host
                        ? _gameSession?.CanBeginHostTick == true
                        : _gameSession != null
                );
        }

        /// <summary>
        /// Assigns host-authenticated campaign ownership before the first authoritative snapshot is sent.
        /// </summary>
        /// <param name="game">The generated or restored campaign being admitted.</param>
        private void InitializeCampaignIdentity(GameRoot game)
        {
            MultiplayerCampaignAdmission.InitializeCampaign(
                game,
                GetAdmittedCampaignIdentity(),
                _role == MultiplayerRole.Host
            );
        }

        /// <summary>
        /// Captures account identifiers from the authenticated backend rather than peer-supplied metadata.
        /// </summary>
        /// <returns>The admitted participant and faction bindings.</returns>
        private MultiplayerCampaignIdentity GetAdmittedCampaignIdentity()
        {
            if (_launchConfiguration == null)
                throw new InvalidOperationException(
                    "The campaign does not have two authenticated participants."
                );
            return GetAdmittedCampaignIdentity(
                _launchConfiguration.HostFactionID,
                _launchConfiguration.GuestFactionID
            );
        }

        /// <summary>
        /// Binds authenticated connection accounts to an explicitly approved faction pair.
        /// </summary>
        /// <param name="hostFactionID">The host-controlled faction.</param>
        /// <param name="guestFactionID">The guest-controlled faction.</param>
        /// <returns>The admitted ownership used before and after the launch boundary.</returns>
        private MultiplayerCampaignIdentity GetAdmittedCampaignIdentity(
            string hostFactionID,
            string guestFactionID
        )
        {
            if ((_waitingForRejoin || _returningToRecovery) && _recoveryRecord != null)
            {
                if (_recoveryRecord.GetRole(_backend.LocalUserID) != _role)
                    throw new InvalidOperationException(
                        "The recovered account has a different campaign role."
                    );
                return new MultiplayerCampaignIdentity
                {
                    HostUserID = _recoveryRecord.hostUserID,
                    GuestUserID = _recoveryRecord.guestUserID,
                    HostFactionID = hostFactionID,
                    GuestFactionID = guestFactionID,
                };
            }
            string host =
                _role == MultiplayerRole.Host ? _backend?.LocalUserID : _backend?.RemoteUserID;
            string guest =
                _role == MultiplayerRole.Host ? _admittedGuestUserID : _backend?.LocalUserID;
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(guest))
                throw new InvalidOperationException(
                    "The campaign does not have two authenticated participants."
                );
            return new MultiplayerCampaignIdentity
            {
                HostUserID = host,
                GuestUserID = guest,
                HostFactionID = hostFactionID,
                GuestFactionID = guestFactionID,
            };
        }

        /// <summary>
        /// Rejects a saved multiplayer campaign belonging to another admitted account or faction binding.
        /// </summary>
        /// <param name="identity">The saved campaign ownership.</param>
        private void ValidateCampaignIdentity(MultiplayerCampaignIdentity identity)
        {
            MultiplayerCampaignAdmission.ValidateSavedCampaign(
                identity,
                GetAdmittedCampaignIdentity()
            );
        }

        /// <summary>
        /// Writes the authoritative host copy or validates the host's selected saved world and content.
        /// </summary>
        /// <param name="command">The operation reserved after both cores settled.</param>
        /// <param name="sourcePayload">The requesting guest's saved-world bytes, or null for host-selected storage.</param>
        /// <returns>The exact metadata or saved-state payload sent to the admitted guest.</returns>
        private async Task<MultiplayerPersistenceCheckpoint> PrepareHostCheckpointAsync(
            MultiplayerPersistenceCommand command,
            byte[] sourcePayload
        )
        {
            GameRuntime runtime = _runtime;
            MultiplayerPersistenceSession owner = _persistence;
            SaveGameManager store = runtime.GetSaveGameManager();
            var cancellationToken = _sessionCancellation.Token;
            if (command.load)
            {
                GameRoot game = command.guestRequest
                    ? MultiplayerSnapshotCodec.Restore(sourcePayload, command.snapshotHash)
                    : await store.LoadGameDataAsync(ResolveHostLoadFileName(command.fileName));
                ContentPack active = AppBootstrap.EnsureExists().GetContentPack();
                ContentPack content = await Task.Run(() =>
                    SaveContentResolver.Resolve(active, game.Metadata).Pack
                );
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_runtime, runtime) || !ReferenceEquals(_persistence, owner))
                    throw new OperationCanceledException(
                        "The outgoing game no longer owns this load."
                    );
                ValidateLoadedCheckpoint(content, game);
                InitializeCampaignIdentity(game);
                ConfigureGame(game);
                game.SetGameSpeed(TickSpeed.Paused);
                if (command.guestRequest)
                    await RestoreLocalCheckpointPresentationAsync(
                        game,
                        command.fileName,
                        content,
                        owner
                    );
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_runtime, runtime) || !ReferenceEquals(_persistence, owner))
                    throw new OperationCanceledException(
                        "The outgoing game no longer owns this load."
                    );
                _persistenceLoadedGame = game;
                _persistenceContent = content;
                MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(game);
                command.snapshotHash = snapshot.Hash;
                command.simulationBuild = MultiplayerCompatibility.SimulationBuild;
                command.gameplayContent = GetGameplayContentIdentity(content);
                return new MultiplayerPersistenceCheckpoint
                {
                    Command = command,
                    Payload = snapshot.Payload,
                };
            }

            GameRoot current = runtime.GetActiveGame();
            InitializeCampaignIdentity(current);
            current.Metadata.SharedSaveToken = command.token;
            Task<bool> publication;
            if (command.automatic)
            {
                publication = store.ProcessMultiplayerAutosaveTickAsync(
                    current,
                    _hostAutosaveSettings
                );
            }
            else
            {
                string fileName = SaveGameManager.GetMultiplayerSaveFileName(
                    current.Metadata.CampaignID,
                    true,
                    command.fileName
                );
                string displayName = command.displayName;
                string logicalName = command.fileName;
                if (
                    SaveGameManager.TryParseMultiplayerSaveFileName(
                        logicalName,
                        out _,
                        out _,
                        out string parsed
                    )
                )
                    logicalName = parsed;
                if (
                    logicalName == SaveGameManager.QuickSaveFileName
                    && string.IsNullOrWhiteSpace(displayName)
                )
                    displayName = "Quicksave";

                publication = runtime.SaveGameLocallyAsync(fileName, displayName);
            }
            byte[] metadataPayload = MultiplayerSnapshotCodec.Serialize(
                typeof(GameMetadata),
                current.Metadata
            );
            if (!await publication)
                throw new InvalidOperationException(
                    command.automatic
                        ? "The scheduled automatic checkpoint was not written."
                        : "The host checkpoint was not at a writable boundary."
                );
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_runtime, runtime) || !ReferenceEquals(_persistence, owner))
                throw new OperationCanceledException(
                    "The outgoing connection no longer owns this checkpoint."
                );
            return new MultiplayerPersistenceCheckpoint
            {
                Command = command,
                Payload = metadataPayload,
            };
        }

        /// <summary>
        /// Writes a manual guest copy with exact host metadata, or prepares the authoritative saved content.
        /// </summary>
        /// <param name="checkpoint">The host's reserved explicit-schema checkpoint.</param>
        /// <returns>The peer's completed durable write or load-content preflight.</returns>
        private async Task PrepareGuestCheckpointAsync(MultiplayerPersistenceCheckpoint checkpoint)
        {
            MultiplayerPersistenceSession owner = _persistence;
            MultiplayerPersistenceCommand command = checkpoint.Command;
            if (command.load)
            {
                GameRoot game = MultiplayerSnapshotCodec.Restore(
                    checkpoint.Payload,
                    command.snapshotHash
                );
                ValidateCampaignIdentity(game.Metadata?.MultiplayerCampaign);
                ContentPack active = AppBootstrap.EnsureExists().GetContentPack();
                var cancellationToken = _sessionCancellation.Token;
                ContentPack content = await Task.Run(() =>
                    SaveContentResolver.Open(active, game.Summary)
                );
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_persistence, owner))
                    throw new OperationCanceledException(
                        "The admitted connection no longer owns this load."
                    );
                ValidateLoadedCheckpoint(content, game);
                MultiplayerCompatibility.ValidateBuild(command.simulationBuild);
                MultiplayerCompatibility.ValidateContent(
                    GetGameplayContentIdentity(content),
                    command.gameplayContent
                );
                await RestoreLocalCheckpointPresentationAsync(
                    game,
                    command.fileName,
                    content,
                    owner
                );
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_persistence, owner))
                    throw new OperationCanceledException(
                        "The admitted connection no longer owns this load."
                    );
                _persistenceLoadedGame = game;
                _persistenceContent = content;
                return;
            }

            GameMetadata metadata = (GameMetadata)
                MultiplayerSnapshotCodec.Deserialize(typeof(GameMetadata), checkpoint.Payload);
            ValidateCampaignIdentity(metadata.MultiplayerCampaign);
            GameRoot current = _runtime.GetActiveGame();
            if (
                metadata.CampaignID != current.Metadata.CampaignID
                || metadata.SharedSaveToken != command.token
                || metadata.CurrentTick != current.CurrentTick
            )
                throw new InvalidDataException(
                    "The checkpoint metadata does not match the settled shared campaign."
                );
            if (command.automatic)
                current.Metadata = metadata;
            else
            {
                string fileName = SaveGameManager.GetMultiplayerSaveFileName(
                    metadata.CampaignID,
                    false,
                    command.fileName
                );
                Task publication;

                publication = _runtime
                    .GetSaveGameManager()
                    .SaveGameDataWithMetadataAsync(current, fileName, metadata);
                await publication;
            }
        }

        /// <summary>
        /// Resolves a selected participant copy to its authoritative host counterpart without trusting a path.
        /// </summary>
        /// <param name="fileName">The selected extension-free identifier.</param>
        /// <returns>The host's corresponding multiplayer copy, or the selected single-player source.</returns>
        private string ResolveHostLoadFileName(string fileName)
        {
            SaveGameManager.ValidateSaveFileName(fileName);
            if (fileName == SaveGameManager.QuickSaveFileName)
                return SaveGameManager.GetMultiplayerSaveFileName(
                    _runtime.GetActiveGame().Metadata.CampaignID,
                    true,
                    fileName
                );
            return SaveGameManager.TryParseMultiplayerSaveFileName(
                fileName,
                out string campaignID,
                out _,
                out string logicalName
            )
                ? SaveGameManager.GetMultiplayerSaveFileName(campaignID, true, logicalName)
                : fileName;
        }

        /// <summary>
        /// Overlays this participant's saved presentation only when a matching local copy exists.
        /// </summary>
        /// <param name="game">The authoritative saved world supplied by the host.</param>
        /// <param name="selectedFileName">The requested logical identifier or participant copy.</param>
        /// <param name="content">The exact verified checkpoint content.</param>
        /// <param name="owner">The admitted operation that owns this asynchronous read.</param>
        /// <returns>The completed local-copy inspection.</returns>
        private async Task RestoreLocalCheckpointPresentationAsync(
            GameRoot game,
            string selectedFileName,
            ContentPack content,
            MultiplayerPersistenceSession owner
        )
        {
            var cancellationToken = _sessionCancellation.Token;
            if (string.IsNullOrEmpty(game.Metadata.SharedSaveToken))
                return;
            SaveGameManager store = _runtime.GetSaveGameManager();
            string localFileName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                _role == MultiplayerRole.Host,
                selectedFileName
            );
            if (!File.Exists(store.GetSaveFilePath(localFileName)))
                return;
            GameRoot localSave = await store.LoadGameDataAsync(localFileName);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_persistence, owner))
                throw new OperationCanceledException(
                    "The admitted connection no longer owns this presentation read."
                );
            ValidateLoadedCheckpoint(content, localSave);
            LocalPlayerPresentationState.TryRestoreSavedCheckpoint(
                game,
                localSave,
                _launchConfiguration.LocalFactionID
            );
        }

        /// <summary>
        /// Verifies both the saved world and its persisted content metadata against the installed pack.
        /// </summary>
        /// <param name="content">The exact resolved content.</param>
        /// <param name="game">The authoritative restored world.</param>
        private static void ValidateLoadedCheckpoint(ContentPack content, GameRoot game)
        {
            if (
                !content.MatchesContentIdentity(game.Summary)
                || !content.MatchesContentIdentity(game.Metadata)
            )
                throw new InvalidDataException(
                    "The saved world and its content metadata do not match."
                );
        }

        /// <summary>
        /// Starts the same exact-content loading path on both peers after both preflights acknowledged.
        /// </summary>
        /// <param name="checkpoint">The preflighted authoritative saved state.</param>
        private void BeginPersistenceTransfer(MultiplayerPersistenceCheckpoint checkpoint)
        {
            if (_persistenceLoadedGame == null || _persistenceContent == null)
                throw new InvalidOperationException(
                    "The shared saved game has not finished preparation."
                );
            if (LoadingScreenController.IsLoadInProgress)
                throw new InvalidOperationException(
                    "Another application content transition is already active."
                );
            MultiplayerLaunchConfiguration previous = _launchConfiguration;
            _hostSummary = _persistenceLoadedGame.Summary;
            BeginInitialization(checkpoint.Command.token);
            _lobbyContent = _persistenceContent;
            _launchConfiguration = new MultiplayerLaunchConfiguration(
                _hostSummary,
                _role == MultiplayerRole.Host ? previous.HostFactionID : previous.GuestFactionID,
                previous.HostFactionID,
                previous.GuestFactionID,
                _persistenceLoadedGame
            );
            _remoteReady = false;
            _awaitingSnapshotReceipt = false;
            _pendingCommits.Clear();
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            DetachGameForSceneTransfer(_runtime);
            LoadingScreenController.StartMultiplayer(
                bootstrap,
                _persistenceContent,
                _launchConfiguration
            );
            _persistenceLoadedGame = null;
            _persistenceContent = null;
            SetStatus("Loading the shared checkpoint");
        }

        /// <summary>
        /// Cancels incomplete persistence when the admitted connection is released.
        /// </summary>
        /// <param name="interruption">The known interruption, when a shared scene transfer must recover.</param>
        private void CancelPersistence(Exception interruption = null)
        {
            bool interruptedTransfer = IsTransferringScene;
            _persistence?.Dispose();
            _persistence = null;
            _persistenceLoadedGame = null;
            _persistenceContent = null;
            _hostAutosaveSettings = null;
            if (interruptedTransfer)
                AppBootstrap.RecoverInterruptedMultiplayerLoad(
                    interruption
                        ?? new IOException(
                            "The shared load was interrupted because its connection ended."
                        )
                );
        }
    }
}
