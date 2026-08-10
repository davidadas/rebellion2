using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects how a persistent integer event variable is updated.
    /// </summary>
    public enum EventVariableOperation
    {
        Set,
        Add,
        Minimum,
        Maximum,
    }

    /// <summary>
    /// Executes one authored action list when its probability roll succeeds.
    /// </summary>
    [PersistableObject(Name = "RandomOutcome")]
    public class RandomOutcomeAction : GameAction
    {
        [PersistableAttribute]
        public double Probability { get; set; }

        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        public RandomOutcomeAction()
            : base() { }

        /// <summary>
        /// Rolls against the configured probability; on success, executes a uniformly-chosen
        /// child action and returns its results. Otherwise returns no results.
        /// </summary>
        /// <param name="game">The game state passed to the chosen child action.</param>
        /// <returns>The results produced by the chosen action, or an empty list if the roll failed.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (provider.NextDouble() < Probability)
            {
                return Actions[provider.NextInt(0, Actions.Count)].Execute(game, provider);
            }

            return new List<GameResult>();
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (provider.NextDouble() >= Probability)
                return new List<GameResult>();
            return Actions[provider.NextInt(0, Actions.Count)].Execute(game, provider, context);
        }
    }

    /// <summary>
    /// Executes every child action when one probability roll succeeds.
    /// </summary>
    [PersistableObject(Name = "Chance")]
    public sealed class ChanceAction : GameAction
    {
        [PersistableAttribute]
        public double Probability { get; set; }

        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) => Execute(game, game.Random);

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider) =>
            Execute(game, provider, null);

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (provider.NextDouble() >= Probability)
                return new List<GameResult>();

            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in Actions)
                results.AddRange(action.Execute(game, provider, context));
            return results;
        }
    }

    /// <summary>
    /// Defines one weighted action list within a random choice.
    /// </summary>
    [PersistableObject(Name = "Choice")]
    public sealed class RandomChoice
    {
        public int Weight { get; set; } = 1;
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    /// <summary>
    /// Selects one weighted outcome and executes every action belonging to that outcome.
    /// </summary>
    [PersistableObject(Name = "RandomChoice")]
    public sealed class RandomChoiceAction : GameAction
    {
        public List<RandomChoice> Choices { get; set; } = new List<RandomChoice>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game) => Execute(game, game.Random);

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider) =>
            Execute(game, provider, null);

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            int totalWeight = Choices.Sum(choice => choice.Weight);
            int roll = provider.NextInt(0, totalWeight);
            RandomChoice selected = null;
            foreach (RandomChoice choice in Choices)
            {
                roll -= choice.Weight;
                if (roll < 0)
                {
                    selected = choice;
                    break;
                }
            }

            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in selected.Actions)
                results.AddRange(action.Execute(game, provider, context));
            return results;
        }
    }

    /// <summary>
    /// Executes another event immediately within the current deterministic result pipeline.
    /// </summary>
    [PersistableObject(Name = "TriggerEvent")]
    public class TriggerEventAction : GameAction
    {
        public string EventInstanceID { get; set; }

        public TriggerEventAction()
            : base() { }

        /// <summary>
        /// Resolves the referenced <see cref="GameEvent"/> and runs its action chain.
        /// Falls back to <see cref="GameRoot.Random"/> if no provider has been injected.
        /// </summary>
        /// <param name="game">The game state used to resolve the event.</param>
        /// <returns>The results produced by the triggered event's actions.</returns>
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);
            return gameEvent.Execute(game, provider ?? game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);
            GameEventExecutionContext childContext =
                context == null
                    ? null
                    : new GameEventExecutionContext(
                        gameEvent,
                        context.State,
                        context.ScopeTarget,
                        context.TriggerResult
                    );
            return gameEvent.Execute(game, provider ?? game.Random, childContext);
        }
    }

    /// <summary>
    /// Executes one of two authored action lists based on data-defined conditions.
    /// </summary>
    [PersistableObject(Name = "Conditional")]
    public class ConditionalAction : GameAction
    {
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
        public List<GameAction> ElseActions { get; set; } = new List<GameAction>();

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameAction> selected = Conditionals.TrueForAll(condition => condition.IsMet(game))
                ? Actions
                : ElseActions;
            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in selected)
                results.AddRange(action.Execute(game, provider));
            return results;
        }

        public override List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            List<GameAction> selected = Conditionals.TrueForAll(condition =>
                condition.IsMet(game, context)
            )
                ? Actions
                : ElseActions;
            List<GameResult> results = new List<GameResult>();
            foreach (GameAction action in selected)
                results.AddRange(action.Execute(game, provider, context));
            return results;
        }
    }

    /// <summary>
    /// Mutates a persistent integer used to coordinate data-defined story stages.
    /// </summary>
    [PersistableObject(Name = "SetEventVariable")]
    public class SetEventVariableAction : GameAction
    {
        public string Key { get; set; }
        public EventVariableOperation Operation { get; set; }
        public int Operand { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            int previousValue = game.GetEventVariable(Key);
            int currentValue = Operation switch
            {
                EventVariableOperation.Set => Operand,
                EventVariableOperation.Add => checked(previousValue + Operand),
                EventVariableOperation.Minimum => Math.Min(previousValue, Operand),
                EventVariableOperation.Maximum => Math.Max(previousValue, Operand),
                _ => throw new InvalidOperationException(
                    $"Unsupported event variable operation '{Operation}'."
                ),
            };
            game.SetEventVariable(Key, currentValue);
            return new List<GameResult>
            {
                new EventVariableChangedResult
                {
                    Key = Key,
                    PreviousValue = previousValue,
                    CurrentValue = currentValue,
                    Tick = game.CurrentTick,
                },
            };
        }
    }
}
