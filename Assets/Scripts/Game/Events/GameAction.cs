using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines an operation that changes game state during an event activation.
    /// </summary>
    [PersistableObject]
    public abstract class GameAction
    {
        /// <summary>
        /// Executes the action within one event activation.
        /// </summary>
        /// <param name="context">The context.</param>
        internal abstract void Execute(GameActionContext context);

        /// <summary>
        /// Executes an ordered action collection, logging failed actions before continuing with
        /// the remaining work.
        /// </summary>
        /// <param name="actions">The actions to execute in authored order.</param>
        /// <param name="context">The shared event activation context.</param>
        internal static void ExecuteAll(IEnumerable<GameAction> actions, GameActionContext context)
        {
            foreach (GameAction action in actions ?? Enumerable.Empty<GameAction>())
            {
                try
                {
                    action.Execute(context);
                }
                catch (GameActionSystemException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    string eventInstanceId = context?.Evaluation?.Event?.InstanceID ?? "unknown";
                    string actionName = action?.GetType().Name ?? "null";
                    GameLogger.Log(
                        $"Event '{eventInstanceId}' action '{actionName}' failed: {exception}",
                        GameLogger.LogLevel.Error
                    );
                }
            }
        }
    }

    /// <summary>
    /// Supplies the dependencies available during one action execution.
    /// </summary>
    public sealed class GameActionContext
    {
        public GameRoot Game { get; }
        public IRandomNumberProvider Random { get; }
        public GameEventEvaluationContext Evaluation { get; }
        public UnitFactory UnitFactory { get; }
        internal List<GameRequest> Requests { get; } = new List<GameRequest>();
        internal List<GameResult> Results { get; } = new List<GameResult>();
        private readonly Func<IReadOnlyList<Officer>, List<GameResult>> _captureMissionInterruptor;

        /// <summary>
        /// Initializes a new instance of the GameActionContext class.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="random">The random.</param>
        /// <param name="evaluation">The evaluation.</param>
        /// <param name="unitFactory">The unit factory.</param>
        /// <param name="captureMissionInterruptor">Interrupts missions containing newly captured officers.</param>
        public GameActionContext(
            GameRoot game,
            IRandomNumberProvider random,
            GameEventEvaluationContext evaluation = null,
            UnitFactory unitFactory = null,
            Func<IReadOnlyList<Officer>, List<GameResult>> captureMissionInterruptor = null
        )
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Evaluation = evaluation;
            UnitFactory = unitFactory;
            _captureMissionInterruptor = captureMissionInterruptor;
        }

        /// <summary>
        /// Adds authoritative work requested by the current action.
        /// </summary>
        /// <param name="request">The request.</param>
        internal void Request(GameRequest request)
        {
            if (request == null)
                return;
            if (string.IsNullOrEmpty(request.SourceEventInstanceID) && Evaluation?.Event != null)
                request.SourceEventInstanceID = Evaluation.Event.InstanceID;
            Requests.Add(request);
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
        /// Interrupts active missions containing newly captured officers before the next authored
        /// action executes.
        /// </summary>
        /// <param name="officers">The newly captured officers.</param>
        internal void InterruptMissionsForCapture(IReadOnlyList<Officer> officers)
        {
            List<GameResult> interruptionResults;
            try
            {
                interruptionResults = _captureMissionInterruptor?.Invoke(officers);
            }
            catch (Exception exception)
            {
                throw new GameActionSystemException(
                    "Failed to interrupt missions for captured officers.",
                    exception
                );
            }
            if (interruptionResults != null)
                Results.AddRange(interruptionResults.Where(result => result != null));
        }
    }

    /// <summary>
    /// Distinguishes failed system work from an invalid authored action so event execution stops.
    /// </summary>
    internal sealed class GameActionSystemException : Exception
    {
        /// <summary>
        /// Initializes an exception raised by system work during an event action.
        /// </summary>
        internal GameActionSystemException() { }

        /// <summary>
        /// Initializes an exception raised by system work during an event action.
        /// </summary>
        /// <param name="message">The failure description.</param>
        internal GameActionSystemException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes an exception raised by system work during an event action.
        /// </summary>
        /// <param name="message">The failure description.</param>
        /// <param name="innerException">The underlying system failure.</param>
        internal GameActionSystemException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
