using System;
using System.Threading.Tasks;
using Rebellion.Game.PlayerRequests;

namespace Rebellion.PlayerRequests
{
    /// <summary>
    /// Retains one session's declared execution owner and rejects requests from ended sessions.
    /// </summary>
    internal sealed class SessionPlayerRequestDispatcher : IPlayerRequestDispatcher
    {
        private readonly Func<bool> _isSessionActive;
        private readonly IPlayerRequestDispatcher _dispatcher;

        /// <summary>
        /// Connects a stable session boundary to its declared execution owner.
        /// </summary>
        /// <param name="dispatcher">The execution owner chosen during session composition.</param>
        /// <param name="isSessionActive">Whether the originating session still owns the runtime.</param>
        public SessionPlayerRequestDispatcher(
            IPlayerRequestDispatcher dispatcher,
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
        /// <param name="request">The typed request submitted by an existing window.</param>
        /// <returns>The owner's execution outcome, or rejection when submission follows session end.</returns>
        public Task<PlayerRequestOutcome> ExecuteAsync(PlayerRequest request)
        {
            return _isSessionActive()
                ? _dispatcher.ExecuteAsync(request)
                : Task.FromResult(
                    new PlayerRequestOutcome(false, "The game session is no longer active.")
                );
        }
    }
}
