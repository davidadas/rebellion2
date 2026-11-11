using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AddNotifyLobbyMemberStatusReceivedOptions = Epic.OnlineServices.Lobby.AddNotifyLobbyMemberStatusReceivedOptions;
using AttributeData = Epic.OnlineServices.Lobby.AttributeData;
using Common = Epic.OnlineServices.Common;
using ComparisonOp = Epic.OnlineServices.ComparisonOp;
using CreateLobbyCallbackInfo = Epic.OnlineServices.Lobby.CreateLobbyCallbackInfo;
using CreateLobbyOptions = Epic.OnlineServices.Lobby.CreateLobbyOptions;
using CreateLobbySearchOptions = Epic.OnlineServices.Lobby.CreateLobbySearchOptions;
using DestroyLobbyCallbackInfo = Epic.OnlineServices.Lobby.DestroyLobbyCallbackInfo;
using DestroyLobbyOptions = Epic.OnlineServices.Lobby.DestroyLobbyOptions;
using JoinLobbyCallbackInfo = Epic.OnlineServices.Lobby.JoinLobbyCallbackInfo;
using JoinLobbyOptions = Epic.OnlineServices.Lobby.JoinLobbyOptions;
using KickMemberCallbackInfo = Epic.OnlineServices.Lobby.KickMemberCallbackInfo;
using KickMemberOptions = Epic.OnlineServices.Lobby.KickMemberOptions;
using LeaveLobbyCallbackInfo = Epic.OnlineServices.Lobby.LeaveLobbyCallbackInfo;
using LeaveLobbyOptions = Epic.OnlineServices.Lobby.LeaveLobbyOptions;
using LobbyDetails = Epic.OnlineServices.Lobby.LobbyDetails;
using LobbyInterface = Epic.OnlineServices.Lobby.LobbyInterface;
using LobbyMemberStatus = Epic.OnlineServices.Lobby.LobbyMemberStatus;
using LobbyMemberStatusReceivedCallbackInfo = Epic.OnlineServices.Lobby.LobbyMemberStatusReceivedCallbackInfo;
using LobbyModification = Epic.OnlineServices.Lobby.LobbyModification;
using LobbyModificationSetPermissionLevelOptions = Epic.OnlineServices.Lobby.LobbyModificationSetPermissionLevelOptions;
using LobbyPermissionLevel = Epic.OnlineServices.Lobby.LobbyPermissionLevel;
using LobbySearch = Epic.OnlineServices.Lobby.LobbySearch;
using LobbySearchCopySearchResultByIndexOptions = Epic.OnlineServices.Lobby.LobbySearchCopySearchResultByIndexOptions;
using LobbySearchFindCallbackInfo = Epic.OnlineServices.Lobby.LobbySearchFindCallbackInfo;
using LobbySearchFindOptions = Epic.OnlineServices.Lobby.LobbySearchFindOptions;
using LobbySearchGetSearchResultCountOptions = Epic.OnlineServices.Lobby.LobbySearchGetSearchResultCountOptions;
using LobbySearchSetLobbyIdOptions = Epic.OnlineServices.Lobby.LobbySearchSetLobbyIdOptions;
using LobbySearchSetParameterOptions = Epic.OnlineServices.Lobby.LobbySearchSetParameterOptions;
using PlatformInterface = Epic.OnlineServices.Platform.PlatformInterface;
using ProductUserId = Epic.OnlineServices.ProductUserId;
using Result = Epic.OnlineServices.Result;
using UpdateLobbyCallbackInfo = Epic.OnlineServices.Lobby.UpdateLobbyCallbackInfo;
using UpdateLobbyModificationOptions = Epic.OnlineServices.Lobby.UpdateLobbyModificationOptions;
using UpdateLobbyOptions = Epic.OnlineServices.Lobby.UpdateLobbyOptions;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Owns one player's lobby membership, browser searches, and admitted P2P connection.
    /// </summary>
    public sealed partial class EpicMultiplayerBackend : IDisposable
    {
        private readonly PlatformInterface _platform;
        private readonly ProductUserId _localUser;
        private readonly LobbyInterface _lobbies;
        private readonly ulong _memberNotification;
        private EpicMultiplayerPeer _peer;
        private string _lobbyId;
        private ProductUserId _remoteUser;
        private bool _usingRecoveryPeer;
        private bool _ownsLobby;
        private bool _disposed;
        private int _sessionVersion;
        private Task<IMultiplayerPeer> _openingGame;
        private Task _closingGame = Task.CompletedTask;

        /// <summary>
        /// Gets the current lobby identifier, when hosting or joined.
        /// </summary>
        public string LobbyId => _lobbyId;

        /// <summary>
        /// Gets room ownership independently of the campaign's simulation role.
        /// </summary>
        public bool IsLobbyOwner => _ownsLobby;

        /// <summary>
        /// Gets the product identity authenticated by the service for this local participant.
        /// </summary>
        public string LocalUserID => _localUser.ToString();

        /// <summary>
        /// Gets the authenticated counterpart admitted by lobby membership or the saved campaign.
        /// </summary>
        public string RemoteUserID => _remoteUser?.ToString();

        /// <summary>
        /// Gets the current admitted peer after a host reopens a vacated slot.
        /// </summary>
        public IMultiplayerPeer Peer => _peer;

        /// <summary>
        /// Removes a guest whose application-level admission was rejected.
        /// </summary>
        /// <returns>The pending membership removal.</returns>
        public Task RejectGuestAsync()
        {
            if (!_ownsLobby || _lobbyId == null || _remoteUser == null)
                return Task.CompletedTask;
            TaskCompletionSource<bool> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            KickMemberOptions options = new KickMemberOptions
            {
                LobbyId = _lobbyId,
                LocalUserId = _localUser,
                TargetUserId = _remoteUser,
            };
            _lobbies.KickMember(
                ref options,
                null,
                (ref KickMemberCallbackInfo result) =>
                    CompleteCleanup(completion, "Remove rejected player", result.ResultCode)
            );
            return completion.Task;
        }

        /// <summary>
        /// Recreates the host's connection after the previous guest has left.
        /// </summary>
        public void ReopenHostPeer()
        {
            if (!_ownsLobby || _lobbyId == null)
                return;
            _peer?.Dispose();
            _remoteUser = null;
            _peer = new EpicMultiplayerPeer(_platform.GetP2PInterface(), _localUser, _lobbyId);
        }

        /// <summary>
        /// Opens a campaign-scoped stream for the saved counterpart independently of lobby ownership.
        /// </summary>
        /// <param name="campaignID">The durable campaign identifier shared by both participants.</param>
        /// <param name="remoteUserID">The authenticated counterpart reserved by the saved campaign.</param>
        /// <param name="initiate">Whether this participant also initiates an outgoing connection.</param>
        /// <returns>The connection admitted for this campaign.</returns>
        public IMultiplayerPeer OpenRecoveryPeer(
            string campaignID,
            string remoteUserID,
            bool initiate
        )
        {
            ThrowIfDisposed();
            if (!Guid.TryParseExact(campaignID, "N", out _))
                throw new ArgumentException(
                    "A campaign identifier is required.",
                    nameof(campaignID)
                );
            ProductUserId counterpart = ProductUserId.FromString(remoteUserID);
            if (counterpart?.IsValid() != true || counterpart.Equals(_localUser))
                throw new ArgumentException(
                    "A distinct counterpart is required.",
                    nameof(remoteUserID)
                );
            _peer?.Dispose();
            _usingRecoveryPeer = true;
            _remoteUser = counterpart;
            _peer = new EpicMultiplayerPeer(
                _platform.GetP2PInterface(),
                _localUser,
                "campaign:" + campaignID
            );
            if (initiate)
                _peer.Connect(counterpart);
            else
                _peer.Listen(counterpart);
            return _peer;
        }

        /// <summary>
        /// Creates a multiplayer backend for an authenticated product user.
        /// </summary>
        /// <param name="platform">The initialized EOS platform.</param>
        /// <param name="localUser">The authenticated product user.</param>
        public EpicMultiplayerBackend(PlatformInterface platform, ProductUserId localUser)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _localUser = localUser ?? throw new ArgumentNullException(nameof(localUser));
            _lobbies = platform.GetLobbyInterface();
            AddNotifyLobbyMemberStatusReceivedOptions options =
                new AddNotifyLobbyMemberStatusReceivedOptions();
            _memberNotification = _lobbies.AddNotifyLobbyMemberStatusReceived(
                ref options,
                null,
                OnMemberStatusChanged
            );
        }

        /// <summary>
        /// Creates and publishes a two-player game after its connection is ready to accept members.
        /// </summary>
        /// <param name="game">The host's game metadata.</param>
        /// <param name="cancellationToken">Cancels this hosting operation and removes any late-created lobby.</param>
        /// <returns>The hosted peer connection.</returns>
        public Task<IMultiplayerPeer> HostAsync(
            MultiplayerGameListing game,
            CancellationToken cancellationToken
        )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (_lobbyId != null || _peer != null || _openingGame?.IsCompleted == false)
                throw new InvalidOperationException(
                    "Leave the current game before hosting another."
                );
            int sessionVersion = ++_sessionVersion;
            return _openingGame = CreateGameAsync(game, sessionVersion, cancellationToken);
        }

        /// <summary>
        /// Creates the owned room after the outgoing admission and membership cleanup finish.
        /// </summary>
        /// <param name="game">The metadata to publish for the owned room.</param>
        /// <param name="sessionVersion">The admission scope that owns the native request.</param>
        /// <param name="cancellationToken">Cancels admission and removes any late-created room.</param>
        /// <returns>The hosted peer after its listing is published.</returns>
        private async Task<IMultiplayerPeer> CreateGameAsync(
            MultiplayerGameListing game,
            int sessionVersion,
            CancellationToken cancellationToken
        )
        {
            await _closingGame;
            EnsureCurrentSession(sessionVersion, cancellationToken);
            TaskCompletionSource<string> created = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                LocalUserId = _localUser,
                MaxLobbyMembers = 2,
                PermissionLevel = LobbyPermissionLevel.Inviteonly,
                BucketId = EpicLobbyListing.BucketId,
                LobbyId = Guid.NewGuid().ToString("N"),
                DisableHostMigration = false,
            };
            _lobbies.CreateLobby(
                ref options,
                null,
                (ref CreateLobbyCallbackInfo result) =>
                {
                    if (result.ResultCode == Result.Success)
                        created.TrySetResult(result.LobbyId);
                    else if (Common.IsOperationComplete(result.ResultCode))
                        created.TrySetException(Failure("Create game", result.ResultCode));
                }
            );
            string lobbyId = await created.Task;
            EpicMultiplayerPeer peer = null;
            try
            {
                EnsureCurrentSession(sessionVersion, cancellationToken);
                peer = new EpicMultiplayerPeer(_platform.GetP2PInterface(), _localUser, lobbyId);
                _lobbyId = lobbyId;
                _ownsLobby = true;
                _peer = peer;
                await PublishAsync(lobbyId, game);
                EnsureCurrentSession(sessionVersion, cancellationToken);
                return peer;
            }
            catch
            {
                peer?.Dispose();
                ClearSession(lobbyId);
                await CloseLobbyAsync(lobbyId, true);
                throw;
            }
        }

        /// <summary>
        /// Finds current public games without retaining native search handles.
        /// </summary>
        /// <param name="cancellationToken">Cancels presentation of a completed search.</param>
        /// <returns>The published games reported by EOS.</returns>
        public async Task<IReadOnlyList<MultiplayerGameListing>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            LobbySearch search = CreateSearch();
            try
            {
                LobbySearchSetParameterOptions bucketOptions = new LobbySearchSetParameterOptions
                {
                    Parameter = new AttributeData
                    {
                        Key = LobbyInterface.SEARCH_BUCKET_ID,
                        Value = EpicLobbyListing.BucketId,
                    },
                    ComparisonOp = ComparisonOp.Equal,
                };
                RequireSuccess(search.SetParameter(ref bucketOptions), "Configure game search");
                LobbySearchSetParameterOptions capacityOptions = new LobbySearchSetParameterOptions
                {
                    Parameter = new AttributeData
                    {
                        Key = LobbyInterface.SEARCH_MINSLOTSAVAILABLE,
                        Value = 0L,
                    },
                    ComparisonOp = ComparisonOp.Greaterthanorequal,
                };
                RequireSuccess(search.SetParameter(ref capacityOptions), "Include full games");
                await FindAsync(search);
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfDisposed();
                LobbySearchGetSearchResultCountOptions countOptions =
                    new LobbySearchGetSearchResultCountOptions();
                uint count = search.GetSearchResultCount(ref countOptions);
                List<MultiplayerGameListing> games = new();
                for (uint index = 0; index < count; index++)
                {
                    LobbyDetails details = CopyResult(search, index);
                    try
                    {
                        MultiplayerGameListing game = EpicLobbyListing.Read(details);
                        if (game != null)
                            games.Add(game);
                    }
                    finally
                    {
                        details.Release();
                    }
                }
                return games;
            }
            finally
            {
                search.Release();
            }
        }

        /// <summary>
        /// Joins a fresh lobby lookup and connects only to its current owner.
        /// </summary>
        /// <param name="game">The selected public game.</param>
        /// <param name="cancellationToken">Cancels joining and leaves any late-joined lobby.</param>
        /// <returns>The guest peer connection.</returns>
        public Task<IMultiplayerPeer> JoinAsync(
            MultiplayerGameListing game,
            CancellationToken cancellationToken
        )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (_lobbyId != null || _peer != null || _openingGame?.IsCompleted == false)
                throw new InvalidOperationException(
                    "Leave the current game before joining another."
                );
            int sessionVersion = ++_sessionVersion;
            return _openingGame = JoinGameAsync(game, sessionVersion, cancellationToken);
        }

        /// <summary>
        /// Admits the selected room after any previous native admission and cleanup finish.
        /// </summary>
        /// <param name="game">The selected browser listing.</param>
        /// <param name="sessionVersion">The admission scope that owns this request.</param>
        /// <param name="cancellationToken">Cancels admission and removes any late-joined membership.</param>
        /// <returns>The admitted peer connected to the room owner.</returns>
        private async Task<IMultiplayerPeer> JoinGameAsync(
            MultiplayerGameListing game,
            int sessionVersion,
            CancellationToken cancellationToken
        )
        {
            await _closingGame;
            EnsureCurrentSession(sessionVersion, cancellationToken);
            LobbySearch search = CreateSearch();
            LobbyDetails details = null;
            EpicMultiplayerPeer peer = null;
            bool joined = false;
            try
            {
                LobbySearchSetLobbyIdOptions idOptions = new LobbySearchSetLobbyIdOptions
                {
                    LobbyId = game.id,
                };
                RequireSuccess(search.SetLobbyId(ref idOptions), "Find selected game");
                await FindAsync(search);
                EnsureCurrentSession(sessionVersion, cancellationToken);
                details = CopyResult(search, 0);
                MultiplayerGameListing current = EpicLobbyListing.Read(details);
                if (
                    !EpicLobbyListing.IsJoinable(current)
                    || !string.Equals(
                        current.contentFingerprint,
                        game.contentFingerprint,
                        StringComparison.Ordinal
                    )
                )
                    throw new InvalidOperationException(
                        "This game is no longer available with the selected settings."
                    );
                ProductUserId owner = ProductUserId.FromString(current.ownerUserID);
                if (owner.Equals(_localUser))
                    throw new InvalidOperationException("You cannot join your own game.");
                peer = new EpicMultiplayerPeer(_platform.GetP2PInterface(), _localUser, game.id);
                // Register and admit the owner before joining, so a host connection request cannot be missed.
                peer.Connect(owner);
                TaskCompletionSource<bool> completion = new(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                JoinLobbyOptions joinOptions = new JoinLobbyOptions
                {
                    LocalUserId = _localUser,
                    LobbyDetailsHandle = details,
                };
                _lobbies.JoinLobby(
                    ref joinOptions,
                    null,
                    (ref JoinLobbyCallbackInfo result) =>
                        Complete(completion, "Join game", result.ResultCode)
                );
                await completion.Task;
                joined = true;
                EnsureCurrentSession(sessionVersion, cancellationToken);
                _lobbyId = game.id;
                _ownsLobby = false;
                _remoteUser = owner;
                _peer = peer;
                return peer;
            }
            catch
            {
                peer?.Dispose();
                if (joined)
                    await CloseLobbyAsync(game.id, false);
                throw;
            }
            finally
            {
                details?.Release();
                search.Release();
            }
        }

        /// <summary>
        /// Updates the waiting or started state of the currently hosted listing.
        /// </summary>
        /// <param name="game">The updated host metadata.</param>
        /// <returns>The pending lobby update.</returns>
        public Task UpdateListingAsync(MultiplayerGameListing game)
        {
            ThrowIfDisposed();
            return _lobbyId != null && _ownsLobby
                ? PublishAsync(_lobbyId, game)
                : Task.CompletedTask;
        }

        /// <summary>
        /// Drains this session's gameplay messages on the main thread.
        /// </summary>
        public void Poll()
        {
            _peer?.Poll();
            PollGeneralChat();
        }

        /// <summary>
        /// Immediately detaches the local connection and asynchronously removes membership.
        /// </summary>
        /// <param name="preserveRoom">Whether a surviving campaign participant may retain the room.</param>
        /// <returns>The pending lobby destruction or membership departure.</returns>
        public Task LeaveAsync(bool preserveRoom = false)
        {
            _sessionVersion++;
            Task opening = _openingGame;
            _openingGame = null;
            string lobbyId = _lobbyId;
            bool owner = _ownsLobby;
            _peer?.Dispose();
            ClearSession(lobbyId);
            Task closing =
                lobbyId == null ? _closingGame : CloseLobbyAsync(lobbyId, owner && !preserveRoom);
            return _closingGame = FinishLeavingAsync(opening, closing);
        }

        /// <summary>
        /// Releases this owner's multiplayer resources and pending connection.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _lobbies.RemoveNotifyLobbyMemberStatusReceived(_memberNotification);
            DisposeGeneralChat();
            _ = LeaveAndReportAsync();
        }

        /// <summary>
        /// Admits remote members from lifecycle events and propagates their departure.
        /// </summary>
        /// <param name="data">The lobby membership event.</param>
        private void OnMemberStatusChanged(ref LobbyMemberStatusReceivedCallbackInfo data)
        {
            HandleGeneralChatMembership(ref data);
            if (
                _disposed
                || _peer == null
                || !string.Equals(data.LobbyId, _lobbyId, StringComparison.Ordinal)
            )
                return;
            if (data.CurrentStatus == LobbyMemberStatus.Promoted)
            {
                _ownsLobby = data.TargetUserId.Equals(_localUser);
                return;
            }
            if (_usingRecoveryPeer)
            {
                if (
                    data.CurrentStatus == LobbyMemberStatus.Closed
                    || data.TargetUserId.Equals(_localUser)
                        && (
                            data.CurrentStatus == LobbyMemberStatus.Left
                            || data.CurrentStatus == LobbyMemberStatus.Kicked
                            || data.CurrentStatus == LobbyMemberStatus.Disconnected
                        )
                )
                {
                    _lobbyId = null;
                    _ownsLobby = false;
                }
                return;
            }
            if (
                data.CurrentStatus == LobbyMemberStatus.Joined
                && _ownsLobby
                && !data.TargetUserId.Equals(_localUser)
            )
            {
                try
                {
                    _remoteUser = data.TargetUserId;
                    _peer.Connect(data.TargetUserId);
                }
                catch (Exception exception)
                {
                    _peer.Disconnect(exception.Message);
                }
            }
            else if (
                (
                    data.CurrentStatus == LobbyMemberStatus.Closed
                    || data.CurrentStatus == LobbyMemberStatus.Kicked
                    || data.CurrentStatus == LobbyMemberStatus.Left
                    || data.CurrentStatus == LobbyMemberStatus.Disconnected
                ) && (!_ownsLobby || data.TargetUserId.Equals(_remoteUser))
            )
            {
                bool localDeparture =
                    data.CurrentStatus == LobbyMemberStatus.Closed
                    || data.TargetUserId.Equals(_localUser);
                _remoteUser = null;
                if (localDeparture)
                {
                    _lobbyId = null;
                    _ownsLobby = false;
                }
                _peer.Disconnect("A player left the game lobby.");
            }
        }

        /// <summary>
        /// Publishes metadata and makes the initialized host discoverable atomically.
        /// </summary>
        /// <param name="lobbyId">The owned lobby.</param>
        /// <param name="game">The game metadata.</param>
        /// <returns>The pending metadata publication.</returns>
        private async Task PublishAsync(string lobbyId, MultiplayerGameListing game)
        {
            UpdateLobbyModificationOptions options = new UpdateLobbyModificationOptions
            {
                LocalUserId = _localUser,
                LobbyId = lobbyId,
            };
            RequireSuccess(
                _lobbies.UpdateLobbyModification(ref options, out LobbyModification modification),
                "Prepare game listing"
            );
            try
            {
                EpicLobbyListing.Write(modification, game);
                LobbyModificationSetPermissionLevelOptions permissionOptions =
                    new LobbyModificationSetPermissionLevelOptions
                    {
                        PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                    };
                RequireSuccess(
                    modification.SetPermissionLevel(ref permissionOptions),
                    "Publish game listing"
                );
                TaskCompletionSource<bool> completion = new(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                UpdateLobbyOptions updateOptions = new UpdateLobbyOptions
                {
                    LobbyModificationHandle = modification,
                };
                _lobbies.UpdateLobby(
                    ref updateOptions,
                    null,
                    (ref UpdateLobbyCallbackInfo result) =>
                        Complete(completion, "Update game listing", result.ResultCode)
                );
                await completion.Task;
            }
            finally
            {
                modification.Release();
            }
        }

        /// <summary>
        /// Creates a search using the service's maximum supported result count.
        /// </summary>
        /// <returns>The owned native search handle.</returns>
        private LobbySearch CreateSearch()
        {
            CreateLobbySearchOptions options = new CreateLobbySearchOptions
            {
                MaxResults = LobbyInterface.MAX_SEARCH_RESULTS,
            };
            RequireSuccess(
                _lobbies.CreateLobbySearch(ref options, out LobbySearch search),
                "Create game search"
            );
            return search;
        }

        /// <summary>
        /// Runs a configured search for this authenticated player.
        /// </summary>
        /// <param name="search">The configured search.</param>
        /// <returns>The completed search operation.</returns>
        private Task FindAsync(LobbySearch search)
        {
            TaskCompletionSource<bool> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            LobbySearchFindOptions options = new LobbySearchFindOptions
            {
                LocalUserId = _localUser,
            };
            search.Find(
                ref options,
                null,
                (ref LobbySearchFindCallbackInfo result) =>
                    Complete(completion, "Find games", result.ResultCode)
            );
            return completion.Task;
        }

        /// <summary>
        /// Copies an owned lobby details handle from one result.
        /// </summary>
        /// <param name="search">The completed search.</param>
        /// <param name="index">The result index.</param>
        /// <returns>The owned result handle.</returns>
        private static LobbyDetails CopyResult(LobbySearch search, uint index)
        {
            LobbySearchCopySearchResultByIndexOptions options =
                new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = index };
            RequireSuccess(
                search.CopySearchResultByIndex(ref options, out LobbyDetails details),
                "Read game listing"
            );
            return details;
        }

        /// <summary>
        /// Removes only the captured membership, even when a later operation has already started.
        /// </summary>
        /// <param name="lobbyId">The captured lobby identifier.</param>
        /// <param name="owner">Whether this player created the lobby.</param>
        /// <returns>The pending cleanup operation.</returns>
        private Task CloseLobbyAsync(string lobbyId, bool owner)
        {
            TaskCompletionSource<bool> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            if (owner)
            {
                DestroyLobbyOptions options = new DestroyLobbyOptions
                {
                    LocalUserId = _localUser,
                    LobbyId = lobbyId,
                };
                _lobbies.DestroyLobby(
                    ref options,
                    null,
                    (ref DestroyLobbyCallbackInfo result) =>
                        CompleteCleanup(completion, "Close hosted game", result.ResultCode)
                );
            }
            else
            {
                LeaveLobbyOptions options = new LeaveLobbyOptions
                {
                    LocalUserId = _localUser,
                    LobbyId = lobbyId,
                };
                _lobbies.LeaveLobby(
                    ref options,
                    null,
                    (ref LeaveLobbyCallbackInfo result) =>
                        CompleteCleanup(completion, "Leave game", result.ResultCode)
                );
            }
            return completion.Task;
        }

        /// <summary>
        /// Finishes an outgoing admission and its cleanup before another join can use the same room.
        /// </summary>
        /// <param name="opening">The outgoing admission, whose caller retains its failure.</param>
        /// <param name="closing">The previously pending or newly requested membership removal.</param>
        /// <returns>The completion of both outgoing operations.</returns>
        private static async Task FinishLeavingAsync(Task opening, Task closing)
        {
            try
            {
                if (opening != null)
                    await opening;
            }
            catch (Exception)
            {
                // The admission caller owns its error; its cleanup has finished before this task completes.
            }
            await closing;
        }

        /// <summary>
        /// Reports asynchronous cleanup failures during application teardown.
        /// </summary>
        /// <returns>The pending cleanup.</returns>
        private async Task LeaveAndReportAsync()
        {
            try
            {
                await LeaveAsync();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "Multiplayer lobby cleanup failed: " + exception.Message
                );
            }
        }

        /// <summary>
        /// Clears state only when it still belongs to the specified operation.
        /// </summary>
        /// <param name="lobbyId">The session being cleared.</param>
        private void ClearSession(string lobbyId)
        {
            if (!string.Equals(_lobbyId, lobbyId, StringComparison.Ordinal))
                return;
            _lobbyId = null;
            _ownsLobby = false;
            _remoteUser = null;
            _usingRecoveryPeer = false;
            _peer = null;
        }

        /// <summary>
        /// Completes an asynchronous SDK operation.
        /// </summary>
        /// <param name="completion">The pending operation.</param>
        /// <param name="operation">The operation name.</param>
        /// <param name="result">The terminal SDK result.</param>
        private static void Complete(
            TaskCompletionSource<bool> completion,
            string operation,
            Result result
        )
        {
            if (result == Result.Success)
                completion.TrySetResult(true);
            else if (Common.IsOperationComplete(result))
                completion.TrySetException(Failure(operation, result));
        }

        /// <summary>
        /// Allows cleanup to finish when the remote host has already removed the lobby.
        /// </summary>
        /// <param name="completion">The pending cleanup.</param>
        /// <param name="operation">The cleanup operation.</param>
        /// <param name="result">The SDK result.</param>
        private static void CompleteCleanup(
            TaskCompletionSource<bool> completion,
            string operation,
            Result result
        )
        {
            Complete(completion, operation, result == Result.NotFound ? Result.Success : result);
        }

        /// <summary>
        /// Converts an SDK failure to a safe connection error.
        /// </summary>
        /// <param name="operation">The failed operation.</param>
        /// <param name="result">The SDK result.</param>
        /// <returns>The connection exception.</returns>
        private static InvalidOperationException Failure(string operation, Result result) =>
            new(operation + ": " + result);

        /// <summary>
        /// Rejects unsuccessful synchronous SDK operations.
        /// </summary>
        /// <param name="result">The SDK result.</param>
        /// <param name="operation">The requested operation.</param>
        private static void RequireSuccess(Result result, string operation)
        {
            if (result != Result.Success)
                throw Failure(operation, result);
        }

        /// <summary>
        /// Rejects operations after application teardown.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EpicMultiplayerBackend));
        }

        /// <summary>
        /// Prevents a late SDK callback from restoring a departed or superseded session.
        /// </summary>
        /// <param name="sessionVersion">The operation's captured ownership version.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        private void EnsureCurrentSession(int sessionVersion, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || sessionVersion != _sessionVersion)
                throw new OperationCanceledException(
                    "The multiplayer session was closed or replaced."
                );
        }
    }
}
