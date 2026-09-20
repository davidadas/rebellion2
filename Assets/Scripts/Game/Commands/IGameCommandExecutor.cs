using System.Collections.Generic;

namespace Rebellion.Game.Commands
{
    /// <summary>
    /// Executes authoritative game commands without exposing their implementation owners.
    /// </summary>
    public interface IGameCommandExecutor
    {
        /// <summary>
        /// Executes one command without resolving its resulting facts.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>The command outcome.</returns>
        GameCommandResult ExecuteRaw(GameCommand command);

        /// <summary>
        /// Executes commands in authored order without resolving their resulting facts.
        /// </summary>
        /// <param name="commands">The commands to execute.</param>
        /// <returns>The produced factual results.</returns>
        List<Results.GameResult> ExecuteRaw(IEnumerable<GameCommand> commands);
    }
}
