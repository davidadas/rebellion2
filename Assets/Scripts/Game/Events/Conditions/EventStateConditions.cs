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
    /// A <see cref="GameConditional"/> that is met after the specified event has triggered.
    /// </summary>
    [PersistableObject(Name = "HasEventTriggered")]
    public sealed class HasEventTriggeredConditional : GameConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }

        /// <summary>
        /// Checks whether the event with the configured instance ID has executed at least once.
        /// </summary>
        /// <param name="context">The context providing event runtime state.</param>
        /// <returns>True if the event has executed; otherwise false.</returns>
        public override bool IsMet(GameConditionContext context)
        {
            return context.Game.EventRuntime.GetState(EventInstanceID).ExecutionCount > 0;
        }
    }

    /// <summary>
    /// Tests whether the specified event can no longer execute.
    /// </summary>
    [PersistableObject(Name = "IsEventExhausted")]
    public sealed class IsEventExhaustedConditional : GameConditional
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            GameEventState state = context.Game.EventRuntime.GetState(EventInstanceID);
            if (state.IsExhausted)
                return true;

            GameEvent definition = context
                .Game.GetEventPool()
                .Find(gameEvent =>
                    string.Equals(gameEvent.InstanceID, EventInstanceID, StringComparison.Ordinal)
                );
            int? triggerCount = definition?.GetTriggerCount();
            return triggerCount.HasValue && state.ExecutionCount >= triggerCount.Value;
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
        public int CompareTo { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameConditionContext context)
        {
            int current = context.Game.EventRuntime.GetVariable(Key);
            return Comparison switch
            {
                EventVariableComparison.Equal => current == CompareTo,
                EventVariableComparison.NotEqual => current != CompareTo,
                EventVariableComparison.GreaterThan => current > CompareTo,
                EventVariableComparison.GreaterThanOrEqual => current >= CompareTo,
                EventVariableComparison.LessThan => current < CompareTo,
                EventVariableComparison.LessThanOrEqual => current <= CompareTo,
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
        public string CompareTo { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            if (
                context.Activation == null
                || !context.Activation.TryGetBindingReference(Binding, out object actual)
            )
                return false;

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

            int comparison = Compare(actual, CompareTo);
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

    /// <summary>
    /// Tests whether a bound scene-node collection contains one canonical unit.
    /// </summary>
    [PersistableObject(Name = "BindingIncludesUnit")]
    public sealed class BindingIncludesUnitConditional : GameConditional
    {
        [PersistableAttribute]
        public string Binding { get; set; }

        [PersistableAttribute]
        public string UnitInstanceID { get; set; }

        public override bool IsMet(GameConditionContext context)
        {
            if (
                context.Activation == null
                || !context.Activation.TryGetBindingReference(Binding, out object actual)
                || actual is not IEnumerable values
            )
                return false;

            foreach (object value in values)
            {
                if (
                    value is IGameEntity entity
                    && string.Equals(entity.InstanceID, UnitInstanceID, StringComparison.Ordinal)
                )
                    return true;
            }
            return false;
        }
    }
}
