using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages game events and their scheduling.
/// </summary>
public class GameEventManager
{
    private Game game;

    public GameEventManager(Game game)
    {
        this.game = game;
    }

    /// <summary>
    /// Schedules the specified game event to occur at the specified tick.
    /// </summary>
    /// <param name="gameEvent"></param>
    /// <param name="tick"></param>
    public void ScheduleEvent(GameEvent gameEvent, int tick)
    {
        game.ScheduleGameEvent(gameEvent, tick);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="actions"></param>
    /// <param name="tick"></param>
    public void ScheduleEvent(List<GameAction> actions, int tick)
    {
        List<GameConditional> conditionals = new List<GameConditional>();
        GameEvent gameEvent = new GameEvent(conditionals, actions);
        ScheduleEvent(gameEvent, tick);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="conditionals"></param>
    /// <param name="actions"></param>
    /// <param name="tick"></param>
    public void ScheduleEvent(
        List<GameConditional> conditionals,
        List<GameAction> actions,
        int tick
    )
    {
        GameEvent gameEvent = new GameEvent(conditionals, actions);
        ScheduleEvent(gameEvent, tick);
    }

    /// <summary>
    /// Processes the game events for the specified tick.
    /// </summary>
    /// <param name="currentTick">The current tick.</param>
    public void ProcessEvents(int currentTick)
    {
        List<ScheduledEvent> scheduledEvents = game.GetScheduledEvents(currentTick);

        // Check if there are any events scheduled for this tick.
        if (scheduledEvents.Any())
        {
            // Execute each event.
            foreach (ScheduledEvent scheduledEvent in scheduledEvents)
            {
                GameEvent gameEvent = scheduledEvent.GetEvent();

                gameEvent.Execute(game);

                // Add the event to the list of completed events.
                game.AddCompletedEvent(gameEvent);
            }
        }
    }
}
