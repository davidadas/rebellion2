using System;
using System.Collections;
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
    public sealed class TickCountConditional : GameConditional
    {
        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public int Ticks { get; set; }

        /// <summary>
        /// Compares the current tick against the authored tick count.
        /// </summary>
        /// <param name="context">The context providing the current game state.</param>
        /// <returns>True when the tick comparison holds; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            GameRoot game = context.Game;
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
    public sealed class IsEventCompleteConditional : GameConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }

        /// <summary>
        /// Checks whether the event with the configured instance ID has been marked complete.
        /// </summary>
        /// <param name="context">The context providing event runtime state.</param>
        /// <returns>True if the event is complete; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            return context.Game.EventRuntime.IsComplete(EventInstanceID);
        }
    }

    /// <summary>
    /// Compares a persistent, data-defined event variable with an authored value.
    /// </summary>
    [PersistableObject(Name = "EvaluateEventVariable")]
    public sealed class EvaluateEventVariableConditional : GameConditional
    {
        [PersistableAttribute]
        public string Key { get; set; }

        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public int ExpectedValue { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameConditionContext context)
        {
            int current = context.Game.EventRuntime.GetVariable(Key);
            return Comparison switch
            {
                EventVariableComparison.Equal => current == ExpectedValue,
                EventVariableComparison.NotEqual => current != ExpectedValue,
                EventVariableComparison.GreaterThan => current > ExpectedValue,
                EventVariableComparison.GreaterThanOrEqual => current >= ExpectedValue,
                EventVariableComparison.LessThan => current < ExpectedValue,
                EventVariableComparison.LessThanOrEqual => current <= ExpectedValue,
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
        public string Binding { get; set; }

        [PersistableAttribute]
        public EventVariableComparison Comparison { get; set; }

        [PersistableAttribute]
        public string ExpectedValue { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            if (
                context.Activation == null
                || !context.Activation.TryGetBindingReference(Binding, out object actual)
            )
                return false;

            if (actual is IEnumerable values && actual is not string)
            {
                bool contains = false;
                foreach (object value in values)
                    contains |= Compare(value, ExpectedValue) == 0;
                return Comparison switch
                {
                    EventVariableComparison.Equal => contains,
                    EventVariableComparison.NotEqual => !contains,
                    _ => throw new InvalidOperationException(
                        "Collection bindings support only Equal and NotEqual."
                    ),
                };
            }

            if (
                Comparison
                    is EventVariableComparison.GreaterThan
                        or EventVariableComparison.GreaterThanOrEqual
                        or EventVariableComparison.LessThan
                        or EventVariableComparison.LessThanOrEqual
                && actual is not int
            )
                throw new InvalidOperationException(
                    $"Binding '{Binding}' supports ordered comparisons only when it contains an integer."
                );

            int comparison = Compare(actual, ExpectedValue);
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
                return string.Compare(entity.InstanceID, expected, StringComparison.Ordinal);
            }
            if (actual is bool boolean && bool.TryParse(expected, out bool expectedBoolean))
                return boolean.CompareTo(expectedBoolean);
            if (actual is bool)
                throw new InvalidOperationException($"'{expected}' is not a Boolean value.");
            if (actual is int integer)
            {
                if (!int.TryParse(expected, out int expectedInteger))
                    throw new InvalidOperationException($"'{expected}' is not an integer value.");
                return integer.CompareTo(expectedInteger);
            }
            if (actual is Enum)
                return string.Compare(actual.ToString(), expected, StringComparison.Ordinal);
            if (actual is string text)
                return string.Compare(text, expected, StringComparison.Ordinal);
            throw new InvalidOperationException(
                $"Binding values of type '{actual?.GetType().Name ?? "null"}' cannot be compared."
            );
        }
    }
}
