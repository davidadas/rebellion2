using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// 
/// </summary>
public class NarrativeEventManager
{
    private Game game;
    private GameEventManager eventManager;

    public NarrativeManager(Game game, GameEventManager eventManager)
    {
        this.game = game;
        this.eventManager = eventManager;

        initializeNarrativeEvents(game);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    public void ProcessNarrativeEvents(Game game)
    {
        foreach(NarrativeEvent narrativeEvent in game.NarrativeEvents)
        {
            // Check if the event's conditions are met.
            if (narrativeEvent.MeetsConditions(game))
            {
                // Schedule the event.
                eventManager.ScheduleEvent(narrativeEvent);

                // Remove the event if it is not repeatable.
                if (!narrativeEvent.Repeatable)
                {
                    game.RemoveNarrativeEvent(narrativeEvent);
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void initializeNarrativeEvents(Game game)
    {
        // Create conditionals for each narrative event.
        // foreach(GameEvent narrativeEvent in narrativeEvents)
        // {
        //     List<GameConditional> conditionals = narrativeEvent.ConditionalParamsDictionary.Keys
        //         .Aggregate(new List<GameConditional>(), (acc, key) => {
        //             // Get parameters for the conditional.
        //             SerializableDictionary<string, object> parameters = narrativeEvent.ConditionalParamsDictionary.TryGetValue(key, out parameters) ? parameters : null;
                    
        //             // Create the conditional and add it to the list.
        //             acc.Add(ConditionalFactory.CreateConditional(key, parameters));
        //             return acc;
        //         });

        // }
        
    }
}
