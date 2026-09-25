using System;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
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
