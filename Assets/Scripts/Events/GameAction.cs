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
    protected string ActionType { get; set; }

    [PersistableAttribute(Name = "Value")]
    protected string ActionValue { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public GameAction() { }

    /// <summary>
    /// Creates a new GameAction with a specific value (as an XML attribute).
    /// </summary>
    /// <param name="type">The type of the action.</param>
    /// <param name="value">The value of the action.</param>
    public GameAction(string type, string value)
    {
        ActionType = type;
        ActionValue = value;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    protected string GetActionType()
    {
        return ActionType;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    protected string GetActionValue()
    {
        return ActionValue;
    }

    /// <summary>
    /// Executes the action, modifying the game state.
    /// </summary>
    /// <param name="game"></param>
    public abstract void Execute(Game game);
}
