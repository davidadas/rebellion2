using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// 
/// </summary>
public class NarrativeEvent : GameEvent
{
    public List<GameConditional> Conditionals { get; set; }
    public bool Repeatable { get; set; }
    public SerializableDictionary<string, SerializableDictionary<string, object>> ConditionalParamsDictionary { get; set; }
    
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public NarrativeEvent() { }

    /// <summary>
    /// Checks if each conditional is met.
    /// </summary>
    /// <param name="game">The game object.</param>
    /// <returns>True if all conditionals are met, false otherwise.</returns>
    public bool MeetsConditions(Game game)
    {
        return Conditionals.All(conditional => conditional.IsMet(game));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    protected override void TriggerEvent(Game game)
    {
        // @TODO: Implement trigger.
    }
}
