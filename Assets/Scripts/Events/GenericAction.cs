using System;
using DependencyInjectionExtensions;

/// <summary>
/// Represents a generic action that can be executed on a game.
/// </summary>
public class GenericAction : GameAction
{
    private readonly Action<IServiceLocator, SerializableDictionary<string, object>> callback;
    public SerializableDictionary<string, object> parameters;

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public GenericAction() { }

    /// <summary>
    /// Initializes a new instance of the GenericAction class.
    /// </summary>
    /// 
    /// <param name="parameters">The parameters required for the condition.</param>
    public GenericAction(Action<IServiceLocator, SerializableDictionary<string, object>> callback, SerializableDictionary<string, object> parameters)
    {
        this.callback = callback;
        this.parameters = parameters;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="locator"></param>
    public override void Execute(IServiceLocator locator)
    {
        callback.Invoke(locator, parameters);
    }
}
