using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Represents a triggered game event: a set of conditions that, when met, execute a set of actions.
    /// Execute returns the results of those actions for notification and logging.
    /// </summary>
    [PersistableObject]
    public sealed class GameEvent
    {
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public int? MinimumRuns { get; set; }

        [PersistableAttribute]
        public int? MaximumRuns { get; set; }

        [PersistableAttribute]
        public bool UnlimitedRuns { get; set; }

        public int GetMinimumRuns() => MinimumRuns ?? 1;

        public int? GetMaximumRuns() => UnlimitedRuns ? null : MaximumRuns ?? MinimumRuns ?? 1;

        public void ValidateRunLimits()
        {
            int minimum = GetMinimumRuns();
            if (minimum < 1)
                throw new System.InvalidOperationException(
                    $"Event '{InstanceID}' MinimumRuns must be positive."
                );
            if (UnlimitedRuns && MaximumRuns.HasValue)
                throw new System.InvalidOperationException(
                    $"Event '{InstanceID}' cannot combine MaximumRuns with UnlimitedRuns."
                );
            if (MaximumRuns is < 1)
                throw new System.InvalidOperationException(
                    $"Event '{InstanceID}' MaximumRuns must be positive."
                );
            if (MaximumRuns.HasValue && minimum > MaximumRuns.Value)
                throw new System.InvalidOperationException(
                    $"Event '{InstanceID}' MinimumRuns cannot exceed MaximumRuns."
                );
        }

        public bool CanExecute(int completedRuns)
        {
            ValidateRunLimits();
            int? maximum = GetMaximumRuns();
            return !maximum.HasValue || completedRuns < maximum.Value;
        }

        public bool IsComplete(int completedRuns)
        {
            ValidateRunLimits();
            int? maximum = GetMaximumRuns();
            return maximum.HasValue && completedRuns >= maximum.Value;
        }

        // Result Triggers.
        public List<GameEventTrigger> Triggers { get; set; } = new List<GameEventTrigger>();

        // Schedule and Execution Pipeline.
        public GameEventScheduler Schedule { get; set; }
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public GameEventForEach ForEach { get; set; }
        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        /// <summary>
        /// Creates an empty event definition for deserialization.
        /// </summary>
        public GameEvent() { }

        /// <summary>
        /// Creates an event from an in-memory condition and action pipeline.
        /// </summary>
        /// <param name="conditionals">The conditions that must all pass.</param>
        /// <param name="actions">The actions executed in authored order.</param>
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
            return AreConditionsMet(game, (GameResult)null);
        }

        /// <summary>
        /// Returns true if all conditions accept the current game and triggering result.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="triggerResult">The result that activated this event, if any.</param>
        /// <returns>True if every conditional is satisfied.</returns>
        public bool AreConditionsMet(GameRoot game, GameResult triggerResult)
        {
            return AreConditionsMet(
                game,
                new GameEventExecutionContext(this, null, null, triggerResult)
            );
        }

        /// <summary>
        /// Returns true if all conditions accept the supplied execution context.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="context">The scoped target, trigger, state, and runtime bindings.</param>
        /// <returns>True if every conditional is satisfied.</returns>
        public bool AreConditionsMet(GameRoot game, GameEventExecutionContext context)
        {
            foreach (GameConditional conditional in Conditionals)
            {
                if (!conditional.IsMet(game, context))
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
            return Execute(game, provider, null);
        }

        /// <summary>
        /// Executes the event for one concrete global or scoped schedule.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">Random number provider for stochastic actions.</param>
        /// <param name="context">The execution state and bindings for this activation.</param>
        /// <returns>Combined results from all executed actions.</returns>
        public List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            GameActionContext actionContext = new GameActionContext(game, provider, context);
            return GameAction.ExecuteAll(Actions, actionContext);
        }
    }
}
