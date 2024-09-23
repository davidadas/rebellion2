using System;

/// <summary>
/// Represents a base class for all game events within the game.
/// </summary>
/// <remarks>
/// Game events are integral to the game's event-driven architecture, encapsulating 
/// specific occurrences or triggers that happen during gameplay. Each event represents 
/// a point in time where certain conditions are evaluated, and if met, certain actions 
/// are executed.
/// 
/// Unlike Actions, which directly represent a specific operation or change in the game 
/// state, Game Events focus on the occurrence of a particular scenario or trigger. 
/// Events can lead to the execution of one or more Actions but are primarily concerned 
/// with the context and timing of those actions.
/// </remarks>
public abstract class GameEvent : GameEntity
{
    public int ScheduledTick { get; set; }
    public event Action<GameEvent> OnEventTriggered;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameEvent"/> class with the specified parameters.
    /// </summary>
    /// <param name="scheduledTick">The tick at which the event is scheduled to occur.</param>
    protected GameEvent(int scheduledTick)
    {
        ScheduledTick = scheduledTick;
    }

    /// <summary>
    /// Executes the event, applying any relevant logic or actions to the game state.
    /// </summary>
    /// <param name="game">The current game instance in which the event is executed.</param>
    /// <remarks>
    /// The `Execute` method is the core of the `GameEvent` class. When the event is triggered, 
    /// this method is called to perform the event's logic. This might include evaluating conditions,
    /// triggering actions, or scheduling follow-up events.
    /// </remarks>
    public void Execute(Game game)
    {
        TriggerEvent(game);
        OnEventTriggered?.Invoke(this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    protected abstract void TriggerEvent(Game game);
    
}
