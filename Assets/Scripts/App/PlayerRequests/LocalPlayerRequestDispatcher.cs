using System;
using System.Threading.Tasks;
using Rebellion.Game.PlayerRequests;
using Rebellion.Gameplay.PlayerRequests;

namespace Rebellion.App.PlayerRequests
{
    /// <summary>
    /// Executes single-player requests immediately through the same player-request boundary.
    /// </summary>
    internal sealed class LocalPlayerRequestDispatcher : IPlayerRequestDispatcher
    {
        private readonly PlayerRequestExecutor _executor;
        private readonly Func<string> _getActorFactionID;
        private readonly Action _stateChanged;

        /// <summary>
        /// Binds immediate request execution to the active session and its local player.
        /// </summary>
        /// <param name="executor">The active session's domain executor.</param>
        /// <param name="getActorFactionID">The application's local player binding.</param>
        /// <param name="stateChanged">Invalidates projections after accepted execution.</param>
        public LocalPlayerRequestDispatcher(
            PlayerRequestExecutor executor,
            Func<string> getActorFactionID,
            Action stateChanged = null
        )
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _getActorFactionID =
                getActorFactionID ?? throw new ArgumentNullException(nameof(getActorFactionID));
            _stateChanged = stateChanged;
        }

        /// <summary>
        /// Resolves the local actor and completes after execution at a writable session boundary.
        /// </summary>
        /// <param name="request">The typed player request.</param>
        /// <returns>The completed execution outcome.</returns>
        public async Task<PlayerRequestOutcome> ExecuteAsync(PlayerRequest request)
        {
            PlayerRequestOutcome result;
            try
            {
                result = await _executor.ExecuteAsync(_getActorFactionID(), request);
            }
            catch (OperationCanceledException)
            {
                return new PlayerRequestOutcome(
                    false,
                    "The request was cancelled before execution."
                );
            }
            if (result.Accepted)
                _stateChanged?.Invoke();
            return result;
        }
    }
}
