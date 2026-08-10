using System;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines exactly one absolute, fixed-interval, or random-range event schedule.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventScheduler
    {
        public AtTick At { get; set; }
        public EveryTicks Every { get; set; }
        public RandomTickRange Random { get; set; }

        /// <summary>
        /// Gets the inclusive delay range for an event's first activation.
        /// </summary>
        /// <param name="minimum">Receives the minimum initial delay.</param>
        /// <param name="maximum">Receives the maximum initial delay.</param>
        public void GetInitialRange(out int minimum, out int maximum)
        {
            EnsureSingleMode();
            if (At != null)
            {
                minimum = maximum = At.Tick;
                return;
            }

            if (Every != null)
            {
                minimum = maximum = Every.InitialDelayTicks;
                return;
            }

            Random.GetRange(out minimum, out maximum);
        }

        /// <summary>
        /// Gets the inclusive delay range between activations of a repeatable event.
        /// </summary>
        /// <param name="minimum">Receives the minimum repeat delay.</param>
        /// <param name="maximum">Receives the maximum repeat delay.</param>
        public void GetRepeatRange(out int minimum, out int maximum)
        {
            EnsureSingleMode();
            if (At != null)
                throw new InvalidOperationException("An At schedule cannot repeat.");

            if (Every != null)
            {
                if (Every.Ticks < 1)
                    throw new InvalidOperationException("Every.Ticks must be at least one.");
                minimum = maximum = Every.Ticks;
                return;
            }

            Random.GetRange(out minimum, out maximum);
            if (minimum < 1)
                throw new InvalidOperationException(
                    "A repeating random tick range must start at one or later."
                );
        }

        /// <summary>
        /// Ensures content configured exactly one mutually exclusive schedule mode.
        /// </summary>
        private void EnsureSingleMode()
        {
            int configuredModes =
                (At == null ? 0 : 1) + (Every == null ? 0 : 1) + (Random == null ? 0 : 1);
            if (configuredModes != 1)
                throw new InvalidOperationException(
                    "Schedule requires exactly one of At, Every, or Random."
                );
        }
    }

    /// <summary>
    /// Schedules a one-shot event at an absolute campaign tick.
    /// </summary>
    [PersistableObject]
    public sealed class AtTick
    {
        [PersistableAttribute]
        public int Tick { get; set; }
    }

    /// <summary>
    /// Schedules an event at a fixed interval with an optional initial delay.
    /// </summary>
    [PersistableObject]
    public sealed class EveryTicks
    {
        [PersistableAttribute]
        public int Ticks { get; set; }

        [PersistableAttribute]
        public int InitialDelayTicks { get; set; }
    }

    /// <summary>
    /// Schedules an event after a uniformly selected inclusive tick delay.
    /// </summary>
    [PersistableObject]
    public sealed class RandomTickRange
    {
        [PersistableAttribute]
        public int MinimumTicks { get; set; }

        [PersistableAttribute]
        public int MaximumTicks { get; set; }

        /// <summary>
        /// Validates and returns the configured inclusive delay range.
        /// </summary>
        /// <param name="minimum">Receives the minimum delay.</param>
        /// <param name="maximum">Receives the maximum delay.</param>
        public void GetRange(out int minimum, out int maximum)
        {
            if (MinimumTicks < 0)
                throw new InvalidOperationException("MinimumTicks cannot be negative.");
            if (MaximumTicks < MinimumTicks)
                throw new InvalidOperationException("MaximumTicks cannot precede MinimumTicks.");

            minimum = MinimumTicks;
            maximum = MaximumTicks;
        }
    }
}
