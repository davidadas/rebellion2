using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Owns the browser conversation separately from the two-player room transcript.
    /// </summary>
    public sealed partial class MultiplayerCoordinator
    {
        private readonly List<MultiplayerChatMessage> _generalChat = new();
        private EpicMultiplayerBackend _chatBackend;

        /// <summary>
        /// Gets the general conversation received by this signed-in client.
        /// </summary>
        public IReadOnlyList<MultiplayerChatMessage> GeneralChat => _generalChat;

        /// <summary>
        /// Gets whether general conversation can currently be sent.
        /// </summary>
        public bool IsGeneralChatConnected => _chatBackend?.IsGeneralChatConnected == true;

        /// <summary>
        /// Gets the current number of participants in the browser conversation.
        /// </summary>
        public int GeneralChatMemberCount => _chatBackend?.GeneralChatMemberCount ?? 0;

        /// <summary>
        /// Opens online browser conversation using the already cached store identity.
        /// </summary>
        /// <returns>The pending general-channel admission.</returns>
        public async Task OpenGeneralChatAsync()
        {
            var cancellationToken = _sessionCancellation.Token;
            try
            {
                EpicMultiplayerBackend backend = await _onlineClient.ConnectAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (!_browserOpen)
                    return;
                _backend = backend;
                if (!ReferenceEquals(_chatBackend, _backend))
                {
                    if (_chatBackend != null)
                        _chatBackend.GeneralChatReceived -= ReceiveGeneralChat;
                    _chatBackend = _backend;
                    _chatBackend.GeneralChatReceived += ReceiveGeneralChat;
                    _generalChat.Clear();
                }
                await _chatBackend.OpenGeneralChatAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (!_browserOpen)
                    return;
                StateChanged?.Invoke();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested || !_browserOpen)
                    return;
                ReceiveGeneralChat(
                    new MultiplayerChatMessage
                    {
                        isSystem = true,
                        text = "General chat unavailable: " + exception.Message,
                    }
                );
            }
        }

        /// <summary>
        /// Sends a plain-text browser message and records it only after service acceptance.
        /// </summary>
        /// <param name="text">The player's entered message.</param>
        /// <returns>True when the message was accepted for delivery.</returns>
        public bool SendGeneralChat(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !IsGeneralChatConnected || PlayerSession == null)
                return false;
            try
            {
                MultiplayerChatMessage message = new MultiplayerChatMessage
                {
                    sender = PlayerSession.DisplayName,
                    text = text.Trim(),
                };
                _chatBackend.SendGeneralChat(message);
                ReceiveGeneralChat(message);
                return true;
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message);
                return false;
            }
        }

        /// <summary>
        /// Appends a received general message and notifies the native browser.
        /// </summary>
        /// <param name="message">The admitted channel message.</param>
        private void ReceiveGeneralChat(MultiplayerChatMessage message)
        {
            _generalChat.Add(message);
            StateChanged?.Invoke();
        }
    }
}
