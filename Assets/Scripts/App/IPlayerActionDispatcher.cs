using System;
using System.Threading.Tasks;
using Rebellion.App.PlayerActions;

namespace Rebellion.App
{
    /// <summary>
    /// Reports ordered domain execution or an explicitly unknown outcome after a connection ends.
    /// </summary>
    public interface IPlayerActionDispatcher
    {
        /// <summary>
        /// Submits a request from the player bound to this dispatcher.
        /// </summary>
        /// <param name="action">The typed player request.</param>
        /// <returns>The execution outcome, which can be unconfirmed after disconnection.</returns>
        Task<PlayerActionResult> ExecuteAsync(PlayerAction action);
    }

    /// <summary>
    /// Executes single-player requests immediately through the same player-action boundary.
    /// </summary>
    internal sealed class LocalPlayerActionDispatcher : IPlayerActionDispatcher
    {
        private readonly PlayerActionExecutor _executor;
        private readonly Func<string> _getActorFactionID;
        private readonly Action _stateChanged;

        /// <summary>
        /// Binds immediate request execution to the active session and its local player.
        /// </summary>
        /// <param name="executor">The active session's domain executor.</param>
        /// <param name="getActorFactionID">The application's local player binding.</param>
        /// <param name="stateChanged">Invalidates projections after accepted execution.</param>
        public LocalPlayerActionDispatcher(
            PlayerActionExecutor executor,
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
        /// <param name="action">The typed player request.</param>
        /// <returns>The completed execution outcome.</returns>
        public async Task<PlayerActionResult> ExecuteAsync(PlayerAction action)
        {
            PlayerActionResult result;
            try
            {
                result = await _executor.ExecuteAsync(_getActorFactionID(), action);
            }
            catch (OperationCanceledException)
            {
                return new PlayerActionResult(false, "The action was cancelled before execution.");
            }
            if (result.Accepted)
                _stateChanged?.Invoke();
            return result;
        }
    }
}
