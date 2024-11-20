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
    [PersistableMember]
    public string Value { get; set; }
    public Dictionary<string, object> Parameters { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public GameConditional() { }

    /// <summary>
    /// Creates a new GameConditional with a specific value (as an XML attribute).
    /// </summary>
    /// <param name="value">The value of the condition.</param>
    public GameConditional(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new GameConditional with specific parameters.
    /// </summary>
    /// <param name="parameters">The parameters of the condition.</param>
    public GameConditional(Dictionary<string, object> parameters)
    {
        Parameters = parameters;
    }

    /// <summary>
    /// Determines whether the condition is met in the specified game.
    /// </summary>
    /// <param name="game">The game instance to evaluate.</param>
    /// <returns>True if the condition is met; otherwise, false.</returns>
    public abstract bool IsMet(Game game);
}
