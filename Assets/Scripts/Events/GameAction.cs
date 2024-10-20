using System;
using System.Xml.Serialization;
using DependencyInjectionExtensions;

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
[Serializable]
[XmlInclude(typeof(StartMissionAction))]
public abstract class GameAction
{
    [XmlAttribute("Type")]
    public string ActionType { get; set; }
    public SerializableDictionary<string, object> Parameters { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public GameAction() { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameters"></param>
    public GameAction(SerializableDictionary<string, object> parameters)
    {
        Parameters = parameters;
    }

    /// <summary>
    /// Executes the action, using the provided game instance and service locator.
    /// </summary>
    /// <param name="locator">The service locator for retrieving necessary services during execution.</param>
    public abstract void Execute(IServiceLocator locator);
}
