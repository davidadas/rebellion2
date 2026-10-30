using System.Threading.Tasks;
using Rebellion.Game.PlayerRequests;

namespace Rebellion.Gameplay.PlayerRequests
{
    /// <summary>
    /// Reports ordered domain execution or an explicitly unknown outcome after a connection ends.
    /// </summary>
    public interface IPlayerRequestDispatcher
    {
        /// <summary>
        /// Submits a request from the player bound to this dispatcher.
        /// </summary>
        /// <param name="request">The typed player request.</param>
        /// <returns>The execution outcome, which can be unconfirmed after disconnection.</returns>
        Task<PlayerRequestOutcome> ExecuteAsync(PlayerRequest request);
    }
}
