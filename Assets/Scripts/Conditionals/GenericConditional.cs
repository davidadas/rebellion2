using System;
using System.Collections.Generic;

/// <summary>
/// A generic conditional that uses a callback to determine if the condition is met.
/// </summary>
public class GenericConditional : GameConditional
{
    private readonly Func<Game, Dictionary<string, object>, bool> callback;
    private Dictionary<string, object> parameters;

    /// <summary>
    /// Initializes a new instance of the GenericConditional class.
    /// </summary>
    /// <param name="callback">The function that evaluates whether the condition is met.</param>
    /// <param name="parameters">The parameters required for the condition.</param>
    public GenericConditional(Func<Game, Dictionary<string, object>, bool> callback, Dictionary<string, object> parameters)
    {
        this.callback = callback;
        this.parameters = parameters;
    }

    /// <summary>
    /// Evaluates the condition in the context of the provided game instance.
    /// </summary>
    /// <param name="game">The current game instance to evaluate the condition against.</param>
    /// <returns>True if the condition is met, otherwise false.</returns>
    public override bool IsMet(Game game)
    {
        return callback(game, parameters);
    }
}
