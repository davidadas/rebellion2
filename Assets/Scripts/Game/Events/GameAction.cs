using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Separates authoritative work requests from factual results produced directly by an action.
    /// </summary>
    public sealed class GameActionExecution
    {
        public List<GameRequest> Requests { get; } = new List<GameRequest>();
        public List<GameResult> Results { get; } = new List<GameResult>();

        /// <summary>
        /// Wraps an existing result collection as an action execution.
        /// </summary>
        public static implicit operator GameActionExecution(List<GameResult> results)
        {
            GameActionExecution execution = new GameActionExecution();
            if (results != null)
                execution.Results.AddRange(results.Where(result => result != null));
            return execution;
        }

        /// <summary>
        /// Creates an execution containing one authoritative request.
        /// </summary>
        internal static GameActionExecution FromRequest(GameRequest request)
        {
            GameActionExecution execution = new GameActionExecution();
            if (request != null)
                execution.Requests.Add(request);
            return execution;
        }
    }

    /// <summary>
    /// Defines an operation that changes game state during an event activation.
    /// </summary>
    [PersistableObject]
    public abstract class GameAction
    {
        /// <summary>
        /// Executes the action within one event activation.
        /// </summary>
        internal abstract GameActionExecution Execute(GameActionContext context);

        /// <summary>
        /// Executes an ordered action collection and combines its requested work and factual results.
        /// </summary>
        internal static GameActionExecution ExecuteAll(
            IEnumerable<GameAction> actions,
            GameActionContext context
        )
        {
            GameActionExecution execution = new GameActionExecution();
            foreach (GameAction action in actions ?? Enumerable.Empty<GameAction>())
            {
                GameActionExecution actionExecution = action.Execute(context);
                foreach (GameRequest request in actionExecution.Requests)
                {
                    if (
                        string.IsNullOrEmpty(request.SourceEventInstanceID)
                        && context.Activation?.Event != null
                    )
                        request.SourceEventInstanceID = context.Activation.Event.InstanceID;
                    execution.Requests.Add(request);
                }
                foreach (GameResult result in actionExecution.Results)
                {
                    if (result == null)
                        continue;

                    if (
                        string.IsNullOrEmpty(result.SourceEventInstanceID)
                        && context.Activation?.Event != null
                    )
                        result.SourceEventInstanceID = context.Activation.Event.InstanceID;
                    context.Activation?.AddResult(result);
                    execution.Results.Add(result);
                }
            }
            return execution;
        }
    }

    /// <summary>
    /// Supplies the dependencies available during one action execution.
    /// </summary>
    public sealed class GameActionContext
    {
        public GameRoot Game { get; }
        public IRandomNumberProvider Random { get; }
        public GameEventExecutionContext Activation { get; }
        public UnitFactory UnitFactory { get; }

        public GameActionContext(
            GameRoot game,
            IRandomNumberProvider random,
            GameEventExecutionContext activation = null,
            UnitFactory unitFactory = null
        )
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Activation = activation;
            UnitFactory = unitFactory;
        }
    }
}
