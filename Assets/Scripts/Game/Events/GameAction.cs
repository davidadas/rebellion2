using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Attributes;

/// <summary>
/// Defines a contract for actions that modify the game state when executed.
/// Each action returns a list of results describing what changed, which the
/// caller can use for notifications, logging, or AI reactions.
/// </summary>
[PersistableObject]
public abstract class GameAction
{
    [PersistableAttribute(Name = "Type")]
    protected string ActionType { get; set; }

    [PersistableAttribute(Name = "Value")]
    protected string ActionValue { get; set; }

    public GameAction() { }

    public GameAction(string type, string value)
    {
        ActionType = type;
        ActionValue = value;
    }

    protected string GetActionType() => ActionType;

    protected string GetActionValue() => ActionValue;

    /// <summary>
    /// Executes the action, modifying the game state.
    /// </summary>
    /// <param name="game">The current game state.</param>
    /// <returns>Results describing what changed.</returns>
    public abstract List<GameResult> Execute(GameRoot game);
}
