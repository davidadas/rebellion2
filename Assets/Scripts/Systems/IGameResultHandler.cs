using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Produces domain reactions to a completed batch of game results.
    /// </summary>
    /// <typeparam name="T">The result type handled by this subscriber.</typeparam>
    public interface IGameResultHandler<T>
        where T : GameResult
    {
        /// <summary>
        /// Handles one result batch and returns newly produced results.
        /// </summary>
        /// <param name="results">The completed results to inspect.</param>
        /// <returns>The domain reactions produced by the batch.</returns>
        List<GameResult> HandleResults(IReadOnlyList<T> results);
    }
}
