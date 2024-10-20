using System.Collections.Generic;
using System.Linq;
using System;
using DependencyInjectionExtensions;

/// <summary>
/// Manages game events and their scheduling.
/// </summary>
public class GameEventManager
{
    private readonly IServiceLocator locator;
    private readonly IEventService eventService;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="locator"></param>
    public GameEventManager(IServiceLocator locator)
    {
        this.locator = (ServiceLocator)locator;
        this.eventService = locator.GetService<IEventService>();
    }

    /// <summary>
    /// Processes the game events for the specified tick.
    /// </summary>
    /// <param name="currentTick">The current tick.</param>
    public void ProcessEvents(int currentTick)
    {
        List<ScheduledEvent> scheduledEvents = eventService.GetScheduledEvents(currentTick);

        // Check if there are any events scheduled for this tick.
        if (scheduledEvents.Any())
        {
            // Execute each event.
            foreach (ScheduledEvent scheduledEvent in scheduledEvents)
            {
                GameEvent gameEvent = scheduledEvent.GetEvent();

                gameEvent.Execute(locator);

                // Add the event to the list of completed events.
                eventService.AddCompletedEvent(gameEvent);

                // Remove processed events for this tick.
                eventService.RemoveScheduledEvent(currentTick, scheduledEvent);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    
    public void Initialize(Game game)
    {
        // List<GameConditional> mappedConditionals = new List<GameConditional>();
        // List<GameAction> mappedActions = new List<GameAction>();
        // List<GameEvent> gameEvents = game.EventPool;

        // foreach(GameEvent gameEvent in gameEvents)
        // {
        //     foreach (GameConditional conditional in gameEvent.Conditionals)
        //     {
        //         // Assuming conditional has a 'ClassName' or similar property
        //         string type = ((GameConditional)conditional).ConditionalType;

        //         GameConditional mappedConditional = ConditionalFactory.CreateConditional(type);
        //         mappedConditionals.Add(mappedConditional);
        //     }

        //     foreach (GameAction action in gameEvent.Actions)
        //     {
        //         string type = ((GameAction)action).ActionType;

        //         GameAction mappedAction = ActionFactory.CreateAction(type);
        //         mappedActions.Add(mappedAction);
        //     }

        //     // Replace placeholders with the real mapped objects
        //     gameEvent.Conditionals = mappedConditionals.Cast<IConditional>().ToList();
        //     gameEvent.Actions = mappedActions.Cast<IAction>().ToList();
        // }
    }
}
