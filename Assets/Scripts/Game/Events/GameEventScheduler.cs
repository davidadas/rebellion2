using System.Collections.Generic;
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
        public AfterEvent After { get; set; }
        public AfterEvents AfterAll { get; set; }
        public AfterEvents AfterAny { get; set; }

        [PersistableIgnore]
        public bool IsRecurring => Every != null || Random != null;

        /// <summary>
        /// Gets the inclusive delay range for an event's first activation.
        /// </summary>
        /// <param name="minimum">Receives the minimum initial delay.</param>
        /// <param name="maximum">Receives the maximum initial delay.</param>
        public void GetInitialRange(out int minimum, out int maximum)
        {
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

            if (After != null || AfterAll != null || AfterAny != null)
            {
                minimum = maximum = (AfterAll ?? AfterAny)?.DelayTicks ?? After.DelayTicks;
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
            if (Every != null)
            {
                minimum = maximum = Every.Ticks;
                return;
            }

            Random.GetRange(out minimum, out maximum);
        }
    }

    /// <summary>
    /// Schedules a one-shot event relative to the most recent execution of another event.
    /// </summary>
    [PersistableObject]
    public sealed class AfterEvent
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }

        [PersistableAttribute]
        public int DelayTicks { get; set; }
    }

    /// <summary>
    /// Schedules a one-shot event relative to either every or any listed predecessor.
    /// </summary>
    [PersistableObject]
    public sealed class AfterEvents
    {
        [PersistableAttribute]
        public int DelayTicks { get; set; }

        public List<EventDependency> Events { get; set; } = new List<EventDependency>();
    }

    [PersistableObject(Name = "Event")]
    public sealed class EventDependency
    {
        [PersistableAttribute]
        public string EventInstanceID { get; set; }
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
        /// Returns the configured inclusive delay range after load-time validation.
        /// </summary>
        /// <param name="minimum">Receives the minimum delay.</param>
        /// <param name="maximum">Receives the maximum delay.</param>
        public void GetRange(out int minimum, out int maximum)
        {
            minimum = MinimumTicks;
            maximum = MaximumTicks;
        }
    }
}
