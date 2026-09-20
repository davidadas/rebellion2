using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Game.Commands
{
    /// <summary>
    /// Reports whether a gameplay command was accepted and the facts it produced.
    /// </summary>
    public sealed class GameCommandResult
    {
        /// <summary>
        /// Gets whether the command was accepted.
        /// </summary>
        public bool Accepted { get; }

        /// <summary>
        /// Gets the factual results produced by the command.
        /// </summary>
        public IReadOnlyList<GameResult> Results { get; }

        /// <summary>
        /// Creates a command result.
        /// </summary>
        /// <param name="accepted">Whether the command was accepted.</param>
        /// <param name="results">The factual results produced by the command.</param>
        public GameCommandResult(bool accepted, IReadOnlyList<GameResult> results)
        {
            Accepted = accepted;
            Results = results ?? Array.Empty<GameResult>();
        }
    }
}
