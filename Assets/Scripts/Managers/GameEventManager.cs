using System;
using System.Collections.Generic;

/// <summary>
/// Manages game events and their scheduling.
/// </summary>
public class EventManager
{
    private readonly Game game;

    public EventManager(Game game)
    {
        this.game = game;
    }

    /// <summary>
    /// Retrieves all game events of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of game event to retrieve.</typeparam>
    /// <returns>A list of game events of the specified type.</returns>
    public List<GameEvent> GetEventsByType<T>() where T : GameEvent
    {
        List<GameEvent> events = new List<GameEvent>();

        // Iterate through all events in the dictionary.
        foreach (var eventList in game.GameEventDictionary.Values)
        {
            // Check if the event is of the specified type.
            foreach (var gameEvent in eventList)
            {
                // Add the event to the list if it is of the specified type.
                if (gameEvent is T)
                {
                    events.Add(gameEvent);
                }
            }
        }

        return events;
    }

    /// <summary>
    /// Schedules a game event.
    /// </summary>
    /// <param name="gameEvent">The game event to schedule.</param>
    public void ScheduleEvent(GameEvent gameEvent)
    {
        // Add the event to the dictionary.
        if (!game.GameEventDictionary.ContainsKey(gameEvent.ScheduledTick))
        {
            game.GameEventDictionary[gameEvent.ScheduledTick] = new List<GameEvent>();
        }

        // Add the event to the list of events for this tick.
        game.GameEventDictionary[gameEvent.ScheduledTick].Add(gameEvent);
    }

    /// <summary>
    /// Processes the game events for the specified tick.
    /// </summary>
    /// <param name="currentTick">The current tick.</param>
    public void ProcessEvents(int currentTick)
    {
        // Check if there are any events scheduled for this tick.
        if (game.GameEventDictionary.ContainsKey(currentTick))
        {
            foreach (var gameEvent in game.GameEventDictionary[currentTick])
            {
                gameEvent.Execute(game);
            }

            // Remove processed events for this tick
            game.GameEventDictionary.Remove(currentTick);
        }
    }
}
