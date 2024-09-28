using System.Collections.Generic;
using System;

public class NarrativeEvent : GameEvent
{
    public IConditional Conditionals { get; set; }
    public SerializableDictionary<string, SerializableDictionary<string, object>> ConditionalParamsDictionary { get; set; }
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public NarrativeEvent() { }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="scheduledTick"></param>
    /// <param name="conditionals"></param>
    public NarrativeEvent(int scheduledTick, List<IConditional> conditionals) : base(scheduledTick) { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    protected override void TriggerEvent(Game game)
    {
        // @TODO: Implement trigger.
    }
}
