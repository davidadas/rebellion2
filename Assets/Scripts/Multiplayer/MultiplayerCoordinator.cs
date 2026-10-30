using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.App;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Owns online discovery, the peer connection, launch negotiation, and game-state ordering.
    /// </summary>
    public sealed partial class MultiplayerCoordinator : MonoBehaviour, IGameSessionOwner
    {
        private const int _protocolVersion = 11;

        private readonly List<MatchmakerGame> _games = new();
        private readonly MultiplayerSnapshotBuffer _pendingCommits = new();
        private readonly MultiplayerSnapshotAdmission _snapshotAdmission = new();
        private EpicOnlineServicesClient _onlineClient;
        private EpicMultiplayerBackend _backend;
        private IMultiplayerPeer _peer;
        private CancellationTokenSource _sessionCancellation = new();
        private Task _leaveTask = Task.CompletedTask;
        private int _browseVersion;
        private MultiplayerLaunchConfiguration _launchConfiguration;
        private GameRuntime _runtime;
        private GameSummary _hostSummary;
        private ContentPack _identityContent;
        private string _gameplayContentIdentity;
        private MultiplayerRole _role;
        private string _playerName;
        private string _hostPlayerName;
        private string _gameName;
        private string _listingState = "waiting";
        private string _status = "Offline";
        private string _lastAcceptedHash;
        private string _initializationID => _snapshotAdmission.InitializationID;
        private bool _remoteReady;
        private bool _synchronizationReady;
        private bool _previousRunInBackground;
        private bool _backgroundOwned;
        private bool _browserOpen;
        private bool _disposed;

        /// <summary>
        /// Gets the application-lifetime multiplayer coordinator.
        /// </summary>
        public static MultiplayerCoordinator Instance { get; private set; }

        /// <summary>
        /// Gets the terminal notice retained until acknowledgment, independently of connection teardown.
        /// </summary>
        public MultiplayerSessionEndNotice SessionEndNotice { get; } = new();

        /// <summary>
        /// Gets the local multiplayer role.
        /// </summary>
        public MultiplayerRole Role => _role;

        /// <summary>
        /// Gets whether a multiplayer launch has been negotiated.
        /// </summary>
        public bool IsMultiplayerGame => _launchConfiguration != null;

        /// <summary>
        /// Gets the last connection or content failure for the form that initiated it.
        /// </summary>
        public Exception Failure { get; private set; }

        /// <summary>
        /// Gets whether a cancellable room connection is still being negotiated.
        /// </summary>
        public bool IsConnecting =>
            _launchConfiguration == null
            && (
                _role == MultiplayerRole.Host && _peer == null
                || _role == MultiplayerRole.Guest && _guestLobby == null
            );

        /// <summary>
        /// Gets whether the local peer owns simulation ticks.
        /// </summary>
        public bool IsHost => _role == MultiplayerRole.Host;

        /// <summary>
        /// Gets whether ordered synchronization is ready for gameplay.
        /// </summary>
        public bool IsSynchronized => _synchronizationReady;

        /// <summary>
        /// Gets the current connection status for presentation.
        /// </summary>
        public string Status => _status;

        public StorePlayerSession PlayerSession => _onlineClient?.Session;

        public bool IsSigningIn { get; private set; }
        public bool IsSearchingGames { get; private set; }

        /// <summary>
        /// Gets the most recently discovered live games.
        /// </summary>
        public IReadOnlyList<MatchmakerGame> Games => _games;

        /// <summary>
        /// Raised when a negotiated game should leave the main menu.
        /// </summary>
        public event Action<MultiplayerLaunchConfiguration> LaunchRequested;

        /// <summary>
        /// Raised when connection or matchmaking presentation changes.
        /// </summary>
        public event Action StateChanged;

        /// <summary>
        /// Authenticates a store account before hosting or browsing multiplayer games.
        /// </summary>
        /// <param name="provider">The Steam or GOG provider identifier.</param>
        /// <returns>The pending store and online identity sign-in.</returns>
        public async Task SignInAsync(string provider)
        {
            if (IsSigningIn)
                return;
            Disconnect();
            IsSigningIn = true;
            SetStatus("Signing in with " + (provider == "steam" ? "Steam" : "GOG"));
            try
            {
                StorePlayerSession session = await _onlineClient.SignInAsync(provider);
                SetStatus("Signed in as " + session.DisplayName);
            }
            catch (Exception exception)
            {
                Failure = exception;
                SetStatus(exception.Message);
            }
            finally
            {
                IsSigningIn = false;
                StateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Returns the existing coordinator or creates it on the persistent application object.
        /// </summary>
        /// <returns>The application-lifetime coordinator.</returns>
        public static MultiplayerCoordinator EnsureExists()
        {
            if (Instance != null)
                return Instance;

            return new GameObject("Multiplayer session").AddComponent<MultiplayerCoordinator>();
        }

        /// <summary>
        /// Clears a dismissed form failure before starting another operation.
        /// </summary>
        public void ClearFailure()
        {
            Failure = null;
        }

        /// <summary>
        /// Claims the singleton and prepares lazy online authentication.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
            _onlineClient = new EpicOnlineServicesClient(gameObject);
        }

        /// <summary>
        /// Processes queued network input and stable game-state synchronization on the main thread.
        /// </summary>
        private void Update()
        {
            _backend?.Poll();
            ProcessTransportEvents();
            ProcessPendingCommit();
            if (_persistence?.BlocksGameplay != true)
                _gameSession?.Pump();
            _persistence?.Pump();
            ProcessRejoinRecovery();
            TrySendInitialSnapshot();
        }

        /// <summary>
        /// Releases sockets, directory resources, and the singleton claim.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance != this)
                return;

            _browserOpen = false;
            Disconnect();
            _disposed = true;
            _sessionCancellation.Dispose();
            _onlineClient?.Dispose();
            _onlineClient = null;
            Instance = null;
        }

        /// <summary>
        /// Authenticates the host and publishes a named two-player EOS lobby.
        /// </summary>
        /// <param name="playerName">The host display name.</param>
        /// <param name="gameName">The advertised game name.</param>
        /// <param name="summary">The selected launch settings.</param>
        /// <param name="password">The optional private-room password.</param>
        /// <returns>The pending lobby setup.</returns>
        public async Task HostAsync(
            string playerName,
            string gameName,
            GameSummary summary,
            string password = null
        )
        {
            CancellationToken cancellationToken = BeginSession(MultiplayerRole.Host);
            try
            {
                _playerName = NormalizePlayerName(playerName);
                _hostPlayerName = _playerName;
                _gameName = NormalizeGameName(gameName, _playerName);
                _listingState = "waiting";
                _hostSummary = MultiplayerSnapshotCodec.DeserializeSummary(
                    MultiplayerSnapshotCodec.SerializeSummary(
                        summary ?? throw new ArgumentNullException(nameof(summary))
                    )
                );
                ValidatePlayableFactions(_hostSummary);
                _password = password ?? string.Empty;
                _lobbyContent = SaveContentResolver.Open(
                    AppBootstrap.EnsureExists().GetContentPack(),
                    _hostSummary
                );
                SetStatus("Signing in for multiplayer");
                await _leaveTask;
                cancellationToken.ThrowIfCancellationRequested();
                EpicMultiplayerBackend backend = await _onlineClient.ConnectAsync();
                cancellationToken.ThrowIfCancellationRequested();
                _backend = backend;
                _playerName = NormalizePlayerName(_onlineClient.Session.DisplayName);
                _hostPlayerName = _playerName;
                _lobby = new MultiplayerLobby(_gameName, _hostPlayerName, _hostSummary);
                cancellationToken.ThrowIfCancellationRequested();
                IMultiplayerPeer peer = await _backend.HostAsync(
                    CreateListing(),
                    cancellationToken
                );
                cancellationToken.ThrowIfCancellationRequested();
                _peer = peer;
                SetStatus($"Listed as '{_gameName}'; waiting for a player");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                Disconnect();
                Failure = exception;
                SetStatus($"Host failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Joins the selected EOS lobby and negotiates its peer connection.
        /// </summary>
        /// <param name="playerName">The guest display name.</param>
        /// <param name="game">The selected game listing.</param>
        /// <param name="password">The password entered for a protected game.</param>
        /// <returns>The pending lobby join.</returns>
        public async Task JoinAsync(string playerName, MatchmakerGame game, string password = null)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            CancellationToken cancellationToken = BeginSession(MultiplayerRole.Guest);
            try
            {
                _playerName = NormalizePlayerName(playerName);
                _passwordProof = MultiplayerLobbyPassword.CreateProof(game.id, password);
                _expectedFingerprint = game.contentFingerprint;
                SetStatus($"Joining '{game.name}'");
                await _leaveTask;
                cancellationToken.ThrowIfCancellationRequested();
                EpicMultiplayerBackend backend = await _onlineClient.ConnectAsync();
                cancellationToken.ThrowIfCancellationRequested();
                _backend = backend;
                _playerName = NormalizePlayerName(_onlineClient.Session.DisplayName);
                cancellationToken.ThrowIfCancellationRequested();
                IMultiplayerPeer peer = await _backend.JoinAsync(game, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _peer = peer;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                Disconnect();
                Failure = exception;
                SetStatus($"Connection failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Refreshes public EOS games for the browser.
        /// </summary>
        /// <returns>The pending lookup.</returns>
        public async Task RefreshGamesAsync()
        {
            CancellationToken cancellationToken = _sessionCancellation.Token;
            int browseVersion = ++_browseVersion;
            IsSearchingGames = true;
            Failure = null;
            try
            {
                SetStatus("Finding online games");
                EpicMultiplayerBackend backend = await _onlineClient.ConnectAsync();
                cancellationToken.ThrowIfCancellationRequested();
                _backend = backend;
                RefreshRecoveryRecord();
                IReadOnlyList<MatchmakerGame> games = await _backend.ListAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (browseVersion != _browseVersion)
                    return;
                _games.Clear();
                _games.AddRange(games);
                SetStatus($"Found {_games.Count} game{(_games.Count == 1 ? string.Empty : "s")}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested && browseVersion == _browseVersion)
                {
                    Failure = exception;
                    SetStatus($"Game search failed: {exception.Message}");
                }
            }
            finally
            {
                if (browseVersion == _browseVersion)
                {
                    IsSearchingGames = false;
                    StateChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Determines whether a directory listing uses the same content identity as local settings.
        /// </summary>
        /// <param name="game">The directory listing to compare.</param>
        /// <param name="summary">The local launch settings.</param>
        /// <returns>True when the game can pass the content handshake.</returns>
        public bool IsCompatible(MatchmakerGame game, GameSummary summary)
        {
            return game != null
                && string.Equals(
                    game.transport,
                    EpicLobbyListing.Transport,
                    StringComparison.Ordinal
                )
                && string.Equals(
                    game.contentFingerprint,
                    EpicLobbyListing.HashContent(CreateContentFingerprint(summary)),
                    StringComparison.Ordinal
                );
        }

        /// <summary>
        /// Closes the peer connection and removes any hosted directory listing.
        /// </summary>
        public void Disconnect()
        {
            if (_disposed)
                return;
            if (!_browserOpen)
                ReleaseBackgroundProcessing();
            _sessionCancellation.Cancel();
            _sessionCancellation.Dispose();
            _sessionCancellation = new CancellationTokenSource();
            _browseVersion++;
            IsSearchingGames = false;
            _leaveTask = LeaveLobbyAsync();
            _peer?.Dispose();
            _peer = null;
            ReleaseGameplay();
            ResetRecovery();
            _runtime = null;
            _hostSummary = null;
            _launchConfiguration = null;
            Failure = null;
            _pendingCommits.Clear();
            _role = MultiplayerRole.None;
            _gameName = null;
            _listingState = "waiting";
            _snapshotAdmission.Clear();
            _lastAcceptedHash = null;
            _remoteReady = false;
            _synchronizationReady = false;
            ResetLobby();
            SetStatus("Offline");
        }

        /// <summary>
        /// Configures generated or restored state with both human participants and the local view.
        /// </summary>
        /// <param name="game">The game state to configure.</param>
        public void ConfigureGame(GameRoot game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (_launchConfiguration == null)
                return;

            MultiplayerCampaignAdmission.ConfigureGame(
                game,
                GetAdmittedCampaignIdentity(),
                _launchConfiguration,
                _role == MultiplayerRole.Host,
                _hostPlayerName,
                _role == MultiplayerRole.Guest
                    ? _playerName
                    : _lobby?.State.guestName ?? _recoveryRecord?.guestName
            );
        }

        /// <summary>
        /// Drains background transport events on Unity's main thread.
        /// </summary>
        private void ProcessTransportEvents()
        {
            while (_peer?.TryDequeue(out MultiplayerTransportEvent transportEvent) == true)
            {
                switch (transportEvent.Type)
                {
                    case MultiplayerTransportEventType.Connected:
                        HandleConnected();
                        break;
                    case MultiplayerTransportEventType.PacketReceived:
                        HandlePacket(transportEvent.Packet);
                        break;
                    case MultiplayerTransportEventType.Disconnected:
                        _synchronizationReady = false;
                        HandlePeerDeparture(transportEvent.Error);
                        break;
                }
            }
        }

        /// <summary>
        /// Starts the role-specific handshake after a socket connects.
        /// </summary>
        private void HandleConnected()
        {
            if (_waitingForRejoin && _backend.IsLobbyOwner)
            {
                SetStatus("Player connected; verifying the returning account");
                return;
            }
            if (_backend.IsLobbyOwner)
            {
                SetStatus("Player connected; negotiating game");
                return;
            }

            MultiplayerHelloMessage hello = new MultiplayerHelloMessage
            {
                protocolVersion = _protocolVersion,
                simulationBuild = MultiplayerCompatibility.SimulationBuild,
                playerName = _playerName,
                contentFingerprint = _expectedFingerprint,
                passwordProof = _passwordProof,
                recoveryCampaignID =
                    _returningToRecovery || _waitingForRejoin ? _recoveryRecord?.campaignID : null,
            };
            QueueSend(
                new MultiplayerPacket(MultiplayerMessageType.Hello, JsonUtility.ToJson(hello))
            );
        }

        /// <summary>
        /// Dispatches one decoded peer packet.
        /// </summary>
        /// <param name="packet">The received packet.</param>
        private void HandlePacket(MultiplayerPacket packet)
        {
            try
            {
                switch (packet.Type)
                {
                    case MultiplayerMessageType.Hello:
                        HandleHello(packet);
                        break;
                    case MultiplayerMessageType.Welcome:
                        HandleWelcome(packet);
                        break;
                    case MultiplayerMessageType.RecoveryWelcome:
                        HandleRecoveryWelcome(packet);
                        break;
                    case MultiplayerMessageType.Ready:
                        HandleReady(packet);
                        break;
                    case MultiplayerMessageType.SnapshotCommit:
                        HandleSnapshotCommit(packet);
                        break;
                    case MultiplayerMessageType.SnapshotReceipt:
                        HandleSnapshotReceipt(packet);
                        break;
                    case MultiplayerMessageType.PersistenceRequest:
                    case MultiplayerMessageType.PersistencePrepare:
                    case MultiplayerMessageType.PersistenceReady:
                    case MultiplayerMessageType.PersistenceCommit:
                    case MultiplayerMessageType.PersistenceReceipt:
                    case MultiplayerMessageType.PersistenceComplete:
                    case MultiplayerMessageType.PersistenceTransfer:
                    case MultiplayerMessageType.PersistenceFailed:
                        if (_persistence == null)
                        {
                            if (_waitingForRejoin)
                                break;
                            throw new InvalidOperationException(
                                "Shared persistence has not started."
                            );
                        }
                        _persistence.Receive(packet);
                        break;
                    case MultiplayerMessageType.PlayerActionRequest:
                    case MultiplayerMessageType.PlayerActionCommit:
                    case MultiplayerMessageType.PlayerActionReceipt:
                    case MultiplayerMessageType.PlayerActionRejected:
                    case MultiplayerMessageType.AdvanceDay:
                    case MultiplayerMessageType.DayReceipt:
                    case MultiplayerMessageType.SynchronizationFailed:
                        MultiplayerSessionIdentity gameplayIdentity =
                            JsonUtility.FromJson<MultiplayerSessionIdentity>(packet.Metadata);
                        if (
                            !MultiplayerSessionIdentity.Matches(
                                _initializationID,
                                gameplayIdentity?.initializationID
                            )
                        )
                            break;
                        if (_gameSession == null)
                        {
                            if (packet.Type == MultiplayerMessageType.SynchronizationFailed)
                            {
                                StopGameplay(
                                    new InvalidOperationException(gameplayIdentity.reason),
                                    false
                                );
                                break;
                            }
                            throw new InvalidOperationException(
                                "Gameplay synchronization has not started."
                            );
                        }
                        _gameSession.Receive(packet);
                        break;
                    case MultiplayerMessageType.Disconnect:
                        HandlePeerDeparture("The other player ended the session");
                        break;
                    case MultiplayerMessageType.LobbyState:
                        HandleLobbyState(packet);
                        break;
                    case MultiplayerMessageType.SelectFaction:
                    case MultiplayerMessageType.LobbyReady:
                    case MultiplayerMessageType.LobbyContentReady:
                        HandleLobbyCommand(packet);
                        break;
                    case MultiplayerMessageType.LobbyChat:
                        HandleLobbyChat(packet);
                        break;
                    case MultiplayerMessageType.StartGame:
                        HandleStartGame();
                        break;
                    case MultiplayerMessageType.AdmissionRejectedAck:
                        if (_backend.IsLobbyOwner && _rejectingGuest)
                            RejectGuest();
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unexpected packet type {packet.Type}."
                        );
                }
            }
            catch (Exception exception)
            {
                SetStatus($"Network state rejected: {exception.Message}");
            }
        }

        /// <summary>
        /// Validates a guest handshake and sends the authoritative launch settings.
        /// </summary>
        /// <param name="packet">The hello packet.</param>
        private void HandleHello(MultiplayerPacket packet)
        {
            if (_rejectingGuest)
                return;
            if (_waitingForRejoin)
            {
                HandleRecoveryHello(packet);
                return;
            }
            if (_role != MultiplayerRole.Host || _hostSummary == null)
                throw new InvalidOperationException("Only a waiting host can accept a player.");

            MultiplayerHelloMessage hello = JsonUtility.FromJson<MultiplayerHelloMessage>(
                packet.Metadata
            );
            string error = null;
            if (hello == null || hello.protocolVersion != _protocolVersion)
                error = "The multiplayer protocol versions do not match.";
            else if (
                MultiplayerCompatibility.GetBuildError(hello.simulationBuild) is string buildError
            )
                error = buildError;
            else if (
                !string.Equals(
                    hello.contentFingerprint,
                    EpicLobbyListing.HashContent(CreateContentFingerprint(_hostSummary)),
                    StringComparison.Ordinal
                )
            )
                error = "The content pack, scenario, or enabled mods do not match.";
            else if (
                !string.Equals(
                    hello.passwordProof ?? string.Empty,
                    MultiplayerLobbyPassword.CreateProof(_backend.LobbyId, _password),
                    StringComparison.Ordinal
                )
            )
                error = "The game password is incorrect.";
            else if (
                MultiplayerCampaignAdmission.GetParticipantError(
                    _restoredGame?.Metadata?.MultiplayerCampaign,
                    _backend.LocalUserID,
                    _backend.RemoteUserID,
                    true
                )
                is string participantError
            )
                error = participantError;
            else if (_lobby?.State.guestName != null || _launchConfiguration != null)
                error = "The game is no longer accepting players.";

            string hostFaction = _hostSummary.PlayerFactionID;
            string guestFaction = _hostSummary.StartingFactionIDs.First(id => id != hostFaction);
            MultiplayerWelcomeMessage welcome = new MultiplayerWelcomeMessage
            {
                accepted = error == null,
                simulationBuild = MultiplayerCompatibility.SimulationBuild,
                gameplayContent = GetGameplayContentIdentity(_lobbyContent),
                error = error,
                hostPlayerName = _hostPlayerName,
                hostFactionID = hostFaction,
                guestFactionID = guestFaction,
                chat = _chat.ToArray(),
                initializationID = _initializationID,
            };
            QueueSend(
                new MultiplayerPacket(
                    MultiplayerMessageType.Welcome,
                    JsonUtility.ToJson(welcome),
                    error == null
                        ? MultiplayerSnapshotCodec.SerializeSummary(_hostSummary)
                        : Array.Empty<byte>()
                )
            );
            if (error != null)
            {
                SetStatus($"Player rejected: {error}");
                _rejectingGuest = true;
                return;
            }

            MarkListingFull();
            _admittedGuestUserID = _backend.RemoteUserID;
            _lobby.Join(NormalizePlayerName(hello.playerName));
            PublishLobby();
            AddLobbyNotice(_lobby.State.guestName + " joined the lobby.");
            SetStatus(string.Empty);
        }

        /// <summary>
        /// Accepts the host's launch settings and requests the strategy scene.
        /// </summary>
        /// <param name="packet">The welcome packet.</param>
        private void HandleWelcome(MultiplayerPacket packet)
        {
            if (_role != MultiplayerRole.Guest)
                throw new InvalidOperationException("Only a guest can receive a welcome packet.");
            MultiplayerWelcomeMessage welcome = JsonUtility.FromJson<MultiplayerWelcomeMessage>(
                packet.Metadata
            );
            if (welcome?.accepted != true)
            {
                FinishRejectedJoin(welcome?.error ?? "The host rejected the join.");
                return;
            }

            MultiplayerCompatibility.ValidateBuild(welcome.simulationBuild);
            GameSummary summary = MultiplayerSnapshotCodec.DeserializeSummary(packet.Payload);
            if (
                EpicLobbyListing.HashContent(CreateContentFingerprint(summary))
                != _expectedFingerprint
            )
                throw new InvalidOperationException(
                    "The host changed content. Refresh and join the game again."
                );
            _hostPlayerName = NormalizePlayerName(welcome.hostPlayerName);
            _hostSummary = summary;
            _chat.Clear();
            _chat.AddRange(welcome.chat ?? Array.Empty<MultiplayerChatMessage>());
            SetStatus($"Connected to {_hostPlayerName}; preparing the lobby");
        }

        /// <summary>
        /// Records that the guest finished deterministic game generation.
        /// </summary>
        /// <param name="packet">The readiness message for the current initialization.</param>
        private void HandleReady(MultiplayerPacket packet)
        {
            if (_role != MultiplayerRole.Host)
                return;
            MultiplayerSessionIdentity identity = JsonUtility.FromJson<MultiplayerSessionIdentity>(
                packet.Metadata
            );
            if (!MultiplayerSessionIdentity.Matches(_initializationID, identity?.initializationID))
                return;
            _remoteReady = true;
            TrySendInitialSnapshot();
        }

        /// <summary>
        /// Retains the newest complete authoritative state without rebuilding presentation inside the receive loop.
        /// </summary>
        /// <param name="packet">The committed snapshot packet.</param>
        private void HandleSnapshotCommit(MultiplayerPacket packet)
        {
            if (_role != MultiplayerRole.Guest || _gameSession != null)
                return;
            MultiplayerSnapshotMessage metadata = JsonUtility.FromJson<MultiplayerSnapshotMessage>(
                packet.Metadata
            );
            if (
                metadata == null
                || !_snapshotAdmission.Accepts(metadata.initializationID, metadata.revision)
            )
                return;

            _pendingCommits.Enqueue(packet, metadata.revision);
        }

        /// <summary>
        /// Applies at most one complete host state per frame, after all received revisions have been inspected.
        /// </summary>
        private void ProcessPendingCommit()
        {
            if (
                _role != MultiplayerRole.Guest
                || _runtime == null
                || !ReferenceEquals(_readyPresentationSession, _runtime.GetActiveGameSession())
            )
                return;
            MultiplayerPacket packet = _pendingCommits.Take();
            if (packet == null)
                return;
            try
            {
                ApplySnapshotCommit(packet);
            }
            catch (Exception exception)
            {
                HandleGameplayFailure(exception);
            }
        }

        /// <summary>
        /// Adopts snapshot ordering for a new initialization and discards its predecessor's pending state.
        /// </summary>
        /// <param name="initializationID">The identity agreed for the initialized game.</param>
        private void BeginInitialization(string initializationID)
        {
            if (_snapshotAdmission.Begin(initializationID))
                _pendingCommits.Clear();
        }

        /// <summary>
        /// Sends a packet and reports asynchronous socket failures as connection status.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        private async void QueueSend(MultiplayerPacket packet)
        {
            CancellationToken cancellationToken = _sessionCancellation.Token;
            IMultiplayerPeer sendingPeer = _peer;
            try
            {
                await sendingPeer.SendAsync(packet);
            }
            catch (Exception exception)
            {
                if (
                    cancellationToken.IsCancellationRequested
                    || !ReferenceEquals(sendingPeer, _peer)
                    || sendingPeer?.IsConnected != true
                )
                    return;
                if (_runtime != null)
                    HandleGameplayFailure(exception);
                else
                    SetStatus($"Send failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Removes lobby membership without blocking synchronous menu teardown.
        /// </summary>
        /// <returns>The pending lobby cleanup.</returns>
        private async Task LeaveLobbyAsync()
        {
            if (_backend == null)
                return;
            try
            {
                await _backend.LeaveAsync(_launchConfiguration != null);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Multiplayer lobby cleanup failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Marks the accepted two-player room as full in its discovery metadata.
        /// </summary>
        private void MarkListingFull()
        {
            _listingState = "full";
            _ = UpdateHostedListingAsync(_sessionCancellation.Token);
        }

        /// <summary>
        /// Publishes changed host state once, without a directory heartbeat.
        /// </summary>
        /// <param name="cancellationToken">Identifies the session that owns the update.</param>
        /// <returns>The pending lobby update.</returns>
        private async Task UpdateHostedListingAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _backend.UpdateListingAsync(CreateListing());
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                    Debug.LogWarning($"Multiplayer listing update failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Creates the advertised metadata for the current host settings.
        /// </summary>
        /// <returns>The host's game listing.</returns>
        private MatchmakerGame CreateListing()
        {
            return new MatchmakerGame
            {
                name = _gameName,
                transport = EpicLobbyListing.Transport,
                maxPlayers = 2,
                state = _listingState,
                hostFaction = _hostSummary?.PlayerFactionID,
                contentFingerprint = CreateContentFingerprint(_hostSummary),
                packID = _hostSummary?.PackID,
                packName = _lobbyContent?.Definition.DisplayName,
                packVersion = _hostSummary?.PackVersion,
                scenarioID = _hostSummary?.ScenarioID,
                passwordProtected = !string.IsNullOrEmpty(_password),
                campaignID =
                    _runtime?.GetActiveGame()?.Metadata?.CampaignID
                    ?? (
                        _waitingForRejoin || _returningToRecovery
                            ? _recoveryRecord?.campaignID
                            : null
                    ),
            };
        }

        /// <summary>
        /// Produces a stable content identity used by the connection handshake and browser.
        /// </summary>
        /// <param name="summary">The active launch summary.</param>
        /// <returns>The stable identity string.</returns>
        private static string CreateContentFingerprint(GameSummary summary)
        {
            if (summary == null)
                return string.Empty;
            string mods = string.Join(
                ",",
                (summary.ModIDs ?? Array.Empty<string>()).Select(
                    (id, index) =>
                        $"{id}@{(index < (summary.ModVersions?.Length ?? 0) ? summary.ModVersions[index] : "?")}"
                )
            );
            return $"{summary.PackID}|{summary.PackVersion}|{summary.ScenarioID}|{mods}";
        }

        /// <summary>
        /// Computes admission identity once per fully loaded content selection.
        /// Runtime custom settings are checked separately as shared simulation state.
        /// </summary>
        /// <param name="content">The loaded selection used by simulation factories.</param>
        /// <returns>The actual gameplay-data identity.</returns>
        private string GetGameplayContentIdentity(ContentPack content)
        {
            if (content == null)
                throw new InvalidOperationException("Gameplay content has not finished loading.");
            if (!ReferenceEquals(_identityContent, content))
            {
                _gameplayContentIdentity = GameplayContentIdentity.Compute(content.GameData);
                _identityContent = content;
            }
            return _gameplayContentIdentity;
        }

        /// <summary>
        /// Verifies that a launch has distinct host and guest factions.
        /// </summary>
        /// <param name="summary">The launch settings to validate.</param>
        private static void ValidatePlayableFactions(GameSummary summary)
        {
            string[] factions = summary.StartingFactionIDs ?? Array.Empty<string>();
            if (
                string.IsNullOrWhiteSpace(summary.PlayerFactionID)
                || factions.Count(id => !string.IsNullOrWhiteSpace(id)) < 2
                || !factions.Contains(summary.PlayerFactionID)
                || !factions.Any(id => id != summary.PlayerFactionID)
            )
                throw new InvalidOperationException(
                    "Multiplayer requires two distinct playable factions."
                );
        }

        /// <summary>
        /// Normalizes an optional player name for status and game listings.
        /// </summary>
        /// <param name="playerName">The requested display name.</param>
        /// <returns>A non-empty bounded name.</returns>
        private static string NormalizePlayerName(string playerName)
        {
            string value = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            return value.Length <= 32 ? value : value.Substring(0, 32);
        }

        /// <summary>
        /// Normalizes an optional directory game name and falls back to the host identity.
        /// </summary>
        /// <param name="gameName">The requested game name.</param>
        /// <param name="playerName">The normalized host display name.</param>
        /// <returns>A non-empty bounded game name.</returns>
        private static string NormalizeGameName(string gameName, string playerName)
        {
            string value = string.IsNullOrWhiteSpace(gameName)
                ? $"{playerName}'s Game"
                : gameName.Trim();
            return value.Length <= 64 ? value : value.Substring(0, 64);
        }

        /// <summary>
        /// Updates connection status and notifies presentation listeners.
        /// </summary>
        /// <param name="status">The new status.</param>
        private void SetStatus(string status)
        {
            _status = status ?? string.Empty;
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Starts one session scope and keeps networking responsive when the player window loses focus.
        /// </summary>
        /// <param name="role">The local participant's role.</param>
        /// <returns>The cancellation token owned by this session.</returns>
        private CancellationToken BeginSession(MultiplayerRole role)
        {
            Disconnect();
            RetainBackgroundProcessing();
            _role = role;
            return _sessionCancellation.Token;
        }

        /// <summary>
        /// Keeps authentication, discovery, and chat responsive while the console loses focus.
        /// </summary>
        public void OpenBrowser()
        {
            _browserOpen = true;
            RetainBackgroundProcessing();
        }

        /// <summary>
        /// Releases browser ownership while retaining background networking for an active session.
        /// </summary>
        public async void CloseBrowser()
        {
            _browserOpen = false;
            try
            {
                if (_chatBackend != null)
                    await _chatBackend.CloseGeneralChatAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("General chat cleanup failed: " + exception.Message);
            }
            finally
            {
                if (!_browserOpen && _role == MultiplayerRole.None)
                    ReleaseBackgroundProcessing();
            }
        }

        /// <summary>
        /// Captures the application's background preference once for the complete online lifecycle.
        /// </summary>
        private void RetainBackgroundProcessing()
        {
            if (_backgroundOwned)
                return;
            _previousRunInBackground = Application.runInBackground;
            _backgroundOwned = true;
            Application.runInBackground = true;
        }

        /// <summary>
        /// Restores the preference captured before opening online play.
        /// </summary>
        private void ReleaseBackgroundProcessing()
        {
            if (!_backgroundOwned)
                return;
            Application.runInBackground = _previousRunInBackground;
            _backgroundOwned = false;
        }
    }
}
