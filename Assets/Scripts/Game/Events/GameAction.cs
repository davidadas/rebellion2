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
    /// Defines an operation that changes game state during an event activation.
    /// </summary>
    [PersistableObject]
    public abstract class GameAction
    {
        /// <summary>
        /// Executes the action within one event activation.
        /// </summary>
        internal abstract void Execute(GameActionContext context);

        /// <summary>
        /// Executes an ordered action collection against one shared action context.
        /// </summary>
        internal static void ExecuteAll(IEnumerable<GameAction> actions, GameActionContext context)
        {
            foreach (GameAction action in actions ?? Enumerable.Empty<GameAction>())
                action.Execute(context);
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
        internal List<GameRequest> Requests { get; } = new List<GameRequest>();
        internal List<GameResult> Results { get; } = new List<GameResult>();

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

        /// <summary>
        /// Adds authoritative work requested by the current action.
        /// </summary>
        internal void Request(GameRequest request)
        {
            if (request == null)
                return;
            if (string.IsNullOrEmpty(request.SourceEventInstanceID) && Activation?.Event != null)
                request.SourceEventInstanceID = Activation.Event.InstanceID;
            Requests.Add(request);
        }

        /// <summary>
        /// Records one factual result produced directly by the current action.
        /// </summary>
        internal void Record(GameResult result)
        {
            if (result == null)
                return;
            if (string.IsNullOrEmpty(result.SourceEventInstanceID) && Activation?.Event != null)
                result.SourceEventInstanceID = Activation.Event.InstanceID;
            Activation?.AddResult(result);
            Results.Add(result);
        }

        /// <summary>
        /// Records factual results produced directly by the current action.
        /// </summary>
        internal void Record(IEnumerable<GameResult> results)
        {
            foreach (GameResult result in results ?? Enumerable.Empty<GameResult>())
                Record(result);
        }
    }
}
