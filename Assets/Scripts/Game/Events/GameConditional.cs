using System;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Represents a condition evaluated within one game-event activation.
    /// </summary>
    [PersistableObject]
    public abstract class GameConditional : BaseGameEntity
    {
        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        public abstract bool IsMet(GameConditionContext context);

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        public bool IsMet(GameRoot game) => IsMet(new GameConditionContext(game));

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="triggerResult">The trigger result.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        public bool IsMet(GameRoot game, GameResult triggerResult) =>
            IsMet(new GameConditionContext(game, triggerResult));

        /// <summary>
        /// Checks whether the condition is met.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="evaluation">The evaluation.</param>
        /// <returns>True when the condition is met; otherwise false.</returns>
        internal bool IsMet(GameRoot game, GameEventEvaluationContext evaluation) =>
            IsMet(new GameConditionContext(game, evaluation));
    }

    /// <summary>
    /// Supplies the dependencies available during one condition evaluation.
    /// </summary>
    public sealed class GameConditionContext
    {
        public GameRoot Game { get; }
        public GameEventEvaluationContext Evaluation { get; }
        public GameResult TriggerResult { get; }
        public IRandomNumberProvider Random { get; }

        /// <summary>
        /// Initializes a new instance of the GameConditionContext class.
        /// </summary>
        /// <param name="game">The game.</param>
        public GameConditionContext(GameRoot game)
            : this(game, null, null) { }

        /// <summary>
        /// Initializes a new instance of the GameConditionContext class.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="triggerResult">The trigger result.</param>
        public GameConditionContext(GameRoot game, GameResult triggerResult)
            : this(game, null, triggerResult) { }

        /// <summary>
        /// Initializes a new instance of the GameConditionContext class.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="evaluation">The evaluation.</param>
        public GameConditionContext(GameRoot game, GameEventEvaluationContext evaluation)
            : this(game, evaluation, evaluation?.TriggerResult) { }

        /// <summary>
        /// Initializes a new instance of the GameConditionContext class.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="evaluation">The evaluation.</param>
        /// <param name="triggerResult">The trigger result.</param>
        private GameConditionContext(
            GameRoot game,
            GameEventEvaluationContext evaluation,
            GameResult triggerResult
        )
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Evaluation = evaluation;
            TriggerResult = triggerResult;
            Random = game.Random;
        }
    }
}
