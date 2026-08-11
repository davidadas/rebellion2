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
    /// Contains conditions nested beneath a weighted outcome.
    /// </summary>
    [PersistableObject]
    public sealed class GameConditionBlock
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
    }

    /// <summary>
    /// Defines one eligible weighted action outcome.
    /// </summary>
    [PersistableObject(Name = "Outcome")]
    public sealed class RandomOutcome
    {
        [PersistableAttribute]
        public int Weight { get; set; } = 1;

        public GameConditionBlock When { get; set; }

        [PersistableInlineCollection]
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    /// <summary>
    /// Selects one weighted outcome and executes every action belonging to that outcome.
    /// </summary>
    [PersistableObject(Name = "Random")]
    public sealed class RandomAction : GameAction
    {
        [PersistableInlineCollection]
        public List<RandomOutcome> Outcomes { get; set; } = new List<RandomOutcome>();

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

            List<RandomOutcome> eligible = Outcomes
                .Where(outcome =>
                    outcome.Weight > 0
                    && outcome.When?.Conditionals.All(condition => condition.IsMet(game, context))
                        != false
                )
                .ToList();
            if (eligible.Count == 0)
                return new List<GameResult>();

            int totalWeight = eligible.Sum(outcome => outcome.Weight);
            int roll = provider.NextInt(0, totalWeight);
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
    /// Contains actions nested beneath a control-flow branch.
    /// </summary>
    [PersistableObject]
    public sealed class GameActionBlock
    {
        [PersistableInlineCollection]
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    /// <summary>
    /// Executes one of two authored action lists based on data-defined conditions.
    /// </summary>
    [PersistableObject(Name = "If")]
    public class IfAction : GameAction
    {
        [PersistableInlineCollection]
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();

        public GameActionBlock Then { get; set; } = new GameActionBlock();
        public GameActionBlock Else { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            return Execute(game, game.Random);
        }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            List<GameAction> selected = Conditionals.TrueForAll(condition => condition.IsMet(game))
                ? Then.Actions
                : Else?.Actions ?? new List<GameAction>();
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
                ? Then.Actions
                : Else?.Actions ?? new List<GameAction>();
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
