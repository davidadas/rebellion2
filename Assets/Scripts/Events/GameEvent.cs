using System;
using System.Collections.Generic;

/// <summary>
/// Represents a base class for all game events within the game.
/// </summary>
/// <remarks>
/// Game events are integral to the game's event-driven architecture, encapsulating
/// specific occurrences or triggers that happen during gameplay. Each event represents
/// a point in time where certain conditions are evaluated, and if met, certain actions
/// are executed.
///
/// Unlike Actions, which directly represent a specific operation or change in the game
/// state, game events focus on the occurrence of a particular scenario or trigger.
/// Events can lead to the execution of one or more Actions but are primarily concerned
/// with the context and timing of those actions.
/// </remarks>
public class GameEvent : BaseGameEntity
{
    // Event Properties
    public bool IsRepeatable { get; set; }
    public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
    public List<GameAction> Actions { get; set; } = new List<GameAction>();

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public GameEvent() { }

    /// <summary>
    /// Creates a new game event with the specified conditionals and actions.
    /// </summary>
    /// <param name="conditionals">The list of conditionals that must be met for the event to occur.</param>
    /// <param name="actions">The list of actions to execute when
    public GameEvent(List<GameConditional> conditionals, List<GameAction> actions)
    {
        Conditionals = conditionals;
        Actions = actions;
    }

    /// <summary>
    /// Determines whether all conditions for the event are met.
    /// </summary>
    /// <param name="game">The game instance to evaluate.</param>
    /// <returns>True if all conditions are met; otherwise, false.</returns>
    public bool AreConditionsMet(Game game)
    {
        foreach (GameConditional conditional in Conditionals)
        {
            if (!conditional.IsMet(game))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Executes the event, modifying the game state.
    /// </summary>
    /// <param name="game">The game instance to modify.</param>
    public void Execute(Game game)
    {
        // Evaluate the event's actions.
        foreach (GameAction action in Actions)
        {
            action.Execute(game);
        }
    }
}
