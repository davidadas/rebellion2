using System;
using System.Threading.Tasks;
using Rebellion.App.PlayerActions;

namespace Rebellion.App
{
    /// <summary>
    /// Retains one session's declared execution owner and rejects requests from ended sessions.
    /// </summary>
    internal sealed class SessionPlayerActionDispatcher : IPlayerActionDispatcher
    {
        private readonly Func<bool> _isSessionActive;
        private readonly IPlayerActionDispatcher _dispatcher;

        /// <summary>
        /// Connects a stable session boundary to its declared execution owner.
        /// </summary>
        /// <param name="dispatcher">The execution owner chosen during session composition.</param>
        /// <param name="isSessionActive">Whether the originating session still owns the runtime.</param>
        public SessionPlayerActionDispatcher(
            IPlayerActionDispatcher dispatcher,
            Func<bool> isSessionActive
        )
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _isSessionActive =
                isSessionActive ?? throw new ArgumentNullException(nameof(isSessionActive));
        }

        /// <summary>
        /// Forwards a current session's request to its execution owner without applying it locally.
        /// </summary>
        /// <param name="action">The typed request submitted by an existing window.</param>
        /// <returns>The owner's execution outcome, or rejection after the originating session ends.</returns>
        public Task<PlayerActionResult> ExecuteAsync(PlayerAction action)
        {
            return _isSessionActive()
                ? _dispatcher.ExecuteAsync(action)
                : Task.FromResult(
                    new PlayerActionResult(false, "The game session is no longer active.")
                );
        }
    }
}
