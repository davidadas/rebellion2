using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Util.Logging;

namespace Rebellion.Gameplay.Scripting
{
    /// <summary>
    /// Interprets authored branching and action sequences using a supplied gameplay executor.
    /// </summary>
    public static class GameActionInterpreter
    {
        /// <summary>
        /// Interprets one authored action in the supplied activation without catching failures.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The activation's state, random source, and pending work.</param>
        /// <param name="executor">Executes individual gameplay actions.</param>
        public static void ExecuteAction(
            GameAction action,
            GameActionContext context,
            IGameActionExecutor executor
        )
        {
            switch (action)
            {
                case RollOutcomeAction definition:
                    Execute(definition, context, executor);
                    break;
                case RollChanceAction definition:
                    Execute(definition, context, executor);
                    break;
                case IfAction definition:
                    Execute(definition, context, executor);
                    break;
                case PerformSkillCheckAction definition:
                    Execute(definition, context, executor);
                    break;
                case null:
                    throw new NullReferenceException();
                default:
                    executor.Execute(action, context);
                    break;
            }
        }

        /// <summary>
        /// Executes authored actions in order, logging each failure and continuing the activation.
        /// </summary>
        /// <param name="actions">The ordered action definitions.</param>
        /// <param name="context">The shared activation state.</param>
        /// <param name="executor">Executes individual gameplay actions.</param>
        public static void ExecuteActions(
            IEnumerable<GameAction> actions,
            GameActionContext context,
            IGameActionExecutor executor
        )
        {
            foreach (GameAction action in actions ?? Enumerable.Empty<GameAction>())
            {
                try
                {
                    ExecuteAction(action, context, executor);
                }
                catch (GameActionCommandException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    string eventInstanceId = context?.Evaluation?.SourceID ?? "unknown";
                    string actionName = action?.GetType().Name ?? "null";
                    GameLogger.Log(
                        $"{context?.Evaluation?.SourceKind ?? "Event"} '{eventInstanceId}' action '{actionName}' failed: {exception}",
                        GameLogger.LogLevel.Error
                    );
                }
            }
        }

        /// <summary>
        /// Executes one eligible outcome selected by its authored weight.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        /// <param name="executor">Executes individual gameplay actions.</param>
        private static void Execute(
            RollOutcomeAction action,
            GameActionContext context,
            IGameActionExecutor executor
        )
        {
            List<RandomOutcome> eligible = action
                .Outcomes.Where(outcome =>
                    outcome.Weight > 0
                    && outcome.Conditionals.All(condition =>
                        GameConditionEvaluator.IsMet(condition, context.Game, context.Evaluation)
                    )
                )
                .ToList();
            if (eligible.Count == 0)
                return;

            int roll = context.Random.NextInt(0, eligible.Sum(outcome => outcome.Weight));
            RandomOutcome selected = null;
            foreach (RandomOutcome outcome in eligible)
            {
                roll -= outcome.Weight;
                if (roll < 0)
                {
                    selected = outcome;
                    break;
                }
            }

            ExecuteActions(selected.Actions, context, executor);
            return;
        }

        /// <summary>
        /// Executes the authored actions when the resolved probability accepts the random roll.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        /// <param name="executor">Executes individual gameplay actions.</param>
        private static void Execute(
            RollChanceAction action,
            GameActionContext context,
            IGameActionExecutor executor
        )
        {
            double probability = ResolveProbability(action, context);
            if (double.IsNaN(probability) || probability < 0 || probability > 1)
                throw new InvalidOperationException(
                    "RollChance Probability must be between zero and one."
                );
            if (context.Random.NextDouble() < probability)
                ExecuteActions(action.Actions, context, executor);
        }

        /// <summary>
        /// Resolves exactly one fixed, bound, or rolled probability.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The current action execution context.</param>
        /// <returns>The normalized probability.</returns>
        private static double ResolveProbability(RollChanceAction action, GameActionContext context)
        {
            return GameActionNumericValue.ResolveDouble(
                action.Probability,
                action.ProbabilityBinding,
                action.RollDouble,
                context,
                "RollChance",
                "Probability"
            );
        }

        /// <summary>
        /// Executes the authored success or fallback actions for the current conditions.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        /// <param name="executor">Executes individual gameplay actions.</param>
        private static void Execute(
            IfAction action,
            GameActionContext context,
            IGameActionExecutor executor
        )
        {
            IEnumerable<GameAction> selected = action.Conditionals.TrueForAll(condition =>
                GameConditionEvaluator.IsMet(condition, context.Game, context.Evaluation)
            )
                ? action.Actions
                : action.Else;
            ExecuteActions(selected, context, executor);
            return;
        }

        /// <summary>
        /// Performs the authored officer skill check and executes its matching branch.
        /// </summary>
        /// <param name="action">The authored action definition.</param>
        /// <param name="context">The context.</param>
        /// <param name="executor">Executes individual gameplay actions.</param>
        private static void Execute(
            PerformSkillCheckAction action,
            GameActionContext context,
            IGameActionExecutor executor
        )
        {
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>(
                action.OfficerInstanceID,
                includeDisabled: true
            );
            if (officer == null)
                throw new InvalidOperationException(
                    $"PerformSkillCheck could not resolve officer '{action.OfficerInstanceID}'."
                );
            if (action.Rating == SkillRating.None)
                throw new InvalidOperationException("PerformSkillCheck requires a rating.");
            if (action.RatingMultiplier == 0)
                throw new InvalidOperationException(
                    "PerformSkillCheck RatingMultiplier cannot be zero."
                );

            GameConfig.MissionProbabilityTablesConfig tables = context
                .Game
                .Config
                ?.ProbabilityTables
                ?.Mission;
            if (tables?.GetSuccessTable(action.ProbabilityTable) == null)
                throw new InvalidOperationException(
                    $"PerformSkillCheck could not resolve probability table '{action.ProbabilityTable}'."
                );

            int probability = tables.GetSuccessProbability(
                action.ProbabilityTable,
                checked(officer.GetEffectiveRating(action.Rating) * action.RatingMultiplier)
            );
            bool succeeded = context.Random.NextDouble() * 100 < probability;
            IEnumerable<GameAction> actions = succeeded ? action.OnSuccess : action.OnFailure;
            ExecuteActions(actions, context, executor);
            return;
        }
    }
}
