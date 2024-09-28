using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// 
/// </summary>
public class NarrativeEventManager
{
    private GameEventManager eventManager;

    public NarrativeEventManager(GameEventManager eventManager)
    {
        this.eventManager = eventManager;

        initializeNarrativeEvents();
    }

    /// <summary>
    /// 
    /// </summary>
    private void initializeNarrativeEvents()
    {
        // Get all narrative events
        List<NarrativeEvent> narrativeEvents = eventManager
            .GetEventsByType<NarrativeEvent>()
            .OfType<NarrativeEvent>()
            .ToList();

        // Create conditionals for each narrative event
        foreach(NarrativeEvent narrativeEvent in narrativeEvents)
        {
            List<IConditional> conditionals = narrativeEvent.ConditionalParamsDictionary.Keys
                .Aggregate(new List<IConditional>(), (acc, key) => {
                    //
                    SerializableDictionary<string, object> parameters = narrativeEvent.ConditionalParamsDictionary.TryGetValue(key, out parameters) ? parameters : null;
                    acc.Add(ConditionalFactory.CreateConditional(key, parameters));
                    return acc;
                });
            
        }
        
    }
}
