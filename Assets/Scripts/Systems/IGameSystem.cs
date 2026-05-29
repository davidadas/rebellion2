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
}
