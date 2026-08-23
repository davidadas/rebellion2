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
        public abstract bool IsMet(GameConditionContext context);

        public bool IsMet(GameRoot game) => IsMet(new GameConditionContext(game));

        public bool IsMet(GameRoot game, GameResult triggerResult) =>
            IsMet(new GameConditionContext(game, triggerResult));

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

        public GameConditionContext(GameRoot game)
            : this(game, null, null) { }

        public GameConditionContext(GameRoot game, GameResult triggerResult)
            : this(game, null, triggerResult) { }

        public GameConditionContext(GameRoot game, GameEventEvaluationContext evaluation)
            : this(game, evaluation, evaluation?.TriggerResult) { }

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
