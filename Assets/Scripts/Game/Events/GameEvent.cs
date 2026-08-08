using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Persists the runtime scheduling state for one data-defined game event.
    /// Event definitions remain content; this state is the save-game-owned execution history.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventState
    {
        public bool IsInitialized { get; set; }
        public int NextEligibleTick { get; set; }
        public int ExecutionCount { get; set; }
        public int LastExecutionTick { get; set; } = -1;
    }

    /// <summary>
    /// Represents a triggered game event: a set of conditions that, when met, execute a set of actions.
    /// Execute returns the results of those actions for notification and logging.
    /// </summary>
    public class GameEvent : BaseGameEntity
    {
        public bool IsRepeatable { get; set; }
        public int InitialDelayTicks { get; set; }
        public int InitialDelayRandomTicks { get; set; }
        public int RepeatDelayTicks { get; set; }
        public int RepeatDelayRandomTicks { get; set; }
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        public GameEvent() { }

        public GameEvent(List<GameConditional> conditionals, List<GameAction> actions)
        {
            Conditionals = conditionals;
            Actions = actions;
        }

        /// <summary>
        /// Returns true if all conditions are met.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if every conditional is satisfied.</returns>
        public bool AreConditionsMet(GameRoot game)
        {
            foreach (GameConditional conditional in Conditionals)
            {
                if (!conditional.IsMet(game))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Executes the event's actions and returns all results.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">Random number provider for stochastic actions.</param>
        /// <returns>Combined results from all executed actions.</returns>
        public List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();

            foreach (GameAction action in Actions)
            {
                if (action is RandomOutcomeAction randomAction)
                    randomAction.SetRandomProvider(provider);
                else if (action is TriggerEventAction triggerAction)
                    triggerAction.SetRandomProvider(provider);

                results.AddRange(action.Execute(game));
            }

            return results;
        }
    }
}
