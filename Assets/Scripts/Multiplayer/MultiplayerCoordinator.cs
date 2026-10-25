using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Rebellion.Game;
using UnityEngine;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Owns matchmaking, the direct peer connection, launch negotiation, and game-state ordering.
    /// </summary>
    public sealed class MultiplayerCoordinator : MonoBehaviour
    {
        private const int _protocolVersion = 1;
        private const float _snapshotIntervalSeconds = 0.2f;
        private const float _heartbeatIntervalSeconds = 20f;
        private const string _hostPlayerID = "MULTIPLAYER_HOST";
        private const string _guestPlayerID = "MULTIPLAYER_GUEST";

        private readonly List<MatchmakerGame> _games = new();
        private MatchmakerClient _matchmaker;
        private MultiplayerPeer _peer;
        private MultiplayerLaunchConfiguration _launchConfiguration;
        private GameRuntime _runtime;
        private GameSummary _hostSummary;
        private MultiplayerPacket _pendingProposal;
        private MultiplayerRole _role;
        private string _playerName;
        private string _hostPlayerName;
        private string _matchmakerUrl;
        private string _listingID;
        private int _hostPort;
        private string _status = "Offline";
        private string _lastAcceptedHash;
        private long _revision;
        private float _nextSnapshotTime;
        private float _nextHeartbeatTime;
        private bool _remoteReady;
        private bool _synchronizationReady;
        private bool _proposalPending;
        private bool _heartbeatPending;

        /// <summary>
        /// Gets the application-lifetime multiplayer coordinator.
        /// </summary>
        public static MultiplayerCoordinator Instance { get; private set; }

        /// <summary>
        /// Gets the local multiplayer role.
        /// </summary>
        public MultiplayerRole Role => _role;

        /// <summary>
        /// Gets whether a multiplayer launch has been negotiated.
        /// </summary>
        public bool IsMultiplayerGame => _launchConfiguration != null;

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
        /// Returns the existing coordinator or creates it on the persistent application object.
        /// </summary>
        /// <returns>The application-lifetime coordinator.</returns>
        public static MultiplayerCoordinator EnsureExists()
        {
            if (Instance != null)
                return Instance;

            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            MultiplayerCoordinator coordinator = bootstrap.GetComponent<MultiplayerCoordinator>();
            return coordinator != null
                ? coordinator
                : bootstrap.gameObject.AddComponent<MultiplayerCoordinator>();
        }

        /// <summary>
        /// Claims the singleton and prepares the directory client.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _matchmaker = new MatchmakerClient();
        }

        /// <summary>
        /// Processes queued network input and stable game-state synchronization on the main thread.
        /// </summary>
        private void Update()
        {
            ProcessTransportEvents();
            ProcessPendingProposal();
            ProcessSynchronization();
            ProcessHeartbeat();
        }

        /// <summary>
        /// Releases sockets, directory resources, and the singleton claim.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Disconnect();
            _matchmaker?.Dispose();
            _matchmaker = null;
            Instance = null;
        }

        /// <summary>
        /// Starts a direct listen socket and advertises it through the optional directory.
        /// </summary>
        /// <param name="playerName">The host display name.</param>
        /// <param name="port">The TCP listen port.</param>
        /// <param name="matchmakerUrl">The optional directory base URL.</param>
        /// <param name="summary">The host's selected launch settings.</param>
        /// <returns>A task representing listener and directory setup.</returns>
        public async Task HostAsync(
            string playerName,
            int port,
            string matchmakerUrl,
            GameSummary summary
        )
        {
            try
            {
                Disconnect();
                _role = MultiplayerRole.Host;
                _playerName = NormalizePlayerName(playerName);
                _hostPlayerName = _playerName;
                _matchmakerUrl = matchmakerUrl?.Trim();
                _hostSummary = MultiplayerSnapshotCodec.DeserializeSummary(
                    MultiplayerSnapshotCodec.SerializeSummary(
                        summary ?? throw new ArgumentNullException(nameof(summary))
                    )
                );
                ValidatePlayableFactions(_hostSummary);
                _peer = new MultiplayerPeer();
                _peer.StartHost(port);
                int listeningPort = _peer.ListeningPort;
                _hostPort = listeningPort;
                SetStatus($"Hosting on port {listeningPort}; waiting for a player");

                if (!string.IsNullOrWhiteSpace(_matchmakerUrl))
                {
                    MatchmakerGame listing = await _matchmaker.RegisterAsync(
                        _matchmakerUrl,
                        CreateListing(listeningPort)
                    );
                    _listingID = listing.id;
                    _nextHeartbeatTime = Time.unscaledTime + _heartbeatIntervalSeconds;
                    SetStatus($"Listed as '{listing.name}'; waiting for a player");
                }
            }
            catch (Exception exception)
            {
                Disconnect();
                SetStatus($"Host failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Connects directly to a player-hosted game.
        /// </summary>
        /// <param name="playerName">The guest display name.</param>
        /// <param name="host">The host name or address.</param>
        /// <param name="port">The host TCP port.</param>
        /// <param name="matchmakerUrl">The directory used for later refreshes.</param>
        /// <returns>A task representing the connection attempt.</returns>
        public async Task JoinAsync(string playerName, string host, int port, string matchmakerUrl)
        {
            try
            {
                Disconnect();
                _role = MultiplayerRole.Guest;
                _playerName = NormalizePlayerName(playerName);
                _matchmakerUrl = matchmakerUrl?.Trim();
                _peer = new MultiplayerPeer();
                SetStatus($"Connecting to {host}:{port}");
                await _peer.ConnectAsync(host, port);
            }
            catch (Exception exception)
            {
                Disconnect();
                SetStatus($"Connection failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Refreshes live games from the configured directory.
        /// </summary>
        /// <param name="matchmakerUrl">The directory base URL.</param>
        /// <returns>A task representing the lookup.</returns>
        public async Task RefreshGamesAsync(string matchmakerUrl)
        {
            try
            {
                _matchmakerUrl = matchmakerUrl?.Trim();
                IReadOnlyList<MatchmakerGame> games = await _matchmaker.ListAsync(_matchmakerUrl);
                _games.Clear();
                _games.AddRange(games);
                SetStatus($"Found {_games.Count} game{(_games.Count == 1 ? string.Empty : "s")}");
            }
            catch (Exception exception)
            {
                SetStatus($"Directory lookup failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Closes the peer connection and removes any hosted directory listing.
        /// </summary>
        public void Disconnect()
        {
            StopAdvertising();
            _peer?.Dispose();
            _peer = null;
            _runtime = null;
            _hostSummary = null;
            _launchConfiguration = null;
            _pendingProposal = null;
            _role = MultiplayerRole.None;
            _hostPort = 0;
            _revision = 0;
            _lastAcceptedHash = null;
            _remoteReady = false;
            _synchronizationReady = false;
            _proposalPending = false;
            _heartbeatPending = false;
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

            game.SetFactionController(
                _launchConfiguration.HostFactionID,
                _hostPlayerID,
                PlayerControllerType.Human
            );
            game.SetFactionController(
                _launchConfiguration.GuestFactionID,
                _guestPlayerID,
                PlayerControllerType.Human
            );
            game.LocalPlayerFactionID = _launchConfiguration.LocalFactionID;
        }

        /// <summary>
        /// Attaches the active runtime after deterministic scene generation finishes.
        /// </summary>
        /// <param name="runtime">The active game runtime.</param>
        public void AttachGame(GameRuntime runtime)
        {
            if (_launchConfiguration == null)
                return;
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            ConfigureGame(_runtime.GetActiveGame());
            MultiplayerSnapshot initial = MultiplayerSnapshotCodec.Capture(
                _runtime.GetActiveGame()
            );
            _lastAcceptedHash = initial.Hash;
            _nextSnapshotTime = Time.unscaledTime + _snapshotIntervalSeconds;

            if (_role == MultiplayerRole.Guest)
            {
                QueueSend(new MultiplayerPacket(MultiplayerMessageType.Ready));
                SetStatus("Game loaded; waiting for the host state");
            }
            else
            {
                SetStatus("Game loaded; waiting for the other player");
                TrySendInitialSnapshot();
            }
        }

        /// <summary>
        /// Returns whether this machine may advance the simulation clock.
        /// </summary>
        /// <returns>True outside multiplayer, or for a synchronized host.</returns>
        public bool ShouldAdvanceLocalTicks()
        {
            return _launchConfiguration == null
                || (_role == MultiplayerRole.Host && _synchronizationReady);
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
                        SetStatus(
                            string.IsNullOrWhiteSpace(transportEvent.Error)
                                ? "The other player disconnected"
                                : $"Disconnected: {transportEvent.Error}"
                        );
                        break;
                }
            }
        }

        /// <summary>
        /// Starts the role-specific handshake after a socket connects.
        /// </summary>
        private void HandleConnected()
        {
            if (_role == MultiplayerRole.Host)
            {
                SetStatus("Player connected; negotiating game");
                return;
            }

            MultiplayerHelloMessage hello = new MultiplayerHelloMessage
            {
                protocolVersion = _protocolVersion,
                playerName = _playerName,
                contentFingerprint = CreateContentFingerprint(GameLaunchContext.Summary),
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
                    case MultiplayerMessageType.Ready:
                        HandleReady();
                        break;
                    case MultiplayerMessageType.SnapshotProposal:
                        HandleSnapshotProposal(packet);
                        break;
                    case MultiplayerMessageType.SnapshotCommit:
                    case MultiplayerMessageType.SnapshotRejected:
                        HandleSnapshotCommit(packet);
                        break;
                    case MultiplayerMessageType.Disconnect:
                        SetStatus("The other player ended the session");
                        _synchronizationReady = false;
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
            if (_role != MultiplayerRole.Host || _hostSummary == null)
                throw new InvalidOperationException("Only a waiting host can accept a player.");

            MultiplayerHelloMessage hello = JsonUtility.FromJson<MultiplayerHelloMessage>(
                packet.Metadata
            );
            string error = null;
            if (hello == null || hello.protocolVersion != _protocolVersion)
                error = "The multiplayer protocol versions do not match.";
            else if (
                !string.Equals(
                    hello.contentFingerprint,
                    CreateContentFingerprint(_hostSummary),
                    StringComparison.Ordinal
                )
            )
                error = "The content pack, scenario, or enabled mods do not match.";

            string hostFaction = _hostSummary.PlayerFactionID;
            string guestFaction = _hostSummary.StartingFactionIDs.First(id => id != hostFaction);
            MultiplayerWelcomeMessage welcome = new MultiplayerWelcomeMessage
            {
                accepted = error == null,
                error = error,
                hostPlayerName = _hostPlayerName,
                hostFactionID = hostFaction,
                guestFactionID = guestFaction,
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
                return;
            }

            StopAdvertising();
            _launchConfiguration = new MultiplayerLaunchConfiguration(
                MultiplayerSnapshotCodec.DeserializeSummary(
                    MultiplayerSnapshotCodec.SerializeSummary(_hostSummary)
                ),
                hostFaction,
                hostFaction,
                guestFaction
            );
            SetStatus($"Connected to {NormalizePlayerName(hello.playerName)}; starting game");
            LaunchRequested?.Invoke(_launchConfiguration);
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
                throw new InvalidOperationException(
                    welcome?.error ?? "The host rejected the join."
                );

            GameSummary summary = MultiplayerSnapshotCodec.DeserializeSummary(packet.Payload);
            _hostPlayerName = NormalizePlayerName(welcome.hostPlayerName);
            _launchConfiguration = new MultiplayerLaunchConfiguration(
                summary,
                welcome.guestFactionID,
                welcome.hostFactionID,
                welcome.guestFactionID
            );
            SetStatus($"Connected to {_hostPlayerName}; starting game");
            LaunchRequested?.Invoke(_launchConfiguration);
        }

        /// <summary>
        /// Records that the guest finished deterministic game generation.
        /// </summary>
        private void HandleReady()
        {
            if (_role != MultiplayerRole.Host)
                return;
            _remoteReady = true;
            TrySendInitialSnapshot();
        }

        /// <summary>
        /// Queues a guest proposal until the host reaches a stable simulation boundary.
        /// </summary>
        /// <param name="packet">The proposed snapshot packet.</param>
        private void HandleSnapshotProposal(MultiplayerPacket packet)
        {
            if (_role != MultiplayerRole.Host)
                throw new InvalidOperationException("Only the host can order state proposals.");
            _pendingProposal = packet;
        }

        /// <summary>
        /// Applies an authoritative host snapshot on the guest.
        /// </summary>
        /// <param name="packet">The committed snapshot packet.</param>
        private void HandleSnapshotCommit(MultiplayerPacket packet)
        {
            if (_role != MultiplayerRole.Guest || _runtime == null)
                return;
            MultiplayerSnapshotMessage metadata = JsonUtility.FromJson<MultiplayerSnapshotMessage>(
                packet.Metadata
            );
            if (metadata == null || metadata.revision < _revision)
                return;

            GameRoot game = MultiplayerSnapshotCodec.Restore(packet.Payload, metadata.hash);
            ConfigureGame(game);
            _runtime.ReplaceMultiplayerGame(game);
            _revision = metadata.revision;
            _lastAcceptedHash = MultiplayerSnapshotCodec.Capture(game).Hash;
            _proposalPending = false;
            _synchronizationReady = true;
            _nextSnapshotTime = Time.unscaledTime + _snapshotIntervalSeconds;
            SetStatus($"Connected to {_hostPlayerName} — revision {_revision}");
        }

        /// <summary>
        /// Applies or rejects the latest queued guest proposal once the host is stable.
        /// </summary>
        private void ProcessPendingProposal()
        {
            if (
                _role != MultiplayerRole.Host
                || _pendingProposal == null
                || _runtime?.CanSave != true
            )
                return;

            CommitLocalChanges();
            MultiplayerPacket proposal = _pendingProposal;
            _pendingProposal = null;
            MultiplayerSnapshotMessage metadata = JsonUtility.FromJson<MultiplayerSnapshotMessage>(
                proposal.Metadata
            );
            if (metadata == null || metadata.baseRevision != _revision)
            {
                SendCurrentSnapshot(MultiplayerMessageType.SnapshotRejected);
                return;
            }

            GameRoot game = MultiplayerSnapshotCodec.Restore(proposal.Payload, metadata.hash);
            ConfigureGame(game);
            _runtime.ReplaceMultiplayerGame(game);
            _revision++;
            _lastAcceptedHash = MultiplayerSnapshotCodec.Capture(game).Hash;
            MultiplayerSnapshotMessage commit = new MultiplayerSnapshotMessage
            {
                revision = _revision,
                baseRevision = metadata.baseRevision,
                hash = metadata.hash,
            };
            QueueSend(
                new MultiplayerPacket(
                    MultiplayerMessageType.SnapshotCommit,
                    JsonUtility.ToJson(commit),
                    proposal.Payload
                )
            );
            SetStatus($"Player action synchronized — revision {_revision}");
        }

        /// <summary>
        /// Detects stable local changes and sends them through the host ordering boundary.
        /// </summary>
        private void ProcessSynchronization()
        {
            if (
                !_synchronizationReady
                || _runtime?.CanSave != true
                || _peer?.IsConnected != true
                || Time.unscaledTime < _nextSnapshotTime
            )
                return;

            _nextSnapshotTime = Time.unscaledTime + _snapshotIntervalSeconds;
            if (_role == MultiplayerRole.Host)
                CommitLocalChanges();
            else if (_role == MultiplayerRole.Guest && !_proposalPending)
                ProposeLocalChanges();
        }

        /// <summary>
        /// Commits changed host state and broadcasts a new authoritative revision.
        /// </summary>
        private void CommitLocalChanges()
        {
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                _runtime.GetActiveGame()
            );
            if (string.Equals(snapshot.Hash, _lastAcceptedHash, StringComparison.Ordinal))
                return;

            long baseRevision = _revision;
            _revision++;
            _lastAcceptedHash = snapshot.Hash;
            SendSnapshot(MultiplayerMessageType.SnapshotCommit, snapshot, _revision, baseRevision);
        }

        /// <summary>
        /// Sends changed guest state to the host as a revision-checked proposal.
        /// </summary>
        private void ProposeLocalChanges()
        {
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                _runtime.GetActiveGame()
            );
            if (string.Equals(snapshot.Hash, _lastAcceptedHash, StringComparison.Ordinal))
                return;

            _proposalPending = true;
            SendSnapshot(MultiplayerMessageType.SnapshotProposal, snapshot, _revision, _revision);
        }

        /// <summary>
        /// Sends the initial authoritative state after both generated sessions are ready.
        /// </summary>
        private void TrySendInitialSnapshot()
        {
            if (
                _role != MultiplayerRole.Host
                || !_remoteReady
                || _runtime?.GetActiveGame() == null
                || _synchronizationReady
            )
                return;

            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                _runtime.GetActiveGame()
            );
            _revision = 1;
            _lastAcceptedHash = snapshot.Hash;
            _synchronizationReady = true;
            SendSnapshot(MultiplayerMessageType.SnapshotCommit, snapshot, _revision, 0);
            SetStatus("Both players synchronized — revision 1");
        }

        /// <summary>
        /// Sends the current host state as a correction or resynchronization snapshot.
        /// </summary>
        /// <param name="type">The correction message type.</param>
        private void SendCurrentSnapshot(MultiplayerMessageType type)
        {
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                _runtime.GetActiveGame()
            );
            _lastAcceptedHash = snapshot.Hash;
            SendSnapshot(type, snapshot, _revision, _revision);
        }

        /// <summary>
        /// Sends one revisioned snapshot packet.
        /// </summary>
        /// <param name="type">The snapshot message type.</param>
        /// <param name="snapshot">The captured state.</param>
        /// <param name="revision">The resulting authoritative revision.</param>
        /// <param name="baseRevision">The revision on which the state was based.</param>
        private void SendSnapshot(
            MultiplayerMessageType type,
            MultiplayerSnapshot snapshot,
            long revision,
            long baseRevision
        )
        {
            MultiplayerSnapshotMessage metadata = new MultiplayerSnapshotMessage
            {
                revision = revision,
                baseRevision = baseRevision,
                hash = snapshot.Hash,
            };
            QueueSend(new MultiplayerPacket(type, JsonUtility.ToJson(metadata), snapshot.Payload));
        }

        /// <summary>
        /// Refreshes a hosted directory listing before its lease expires.
        /// </summary>
        private void ProcessHeartbeat()
        {
            if (
                _role != MultiplayerRole.Host
                || string.IsNullOrWhiteSpace(_listingID)
                || _heartbeatPending
                || Time.unscaledTime < _nextHeartbeatTime
            )
                return;

            _heartbeatPending = true;
            _nextHeartbeatTime = Time.unscaledTime + _heartbeatIntervalSeconds;
            RefreshListingAsync();
        }

        /// <summary>
        /// Sends a directory heartbeat without interrupting an active direct connection.
        /// </summary>
        private async void RefreshListingAsync()
        {
            try
            {
                MatchmakerGame listing = CreateListing(_hostPort);
                listing.id = _listingID;
                await _matchmaker.RegisterAsync(_matchmakerUrl, listing);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Multiplayer directory heartbeat failed: {exception.Message}");
            }
            finally
            {
                _heartbeatPending = false;
            }
        }

        /// <summary>
        /// Sends a packet and reports asynchronous socket failures as connection status.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        private async void QueueSend(MultiplayerPacket packet)
        {
            try
            {
                await _peer.SendAsync(packet);
            }
            catch (Exception exception)
            {
                _synchronizationReady = false;
                SetStatus($"Send failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Removes a hosted listing without delaying local teardown.
        /// </summary>
        /// <param name="matchmakerUrl">The directory base URL.</param>
        /// <param name="listingID">The listing identifier.</param>
        private async void RemoveListingAsync(string matchmakerUrl, string listingID)
        {
            try
            {
                await _matchmaker.RemoveAsync(matchmakerUrl, listingID);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Multiplayer listing removal failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Stops advertising the hosted game while leaving its direct connection intact.
        /// </summary>
        private void StopAdvertising()
        {
            string listingID = _listingID;
            string matchmakerUrl = _matchmakerUrl;
            _listingID = null;
            _heartbeatPending = false;
            if (!string.IsNullOrWhiteSpace(listingID) && !string.IsNullOrWhiteSpace(matchmakerUrl))
                RemoveListingAsync(matchmakerUrl, listingID);
        }

        /// <summary>
        /// Creates a directory listing for the current host settings.
        /// </summary>
        /// <param name="port">The advertised direct-connect port.</param>
        /// <returns>The directory listing.</returns>
        private MatchmakerGame CreateListing(int port)
        {
            return new MatchmakerGame
            {
                id = _listingID,
                name = $"{_playerName}'s Game",
                address = GetLocalAddress(),
                port = port,
                hostFaction = _hostSummary?.PlayerFactionID,
                contentFingerprint = CreateContentFingerprint(_hostSummary),
            };
        }

        /// <summary>
        /// Returns a best-effort LAN address for directory clients on the same network.
        /// </summary>
        /// <returns>The selected IPv4 address, or loopback as a fallback.</returns>
        private static string GetLocalAddress()
        {
            try
            {
                return Dns.GetHostAddresses(Dns.GetHostName())
                        .FirstOrDefault(address =>
                            address.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(address)
                        )
                        ?.ToString()
                    ?? IPAddress.Loopback.ToString();
            }
            catch (SocketException)
            {
                return IPAddress.Loopback.ToString();
            }
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
        /// Updates connection status and notifies presentation listeners.
        /// </summary>
        /// <param name="status">The new status.</param>
        private void SetStatus(string status)
        {
            _status = status ?? string.Empty;
            StateChanged?.Invoke();
        }
    }
}
