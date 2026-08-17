using System;
using System.Collections.Generic;
using System.Linq;
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
        internal abstract List<GameResult> Execute(GameActionContext context);

        internal static List<GameResult> ExecuteAll(
            IEnumerable<GameAction> actions,
            GameActionContext context
        )
        {
            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in actions ?? Enumerable.Empty<GameAction>())
            {
                foreach (GameResult result in action.Execute(context))
                {
                    if (result == null)
                        continue;

                    if (
                        string.IsNullOrEmpty(result.SourceEventInstanceID)
                        && context.Activation?.Event != null
                    )
                        result.SourceEventInstanceID = context.Activation.Event.InstanceID;
                    context.Activation?.AddResult(result);
                    results.Add(result);
                }
            }
            return results;
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
