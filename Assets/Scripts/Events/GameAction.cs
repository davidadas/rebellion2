using System;
using System.Collections.Generic;

/// <summary>
/// The GameAction class defines a contract for actions that modify the game state when executed.
/// </summary>
/// <remarks>
/// GameActions encapsulate specific operations triggered by events, user interactions,
/// or other game mechanics. They rely on the ServiceLocator to retrieve necessary
/// services at execution, ensuring flexibility and reusability.
///
/// Unlike GameEvents, which encapsulate the occurrence of a specific scenario or trigger
/// within the game, Actions focus solely on the execution of a specific operation. Events
/// may trigger Actions, but Actions are concerned with the actual changes made to the game
/// state.
/// </remarks>
[PersistableObject]
public abstract class GameAction
{
    [PersistableAttribute(Name = "Type")]
    public string ActionType { get; set; }

    public string Value { get; set; }
    public Dictionary<string, object> Parameters { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public GameAction() { }

    /// <summary>
    /// Creates a new GameAction with a specific value (as an XML attribute).
    /// </summary>
    /// <param name="value">The value of the action.</param>
    public GameAction(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new GameAction with specific parameters.
    /// </summary>
    /// <param name="parameters">The parameters of the action.</param>
    public GameAction(Dictionary<string, object> parameters)
    {
        Parameters = parameters;
    }

    /// <summary>
    /// Executes the action, modifying the game state.
    /// </summary>
    /// <param name="game"></param>
    public abstract void Execute(Game game);
}
