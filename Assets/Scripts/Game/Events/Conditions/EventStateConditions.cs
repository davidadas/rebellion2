using System;
using System.Collections;
using System.Linq;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects the comparison applied to a persistent event variable.
    /// </summary>
    public enum EventVariableComparison
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the current tick count satisfies a comparison against a target value.
    /// </summary>
    [PersistableObject(Name = "TickCount")]
    public class TickCountConditional : GameConditional
    {
        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public int Ticks { get; set; }

        /// <summary>
        /// Compares the current tick against the authored tick count.
        /// </summary>
        /// <param name="game">The game state providing the current tick.</param>
        /// <returns>True when the tick comparison holds; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return Comparison switch
            {
                EventVariableComparison.Equal => game.CurrentTick == Ticks,
                EventVariableComparison.NotEqual => game.CurrentTick != Ticks,
                EventVariableComparison.GreaterThan => game.CurrentTick > Ticks,
                EventVariableComparison.GreaterThanOrEqual => game.CurrentTick >= Ticks,
                EventVariableComparison.LessThan => game.CurrentTick < Ticks,
                EventVariableComparison.LessThanOrEqual => game.CurrentTick <= Ticks,
                _ => throw new InvalidOperationException(
                    $"Invalid comparison type \"{Comparison}\" for TickCountConditional."
                ),
            };
        }
    }

    /// <summary>
    /// A <see cref="GameConditional"/> that is met when the specified game event has been completed.
    /// </summary>
    [PersistableObject(Name = "IsEventComplete")]
    public class IsEventCompleteConditional : GameConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }

        /// <summary>
        /// Checks whether the event with the configured instance ID has been marked complete.
        /// </summary>
        /// <param name="game">The game state tracking completed events.</param>
        /// <returns>True if the event is complete; otherwise false.</returns>
        public override bool IsMet(GameRoot game)
        {
            return game.IsEventComplete(EventInstanceID);
        }
    }

    /// <summary>
    /// Compares a persistent, data-defined event variable with an authored value.
    /// </summary>
    [PersistableObject(Name = "EvaluateEventVariable")]
    public class EvaluateEventVariableConditional : GameConditional
    {
        [PersistableAttribute]
        public string Key { get; set; }

        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public int Value { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            int current = game.GetEventVariable(Key);
            return Comparison switch
            {
                EventVariableComparison.Equal => current == Value,
                EventVariableComparison.NotEqual => current != Value,
                EventVariableComparison.GreaterThan => current > Value,
                EventVariableComparison.GreaterThanOrEqual => current >= Value,
                EventVariableComparison.LessThan => current < Value,
                EventVariableComparison.LessThanOrEqual => current <= Value,
                _ => throw new InvalidOperationException(
                    $"Unsupported event variable comparison '{Comparison}'."
                ),
            };
        }
    }

    /// <summary>
    /// Compares one typed trigger binding with an authored scalar value.
    /// </summary>
    [PersistableObject(Name = "EvaluateBinding")]
    public sealed class EvaluateBindingConditional : GameConditional
    {
        [PersistableAttribute]
        public string Name { get; set; }

        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public string Value { get; set; }

        public override bool IsMet(GameRoot game) => false;

        public override bool IsMet(GameRoot game, GameEventExecutionContext context)
        {
            if (context == null || !context.TryGetBinding(Name, out object actual))
                return false;

            if (actual is IEnumerable values && actual is not string)
            {
                bool contains = false;
                foreach (object value in values)
                    contains |= Compare(value, Value) == 0;
                return Comparison switch
                {
                    EventVariableComparison.Equal => contains,
                    EventVariableComparison.NotEqual => !contains,
                    _ => throw new InvalidOperationException(
                        "Collection bindings support only Equal and NotEqual."
                    ),
                };
            }

            int comparison = Compare(actual, Value);
            return Comparison switch
            {
                EventVariableComparison.Equal => comparison == 0,
                EventVariableComparison.NotEqual => comparison != 0,
                EventVariableComparison.GreaterThan => comparison > 0,
                EventVariableComparison.GreaterThanOrEqual => comparison >= 0,
                EventVariableComparison.LessThan => comparison < 0,
                EventVariableComparison.LessThanOrEqual => comparison <= 0,
                _ => throw new InvalidOperationException(
                    $"Unsupported binding comparison '{Comparison}'."
                ),
            };
        }

        private static int Compare(object actual, string expected)
        {
            if (actual is IGameEntity entity)
            {
                if (entity.InstanceID == expected)
                    return 0;
                if (
                    actual is ISceneNode node
                    && node.GetChildren<ISceneNode>(child => child.InstanceID == expected).Any()
                )
                    return 0;
                return string.Compare(entity.InstanceID, expected, StringComparison.Ordinal);
            }
            if (actual is bool boolean && bool.TryParse(expected, out bool expectedBoolean))
                return boolean.CompareTo(expectedBoolean);
            if (actual is int integer && int.TryParse(expected, out int expectedInteger))
                return integer.CompareTo(expectedInteger);
            if (actual is Enum)
                return string.Compare(actual.ToString(), expected, StringComparison.Ordinal);
            return string.Compare(actual?.ToString(), expected, StringComparison.Ordinal);
        }
    }
}
