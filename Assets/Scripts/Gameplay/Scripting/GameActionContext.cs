using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay.Scripting
{
    /// <summary>
    /// Supplies the dependencies available during one action execution.
    /// </summary>
    public sealed class GameActionContext
    {
        public GameRoot Game { get; }
        public IRandomNumberProvider Random { get; }
        public GameEvaluationContext Evaluation { get; }
        internal List<(
            string Name,
            string SourceEventInstanceID,
            Func<string, List<GameResult>> Execute
        )> DeferredOperations { get; } = new();
        internal List<GameResult> Results { get; } = new List<GameResult>();

        /// <summary>
        /// Creates temporary execution state without strategic command dependencies.
        /// </summary>
        /// <param name="game">The game whose objects the script addresses.</param>
        /// <param name="random">The random source used by actions.</param>
        /// <param name="evaluation">The bindings and source attribution for this activation.</param>
        public GameActionContext(
            GameRoot game,
            IRandomNumberProvider random,
            GameEvaluationContext evaluation = null
        )
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Evaluation = evaluation;
        }

        /// <summary>
        /// Queues a command call after the complete authored action list has been interpreted.
        /// </summary>
        /// <param name="name">The authored operation name used in failure diagnostics.</param>
        /// <param name="execute">The command call and the resolved inputs it retains.</param>
        internal void Defer(string name, Func<string, List<GameResult>> execute)
        {
            if (execute == null)
                return;
            DeferredOperations.Add((name, Evaluation?.SourceEventInstanceID, execute));
        }

        /// <summary>
        /// Records one factual result produced directly by the current action.
        /// </summary>
        /// <param name="result">The result.</param>
        internal void Record(GameResult result)
        {
            if (result == null)
                return;
            if (
                string.IsNullOrEmpty(result.SourceEventInstanceID)
                && !string.IsNullOrEmpty(Evaluation?.SourceEventInstanceID)
            )
                result.SourceEventInstanceID = Evaluation.SourceEventInstanceID;
            Evaluation?.AddResult(result);
            Results.Add(result);
        }

        /// <summary>
        /// Records factual results produced directly by the current action.
        /// </summary>
        /// <param name="results">The results.</param>
        internal void Record(IEnumerable<GameResult> results)
        {
            foreach (GameResult result in results ?? Enumerable.Empty<GameResult>())
                Record(result);
        }
    }

    /// <summary>
    /// Distinguishes failed command execution from invalid authored action data.
    /// </summary>
    internal sealed class GameActionCommandException : Exception
    {
        /// <summary>
        /// Creates an action-command failure.
        /// </summary>
        internal GameActionCommandException() { }

        /// <summary>
        /// Creates an action-command failure with a description.
        /// </summary>
        /// <param name="message">The failure description.</param>
        internal GameActionCommandException(string message)
            : base(message) { }

        /// <summary>
        /// Creates an action-command failure with its underlying cause.
        /// </summary>
        /// <param name="message">The failure description.</param>
        /// <param name="innerException">The underlying command failure.</param>
        internal GameActionCommandException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
