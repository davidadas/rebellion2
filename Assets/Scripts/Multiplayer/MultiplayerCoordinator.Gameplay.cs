using System;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.PlayerRequests;
using Rebellion.Gameplay;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Connects negotiated peers to stable player-action execution and shared day barriers.
    /// </summary>
    public sealed partial class MultiplayerCoordinator
    {
        private MultiplayerGameSession _gameSession;
        private bool _awaitingSnapshotReceipt;
        private string _initialSimulationFingerprint;
        private MultiplayerSnapshotMessage _pendingRejoinSnapshot;
        private GameSession _readyPresentationSession;
        private string _readyPresentationInitializationID;

        /// <summary>
        /// Gets whether the admitted peer can receive an ordered gameplay chat message.
        /// </summary>
        public bool CanSendPeerChat =>
            _synchronizationReady && _gameSession != null && _peer?.IsConnected == true;

        /// <summary>
        /// Sends unchanged composer text through the admitted player's ordered action boundary.
        /// </summary>
        /// <param name="rawText">The unchanged multiline message text.</param>
        /// <returns>Whether the message was accepted and delivered.</returns>
        public async Task<bool> SendPeerChatAsync(string rawText)
        {
            if (!CanSendPeerChat)
                return false;
            PlayerRequestOutcome result = await ExecuteAsync(
                new SendPeerChatRequest { Text = rawText }
            );
            return result.Accepted;
        }

        /// <summary>
        /// Establishes admitted participant ownership before simulation services are constructed.
        /// </summary>
        /// <param name="game">The graph entering the declared multiplayer session.</param>
        public void PrepareGame(GameRoot game)
        {
            if (_launchConfiguration == null)
                throw new InvalidOperationException("A negotiated multiplayer launch is required.");
            ConfigureGame(game);
        }

        /// <summary>
        /// Binds execution, speed requests, and persistence before any strategy presentation starts.
        /// </summary>
        /// <param name="runtime">The active game runtime.</param>
        public void BindRuntime(GameRuntime runtime)
        {
            if (_launchConfiguration == null)
                throw new InvalidOperationException("A negotiated multiplayer launch is required.");
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _readyPresentationSession = null;
            _readyPresentationInitializationID = null;
            runtime.GetActiveGameManager().BindSpeedRequests(RequestGameSpeed);
        }

        /// <summary>
        /// Starts the synchronization handshake after the declared runtime's presentation is ready.
        /// </summary>
        /// <param name="runtime">The runtime reporting its completed presentation setup.</param>
        public void NotifyPresentationReady(GameRuntime runtime)
        {
            if (!ReferenceEquals(_runtime, runtime))
                throw new InvalidOperationException(
                    "The ready presentation belongs to another runtime."
                );
            if (
                ReferenceEquals(_readyPresentationSession, runtime.GetActiveGameSession())
                && _readyPresentationInitializationID == _initializationID
            )
                return;
            _readyPresentationSession = runtime.GetActiveGameSession();
            _readyPresentationInitializationID = _initializationID;
            runtime.GetActiveGameManager().EnableCoordinatedSpeeds();
            if (_pendingRejoinSnapshot != null)
            {
                MultiplayerSnapshotMessage metadata = _pendingRejoinSnapshot;
                _pendingRejoinSnapshot = null;
                AcceptInitialSnapshot(metadata);
                return;
            }
            if (_role == MultiplayerRole.Guest)
            {
                QueueSend(
                    new MultiplayerPacket(
                        MultiplayerMessageType.Ready,
                        JsonUtility.ToJson(
                            new MultiplayerSessionIdentity { initializationID = _initializationID }
                        )
                    )
                );
                SetStatus("Game loaded; waiting for the host state");
            }
            else
            {
                SetStatus("Game loaded; waiting for the other player");
                TrySendInitialSnapshot();
            }
        }

        /// <summary>
        /// Releases only the outgoing runtime while preserving an agreed scene-transfer operation.
        /// </summary>
        /// <param name="runtime">The runtime ending its current session.</param>
        public void ReleaseRuntime(GameRuntime runtime)
        {
            if (!ReferenceEquals(_runtime, runtime))
                return;
            if (IsTransferringScene)
                DetachGameForSceneTransfer(runtime);
            else
            {
                ReleaseGameplay();
                _runtime = null;
                _synchronizationReady = false;
            }
        }

        /// <summary>
        /// Submits a local operation to the negotiated ordering owner.
        /// </summary>
        /// <param name="action">The typed operation.</param>
        /// <returns>The completed operation, or a rejection while synchronization is unavailable.</returns>
        public Task<PlayerRequestOutcome> ExecuteAsync(PlayerRequest action)
        {
            if (!_synchronizationReady || _gameSession == null)
                return Task.FromResult(
                    new PlayerRequestOutcome(false, "Waiting for the other player.")
                );
            return _gameSession.ExecuteAsync(action);
        }

        /// <summary>
        /// Reports whether a negotiated simulation can participate in its shared day loop.
        /// </summary>
        /// <returns>True outside multiplayer or while the live peers are synchronized.</returns>
        public bool ShouldAdvanceLocalTicks()
        {
            return _launchConfiguration == null
                || _synchronizationReady
                    && _gameSession != null
                    && !_checkpointPending
                    && _persistence?.BlocksNewTicks != true;
        }

        /// <summary>
        /// Starts the next shared day when the configured host clock and both peers are ready.
        /// </summary>
        /// <param name="elapsedSeconds">The frame interval available to the host clock.</param>
        /// <returns>Whether this peer should start its ordinary incremental tick.</returns>
        public bool TryBeginGameTick(float elapsedSeconds)
        {
            if (
                !_synchronizationReady
                || _gameSession == null
                || _checkpointPending
                || _persistence?.BlocksNewTicks == true
            )
                return false;
            bool elapsed =
                _gameSession.CanBeginHostTick
                && _runtime.GetActiveGameManager().TryAdvanceTickTimer(elapsedSeconds);
            return _gameSession.TryBeginTick(elapsed);
        }

        /// <summary>
        /// Stops replay after a tick exception that occurred in the strategy frame loop.
        /// </summary>
        /// <param name="exception">The tick failure.</param>
        internal void ReportGameplayFailure(Exception exception)
        {
            if (_gameSession != null)
                _gameSession.ReportFailure(exception);
            else
                HandleGameplayFailure(exception);
        }

        /// <summary>
        /// Sends the full model once both initial presentation lifecycles are ready.
        /// </summary>
        private void TrySendInitialSnapshot()
        {
            if (
                _role != MultiplayerRole.Host
                || !_remoteReady
                || _runtime?.GetActiveGame() == null
                || !ReferenceEquals(_readyPresentationSession, _runtime.GetActiveGameSession())
                || _synchronizationReady
                || _awaitingSnapshotReceipt
                || Failure != null
            )
                return;
            if (!_runtime.GetActiveGameSession().CanCaptureSettledState)
                return;
            try
            {
                MultiplayerSnapshot snapshot = PrepareInitialSnapshot(
                    out MultiplayerSnapshotMessage metadata
                );
                QueueSend(
                    new MultiplayerPacket(
                        MultiplayerMessageType.SnapshotCommit,
                        JsonUtility.ToJson(metadata),
                        snapshot.Payload
                    )
                );
                SetStatus("Synchronizing the game");
            }
            catch (Exception exception)
            {
                HandleGameplayFailure(exception);
            }
        }

        /// <summary>
        /// Loads the selected saved world locally and prepares the ordinary initial-state receipt barrier.
        /// </summary>
        /// <param name="metadata">Receives the revision, payload hash, and loaded-state fingerprint.</param>
        /// <returns>The one world payload supplied to the other participant.</returns>
        private MultiplayerSnapshot PrepareInitialSnapshot(out MultiplayerSnapshotMessage metadata)
        {
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                _runtime.GetActiveGameSession()
            );
            GameRoot game = MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash);
            ConfigureGame(game);
            ReplaceGame(game);
            _runtime.BlockGameOperations();
            _initialSimulationFingerprint = MultiplayerStateFingerprint.ComputeSimulation(
                _runtime.GetActiveGameSession()
            );
            metadata = new MultiplayerSnapshotMessage
            {
                revision = _snapshotAdmission.NextRevision(),
                hash = snapshot.Hash,
                simulationFingerprint = _initialSimulationFingerprint,
                initializationID = _initializationID,
            };
            _lastAcceptedHash = snapshot.Hash;
            _awaitingSnapshotReceipt = true;
            return snapshot;
        }

        /// <summary>
        /// Applies the initial model once, then retains that graph throughout ordered gameplay.
        /// </summary>
        /// <param name="packet">The initial authoritative model.</param>
        private void ApplySnapshotCommit(MultiplayerPacket packet)
        {
            MultiplayerSnapshotMessage metadata = JsonUtility.FromJson<MultiplayerSnapshotMessage>(
                packet.Metadata
            );
            if (!MultiplayerSessionIdentity.Matches(_initializationID, metadata?.initializationID))
                return;
            GameRoot state = MultiplayerSnapshotCodec.Restore(packet.Payload, metadata.hash);
            ConfigureGame(state);
            ReplaceGame(state);
            AcceptInitialSnapshot(metadata);
        }

        /// <summary>
        /// Verifies a normally loaded world and acknowledges the shared initial-state barrier.
        /// </summary>
        /// <param name="metadata">The source's negotiated initial snapshot metadata.</param>
        private void AcceptInitialSnapshot(MultiplayerSnapshotMessage metadata)
        {
            metadata.simulationFingerprint = MultiplayerStateFingerprint.VerifyLoadedGame(
                _runtime.GetActiveGameSession(),
                metadata.simulationFingerprint
            );
            _snapshotAdmission.RecordRevision(metadata.revision);
            _lastAcceptedHash = metadata.hash;
            StartOrderedGameplay();
            QueueSend(
                new MultiplayerPacket(
                    MultiplayerMessageType.SnapshotReceipt,
                    JsonUtility.ToJson(metadata)
                )
            );
        }

        /// <summary>
        /// Opens ordered gameplay after the counterpart confirms the supplied initial world.
        /// </summary>
        /// <param name="packet">The counterpart's verified initial-state receipt.</param>
        private void HandleSnapshotReceipt(MultiplayerPacket packet)
        {
            if (!_awaitingSnapshotReceipt)
                return;
            MultiplayerSnapshotMessage metadata = JsonUtility.FromJson<MultiplayerSnapshotMessage>(
                packet.Metadata
            );
            if (!MultiplayerSessionIdentity.Matches(_initializationID, metadata?.initializationID))
                return;
            if (
                !_snapshotAdmission.AcceptsReceipt(
                    metadata,
                    _lastAcceptedHash,
                    _initialSimulationFingerprint
                )
            )
            {
                HandleGameplayFailure(
                    new InvalidOperationException(
                        "The initial synchronization receipt does not match."
                    )
                );
                return;
            }
            _awaitingSnapshotReceipt = false;
            StartOrderedGameplay();
        }

        /// <summary>
        /// Binds command completion and lightweight invalidation to the current stable session.
        /// </summary>
        private void StartOrderedGameplay()
        {
            _gameSession?.Dispose();
            _gameSession = new MultiplayerGameSession(
                _role,
                _launchConfiguration.HostFactionID,
                _launchConfiguration.GuestFactionID,
                _runtime.GetActiveGameSession(),
                _runtime.GetActiveGameManager(),
                QueueSend,
                _initializationID
            );
            _gameSession.StateChanged += _runtime.NotifyGameStateChanged;
            _gameSession.Faulted += HandleGameplayFailure;
            _synchronizationReady = true;
            _runtime.ResumeMultiplayerOperations();
            EnsurePersistenceSession();
            _runtime.BindPersistenceCoordinator(this);
            _persistence.NotifySynchronized();
            _waitingForRejoin = false;
            _returningToRecovery = false;
            _recoveryOfferSent = false;
            _checkpointPending = true;
            if (_backend.IsLobbyOwner)
            {
                _listingState = "started";
                _ = UpdateHostedListingAsync(_sessionCancellation.Token);
            }
            SetStatus("Connected");
        }

        /// <summary>
        /// Routes existing speed controls through the same ordered participant operation.
        /// </summary>
        /// <param name="speed">The local participant's speed choice.</param>
        private async void RequestGameSpeed(TickSpeed speed)
        {
            try
            {
                await ExecuteAsync(new SetSpeedRequest { Speed = speed });
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                HandleGameplayFailure(exception);
            }
        }

        /// <summary>
        /// Pauses a failed session and retains the complete exception for diagnosis.
        /// </summary>
        /// <param name="exception">The failure that prevents safe continued replay.</param>
        private void HandleGameplayFailure(Exception exception)
        {
            StopGameplay(exception, true);
        }

        /// <summary>
        /// Stops both initialization and ordered gameplay without echoing a received failure indefinitely.
        /// </summary>
        /// <param name="exception">The failure that prevents safe continuation.</param>
        /// <param name="notifyPeer">Whether the remote participant must also be stopped.</param>
        private void StopGameplay(Exception exception, bool notifyPeer)
        {
            if (!_synchronizationReady && ReferenceEquals(Failure, exception))
                return;
            _synchronizationReady = false;
            Failure = exception;
            if (_gameSession != null)
                _gameSession.ReportFailure(exception);
            else if (
                notifyPeer
                && !string.IsNullOrEmpty(_initializationID)
                && _peer?.IsConnected == true
            )
                QueueSend(
                    new MultiplayerPacket(
                        MultiplayerMessageType.SynchronizationFailed,
                        JsonUtility.ToJson(
                            new MultiplayerSessionIdentity
                            {
                                initializationID = _initializationID,
                                reason = exception.Message,
                            }
                        )
                    )
                );
            if (IsTransferringScene)
                CancelPersistence(exception);
            else
                _persistence?.ReportFailure(exception);
            _runtime?.BlockGameOperations();
            _runtime?.GetActiveGameManager()?.ApplyGameSpeed(TickSpeed.Paused);
            Debug.LogException(exception);
            SetStatus("Game synchronization stopped.");
        }

        /// <summary>
        /// Releases connection-owned callbacks and pending operation completions.
        /// </summary>
        private void ReleaseGameplay()
        {
            CancelPersistence();
            _runtime?.UnbindPersistenceCoordinator();
            _gameSession?.Dispose();
            _gameSession = null;
            _awaitingSnapshotReceipt = false;
            _initialSimulationFingerprint = null;
            _pendingRejoinSnapshot = null;
            _readyPresentationSession = null;
            _readyPresentationInitializationID = null;
            _runtime?.GetActiveGameManager()?.UnbindSpeedRequests();
        }
    }
}
