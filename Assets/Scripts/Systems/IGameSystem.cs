using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Common interface for all tick-based game systems.
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>
        /// Processes one game tick for the system.
        /// </summary>
        /// <returns>Results produced during the tick.</returns>
        List<GameResult> ProcessTick();
    }

    /// <summary>
    /// Produces domain reactions to a completed batch of game results.
    /// </summary>
    public interface IGameResultHandler
    {
        /// <summary>
        /// Handles one result batch and returns newly produced results.
        /// </summary>
        /// <param name="results">The completed results to inspect.</param>
        /// <returns>The domain reactions produced by the batch.</returns>
        List<GameResult> HandleResults(IReadOnlyList<GameResult> results);
    }
}
