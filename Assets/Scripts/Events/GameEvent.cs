using System.Collections.Generic;
using System;
using DependencyInjectionExtensions;

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
public class GameEvent : GameEntity
{
    // Event Properties
    public bool IsRepeatable { get; set; }
    public List<GameConditional> Conditionals { get; set; }
    public List<GameAction> Actions { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public GameEvent() { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceLocator"></param>
    /// <returns></returns>
    public bool AreConditionsMet(IServiceLocator serviceLocator)
    {
        foreach (GameConditional conditional in Conditionals)
        {
            if (!conditional.IsMet(serviceLocator))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceLocator"></param>
    public void Execute(IServiceLocator serviceLocator)
    {
        // Evaluate the event's actions.
        foreach (GameAction action in Actions)
        {
            action.Execute(serviceLocator);
        }
    }
}
