/// <summary>
/// The IAction interface defines a contract for actions that can be executed within the game.
/// </summary>
/// <remarks>
/// Actions in this design represent discrete tasks or operations that affect the game state.
/// They are meant to be flexible, allowing developers to encapsulate specific behaviors that
/// can be triggered by events, user interactions, or other game mechanics.
/// 
/// Unlike Game Events, which encapsulate the occurrence of a specific scenario or trigger 
/// within the game, Actions focus solely on the execution of a specific operation. Events 
/// may trigger Actions, but Actions are concerned with the actual changes made to the game 
/// state.
/// </remarks>
public interface IAction
{
    // <summary>
    /// Executes the action within the context of the provided game instance.
    /// </summary>
    /// <param name="game">The current game instance in which the action should be executed.</param>
    /// <remarks>
    /// The Execute method is the core of the IAction interface. When an action is executed, it typically
    /// performs one or more operations on the game state. For example, an action might move a unit, 
    /// update a resource count, or trigger another event.
    /// </remarks>
    void Execute(Game game);
}
