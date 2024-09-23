using System;

/// <summary>
/// Represents a generic action that can be executed on a game.
/// </summary>
public class GenericAction : IAction
{
    private Action<Game> action;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericAction"/> class.
    /// </summary>
    /// <param name="action">The action to be executed on the game.</param>
    public GenericAction(Action<Game> action)
    {
        this.action = action;
    }

    /// <summary>
    /// Executes the generic action on the specified game.
    /// </summary>
    /// <param name="game">The game on which the action is to be executed.</param>
    public void Execute(Game game)
    {
        action.Invoke(game);
    }
}
