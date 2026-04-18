using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Common interface for all tick-based game systems.
    /// </summary>
    public interface IGameSystem
    {
        List<GameResult> ProcessTick();
    }
}
