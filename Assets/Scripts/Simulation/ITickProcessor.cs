using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances one simulation feature through the current game tick.
    /// </summary>
    internal interface ITickProcessor
    {
        /// <summary>
        /// Advances the owned feature through the current tick.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>The gameplay results produced while advancing the feature.</returns>
        IReadOnlyList<GameResult> ProcessTick(GameRoot game);
    }
}
