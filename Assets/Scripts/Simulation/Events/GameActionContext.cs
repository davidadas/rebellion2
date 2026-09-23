using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Supplies the dependencies available during one action execution.
    /// </summary>
    public sealed class GameActionContext
    {
        public GameRoot Game { get; }
        public IRandomNumberProvider Random { get; }
        public GameEventEvaluationContext Evaluation { get; }
        public UnitFactory UnitFactory { get; }
        internal List<(
            string Name,
            string SourceEventInstanceID,
            Func<GameEventExecutor, string, List<GameResult>> Execute
        )> DeferredOperations { get; } = new();
        internal List<GameResult> Results { get; } = new List<GameResult>();
        private readonly MissionCommands _missionCommands;

        /// <summary>
        /// Initializes a new instance of the GameActionContext class.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="random">The random.</param>
        /// <param name="evaluation">The evaluation.</param>
        /// <param name="unitFactory">The unit factory.</param>
        /// <param name="missionCommands">The mission operations required by ordered capture actions.</param>
        public GameActionContext(
            GameRoot game,
            IRandomNumberProvider random,
            GameEventEvaluationContext evaluation = null,
            UnitFactory unitFactory = null,
            MissionCommands missionCommands = null
        )
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Evaluation = evaluation;
            UnitFactory = unitFactory;
            _missionCommands = missionCommands;
        }

        /// <summary>
        /// Queues a command call after the complete authored action list has been interpreted.
        /// </summary>
        /// <param name="name">The authored operation name used in failure diagnostics.</param>
        /// <param name="execute">The command call and the resolved inputs it retains.</param>
        internal void Defer(string name, Func<GameEventExecutor, string, List<GameResult>> execute)
        {
            if (execute == null)
                return;
            DeferredOperations.Add((name, Evaluation?.Event?.InstanceID, execute));
        }

        /// <summary>
        /// Records one factual result produced directly by the current action.
        /// </summary>
        /// <param name="result">The result.</param>
        internal void Record(GameResult result)
        {
            if (result == null)
                return;
            if (string.IsNullOrEmpty(result.SourceEventInstanceID) && Evaluation?.Event != null)
                result.SourceEventInstanceID = Evaluation.Event.InstanceID;
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

        /// <summary>
        /// Interrupts missions containing newly captured officers before the next authored action.
        /// </summary>
        /// <param name="officers">The newly captured officers.</param>
        internal void InterruptMissionsForCapture(IReadOnlyList<Officer> officers)
        {
            if (_missionCommands == null)
                return;

            try
            {
                Results.AddRange(
                    _missionCommands
                        .InterruptMissionsForCapturedOfficers(officers)
                        .Where(result => result != null)
                );
            }
            catch (Exception exception)
            {
                throw new GameActionCommandException(
                    "Failed to interrupt missions for captured officers.",
                    exception
                );
            }
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
