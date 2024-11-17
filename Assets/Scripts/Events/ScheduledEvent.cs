/// <summary>
/// Represents a scheduled event within the game.
/// A ScheduledEvent is a wrapper around a GameEvent, responsible for tracking
/// when (at what game tick) the event should be executed.
/// </summary>
/// <remarks>
/// The purpose of the ScheduledEvent class is to separate the timing aspect from the logic of the GameEvent itself.
///
/// This allows the GameEvent class to be immutable and stateless after creation, while the ScheduledEvent class
/// can be modified to change the execution time of the event, in addition to anything else that might be needed.
/// </remarks>
public class ScheduledEvent
{
    public GameEvent Event { get; }
    public int ScheduledTick { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public ScheduledEvent() { }

    /// <summary>
    /// Initializes a new instance of the ScheduledEvent class with a given event and tick.
    /// </summary>
    /// <param name="gameEvent">The GameEvent to be executed.</param>
    /// <param name="scheduledTick">The tick at which the event should execute.</param>
    public ScheduledEvent(GameEvent gameEvent, int scheduledTick)
    {
        Event = gameEvent;
        ScheduledTick = scheduledTick;
    }

    /// <summary>
    /// Gets the GameEvent associated with this ScheduledEvent.
    /// </summary>
    /// <returns></returns>
    public GameEvent GetEvent()
    {
        return Event;
    }
}
