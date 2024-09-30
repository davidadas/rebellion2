using System;
using System.Collections.Generic;

/// <summary>
/// Manages game events and their scheduling.
/// </summary>
public class GameEventManager
{
    private readonly Game game;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    public GameEventManager(Game game)
    {
        this.game = game;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="gameEvent"></param>
    public void ScheduleEvent(GameEvent gameEvent)
    {
        // If the event is scheduled for the current tick, execute it immediately.
        if (game.CurrentTick >= gameEvent.Tick)
        {
            gameEvent.Execute(game);
        }
        // Otherwise, schedule the event for a future tick.
        else
        {
            game.AddGameEvent(gameEvent);
        }
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
