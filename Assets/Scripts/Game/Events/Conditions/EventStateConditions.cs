using System;
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
    [PersistableObject(Name = "EventVariable")]
    public class EventVariableConditional : GameConditional
    {
        public string Key { get; set; }
        public EventVariableComparison Comparison { get; set; }
        public int ExpectedValue { get; set; }

        /// <inheritdoc />
        public override bool IsMet(GameRoot game)
        {
            int current = game.GetEventVariable(Key);
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
}
