using System.Xml.Serialization;
using System;

/// <summary>
/// Represents a condition that must be met for an event or action to proceed.
/// </summary>
[Serializable]
public abstract class GameConditional
{
    public GameConditional() { }
    
    /// <summary>
    /// Evaluates the condition in the context of the provided game instance.
    /// </summary>
    /// <param name="game">The current game instance to evaluate the condition against.</param>
    /// <returns>True if the condition is met, otherwise false.</returns>
    public abstract bool IsMet(Game game);
}
