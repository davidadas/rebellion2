using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using AcceptConnectionOptions = Epic.OnlineServices.P2P.AcceptConnectionOptions;
using AddNotifyPeerConnectionRequestOptions = Epic.OnlineServices.P2P.AddNotifyPeerConnectionRequestOptions;
using CloseConnectionsOptions = Epic.OnlineServices.P2P.CloseConnectionsOptions;
using Common = Epic.OnlineServices.Common;
using CopyLobbyDetailsHandleOptions = Epic.OnlineServices.Lobby.CopyLobbyDetailsHandleOptions;
using CreateLobbyCallbackInfo = Epic.OnlineServices.Lobby.CreateLobbyCallbackInfo;
using CreateLobbyOptions = Epic.OnlineServices.Lobby.CreateLobbyOptions;
using JoinLobbyByIdCallbackInfo = Epic.OnlineServices.Lobby.JoinLobbyByIdCallbackInfo;
using JoinLobbyByIdOptions = Epic.OnlineServices.Lobby.JoinLobbyByIdOptions;
using LobbyDetails = Epic.OnlineServices.Lobby.LobbyDetails;
using LobbyDetailsGetMemberByIndexOptions = Epic.OnlineServices.Lobby.LobbyDetailsGetMemberByIndexOptions;
using LobbyDetailsGetMemberCountOptions = Epic.OnlineServices.Lobby.LobbyDetailsGetMemberCountOptions;
using LobbyInterface = Epic.OnlineServices.Lobby.LobbyInterface;
using LobbyMemberStatus = Epic.OnlineServices.Lobby.LobbyMemberStatus;
using LobbyMemberStatusReceivedCallbackInfo = Epic.OnlineServices.Lobby.LobbyMemberStatusReceivedCallbackInfo;
using LobbyPermissionLevel = Epic.OnlineServices.Lobby.LobbyPermissionLevel;
using OnIncomingConnectionRequestInfo = Epic.OnlineServices.P2P.OnIncomingConnectionRequestInfo;
using P2PInterface = Epic.OnlineServices.P2P.P2PInterface;
using PacketReliability = Epic.OnlineServices.P2P.PacketReliability;
using ProductUserId = Epic.OnlineServices.ProductUserId;
using ReceivePacketOptions = Epic.OnlineServices.P2P.ReceivePacketOptions;
using Result = Epic.OnlineServices.Result;
using SendPacketOptions = Epic.OnlineServices.P2P.SendPacketOptions;
using SocketId = Epic.OnlineServices.P2P.SocketId;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Exchanges browser conversation on a separate EOS lobby and reliable P2P channel.
    /// </summary>
    public sealed partial class EpicMultiplayerBackend
    {
        private const string _chatBucket = "rebellion2-chat-v2";
        private const string _sharedChatLobbyID = "0b7a7e4e338545aab406c87d08ab8855";
        private int _chatVersion;
        private readonly Dictionary<string, ProductUserId> _chatMembers = new();
        private readonly byte[] _chatReceiveBuffer = new byte[P2PInterface.MAX_PACKET_SIZE];
        private readonly SocketId _chatSocket = new SocketId { SocketName = "r2-general-chat" };
        private string _chatLobbyID;
        private Task _openingChat;
        private Task _closingChat = Task.CompletedTask;
        private ulong _chatRequestNotification;

        /// <summary>
        /// Gets whether the browser has joined a general conversation channel.
        /// </summary>
        public bool IsGeneralChatConnected => _chatLobbyID != null;

        /// <summary>
        /// Gets the current service-admitted conversation membership count.
        /// </summary>
        public int GeneralChatMemberCount => _chatMembers.Count;

        /// <summary>
        /// Occurs when a message arrives from a current general-channel member.
        /// </summary>
        public event Action<MultiplayerChatMessage> GeneralChatReceived;

        /// <summary>
        /// Releases the browser conversation when its native console closes.
        /// </summary>
        /// <returns>The pending service membership removal.</returns>
        public Task CloseGeneralChatAsync()
        {
            _chatVersion++;
            Task opening = _openingChat;
            _openingChat = null;
            if (_chatRequestNotification != 0)
                _platform
                    .GetP2PInterface()
                    .RemoveNotifyPeerConnectionRequest(_chatRequestNotification);
            _chatRequestNotification = 0;
            CloseConnectionsOptions connections = new CloseConnectionsOptions
            {
                LocalUserId = _localUser,
                SocketId = _chatSocket,
            };
            _platform.GetP2PInterface().CloseConnections(ref connections);
            string lobbyID = _chatLobbyID;
            _chatLobbyID = null;
            _chatMembers.Clear();
            Task closing = lobbyID == null ? _closingChat : CloseLobbyAsync(lobbyID, false);
            _closingChat = FinishClosingChatAsync(opening, closing);
            return _closingChat;
        }

        /// <summary>
        /// Finishes an outgoing admission and its cleanup before another join can use the same room.
        /// </summary>
        /// <param name="opening">The outgoing admission, whose caller retains its failure.</param>
        /// <param name="closing">The previously pending or newly requested membership removal.</param>
        /// <returns>The completion of both outgoing operations.</returns>
        private static async Task FinishClosingChatAsync(Task opening, Task closing)
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
        /// Joins a public conversation channel or creates one with EOS host migration enabled.
        /// </summary>
        /// <returns>The shared pending channel admission.</returns>
        public Task OpenGeneralChatAsync()
        {
            ThrowIfDisposed();
            return _chatLobbyID != null
                ? Task.CompletedTask
                : _openingChat ??= JoinGeneralChatAsync();
        }

        /// <summary>
        /// Sends one bounded UTF-8 message to each currently admitted channel member.
        /// </summary>
        /// <param name="message">The authenticated local player's plain-text message.</param>
        public void SendGeneralChat(MultiplayerChatMessage message)
        {
            ThrowIfDisposed();
            if (_chatLobbyID == null)
                throw new InvalidOperationException(
                    "General chat is connecting. Try again shortly."
                );
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
            if (bytes.Length > P2PInterface.MAX_PACKET_SIZE)
                throw new InvalidOperationException("This message is too long. Please shorten it.");
            RefreshChatMembers();
            foreach (ProductUserId member in _chatMembers.Values)
            {
                if (member.Equals(_localUser))
                    continue;
                SendPacketOptions options = new SendPacketOptions
                {
                    LocalUserId = _localUser,
                    RemoteUserId = member,
                    SocketId = _chatSocket,
                    Channel = 1,
                    Data = new ArraySegment<byte>(bytes),
                    Reliability = PacketReliability.ReliableOrdered,
                    AllowDelayedDelivery = true,
                };
                RequireSuccess(
                    _platform.GetP2PInterface().SendPacket(ref options),
                    "Send general chat"
                );
            }
        }

        /// <summary>
        /// Atomically creates or joins the shared channel and registers its isolated P2P admission callback.
        /// </summary>
        /// <returns>The pending channel membership.</returns>
        private async Task JoinGeneralChatAsync()
        {
            string joinedID = null;
            int version = _chatVersion;
            try
            {
                await _closingChat;
                ThrowIfDisposed();
                if (version != _chatVersion)
                    return;
                TaskCompletionSource<Result> created = new(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    LocalUserId = _localUser,
                    LobbyId = _sharedChatLobbyID,
                    MaxLobbyMembers = LobbyInterface.MAX_LOBBY_MEMBERS,
                    PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                    BucketId = _chatBucket,
                    DisableHostMigration = false,
                    EnableJoinById = true,
                };
                _lobbies.CreateLobby(
                    ref options,
                    null,
                    (ref CreateLobbyCallbackInfo info) =>
                    {
                        if (
                            info.ResultCode == Result.Success
                            || info.ResultCode == Result.LobbyLobbyAlreadyExists
                        )
                            created.TrySetResult(info.ResultCode);
                        else if (Common.IsOperationComplete(info.ResultCode))
                            created.TrySetException(Failure("Open general chat", info.ResultCode));
                    }
                );
                if (await created.Task == Result.LobbyLobbyAlreadyExists)
                {
                    TaskCompletionSource<bool> joined = new(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );
                    JoinLobbyByIdOptions join = new JoinLobbyByIdOptions
                    {
                        LocalUserId = _localUser,
                        LobbyId = _sharedChatLobbyID,
                    };
                    _lobbies.JoinLobbyById(
                        ref join,
                        null,
                        (ref JoinLobbyByIdCallbackInfo info) =>
                            Complete(joined, "Join general chat", info.ResultCode)
                    );
                    await joined.Task;
                }
                joinedID = _sharedChatLobbyID;
                ThrowIfDisposed();
                if (version != _chatVersion)
                {
                    await CloseLobbyAsync(joinedID, false);
                    return;
                }
                _chatLobbyID = joinedID;
                RefreshChatMembers();
                AddNotifyPeerConnectionRequestOptions notification =
                    new AddNotifyPeerConnectionRequestOptions
                    {
                        LocalUserId = _localUser,
                        SocketId = _chatSocket,
                    };
                _chatRequestNotification = _platform
                    .GetP2PInterface()
                    .AddNotifyPeerConnectionRequest(ref notification, null, AcceptChatConnection);
            }
            catch
            {
                if (version == _chatVersion)
                    _chatLobbyID = null;
                if (joinedID != null)
                    await CloseLobbyAsync(joinedID, false);
                throw;
            }
            finally
            {
                if (version == _chatVersion)
                    _openingChat = null;
            }
        }

        /// <summary>
        /// Resolves the current channel roster from one lobby-details snapshot.
        /// </summary>
        private void RefreshChatMembers()
        {
            if (_chatLobbyID == null || _disposed)
                return;
            CopyLobbyDetailsHandleOptions options = new CopyLobbyDetailsHandleOptions
            {
                LocalUserId = _localUser,
                LobbyId = _chatLobbyID,
            };
            RequireSuccess(
                _lobbies.CopyLobbyDetailsHandle(ref options, out LobbyDetails details),
                "Read chat members"
            );
            try
            {
                _chatMembers.Clear();
                LobbyDetailsGetMemberCountOptions countOptions =
                    new LobbyDetailsGetMemberCountOptions();
                uint count = details.GetMemberCount(ref countOptions);
                for (uint i = 0; i < count; i++)
                {
                    LobbyDetailsGetMemberByIndexOptions memberOptions =
                        new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                    ProductUserId member = details.GetMemberByIndex(ref memberOptions);
                    if (member != null)
                        _chatMembers[member.ToString()] = member;
                }
            }
            finally
            {
                details.Release();
            }
        }

        /// <summary>
        /// Admits P2P requests only from current general-channel members.
        /// </summary>
        /// <param name="info">The incoming channel connection request.</param>
        private void AcceptChatConnection(ref OnIncomingConnectionRequestInfo info)
        {
            if (_disposed || _chatLobbyID == null)
                return;
            RefreshChatMembers();
            if (!_chatMembers.ContainsKey(info.RemoteUserId.ToString()))
                return;
            AcceptConnectionOptions options = new AcceptConnectionOptions
            {
                LocalUserId = _localUser,
                RemoteUserId = info.RemoteUserId,
                SocketId = _chatSocket,
            };
            _platform.GetP2PInterface().AcceptConnection(ref options);
        }

        /// <summary>
        /// Maintains chat admission from service lifecycle events rather than per-frame roster queries.
        /// </summary>
        /// <param name="info">The channel membership update.</param>
        private void HandleGeneralChatMembership(ref LobbyMemberStatusReceivedCallbackInfo info)
        {
            if (
                _disposed
                || _chatLobbyID == null
                || !string.Equals(info.LobbyId, _chatLobbyID, StringComparison.Ordinal)
            )
                return;
            if (
                info.CurrentStatus == LobbyMemberStatus.Closed
                || (
                    (
                        info.CurrentStatus == LobbyMemberStatus.Left
                        || info.CurrentStatus == LobbyMemberStatus.Kicked
                        || info.CurrentStatus == LobbyMemberStatus.Disconnected
                    ) && info.TargetUserId.Equals(_localUser)
                )
            )
                _chatLobbyID = null;
            else
                RefreshChatMembers();
        }

        /// <summary>
        /// Drains only chat channel packets and leaves gameplay packets for the game transport.
        /// </summary>
        private void PollGeneralChat()
        {
            if (_disposed || _chatLobbyID == null)
                return;
            ReceivePacketOptions options = new ReceivePacketOptions
            {
                LocalUserId = _localUser,
                RequestedChannel = 1,
                MaxDataSizeBytes = P2PInterface.MAX_PACKET_SIZE,
            };
            ProductUserId sender = null;
            SocketId socket = new SocketId();
            while (
                _platform
                    .GetP2PInterface()
                    .ReceivePacket(
                        ref options,
                        ref sender,
                        ref socket,
                        out byte channel,
                        new ArraySegment<byte>(_chatReceiveBuffer),
                        out uint length
                    ) == Result.Success
            )
            {
                if (
                    channel != 1
                    || socket.SocketName != _chatSocket.SocketName
                    || sender == null
                    || !_chatMembers.ContainsKey(sender.ToString())
                )
                    continue;
                try
                {
                    MultiplayerChatMessage message = JsonUtility.FromJson<MultiplayerChatMessage>(
                        Encoding.UTF8.GetString(_chatReceiveBuffer, 0, checked((int)length))
                    );
                    if (!string.IsNullOrWhiteSpace(message?.text))
                        GeneralChatReceived?.Invoke(message);
                }
                catch (ArgumentException) { }
            }
        }

        /// <summary>
        /// Removes the browser channel's notification and service membership during online teardown.
        /// </summary>
        private void DisposeGeneralChat()
        {
            _ = CloseGeneralChatAsync();
        }
    }
}
