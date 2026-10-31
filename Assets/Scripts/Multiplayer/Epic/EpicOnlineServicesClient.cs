using System;
using System.Threading.Tasks;
using UnityEngine;
using AddNotifyAuthExpirationOptions = Epic.OnlineServices.Connect.AddNotifyAuthExpirationOptions;
using AuthExpirationCallbackInfo = Epic.OnlineServices.Connect.AuthExpirationCallbackInfo;
using Common = Epic.OnlineServices.Common;
using ConnectInterface = Epic.OnlineServices.Connect.ConnectInterface;
using CreateUserCallbackInfo = Epic.OnlineServices.Connect.CreateUserCallbackInfo;
using CreateUserOptions = Epic.OnlineServices.Connect.CreateUserOptions;
using Credentials = Epic.OnlineServices.Connect.Credentials;
using EOSManager = PlayEveryWare.EpicOnlineServices.EOSManager;
using ExternalCredentialType = Epic.OnlineServices.ExternalCredentialType;
using LoginCallbackInfo = Epic.OnlineServices.Connect.LoginCallbackInfo;
using LoginOptions = Epic.OnlineServices.Connect.LoginOptions;
using ProductUserId = Epic.OnlineServices.ProductUserId;
using Result = Epic.OnlineServices.Result;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Authenticates launcher store sessions with EOS Connect and owns their multiplayer resources.
    /// </summary>
    public sealed class EpicOnlineServicesClient : IDisposable
    {
        private readonly GameObject _owner;
        private readonly StorePlayerAuthentication _authentication = new();
        private Task<EpicMultiplayerBackend> _connecting;
        private TaskCompletionSource<ProductUserId> _pendingLogin;
        private EpicMultiplayerBackend _backend;
        private ConnectInterface _connect;
        private ProductUserId _localUser;
        private ulong _expirationNotification;
        private bool _shutdownRegistered;
        private bool _disposed;

        public StorePlayerSession Session => _authentication.Session;

        /// <summary>
        /// Creates an online client owned by the persistent application object.
        /// </summary>
        /// <param name="owner">The application object that owns the SDK component.</param>
        public EpicOnlineServicesClient(GameObject owner)
        {
            _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// Opens store sign-in before creating the account's multiplayer backend.
        /// </summary>
        /// <param name="provider">The Steam or GOG provider identifier.</param>
        /// <returns>The authenticated store session.</returns>
        public async Task<StorePlayerSession> SignInAsync(string provider)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EpicOnlineServicesClient));
            StorePlayerSession session = await _authentication.SignInAsync(provider);
            _backend?.Dispose();
            _backend = null;
            _connecting = null;
            await ConnectAsync();
            return session;
        }

        /// <summary>
        /// Shares the authenticated lobby backend across multiplayer menu operations.
        /// </summary>
        /// <returns>The authenticated multiplayer backend.</returns>
        public Task<EpicMultiplayerBackend> ConnectAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EpicOnlineServicesClient));
            if (_authentication.Session == null)
                throw new InvalidOperationException(
                    "Choose Steam or GOG on the multiplayer sign-in screen."
                );
            if (_backend != null)
                return Task.FromResult(_backend);
            if (_connecting?.IsFaulted != false || _connecting.IsCanceled)
                _connecting = ConnectCoreAsync();
            return _connecting;
        }

        /// <summary>
        /// Releases native notifications and transport resources before SDK shutdown.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _pendingLogin?.TrySetCanceled();
            if (_expirationNotification != 0)
                _connect?.RemoveNotifyAuthExpiration(_expirationNotification);
            _expirationNotification = 0;
            _backend?.Dispose();
            _authentication.Dispose();
        }

        /// <summary>
        /// Initializes EOS and exchanges the verified store credential for a product user.
        /// </summary>
        /// <returns>The account's lobby backend.</returns>
        private async Task<EpicMultiplayerBackend> ConnectCoreAsync()
        {
            EOSManager manager = _owner.GetComponent<EOSManager>();
            if (manager == null)
                manager = _owner.AddComponent<EOSManager>();
            EOSManager.EOSSingleton sdk = EOSManager.Instance;
            if (sdk.GetEOSPlatformInterface() == null)
                sdk.Init(manager);
            _connect = sdk.GetEOSPlatformInterface().GetConnectInterface();
            if (!_shutdownRegistered)
            {
                sdk.AddApplicationCloseListener(Dispose);
                _shutdownRegistered = true;
            }
            _localUser = await LoginAsync();
            if (_disposed)
                throw new ObjectDisposedException(nameof(EpicOnlineServicesClient));
            if (_expirationNotification == 0)
            {
                AddNotifyAuthExpirationOptions options = new();
                _expirationNotification = _connect.AddNotifyAuthExpiration(
                    ref options,
                    null,
                    (ref AuthExpirationCallbackInfo info) =>
                    {
                        if (!_disposed && info.LocalUserId == _localUser)
                            RefreshAuthentication();
                    }
                );
            }
            _backend = new EpicMultiplayerBackend(sdk.GetEOSPlatformInterface(), _localUser);
            return _backend;
        }

        /// <summary>
        /// Uses the current store token for both initial sign-in and EOS session renewal.
        /// </summary>
        /// <returns>The authenticated product user.</returns>
        private Task<ProductUserId> LoginAsync()
        {
            StorePlayerSession session =
                _authentication.Session
                ?? throw new InvalidOperationException(
                    "Your store session expired. Sign in with Steam or GOG again."
                );
            TaskCompletionSource<ProductUserId> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            _pendingLogin = completion;
            LoginOptions options = new()
            {
                Credentials = new Credentials
                {
                    Type = ExternalCredentialType.OpenidAccessToken,
                    Token = session.AccessToken,
                },
            };
            _connect.Login(
                ref options,
                null,
                (ref LoginCallbackInfo result) => OnLogin(completion, result)
            );
            return completion.Task;
        }

        /// <summary>
        /// Creates a product identity for a first-time store account and completes sign-in.
        /// </summary>
        /// <param name="completion">The pending authentication.</param>
        /// <param name="result">The Connect login result.</param>
        private void OnLogin(
            TaskCompletionSource<ProductUserId> completion,
            LoginCallbackInfo result
        )
        {
            if (_disposed)
                return;
            if (result.ResultCode == Result.Success)
                completion.TrySetResult(result.LocalUserId);
            else if (result.ResultCode == Result.InvalidUser)
            {
                CreateUserOptions options = new() { ContinuanceToken = result.ContinuanceToken };
                _connect.CreateUser(
                    ref options,
                    null,
                    (ref CreateUserCallbackInfo created) =>
                    {
                        if (_disposed)
                            return;
                        if (created.ResultCode == Result.Success)
                            completion.TrySetResult(created.LocalUserId);
                        else if (Common.IsOperationComplete(created.ResultCode))
                            completion.TrySetException(
                                new InvalidOperationException(
                                    "Create player identity: " + created.ResultCode
                                )
                            );
                    }
                );
            }
            else if (Common.IsOperationComplete(result.ResultCode))
                completion.TrySetException(
                    new InvalidOperationException("Store multiplayer sign-in: " + result.ResultCode)
                );
        }

        /// <summary>
        /// Renews EOS authentication with the latest cached store credential.
        /// </summary>
        private async void RefreshAuthentication()
        {
            try
            {
                await LoginAsync();
            }
            catch (Exception exception)
            {
                if (!_disposed)
                    Debug.LogWarning(exception.Message);
            }
        }
    }
}
