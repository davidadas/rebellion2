using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Represents a condition that must be met for an event.
    /// </summary>
    /// <remarks>
    /// Conditions are critical to the event system, as they determine when an event is
    /// eligible to be executed. Conditions are evaluated at the time the event is scheduled
    /// to occur, and if all conditions are met, the event is executed.
    /// </remarks>
    [PersistableObject]
    public abstract class GameConditional : BaseGameEntity
    {
        /// <summary>
        /// Determines whether the condition is met in the specified game.
        /// </summary>
        /// <param name="game">The game instance to evaluate.</param>
        /// <returns>True if the condition is met; otherwise, false.</returns>
        public abstract bool IsMet(GameRoot game);

        /// <summary>
        /// Evaluates this condition with the result that triggered the event, when present.
        /// </summary>
        /// <param name="game">The game instance to evaluate.</param>
        /// <param name="triggerResult">The result that activated the event, if any.</param>
        /// <returns>True when the condition is met.</returns>
        public virtual bool IsMet(GameRoot game, GameResult triggerResult)
        {
            return IsMet(game);
        }

        /// <summary>
        /// Evaluates this condition against the complete event execution context.
        /// </summary>
        /// <param name="game">The game instance to evaluate.</param>
        /// <param name="context">The current target, trigger, state, and runtime bindings.</param>
        /// <returns>True when the condition is met.</returns>
        public virtual bool IsMet(GameRoot game, GameEventExecutionContext context)
        {
            return IsMet(game, context?.TriggerResult);
        }
    }

    /// <summary>
    /// Represents a condition that can only match the result that triggered an event.
    /// </summary>
    public abstract class GameResultConditional : GameConditional
    {
        /// <inheritdoc />
        public sealed override bool IsMet(GameRoot game) => false;

        /// <inheritdoc />
        public sealed override bool IsMet(GameRoot game, GameResult triggerResult) =>
            triggerResult != null && IsMatch(game, triggerResult);

        /// <summary>
        /// Determines whether the triggering result matches this condition.
        /// </summary>
        protected abstract bool IsMatch(GameRoot game, GameResult triggerResult);
    }
}
