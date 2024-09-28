using System;
using System.Collections.Generic;

/// <summary>
/// 
/// </summary>
public class GenericConditional : IConditional
{
    private readonly Func<Game, Dictionary<string, object>, bool> callback;
    private Dictionary<string, object> parameters;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="callback"></param>
    /// <param name="parameters"></param>
    public GenericConditional(Func<Game, Dictionary<string, object>, bool> callback, Dictionary<string, object> parameters)
    {
        this.callback = callback;
        this.parameters = parameters;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public bool IsMet(Game game)
    {
        return callback(game, parameters);
    }
}
