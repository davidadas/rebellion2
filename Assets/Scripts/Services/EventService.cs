using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 
/// </summary>
public interface IEventService
{
    public void ScheduleGameEvent(int tick, GameEvent gameEvent);
    public List<ScheduledEvent> GetScheduledEvents(int tick);
    public void RemoveScheduledEvent(int tick, ScheduledEvent scheduledEvent);
    public void AddCompletedEvent(GameEvent gameEvent);
}

/// <summary>
/// 
/// </summary>
public class EventService : IEventService
{
    private Game game;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="locator">The service locator.</param>
    /// <param name="game">The game instance.</param>
    public EventService(Game game)
    {
        this.game = game;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="gameEvent"></param>
    public void ScheduleGameEvent(int tick, GameEvent gameEvent)
    {
         game.ScheduleGameEvent(tick, gameEvent);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tick"></param>
    /// <returns></returns>
    public List<ScheduledEvent> GetScheduledEvents(int tick)
    {
        if (game.ScheduledEvents.TryGetValue(tick, out List<ScheduledEvent> scheduledEvents))
        {
            return scheduledEvents;
        }
        else
        {
            return new List<ScheduledEvent>();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="scheduledEvent"></param>
    public void RemoveScheduledEvent(int tick, ScheduledEvent scheduledEvent)
    {
        game.RemoveScheduledEvent(tick, scheduledEvent);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="gameEvent"></param>
    public void AddCompletedEvent(GameEvent gameEvent)
    {
        game.AddCompletedEventID(gameEvent);
    }
}
