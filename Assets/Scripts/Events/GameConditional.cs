using System;
using System.Collections.Generic;
using System.Xml.Serialization;

/// <summary>
/// Represents a condition that must be met for an event.
/// </summary>
/// <remarks>
/// Conditions are critical to the event system, as they determine when an event is
/// eligible to be executed. Conditions are evaluated at the time the event is scheduled
/// to occur, and if all conditions are met, the event is executed.
/// </remarks>
[PersistableObject]
public abstract class GameConditional : BaseGameEntity
{
    [PersistableAttribute(Name = "Value")]
    public string ConditionalValue { get; set; }

    [PersistableAttribute(Name = "Type")]
    public string ConditionalType { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public GameConditional() { }

    /// <summary>
    /// Creates a new GameConditional with a specific value (as an XML attribute).
    /// </summary>
    /// <param name="conditionalValue">The value of the condition.</param>
    public GameConditional(string conditionalValue)
    {
        ConditionalValue = conditionalValue;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public string GetConditionalValue()
    {
        return ConditionalValue;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public string GetConditionalType()
    {
        return ConditionalType;
    }

    /// <summary>
    /// Determines whether the condition is met in the specified game.
    /// </summary>
    /// <param name="game">The game instance to evaluate.</param>
    /// <returns>True if the condition is met; otherwise, false.</returns>
    public abstract bool IsMet(Game game);
}
