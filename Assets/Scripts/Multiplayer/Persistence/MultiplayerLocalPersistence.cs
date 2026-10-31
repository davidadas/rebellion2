using System;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Gameplay;
using Rebellion.Persistence;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Publishes a survivor's settled local saves while shared loading and simulation remain suspended.
    /// </summary>
    internal sealed class MultiplayerLocalPersistence : IGamePersistenceCoordinator
    {
        private readonly GameRuntime _runtime;
        private readonly GameSession _session;
        private readonly MultiplayerRole _role;
        private readonly Action _checkpointPublished;

        /// <summary>
        /// Gets whether the retained campaign has reached a permitted ordinary-save boundary.
        /// </summary>
        public bool CanRequestSave =>
            ReferenceEquals(_runtime.GetActiveGameSession(), _session) && _runtime.CanSave;

        /// <summary>
        /// Retains the surviving session and its original participant namespace.
        /// </summary>
        /// <param name="runtime">The runtime retaining the paused campaign.</param>
        /// <param name="role">The participant's original simulation role.</param>
        /// <param name="checkpointPublished">Requests a recovery refresh after a durable local save.</param>
        public MultiplayerLocalPersistence(
            GameRuntime runtime,
            MultiplayerRole role,
            Action checkpointPublished
        )
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _session =
                runtime.GetActiveGameSession()
                ?? throw new InvalidOperationException("An active campaign is required.");
            if (role != MultiplayerRole.Host && role != MultiplayerRole.Guest)
                throw new ArgumentOutOfRangeException(nameof(role));
            _role = role;
            _checkpointPublished = checkpointPublished;
        }

        /// <summary>
        /// Writes this participant's copy without waiting for an absent peer's acknowledgment.
        /// </summary>
        /// <param name="fileName">The logical identifier or an existing multiplayer copy identifier.</param>
        /// <param name="displayName">The requested label, or null for the slot default.</param>
        /// <returns>True after the settled local save has been durably published.</returns>
        public async Task<bool> SaveGameAsync(string fileName, string displayName)
        {
            if (!CanRequestSave || string.IsNullOrWhiteSpace(fileName))
                return false;
            GameRoot game = _session.Game;
            string localFileName = SaveGameManager.GetMultiplayerSaveFileName(
                game.Metadata.CampaignID,
                _role == MultiplayerRole.Host,
                fileName
            );
            // A local publication must not claim to match an older copy on the absent peer.
            game.Metadata.SharedSaveToken = Guid.NewGuid().ToString("N");
            bool saved = await _runtime.SaveGameLocallyAsync(localFileName, displayName);
            if (saved && ReferenceEquals(_runtime.GetActiveGameSession(), _session))
                _checkpointPublished?.Invoke();
            return saved;
        }

        /// <summary>
        /// Keeps scene replacement unavailable until both participants have reconnected.
        /// </summary>
        /// <param name="fileName">The requested save identifier.</param>
        /// <returns>False while persistence belongs to the surviving participant alone.</returns>
        public Task<bool> LoadGameAsync(string fileName) => Task.FromResult(false);

        /// <summary>
        /// Leaves automatic recovery to the disconnect checkpoint while the clock is suspended.
        /// </summary>
        /// <param name="settings">The configured automatic-save preferences.</param>
        public void ProcessAutosaveTick(UserGameplaySettings settings) { }
    }
}
