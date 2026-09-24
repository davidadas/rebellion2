using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines exactly one absolute, fixed-interval, or random-range event schedule.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventSchedule
    {
        public AtTick At { get; set; }
        public EveryTicks Every { get; set; }
        public RandomDelay RandomDelay { get; set; }
        public RandomInterval RandomInterval { get; set; }
        public AfterEvent After { get; set; }
        public AfterEvents AfterAll { get; set; }
        public AfterEvents AfterAny { get; set; }
    }

    /// <summary>
    /// Schedules a one-shot event relative to the most recent activation of another event.
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

        // Completion Conditions.
        public List<GameConditional> Until { get; set; } = new List<GameConditional>();
    }

    /// <summary>
    /// Schedules an event after a uniformly selected inclusive tick delay.
    /// </summary>
    [PersistableObject]
    public sealed class RandomDelay
    {
        [PersistableAttribute]
        public int MinimumTicks { get; set; }

        [PersistableAttribute]
        public int MaximumTicks { get; set; }
    }

    /// <summary>
    /// Schedules recurring event activations at uniformly selected inclusive tick intervals.
    /// </summary>
    [PersistableObject]
    public sealed class RandomInterval
    {
        [PersistableAttribute]
        public int MinimumTicks { get; set; }

        [PersistableAttribute]
        public int MaximumTicks { get; set; }

        // Completion Conditions.
        public List<GameConditional> Until { get; set; } = new List<GameConditional>();
    }
}
